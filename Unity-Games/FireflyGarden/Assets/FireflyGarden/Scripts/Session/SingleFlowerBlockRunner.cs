using System;
using System.Collections.Generic;
using System.Linq;
using FireflyGarden.Configuration;
using FireflyGarden.Data;
using FireflyGarden.Gameplay;
using FireflyGarden.Generation;
using FireflyGarden.Presentation;
using UnityEngine;

namespace FireflyGarden.Session
{
    public sealed class SingleFlowerBlockRunner : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private SingleFlowerBlockDefinition blockDefinition;
        [SerializeField] private DesktopGameplaySafeArea desktopGameplaySafeArea;
        [SerializeField, Min(0f)] private float homeTargetSafetyMargin = 0.10f;
        [SerializeField, Min(0f)] private float targetTargetSafetyMargin = 0.10f;
        [Header("Scene References")]
        [SerializeField] private Transform fireflyTransform;
        [SerializeField] private Transform homeTransform;
        [SerializeField] private Transform targetTransform;
        [SerializeField] private TargetSlot targetSlotA;
        [SerializeField] private TargetSlot targetSlotB;
        [SerializeField] private CueCardPresenter cuePresenter;
        [SerializeField] private StimulusDefinition cueStimulus;
        [SerializeField] private StimulusDefinition targetSlotAStimulus;
        [SerializeField] private StimulusDefinition targetSlotBStimulus;
        [SerializeField] private TargetPositionDefinition findTheMatchPositionA;
        [SerializeField] private TargetPositionDefinition findTheMatchPositionB;
        [SerializeField] private LogicalFlowerRegion homeRegion;
        [SerializeField] private LogicalFlowerRegion targetRegion;
        [SerializeField] private SingleFlowerCycleController cycleController;
        [SerializeField] private SingleFlowerFeedbackPresenter feedbackPresenter;
        [SerializeField] private HomeTargetGuidancePresenter guidancePresenter;
        [SerializeField] private BlockProgressPresenter progressPresenter;
        [SerializeField] private PatientFeedbackPresenter patientFeedbackPresenter;
        [SerializeField] private TrialProgressLanternPresenter lanternPresenter;
        [SerializeField] private SingleFlowerDataRecorder dataRecorder;
        [Header("Runtime Debugging")]
        [SerializeField] private SingleFlowerBlockState currentBlockState = SingleFlowerBlockState.NotStarted;
        [SerializeField] private int currentTrialIndexZeroBased = -1;
        [SerializeField] private int totalTrialCount;
        [SerializeField] private int completedTrialCount;
        [SerializeField] private int randomSeedUsed;
        [SerializeField] private string generatorVersion;
        [SerializeField] private SingleFlowerTrialRuntimeRecord currentRuntimeRecord;
        [SerializeField] private SingleFlowerTrialRuntimeRecord lastCompletedRuntimeRecord;
        [SerializeField] private string runtimeBlockedReason;
        [SerializeField] private string lastRecordedTrialId;

        private List<SingleFlowerTrialDefinition> trials;
        private List<FindTheMatchTrialDefinition> findTheMatchTrials;
        private FindTheMatchTrialDefinition currentFindTheMatchTrial;
        private Vector3 authoredTargetScale;

        public event Action<BlockCompletionReport> BlockCompleted;
        public SingleFlowerBlockState CurrentBlockState => currentBlockState;
        public int CurrentTrialIndexZeroBased => currentTrialIndexZeroBased;
        public int TotalTrialCount => totalTrialCount;
        public int CompletedTrialCount => completedTrialCount;
        public int RandomSeedUsed => randomSeedUsed;
        public IReadOnlyList<SingleFlowerTrialDefinition> Trials => trials;
        public SingleFlowerTrialRuntimeRecord LastCompletedRuntimeRecord => lastCompletedRuntimeRecord;
        public TrialOutcome CurrentOutcome => currentRuntimeRecord != null ? currentRuntimeRecord.Outcome : TrialOutcome.None;
        public string RuntimeBlockedReason => runtimeBlockedReason;
        public bool IsBlockRunning => currentBlockState == SingleFlowerBlockState.Running;
        public GameMode CurrentGameMode => blockDefinition != null ? blockDefinition.GameMode : GameMode.SingleFlower;

