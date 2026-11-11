using System;
using FireflyGarden.Core;
using FireflyGarden.Configuration;
using FireflyGarden.Data;
using FireflyGarden.Presentation;
using UnityEngine;

namespace FireflyGarden.Gameplay
{
    public sealed class SingleFlowerCycleController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform fireflyTransform;
        [SerializeField] private LogicalFlowerRegion homeRegion;
        [SerializeField] private LogicalFlowerRegion targetRegion;
        [SerializeField] private SingleFlowerFeedbackPresenter feedbackPresenter;
        [SerializeField] private DwellRingPresenter homeDwellRing;
        [SerializeField] private DwellRingPresenter targetDwellRing;
        [SerializeField] private HomeTargetGuidancePresenter guidancePresenter;
        [SerializeField] private PatientFeedbackPresenter patientFeedbackPresenter;
        [SerializeField] private TargetSlot targetSlotA;
        [SerializeField] private TargetSlot targetSlotB;
        [SerializeField] private CueCardPresenter cuePresenter;
        [SerializeField] private TimeoutIndicatorPresenter timeoutIndicatorPresenter;
        [Header("Runtime Debugging")]
        [SerializeField] private SingleFlowerState currentState = SingleFlowerState.WaitingAtHome;
        [SerializeField] private SingleFlowerTrialRuntimeRecord currentRuntimeRecord;
        [SerializeField] private float timeoutElapsed;
        [SerializeField] private bool timeoutActive;

        private float initialHomeDwellDuration;
        private float targetDwellDuration;
        private float successFeedbackDuration;
        private float returnHomeDwellDuration;
        private float cueExposureDuration;
        private float memoryDelayDuration;
        private bool timeoutEnabled;
        private float timeoutDuration;
        private bool trialPrepared;
        private bool completionReported;
        private GameMode gameMode;
        private TargetSlot activeDwellSlot;
        private TargetSlot lockedIncorrectSlot;
        private TargetSlot correctionSlot;
        private bool correctionActive;
        private bool awaitingAnticipatoryReturn;

        public event Action<SingleFlowerTrialRuntimeRecord> TrialCompleted;
        public event Func<bool> TargetActivationRequested;
        public event Action<SingleFlowerTrialRuntimeRecord> AnticipatoryHomeExit;
        public SingleFlowerState CurrentState => currentState;
        public SingleFlowerTrialRuntimeRecord CurrentRuntimeRecord => currentRuntimeRecord;
        public bool TimeoutActive => timeoutActive;
        public float TimeoutElapsed => timeoutElapsed;
        public bool EvaluationPaused { get; set; }

        private readonly ContinuousDwellTimer initialHomeDwellTimer = new ContinuousDwellTimer();
        private readonly ContinuousDwellTimer targetDwellTimer = new ContinuousDwellTimer();
        private readonly ContinuousDwellTimer successFeedbackTimer = new ContinuousDwellTimer();
        private readonly ContinuousDwellTimer returnDwellTimer = new ContinuousDwellTimer();
        private readonly ContinuousDwellTimer cueExposureTimer = new ContinuousDwellTimer();
        private readonly ContinuousDwellTimer memoryDelayTimer = new ContinuousDwellTimer();

        private void Awake()
        {
            if (!HasValidSceneReferences())
            {
                Debug.LogError("SingleFlowerCycleController requires all scene references.", this);
                enabled = false;
                return;
            }
            currentState = SingleFlowerState.CycleComplete;
            ResetAllTimers();
            feedbackPresenter.SetTargetActive(false);
            ClearCyclePresentation();
        }

        private void Update()
        {
            if (trialPrepared && !EvaluationPaused) UpdateCurrentState();
            UpdateTimeoutIndicator();
        }

