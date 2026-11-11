using FireflyGarden.Configuration;
using FireflyGarden.Core;
using FireflyGarden.Data;
using FireflyGarden.Gameplay;
using FireflyGarden.Presentation;
using FireflyGarden.Results;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FireflyGarden.Session
{
    [DefaultExecutionOrder(-100)]
    public sealed class SessionController : MonoBehaviour
    {
        [SerializeField] private SessionDefinition fallbackSession;
        [SerializeField] private SingleFlowerBlockRunner blockRunner;
        [SerializeField] private BlockTransitionPresenter transitionPresenter;
        [SerializeField] private SingleFlowerCycleController cycleController;
        [SerializeField] private FireflyCursorController cursorController;
        [SerializeField] private SingleFlowerDataRecorder dataRecorder;
        [SerializeField] private GameObject sessionControls;
        [SerializeField] private string resultsSceneName = "Results";

        private SessionDefinition definition;
        private SessionRuntimeSummary summary;
        private int currentBlockIndex;
        private bool completionBeingProcessed;

        private void Awake() => blockRunner.TakeSessionControl();

        private void OnEnable() => blockRunner.BlockCompleted += HandleBlockCompleted;
        private void OnDisable() => blockRunner.BlockCompleted -= HandleBlockCompleted;

        private void Start()
        {
            SessionResultsContext.Clear();
            definition = SessionLaunchContext.Consume();
            if (definition == null) definition = fallbackSession;
            if (definition == null)
            {
                Debug.LogError("Session could not begin: no launched or fallback session definition is assigned.", this);
                blockRunner.ClearForSessionTransition();
                return;
            }
            if (!definition.TryValidate(out string error))
            {
                Debug.LogError("Session could not begin: " + error, this);
                blockRunner.ClearForSessionTransition();
                return;
            }
            summary = new SessionRuntimeSummary
            {
                sessionDefinitionId = definition.SessionDefinitionId,
                sessionDisplayName = definition.DisplayName,
                configuredBlockCount = definition.OrderedBlocks.Count
            };
            currentBlockIndex = 0;
            StartCurrentBlock();
        }

        private void StartCurrentBlock()
        {
            completionBeingProcessed = false;
            transitionPresenter.Hide();
            cycleController.EvaluationPaused = false;
            cursorController.InputEnabled = true;
            if (sessionControls != null) sessionControls.SetActive(true);
            blockRunner.ConfigureBlock(definition.OrderedBlocks[currentBlockIndex], currentBlockIndex);
            if (!blockRunner.PrepareBlock()) return;
            blockRunner.StartConfiguredBlock();
        }

        private void HandleBlockCompleted(BlockCompletionReport report)
        {
            if (completionBeingProcessed || report == null || report.scheduleIndex != currentBlockIndex) return;
            completionBeingProcessed = true;
            summary.Add(report);
            blockRunner.ClearForSessionTransition();
            cycleController.EvaluationPaused = true;
            cursorController.InputEnabled = false;
            if (sessionControls != null) sessionControls.SetActive(false);

            if (currentBlockIndex >= definition.OrderedBlocks.Count - 1)
            {
                FinishSession();
                return;
            }

            int nextIndex = currentBlockIndex + 1;
            if (!definition.ShowBlockTransitions)
            {
                currentBlockIndex = nextIndex;
                StartCurrentBlock();
                return;
            }
            transitionPresenter.Show(ModeLabel(report.gameMode), ModeLabel(definition.OrderedBlocks[nextIndex].GameMode),
                definition.RequireManualContinue, definition.MinimumTransitionDisplayDuration, () =>
                {
                    currentBlockIndex = nextIndex;
                    StartCurrentBlock();
                });
        }

        private void FinishSession()
        {
            summary.endedNormally = true;
            summary.endedEarly = false;
            Time.timeScale = 1f;
            dataRecorder.Close();
            GameplayEvents.RaiseLevelCompleted();
            SessionResultsContext.Store(summary, definition);
            SceneManager.LoadScene(resultsSceneName);
        }

        private static string ModeLabel(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.SingleFlower: return "Single Flower";
                case GameMode.FindTheMatch: return "Find the Match";
                case GameMode.RememberTheMatch: return "Remember the Match";
                default: return "Unknown block";
            }
        }
    }
}