        private bool externallyControlled;
        private bool completionEventRaised;
        private int successfulTrialCount;
        private int incompleteTimeoutCount;
        private int abortedAttemptCount;
        private int scheduleIndex;

        private void OnEnable()
        {
            if (cycleController != null)
            {
                cycleController.TrialCompleted += HandleTrialCompleted;
                cycleController.TargetActivationRequested += HandleTargetActivationRequested;
                cycleController.AnticipatoryHomeExit += HandleAnticipatoryHomeExit;
            }
        }

        private void OnDisable()
        {
            if (cycleController != null)
            {
                cycleController.TrialCompleted -= HandleTrialCompleted;
                cycleController.TargetActivationRequested -= HandleTargetActivationRequested;
                cycleController.AnticipatoryHomeExit -= HandleAnticipatoryHomeExit;
            }
        }

        private void Start()
        {
            authoredTargetScale = targetTransform != null ? targetTransform.localScale : Vector3.one;
            if (!externallyControlled) StartBlock();
        }

        public void TakeSessionControl() => externallyControlled = true;

        public void ConfigureBlock(SingleFlowerBlockDefinition definition, int blockScheduleIndex)
        {
            if (IsBlockRunning) throw new InvalidOperationException("A running block cannot be reconfigured.");
            blockDefinition = definition;
            scheduleIndex = blockScheduleIndex;
        }

        public bool PrepareBlock()
        {
            ResetBlockRuntimeAndPresentation();
            return blockDefinition != null;
        }

        public void StartConfiguredBlock()
        {
            if (blockDefinition == null || IsBlockRunning) return;
            if (authoredTargetScale == Vector3.zero)
                authoredTargetScale = targetTransform != null ? targetTransform.localScale : Vector3.one;
            StartBlock();
        }

        public void ClearForSessionTransition()
        {
            cycleController.StopForBlockCompletion();
            feedbackPresenter.SetTargetActive(false);
            guidancePresenter.SetVisible(false);
            cuePresenter.Hide();
            targetSlotA.Hide();
            targetSlotB.Hide();
            targetSlotA.RestoreNormalVisual();
            targetSlotB.RestoreNormalVisual();
            targetSlotA.ResetDwellRing();
            targetSlotB.ResetDwellRing();
            progressPresenter.Clear();
            patientFeedbackPresenter.Clear();
        }

        private void ResetBlockRuntimeAndPresentation()
        {
            ClearForSessionTransition();
            currentBlockState = SingleFlowerBlockState.NotStarted;
            currentTrialIndexZeroBased = -1;
            totalTrialCount = 0;
            completedTrialCount = 0;
            successfulTrialCount = 0;
            incompleteTimeoutCount = 0;
            abortedAttemptCount = 0;
            completionEventRaised = false;
            currentRuntimeRecord = null;
            lastCompletedRuntimeRecord = null;
            lastRecordedTrialId = string.Empty;
            runtimeBlockedReason = string.Empty;
            trials = null;
            findTheMatchTrials = null;
            currentFindTheMatchTrial = null;
            lanternPresenter.Clear();
        }

