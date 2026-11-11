using System.Collections.Generic;
using FireflyGarden.Configuration;
using FireflyGarden.Robot;
using FireflyGarden.Session;
using UnityEngine;

namespace FireflyGarden.UI
{
    /// <summary>First page: one button per level plus Exit, drawn as a pop-up over the garden.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "FireflyGameplay";
        [Tooltip("Ordered levels shown on the first page. Each session definition normally holds one block.")]
        [SerializeField] private List<SessionDefinition> levels = new List<SessionDefinition>();
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private string title = "Firefly Garden";
        [SerializeField] private string subtitle = "Choose a level";

        private void Awake() => LevelLauncher.Register(levels, gameplaySceneName);

        private void Start() => BuildMenu();

        public void StartLevel(int levelIndex)
        {
            if (levelIndex < 0 || levelIndex >= LevelLauncher.Levels.Count)
            {
                Debug.LogError("Cannot start level: index " + levelIndex + " is not configured.", this);
                return;
            }
            LevelLauncher.TryLaunch(LevelLauncher.Levels[levelIndex], this);
        }

        public void ExitApplication()
        {
            RobotOutputSignals.RaiseExitPressed();
#if UNITY_EDITOR
            Debug.Log("Exit requested. Application.Quit only closes a built player.", this);
#else
            Application.Quit();
#endif
        }

        private void BuildMenu()
        {
            Transform canvas = FireflyUiStyle.CreateCanvas(transform, "LevelMenuCanvas", 10);
            FireflyUiStyle.CreateBackground(canvas, backgroundSprite, new Color(0.19f, 0.30f, 0.47f, 1f));
            FireflyUiStyle.CreateDimmer(canvas);

            int levelCount = LevelLauncher.Levels.Count;
            float height = 210f + levelCount * FireflyUiStyle.ButtonSpacing + 24f + FireflyUiStyle.ButtonSpacing + 20f;
            Transform popup = FireflyUiStyle.CreatePopup(canvas, "LevelMenuPopup", height).transform;
            Vector2 textSize = new Vector2(FireflyUiStyle.PopupWidth - 60f, 72f);
            Vector2 buttonSize = new Vector2(FireflyUiStyle.WideButtonWidth, FireflyUiStyle.ButtonHeight);

            float y = -44f;
            FireflyUiStyle.CreateText(popup, title, 56f, true, FireflyUiStyle.TitleColor, new Vector2(0f, y), textSize);
            y -= 76f;
            FireflyUiStyle.CreateText(popup, subtitle, 26f, false, FireflyUiStyle.MutedTextColor, new Vector2(0f, y),
                new Vector2(FireflyUiStyle.PopupWidth - 60f, 40f));
            y -= 60f;
            FireflyUiStyle.CreateDivider(popup, y);
            y -= 30f;

            for (int i = 0; i < levelCount; i++)
            {
                int levelIndex = i;
                FireflyUiStyle.CreateButton(popup, LevelLauncher.LevelLabel(LevelLauncher.Levels[i]),
                    FireflyUiStyle.PrimaryButtonColor, new Vector2(0f, y), buttonSize, () => StartLevel(levelIndex));
                y -= FireflyUiStyle.ButtonSpacing;
            }

            y -= 24f;
            FireflyUiStyle.CreateButton(popup, "Exit Game", FireflyUiStyle.ExitButtonColor, new Vector2(0f, y), buttonSize,
                ExitApplication);
        }
    }
}
