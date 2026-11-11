using System;
using FireflyGarden.Gameplay;
using FireflyGarden.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace FireflyGarden.Session
{
    public sealed class GameplayPauseController : MonoBehaviour
    {
        [SerializeField] private SingleFlowerCycleController cycleController;
        [SerializeField] private FireflyCursorController cursorController;
        [SerializeField] private PatientFeedbackPresenter patientFeedbackPresenter;
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private Image pauseButtonIcon;
        [SerializeField] private Sprite pauseIcon;
        [SerializeField] private Sprite resumeIcon;

        private static GameplayPauseController activeInstance;
        private double currentPauseStartedTimestamp = -1d;
        private double cumulativePausedDuration;

        public event Action<bool> PausedChanged;
        public bool IsPaused { get; private set; }
        public double CurrentPauseStartedTimestamp => currentPauseStartedTimestamp;
        public double CumulativePausedDuration => cumulativePausedDuration;
        public static double PauseAwareTimestamp
        {
            get
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (activeInstance == null) return now;
                double activePause = activeInstance.IsPaused ? now - activeInstance.currentPauseStartedTimestamp : 0d;
                return now - activeInstance.cumulativePausedDuration - activePause;
            }
        }

        private void Awake()
        {
            activeInstance = this;
            IsPaused = false;
            currentPauseStartedTimestamp = -1d;
            cumulativePausedDuration = 0d;
            ApplyPresentation();
        }

        private void OnDestroy() { if (activeInstance == this) activeInstance = null; }
        public void TogglePause() { if (IsPaused) Resume(); else Pause(); }
        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            currentPauseStartedTimestamp = Time.realtimeSinceStartupAsDouble;
            cycleController.EvaluationPaused = true;
            cursorController.InputEnabled = false;
            patientFeedbackPresenter.SetPaused(true);
            ApplyPresentation();
        }

        public void Resume()
        {
            if (!IsPaused) return;
            cumulativePausedDuration += Time.realtimeSinceStartupAsDouble - currentPauseStartedTimestamp;
            currentPauseStartedTimestamp = -1d;
            IsPaused = false;
            cycleController.EvaluationPaused = false;
            cursorController.InputEnabled = true;
            patientFeedbackPresenter.SetPaused(false);
            cycleController.RestoreCurrentStateMessage();
            ApplyPresentation();
        }

        private void ApplyPresentation()
        {
            if (pauseOverlay != null) pauseOverlay.SetActive(IsPaused);
            if (pauseButtonIcon != null) pauseButtonIcon.sprite = IsPaused ? resumeIcon : pauseIcon;
            PausedChanged?.Invoke(IsPaused);
        }
    }
}