        private void StartBlock()
        {
            currentBlockState = SingleFlowerBlockState.Validating;
            runtimeBlockedReason = string.Empty;
            List<string> errors = ValidateConfigurationAndReferences();
            if (blockDefinition != null && blockDefinition.GameMode != GameMode.SingleFlower)
            {
                int effectiveSeed = blockDefinition.RandomizeSeedEachRun
                    ? GenerateNonzeroRuntimeSeed()
                    : blockDefinition.RandomSeed;
                FindTheMatchTrialGenerationResult result = new FindTheMatchTrialGenerator().Generate(blockDefinition,
                    effectiveSeed,
                    desktopGameplaySafeArea, homeRegion.InteractionRadius, targetSlotA.LogicalRegion.InteractionRadius,
                    targetSlotB.LogicalRegion.InteractionRadius, homeTargetSafetyMargin, targetTargetSafetyMargin);
                errors.AddRange(result.Errors);
                foreach (string warning in result.Warnings) Debug.LogWarning(warning, this);
                findTheMatchTrials = new List<FindTheMatchTrialDefinition>(result.Trials);
                randomSeedUsed = result.RandomSeedUsed; generatorVersion = result.GeneratorVersion;
            }
            else
            {
                SingleFlowerTrialGenerationResult result = new SingleFlowerTrialGenerator().Generate(blockDefinition);
                errors.AddRange(result.Errors); trials = new List<SingleFlowerTrialDefinition>(result.Trials);
                randomSeedUsed = result.RandomSeedUsed; generatorVersion = result.GeneratorVersion;
            }
            if (errors.Count > 0)
            {
                EnterInvalidConfiguration(errors);
                return;
            }

            totalTrialCount = blockDefinition.GameMode != GameMode.SingleFlower ? findTheMatchTrials.Count : trials.Count;
            completedTrialCount = 0;
            lanternPresenter.Initialize(totalTrialCount);
            lanternPresenter.SetCompletedCount(0);
            cycleController.ConfigureTimings(blockDefinition.InitialHomeDwellDuration,
                blockDefinition.TargetDwellDuration, blockDefinition.SuccessFeedbackDuration,
                blockDefinition.ReturnHomeDwellDuration, blockDefinition.TimeoutEnabled, blockDefinition.TimeoutDuration);
            cycleController.ConfigureGameMode(blockDefinition.GameMode);
            cycleController.ConfigureRememberTimings(blockDefinition.CueExposureDuration, blockDefinition.MemoryDelayDuration);
            cycleController.ConfigureFindTheMatchSlots(targetSlotA, targetSlotB, cuePresenter);
            targetSlotB.Hide();
            cuePresenter.Hide();
            guidancePresenter.SetBlockGuidanceEnabled(blockDefinition.GuidanceEnabled);
            currentBlockState = SingleFlowerBlockState.Ready;
            ApplyAndBeginTrial(0);
        }

        private static int GenerateNonzeroRuntimeSeed()
        {
            int seed;
            do seed = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
            while (seed == 0);
            return seed;
        }

