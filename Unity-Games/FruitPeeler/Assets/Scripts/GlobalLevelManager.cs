using UnityEngine;
using UnityEngine.SceneManagement;

public class GlobalLevelManager : MonoBehaviour
{
    #region Level Data / Configuration
    [System.Serializable]
    public class SublevelData
    {
        public string levelName;
        public int fruitCount;
        public int attempts;
    }

    [System.Serializable]
    public class LevelConfig
    {
        [Tooltip("Exact scene name in Build Settings. E.g., 'KiwiScreen', 'OrangeScreen'")]
        public string sceneName;

        [Tooltip("Fruit name shown in the menus. E.g., 'Kiwi'. Empty = derived from the scene name.")]
        public string displayName;

        [Tooltip("Sublevels for this fruit (Sublevel 1, 2, 3...)")]
        public SublevelData[] sublevels;
    }

    [Header("── Level Progression ──")]
    [SerializeField] private LevelConfig[] gameLevels;

    [Header("── Scorecard Debug ──")]
    [SerializeField] private bool enableScorecardDebugLogs = true;

    [Header("── Current Fruit Level Debug ──")]
    [SerializeField] private bool enableCurrentFruitLevelDebugLogs = true;

    private readonly SessionMetrics sessionMetrics = new SessionMetrics();
    public SessionMetrics SessionMetrics => sessionMetrics;
    #endregion

    #region Current Level State
    private int currentLevelIndex = 0;
    private int targetSublevelIndex = 0;

    public bool hasSeenIntro = false;

    private const string CompletionSceneName = "CompletionScreen";
    private const string MainMenuSceneName = "MainMenu";
    #endregion

    #region Unity Lifecycle / Singleton
    public static GlobalLevelManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Sync index with whatever scene is active at startup
            SyncLevelIndexWithActiveScene();

            // Instantly load the first configured game level if booting on MainMenu
            if (gameLevels != null && gameLevels.Length > 0 && SceneManager.GetActiveScene().name == MainMenuSceneName)
            {
                Debug.Log($"Booting game instantly. Loading first level: {gameLevels[0].sceneName}");
                SceneManager.LoadScene(gameLevels[0].sceneName);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SyncLevelIndexWithActiveScene();
    }

    /// <summary>
    /// Auto-detects level index based on the currently loaded scene name.
    /// Ensures fruit ID updates correctly even when testing directly in Unity Editor.
    /// </summary>
    private void SyncLevelIndexWithActiveScene()
    {
        if (gameLevels == null || gameLevels.Length == 0) return;

        string activeSceneName = SceneManager.GetActiveScene().name;

        for (int i = 0; i < gameLevels.Length; i++)
        {
            if (gameLevels[i] != null && gameLevels[i].sceneName == activeSceneName)
            {
                if (currentLevelIndex != i)
                {
                    currentLevelIndex = i;
                    LogCurrentFruitLevelDebug($"Auto-detected scene match: '{activeSceneName}'");
                }
                return;
            }
        }
    }
    #endregion

    #region Level Selection / Scene Loading
    // 1-Based Fruit ID (1 = Kiwi, 2 = Orange, 3 = Grapefruit, etc.)
    public int CurrentFruitLevelId => GetCurrentFruitLevelId();

    public int GetCurrentFruitLevelId()
    {
        if (gameLevels == null || gameLevels.Length == 0) return 1;
        if (currentLevelIndex < 0 || currentLevelIndex >= gameLevels.Length) return 1;

        return currentLevelIndex + 1; // 1-based fruit number for TwinCAT
    }

    public void SetFruitLevelById(int fruitNumber)
    {
        int targetIndex = fruitNumber - 1;
        if (targetIndex >= 0 && targetIndex < GetLevelCount())
        {
            StartFromLevel(targetIndex);
        }
        else
        {
            Debug.LogWarning($"[GlobalLevelManager] Fruit ID {fruitNumber} is out of bounds.");
        }
    }

    public int GetLevelCount()
    {
        return gameLevels != null ? gameLevels.Length : 0;
    }

    public string GetLevelSceneName(int levelIndex)
    {
        if (gameLevels == null || levelIndex < 0 || levelIndex >= gameLevels.Length) return string.Empty;
        return gameLevels[levelIndex].sceneName;
    }

    public string GetLevelDisplayName(int levelIndex)
    {
        if (gameLevels == null || levelIndex < 0 || levelIndex >= gameLevels.Length) return $"Level {levelIndex + 1}";

        LevelConfig level = gameLevels[levelIndex];
        if (!string.IsNullOrEmpty(level.displayName)) return level.displayName;
        if (string.IsNullOrEmpty(level.sceneName)) return $"Level {levelIndex + 1}";
        return level.sceneName.Replace("Level_", string.Empty).Replace("_", " ");
    }

