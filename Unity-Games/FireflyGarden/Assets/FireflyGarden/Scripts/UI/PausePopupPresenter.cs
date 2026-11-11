using FireflyGarden.Session;
using UnityEngine;

namespace FireflyGarden.UI
{
    /// <summary>
    /// Shows the pause pop-up while gameplay is paused. The single pause/play toggle button in the
    /// gameplay HUD stays above this pop-up, so it can still resume play.
    /// </summary>
    public sealed class PausePopupPresenter : MonoBehaviour
    {
        [SerializeField] private GameplayPauseController pauseController;
        [SerializeField] private GameplayStopController stopController;
        [Tooltip("Below the gameplay HUD canvas (20) so the pause/play toggle stays visible and clickable.")]
        [SerializeField] private int sortingOrder = 15;

        private GameObject root;

        private void Awake()
        {
            if (pauseController == null || stopController == null)
            {
                Debug.LogError("PausePopupPresenter requires the pause and stop controllers.", this);
                enabled = false;
                return;
            }
            Build();
            root.SetActive(false);
        }

        private void OnEnable()
        {
            if (pauseController == null || root == null) return;
            pauseController.PausedChanged += HandlePausedChanged;
            HandlePausedChanged(pauseController.IsPaused);
        }

        private void OnDisable()
        {
            if (pauseController != null) pauseController.PausedChanged -= HandlePausedChanged;
        }

        private void HandlePausedChanged(bool paused)
        {
            if (root != null) root.SetActive(paused);
        }

        private void Build()
        {
            Transform canvas = FireflyUiStyle.CreateCanvas(transform, "PausePopupCanvas", sortingOrder);
            root = canvas.gameObject;
            FireflyUiStyle.CreateDimmer(canvas);

            Transform popup = FireflyUiStyle.CreatePopup(canvas, "PausePopup", 400f).transform;
            Vector2 buttonSize = new Vector2(FireflyUiStyle.WideButtonWidth, FireflyUiStyle.ButtonHeight);

            float y = -44f;
            FireflyUiStyle.CreateText(popup, "Paused", 56f, true, FireflyUiStyle.TitleColor, new Vector2(0f, y),
                new Vector2(FireflyUiStyle.PopupWidth - 60f, 72f));
            y -= 76f;
            FireflyUiStyle.CreateText(popup, "Take a rest. Press Resume when ready.", 26f, false,
                FireflyUiStyle.MutedTextColor, new Vector2(0f, y), new Vector2(FireflyUiStyle.PopupWidth - 60f, 40f));
            y -= 64f;
            FireflyUiStyle.CreateButton(popup, "Resume", FireflyUiStyle.PrimaryButtonColor, new Vector2(0f, y), buttonSize,
                pauseController.Resume);
            y -= FireflyUiStyle.ButtonSpacing;
            FireflyUiStyle.CreateButton(popup, "Exit to Main Menu", FireflyUiStyle.ExitButtonColor, new Vector2(0f, y),
                buttonSize, stopController.StopAndReturnToMenu);
        }
    }
}
