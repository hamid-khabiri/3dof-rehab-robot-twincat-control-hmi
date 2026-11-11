using System;
using FireflyGarden.Configuration;
using FireflyGarden.Generation;
using UnityEngine;
using FireflyGarden.Session;

namespace FireflyGarden.Data
{
    [Serializable]
    public sealed class SingleFlowerTrialRuntimeRecord
    {
        public const double UnavailableTimestamp = -1d;

        [SerializeField] private int trialIndexZeroBased;
        [SerializeField] private string trialId;
        [SerializeField] private string positionId;
        [SerializeField] private SingleFlowerDirection direction;
        [SerializeField] private TargetDistanceBand distanceBand;
        [SerializeField] private TrialOutcome outcome;
        [SerializeField] private int targetExitCount;
        [SerializeField] private bool targetActivated;
        [SerializeField] private bool firstTargetEntryOccurred;
        [SerializeField] private bool targetDwellCompleted;
        [SerializeField] private bool timeoutOccurred;
        [SerializeField] private bool returnCompleted;
        [SerializeField] private bool runtimePositionSwapOccurred;
        [SerializeField] private double trialStartTimestamp;
        [SerializeField] private double targetActivationTimestamp;
        [SerializeField] private double firstTargetEntryTimestamp;
        [SerializeField] private double targetDwellCompletedTimestamp;
        [SerializeField] private double timeoutTimestamp;
        [SerializeField] private double returnCompletedTimestamp;
        [SerializeField] private bool timeoutEnabled;
        [SerializeField] private float timeoutDuration;
        [SerializeField] private float initialHomeDwellDuration;
        [SerializeField] private float targetDwellDuration;
        [SerializeField] private float returnHomeDwellDuration;
        [SerializeField] private string mode;
        [SerializeField] private string correctStimulusId;
        [SerializeField] private string nonmatchingStimulusId;
        [SerializeField] private string correctTargetSlot;
        [SerializeField] private string firstSelectedTargetSlot;
        [SerializeField] private bool firstChoiceCorrect;
        [SerializeField] private bool firstChoiceRecorded;
        [SerializeField] private int confirmedIncorrectSelectionCount;
        [SerializeField] private double cueOnsetTimestamp = UnavailableTimestamp;
        [SerializeField] private double cueOffsetTimestamp = UnavailableTimestamp;
        [SerializeField] private double choiceOnsetTimestamp = UnavailableTimestamp;
        [SerializeField] private float cueExposureDuration;
        [SerializeField] private float memoryDelayDuration;
        [SerializeField] private string abortPhase;
        [SerializeField] private float anticipatoryExitX;
        [SerializeField] private float anticipatoryExitY;
        [SerializeField] private double anticipatoryExitTimestamp = UnavailableTimestamp;
        [SerializeField] private float elapsedCueExposureAtAbort;

