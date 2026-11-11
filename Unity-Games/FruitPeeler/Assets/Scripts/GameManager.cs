using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    #region Scene References / UI References
    // GameManager controls only the current scene/sublevel gameplay.
    // The global level list, level order, and scene progression are owned by GlobalLevelManager.
    // Menus, popups, pause and the speed setting are owned by PeelerSessionFlow / PeelerMenuUI.

    [Header("── UI ──")]
    [SerializeField] private TextMeshProUGUI attemptsText;
    [SerializeField] private TextMeshProUGUI sublevelText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("── Fruit Groups ──")]
    [SerializeField] private GameObject[] fruitGroups;
    private List<GameObject> activeGroupFruits = new List<GameObject>();

    [Header("── References ──")]
    [SerializeField] private Transform maskContainer;
    [SerializeField] private PeelerController peelerController;

    [Header("── Rules ──")]
    [Tooltip("Peeler width. Check it in the Peeler Inspector -> Actual Cut Width")]
    [SerializeField] private float peelerWidth = 1.3f;

    [Tooltip("Forgiveness for the extreme edges of the fruit (transparent air). Try 0.05")]
    [SerializeField] private float edgeTolerance = 0.05f;

    [Tooltip("Forgiveness for millimeter gaps between cuts. Try 0.02 (very strict)")]
    [SerializeField] private float internalGapTolerance = 0.02f;

    [Header("── Legacy Panels (always hidden, replaced by PeelerMenuUI) ──")]
    [SerializeField] private GameObject practiceChoicePanel;
    [SerializeField] private GameObject levelSelectionPanel;
    [SerializeField] private GameObject finishPracticeButton;
    #endregion

    #region Current Sublevel State
    private GlobalLevelManager.SublevelData currentSublevelData;

    // ── State ──
    private int currentSublevelIndex = 0;
    private int remainingAttempts;
    private List<float> successfulCutsX = new List<float>();
    private bool isSublevelEnding = false;

    // Fruits that already played their cut sound in the current stroke (one sound per fruit per stroke).
    private readonly HashSet<GameObject> fruitsWithCutSound = new HashSet<GameObject>();

    // Peeler left/right speed comes from the Settings popup (Slow / Normal / Fast).
    public float SpeedMultiplier => PeelerSessionFlow.Instance != null ? PeelerSessionFlow.Instance.SpeedFactor : 1.0f;

    public int CurrentFruitCount => currentSublevelData != null ? currentSublevelData.fruitCount : 1;

    public int CurrentStageNumber => currentSublevelIndex + 1;

    public int StageCount => GlobalLevelManager.Instance != null
        ? Mathf.Max(1, GlobalLevelManager.Instance.GetSublevelCountForCurrentLevel())
        : fruitGroups != null ? fruitGroups.Length : 1;
    #endregion

    #region Unity Lifecycle
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        if (PeelerSessionFlow.Instance == null) return;
        PeelerSessionFlow.Instance.OnScreenChanged += HandleScreenChanged;
        PeelerSessionFlow.Instance.OnPausedChanged += HandlePausedChanged;
    }

    void OnDisable()
    {
        if (PeelerSessionFlow.Instance == null) return;
        PeelerSessionFlow.Instance.OnScreenChanged -= HandleScreenChanged;
        PeelerSessionFlow.Instance.OnPausedChanged -= HandlePausedChanged;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (peelerController == null)
            peelerController = FindAnyObjectByType<PeelerController>();
        if (maskContainer == null)
            maskContainer = GameObject.Find("MaskContainer")?.transform;
        if (messageText != null)
            messageText.gameObject.SetActive(false);

        // The practice, "Choose Level" and "move up to start" screens are no longer used.
        if (practiceChoicePanel != null) practiceChoicePanel.SetActive(false);
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (finishPracticeButton != null) finishPracticeButton.SetActive(false);

        int initialSublevel = 0;
        if (GlobalLevelManager.Instance != null)
        {
            initialSublevel = GlobalLevelManager.Instance.GetTargetSublevel();
        }

        StartSublevel(initialSublevel);
    }
    #endregion

    #region Session Flow (menus / pause)
    private static bool IsSessionPlaying()
    {
        return PeelerSessionFlow.Instance == null || PeelerSessionFlow.Instance.IsPlaying;
    }

    private void HandleScreenChanged(PeelerScreen screen) => ApplyPeelerLock();

    private void HandlePausedChanged(bool paused) => ApplyPeelerLock();

    /// <summary>The peeler only moves while the patient is actively playing
    /// (no menu or popup on screen) and the stage is not ending.</summary>
    private void ApplyPeelerLock()
    {
        if (peelerController == null) return;
        peelerController.SetLocked(!IsSessionPlaying() || isSublevelEnding);
    }

    /// <summary>Called by PeelerController when a new upward stroke starts,
    /// so every cutting attempt plays its cut sound again.</summary>
    public void NotifyStrokeStarted()
    {
        fruitsWithCutSound.Clear();
    }

    /// <summary>Called by PeelerController while the blade touches a fruit.
    /// Plays the fruit level's cut sound once for each fruit touched in the stroke.</summary>
    public void NotifyFruitContact(GameObject fruit)
    {
        if (fruit == null || isSublevelEnding || !IsSessionPlaying()) return;
        if (!fruitsWithCutSound.Add(fruit)) return;

        int fruitLevelId = GlobalLevelManager.Instance != null ? GlobalLevelManager.Instance.CurrentFruitLevelId : 1;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayCut(fruitLevelId);
    }
    #endregion

    #region Level Flow / Sublevel Setup
    public void StartSublevel(int index)
    {
        currentSublevelIndex = index;
        currentSublevelData = ResolveSublevelData(index);

        if (currentSublevelData == null)
        {
            Debug.LogError("❌ No sublevel configuration found for index " + index +
                            ". Check GlobalLevelManager → Game Levels → Sublevels.");
            return;
        }

        remainingAttempts = currentSublevelData.attempts;
        successfulCutsX.Clear();
        fruitsWithCutSound.Clear();
        isSublevelEnding = false;

        ClearMasks();
        ActivateFruitGroup(currentSublevelData.fruitCount);
        UpdateUI();

        if (messageText != null) messageText.gameObject.SetActive(false);
        if (peelerController != null)
        {
            peelerController.ResetState();
            // A patient still pushing up when the stage appears must relax first.
            peelerController.ActivateInputReleaseLock();
        }
        ApplyPeelerLock();

        Debug.Log("▶️ " + currentSublevelData.levelName);
    }

    // Fallback when the scene is played directly without the MainMenu boot scene.
    private GlobalLevelManager.SublevelData ResolveSublevelData(int index)
    {
        if (GlobalLevelManager.Instance != null)
        {
            GlobalLevelManager.SublevelData data = GlobalLevelManager.Instance.GetSublevelData(index);
            if (data != null) return data;
        }

        Debug.LogWarning("⚠️ GlobalLevelManager not available or has no data. Using local fallback.");
        return new GlobalLevelManager.SublevelData
        {
            levelName = "Stage " + (index + 1),
            fruitCount = Mathf.Clamp(index + 1, 1, fruitGroups != null ? fruitGroups.Length : 1),
            attempts = 3
        };
    }

    void ActivateFruitGroup(int amount)
    {
        int groupIndex = amount - 1;

        for (int i = 0; i < fruitGroups.Length; i++)
            if (fruitGroups[i] != null)
                fruitGroups[i].SetActive(i == groupIndex);

        activeGroupFruits.Clear();

        GameObject activeGroup = fruitGroups[groupIndex];
        if (activeGroup == null) return;

        foreach (Transform child in activeGroup.transform)
        {
            if (child.gameObject.name.ToLower().Contains("brown"))
            {
                activeGroupFruits.Add(child.gameObject);
                Debug.Log("📋 Registered: " + child.gameObject.name +
                          " at position X=" + child.position.x.ToString("F2"));
            }
        }

        Debug.Log("✅ Total fruits in active group: " + activeGroupFruits.Count);
    }

    public void ClearMasks()
    {
        if (maskContainer == null) return;
        for (int i = maskContainer.childCount - 1; i >= 0; i--)
            Destroy(maskContainer.GetChild(i).gameObject);
    }
    #endregion

    #region Cut Registration
    public void RegisterSuccessfulCut(float positionX)
    {
        if (isSublevelEnding) return;

        remainingAttempts--;
        successfulCutsX.Add(positionX);

        if (GlobalLevelManager.Instance != null)
            GlobalLevelManager.Instance.RecordScorecardSuccessfulPeel();

        Debug.Log("✅ Successful cut at X=" + positionX.ToString("F2") +
                  " | Total cuts: " + successfulCutsX.Count +
                  " | Remaining attempts: " + remainingAttempts);

        UpdateUI();

        if (VerifyActualCoverage())
        {
            Debug.Log("🔍 Early evaluation. Fully peeled before attempts ran out.");
            isSublevelEnding = true;
            StartCoroutine(WinSublevel());
            return;
        }

        EvaluateState();
    }

    public void RegisterFailedCut()
    {
        if (isSublevelEnding) return;

        remainingAttempts--;

        if (GlobalLevelManager.Instance != null)
            GlobalLevelManager.Instance.RecordScorecardFailedOrMissedAttempt();

        UpdateUI();

        Debug.Log("❌ Failed cut. Remaining attempts: " + remainingAttempts);
        EvaluateState();
    }
    #endregion

    #region Win / Fail Evaluation
    void EvaluateState()
    {
        if (isSublevelEnding) return;
        if (remainingAttempts > 0) return;

        bool fullyPeeled = VerifyActualCoverage();

        Debug.Log("🔍 Final evaluation. Fully peeled: " + fullyPeeled);

        if (fullyPeeled)
        {
            isSublevelEnding = true;
            StartCoroutine(WinSublevel());
        }
        else
        {
            isSublevelEnding = true;
            StartCoroutine(FailSublevel());
        }
    }

    // ==========================================
    // LÓGICA DE VICTORIA ORIGINAL — SIN CAMBIOS
    // ==========================================
    bool VerifyActualCoverage()
    {
        if (successfulCutsX.Count == 0)
        {
            Debug.Log("No successful cuts.");
            return false;
        }

        if (activeGroupFruits.Count == 0)
        {
            Debug.LogError("No fruits registered in active group.");
            return false;
        }

        // ── DEBUG TEMPORAL ──
        float actualPeelerWDebug = peelerController != null
            ? peelerController.ActualPeelerWidth
            : peelerWidth;

        Debug.Log("🔧 DEBUG VALORES:" +
                  "\n   ActualPeelerWidth: " + actualPeelerWDebug.ToString("F4") +
                  "\n   edgeTolerance: " + edgeTolerance.ToString("F4") +
                  "\n   internalGapTolerance: " + internalGapTolerance.ToString("F4") +
                  "\n   Cortes registrados: " + successfulCutsX.Count);

        foreach (float cx in successfulCutsX)
            Debug.Log("   Corte en X=" + cx.ToString("F4"));

        foreach (GameObject fruitObj in activeGroupFruits)
        {
            if (fruitObj == null) continue;

            SpriteRenderer sr = fruitObj.GetComponent<SpriteRenderer>();
            float fruitHalfW = sr != null
                ? sr.bounds.size.x / 2f
                : (peelerController != null ? peelerController.fruitHalfWidth : 1.75f);

            float centerX = fruitObj.transform.position.x;
            float leftEdge = (centerX - fruitHalfW) + edgeTolerance;
            float rightEdge = (centerX + fruitHalfW) - edgeTolerance;

            Debug.Log("🔍 " + fruitObj.name +
                      " | Actual width: " + (fruitHalfW * 2f).ToString("F2") +
                      " | Range: [" + leftEdge.ToString("F2") +
                      " → " + rightEdge.ToString("F2") + "]");

            List<Vector2> intervals = new List<Vector2>();
            float actualPeelerW = peelerController != null
                ? peelerController.ActualPeelerWidth
                : peelerWidth;
            float half = actualPeelerW / 2f;

            foreach (float cx in successfulCutsX)
            {
                intervals.Add(new Vector2(cx - half, cx + half));
            }

            if (!IntervalsCoverRange(intervals, leftEdge, rightEdge))
            {
                Debug.Log("❌ " + fruitObj.name + " incomplete.");
                return false;
            }

            Debug.Log("✅ " + fruitObj.name + " peeled completely.");
        }

        return true;
    }

    bool IntervalsCoverRange(List<Vector2> intervals, float rangeMin, float rangeMax)
    {
        intervals.Sort((a, b) => a.x.CompareTo(b.x));

        if (intervals[0].x > rangeMin)
        {
            Debug.Log("❌ Left edge incomplete.");
            return false;
        }

        float coverageUpTo = intervals[0].y;

        for (int i = 1; i < intervals.Count; i++)
        {
            if (intervals[i].x > coverageUpTo + internalGapTolerance)
            {
                Debug.Log("❌ Internal gap detected.");
                return false;
            }
            coverageUpTo = Mathf.Max(coverageUpTo, intervals[i].y);
        }

        if (coverageUpTo < rangeMax)
        {
            Debug.Log("❌ Right edge incomplete.");
            return false;
        }

        return true;
    }
    // ==========================================

    IEnumerator WinSublevel()
    {
        ApplyPeelerLock();
        ShowMessage("Perfectly peeled!");
        if (AudioManager.Instance != null) AudioManager.Instance.PlayWin();

        Debug.Log("🏆 Stage " + (currentSublevelIndex + 1) + " completed.");

        if (GlobalLevelManager.Instance != null)
            GlobalLevelManager.Instance.RecordScorecardCompletedSublevel();

        yield return new WaitForSeconds(2f);

        if (GlobalLevelManager.Instance != null)
        {
            // Next stage, or the result popup after the last stage of this fruit.
            GlobalLevelManager.Instance.AdvanceNextStep();
        }
        else if (currentSublevelIndex + 1 < StageCount)
        {
            StartSublevel(currentSublevelIndex + 1);
        }
        else
        {
            ShowMessage("Fruit completed!");
        }
    }

    IEnumerator FailSublevel()
    {
        ApplyPeelerLock();
        ShowMessage("Try again!");
        if (AudioManager.Instance != null) AudioManager.Instance.PlayFail();

        Debug.Log("❌ Stage failed. Restarting...");

        yield return new WaitForSeconds(2f);

        if (GlobalLevelManager.Instance != null)
        {
            GlobalLevelManager.Instance.ResetCurrentSublevel();
        }
        else
        {
            StartSublevel(currentSublevelIndex);
        }
    }
    #endregion

    #region UI Updates
    void UpdateUI()
    {
        if (attemptsText != null)
            attemptsText.text = "Steps: " + remainingAttempts;

        if (sublevelText != null)
            sublevelText.text = "Stage " + CurrentStageNumber + " / " + StageCount;
    }

    void ShowMessage(string message)
    {
        if (messageText == null) return;
        messageText.gameObject.SetActive(true);
        messageText.text = message;
    }
    #endregion
}
