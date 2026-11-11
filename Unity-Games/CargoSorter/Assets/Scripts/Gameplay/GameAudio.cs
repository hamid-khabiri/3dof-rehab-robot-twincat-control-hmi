using UnityEngine;

public sealed class GameAudio : MonoBehaviour
{
    [SerializeField] private SessionFlowController flow;
    [SerializeField] private TrialManager trials;

    [Header("Clips")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private AudioClip correctBin;
    [SerializeField] private AudioClip wrongBin;
    [SerializeField] private AudioClip victory;
    [SerializeField] private AudioClip lost;

    [Header("Mix")]
    // Above 1 the music is amplified beyond the clip's own level (1.5 = +3.5 dB, 2 = +6 dB).
    [SerializeField, Range(0f, 3f)] private float musicLoudness = 1.35f;
    [SerializeField, Range(0f, 1f)] private float effectsVolume = 1f;
    // Music is lowered to this fraction while a correct/wrong/victory/lost sound plays, so the effect is always heard.
    [SerializeField, Range(0f, 1f)] private float musicDuringEffects = 0.6f;

    [Header("Per Sound")]
    [SerializeField, Range(0f, 1f)] private float correctBinVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float wrongBinVolume = 0.56f;
    // 1 = original pitch; higher = brighter. Pitch also changes the sound's length.
    [SerializeField, Range(0.5f, 2f)] private float correctBinPitch = 1.25f;
    [SerializeField, Range(0.5f, 2f)] private float wrongBinPitch = 0.8f;

    // Accuracy (correct / completed trials) needed at the end of a block to play Victory instead of Lost.
    [SerializeField, Range(0f, 100f)] private float winAccuracyPercent = 70f;

    private AudioSource musicSource;
    private MusicGain musicGain;
    // One source per sound so each can have its own pitch.
    private AudioSource correctSource;
    private AudioSource wrongSource;
    private AudioSource resultSource;
    // Tracked here rather than read from AudioSource.isPlaying, which stays false while a clip is still loading.
    private bool musicStarted;
    private bool musicPaused;
    private float duckUntil;

    private void Awake()
    {
        // Music lives on its own child object so the gain filter only affects the music.
        var musicObject = new GameObject("Music");
        musicObject.transform.SetParent(transform, false);
        musicSource = musicObject.AddComponent<AudioSource>();
        musicSource.clip = backgroundMusic;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicGain = musicObject.AddComponent<MusicGain>();

        correctSource = CreateEffectSource();
        wrongSource = CreateEffectSource();
        resultSource = CreateEffectSource();

        // Load up front so the first correct/wrong sound is not delayed.
        foreach (AudioClip clip in new[] { backgroundMusic, correctBin, wrongBin, victory, lost })
            if (clip != null) clip.LoadAudioData();
    }

    private void OnEnable()
    {
        if (trials != null) trials.OnTrialCompleted += HandleTrialCompleted;
        if (flow != null) flow.OnSummaryReady += HandleSummaryReady;
    }

    private void OnDisable()
    {
        if (trials != null) trials.OnTrialCompleted -= HandleTrialCompleted;
        if (flow != null) flow.OnSummaryReady -= HandleSummaryReady;
    }

    // Music only while the patient is actually playing: stopped in menus/summary, paused (not restarted) during pause.
    private void Update()
    {
        if (flow == null || musicSource.clip == null) return;
        bool ducked = Time.unscaledTime < duckUntil;
        float loudness = musicLoudness * (ducked ? musicDuringEffects : 1f);
        musicSource.volume = Mathf.Min(loudness, 1f);
        musicGain.Gain = Mathf.Max(loudness, 1f);

        bool inGameplay = flow.CurrentState == SessionState.Gameplay;
        if (!inGameplay)
        {
            if (musicStarted) musicSource.Stop();
            musicStarted = musicPaused = false;
        }
        else if (flow.IsPaused)
        {
            if (musicStarted && !musicPaused) { musicSource.Pause(); musicPaused = true; }
        }
        else if (!musicStarted)
        {
            musicSource.Play();
            musicStarted = true;
            musicPaused = false;
        }
        else if (musicPaused)
        {
            musicSource.UnPause();
            musicPaused = false;
        }
    }

    private void HandleTrialCompleted(TrialOutcome outcome, BinTarget selectedTarget)
    {
        if (outcome == TrialOutcome.Correct) PlayOnce(correctSource, correctBin, correctBinVolume, correctBinPitch);
        else if (outcome == TrialOutcome.WrongBin) PlayOnce(wrongSource, wrongBin, wrongBinVolume, wrongBinPitch);
    }

    private void HandleSummaryReady(BlockSummary summary)
    {
        PlayOnce(resultSource, summary.Accuracy >= winAccuracyPercent ? victory : lost, 1f, 1f);
    }

    private void PlayOnce(AudioSource source, AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume * effectsVolume);
        duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + clip.length / pitch);
    }

    private AudioSource CreateEffectSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return source;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        SessionFlowController sessionFlow,
        TrialManager trialManager,
        AudioClip music,
        AudioClip correct,
        AudioClip wrong,
        AudioClip win,
        AudioClip lose)
    {
        flow = sessionFlow;
        trials = trialManager;
        backgroundMusic = music;
        correctBin = correct;
        wrongBin = wrong;
        victory = win;
        lost = lose;
    }
#endif
}