        private List<string> ValidateConfigurationAndReferences()
        {
            var errors = new List<string>();
            if (blockDefinition == null) errors.Add("SingleFlowerBlockRunner requires a block definition.");
            if (desktopGameplaySafeArea == null) errors.Add("SingleFlowerBlockRunner requires a desktop gameplay safe area.");
            if (fireflyTransform == null) errors.Add("SingleFlowerBlockRunner requires the Firefly transform.");
            if (homeTransform == null) errors.Add("SingleFlowerBlockRunner requires HomeFlower.");
            if (targetTransform == null) errors.Add("SingleFlowerBlockRunner requires TargetFlower.");
            if (targetSlotA == null || targetSlotB == null) errors.Add("SingleFlowerBlockRunner requires two target slots.");
            else if (!targetSlotA.IsConfigured || !targetSlotB.IsConfigured) errors.Add("Both target slots require centre, renderer, logical region, dwell ring, and stable identifier references.");
            if (cuePresenter == null) errors.Add("SingleFlowerBlockRunner requires a cue presenter.");
            if (homeRegion == null) errors.Add("SingleFlowerBlockRunner requires the home logical region.");
            if (targetRegion == null) errors.Add("SingleFlowerBlockRunner requires the target logical region.");
            if (cycleController == null) errors.Add("SingleFlowerBlockRunner requires SingleFlowerCycleController.");
            if (feedbackPresenter == null || !feedbackPresenter.IsConfigured) errors.Add("SingleFlowerBlockRunner requires a configured target feedback presenter.");
            if (guidancePresenter == null || !guidancePresenter.IsConfigured) errors.Add("SingleFlowerBlockRunner requires configured guidance.");
            if (progressPresenter == null || !progressPresenter.IsConfigured) errors.Add("SingleFlowerBlockRunner requires configured block progress presentation.");
            if (patientFeedbackPresenter == null || !patientFeedbackPresenter.IsConfigured) errors.Add("SingleFlowerBlockRunner requires configured patient feedback presentation.");
            if (lanternPresenter == null || !lanternPresenter.IsConfigured) errors.Add("SingleFlowerBlockRunner requires configured lantern progress presentation.");
            if (dataRecorder == null) errors.Add("SingleFlowerBlockRunner requires a data recorder.");
            else
            {
                if (!dataRecorder.IsInitialized) errors.Add("The data recorder did not initialize: " + dataRecorder.LastError);
                if (string.IsNullOrWhiteSpace(dataRecorder.SessionId)) errors.Add("The data recorder has no session ID.");
                if (string.IsNullOrWhiteSpace(dataRecorder.OutputFilePath)) errors.Add("The data recorder has no output path.");
                if (!dataRecorder.IsAcceptingRows) errors.Add("The data recorder is not accepting rows.");
            }
            if (homeTargetSafetyMargin < 0f || !IsFinite(homeTargetSafetyMargin)) errors.Add("homeTargetSafetyMargin must be finite and non-negative.");
            if (targetTargetSafetyMargin < 0f || !IsFinite(targetTargetSafetyMargin)) errors.Add("targetTargetSafetyMargin must be finite and non-negative.");
            if (blockDefinition == null) return errors;

            if (blockDefinition.TimeoutEnabled && (!IsFinite(blockDefinition.TimeoutDuration)
                || blockDefinition.TimeoutDuration < 10f || blockDefinition.TimeoutDuration > 60f))
                errors.Add("Enabled timeoutDuration must be finite and within the supported range of 10.0 to 60.0 seconds.");
            if (!IsFinite(blockDefinition.InitialHomeDwellDuration) || !IsFinite(blockDefinition.TargetDwellDuration)
                || !IsFinite(blockDefinition.SuccessFeedbackDuration) || !IsFinite(blockDefinition.ReturnHomeDwellDuration))
                errors.Add("All block timing durations must be finite.");
            if (blockDefinition.GameMode == GameMode.RememberTheMatch
                && (!IsFinite(blockDefinition.CueExposureDuration) || blockDefinition.CueExposureDuration < 1f
                    || blockDefinition.CueExposureDuration > 3f || !IsFinite(blockDefinition.MemoryDelayDuration)
                    || blockDefinition.MemoryDelayDuration < 0.5f || blockDefinition.MemoryDelayDuration > 3f))
                errors.Add("Remember cue exposure must be 1.0-3.0 seconds and memory delay must be 0.5-3.0 seconds.");
            if (!IsFinite(blockDefinition.TargetScaleMultiplier)) errors.Add("targetScaleMultiplier must be finite.");
            if (desktopGameplaySafeArea != null) ValidateSafeAreaDefinition(errors);
            if (homeRegion != null && targetRegion != null && desktopGameplaySafeArea != null)
                ValidateAssignedPositions(errors);
            return errors;
        }

