using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  CRAZY CAR MENUS + HUD BADGES (Cargo Sorter look)
    //  Shows the panel that matches CrazyCarSessionFlow's screen and forwards
    //  button / slider input to it. Built by Tools > Crazy Car > Build Crazy
    //  Car Scene; every reference is optional so the game still runs (with a
    //  Console warning) before the UI exists.
    //
    //  No digits in patient-visible text (project rule): the speed slider
    //  label is a word ("Normal"), the badges are dots and bars. The only
    //  number is the driving-time clock on the result screen, the same M:SS
    //  exception the in-game clock already uses.
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class CrazyCarMenuUI : MonoBehaviour
    {
        [SerializeField] private CrazyCarSessionFlow _flow;

        [Header("Panels")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _hudPanel;
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private GameObject _resultPanel;

        [Header("Main menu")]
        [SerializeField] private Button _taxiButton;
        [SerializeField] private Button _vanButton;
        [SerializeField] private Button _truckButton;
        [SerializeField] private Button _exitButton;

        [Header("Settings")]
        [SerializeField] private TextMeshProUGUI _settingsTitle;
        [SerializeField] private TextMeshProUGUI _settingsSubtitle;
        [SerializeField] private Slider _speedSlider;
        [SerializeField] private TextMeshProUGUI _speedValueText;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _backButton;

        [Header("HUD")]
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Image[] _speedDots = new Image[0];
        [SerializeField] private TextMeshProUGUI _vehicleBadgeTitle;
        [SerializeField] private Image[] _vehicleBars = new Image[0];
        [SerializeField] private Color _speedDotActive = new Color(0.12f, 0.9f, 0.35f, 1f);
        [SerializeField] private Color _inactiveColor = new Color(0.20f, 0.18f, 0.16f, 0.40f);
        [SerializeField] private Color _lightColor = new Color(0.12f, 0.9f, 0.35f, 1f);
        [SerializeField] private Color _mediumColor = new Color(1f, 0.67f, 0.1f, 1f);
        [SerializeField] private Color _heavyColor = new Color(0.92f, 0.22f, 0.18f, 1f);

        [Header("Pause popup")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _pauseMenuButton;

        [Header("Result popup")]
        [SerializeField] private TextMeshProUGUI _resultText;
        [SerializeField] private Button _playAgainButton;
        [SerializeField] private Button _resultMenuButton;

        private void OnEnable()
        {
            if (_flow == null)
            {
                Debug.LogWarning("[CrazyCarMenuUI] The menus are not built yet. Run Tools > Crazy Car > Build Crazy " +
                                 "Car Scene, then save the scene.", this);
                return;
            }

            _flow.OnScreenChanged += ShowScreen;
            _flow.OnResult += ShowResult;
            Listen(_taxiButton, _flow.ChooseTaxi);
            Listen(_vanButton, _flow.ChooseVan);
            Listen(_truckButton, _flow.ChooseTruck);
            Listen(_exitButton, _flow.QuitGame);
            Listen(_startButton, _flow.StartGame);
            Listen(_backButton, _flow.ShowMainMenu);
            Listen(_pauseButton, Pause);
            Listen(_resumeButton, Resume);
            Listen(_pauseMenuButton, _flow.ReturnToMenu);
            Listen(_playAgainButton, _flow.PlayAgain);
            Listen(_resultMenuButton, _flow.ReturnToMenu);
            if (_speedSlider != null) _speedSlider.onValueChanged.AddListener(HandleSpeedChanged);
            ShowScreen(_flow.CurrentScreen);
        }

        private void OnDisable()
        {
            if (_flow == null) return;
            _flow.OnScreenChanged -= ShowScreen;
            _flow.OnResult -= ShowResult;
            Unlisten(_taxiButton, _flow.ChooseTaxi);
            Unlisten(_vanButton, _flow.ChooseVan);
            Unlisten(_truckButton, _flow.ChooseTruck);
            Unlisten(_exitButton, _flow.QuitGame);
            Unlisten(_startButton, _flow.StartGame);
            Unlisten(_backButton, _flow.ShowMainMenu);
            Unlisten(_pauseButton, Pause);
            Unlisten(_resumeButton, Resume);
            Unlisten(_pauseMenuButton, _flow.ReturnToMenu);
            Unlisten(_playAgainButton, _flow.PlayAgain);
            Unlisten(_resultMenuButton, _flow.ReturnToMenu);
            if (_speedSlider != null) _speedSlider.onValueChanged.RemoveListener(HandleSpeedChanged);
        }

        private void Update()
        {
            // Escape or P toggles the pause popup while driving.
            Keyboard kb = Keyboard.current;
            if (_flow == null || kb == null || _flow.CurrentScreen != CrazyCarScreen.Playing) return;
            if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
            {
                if (_flow.IsPaused) Resume();
                else Pause();
            }
        }

        private void ShowScreen(CrazyCarScreen screen)
        {
            SetActive(_mainMenuPanel, screen == CrazyCarScreen.MainMenu);
            SetActive(_settingsPanel, screen == CrazyCarScreen.Settings);
            SetActive(_hudPanel, screen == CrazyCarScreen.Playing);
            SetActive(_resultPanel, screen == CrazyCarScreen.Result);
            SetActive(_pausePanel, false);

            if (screen == CrazyCarScreen.Settings) RefreshSettings();
            if (screen == CrazyCarScreen.Playing) RefreshBadges();
        }

        private void RefreshSettings()
        {
            CrazyCarVehicleSetup vehicle = _flow.CurrentVehicleSetup;
            if (_settingsTitle != null) _settingsTitle.text = vehicle != null ? vehicle.DisplayName : _flow.Vehicle.ToString();
            if (_settingsSubtitle != null) _settingsSubtitle.text = vehicle != null ? vehicle.Description : string.Empty;
            if (_speedSlider != null) _speedSlider.SetValueWithoutNotify(_flow.SpeedLevel);
            if (_speedValueText != null) _speedValueText.text = CrazyCarSessionFlow.SpeedName(_flow.SpeedLevel);
        }

        private void RefreshBadges()
        {
            for (int i = 0; i < _speedDots.Length; i++)
            {
                if (_speedDots[i] == null) continue;
                bool active = i == _flow.SpeedLevel - 1;
                _speedDots[i].color = active ? _speedDotActive : _inactiveColor;
                _speedDots[i].rectTransform.localScale = Vector3.one * (active ? 1.15f : 0.85f);
            }

            int level = (int)_flow.Vehicle;
            Color barColor = level == 1 ? _lightColor : level == 2 ? _mediumColor : _heavyColor;
            for (int i = 0; i < _vehicleBars.Length; i++)
                if (_vehicleBars[i] != null) _vehicleBars[i].color = i < level ? barColor : _inactiveColor;

            CrazyCarVehicleSetup vehicle = _flow.CurrentVehicleSetup;
            if (_vehicleBadgeTitle != null) _vehicleBadgeTitle.text = vehicle != null ? vehicle.DisplayName : _flow.Vehicle.ToString();
        }

        private void HandleSpeedChanged(float value)
        {
            _flow.SetSpeedLevel(Mathf.RoundToInt(value));
            RefreshSettings();
        }

        private void Pause()
        {
            if (_flow.CurrentScreen != CrazyCarScreen.Playing || _flow.IsPaused) return;
            _flow.SetPaused(true);
            SetActive(_pausePanel, true);
        }

        private void Resume()
        {
            if (!_flow.IsPaused) return;
            SetActive(_pausePanel, false);
            _flow.SetPaused(false);
        }

        private void ShowResult(float drivingSeconds)
        {
            if (_resultText == null) return;
            CrazyCarVehicleSetup vehicle = _flow.CurrentVehicleSetup;
            string car = vehicle != null ? vehicle.DisplayName : _flow.Vehicle.ToString();
            _resultText.text =
                $"<b>Driving time</b>\n<size=150%>{HighwayDriveControlMath.FormatElapsedClock(drivingSeconds)}</size>\n" +
                $"{car}  -  {CrazyCarSessionFlow.SpeedName(_flow.SpeedLevel)}";
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private static void Unlisten(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.RemoveListener(action);
        }
    }
}
