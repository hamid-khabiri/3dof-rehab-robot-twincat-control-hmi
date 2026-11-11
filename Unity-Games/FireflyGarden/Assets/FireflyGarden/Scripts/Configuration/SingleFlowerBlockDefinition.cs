using System.Collections.Generic;
using UnityEngine;

namespace FireflyGarden.Configuration
{
    [CreateAssetMenu(menuName = "Firefly Garden/Single Flower Block", fileName = "SingleFlowerBlock")]
    public sealed class SingleFlowerBlockDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string blockId;
        [SerializeField] private string conditionId;
        [SerializeField] private int configurationVersion = 1;
        [Header("Trial Schedule")]
        [SerializeField] private GameMode gameMode = GameMode.SingleFlower;
        [SerializeField] private int trialCount = 12;
        [SerializeField] private int randomSeed = 240731;
        [SerializeField] private bool randomizeSeedEachRun;
        [SerializeField] private List<TargetPositionDefinition> enabledPositions = new List<TargetPositionDefinition>();
        [SerializeField] private bool avoidImmediatePositionRepeat = true;
        [SerializeField] private int maximumConsecutiveSameDirection = 2;
        [Header("Find the Match Schedule")]
        [SerializeField] private List<StimulusDefinition> enabledStimuli = new List<StimulusDefinition>();
        [SerializeField] private bool avoidImmediateStimulusPairRepeat = true;
        [SerializeField] private bool avoidImmediatePositionPairRepeat = true;
        [SerializeField, Min(1)] private int maxSameCorrectSlotConsecutively = 2;
        [Header("Trial Presentation")]
        [SerializeField] private bool guidanceEnabled = true;
        [SerializeField] private float targetScaleMultiplier = 1f;
        [Header("Trial Timing")]
        [SerializeField] private float initialHomeDwellDuration = 0.75f;
        [SerializeField, Range(1f, 3f)] private float cueExposureDuration = 2f;
        [SerializeField, Range(0.5f, 3f)] private float memoryDelayDuration = 1f;
        [SerializeField] private float targetDwellDuration = 0.60f;
        [SerializeField] private float successFeedbackDuration = 0.60f;
        [SerializeField] private float returnHomeDwellDuration = 0.50f;
        [SerializeField] private bool timeoutEnabled = false;
        [SerializeField] private float timeoutDuration = 12.0f;
        [Header("Optional Metadata")]
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string designerNotes;

        public string BlockId => blockId;
        public string ConditionId => conditionId;
        public GameMode GameMode => gameMode;
        public int ConfigurationVersion => configurationVersion;
        public int TrialCount => trialCount;
        public int RandomSeed => randomSeed;
        public bool RandomizeSeedEachRun => randomizeSeedEachRun;
        public IReadOnlyList<TargetPositionDefinition> EnabledPositions => enabledPositions;
        public bool AvoidImmediatePositionRepeat => avoidImmediatePositionRepeat;
        public int MaximumConsecutiveSameDirection => maximumConsecutiveSameDirection;
        public IReadOnlyList<StimulusDefinition> EnabledStimuli => enabledStimuli;
        public bool AvoidImmediateStimulusPairRepeat => avoidImmediateStimulusPairRepeat;
        public bool AvoidImmediatePositionPairRepeat => avoidImmediatePositionPairRepeat;
        public int MaxSameCorrectSlotConsecutively => maxSameCorrectSlotConsecutively;
        public bool GuidanceEnabled => guidanceEnabled;
        public float TargetScaleMultiplier => targetScaleMultiplier;
        public float InitialHomeDwellDuration => initialHomeDwellDuration;
        public float CueExposureDuration => cueExposureDuration;
        public float MemoryDelayDuration => memoryDelayDuration;
        public float TargetDwellDuration => targetDwellDuration;
        public float SuccessFeedbackDuration => successFeedbackDuration;
        public float ReturnHomeDwellDuration => returnHomeDwellDuration;
        public bool TimeoutEnabled => timeoutEnabled;
        public float TimeoutDuration => timeoutDuration;
        public string DisplayName => displayName;
        public string DesignerNotes => designerNotes;

        public bool TryValidate(out string error)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(blockId)) errors.Add("blockId must be non-empty.");
            if (string.IsNullOrWhiteSpace(conditionId)) errors.Add("conditionId must be non-empty.");
            if (configurationVersion < 1) errors.Add("configurationVersion must be at least 1.");
            if (trialCount < 1) errors.Add("trialCount must be at least 1.");
            if (!System.Enum.IsDefined(typeof(GameMode), gameMode)) errors.Add("gameMode is not supported.");
            if (enabledPositions == null || enabledPositions.Count == 0) errors.Add("At least one position is required.");
            else if (enabledPositions.Exists(position => position == null)) errors.Add("Enabled positions cannot contain null references.");
            if (gameMode != GameMode.SingleFlower
                && (enabledStimuli == null || enabledStimuli.Count < 2))
                errors.Add("Cognitive modes require at least two stimuli.");
            else if (enabledStimuli != null && enabledStimuli.Exists(stimulus => stimulus == null))
                errors.Add("Enabled stimuli cannot contain null references.");
            if (!IsFinite(initialHomeDwellDuration) || initialHomeDwellDuration <= 0f
                || !IsFinite(targetDwellDuration) || targetDwellDuration <= 0f
                || !IsFinite(successFeedbackDuration) || successFeedbackDuration < 0f
                || !IsFinite(returnHomeDwellDuration) || returnHomeDwellDuration <= 0f)
                errors.Add("Dwell and feedback timings must be finite and valid.");
            if (timeoutEnabled && (!IsFinite(timeoutDuration) || timeoutDuration < 10f || timeoutDuration > 60f))
                errors.Add("Enabled timeout duration must be between 10 and 60 seconds.");
            if (gameMode == GameMode.RememberTheMatch
                && (!IsFinite(cueExposureDuration) || cueExposureDuration < 1f || cueExposureDuration > 3f
                    || !IsFinite(memoryDelayDuration) || memoryDelayDuration < 0.5f || memoryDelayDuration > 3f))
                errors.Add("Remember timing values are outside their supported ranges.");
            error = string.Join(" ", errors);
            return errors.Count == 0;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