        private void UpdateCurrentState()
        {
            Vector3 fireflyPosition = fireflyTransform.position;
            switch (currentState)
            {
                case SingleFlowerState.WaitingAtHome:
                    if (homeRegion.Contains(fireflyPosition))
                    {
                        awaitingAnticipatoryReturn = false;
                        TransitionTo(SingleFlowerState.InitialHomeDwell);
                    }
                    break;
                case SingleFlowerState.InitialHomeDwell:
                    if (!homeRegion.Contains(fireflyPosition)) TransitionTo(SingleFlowerState.WaitingAtHome);
                    else
                    {
                        bool complete = initialHomeDwellTimer.Advance(Time.deltaTime, initialHomeDwellDuration);
                        homeDwellRing.SetProgress(initialHomeDwellTimer.GetNormalizedProgress(initialHomeDwellDuration));
                        if (complete) TransitionTo(gameMode == GameMode.RememberTheMatch
                            ? SingleFlowerState.RememberCueExposure : SingleFlowerState.ActiveReach);
                    }
                    break;
                case SingleFlowerState.RememberCueExposure:
                    if (!homeRegion.Contains(fireflyPosition)) { AbortRememberSequence("cue_exposure", fireflyPosition); break; }
                    if (cueExposureTimer.Advance(Time.deltaTime, cueExposureDuration))
                        TransitionTo(SingleFlowerState.RememberMemoryDelay);
                    break;
                case SingleFlowerState.RememberMemoryDelay:
                    if (!homeRegion.Contains(fireflyPosition)) { AbortRememberSequence("memory_delay", fireflyPosition); break; }
                    if (memoryDelayTimer.Advance(Time.deltaTime, memoryDelayDuration))
                        TransitionTo(SingleFlowerState.ActiveReach);
                    break;
                case SingleFlowerState.ActiveReach:
                    if (gameMode != GameMode.SingleFlower)
                    {
                        UpdateFindTheMatchChoice(fireflyPosition);
                        if (AdvanceAndCheckTimeout(Time.deltaTime)) HandleTimeout();
                        break;
                    }
                    if (feedbackPresenter.IsTargetActive && targetRegion.Contains(fireflyPosition))
                    {
                        currentRuntimeRecord.MarkFirstTargetEntry();
                        TransitionTo(SingleFlowerState.TargetDwell);
                    }
                    if ((currentState == SingleFlowerState.ActiveReach || currentState == SingleFlowerState.TargetDwell)
                        && AdvanceAndCheckTimeout(Time.deltaTime))
                        HandleTimeout();
                    break;
                case SingleFlowerState.TargetDwell:
                    if (gameMode != GameMode.SingleFlower)
                    {
                        UpdateFindTheMatchDwell(fireflyPosition);
                        if ((currentState == SingleFlowerState.ActiveReach || currentState == SingleFlowerState.TargetDwell)
                            && AdvanceAndCheckTimeout(Time.deltaTime)) HandleTimeout();
                        break;
                    }
                    bool exited = !targetRegion.Contains(fireflyPosition);
                    if (!feedbackPresenter.IsTargetActive || exited)
                    {
                        if (exited && feedbackPresenter.IsTargetActive)
                            currentRuntimeRecord.IncrementTargetExitCount();
                        TransitionTo(SingleFlowerState.ActiveReach);
                        if (exited) patientFeedbackPresenter.ShowTargetExitCorrection();
                    }
                    else
                    {
                        bool dwellComplete = targetDwellTimer.Advance(Time.deltaTime, targetDwellDuration);
                        targetDwellRing.SetProgress(targetDwellTimer.GetNormalizedProgress(targetDwellDuration));
                        // Valid dwell completion wins when both limits are reached on the same frame.
                        if (dwellComplete)
                        {
                            currentRuntimeRecord.MarkTargetDwellCompleted();
                            StopTimeout();
                            TransitionTo(SingleFlowerState.SuccessFeedback);
                            break;
                        }
                    }
                    if ((currentState == SingleFlowerState.ActiveReach || currentState == SingleFlowerState.TargetDwell)
                        && AdvanceAndCheckTimeout(Time.deltaTime)) HandleTimeout();
                    break;
                case SingleFlowerState.SuccessFeedback:
                    if (successFeedbackTimer.Advance(Time.deltaTime, successFeedbackDuration))
                        TransitionTo(SingleFlowerState.ReturnToHome);
                    break;
                case SingleFlowerState.ReturnToHome:
                    if (homeRegion.Contains(fireflyPosition)) TransitionTo(SingleFlowerState.ReturnDwell);
                    break;
                case SingleFlowerState.ReturnDwell:
                    if (!homeRegion.Contains(fireflyPosition)) TransitionTo(SingleFlowerState.ReturnToHome);
                    else
                    {
                        bool complete = returnDwellTimer.Advance(Time.deltaTime, returnHomeDwellDuration);
                        homeDwellRing.SetProgress(returnDwellTimer.GetNormalizedProgress(returnHomeDwellDuration));
                        if (complete)
                        {
                            currentRuntimeRecord.MarkReturnCompleted();
                            TransitionTo(SingleFlowerState.CycleComplete);
                        }
                    }
                    break;
            }
        }

