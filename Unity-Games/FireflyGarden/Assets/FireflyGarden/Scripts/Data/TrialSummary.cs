using System;
using FireflyGarden.Configuration;

namespace FireflyGarden.Data
{
    [Serializable]
    public sealed class TrialSummary
    {
        public const string CurrentSchemaVersion = "3.0";
        public const string SingleFlowerMode = "SingleFlower";
        public const double UnavailableValue = -1d;

        public string SchemaVersion { get; }
        public string SessionId { get; }
        public string BlockId { get; }
        public string ConditionId { get; }
        public int ConfigurationVersion { get; }
        public int RandomSeed { get; }
        public string GeneratorVersion { get; }
        public int TrialIndexZeroBased { get; }
        public int TrialNumberOneBased { get; }
        public string TrialId { get; }
        public string Mode { get; }
        public string PositionId { get; }
        public SingleFlowerDirection Direction { get; }
        public TargetDistanceBand DistanceBand { get; }
        public TrialOutcome Outcome { get; }
        public int TargetExitCount { get; }
        public bool RuntimePositionSwapOccurred { get; }
        public bool TimeoutEnabled { get; }
        public float TimeoutDuration { get; }
        public float InitialHomeDwellDuration { get; }
        public float TargetDwellDuration { get; }
        public float SuccessFeedbackDuration { get; }
        public float ReturnHomeDwellDuration { get; }
        public bool GuidanceEnabled { get; }
        public float TargetScaleMultiplier { get; }
        public double TrialStartTimestamp { get; }
        public double TargetActivationTimestamp { get; }
        public double FirstTargetEntryTimestamp { get; }
        public double TargetDwellCompletedTimestamp { get; }
        public double TimeoutTimestamp { get; }
        public double ReturnCompletedTimestamp { get; }
        public double TimeToFirstTargetEntry { get; }
        public double TargetDwellAccumulationSpan { get; }
        public double TargetAcquisitionElapsed { get; }
        public double ReturnPhaseDuration { get; }
        public double TotalTrialDuration { get; }
        public string CorrectStimulusId { get; }
        public string NonmatchingStimulusId { get; }
        public string CorrectTargetSlot { get; }
        public string FirstSelectedTargetSlot { get; }
        public string FirstChoiceCorrect { get; }
        public int ConfirmedIncorrectSelectionCount { get; }
        public bool AssistedAfterError { get; }
        public string TargetAPositionId { get; }
        public string TargetBPositionId { get; }
        public double CueOnsetTimestamp { get; }
        public double CueOffsetTimestamp { get; }
        public double ChoiceOnsetTimestamp { get; }
        public float CueExposureDuration { get; }
        public float MemoryDelayDuration { get; }
        public string AbortPhase { get; }
        public float AnticipatoryExitX { get; }
        public float AnticipatoryExitY { get; }
        public double AnticipatoryExitTimestamp { get; }
        public float ElapsedCueExposureAtAbort { get; }

