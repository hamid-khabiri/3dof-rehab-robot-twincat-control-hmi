using System;
using UnityEngine;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  CRAZY CAR SESSION FLOW
    //  Main menu -> Settings -> Playing -> Result, plus the pause popup. Owns
    //  the chosen vehicle and speed level and is the ONE place the PLC game
    //  status is computed:
    //
    //      nGameStatus = (Playing && !paused) ? vehicle (1 Taxi, 2 Van, 3 Truck) : 0
    //
    //  Same rule as Cargo Sorter's SessionFlowController: any popup or menu on
    //  screen means 0. It is published on every screen change, pause/resume and
    //  vehicle change. Leaving a game changes the screen FIRST, so the PLC
    //  never briefly sees "driving" again on the way out.
    //
    //  While anything but Playing is showing, HighwayDriveController.Held is
    //  set, which freezes the drive behind the menu.
    // ─────────────────────────────────────────────────────────────────────────

    public enum CrazyCarScreen { MainMenu, Settings, Playing, Result }

    /// <summary>The value IS the PLC game status while driving.</summary>
    public enum CrazyCarVehicle { Taxi = 1, Van = 2, Truck = 3 }

    [Serializable]
    public class CrazyCarVehicleSetup
    {
        public CrazyCarVehicle Vehicle;
        public string DisplayName = "Taxi";
        [Tooltip("Shown under the title in Settings.")]
        public string Description = "Light steering";
        [Tooltip("Unassigned = procedural car in FallbackColor.")]
        public Sprite Sprite;
        [Tooltip("World size (width, length) -- also the collision box. Keep the sprite's aspect ratio.")]
        public Vector2 Size = new Vector2(0.92f, 1.52f);
        public Color FallbackColor = new Color(0.98f, 0.80f, 0.15f, 1f);
    }

    public sealed class CrazyCarSessionFlow : MonoBehaviour
    {
        public const int MinSpeedLevel = 1;
        public const int MaxSpeedLevel = 5;
        public const int NormalSpeedLevel = 3;

        private static readonly float[] SpeedFactors = { 0.6f, 0.8f, 1.0f, 1.2f, 1.4f };
        private static readonly string[] SpeedNames = { "Very Slow", "Slow", "Normal", "Fast", "Very Fast" };

        [Header("Dependencies")]
        [SerializeField] private HighwayDriveController _controller;
        [SerializeField] private HighwayDriveConfig _config;
        [SerializeField] private HighwayDriveInstructionUI _instructionUI;
        [SerializeField] private HighwayDriveDataLogger _logger;
        [SerializeField] private TwincatConnectorCrazyCar _twincat;
        [Tooltip("Highway Drive's PatientCanvas (hearts, clock, banner, speed gauge). Shown only while driving.")]
        [SerializeField] private Canvas _hudCanvas;
        [Tooltip("Re-skinned every HighwayDriveConfig.ThemeChangeSeconds of driving.")]
        [SerializeField] private HighwayDriveRoad _road;

        private static readonly HighwayDriveEnvironmentTheme[] ThemeOrder =
        {
            HighwayDriveEnvironmentTheme.Green,
            HighwayDriveEnvironmentTheme.Desert,
            HighwayDriveEnvironmentTheme.Snow,
        };

        [Header("Vehicles")]
        [SerializeField] private CrazyCarVehicleSetup[] _vehicles = new CrazyCarVehicleSetup[0];

        public event Action<CrazyCarScreen> OnScreenChanged;
        /// <summary>Driving time in seconds, raised when a game ends by
        /// running out of hearts.</summary>
        public event Action<float> OnResult;

        public CrazyCarScreen CurrentScreen { get; private set; } = CrazyCarScreen.MainMenu;
        public CrazyCarVehicle Vehicle { get; private set; } = CrazyCarVehicle.Taxi;
        public int SpeedLevel { get; private set; } = NormalSpeedLevel;
        public bool IsPaused { get; private set; }
        public float LastDrivingSeconds { get; private set; }

        public int GameStatus => CurrentScreen == CrazyCarScreen.Playing && !IsPaused ? (int)Vehicle : 0;

        // Base values captured once, so the speed level always scales the
        // values the scene was saved with rather than compounding.
        private float _baseCruise, _baseSwing, _baseMinSpeed, _baseTrafficMin, _baseTrafficMax, _baseRampStep;

        public static string SpeedName(int level) => SpeedNames[Mathf.Clamp(level, MinSpeedLevel, MaxSpeedLevel) - 1];

        public CrazyCarVehicleSetup CurrentVehicleSetup => FindVehicle(Vehicle);

        private void Awake()
        {
            if (_controller == null || _config == null)
            {
                Debug.LogError("[CrazyCarSessionFlow] Assign _controller and _config. Run Tools > Crazy Car > " +
                               "Build Crazy Car Scene, then save the scene.", this);
                enabled = false;
                return;
            }

            _baseCruise = _config.CruiseSpeedWorldPerS;
            _baseSwing = _config.SpeedSwingWorldPerS;
            _baseMinSpeed = _config.MinSpeedWorldPerS;
            _baseTrafficMin = _config.TrafficSpeedMinWorldPerS;
            _baseTrafficMax = _config.TrafficSpeedMaxWorldPerS;
            _baseRampStep = _config.SpeedRampStepWorldPerS;

            _controller.Held = true;
            ApplyVehicle();
        }

        private void OnEnable() { if (_controller != null) _controller.OnStateChanged += HandleDriveStateChanged; }
        private void OnDisable()
        {
            if (_controller != null) _controller.OnStateChanged -= HandleDriveStateChanged;
            Time.timeScale = 1f;
        }

        private void Start() => ShowMainMenu();

        /// <summary>Green -> Desert -> Snow -> Green ... one step every
        /// ThemeChangeSeconds of driving time (the trial clock, so pauses and
        /// menus do not count).</summary>
        private void Update()
        {
            if (_road == null || CurrentScreen != CrazyCarScreen.Playing || IsPaused) return;
            float interval = _config.ThemeChangeSeconds;
            if (interval <= 0f) return;

            int step = Mathf.FloorToInt(_controller.TrialElapsedSeconds / interval);
            _road.SetTheme(ThemeOrder[step % ThemeOrder.Length]);
        }

        // ── Screens ──────────────────────────────────────────────────────

        public void ShowMainMenu()
        {
            LeaveGame();
            SetScreen(CrazyCarScreen.MainMenu);
        }

        public void ChooseVehicle(CrazyCarVehicle vehicle)
        {
            Vehicle = vehicle;
            ApplyVehicle();
            SetScreen(CrazyCarScreen.Settings);
        }

        public void ChooseTaxi() => ChooseVehicle(CrazyCarVehicle.Taxi);
        public void ChooseVan() => ChooseVehicle(CrazyCarVehicle.Van);
        public void ChooseTruck() => ChooseVehicle(CrazyCarVehicle.Truck);

        public void SetSpeedLevel(int level) => SpeedLevel = Mathf.Clamp(level, MinSpeedLevel, MaxSpeedLevel);

        public void StartGame()
        {
            if (!_controller.IsInitialized)
            {
                Debug.LogWarning("[CrazyCarSessionFlow] The road is still being built -- press Start again.", this);
                return;
            }

            ApplySpeedLevel();
            ApplyVehicle();
            _logger?.BeginNewSession();
            if (_road != null && _config.ThemeChangeSeconds > 0f) _road.SetTheme(ThemeOrder[0]);

            Time.timeScale = 1f;
            IsPaused = false;
            _controller.Held = false;
            _controller.StartRun();
            SetScreen(CrazyCarScreen.Playing);
        }

        public void PlayAgain() => StartGame();

        public void SetPaused(bool paused)
        {
            if (CurrentScreen != CrazyCarScreen.Playing || IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            _controller.Held = paused;
            PublishGameStatus();
        }

        public void ReturnToMenu() => ShowMainMenu();

        public void QuitGame()
        {
            // TwincatConnectorCrazyCar sends bGameClosed from OnApplicationQuit.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Internals ────────────────────────────────────────────────────

        /// <summary>Stops a game in progress. The screen change that follows
        /// publishes status 0; the drive is ended here so its data is written.</summary>
        private void LeaveGame()
        {
            bool wasPlaying = CurrentScreen == CrazyCarScreen.Playing;
            CurrentScreen = CrazyCarScreen.MainMenu;   // first, so the PLC never sees "driving" again
            PublishGameStatus();

            Time.timeScale = 1f;
            IsPaused = false;
            _controller.Held = true;
            if (wasPlaying && _controller.IsInitialized) _controller.EndSession();
            _instructionUI?.ShowFeedback(string.Empty);
            _instructionUI?.ShowCenterTarget(false);
        }

        private void HandleDriveStateChanged(HighwayDriveState state)
        {
            if (state != HighwayDriveState.Finished || CurrentScreen != CrazyCarScreen.Playing) return;

            LastDrivingSeconds = _controller.TrialElapsedSeconds;
            Time.timeScale = 1f;
            IsPaused = false;
            _controller.Held = true;
            _instructionUI?.ShowFeedback(string.Empty);
            SetScreen(CrazyCarScreen.Result);
            OnResult?.Invoke(LastDrivingSeconds);
        }

        private void SetScreen(CrazyCarScreen screen)
        {
            CurrentScreen = screen;
            if (screen != CrazyCarScreen.Playing) IsPaused = false;
            if (_hudCanvas != null) _hudCanvas.enabled = screen == CrazyCarScreen.Playing;
            OnScreenChanged?.Invoke(screen);
            PublishGameStatus();
        }

        private void ApplySpeedLevel()
        {
            float f = SpeedFactors[SpeedLevel - 1];
            _config.CruiseSpeedWorldPerS = _baseCruise * f;
            _config.SpeedSwingWorldPerS = _baseSwing * f;
            _config.MinSpeedWorldPerS = _baseMinSpeed * f;
            _config.TrafficSpeedMinWorldPerS = _baseTrafficMin * f;
            _config.TrafficSpeedMaxWorldPerS = _baseTrafficMax * f;
            _config.SpeedRampStepWorldPerS = _baseRampStep * f;
        }

        private void ApplyVehicle()
        {
            CrazyCarVehicleSetup setup = CurrentVehicleSetup;
            if (setup == null)
            {
                Debug.LogWarning($"[CrazyCarSessionFlow] No vehicle setup for {Vehicle}.", this);
                return;
            }
            _controller.SetPlayerVehicle(setup.Sprite, setup.Size, setup.FallbackColor);
        }

        private CrazyCarVehicleSetup FindVehicle(CrazyCarVehicle vehicle)
        {
            if (_vehicles == null) return null;
            foreach (CrazyCarVehicleSetup setup in _vehicles)
                if (setup != null && setup.Vehicle == vehicle) return setup;
            return null;
        }

        private void PublishGameStatus()
        {
            if (_twincat != null) _twincat.SetGameStatus(GameStatus);
        }
    }
}