        private void UpdateFindTheMatchChoice(Vector3 fireflyPosition)
        {
            if (lockedIncorrectSlot != null && !lockedIncorrectSlot.ContainsFireflyCentre(fireflyPosition))
            {
                lockedIncorrectSlot.SetSelectable(true);
                lockedIncorrectSlot = null;
            }
            if (correctionActive)
            {
                if (!successFeedbackTimer.Advance(Time.deltaTime, successFeedbackDuration)) return;
                correctionActive = false;
                correctionSlot.RestoreNormalVisual();
                correctionSlot.ResetDwellVisual();
                correctionSlot = null;
                if (gameMode == GameMode.RememberTheMatch) patientFeedbackPresenter.ShowRememberChoiceInstruction();
                else patientFeedbackPresenter.ShowFindTheMatchInstruction();
            }
            TargetSlot entered = GetEnteredSelectableSlot(fireflyPosition);
            if (entered == null) return;
            activeDwellSlot = entered;
            currentRuntimeRecord.MarkFirstTargetEntry();
            if (entered == targetSlotA) targetSlotB.ResetDwellVisual(); else targetSlotA.ResetDwellVisual();
            TransitionTo(SingleFlowerState.TargetDwell);
        }

        private TargetSlot GetEnteredSelectableSlot(Vector3 position)
        {
            if (targetSlotA.IsSelectable && targetSlotA.ContainsFireflyCentre(position)) return targetSlotA;
            if (targetSlotB.IsSelectable && targetSlotB.ContainsFireflyCentre(position)) return targetSlotB;
            return null;
        }

        private void UpdateFindTheMatchDwell(Vector3 fireflyPosition)
        {
            if (activeDwellSlot == null || !activeDwellSlot.ContainsFireflyCentre(fireflyPosition))
            {
                if (activeDwellSlot != null) { currentRuntimeRecord.IncrementTargetExitCount(); activeDwellSlot.ResetDwellVisual(); }
                activeDwellSlot = null;
                TransitionTo(SingleFlowerState.ActiveReach);
                return;
            }
            bool complete = targetDwellTimer.Advance(Time.deltaTime, targetDwellDuration);
            activeDwellSlot.ShowDwell(targetDwellTimer.GetNormalizedProgress(targetDwellDuration));
            if (!complete) return;

            currentRuntimeRecord.MarkCompletedChoice(activeDwellSlot.SlotIdentifier, activeDwellSlot.IsCorrect);
            if (activeDwellSlot.IsCorrect)
            {
                currentRuntimeRecord.MarkTargetDwellCompleted();
                StopTimeout();
                TransitionTo(SingleFlowerState.SuccessFeedback);
                return;
            }
            GameplayEvents.RaiseWrongFlowerSelected();
            lockedIncorrectSlot = activeDwellSlot;
            lockedIncorrectSlot.SetSelectable(false);
            lockedIncorrectSlot.ShowIncorrectFeedback();
            correctionSlot = lockedIncorrectSlot;
            activeDwellSlot = null;
            correctionActive = true;
            successFeedbackTimer.Reset();
            TransitionTo(SingleFlowerState.ActiveReach);
            if (gameMode == GameMode.RememberTheMatch) cuePresenter.Show(cuePresenter.CurrentStimulus);
            patientFeedbackPresenter.ShowIncorrectChoiceCorrection();
        }

        private void HandleTimeout()
        {
            currentRuntimeRecord.MarkTimeout();
            StopTimeout();
            TransitionTo(SingleFlowerState.ReturnToHome);
        }

        private void AbortRememberSequence(string phase, Vector3 position)
        {
            float elapsedCue = phase == "cue_exposure" ? cueExposureTimer.ElapsedTime : cueExposureDuration;
            currentRuntimeRecord.MarkAnticipatoryHomeExit(phase, position, elapsedCue);
            ResetAllTimers();
            ClearCyclePresentation();
            trialPrepared = false;
            awaitingAnticipatoryReturn = true;
            patientFeedbackPresenter.ShowMessageForState(SingleFlowerState.ReturnToHome);
            AnticipatoryHomeExit?.Invoke(currentRuntimeRecord);
        }

