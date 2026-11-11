using FireflyGarden.Configuration;
using FireflyGarden.Session;
using FireflyGarden.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FireflyGarden.Results
{
    /// <summary>Level-complete pop-up: summary, Play Again, Next Level (when there is one), Main Menu.</summary>
    public sealed class SessionResultsPresenter : MonoBehaviour
    {
        [SerializeField] private string menuSceneName = "MainMenu";
        [Tooltip("Garden picture behind the pop-up, same as the first page.")]
        [SerializeField] private Sprite backgroundSprite;

        private SessionDefinition completedLevel;
        private SessionDefinition nextLevel;

        private void Awake()
        {
            Time.timeScale = 1f;
            SessionRuntimeSummary summary = SessionResultsContext.Read();
            completedLevel = SessionResultsContext.CompletedSession;
            nextLevel = LevelLauncher.NextAfter(completedLevel);
            Build(summary);
        }

        public void PlayAgain() => LevelLauncher.TryLaunch(completedLevel, this);

        public void PlayNextLevel() => LevelLauncher.TryLaunch(nextLevel, this);

        public void BackToMenu()
        {
            SessionResultsContext.Clear();
            SessionLaunchContext.Clear();
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }

        private void Build(SessionRuntimeSummary summary)
        {
            Transform canvas = FireflyUiStyle.CreateCanvas(transform, "ResultsCanvas", 10);
            FireflyUiStyle.CreateBackground(canvas, backgroundSprite, new Color(0.06f, 0.13f, 0.13f, 1f));
            FireflyUiStyle.CreateDimmer(canvas);

            bool canReplay = completedLevel != null;
            bool hasNext = nextLevel != null;
            int buttonCount = 1 + (canReplay ? 1 : 0) + (hasNext ? 1 : 0);
            float height = 434f + buttonCount * FireflyUiStyle.ButtonSpacing;
            Transform popup = FireflyUiStyle.CreatePopup(canvas, "ResultsPopup", height).transform;
            float textWidth = FireflyUiStyle.PopupWidth - 60f;
            Vector2 buttonSize = new Vector2(FireflyUiStyle.WideButtonWidth, FireflyUiStyle.ButtonHeight);

            float y = -44f;
            FireflyUiStyle.CreateText(popup, "Well done!", 56f, true, FireflyUiStyle.TitleColor, new Vector2(0f, y),
                new Vector2(textWidth, 72f));
            y -= 76f;
            string subtitle = completedLevel != null ? LevelLauncher.LevelLabel(completedLevel) : summary.sessionDisplayName;
            FireflyUiStyle.CreateText(popup, subtitle ?? string.Empty, 26f, false, FireflyUiStyle.MutedTextColor,
                new Vector2(0f, y), new Vector2(textWidth, 40f));
            y -= 60f;
            FireflyUiStyle.CreateDivider(popup, y);
            y -= 24f;

            TextMeshProUGUI stats = FireflyUiStyle.CreateText(popup,
                $"Successful trials   <b>{summary.successfulTrials}</b>\n" +
                $"Trials completed   <b>{summary.trialsCompleted}</b>\n" +
                $"Incomplete timeouts   <b>{summary.incompleteTimeouts}</b>",
                28f, false, FireflyUiStyle.TitleColor, new Vector2(0f, y), new Vector2(textWidth - 40f, 150f));
            stats.lineSpacing = 12f;
            y -= 186f;

            if (canReplay)
            {
                FireflyUiStyle.CreateButton(popup, "Play Again", FireflyUiStyle.PrimaryButtonColor, new Vector2(0f, y),
                    buttonSize, PlayAgain);
                y -= FireflyUiStyle.ButtonSpacing;
            }
            if (hasNext)
            {
                FireflyUiStyle.CreateButton(popup, "Next Level", FireflyUiStyle.PrimaryButtonColor, new Vector2(0f, y),
                    buttonSize, PlayNextLevel);
                y -= FireflyUiStyle.ButtonSpacing;
            }
            FireflyUiStyle.CreateButton(popup, "Main Menu", FireflyUiStyle.SecondaryButtonColor, new Vector2(0f, y),
                buttonSize, BackToMenu);
        }
    }
}
