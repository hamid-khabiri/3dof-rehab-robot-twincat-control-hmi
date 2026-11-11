using System;
using UnityEngine;

public enum SessionState { MainMenu, SubmodeMenu, Gameplay, Summary }

public sealed class SessionFlowController : MonoBehaviour
{
    [SerializeField] private TrialManager trialManager;
    [SerializeField] private BinHighlightController binHighlightController;
    [SerializeField] private MainGameModeSetup[] mainModeSetups;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    [SerializeField] private Sprite threeBinsBackground;
    [SerializeField] private Sprite fiveBinsBackground;
    [SerializeField] private TwincatConnectorSorter twincatConnector;

    public event Action<SessionState> OnStateChanged;
    public event Action<BlockSummary> OnSummaryReady;
    public SessionState CurrentState { get; private set; }
    public MainGameMode CurrentMainMode { get; private set; } = MainGameMode.ThreeBins;
    public GameMode CurrentMode { get; private set; }
    public int CurrentWeightLevel { get; private set; } = SettingLevels.MinWeight;
    public bool IsPaused { get; private set; }
    // Sent to TwinCAT: 0 = not playing (menus, pause, summary), 1..3 = playing with that bin weight.
    public int GameStatus => CurrentState == SessionState.Gameplay && !IsPaused ? CurrentWeightLevel : 0;
    public int CurrentTrial => trialManager != null ? trialManager.CurrentTrial : 0;
    public int TotalTrials => trialManager != null ? trialManager.TotalTrials : 0;

    private void Awake()
    {
        if (trialManager == null)
        {
            Debug.LogError("SessionFlowController requires a TrialManager reference.", this);
            enabled = false;
            return;
        }

        ConfigureMainMode(MainGameMode.ThreeBins);
    }

    private void OnEnable() { if (trialManager != null) trialManager.OnBlockCompleted += HandleBlockCompleted; }
    private void Start() { ShowMainMenu(); }
    private void OnDisable() { if (trialManager != null) trialManager.OnBlockCompleted -= HandleBlockCompleted; }

    public void ShowMainMenu() { trialManager.ResetBlock(); SetState(SessionState.MainMenu); }
    public void ShowThreeBinsSubmodeMenu()
    {
        CurrentMainMode = MainGameMode.ThreeBins;
        SetState(SessionState.SubmodeMenu);
    }
    public void ShowFiveBinsSubmodeMenu()
    {
        CurrentMainMode = MainGameMode.FiveBins;
        SetState(SessionState.SubmodeMenu);
    }
    public void StartGuidedMode() { StartMode(GameMode.Guided); }
    public void StartCategorySortingMode() { StartMode(GameMode.CategorySorting); }
    public void RestartCurrentMode()
    {
        StartMode(CurrentMode);
    }
    public void ReturnToMenu() { ShowMainMenu(); }
    public void SetSpeedLevel(int level) { trialManager.SetSpeedLevel(level); }
    public void SetWeightLevel(int level)
    {
        CurrentWeightLevel = Mathf.Clamp(level, SettingLevels.MinWeight, SettingLevels.MaxWeight);
        PublishGameStatus();
    }
    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        PublishGameStatus();
    }
    public void QuitGame()
    {
        // TwincatConnectorSorter reports the closed game to the PLC from OnApplicationQuit.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void StartMode(GameMode mode)
    {
        if (!ConfigureMainMode(CurrentMainMode)) return;
        trialManager.ResetBlock();
        CurrentMode = mode;
        SetState(SessionState.Gameplay);
        if (!trialManager.StartBlock(mode)) Debug.LogError("Unable to start trial block.", trialManager);
    }

    private bool ConfigureMainMode(MainGameMode mode)
    {
        MainGameModeSetup selectedSetup = null;
        if (mainModeSetups != null)
        {
            foreach (MainGameModeSetup setup in mainModeSetups)
            {
                bool isSelected = setup != null && setup.MainGameMode == mode;
                if (setup != null && setup.SetupRoot != null) setup.SetupRoot.SetActive(isSelected);
                if (isSelected) selectedSetup = setup;
            }
        }

        if (selectedSetup == null)
        {
            Debug.LogError($"No setup is configured for {mode}.", this);
            return false;
        }

        if (backgroundRenderer != null)
            backgroundRenderer.sprite = mode == MainGameMode.FiveBins ? fiveBinsBackground : threeBinsBackground;

        TrolleyController trolley = selectedSetup.TrolleyController;
        if (trolley != null)
        {
            trolley.numberOfBins = selectedSetup.BinTargets.Count;
            if (twincatConnector != null) twincatConnector.trolleyController = trolley;
        }

        if (!trialManager.Configure(selectedSetup.ProductDefinitions, selectedSetup.BinTargets)) return false;
        if (binHighlightController != null) binHighlightController.SetActiveTargets(selectedSetup.BinTargets);
        return true;
    }
    private void HandleBlockCompleted(BlockSummary summary) { SetState(SessionState.Summary); OnSummaryReady?.Invoke(summary); }
    private void SetState(SessionState state)
    {
        CurrentState = state;
        if (state != SessionState.Gameplay) IsPaused = false;
        OnStateChanged?.Invoke(state);
        PublishGameStatus();
    }
    private void PublishGameStatus() { if (twincatConnector != null) twincatConnector.SetGameStatus(GameStatus); }
#if UNITY_EDITOR
    public void EditorConfigure(TrialManager trials) { trialManager = trials; }
#endif

}