        private void TransitionTo(SingleFlowerState nextState)
        {
            ExitState(currentState);
            currentState = nextState;
            EnterState(nextState);
        }

        private void EnterState(SingleFlowerState state)
        {
            switch (state)
            {
                case SingleFlowerState.WaitingAtHome:
                    initialHomeDwellTimer.Reset(); feedbackPresenter.SetTargetActive(false); ClearCyclePresentation(); break;
                case SingleFlowerState.InitialHomeDwell:
                    initialHomeDwellTimer.Reset(); feedbackPresenter.SetTargetActive(false); ClearCyclePresentation(); homeDwellRing.Show(); break;
                case SingleFlowerState.RememberCueExposure:
                    cueExposureTimer.Reset();
                    feedbackPresenter.SetTargetActive(false);
                    HideFindTheMatchPresentation();
                    currentRuntimeRecord.MarkCueOnset();
                    cuePresenter.Show(cuePresenter.CurrentStimulus);
                    patientFeedbackPresenter.ShowRememberCueInstruction();
                    break;
                case SingleFlowerState.RememberMemoryDelay:
                    cueExposureTimer.Reset();
                    memoryDelayTimer.Reset();
                    currentRuntimeRecord.MarkCueOffset();
                    cuePresenter.Hide();
                    targetSlotA.Hide(); targetSlotB.Hide();
                    patientFeedbackPresenter.ShowRememberDelayInstruction();
                    break;
                case SingleFlowerState.ActiveReach:
                    initialHomeDwellTimer.Reset();
                    if (!currentRuntimeRecord.TargetActivated && TargetActivationRequested != null && !TargetActivationRequested())
                    {
                        trialPrepared = false;
                        feedbackPresenter.SetTargetActive(false);
                        ClearCyclePresentation();
                        return;
                    }
                    feedbackPresenter.SetTargetActive(true);
                    if (!currentRuntimeRecord.TargetActivated)
                    {
                        currentRuntimeRecord.MarkTargetActivated();
                        if (gameMode == GameMode.RememberTheMatch) currentRuntimeRecord.MarkChoiceOnset();
                        StartTimeout();
                    }
                    homeDwellRing.ResetProgress(); homeDwellRing.Hide();
                    targetDwellRing.ResetProgress(); targetDwellRing.Hide(); SetGuidanceVisibleForCurrentMode(true); break;
                case SingleFlowerState.TargetDwell:
                    targetDwellTimer.Reset();
                    if (gameMode != GameMode.SingleFlower) activeDwellSlot.ShowDwell(0f);
                    else { targetDwellRing.ResetProgress(); targetDwellRing.Show(); }
                    SetGuidanceVisibleForCurrentMode(true); break;
                case SingleFlowerState.SuccessFeedback:
                    successFeedbackTimer.Reset();
                    GameplayEvents.RaiseCorrectFlowerReached();
                    if (gameMode != GameMode.SingleFlower) { activeDwellSlot.ShowCompleted(); cuePresenter.Hide(); } else targetDwellRing.ShowCompleted();
                    SetGuidanceVisibleForCurrentMode(true); break;
                case SingleFlowerState.ReturnToHome:
                    feedbackPresenter.SetTargetActive(false); targetDwellRing.ResetProgress(); targetDwellRing.Hide();
                    if (gameMode != GameMode.SingleFlower) HideFindTheMatchPresentation();
                    homeDwellRing.ResetProgress(); homeDwellRing.Hide(); guidancePresenter.SetVisible(false); break;
                case SingleFlowerState.ReturnDwell:
                    returnDwellTimer.Reset(); homeDwellRing.ResetProgress(); homeDwellRing.Show(); break;
                case SingleFlowerState.CycleComplete:
                    ResetAllTimers(); feedbackPresenter.SetTargetActive(false); ClearCyclePresentation(); trialPrepared = false;
                    if (!completionReported)
                    {
                        completionReported = true;
                        TrialCompleted?.Invoke(currentRuntimeRecord);
                    }
                    break;
            }
            if (gameMode != GameMode.SingleFlower && state == SingleFlowerState.ActiveReach)
            {
                if (gameMode == GameMode.RememberTheMatch) patientFeedbackPresenter.ShowRememberChoiceInstruction();
                else patientFeedbackPresenter.ShowFindTheMatchInstruction();
            }
            else if (state == SingleFlowerState.RememberCueExposure || state == SingleFlowerState.RememberMemoryDelay) { }
            else if (state == SingleFlowerState.WaitingAtHome && awaitingAnticipatoryReturn)
                patientFeedbackPresenter.ShowMessageForState(SingleFlowerState.ReturnToHome);
            else
                patientFeedbackPresenter.ShowMessageForState(state);

            UpdateTimeoutIndicator();
        }