        private TrialSummary(SingleFlowerTrialRuntimeRecord record, string sessionId,
            SingleFlowerBlockDefinition block, int randomSeedUsed, string generatorVersion)
        {
            SchemaVersion = CurrentSchemaVersion;
            SessionId = sessionId;
            BlockId = block.BlockId;
            ConditionId = block.ConditionId;
            ConfigurationVersion = block.ConfigurationVersion;
            RandomSeed = randomSeedUsed;
            GeneratorVersion = generatorVersion;
            TrialIndexZeroBased = record.TrialIndexZeroBased;
            TrialNumberOneBased = record.TrialIndexZeroBased + 1;
            TrialId = record.TrialId;
            Mode = record.Mode;
            PositionId = record.PositionId;
            Direction = record.Direction;
            DistanceBand = record.DistanceBand;
            Outcome = record.Outcome;
            TargetExitCount = record.TargetExitCount;
            RuntimePositionSwapOccurred = record.RuntimePositionSwapOccurred;
            TimeoutEnabled = record.TimeoutEnabled;
            TimeoutDuration = record.TimeoutDuration;
            InitialHomeDwellDuration = record.InitialHomeDwellDuration;
            TargetDwellDuration = record.TargetDwellDuration;
            SuccessFeedbackDuration = block.SuccessFeedbackDuration;
            ReturnHomeDwellDuration = record.ReturnHomeDwellDuration;
            GuidanceEnabled = block.GuidanceEnabled;
            TargetScaleMultiplier = block.TargetScaleMultiplier;
            TrialStartTimestamp = record.TrialStartTimestamp;
            TargetActivationTimestamp = record.TargetActivationTimestamp;
            FirstTargetEntryTimestamp = record.FirstTargetEntryTimestamp;
            TargetDwellCompletedTimestamp = record.TargetDwellCompletedTimestamp;
            TimeoutTimestamp = record.TimeoutTimestamp;
            ReturnCompletedTimestamp = record.ReturnCompletedTimestamp;
            TimeToFirstTargetEntry = record.FirstTargetEntryOccurred
                ? record.FirstTargetEntryTimestamp - record.TargetActivationTimestamp : UnavailableValue;
            TargetDwellAccumulationSpan = record.TargetDwellCompleted && record.FirstTargetEntryOccurred
                ? record.TargetDwellCompletedTimestamp - record.FirstTargetEntryTimestamp : UnavailableValue;
            TargetAcquisitionElapsed = record.Outcome == TrialOutcome.Success
                ? record.TargetDwellCompletedTimestamp - record.TargetActivationTimestamp : UnavailableValue;
            double returnPhaseStart = record.Outcome == TrialOutcome.Success
                ? record.TargetDwellCompletedTimestamp : record.TimeoutTimestamp;
            ReturnPhaseDuration = record.ReturnCompletedTimestamp - returnPhaseStart;
            TotalTrialDuration = record.ReturnCompletedTimestamp - record.TrialStartTimestamp;
            CorrectStimulusId = record.CorrectStimulusId;
            NonmatchingStimulusId = record.NonmatchingStimulusId;
            CorrectTargetSlot = record.CorrectTargetSlot;
            FirstSelectedTargetSlot = record.FirstSelectedTargetSlot;
            FirstChoiceCorrect = record.FirstChoiceRecorded ? record.FirstChoiceCorrect.ToString().ToLowerInvariant() : string.Empty;
            ConfirmedIncorrectSelectionCount = record.ConfirmedIncorrectSelectionCount;
            AssistedAfterError = record.AssistedAfterError;
            TargetAPositionId = record.TargetAPositionId;
            TargetBPositionId = record.TargetBPositionId;
            CueOnsetTimestamp = record.CueOnsetTimestamp;
            CueOffsetTimestamp = record.CueOffsetTimestamp;
            ChoiceOnsetTimestamp = record.ChoiceOnsetTimestamp;
            CueExposureDuration = record.CueExposureDuration;
            MemoryDelayDuration = record.MemoryDelayDuration;
            AbortPhase = record.AbortPhase;
            AnticipatoryExitX = record.AnticipatoryExitX;
            AnticipatoryExitY = record.AnticipatoryExitY;
            AnticipatoryExitTimestamp = record.AnticipatoryExitTimestamp;
            ElapsedCueExposureAtAbort = record.ElapsedCueExposureAtAbort;
        }

        public static bool TryFromRuntimeRecord(SingleFlowerTrialRuntimeRecord record, string sessionId,
            SingleFlowerBlockDefinition block, int randomSeedUsed, string generatorVersion,
            out TrialSummary summary, out string error)
        {
            summary = null;
            error = Validate(record, sessionId, block, randomSeedUsed, generatorVersion);
            if (!string.IsNullOrEmpty(error)) return false;
            summary = new TrialSummary(record, sessionId, block, randomSeedUsed, generatorVersion);
            return true;
        }

