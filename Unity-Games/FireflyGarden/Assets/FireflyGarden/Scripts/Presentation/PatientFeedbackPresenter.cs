using FireflyGarden.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FireflyGarden.Presentation
{
    public sealed class PatientFeedbackPresenter : MonoBehaviour
    {
        [SerializeField] private Text messageText;

        [Header("Patient Instructions")]
        [SerializeField] private string waitingAtHomeMessage = "Ready";
        [SerializeField] private string initialHomeDwellMessage = "Hold steady";
        [SerializeField] private string activeReachMessage = "Reach to the flower";
        [SerializeField] private string targetDwellMessage = "Hold steady";
        [SerializeField] private string targetExitCorrectionMessage = "Hold inside the flower";
        [SerializeField] private string successFeedbackMessage = "Target reached";
        [SerializeField] private string returnToHomeMessage = "Return to the centre";
        [SerializeField] private string returnDwellMessage = "Hold steady";
        [SerializeField] private string blockCompleteMessage = "Block complete";
        [SerializeField, Min(0f)] private float targetExitCorrectionDuration = 1.20f;
        private bool presentationPaused;

        private SingleFlowerState currentState;
        private float correctionTimeRemaining;
        private bool correctionIsActive;
        private bool blockIsComplete;

        public bool IsConfigured => messageText != null;

        private void Awake()
        {
            if (messageText == null)
            {
                Debug.LogError("PatientFeedbackPresenter requires an assigned Text component.", this);
                enabled = false;
                return;
            }

            Clear();
        }

        private void Update()
        {
            if (presentationPaused || !correctionIsActive)
            {
                return;
            }

            correctionTimeRemaining -= Time.deltaTime;
            if (correctionTimeRemaining > 0f)
            {
                return;
            }

            correctionIsActive = false;
            if (!blockIsComplete)
            {
                SetMessage(GetMessageForState(currentState));
            }
        }

        public void ShowMessageForState(SingleFlowerState state)
        {
            currentState = state;
            correctionIsActive = false;
            blockIsComplete = false;

            if (state == SingleFlowerState.CycleComplete)
            {
                Clear();
                return;
            }

            SetMessage(GetMessageForState(state));
        }

        public void ShowTargetExitCorrection()
        {
            if (blockIsComplete || currentState != SingleFlowerState.ActiveReach)
            {
                return;
            }

            correctionIsActive = true;
            correctionTimeRemaining = targetExitCorrectionDuration;
            SetMessage(targetExitCorrectionMessage);
        }

        public void ShowBlockComplete()
        {
            correctionIsActive = false;
            blockIsComplete = true;
            SetMessage(blockCompleteMessage);
        }

        public void ShowFindTheMatchInstruction()
        {
            currentState = SingleFlowerState.ActiveReach;
            correctionIsActive = false;
            blockIsComplete = false;
            SetMessage("Find the matching flower");
        }

        public void ShowRememberChoiceInstruction() => ShowInstruction("Remember and choose", SingleFlowerState.ActiveReach);
        public void ShowRememberCueInstruction() => ShowInstruction("Remember this", SingleFlowerState.RememberCueExposure);
        public void ShowRememberDelayInstruction() => ShowInstruction("Stay at the centre", SingleFlowerState.RememberMemoryDelay);

        private void ShowInstruction(string message, SingleFlowerState state)
        {
            currentState = state;
            correctionIsActive = false;
            blockIsComplete = false;
            SetMessage(message);
        }

        public void ShowIncorrectChoiceCorrection()
        {
            currentState = SingleFlowerState.ActiveReach;
            correctionIsActive = false;
            blockIsComplete = false;
            SetMessage("Look at the cue");
        }

        public void SetPaused(bool paused)
        {
            presentationPaused = paused;
            if (paused) SetMessage("Session paused");
            else if (!blockIsComplete) SetMessage(GetMessageForState(currentState));
        }

        public void Clear()
        {
            correctionIsActive = false;
            blockIsComplete = false;
            correctionTimeRemaining = 0f;
            messageText.text = string.Empty;
            messageText.enabled = false;
        }

        private string GetMessageForState(SingleFlowerState state)
        {
            switch (state)
            {
                case SingleFlowerState.WaitingAtHome: return waitingAtHomeMessage;
                case SingleFlowerState.InitialHomeDwell: return initialHomeDwellMessage;
                case SingleFlowerState.RememberCueExposure: return "Remember this";
                case SingleFlowerState.RememberMemoryDelay: return "Stay at the centre";
                case SingleFlowerState.ActiveReach: return activeReachMessage;
                case SingleFlowerState.TargetDwell: return targetDwellMessage;
                case SingleFlowerState.SuccessFeedback: return successFeedbackMessage;
                case SingleFlowerState.ReturnToHome: return returnToHomeMessage;
                case SingleFlowerState.ReturnDwell: return returnDwellMessage;
                default: return string.Empty;
            }
        }

        private void SetMessage(string message)
        {
            messageText.text = message;
            messageText.enabled = !string.IsNullOrEmpty(message);
        }
    }
}