    /// <summary>
    /// Loads a fruit scene at stage 1 without starting a session,
    /// so the menus can show that fruit in the background.
    /// </summary>
    public void LoadLevelPreview(int levelIndex)
    {
        if (gameLevels == null || levelIndex < 0 || levelIndex >= gameLevels.Length) return;

        currentLevelIndex = levelIndex;
        targetSublevelIndex = 0;
        hasSeenIntro = true;
        ClearScorecardSession();

        SceneManager.LoadScene(gameLevels[levelIndex].sceneName);
    }

    public void StartFromLevel(int levelIndex)
    {
        if (gameLevels == null || gameLevels.Length == 0)
        {
            Debug.LogWarning("Cannot start selected level: no game levels are configured.");
            return;
        }

        if (levelIndex < 0 || levelIndex >= gameLevels.Length)
        {
            Debug.LogWarning($"Cannot start selected level: index {levelIndex} is out of range.");
            return;
        }

        currentLevelIndex = levelIndex;
        targetSublevelIndex = 0;
        hasSeenIntro = true;

        BeginScorecardSession(levelIndex);
        LogScorecardDebug("Session started from level selection");
        LogCurrentFruitLevelDebug("Level started through StartFromLevel");

        Debug.Log($"Starting selected level: {gameLevels[currentLevelIndex].sceneName} (Fruit ID: {CurrentFruitLevelId})");
        SceneManager.LoadScene(gameLevels[currentLevelIndex].sceneName);
    }

    public void ResetCurrentSublevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    #endregion

    #region Sublevel Progression
    public int GetTargetSublevel()
    {
        return targetSublevelIndex;
    }

    public SublevelData GetCurrentSublevelData()
    {
        if (gameLevels == null || gameLevels.Length == 0) return null;
        if (currentLevelIndex >= gameLevels.Length) return null;

        SublevelData[] sublevels = gameLevels[currentLevelIndex].sublevels;
        if (sublevels == null || sublevels.Length == 0) return null;
        if (targetSublevelIndex >= sublevels.Length) return null;

        return sublevels[targetSublevelIndex];
    }

    public SublevelData GetSublevelData(int sublevelIndex)
    {
        if (gameLevels == null || gameLevels.Length == 0) return null;
        if (currentLevelIndex >= gameLevels.Length) return null;

        SublevelData[] sublevels = gameLevels[currentLevelIndex].sublevels;
        if (sublevels == null) return null;
        if (sublevelIndex < 0 || sublevelIndex >= sublevels.Length) return null;

        return sublevels[sublevelIndex];
    }

    public int GetSublevelCountForCurrentLevel()
    {
        if (gameLevels == null || gameLevels.Length == 0) return 0;
        if (currentLevelIndex >= gameLevels.Length) return 0;

        SublevelData[] sublevels = gameLevels[currentLevelIndex].sublevels;
        return sublevels != null ? sublevels.Length : 0;
    }

    public void AdvanceNextStep(int maxSublevelsInCurrentFruit = 0)
    {
        targetSublevelIndex++;

        int realMax = maxSublevelsInCurrentFruit > 0
            ? maxSublevelsInCurrentFruit
            : GetSublevelCountForCurrentLevel();

        if (targetSublevelIndex >= realMax)
        {
            // Fruit completed: stay on this fruit and show the result popup.
            // The patient chooses Play Again / Next Fruit / Main Menu there.
            targetSublevelIndex = 0;
            UpdateScorecardFinalLevelReached(currentLevelIndex);
            EndScorecardSession();
            LogCurrentFruitLevelDebug("Fruit level completed");

            if (PeelerSessionFlow.Instance != null) PeelerSessionFlow.Instance.ShowResult();
        }
        else
        {
            GameManager localManager = FindAnyObjectByType<GameManager>();
            if (localManager != null) localManager.StartSublevel(targetSublevelIndex);
        }
    }

    public int GetCurrentLevelIndex()
    {
        return currentLevelIndex;
    }

    public void RestartFromFirstLevel()
    {
        currentLevelIndex = 0;
        targetSublevelIndex = 0;
        hasSeenIntro = true;

        BeginScorecardSession(0);
        LogCurrentFruitLevelDebug("Play Again started Level 1");

        if (gameLevels != null && gameLevels.Length > 0)
        {
            SceneManager.LoadScene(gameLevels[0].sceneName);
        }
        else
        {
            Debug.LogWarning("Cannot restart: no game levels are configured.");
        }
    }