        private void ExitState(SingleFlowerState state)
        {
            if (state == SingleFlowerState.InitialHomeDwell) initialHomeDwellTimer.Reset();
            else if (state == SingleFlowerState.TargetDwell) targetDwellTimer.Reset();
            else if (state == SingleFlowerState.SuccessFeedback) successFeedbackTimer.Reset();
            else if (state == SingleFlowerState.ReturnDwell) returnDwellTimer.Reset();
            else if (state == SingleFlowerState.RememberCueExposure) cueExposureTimer.Reset();
            else if (state == SingleFlowerState.RememberMemoryDelay) memoryDelayTimer.Reset();
        }

        private void StartTimeout()
        {
            if (timeoutActive || !timeoutEnabled) return;
            timeoutElapsed = 0f;
            timeoutActive = true;
        }

        private void StopTimeout()
        {
            timeoutActive = false;
            UpdateTimeoutIndicator();
        }
        private void ResetTimeout()
        {
            timeoutElapsed = 0f;
            timeoutActive = false;
            UpdateTimeoutIndicator();
        }
        private bool AdvanceAndCheckTimeout(float deltaTime)
        {
            if (!timeoutActive) return false;
            timeoutElapsed += deltaTime;
            UpdateTimeoutIndicator();
            return timeoutElapsed >= timeoutDuration;
        }

        private void ResetAllTimers()
        {
            initialHomeDwellTimer.Reset(); targetDwellTimer.Reset(); successFeedbackTimer.Reset(); returnDwellTimer.Reset();
            cueExposureTimer.Reset(); memoryDelayTimer.Reset(); ResetTimeout();
        }

        public void ConfigureTimings(float initialHomeDuration, float targetDuration, float successDuration,
            float returnHomeDuration, bool configuredTimeoutEnabled, float configuredTimeoutDuration)
        {
            initialHomeDwellDuration = initialHomeDuration;
            targetDwellDuration = targetDuration;
            successFeedbackDuration = successDuration;
            returnHomeDwellDuration = returnHomeDuration;
            timeoutEnabled = configuredTimeoutEnabled;
            timeoutDuration = configuredTimeoutDuration;
            UpdateTimeoutIndicator();
        }

        public void ConfigureRememberTimings(float cueDuration, float delayDuration)
        {
            cueExposureDuration = cueDuration;
            memoryDelayDuration = delayDuration;
        }

        public void ConfigureGameMode(GameMode configuredGameMode)
        {
            gameMode = configuredGameMode;
            if (gameMode != GameMode.SingleFlower) guidancePresenter.SetVisible(false);
            UpdateTimeoutIndicator();
        }

        public void ConfigureFindTheMatchSlots(TargetSlot slotA, TargetSlot slotB, CueCardPresenter cue)
        {
            targetSlotA = slotA;
            targetSlotB = slotB;
            cuePresenter = cue;
        }

        private void SetGuidanceVisibleForCurrentMode(bool stateRequiresGuidance)
        {
            guidancePresenter.SetVisible(gameMode == GameMode.SingleFlower && stateRequiresGuidance);
        }

        public void PrepareNextTrial(SingleFlowerTrialRuntimeRecord runtimeRecord)
        {
            trialPrepared = false; completionReported = false; currentRuntimeRecord = runtimeRecord;
            ResetAllTimers(); feedbackPresenter.SetTargetActive(false); ClearCyclePresentation();
            UpdateTimeoutIndicator();
        }