        public int TrialIndexZeroBased => trialIndexZeroBased;
        public string TrialId => trialId;
        public string PositionId => positionId;
        public SingleFlowerDirection Direction => direction;
        public TargetDistanceBand DistanceBand => distanceBand;
        public TrialOutcome Outcome => outcome;
        public int TargetExitCount => targetExitCount;
        public bool TargetActivated => targetActivated;
        public bool FirstTargetEntryOccurred => firstTargetEntryOccurred;
        public bool TargetDwellCompleted => targetDwellCompleted;
        public bool TimeoutOccurred => timeoutOccurred;
        public bool ReturnCompleted => returnCompleted;
        public bool RuntimePositionSwapOccurred => runtimePositionSwapOccurred;
        public double TrialStartTimestamp => trialStartTimestamp;
        public double TargetActivationTimestamp => targetActivationTimestamp;
        public double FirstTargetEntryTimestamp => firstTargetEntryTimestamp;
        public double TargetDwellCompletedTimestamp => targetDwellCompletedTimestamp;
        public double TimeoutTimestamp => timeoutTimestamp;
        public double ReturnCompletedTimestamp => returnCompletedTimestamp;
        public bool TimeoutEnabled => timeoutEnabled;
        public float TimeoutDuration => timeoutDuration;
        public float InitialHomeDwellDuration => initialHomeDwellDuration;
        public float TargetDwellDuration => targetDwellDuration;
        public float ReturnHomeDwellDuration => returnHomeDwellDuration;
        public string Mode => mode;
        public string CorrectStimulusId => correctStimulusId;
        public string NonmatchingStimulusId => nonmatchingStimulusId;
        public string CorrectTargetSlot => correctTargetSlot;
        public string FirstSelectedTargetSlot => firstSelectedTargetSlot;
        public bool FirstChoiceCorrect => firstChoiceCorrect;
        public bool FirstChoiceRecorded => firstChoiceRecorded;
        public int ConfirmedIncorrectSelectionCount => confirmedIncorrectSelectionCount;
        public bool AssistedAfterError => confirmedIncorrectSelectionCount > 0;
        public double CueOnsetTimestamp => cueOnsetTimestamp;
        public double CueOffsetTimestamp => cueOffsetTimestamp;
        public double ChoiceOnsetTimestamp => choiceOnsetTimestamp;
        public float CueExposureDuration => cueExposureDuration;
        public float MemoryDelayDuration => memoryDelayDuration;
        public string AbortPhase => abortPhase;
        public float AnticipatoryExitX => anticipatoryExitX;
        public float AnticipatoryExitY => anticipatoryExitY;
        public double AnticipatoryExitTimestamp => anticipatoryExitTimestamp;
        public float ElapsedCueExposureAtAbort => elapsedCueExposureAtAbort;
        public string TargetAPositionId { get; private set; }
        public string TargetBPositionId { get; private set; }

        public void Initialize(SingleFlowerTrialDefinition trial, int scheduleIndex,
            bool wasPositionSwapped, bool configuredTimeoutEnabled, float configuredTimeoutDuration,
            float configuredInitialHomeDwell, float configuredTargetDwell, float configuredReturnDwell)
        {
            trialIndexZeroBased = scheduleIndex;
            trialId = trial.TrialId;
            positionId = trial.PositionId;
            direction = trial.Direction;
            distanceBand = trial.DistanceBand;
            outcome = TrialOutcome.None;
            targetExitCount = 0;
            targetActivated = false;
            firstTargetEntryOccurred = false;
            targetDwellCompleted = false;
            timeoutOccurred = false;
            returnCompleted = false;
            runtimePositionSwapOccurred = wasPositionSwapped;
            trialStartTimestamp = UnavailableTimestamp;
            targetActivationTimestamp = UnavailableTimestamp;
            firstTargetEntryTimestamp = UnavailableTimestamp;
            targetDwellCompletedTimestamp = UnavailableTimestamp;
            timeoutTimestamp = UnavailableTimestamp;
            returnCompletedTimestamp = UnavailableTimestamp;
            timeoutEnabled = configuredTimeoutEnabled;
            timeoutDuration = configuredTimeoutDuration;
            initialHomeDwellDuration = configuredInitialHomeDwell;
            targetDwellDuration = configuredTargetDwell;
            returnHomeDwellDuration = configuredReturnDwell;
            mode = GameMode.SingleFlower.ToString();
            correctStimulusId = nonmatchingStimulusId = correctTargetSlot = firstSelectedTargetSlot = string.Empty;
            firstChoiceCorrect = false;
            firstChoiceRecorded = false;
            confirmedIncorrectSelectionCount = 0;
            TargetAPositionId = TargetBPositionId = string.Empty;
            cueOnsetTimestamp = cueOffsetTimestamp = choiceOnsetTimestamp = UnavailableTimestamp;
            cueExposureDuration = memoryDelayDuration = 0f;
            abortPhase = string.Empty;
        }