    public void BackToMainMenu()
    {
        currentLevelIndex = 0;
        targetSublevelIndex = 0;
        hasSeenIntro = false;
        ClearScorecardSession();

        Instance = null;
        Destroy(gameObject);

        SceneManager.LoadScene(MainMenuSceneName);
    }
    #endregion

    #region Session Metrics
    private void BeginScorecardSession(int startingLevelIndex)
    {
        if (sessionMetrics == null) return;

        int configuredLevelCount = GetLevelCount();
        int expectedSublevels = CalculateExpectedSublevelsFromLevel(startingLevelIndex);
        sessionMetrics.StartSession(
            startingLevelIndex + 1,
            expectedSublevels,
            configuredLevelCount,
            Time.realtimeSinceStartup);
    }

    private int CalculateExpectedSublevelsFromLevel(int startingLevelIndex)
    {
        if (gameLevels == null || gameLevels.Length == 0) return 0;
        if (startingLevelIndex < 0 || startingLevelIndex >= gameLevels.Length) return 0;

        int expectedSublevels = 0;
        for (int i = startingLevelIndex; i < gameLevels.Length; i++)
        {
            SublevelData[] sublevels = gameLevels[i].sublevels;
            if (sublevels != null) expectedSublevels += sublevels.Length;
        }

        return expectedSublevels;
    }

    private void UpdateScorecardFinalLevelReached(int levelIndex)
    {
        if (sessionMetrics == null) return;
        if (gameLevels == null || gameLevels.Length == 0) return;
        if (levelIndex < 0 || levelIndex >= gameLevels.Length) return;

        sessionMetrics.UpdateFinalLevelReached(levelIndex + 1, gameLevels.Length);
    }

    private void EndScorecardSession()
    {
        if (sessionMetrics == null) return;
        sessionMetrics.EndSession(Time.realtimeSinceStartup);
        LogScorecardDebug("Session ended");
    }

    private void ClearScorecardSession()
    {
        if (sessionMetrics == null) return;
        sessionMetrics.Reset();
    }

    public void RecordScorecardSuccessfulPeel()
    {
        if (sessionMetrics == null) return;
        sessionMetrics.RecordSuccessfulPeel();
        LogScorecardDebug("Successful peel recorded");
    }

    public void RecordScorecardFailedOrMissedAttempt()
    {
        if (sessionMetrics == null) return;
        sessionMetrics.RecordFailedOrMissedAttempt();
        LogScorecardDebug("Failed/missed attempt recorded");
    }

    public void RecordScorecardCompletedSublevel()
    {
        if (sessionMetrics == null) return;
        sessionMetrics.RecordCompletedSublevel();
        UpdateScorecardFinalLevelReached(currentLevelIndex);
        LogScorecardDebug("Completed sublevel recorded");
    }

    private void LogCurrentFruitLevelDebug(string eventName)
    {
        if (!enableCurrentFruitLevelDebugLogs) return;

        Debug.Log(
            "[Current Fruit Level Test] " + eventName +
            " | CurrentFruitLevelId: " + CurrentFruitLevelId);
    }

    private void LogScorecardDebug(string eventName)
    {
        if (!enableScorecardDebugLogs || sessionMetrics == null) return;

        Debug.Log(
            "[Session Scorecard Test] " + eventName +
            " | IsActive: " + sessionMetrics.IsActive +
            " | StartingLevel: " + sessionMetrics.StartingLevel +
            " | FinalLevelReached: " + sessionMetrics.FinalLevelReached +
            " | SuccessfulPeels: " + sessionMetrics.SuccessfulPeels +
            " | FailedOrMissedAttempts: " + sessionMetrics.FailedOrMissedAttempts +
            " | TotalAttempts: " + sessionMetrics.TotalAttempts +
            " | CompletedSublevels: " + sessionMetrics.CompletedSublevels +
            " | ExpectedSublevels: " + sessionMetrics.ExpectedSublevels +
            " | PeelSuccessRate: " + (sessionMetrics.PeelSuccessRate * 100f).ToString("F2") + "%" +
            " | SessionCompletionRate: " + (sessionMetrics.SessionCompletionRate * 100f).ToString("F2") + "%" +
            " | MissRate: " + (sessionMetrics.MissRate * 100f).ToString("F2") + "%" +
            " | DurationSeconds: " + sessionMetrics.DurationSeconds.ToString("F2"));
    }
    #endregion
}