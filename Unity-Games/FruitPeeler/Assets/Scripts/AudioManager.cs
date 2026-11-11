using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────
//  AUDIO (same rules as Crazy Car's CrazyCarAudio)
//  - Background music loops ONLY while playing: stopped in menus and on the
//    result popup, paused (and resumed from the same spot) under the pause
//    popup. musicStarted / musicPaused are tracked here rather than read
//    from AudioSource.isPlaying, which reads false while a clip is loading.
//  - Cut: when the peeler starts cutting a fruit, the fruit level's cut clip
//    plays for cutClipSeconds, once for each fruit (3 fruits = 3 times).
//    Kiwi = Cut_1, Orange = Cut_2, Grapefruit = Cut_3, Melon = Cut_4, ...
//  - Stage peeled = winClip, stage failed = lostClip, fruit completed
//    (result popup) = victoryClip.
//  - While an effect plays the music dips to musicDuck so it is heard.
//  Empty clip fields are loaded from Assets/Resources/PeelerAudio/.
// ─────────────────────────────────────────────────────────────────────────

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string ResourceFolder = "PeelerAudio/";

    [Header("── Audio Sources ──")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("── Audio Clips ──")]
    [SerializeField] private AudioClip peelClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip failClip;

    [Header("── Background Music ──")]
    [Tooltip("Old background music. Used only if Resources/PeelerAudio/BackgroundMusic is missing.")]
    [SerializeField] private AudioClip backgroundMusicClip;
    [SerializeField, Range(0f, 1f)] private float backgroundMusicVolume = 0.15f;
    [SerializeField] private bool enableBackgroundMusic = true;
    [Tooltip("Music level while an effect is playing, as a fraction of the music volume.")]
    [SerializeField, Range(0f, 1f)] private float musicDuck = 0.6f;

    [Header("── Session Clips (empty = loaded from Resources/PeelerAudio) ──")]
    [SerializeField] private AudioClip sessionMusicClip;
    [Tooltip("One cut clip per fruit level: element 0 = Kiwi (level 1), element 1 = Orange (level 2), ...")]
    [SerializeField] private AudioClip[] cutClips = new AudioClip[0];
    [SerializeField] private float cutClipSeconds = 1f;
    [SerializeField, Range(0f, 1f)] private float cutVolume = 1f;
    [SerializeField] private AudioClip victoryClip;
    [SerializeField] private AudioClip lostClip;

    private AudioSource cutSource;
    private bool musicStarted;
    private bool musicPaused;
    private readonly Queue<AudioClip> pendingCuts = new Queue<AudioClip>();
    private Coroutine cutRoutine;

    void Awake()
    {
        // Singleton pattern configuration
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keeps audio alive between scenes
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
        if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
        cutSource = gameObject.AddComponent<AudioSource>();
        cutSource.playOnAwake = false;

        LoadMissingClips();
        ConfigureBackgroundMusic();
    }

    void Update()
    {
        UpdateMusic();
    }

    private void LoadMissingClips()
    {
        if (sessionMusicClip == null) sessionMusicClip = Resources.Load<AudioClip>(ResourceFolder + "BackgroundMusic");
        if (victoryClip == null) victoryClip = Resources.Load<AudioClip>(ResourceFolder + "Victory");
        if (lostClip == null) lostClip = Resources.Load<AudioClip>(ResourceFolder + "Lost");

        if (cutClips == null || cutClips.Length == 0)
        {
            List<AudioClip> loaded = new List<AudioClip>();
            for (int i = 1; ; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>(ResourceFolder + "Cut_" + i);
                if (clip == null) break;
                loaded.Add(clip);
            }
            cutClips = loaded.ToArray();
        }

        // Short clips are loaded up front so the first one is not late.
        Preload(winClip, failClip, victoryClip, lostClip);
        Preload(cutClips);
    }

    private void ConfigureBackgroundMusic()
    {
        if (musicSource == null) return;

        musicSource.clip = sessionMusicClip != null ? sessionMusicClip : backgroundMusicClip;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = backgroundMusicVolume;
    }

    // ── Background music ─────────────────────────────────────────────

    private void UpdateMusic()
    {
        if (musicSource == null) return;

        PeelerSessionFlow flow = PeelerSessionFlow.Instance;
        bool inGame = flow == null || flow.CurrentScreen == PeelerScreen.Playing;
        bool paused = flow != null && flow.IsPaused;

        if (!inGame || !enableBackgroundMusic || musicSource.clip == null)
        {
            if (musicStarted) StopMusic();
        }
        else if (!musicStarted)
        {
            musicSource.volume = backgroundMusicVolume;
            musicSource.Play();
            musicStarted = true;
            musicPaused = false;
        }

        // Pause popup: hold the music where it is, resume from the same spot.
        if (musicStarted && paused && !musicPaused) { musicSource.Pause(); musicPaused = true; }
        else if (musicStarted && !paused && musicPaused) { musicSource.UnPause(); musicPaused = false; }

        bool effectPlaying = (sfxSource != null && sfxSource.isPlaying) || cutSource.isPlaying;
        float target = backgroundMusicVolume * (effectPlaying ? musicDuck : 1f);
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * 2f);
    }

    private void StopMusic()
    {
        musicSource.Stop();
        musicStarted = false;
        musicPaused = false;

        // Leaving the game also drops cut sounds that are still waiting.
        pendingCuts.Clear();
    }

    // ── Effects ──────────────────────────────────────────────────────

    // Method for the peeling sound (Loops while the player is ascending)
    public void PlayPeel()
    {
        if (sfxSource != null && peelClip != null && !sfxSource.isPlaying)
        {
            sfxSource.clip = peelClip;
            sfxSource.loop = true;
            sfxSource.Play();
        }
    }

    public void StopPeel()
    {
        // Only stops audio if the current clip is the peel clip
        if (sfxSource != null && sfxSource.clip == peelClip && sfxSource.isPlaying)
        {
            sfxSource.Stop();
        }
    }

    /// <summary>Plays the cut clip of this fruit level (1 = Kiwi) for cutClipSeconds.
    /// Calls that arrive while one is playing wait their turn, so every fruit is heard.</summary>
    public void PlayCut(int fruitLevelId)
    {
        if (cutClips == null || cutClips.Length == 0) return;

        AudioClip clip = cutClips[Mathf.Clamp(fruitLevelId - 1, 0, cutClips.Length - 1)];
        if (clip == null) return;

        pendingCuts.Enqueue(clip);
        if (cutRoutine == null) cutRoutine = StartCoroutine(PlayCutQueue());
    }

    private IEnumerator PlayCutQueue()
    {
        while (pendingCuts.Count > 0)
        {
            AudioClip clip = pendingCuts.Dequeue();
            cutSource.clip = clip;
            cutSource.volume = cutVolume;
            cutSource.Play();

            yield return new WaitForSecondsRealtime(Mathf.Min(cutClipSeconds, clip.length));
            cutSource.Stop();
        }

        cutRoutine = null;
    }

    // Stage peeled
    public void PlayWin()
    {
        PlayOneShot(winClip);
    }

    // Stage failed (out of steps)
    public void PlayFail()
    {
        PlayOneShot(lostClip != null ? lostClip : failClip);
    }

    // Fruit completed (result popup)
    public void PlayVictory()
    {
        PlayOneShot(victoryClip != null ? victoryClip : winClip);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (sfxSource == null || clip == null) return;
        if (sfxSource.clip == peelClip) sfxSource.Stop();
        sfxSource.loop = false;
        sfxSource.clip = clip;
        sfxSource.Play();
    }

    private static void Preload(params AudioClip[] clips)
    {
        if (clips == null) return;
        foreach (AudioClip clip in clips)
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
    }
}
