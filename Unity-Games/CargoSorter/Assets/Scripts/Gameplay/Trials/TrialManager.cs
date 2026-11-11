using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TrialManager : MonoBehaviour
{
    public event Action<int, int> OnTrialStarted;
    public event Action<TrialOutcome, BinTarget> OnTrialCompleted;
    public event Action<int, int> OnTrialProgressChanged;
    public event Action<BinTarget> OnGuidedTargetChanged;
    public event Action<BlockSummary> OnBlockCompleted;
    private static readonly Vector3 ProductStartScale = new Vector3(0.22f, 0.22f, 0.22f);
    // Keeps perspective growth while fitting the widest product sprites comfortably inside a bin opening.
    private static readonly Vector3 ProductEndScale = new Vector3(0.55f, 0.55f, 0.55f);
    private static readonly Vector3 CollectionTargetOffset = new Vector3(0f, -0.2f, 0f);
    private const float CollectionDuration = 0.25f;
    private const float CollectionScaleMultiplier = 0.8f;

    [SerializeField] private ProductMover productPrefab;
    [SerializeField] private List<ProductDefinition> productDefinitions = new List<ProductDefinition>();
    [SerializeField] private Transform productSpawnPoint;
    [SerializeField] private Transform productLandingPoint;
    [SerializeField] private Transform loadingPoint;
    [SerializeField] private List<BinTarget> binTargets = new List<BinTarget>();
    [SerializeField] private int trialCount = 10;
    [SerializeField] private float interTrialInterval = 0.75f;
    [SerializeField] private int randomSeed = 2026;
    [SerializeField] private float alignmentTolerance = 0.6f;
    [SerializeField] private OutcomeFeedbackUI outcomeFeedback;
    [SerializeField] private SpeedLevelSettings speedSettings = new SpeedLevelSettings();

    private readonly List<ProductDefinition> trialSequence = new List<ProductDefinition>();
    private ProductMover activeProduct;
    private int completedTrialCount;
    private int correctCount;
    private int wrongBinCount;
    private int missCount;
    private float interTrialTimeRemaining;
    private bool isWaitingForNextTrial;
    private bool isHandlingCompletion;
    private bool trialsFinished;
    private bool feedbackWarningLogged;
    private bool isBlockRunning;
    private GameMode currentMode;
    private float currentBlockTravelDuration;

    public int CurrentTrial => isBlockRunning ? Mathf.Min(completedTrialCount + 1, trialCount) : completedTrialCount;
    public int TotalTrials => trialCount;
    public GameMode CurrentMode => currentMode;
    public bool IsBlockRunning => isBlockRunning;
    public int CurrentSpeedLevel => speedSettings.CurrentSpeedLevel;
    // Takes effect when the next block starts.
    public void SetSpeedLevel(int level) { speedSettings.SetLevel(level); }
    public int CurrentBlockSeed => randomSeed;

    public bool Configure(IReadOnlyList<ProductDefinition> products, IReadOnlyList<BinTarget> targets)
    {
        if (isBlockRunning)
        {
            Debug.LogError("TrialManager cannot be configured while a block is running.", this);
            return false;
        }

        productDefinitions = products != null ? new List<ProductDefinition>(products) : new List<ProductDefinition>();
        binTargets = targets != null ? new List<BinTarget>(targets) : new List<BinTarget>();
        if (IsConfigurationValid(out string validationError)) return true;

        Debug.LogError($"TrialManager configuration is invalid: {validationError}", this);
        return false;
    }

    private void Awake()
    {
        if (!IsConfigurationValid(out string validationError))
        {
            Debug.LogError($"TrialManager configuration is invalid: {validationError}", this);
            enabled = false;
            return;
        }

        WarnIfFeedbackIsMissing();
    }

    public bool StartBlock(GameMode mode)
    {
        if (!enabled || isBlockRunning) return false;
        ResetBlock();
        currentBlockTravelDuration = speedSettings.GetSelectedDuration();
        currentMode = mode;
        isBlockRunning = true;
        randomSeed = Guid.NewGuid().GetHashCode();
        BuildTrialSequence();
        SpawnNextProduct();
        return true;
    }

    public void ResetBlock()
    {
        if (activeProduct != null)
        {
            activeProduct.Completed -= HandleProductCompleted;
            Destroy(activeProduct.gameObject);
        }
        activeProduct = null;
        trialSequence.Clear();
        completedTrialCount = correctCount = wrongBinCount = missCount = 0;
        interTrialTimeRemaining = 0f;
        isWaitingForNextTrial = isHandlingCompletion = trialsFinished = isBlockRunning = false;
        OnGuidedTargetChanged?.Invoke(null);
        OnTrialProgressChanged?.Invoke(0, trialCount);
    }

    private void Update()
    {
        if (!isBlockRunning || !isWaitingForNextTrial || trialsFinished)
        {
            return;
        }

        interTrialTimeRemaining -= Time.deltaTime;
        if (interTrialTimeRemaining > 0f)
        {
            return;
        }

        isWaitingForNextTrial = false;
        if (completedTrialCount >= trialCount)
        {
            FinishBlock();
            return;
        }

        SpawnNextProduct();
    }

    private bool IsConfigurationValid(out string error)
    {
        if (productPrefab == null || productSpawnPoint == null || productLandingPoint == null || loadingPoint == null)
        {
            error = "the product prefab and all reference points must be assigned.";
            return false;
        }

        if (trialCount <= 0 || interTrialInterval < 0f || alignmentTolerance < 0f)
        {
            error = "trial count must be positive, and timing and tolerance values cannot be negative.";
            return false;
        }

        if (productDefinitions == null || productDefinitions.Count == 0 || HasNullOrDuplicateProducts())
        {
            error = "at least one unique ProductDefinition asset must be assigned.";
            return false;
        }

        if (binTargets == null || binTargets.Count == 0 || HasInvalidBinTargets())
        {
            error = "at least one unique BinTarget with an alignment point and category must be assigned.";
            return false;
        }

        var targetCategories = new HashSet<ProductCategory>();
        foreach (BinTarget target in binTargets) targetCategories.Add(target.Category);
        foreach (ProductDefinition definition in productDefinitions)
        {
            if (!targetCategories.Contains(definition.Category))
            {
                error = $"no BinTarget is assigned for product category {definition.Category}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private bool HasNullOrDuplicateProducts()
    {
        var uniqueProducts = new HashSet<ProductDefinition>();
        foreach (ProductDefinition definition in productDefinitions)
        {
            if (definition == null || !uniqueProducts.Add(definition))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasInvalidBinTargets()
    {
        var uniqueTargets = new HashSet<BinTarget>();
        var categories = new HashSet<ProductCategory>();
        foreach (BinTarget target in binTargets)
        {
            if (target == null || target.AlignmentPoint == null || !uniqueTargets.Add(target) || !categories.Add(target.Category))
            {
                return true;
            }
        }

        return false;
    }

    private void BuildTrialSequence()
    {
        var random = new System.Random(randomSeed);
        while (trialSequence.Count < trialCount)
        {
            var shuffledProducts = new List<ProductDefinition>(productDefinitions);
            for (int index = shuffledProducts.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                (shuffledProducts[index], shuffledProducts[swapIndex]) =
                    (shuffledProducts[swapIndex], shuffledProducts[index]);
            }

            int remainingTrials = trialCount - trialSequence.Count;
            trialSequence.AddRange(shuffledProducts.GetRange(0, Mathf.Min(remainingTrials, shuffledProducts.Count)));
        }
    }

    private void SpawnNextProduct()
    {
        if (activeProduct != null || completedTrialCount >= trialCount || trialsFinished)
        {
            return;
        }

        ProductDefinition definition = trialSequence[completedTrialCount];
        BinTarget guidedTarget = currentMode == GameMode.Guided ? FindTargetForCategory(definition.Category) : null;
        OnGuidedTargetChanged?.Invoke(guidedTarget);
        OnTrialStarted?.Invoke(completedTrialCount + 1, trialCount);
        activeProduct = Instantiate(productPrefab);
        activeProduct.Completed += HandleProductCompleted;
        activeProduct.Initialize(
            definition,
            productSpawnPoint,
            productLandingPoint,
            currentBlockTravelDuration,
            ProductStartScale,
            ProductEndScale);
    }

    private void HandleProductCompleted(ProductMover product)
    {
        if (isHandlingCompletion || product == null || product != activeProduct)
        {
            return;
        }

        isHandlingCompletion = true;
        product.Completed -= HandleProductCompleted;

        ProductDefinition definition = product.ProductDefinition;
        BinTarget selectedTarget = FindClosestTarget(out float alignmentError);
        TrialOutcome outcome = EvaluateOutcome(definition, selectedTarget, alignmentError);
        RecordOutcome(outcome);
        OnGuidedTargetChanged?.Invoke(null);
        OnTrialCompleted?.Invoke(outcome, selectedTarget);
        if (outcomeFeedback != null)
        {
            Transform feedbackAnchor = outcome == TrialOutcome.Miss || selectedTarget == null
                ? null
                : selectedTarget.transform;
            outcomeFeedback.ShowOutcome(outcome, feedbackAnchor);
        }

        completedTrialCount++;
        OnTrialProgressChanged?.Invoke(completedTrialCount, trialCount);
        string selectedCategory = outcome == TrialOutcome.Miss ? "None" : selectedTarget.Category.ToString();
        Debug.Log(
            $"Trial {completedTrialCount}/{trialCount} | Product: {definition.DisplayName} | Category: {definition.Category} | " +
            $"Selected: {selectedCategory} | Error: {alignmentError:F2} | Outcome: {outcome}");

        if (outcome == TrialOutcome.Correct && selectedTarget != null &&
            product.BeginCollection(
                selectedTarget.AlignmentPoint,
                CollectionTargetOffset,
                CollectionDuration,
                CollectionScaleMultiplier,
                () => FinishProduct(product, CollectionDuration)))
        {
            return;
        }

        FinishProduct(product, 0f);
    }

    private void FinishProduct(ProductMover product, float feedbackElapsed)
    {
        if (product == null || product != activeProduct) return;

        Destroy(product.gameObject);
        activeProduct = null;
        isHandlingCompletion = false;
        // Collection runs alongside the existing feedback, so only its remaining time is awaited.
        float remainingFeedbackDuration = outcomeFeedback != null
            ? Mathf.Max(0f, outcomeFeedback.TotalDuration - feedbackElapsed)
            : 0f;
        interTrialTimeRemaining = interTrialInterval + remainingFeedbackDuration;
        isWaitingForNextTrial = true;
        if (completedTrialCount >= trialCount && interTrialInterval <= 0f && remainingFeedbackDuration <= 0f)
            FinishBlock();
    }

    private void WarnIfFeedbackIsMissing()
    {
        if (outcomeFeedback != null || feedbackWarningLogged)
        {
            return;
        }

        Debug.LogWarning(
            "TrialManager has no outcome feedback UI assigned; trials will continue without on-screen feedback.",
            this);
        feedbackWarningLogged = true;
    }

    private BinTarget FindClosestTarget(out float alignmentError)
    {
        BinTarget closestTarget = null;
        alignmentError = float.PositiveInfinity;

        foreach (BinTarget target in binTargets)
        {
            float horizontalDistance = Mathf.Abs(loadingPoint.position.x - target.AlignmentPoint.position.x);
            if (horizontalDistance < alignmentError)
            {
                alignmentError = horizontalDistance;
                closestTarget = target;
            }
        }

        return closestTarget;
    }

    private TrialOutcome EvaluateOutcome(
        ProductDefinition definition,
        BinTarget selectedTarget,
        float alignmentError)
    {
        if (selectedTarget == null || alignmentError > alignmentTolerance)
        {
            return TrialOutcome.Miss;
        }

        return selectedTarget.Category == definition.Category ? TrialOutcome.Correct : TrialOutcome.WrongBin;
    }

    private void RecordOutcome(TrialOutcome outcome)
    {
        switch (outcome)
        {
            case TrialOutcome.Correct:
                correctCount++;
                break;
            case TrialOutcome.WrongBin:
                wrongBinCount++;
                break;
            case TrialOutcome.Miss:
                missCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
        }
    }
    private BinTarget FindTargetForCategory(ProductCategory category)
    {
        return binTargets.Find(target => target != null && target.Category == category);
    }

    private void FinishBlock()
    {
        if (!isBlockRunning) return;
        trialsFinished = true;
        isBlockRunning = false;
        isWaitingForNextTrial = false;
        OnGuidedTargetChanged?.Invoke(null);
        var summary = new BlockSummary(currentMode, trialCount, correctCount, wrongBinCount, missCount);
        Debug.Log($"Trials complete | Correct: {correctCount} | WrongBin: {wrongBinCount} | Miss: {missCount}");
        OnBlockCompleted?.Invoke(summary);
    }

    private void OnDestroy()
    {
        if (activeProduct != null) activeProduct.Completed -= HandleProductCompleted;
    }

    private void OnValidate()
    {
        if (speedSettings == null) speedSettings = new SpeedLevelSettings();
        speedSettings.Validate();
    }

}