        private void ValidateFindTheMatchPreview(List<string> errors)
        {
            if (cueStimulus == null || targetSlotAStimulus == null || targetSlotBStimulus == null)
                errors.Add("Find the Match preview requires cue and target stimuli.");
            else if ((cueStimulus == targetSlotAStimulus) == (cueStimulus == targetSlotBStimulus))
                errors.Add("Exactly one Find the Match target stimulus must match the cue stimulus.");

            if (findTheMatchPositionA == null || findTheMatchPositionB == null
                || findTheMatchPositionA == findTheMatchPositionB)
            {
                errors.Add("Find the Match preview requires two different position definitions.");
                return;
            }

            if (!blockDefinition.EnabledPositions.Contains(findTheMatchPositionA)
                || !blockDefinition.EnabledPositions.Contains(findTheMatchPositionB))
                errors.Add("Find the Match preview positions must be enabled in the existing position library.");
            if (findTheMatchPositionA.DistanceBand != findTheMatchPositionB.DistanceBand)
                errors.Add("Find the Match preview positions must use the same distance band.");
            float targetSeparation = Vector2.Distance(findTheMatchPositionA.LocalOffsetFromHome,
                findTheMatchPositionB.LocalOffsetFromHome);
            if (targetRegion != null && targetSlotB != null
                && targetSeparation <= targetRegion.InteractionRadius + targetSlotB.LogicalRegion.InteractionRadius)
                errors.Add("Find the Match preview positions overlap logical target regions.");
        }

