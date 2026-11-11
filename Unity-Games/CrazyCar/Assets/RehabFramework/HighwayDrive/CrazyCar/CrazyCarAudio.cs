using UnityEngine;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  CRAZY CAR AUDIO (same rules as Cargo Sorter's GameAudio)
    //  - Background music loops ONLY while driving: stopped in menus and on the
    //    result screen, paused (and resumed from the same spot) under the pause
    //    popup. musicStarted / musicPaused are tracked here rather than read
    //    from AudioSource.isPlaying, which reads false while a streamed clip is
    //    still loading.
    //  - Every event sound plays once per event, through its own AudioSource so
    //    each has its own volume and pitch:
    //      horn   -- a car passes the player, HornChance of the time, random clip
    //      crash  -- contact with another vehicle, random clip
    //      gas    -- the patient pushes the gas (Y) up into a new GasStepPercent
    //                band (0->20, 20->40 ...), random clip, once per step. Holding
    //                still never replays it, and Y must drop GasHysteresisPercent
    //                below a band before that band can trigger again, so a
    //                trembling hand on a boundary stays silent.
    //      heart  -- a heart pickup is collected
    //      lost   -- the last heart is gone (result screen)
    //  - While an effect plays the music dips to MusicDuck so it is heard.
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class CrazyCarAudio : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private CrazyCarSessionFlow _flow;
        [SerializeField] private HighwayDriveController _controller;
        [SerializeField] private HighwayDriveTraffic _traffic;
        [SerializeField] private HighwayDriveInput _input;

        [Header("Clips")]
        [SerializeField] private AudioClip _music;
        [SerializeField] private AudioClip[] _horns = new AudioClip[0];
        [SerializeField] private AudioClip[] _crashes = new AudioClip[0];
        [SerializeField] private AudioClip[] _gasUps = new AudioClip[0];
        [SerializeField] private AudioClip _heart;
        [SerializeField] private AudioClip _lost;

        [Header("Music")]
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.45f;
        [Tooltip("Music level while an effect is playing, as a fraction of Music Volume.")]
        [SerializeField, Range(0f, 1f)] private float _musicDuck = 0.6f;

        [Header("Horns")]
        [Tooltip("Share of passed cars that honk (0.3 = 30 %).")]
        [SerializeField, Range(0f, 1f)] private float _hornChance = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _hornVolume = 0.55f;
        [Tooltip("Minimum seconds between two horns, so a burst of passes is not a wall of noise.")]
        [SerializeField] private float _hornMinGapSeconds = 1.2f;

        [Header("Gas sound")]
        [Tooltip("Size of one gas step, % of Y. A sound plays each time Y rises into the next step.")]
        [SerializeField, Range(5f, 50f)] private float _gasStepPercent = 20f;
        [Tooltip("How far Y must fall below a step before that step can play again, %. Ignores hand tremor.")]
        [SerializeField, Range(0f, 15f)] private float _gasHysteresisPercent = 5f;
        [SerializeField, Range(0f, 1f)] private float _gasUpVolume = 0.8f;

        [Header("Effect volume / pitch")]
        [SerializeField, Range(0f, 1f)] private float _crashVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _heartVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _lostVolume = 0.9f;
        [SerializeField, Range(0.5f, 2f)] private float _crashPitch = 1f;
        [SerializeField, Range(0.5f, 2f)] private float _heartPitch = 1f;

        private AudioSource _musicSource;
        private AudioSource _hornSource;
        private AudioSource _crashSource;
        private AudioSource _gasSource;
        private AudioSource _heartSource;
        private AudioSource _lostSource;
        private bool _musicStarted;
        private bool _musicPaused;
        private float _lastHornTime = -10f;
        private int _gasLevel;
        private int _lastGasClip = -1;

        private void Awake()
        {
            _musicSource = NewSource("Music", loop: true);
            _musicSource.clip = _music;
            _hornSource = NewSource("Horn");
            _crashSource = NewSource("Crash");
            _gasSource = NewSource("GasUp");
            _heartSource = NewSource("Heart");
            _lostSource = NewSource("Lost");

            // Short clips are loaded up front so the first one is not late.
            Preload(_horns);
            Preload(_crashes);
            Preload(_gasUps);
            Preload(_heart, _lost);

            if (_music == null)
                Debug.LogWarning("[CrazyCarAudio] No background music assigned. Run Tools > Crazy Car > Build " +
                                 "Crazy Car Scene, then save the scene.", this);
        }

        private void OnEnable()
        {
            if (_flow != null) { _flow.OnScreenChanged += HandleScreen; _flow.OnResult += HandleResult; }
            if (_controller != null)
            {
                _controller.OnCrash += HandleCrash;
                _controller.OnHeartCollected += HandleHeart;
            }
            if (_traffic != null) _traffic.OnCarPassed += HandleCarPassed;
        }

        private void OnDisable()
        {
            if (_flow != null) { _flow.OnScreenChanged -= HandleScreen; _flow.OnResult -= HandleResult; }
            if (_controller != null)
            {
                _controller.OnCrash -= HandleCrash;
                _controller.OnHeartCollected -= HandleHeart;
            }
            if (_traffic != null) _traffic.OnCarPassed -= HandleCarPassed;
        }

        private void Update()
        {
            if (_flow == null) return;

            // Pause popup: hold the music where it is, resume from the same spot.
            bool shouldPause = _flow.CurrentScreen == CrazyCarScreen.Playing && _flow.IsPaused;
            if (_musicStarted && shouldPause && !_musicPaused) { _musicSource.Pause(); _musicPaused = true; }
            else if (_musicStarted && !shouldPause && _musicPaused) { _musicSource.UnPause(); _musicPaused = false; }

            bool effectPlaying = _hornSource.isPlaying || _crashSource.isPlaying || _gasSource.isPlaying
                              || _heartSource.isPlaying;
            float target = _musicVolume * (effectPlaying ? _musicDuck : 1f);
            _musicSource.volume = Mathf.MoveTowards(_musicSource.volume, target, Time.unscaledDeltaTime * 2f);

            UpdateGasSteps();
        }

        /// <summary>One sound per new gas step. The step level follows Y down
        /// only once Y is GasHysteresisPercent below the step's floor, so a
        /// hand trembling around 20 % does not retrigger the 20 % step. Outside
        /// active driving the level is tracked silently, so a game never starts
        /// (or resumes after a crash) with a burst of sound.</summary>
        private void UpdateGasSteps()
        {
            if (_input == null || _gasStepPercent <= 0f) return;

            float gas = _input.GasPercent;
            int level = Mathf.FloorToInt(gas / _gasStepPercent);

            while (_gasLevel > 0 && gas < _gasLevel * _gasStepPercent - _gasHysteresisPercent) _gasLevel--;

            if (level <= _gasLevel) return;
            _gasLevel = level;
            if (IsDriving()) Play(_gasSource, PickDifferent(_gasUps, ref _lastGasClip), _gasUpVolume, 1f);
        }

        // ── Events ───────────────────────────────────────────────────────

        private void HandleScreen(CrazyCarScreen screen)
        {
            if (screen == CrazyCarScreen.Playing) StartMusic();
            else StopMusic();
        }

        private void HandleResult(float drivingSeconds)
        {
            StopMusic();
            Play(_lostSource, _lost, _lostVolume, 1f);
        }

        private void HandleCrash(Vector2 contact) => Play(_crashSource, Pick(_crashes), _crashVolume, _crashPitch);

        private void HandleHeart() => Play(_heartSource, _heart, _heartVolume, _heartPitch);

        private void HandleCarPassed()
        {
            if (!IsDriving()) return;
            if (Time.time - _lastHornTime < _hornMinGapSeconds) return;
            if (Random.value >= _hornChance) return;
            _lastHornTime = Time.time;
            Play(_hornSource, Pick(_horns), _hornVolume, 1f);
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private bool IsDriving()
            => _flow != null && _flow.CurrentScreen == CrazyCarScreen.Playing && !_flow.IsPaused
            && _controller != null && _controller.State == HighwayDriveState.Driving;

        private void StartMusic()
        {
            if (_music == null) return;
            if (_musicStarted && _musicPaused) { _musicSource.UnPause(); _musicPaused = false; return; }
            if (_musicStarted) return;
            _musicSource.volume = _musicVolume;
            _musicSource.Play();
            _musicStarted = true;
            _musicPaused = false;
        }

        private void StopMusic()
        {
            _musicSource.Stop();
            _musicStarted = false;
            _musicPaused = false;
        }

        private static void Play(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            if (source == null || clip == null) return;
            source.Stop();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }

        private static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }

        /// <summary>Random clip, but never the same one twice in a row.</summary>
        private static AudioClip PickDifferent(AudioClip[] clips, ref int lastIndex)
        {
            if (clips == null || clips.Length == 0) return null;
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastIndex) index = (index + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            lastIndex = index;
            return clips[index];
        }

        private AudioSource NewSource(string name, bool loop = false)
        {
            var go = new GameObject("Audio_" + name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private static void Preload(params AudioClip[] clips)
        {
            if (clips == null) return;
            foreach (AudioClip clip in clips)
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }
    }
}
