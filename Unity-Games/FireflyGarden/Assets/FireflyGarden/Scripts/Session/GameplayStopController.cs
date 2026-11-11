using System;
using FireflyGarden.Data;
using FireflyGarden.Gameplay;
using FireflyGarden.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using FireflyGarden.Results;
using FireflyGarden.Robot;

namespace FireflyGarden.Session
{
    public sealed class GameplayStopController : MonoBehaviour
    {
        [SerializeField] private string menuSceneName = "MainMenu";
        [SerializeField] private SingleFlowerCycleController cycleController;
        [SerializeField] private FireflyCursorController cursorController;
        [SerializeField] private SingleFlowerDataRecorder dataRecorder;
        [SerializeField] private DwellRingPresenter homeDwellRing;
        [SerializeField] private DwellRingPresenter targetDwellRing;
        [SerializeField] private HomeTargetGuidancePresenter guidancePresenter;
        private bool stopRequested;

        public void StopAndReturnToMenu()
        {
            if (stopRequested) return;
            stopRequested = true;
            RobotOutputSignals.RaiseExitPressed();
            cursorController.InputEnabled = false;
            cycleController.StopForSessionExit();
            homeDwellRing.ResetProgress(); homeDwellRing.Hide();
            targetDwellRing.ResetProgress(); targetDwellRing.Hide();
            guidancePresenter.SetVisible(false);
            dataRecorder.Close();
            SessionLaunchContext.Clear();
            SessionResultsContext.Clear();
            Time.timeScale = 1f;
            if (string.IsNullOrWhiteSpace(menuSceneName)) { Debug.LogError("Cannot return to menu: menu scene name is blank.", this); return; }
            try { SceneManager.LoadScene(menuSceneName); }
            catch (Exception exception) { Debug.LogError("Failed to load menu scene '" + menuSceneName + "': " + exception.Message, this); }
        }
    }
}