        private void ValidateSafeAreaDefinition(List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(desktopGameplaySafeArea.SafeAreaId)) errors.Add("Desktop safeAreaId must be non-empty.");
            if (!IsFinite(desktopGameplaySafeArea.MinimumXOffsetFromHome)
                || !IsFinite(desktopGameplaySafeArea.MaximumXOffsetFromHome)
                || !IsFinite(desktopGameplaySafeArea.MinimumYOffsetFromHome)
                || !IsFinite(desktopGameplaySafeArea.MaximumYOffsetFromHome)
                || !IsFinite(desktopGameplaySafeArea.TargetVisualMargin))
                errors.Add("Desktop safe-area bounds and margin must be finite.");
            else if (desktopGameplaySafeArea.MinimumXOffsetFromHome >= desktopGameplaySafeArea.MaximumXOffsetFromHome
                || desktopGameplaySafeArea.MinimumYOffsetFromHome >= desktopGameplaySafeArea.MaximumYOffsetFromHome
                || desktopGameplaySafeArea.TargetVisualMargin < 0f)
                errors.Add("Desktop safe-area bounds must be ordered and targetVisualMargin must be non-negative.");
        }

        private void ValidateAssignedPositions(List<string> errors)
        {
            if (blockDefinition.EnabledPositions == null) return;
            float requiredSeparation = homeRegion.InteractionRadius + targetRegion.InteractionRadius + homeTargetSafetyMargin;
            foreach (TargetPositionDefinition position in blockDefinition.EnabledPositions)
            {
                if (position == null) continue;
                Vector2 offset = position.LocalOffsetFromHome;
                if (offset.magnitude <= requiredSeparation)
                    errors.Add($"Position {position.PositionId} overlaps HomeFlower logical space; distance must exceed {requiredSeparation:0.###}.");
                if (!desktopGameplaySafeArea.ContainsCircle(offset, targetRegion.InteractionRadius))
                    errors.Add($"Position {position.PositionId} does not keep the complete target and visual margin inside desktop safe area {desktopGameplaySafeArea.SafeAreaId}.");
            }
        }

        private void ApplyAndBeginTrial(int index)
        {
            feedbackPresenter.SetTargetActive(false);
            guidancePresenter.SetVisible(false);
            if (blockDefinition.GameMode != GameMode.SingleFlower)
            {
                currentTrialIndexZeroBased = index;
                currentFindTheMatchTrial = findTheMatchTrials[index];
                currentRuntimeRecord = new SingleFlowerTrialRuntimeRecord();
                currentRuntimeRecord.Initialize(currentFindTheMatchTrial, blockDefinition.TimeoutEnabled,
                    blockDefinition.TimeoutDuration, blockDefinition.InitialHomeDwellDuration,
                    blockDefinition.TargetDwellDuration, blockDefinition.ReturnHomeDwellDuration);
                if (blockDefinition.GameMode == GameMode.RememberTheMatch)
                    currentRuntimeRecord.ConfigureRemember(blockDefinition.CueExposureDuration, blockDefinition.MemoryDelayDuration);
                cuePresenter.Show(currentFindTheMatchTrial.CorrectStimulus);
                cuePresenter.Hide();
                targetSlotA.Hide(); targetSlotB.Hide(); cuePresenter.Hide();
                targetSlotA.RestoreNormalVisual(); targetSlotB.RestoreNormalVisual();
                targetSlotA.ResetDwellRing(); targetSlotB.ResetDwellRing();
                cycleController.PrepareNextTrial(currentRuntimeRecord);
                progressPresenter.ShowProgress(index + 1, totalTrialCount);
                currentBlockState = SingleFlowerBlockState.Running;
                cycleController.BeginPreparedTrial();
                return;
            }
            bool swapped;
            if (!ResolveUnoccupiedCandidate(index, out swapped)) return;

            currentTrialIndexZeroBased = index;
            SingleFlowerTrialDefinition trial = trials[index];
            Vector3 offset = new Vector3(trial.LocalOffsetFromHome.x, trial.LocalOffsetFromHome.y, 0f);
            targetTransform.position = homeTransform.position + offset;
            targetTransform.localScale = authoredTargetScale * blockDefinition.TargetScaleMultiplier;
            currentRuntimeRecord = new SingleFlowerTrialRuntimeRecord();
            currentRuntimeRecord.Initialize(trial, index, swapped, blockDefinition.TimeoutEnabled,
                blockDefinition.TimeoutDuration, blockDefinition.InitialHomeDwellDuration,
                blockDefinition.TargetDwellDuration, blockDefinition.ReturnHomeDwellDuration);
            cycleController.PrepareNextTrial(currentRuntimeRecord);
            progressPresenter.ShowProgress(index + 1, totalTrialCount);
            currentBlockState = SingleFlowerBlockState.Running;
            cycleController.BeginPreparedTrial();
        }

        private bool ResolveUnoccupiedCandidate(int index, out bool swapped)
        {
            swapped = false;
            if (!IsCandidateOccupied(trials[index])) return true;

            // Search pending trials in their existing order; accept only a swap that preserves all adjacency rules.
            for (int candidateIndex = index + 1; candidateIndex < trials.Count; candidateIndex++)
            {
                if (IsCandidateOccupied(trials[candidateIndex])) continue;
                SwapTrials(index, candidateIndex);
                if (ScheduleStillSatisfiesConstraints())
                {
                    swapped = true;
                    return true;
                }
                SwapTrials(index, candidateIndex);
            }

            runtimeBlockedReason = $"No deterministic unoccupied target can be scheduled for pending trial {index + 1}.";
            currentBlockState = SingleFlowerBlockState.InvalidConfiguration;
            cycleController.StopForBlockCompletion();
            feedbackPresenter.SetTargetActive(false);
            guidancePresenter.SetVisible(false);
            Debug.LogError("Single Flower runtime blocked: " + runtimeBlockedReason, this);
            return false;
        }

        private bool HandleTargetActivationRequested()
        {
            if (currentBlockState != SingleFlowerBlockState.Running) return false;
            if (blockDefinition.GameMode != GameMode.SingleFlower)
            {
                if (!PrepareFindTheMatchPreview()) return false;
                return true;
            }
            bool swapped;
            if (!ResolveUnoccupiedCandidate(currentTrialIndexZeroBased, out swapped)) return false;
            if (!swapped) return true;

            SingleFlowerTrialDefinition trial = trials[currentTrialIndexZeroBased];
            Vector3 offset = new Vector3(trial.LocalOffsetFromHome.x, trial.LocalOffsetFromHome.y, 0f);
            targetTransform.position = homeTransform.position + offset;
            currentRuntimeRecord.ReassignScheduledPosition(trial);
            return true;
        }

        private bool PrepareFindTheMatchPreview()
        {
            if (currentFindTheMatchTrial == null)
            {
                Debug.LogError("The pre-generated Find the Match trial is missing.", this);
                return false;
            }

            Vector3 positionA = homeTransform.position + (Vector3)currentFindTheMatchTrial.TargetAPosition.LocalOffsetFromHome;
            Vector3 positionB = homeTransform.position + (Vector3)currentFindTheMatchTrial.TargetBPosition.LocalOffsetFromHome;
            targetSlotB.transform.localScale = targetSlotA.transform.localScale;
            bool aCorrect = currentFindTheMatchTrial.CorrectTargetSlot == TargetSlotId.A;
            targetSlotA.Configure(aCorrect ? currentFindTheMatchTrial.CorrectStimulus : currentFindTheMatchTrial.NonMatchingStimulus, aCorrect, positionA);
            targetSlotB.Configure(aCorrect ? currentFindTheMatchTrial.NonMatchingStimulus : currentFindTheMatchTrial.CorrectStimulus, !aCorrect, positionB);
            if (blockDefinition.GameMode == GameMode.FindTheMatch) cuePresenter.Show(currentFindTheMatchTrial.CorrectStimulus);
            else cuePresenter.Hide();
            targetSlotA.Show(true);
            targetSlotB.Show(true);
            guidancePresenter.SetVisible(false);
            if (blockDefinition.GameMode == GameMode.FindTheMatch) patientFeedbackPresenter.ShowFindTheMatchInstruction();
            else patientFeedbackPresenter.ShowRememberChoiceInstruction();
            return true;
        }

        private bool IsCandidateOccupied(SingleFlowerTrialDefinition trial)
        {
            Vector3 centre = homeTransform.position + new Vector3(trial.LocalOffsetFromHome.x, trial.LocalOffsetFromHome.y, 0f);
            float x = fireflyTransform.position.x - centre.x;
            float y = fireflyTransform.position.y - centre.y;
            return x * x + y * y <= targetRegion.InteractionRadius * targetRegion.InteractionRadius;
        }

        private void SwapTrials(int first, int second)
        {
            SingleFlowerTrialDefinition temporary = trials[first];
            trials[first] = trials[second];
            trials[second] = temporary;
        }

        private bool ScheduleStillSatisfiesConstraints()
        {
            for (int i = 1; i < trials.Count; i++)
                if (blockDefinition.AvoidImmediatePositionRepeat && trials[i - 1].PositionId == trials[i].PositionId) return false;
            int run = 1;
            for (int i = 1; i < trials.Count; i++)
            {
                run = trials[i].Direction == trials[i - 1].Direction ? run + 1 : 1;
                if (run > blockDefinition.MaximumConsecutiveSameDirection) return false;
            }
            return true;
        }

        private void HandleTrialCompleted(SingleFlowerTrialRuntimeRecord completedRecord)
        {
            if (currentBlockState != SingleFlowerBlockState.Running || completedRecord == null || !completedRecord.ReturnCompleted) return;
            if (string.Equals(lastRecordedTrialId, completedRecord.TrialId, StringComparison.Ordinal))
            {
                BlockForRecordingFailure("Duplicate completion payload was rejected for trial " + completedRecord.TrialId + ".");
                return;
            }

            TrialSummary summary;
            string error;
            if (!TrialSummary.TryFromRuntimeRecord(completedRecord, dataRecorder.SessionId, blockDefinition,
                randomSeedUsed, generatorVersion, out summary, out error))
            {
                BlockForRecordingFailure("The completed trial record is invalid: " + error, true);
                return;
            }
            if (!dataRecorder.TryRecord(summary, out error))
            {
                BlockForRecordingFailure("The completed trial could not be persisted: " + error, false);
                return;
            }

            lastCompletedRuntimeRecord = completedRecord;
            lastRecordedTrialId = completedRecord.TrialId;
            if (completedRecord.Outcome == TrialOutcome.Success) successfulTrialCount++;
            else if (completedRecord.Outcome == TrialOutcome.IncompleteTimeout) incompleteTimeoutCount++;
            // Persist before progress so the patient-visible state can never advance beyond durable research data.
            completedTrialCount++;
            lanternPresenter.SetCompletedCount(completedTrialCount);
            if (completedTrialCount >= totalTrialCount) CompleteBlock();
            else ApplyAndBeginTrial(currentTrialIndexZeroBased + 1);
        }

        private void HandleAnticipatoryHomeExit(SingleFlowerTrialRuntimeRecord abortedRecord)
        {
            if (currentBlockState != SingleFlowerBlockState.Running || abortedRecord == null
                || abortedRecord.Outcome != TrialOutcome.AbortedAnticipatoryHomeExit) return;

            TrialSummary summary;
            string error;
            if (!TrialSummary.TryFromRuntimeRecord(abortedRecord, dataRecorder.SessionId, blockDefinition,
                randomSeedUsed, generatorVersion, out summary, out error) || !dataRecorder.TryRecord(summary, out error))
            {
                BlockForRecordingFailure("The anticipatory exit could not be persisted: " + error, false);
                return;
            }

            abortedAttemptCount++;

            FindTheMatchTrialDefinition source = findTheMatchTrials[(currentTrialIndexZeroBased + 1) % findTheMatchTrials.Count];
            int replacementIndex = findTheMatchTrials.Count;
            string replacementId = $"{blockDefinition.BlockId}_replacement_{replacementIndex + 1:D3}_{source.CorrectStimulus.StimulusId}";
            findTheMatchTrials.Add(new FindTheMatchTrialDefinition(replacementIndex, replacementId,
                source.CorrectStimulus, source.NonMatchingStimulus, source.CorrectTargetSlot,
                source.TargetAPosition, source.TargetBPosition));
            ApplyAndBeginTrial(currentTrialIndexZeroBased + 1);
        }

        private void CompleteBlock()
        {
            cycleController.StopForBlockCompletion(); feedbackPresenter.SetTargetActive(false); guidancePresenter.SetVisible(false);
            currentTrialIndexZeroBased = totalTrialCount - 1; currentBlockState = SingleFlowerBlockState.Complete;
            lanternPresenter.SetCompletedCount(totalTrialCount); patientFeedbackPresenter.ShowBlockComplete();
            progressPresenter.ShowBlockComplete(totalTrialCount);
            if (!completionEventRaised)
            {
                completionEventRaised = true;
                BlockCompleted?.Invoke(new BlockCompletionReport
                {
                    blockDefinitionId = blockDefinition.BlockId,
                    gameMode = blockDefinition.GameMode,
                    formalTrialsCompleted = completedTrialCount,
                    successfulTrials = successfulTrialCount,
                    incompleteTimeouts = incompleteTimeoutCount,
                    abortedAttempts = abortedAttemptCount,
                    blockCompletedNormally = true,
                    scheduleIndex = scheduleIndex
                });
            }
        }

        private void BlockForRecordingFailure(string reason, bool logError = true)
        {
            runtimeBlockedReason = reason;
            currentBlockState = SingleFlowerBlockState.InvalidConfiguration;
            cycleController.StopForBlockCompletion();
            feedbackPresenter.SetTargetActive(false);
            guidancePresenter.SetVisible(false);
            if (logError) Debug.LogError("Single Flower runtime blocked: " + runtimeBlockedReason, this);
        }

        private void EnterInvalidConfiguration(List<string> errors)
        {
            currentBlockState = SingleFlowerBlockState.InvalidConfiguration;
            if (cycleController != null) cycleController.StopForBlockCompletion();
            if (feedbackPresenter != null && feedbackPresenter.IsConfigured) feedbackPresenter.SetTargetActive(false);
            if (guidancePresenter != null && guidancePresenter.IsConfigured) guidancePresenter.SetVisible(false);
            if (progressPresenter != null) progressPresenter.Clear();
            if (patientFeedbackPresenter != null && patientFeedbackPresenter.IsConfigured) patientFeedbackPresenter.Clear();
            if (dataRecorder != null) dataRecorder.Close();
            Debug.LogError("Single Flower block could not start:\n- " + string.Join("\n- ", errors), this);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
