using UnityEngine;

namespace FireflyGarden.Robot
{
    /// <summary>
    /// TwinCAT ADS connection, PLC symbol names, and robot workspace calibration.
    /// The active asset lives at Resources/FireflyRobot/TwinCatSettings.
    /// </summary>
    [CreateAssetMenu(menuName = "Firefly Garden/TwinCAT Settings", fileName = "TwinCatSettings")]
    public sealed class TwinCatSettings : ScriptableObject
    {
        public const string ResourcePath = "FireflyRobot/TwinCatSettings";

        [Header("Connection")]
        [Tooltip("Turn off to play with the mouse only, without trying to reach the PLC.")]
        [SerializeField] private bool connectOnStart = true;
        [Tooltip("Target AmsNetId, e.g. '127.0.0.1.1.1' when TwinCAT runs on this PC.")]
        [SerializeField] private string amsNetId = "127.0.0.1.1.1";
        [SerializeField] private int amsPort = 851;

        [Header("PLC -> Unity (REAL, percent)")]
        [Tooltip("fX and fY are -100..+100 %. (0, 0) = home flower in the middle of the scene; " +
            "+/-100 % = the edges of the gameplay area. TwinCAT scales the robot's real range to this.")]
        [SerializeField] private string robotXSymbol = "MAIN.fbFireflyGame.stToUnity.fX";
        [SerializeField] private string robotYSymbol = "MAIN.fbFireflyGame.stToUnity.fY";

        [Header("Unity -> PLC")]
        [Tooltip("INT: 0 = menu/results, 1/2/3 = playing Level 1/2/3, 4 = paused.")]
        [SerializeField] private string gameStatusSymbol = "MAIN.fbFireflyGame.stFromUnity.nGameStatus";
        [Header("Unity -> PLC (BOOL pulses)")]
        [SerializeField] private string exitPressedSymbol = "MAIN.fbFireflyGame.stFromUnity.bExitPressed";
        [SerializeField] private string wrongFlowerSymbol = "MAIN.fbFireflyGame.stFromUnity.bWrongFlower";
        [Tooltip("How long each pulse stays TRUE (seconds). Keep it longer than a few PLC cycles.")]
        [SerializeField, Min(0.02f)] private float pulseHighSeconds = 0.20f;
        [Tooltip("Minimum FALSE time between two pulses (seconds), so the PLC sees a new rising edge.")]
        [SerializeField, Min(0.02f)] private float pulseLowSeconds = 0.20f;

        [Header("Robot Workspace (PLC units)")]
        [Tooltip("Robot X that maps to the left edge of the gameplay area.")]
        [SerializeField] private float robotMinX = -100f;
        [Tooltip("Robot X that maps to the right edge of the gameplay area.")]
        [SerializeField] private float robotMaxX = 100f;
        [Tooltip("Robot Y that maps to the bottom edge of the gameplay area.")]
        [SerializeField] private float robotMinY = -100f;
        [Tooltip("Robot Y that maps to the top edge of the gameplay area.")]
        [SerializeField] private float robotMaxY = 100f;
        [SerializeField] private bool invertX;
        [SerializeField] private bool invertY;

        public bool ConnectOnStart => connectOnStart;
        public string AmsNetId => amsNetId;
        public int AmsPort => amsPort;
        public string RobotXSymbol => robotXSymbol;
        public string RobotYSymbol => robotYSymbol;
        public string GameStatusSymbol => gameStatusSymbol;
        public string ExitPressedSymbol => exitPressedSymbol;
        public string WrongFlowerSymbol => wrongFlowerSymbol;
        public float PulseHighSeconds => pulseHighSeconds;
        public float PulseLowSeconds => pulseLowSeconds;

        /// <summary>
        /// Converts a raw robot position into the gameplay area as -1..+1 per axis
        /// (-1 = left/bottom edge, +1 = right/top edge). Direct mapping, clamped, no smoothing.
        /// </summary>
        public Vector2 NormalizeRobotPosition(float robotX, float robotY)
        {
            float x = Normalize(robotX, robotMinX, robotMaxX);
            float y = Normalize(robotY, robotMinY, robotMaxY);
            return new Vector2(invertX ? -x : x, invertY ? -y : y);
        }

        private static float Normalize(float value, float min, float max)
        {
            if (Mathf.Approximately(min, max) || float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Mathf.Clamp(Mathf.InverseLerp(min, max, value) * 2f - 1f, -1f, 1f);
        }
    }
}