        public void BeginPreparedTrial()
        {
            if (currentRuntimeRecord == null || initialHomeDwellDuration <= 0f || targetDwellDuration <= 0f
                || successFeedbackDuration <= 0f || returnHomeDwellDuration <= 0f)
            {
                Debug.LogError("SingleFlowerCycleController requires configured timings and a runtime record before a trial begins.", this);
                return;
            }
            currentRuntimeRecord.MarkTrialStarted();
            trialPrepared = true;
            TransitionTo(homeRegion.Contains(fireflyTransform.position) ? SingleFlowerState.InitialHomeDwell : SingleFlowerState.WaitingAtHome);
        }

        public void StopForBlockCompletion()
        {
            trialPrepared = false; feedbackPresenter.SetTargetActive(false); ResetAllTimers(); ClearCyclePresentation();
        }

        public void StopForSessionExit()
        {
            EvaluationPaused = true;
            trialPrepared = false;
            feedbackPresenter.SetTargetActive(false);
            ResetAllTimers();
            ClearCyclePresentation();
        }

        public void RestoreCurrentStateMessage()
        {
            if (gameMode != GameMode.SingleFlower && correctionActive) patientFeedbackPresenter.ShowIncorrectChoiceCorrection();
            else if (gameMode == GameMode.RememberTheMatch && currentState == SingleFlowerState.ActiveReach) patientFeedbackPresenter.ShowRememberChoiceInstruction();
            else if (gameMode == GameMode.FindTheMatch && currentState == SingleFlowerState.ActiveReach) patientFeedbackPresenter.ShowFindTheMatchInstruction();
            else patientFeedbackPresenter.ShowMessageForState(currentState);
        }

        private bool HasValidSceneReferences() => fireflyTransform != null && homeRegion != null && targetRegion != null
            && feedbackPresenter != null && feedbackPresenter.IsConfigured && homeDwellRing != null && homeDwellRing.IsConfigured
            && targetDwellRing != null && targetDwellRing.IsConfigured && guidancePresenter != null && guidancePresenter.IsConfigured
            && patientFeedbackPresenter != null && patientFeedbackPresenter.IsConfigured
            && timeoutIndicatorPresenter != null && timeoutIndicatorPresenter.IsConfigured;

        private void ClearCyclePresentation()
        {
            homeDwellRing.Hide(); targetDwellRing.Hide(); guidancePresenter.SetVisible(false); patientFeedbackPresenter.Clear();
            if (gameMode != GameMode.SingleFlower) HideFindTheMatchPresentation();
            if (timeoutIndicatorPresenter != null) timeoutIndicatorPresenter.ResetPresentation();
        }

        private void UpdateTimeoutIndicator()
        {
            if (timeoutIndicatorPresenter == null) return;

            bool cognitiveMode = gameMode == GameMode.FindTheMatch || gameMode == GameMode.RememberTheMatch;
            bool visible = trialPrepared && cognitiveMode && timeoutEnabled && IsTimeoutIndicatorState(currentState);
            if (!visible)
            {
                timeoutIndicatorPresenter.ResetPresentation();
                return;
            }

            float remainingSeconds = timeoutActive ? timeoutDuration - timeoutElapsed : timeoutDuration;
            remainingSeconds = Mathf.Clamp(remainingSeconds, 0f, timeoutDuration);
            timeoutIndicatorPresenter.SetRemainingSeconds(remainingSeconds);
            timeoutIndicatorPresenter.SetVisible(true);
        }

        private static bool IsTimeoutIndicatorState(SingleFlowerState state)
        {
            return state == SingleFlowerState.WaitingAtHome
                || state == SingleFlowerState.InitialHomeDwell
                || state == SingleFlowerState.RememberCueExposure
                || state == SingleFlowerState.RememberMemoryDelay
                || state == SingleFlowerState.ActiveReach
                || state == SingleFlowerState.TargetDwell;
        }

        private void HideFindTheMatchPresentation()
        {
            if (targetSlotA != null) { targetSlotA.Hide(); targetSlotA.RestoreNormalVisual(); }
            if (targetSlotB != null) { targetSlotB.Hide(); targetSlotB.RestoreNormalVisual(); }
            if (cuePresenter != null) cuePresenter.Hide();
            activeDwellSlot = lockedIncorrectSlot = correctionSlot = null;
            correctionActive = false;
        }
    }
}