        private static string Validate(SingleFlowerTrialRuntimeRecord record, string sessionId,
            SingleFlowerBlockDefinition block, int randomSeedUsed, string generatorVersion)
        {
            if (record == null) return "The finalized runtime record is missing.";
            if (block == null) return "The block definition is missing.";
            if (string.IsNullOrWhiteSpace(sessionId)) return "The recording session ID is missing.";
            if (string.IsNullOrWhiteSpace(generatorVersion)) return "The generator version is missing.";
            if (randomSeedUsed == 0) return "The effective random seed is missing.";
            if (string.IsNullOrWhiteSpace(record.TrialId) || string.IsNullOrWhiteSpace(record.PositionId)) return "Trial identity is incomplete.";
            if (record.TrialIndexZeroBased < 0) return "The trial index is invalid.";
            if (record.Outcome == TrialOutcome.None) return "The completed trial has no outcome.";
            if (!IsAvailable(record.TrialStartTimestamp)) return "The trial start timestamp is missing.";
            if (record.Outcome == TrialOutcome.AbortedAnticipatoryHomeExit)
            {
                if (string.IsNullOrWhiteSpace(record.AbortPhase) || !IsAvailable(record.CueOnsetTimestamp)
                    || !IsAvailable(record.AnticipatoryExitTimestamp))
                    return "Anticipatory exit phase or cue onset is missing.";
                return string.Empty;
            }
            if (!record.ReturnCompleted || !IsAvailable(record.ReturnCompletedTimestamp)) return "ReturnDwell has not formally completed.";
            if (!record.TargetActivated || !IsAvailable(record.TargetActivationTimestamp)) return "Required target activation timestamp is missing.";
            if (record.TargetActivationTimestamp < record.TrialStartTimestamp) return "Target activation precedes trial start.";
            if (record.FirstTargetEntryOccurred && (!IsAvailable(record.FirstTargetEntryTimestamp) || record.FirstTargetEntryTimestamp < record.TargetActivationTimestamp)) return "First target entry timestamp is inconsistent.";
            if (!record.FirstTargetEntryOccurred && IsAvailable(record.FirstTargetEntryTimestamp)) return "First target entry timestamp exists without its event flag.";

            double outcomeTimestamp;
            if (record.Outcome == TrialOutcome.Success)
            {
                if (!record.TargetDwellCompleted || !IsAvailable(record.TargetDwellCompletedTimestamp)) return "Success requires target dwell completion.";
                if (!record.FirstTargetEntryOccurred || record.TargetDwellCompletedTimestamp < record.FirstTargetEntryTimestamp) return "Success target dwell timestamps are inconsistent.";
                if (record.TimeoutOccurred || IsAvailable(record.TimeoutTimestamp)) return "Success cannot contain a timeout timestamp.";
                outcomeTimestamp = record.TargetDwellCompletedTimestamp;
            }
            else if (record.Outcome == TrialOutcome.IncompleteTimeout)
            {
                if (!record.TimeoutOccurred || !IsAvailable(record.TimeoutTimestamp)) return "IncompleteTimeout requires a timeout timestamp.";
                if (record.TargetDwellCompleted || IsAvailable(record.TargetDwellCompletedTimestamp)) return "IncompleteTimeout cannot contain target dwell completion.";
                if (record.TimeoutTimestamp < record.TargetActivationTimestamp) return "Timeout precedes target activation.";
                outcomeTimestamp = record.TimeoutTimestamp;
            }
            else return "The completed trial outcome is unsupported.";

            if (record.ReturnCompletedTimestamp < outcomeTimestamp) return "Return completion precedes the final outcome event.";
            if (record.ReturnCompletedTimestamp < record.TrialStartTimestamp) return "Return completion precedes trial start.";
            return string.Empty;
        }

        private static bool IsAvailable(double value) => value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
