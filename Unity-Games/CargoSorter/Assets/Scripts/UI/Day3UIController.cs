using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Day3UIController : MonoBehaviour
{
    [SerializeField] private SessionFlowController flow;
    [SerializeField] private TrialManager trials;
    [SerializeField] private BinHighlightController highlights;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject submodeMenuPanel;
    [SerializeField] private GameObject gameplayPanel;
    [SerializeField] private GameObject summaryPanel;
    [SerializeField] private TextMeshProUGUI trialProgress;
    [SerializeField] private TextMeshProUGUI summaryResults;
    [SerializeField] private Button guidedButton;
    [SerializeField] private Button categoryButton;
    [SerializeField] private Button threeBinsButton;
    [SerializeField] private Button fiveBinsButton;
    [SerializeField] private TextMeshProUGUI selectedMainModeTitle;
    [SerializeField] private Button submodeBackButton;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button returnButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private GameObject pauseOverlay;
    [SerializeField] private TrialProgressDotsUI trialProgressDots;
    [SerializeField] private SpeedIndicatorUI speedIndicator;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseReturnToMenuButton;
    [SerializeField] private Slider speedSlider;
    [SerializeField] private TextMeshProUGUI speedValueText;
    [SerializeField] private Slider weightSlider;
    [SerializeField] private TextMeshProUGUI weightValueText;
    [SerializeField] private WeightIndicatorUI weightIndicator;
    [SerializeField] private Button exitButton;

    private bool isPaused;
    private float previousTimeScale = 1f;

    private void OnEnable()
    {
        flow.OnStateChanged += ShowState;
        flow.OnSummaryReady += ShowSummary;
        trials.OnTrialStarted += RefreshTrial;
        trials.OnGuidedTargetChanged += highlights.SetGuidedTarget;
        trials.OnTrialCompleted += HandleTrialCompleted;
        trials.OnTrialProgressChanged += RefreshTrialDots;
        threeBinsButton.onClick.AddListener(flow.ShowThreeBinsSubmodeMenu);
        fiveBinsButton.onClick.AddListener(flow.ShowFiveBinsSubmodeMenu);
        guidedButton.onClick.AddListener(flow.StartGuidedMode);
        categoryButton.onClick.AddListener(flow.StartCategorySortingMode);
        submodeBackButton.onClick.AddListener(flow.ShowMainMenu);
        playAgainButton.onClick.AddListener(flow.RestartCurrentMode);
        returnButton.onClick.AddListener(flow.ReturnToMenu);
        pauseButton.onClick.AddListener(Pause);
        stopButton.onClick.AddListener(Stop);
        resumeButton.onClick.AddListener(Resume);
        pauseReturnToMenuButton.onClick.AddListener(Stop);
        if (speedSlider == null || weightSlider == null)
            Debug.LogWarning("Speed / Bin Weight settings are not in the scene yet. Run Tools > Cargo Sorter > Setup Day 3 UI, then save the scene.", this);
        if (speedSlider != null) speedSlider.onValueChanged.AddListener(HandleSpeedChanged);
        if (weightSlider != null) weightSlider.onValueChanged.AddListener(HandleWeightChanged);
        if (exitButton != null) exitButton.onClick.AddListener(flow.QuitGame);
        pauseOverlay.SetActive(false);
    }

    private void OnDisable()
    {
        if (flow != null) { flow.OnStateChanged -= ShowState; flow.OnSummaryReady -= ShowSummary; }
        if (trials != null) { trials.OnTrialStarted -= RefreshTrial; trials.OnGuidedTargetChanged -= highlights.SetGuidedTarget; trials.OnTrialCompleted -= HandleTrialCompleted; trials.OnTrialProgressChanged -= RefreshTrialDots; }
        threeBinsButton.onClick.RemoveListener(flow.ShowThreeBinsSubmodeMenu);
        fiveBinsButton.onClick.RemoveListener(flow.ShowFiveBinsSubmodeMenu);
        guidedButton.onClick.RemoveListener(flow.StartGuidedMode);
        categoryButton.onClick.RemoveListener(flow.StartCategorySortingMode);
        submodeBackButton.onClick.RemoveListener(flow.ShowMainMenu);
        playAgainButton.onClick.RemoveListener(flow.RestartCurrentMode);
        returnButton.onClick.RemoveListener(flow.ReturnToMenu);
        pauseButton.onClick.RemoveListener(Pause);
        stopButton.onClick.RemoveListener(Stop);
        resumeButton.onClick.RemoveListener(Resume);
        pauseReturnToMenuButton.onClick.RemoveListener(Stop);
        if (speedSlider != null) speedSlider.onValueChanged.RemoveListener(HandleSpeedChanged);
        if (weightSlider != null) weightSlider.onValueChanged.RemoveListener(HandleWeightChanged);
        if (exitButton != null) exitButton.onClick.RemoveListener(flow.QuitGame);
        RestoreTimeScale();
    }

    private void ShowState(SessionState state)
    {
        if (state != SessionState.Gameplay) RestoreTimeScale();
        if (state != SessionState.Gameplay && highlights != null) highlights.ClearGuidedTarget();
        mainMenuPanel.SetActive(state == SessionState.MainMenu);
        submodeMenuPanel.SetActive(state == SessionState.SubmodeMenu);
        gameplayPanel.SetActive(state == SessionState.Gameplay);
        summaryPanel.SetActive(state == SessionState.Summary);
        if (state == SessionState.SubmodeMenu && selectedMainModeTitle != null)
            selectedMainModeTitle.text = flow.CurrentMainMode == MainGameMode.FiveBins ? "5 Bins" : "3 Bins";
        if (state == SessionState.SubmodeMenu) RefreshSettings();
        if (state == SessionState.Gameplay)
        {
            if (speedIndicator != null) speedIndicator.SetLevel(trials.CurrentSpeedLevel);
            if (weightIndicator != null) weightIndicator.SetLevel(flow.CurrentWeightLevel);
        }
    }

    private void RefreshSettings()
    {
        if (speedSlider != null) speedSlider.SetValueWithoutNotify(trials.CurrentSpeedLevel);
        if (weightSlider != null) weightSlider.SetValueWithoutNotify(flow.CurrentWeightLevel);
        if (speedValueText != null) speedValueText.text = $"{trials.CurrentSpeedLevel}  ·  {SettingLevels.SpeedName(trials.CurrentSpeedLevel)}";
        if (weightValueText != null) weightValueText.text = $"{flow.CurrentWeightLevel}  ·  {SettingLevels.WeightName(flow.CurrentWeightLevel)}";
    }

    private void HandleSpeedChanged(float value)
    {
        flow.SetSpeedLevel(Mathf.RoundToInt(value));
        RefreshSettings();
    }

    private void HandleWeightChanged(float value)
    {
        flow.SetWeightLevel(Mathf.RoundToInt(value));
        RefreshSettings();
    }

    private void Pause()
    {
        if (isPaused || flow.CurrentState != SessionState.Gameplay) return;
        previousTimeScale = Time.timeScale;
        isPaused = true;
        Time.timeScale = 0f;
        pauseOverlay.SetActive(true);
        flow.SetPaused(true);
    }

    private void Resume()
    {
        if (!isPaused) return;
        RestoreTimeScale();
    }

    private void Stop()
    {
        // Leave gameplay first so TwinCAT goes straight to status 0 instead of briefly seeing "unpaused".
        flow.ReturnToMenu();
        RestoreTimeScale();
    }

    private void RestoreTimeScale()
    {
        if (isPaused) Time.timeScale = previousTimeScale;
        bool wasPaused = isPaused;
        isPaused = false;
        if (pauseOverlay != null) pauseOverlay.SetActive(false);
        if (wasPaused && flow != null) flow.SetPaused(false);
    }

    private void RefreshTrial(int current, int total) { trialProgress.text = $"<b>Trial</b> {current} / {total}"; }
    private void RefreshTrialDots(int completed, int total)
    {
        if (trialProgressDots != null) trialProgressDots.SetProgress(completed, total);
    }
    private void HandleTrialCompleted(TrialOutcome outcome, BinTarget selectedTarget)
    {
        if (trialProgressDots != null) trialProgressDots.RecordOutcome(outcome);
        highlights.ShowLandingFeedback(selectedTarget, outcome);
    }
    private void ShowSummary(BlockSummary value)
    {
        summaryResults.text = $"Correct Deliveries: {value.Correct}\nWrong Bin: {value.WrongBin}\nMisses: {value.Misses}\nAccuracy: {value.Accuracy:0}%";
    }
}