        public void Initialize(FindTheMatchTrialDefinition trial, bool configuredTimeoutEnabled,
            float configuredTimeoutDuration, float configuredInitialHomeDwell,
            float configuredTargetDwell, float configuredReturnDwell)
        {
            var position = new SingleFlowerTrialDefinition(trial.TrialIndexZeroBased, trial.TrialId, trial.TargetAPosition);
            Initialize(position, trial.TrialIndexZeroBased, false, configuredTimeoutEnabled,
                configuredTimeoutDuration, configuredInitialHomeDwell, configuredTargetDwell, configuredReturnDwell);
            ConfigureFindTheMatch(trial.CorrectStimulus.StimulusId, trial.NonMatchingStimulus.StimulusId,
                trial.CorrectTargetSlot.ToString());
            TargetAPositionId = trial.TargetAPosition.PositionId;
            TargetBPositionId = trial.TargetBPosition.PositionId;
        }

        public void ConfigureRemember(float configuredCueExposure, float configuredMemoryDelay)
        {
            mode = GameMode.RememberTheMatch.ToString();
            cueExposureDuration = configuredCueExposure;
            memoryDelayDuration = configuredMemoryDelay;
        }

        internal void ConfigureFindTheMatch(string correctId, string nonmatchingId, string correctSlot)
        {
            mode = GameMode.FindTheMatch.ToString();
            correctStimulusId = correctId;
            nonmatchingStimulusId = nonmatchingId;
            correctTargetSlot = correctSlot;
        }

        internal void MarkCompletedChoice(string slot, bool correct)
        {
            if (!firstChoiceRecorded) { firstChoiceRecorded = true; firstSelectedTargetSlot = slot; firstChoiceCorrect = correct; }
            if (!correct) confirmedIncorrectSelectionCount++;
        }

        // This is the single replaceable monotonic desktop clock boundary.
        private static double CurrentTimestamp() => GameplayPauseController.PauseAwareTimestamp;

        internal void MarkTrialStarted() => trialStartTimestamp = CurrentTimestamp();
        internal void MarkCueOnset() => cueOnsetTimestamp = CurrentTimestamp();
        internal void MarkCueOffset() => cueOffsetTimestamp = CurrentTimestamp();
        internal void MarkChoiceOnset() => choiceOnsetTimestamp = CurrentTimestamp();
        internal void MarkAnticipatoryHomeExit(string phase, Vector2 position, float elapsedCueExposure)
        {
            abortPhase = phase;
            anticipatoryExitX = position.x;
            anticipatoryExitY = position.y;
            anticipatoryExitTimestamp = CurrentTimestamp();
            elapsedCueExposureAtAbort = elapsedCueExposure;
            outcome = TrialOutcome.AbortedAnticipatoryHomeExit;
        }

        internal void ReassignScheduledPosition(SingleFlowerTrialDefinition trial)
        {
            trialId = trial.TrialId;
            positionId = trial.PositionId;
            direction = trial.Direction;
            distanceBand = trial.DistanceBand;
            runtimePositionSwapOccurred = true;
        }

        internal void MarkTargetActivated()
        {
            if (targetActivated) return;
            targetActivated = true;
            targetActivationTimestamp = CurrentTimestamp();
        }

        internal void MarkFirstTargetEntry()
        {
            if (firstTargetEntryOccurred) return;
            firstTargetEntryOccurred = true;
            firstTargetEntryTimestamp = CurrentTimestamp();
        }

        internal void IncrementTargetExitCount() => targetExitCount++;

        internal void MarkTargetDwellCompleted()
        {
            if (outcome != TrialOutcome.None) return;
            targetDwellCompleted = true;
            targetDwellCompletedTimestamp = CurrentTimestamp();
            outcome = TrialOutcome.Success;
        }

        internal void MarkTimeout()
        {
            if (outcome != TrialOutcome.None) return;
            timeoutOccurred = true;
            timeoutTimestamp = CurrentTimestamp();
            outcome = TrialOutcome.IncompleteTimeout;
        }

        internal void MarkReturnCompleted()
        {
            if (returnCompleted) return;
            returnCompleted = true;
            returnCompletedTimestamp = CurrentTimestamp();
        }
    }
}
