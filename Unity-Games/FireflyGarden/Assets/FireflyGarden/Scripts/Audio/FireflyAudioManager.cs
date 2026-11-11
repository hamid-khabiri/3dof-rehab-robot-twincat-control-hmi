using FireflyGarden.Core;
using FireflyGarden.Session;
using UnityEngine;

namespace FireflyGarden.Audio
{
    /// <summary>
    /// Game sound (clips from Resources/FireflyAudio):
    /// - BackgroundMusic loops only while a level is being played. It pauses under the pause pop-up
    ///   (and resumes from the same spot) and stops on the menu, between levels, and on the results pop-up.
    /// - CorrectFlower when a target is reached, WrongFlower when a wrong flower is selected,
    ///   Victory when a level is finished.
    /// While an effect plays, the music dips so the effect is clearly heard.
    /// Created automatically at startup and kept across scenes.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FireflyAudioManager : MonoBehaviour
    {
        private const string ResourceFolder = "FireflyAudio/";

        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 1f;
        [Tooltip("Music level while an effect plays, as a fraction of the music volume.")]
        [SerializeField, Range(0f, 1f)] private float musicDuck = 0.5f;
        [Header("Runtime Debugging")]
        [SerializeField] private bool gameplayActive;

        private AudioSource musicSource;
        private AudioSource effectsSource;
        private AudioClip backgroundMusic;
        private AudioClip correctFlower;
        private AudioClip wrongFlower;
        private AudioClip victory;
        private bool musicStarted;
        private bool musicPaused;

        public static FireflyAudioManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateAtStartup()
        {
            if (Instance != null) return;
            var audioObject = new GameObject("FireflyAudioManager");
            DontDestroyOnLoad(audioObject);
            audioObject.AddComponent<FireflyAudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            effectsSource = gameObject.AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.loop = false;

            backgroundMusic = Load("BackgroundMusic");
            correctFlower = Load("CorrectFlower");
            wrongFlower = Load("WrongFlower");
            victory = Load("Victory");
            musicSource.clip = backgroundMusic;
            musicSource.volume = musicVolume;
            // Short effects are loaded up front so the first one is not late.
            Preload(correctFlower, wrongFlower, victory);
        }

        private void OnEnable()
        {
            GameplayEvents.CorrectFlowerReached += PlayCorrectFlower;
            GameplayEvents.WrongFlowerSelected += PlayWrongFlower;
            GameplayEvents.LevelCompleted += PlayVictory;
        }

        private void OnDisable()
        {
            GameplayEvents.CorrectFlowerReached -= PlayCorrectFlower;
            GameplayEvents.WrongFlowerSelected -= PlayWrongFlower;
            GameplayEvents.LevelCompleted -= PlayVictory;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            bool levelRunning = GameplayStatus.IsLevelRunning;
            bool paused = GameplayStatus.IsPaused;
            gameplayActive = levelRunning && !paused;
            UpdateMusic(levelRunning, paused);
        }

        private void UpdateMusic(bool levelRunning, bool paused)
        {
            if (musicSource.clip == null) return;

            if (!levelRunning)
            {
                if (musicStarted) StopMusic();
            }
            else if (!musicStarted)
            {
                musicSource.volume = musicVolume;
                musicSource.Play();
                musicStarted = true;
                musicPaused = false;
            }

            // Pause pop-up: hold the music where it is and resume from the same spot.
            if (musicStarted && paused && !musicPaused) { musicSource.Pause(); musicPaused = true; }
            else if (musicStarted && !paused && musicPaused) { musicSource.UnPause(); musicPaused = false; }

            float target = musicVolume * (effectsSource.isPlaying ? musicDuck : 1f);
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * 2f);
        }

        private void StopMusic()
        {
            musicSource.Stop();
            musicStarted = false;
            musicPaused = false;
        }

        private void PlayCorrectFlower() => PlayEffect(correctFlower);
        private void PlayWrongFlower() => PlayEffect(wrongFlower);

        private void PlayVictory()
        {
            StopMusic();
            PlayEffect(victory);
        }

        private void PlayEffect(AudioClip clip)
        {
            if (clip == null) return;
            effectsSource.PlayOneShot(clip, effectsVolume);
        }

        private static AudioClip Load(string clipName)
        {
            AudioClip clip = Resources.Load<AudioClip>(ResourceFolder + clipName);
            if (clip == null) Debug.LogWarning("Firefly Garden audio clip missing: Resources/" + ResourceFolder + clipName);
            return clip;
        }

        private static void Preload(params AudioClip[] clips)
        {
            foreach (AudioClip clip in clips)
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        }
    }
}
