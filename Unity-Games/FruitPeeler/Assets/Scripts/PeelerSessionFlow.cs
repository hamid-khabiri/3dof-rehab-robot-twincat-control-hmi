using System;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────
//  PEELER SESSION FLOW (same pattern as Crazy Car's CrazyCarSessionFlow)
//  Main menu -> Settings -> Playing -> Result, plus the pause popup.
//  Owns the chosen fruit level and the speed level, and is the ONE place
//  that decides whether the patient is actively playing:
//
//      IsPlaying = (Playing && !paused)
//
//  TwinCATManager sends fruit ID 0 (no resistance) whenever IsPlaying is
//  false, so any popup or menu on screen means no resistive force.
//
//  Created automatically before the first scene starts, survives scene
//  loads, so no scene needs to be edited to use it.
// ─────────────────────────────────────────────────────────────────────────

public enum PeelerScreen { MainMenu, Settings, Playing, Result }

public class PeelerSessionFlow : MonoBehaviour
{
    public const int MinSpeedLevel = 1;
    public const int MaxSpeedLevel = 3;
    public const int NormalSpeedLevel = 2;

    // Peeler left/right speed as a share of the speed saved in each scene.
    private static readonly float[] SpeedFactors = { 0.7f, 1.0f, 1.4f };
    private static readonly string[] SpeedNames = { "Slow", "Normal", "Fast" };

    public static PeelerSessionFlow Instance { get; private set; }

    public event Action<PeelerScreen> OnScreenChanged;
    public event Action<bool> OnPausedChanged;

    public PeelerScreen CurrentScreen { get; private set; } = PeelerScreen.MainMenu;
    public int SelectedLevelIndex { get; private set; }
    public int SpeedLevel { get; private set; } = NormalSpeedLevel;
    public bool IsPaused { get; private set; }

    public bool IsPlaying => CurrentScreen == PeelerScreen.Playing && !IsPaused;
    public float SpeedFactor => SpeedFactors[SpeedLevel - 1];

    public static string SpeedName(int level) => SpeedNames[Mathf.Clamp(level, MinSpeedLevel, MaxSpeedLevel) - 1];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateSession()
    {
        if (Instance != null) return;

        // Keep playing (robot input, peeler, music) when the therapist clicks
        // the TwinCAT HMI on another monitor and the game window loses focus.
        Application.runInBackground = true;

#if !UNITY_EDITOR
        // Always borderless full screen at the monitor's native resolution (no title bar),
        // even if an earlier windowed run was remembered by Windows.
        Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
#endif

        GameObject session = new GameObject("PeelerSession");
        DontDestroyOnLoad(session);
        session.AddComponent<PeelerSessionFlow>();
        session.AddComponent<PeelerMenuUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }

    // ── Screens ──────────────────────────────────────────────────────

    public void ShowMainMenu()
    {
        LeaveGame();
        SetScreen(PeelerScreen.MainMenu);

        // Reload the current fruit at stage 1 so no half-peeled fruit stays behind the menu.
        GlobalLevelManager levelManager = GlobalLevelManager.Instance;
        if (levelManager != null) levelManager.LoadLevelPreview(levelManager.GetCurrentLevelIndex());
    }

    public void ChooseLevel(int levelIndex)
    {
        LeaveGame();
        SelectedLevelIndex = Mathf.Clamp(levelIndex, 0, Mathf.Max(0, GetLevelCount() - 1));
        SetScreen(PeelerScreen.Settings);

        // Show the chosen fruit behind the Settings popup.
        if (GlobalLevelManager.Instance != null) GlobalLevelManager.Instance.LoadLevelPreview(SelectedLevelIndex);
    }

    public void SetSpeedLevel(int level) => SpeedLevel = Mathf.Clamp(level, MinSpeedLevel, MaxSpeedLevel);

    public void StartGame()
    {
        Time.timeScale = 1f;
        IsPaused = false;

        // Playing is set before the scene loads, so the new scene's GameManager starts unlocked.
        SetScreen(PeelerScreen.Playing);

        if (GlobalLevelManager.Instance != null)
        {
            GlobalLevelManager.Instance.StartFromLevel(SelectedLevelIndex);
        }
        else if (GameManager.Instance != null)
        {
            // Scene opened directly in the editor without the MainMenu boot scene.
            GameManager.Instance.StartSublevel(0);
        }
    }

    public void PlayAgain() => StartGame();

    public bool HasNextLevel => SelectedLevelIndex + 1 < GetLevelCount();

    public void ChooseNextLevel()
    {
        if (HasNextLevel) ChooseLevel(SelectedLevelIndex + 1);
    }

    public void SetPaused(bool paused)
    {
        if (CurrentScreen != PeelerScreen.Playing || IsPaused == paused) return;

        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        OnPausedChanged?.Invoke(paused);
    }

    /// <summary>Called by GlobalLevelManager when the last stage of the fruit is peeled.</summary>
    public void ShowResult()
    {
        if (CurrentScreen != PeelerScreen.Playing) return;

        LeaveGame();
        SetScreen(PeelerScreen.Result);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayVictory();
    }

    public void QuitGame()
    {
        // TwinCATManager sends "no fruit / game finished" from OnApplicationQuit.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Level info for the menus ─────────────────────────────────────

    public int GetLevelCount()
    {
        return GlobalLevelManager.Instance != null ? GlobalLevelManager.Instance.GetLevelCount() : 1;
    }

    public string GetLevelDisplayName(int levelIndex)
    {
        return GlobalLevelManager.Instance != null
            ? GlobalLevelManager.Instance.GetLevelDisplayName(levelIndex)
            : "Fruit";
    }

    // ── Internals ────────────────────────────────────────────────────

    /// <summary>Stops a game in progress. The screen changes FIRST, so the PLC
    /// never briefly sees "playing" again on the way out.</summary>
    private void LeaveGame()
    {
        bool wasPaused = IsPaused;
        CurrentScreen = PeelerScreen.MainMenu;
        IsPaused = false;
        Time.timeScale = 1f;
        if (wasPaused) OnPausedChanged?.Invoke(false);
    }

    private void SetScreen(PeelerScreen screen)
    {
        CurrentScreen = screen;
        if (screen != PeelerScreen.Playing) IsPaused = false;
        OnScreenChanged?.Invoke(screen);
    }
}
