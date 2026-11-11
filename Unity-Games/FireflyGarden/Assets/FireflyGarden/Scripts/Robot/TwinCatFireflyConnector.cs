using System;
using System.Diagnostics;
using System.Threading;
using FireflyGarden.Core;
using FireflyGarden.Session;
using TwinCAT.Ads;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FireflyGarden.Robot
{
    /// <summary>
    /// TwinCAT ADS link for Firefly Garden (same TwinCAT.Ads.dll / TcAdsClient API as the lab's other games).
    /// Reads the end-effector X and Y every frame and writes one-shot BOOL pulses for game events.
    /// Created automatically at startup from Resources/FireflyRobot/TwinCatSettings and kept across scenes.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class TwinCatFireflyConnector : MonoBehaviour
    {
        private const double QuitFlushTimeoutSeconds = 1.5d;

        [Header("Live Status (read only)")]
        [SerializeField] private bool isConnected;
        [SerializeField] private bool exitOutputAvailable;
        [SerializeField] private bool wrongFlowerOutputAvailable;
        [SerializeField] private float robotX;
        [SerializeField] private float robotY;
        [SerializeField] private Vector2 normalizedPosition;
        [SerializeField] private int gameStatusOutput;
        [SerializeField] private bool exitPressedOutput;
        [SerializeField] private bool wrongFlowerOutput;
        [SerializeField] private string lastError;

        private TwinCatSettings settings;
        private TcAdsClient adsClient;
        private int robotXHandle;
        private int robotYHandle;
        private int exitPressedHandle;
        private int wrongFlowerHandle;
        private int gameStatusHandle;
        private short writtenGameStatus;
        private BooleanPulse exitPulse;
        private BooleanPulse wrongFlowerPulse;
        private bool writtenExitValue;
        private bool writtenWrongFlowerValue;
        private bool hasRobotPosition;
        private bool readErrorLogged;
        private bool writeErrorLogged;

        public static TwinCatFireflyConnector Instance { get; private set; }

        public bool IsConnected => isConnected;
        /// <summary>True once a valid X/Y has been read from the PLC over the current connection.</summary>
        public bool HasRobotPosition => isConnected && hasRobotPosition;
        public float RobotX => robotX;
        public float RobotY => robotY;
        /// <summary>Robot position inside the gameplay area, -1..+1 per axis (see TwinCatSettings).</summary>
        public Vector2 NormalizedPosition => normalizedPosition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateAtStartup()
        {
            if (Instance != null) return;
            TwinCatSettings loaded = Resources.Load<TwinCatSettings>(TwinCatSettings.ResourcePath);
            if (loaded == null)
            {
                Debug.LogWarning("TwinCAT settings not found at Resources/" + TwinCatSettings.ResourcePath +
                    "; the firefly follows the mouse.");
                return;
            }
            if (!loaded.ConnectOnStart)
            {
                Debug.Log("TwinCAT connection is turned off in TwinCatSettings; the firefly follows the mouse.");
                return;
            }

            var connectorObject = new GameObject("TwinCatFireflyConnector");
            DontDestroyOnLoad(connectorObject);
            connectorObject.AddComponent<TwinCatFireflyConnector>().Initialize(loaded);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            RobotOutputSignals.ExitPressed += HandleExitPressed;
            GameplayEvents.WrongFlowerSelected += HandleWrongFlowerSelected;
        }

        private void OnDisable()
        {
            RobotOutputSignals.ExitPressed -= HandleExitPressed;
            GameplayEvents.WrongFlowerSelected -= HandleWrongFlowerSelected;
        }

        private void Initialize(TwinCatSettings configuredSettings)
        {
            settings = configuredSettings;
            exitPulse = new BooleanPulse(settings.PulseHighSeconds, settings.PulseLowSeconds);
            wrongFlowerPulse = new BooleanPulse(settings.PulseHighSeconds, settings.PulseLowSeconds);
            Connect();
        }

        [ContextMenu("Reconnect")]
        public void Reconnect()
        {
            if (settings == null) return;
            Disconnect();
            Connect();
        }

        private void Connect()
        {
            try
            {
                adsClient = new TcAdsClient();
                adsClient.Connect(settings.AmsNetId, settings.AmsPort);
                robotXHandle = adsClient.CreateVariableHandle(settings.RobotXSymbol);
                robotYHandle = adsClient.CreateVariableHandle(settings.RobotYSymbol);
                isConnected = true;
                lastError = string.Empty;
                Debug.Log($"TwinCAT ADS connected ({settings.AmsNetId}:{settings.AmsPort}); reading " +
                    $"'{settings.RobotXSymbol}' and '{settings.RobotYSymbol}'.", this);
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                Debug.LogWarning($"TwinCAT ADS connection failed: {exception.Message}. The firefly follows the mouse.", this);
                Disconnect();
                return;
            }

            // Outputs are optional: a missing output symbol must never stop the robot position from being read.
            exitPressedHandle = CreateOptionalHandle(settings.ExitPressedSymbol);
            wrongFlowerHandle = CreateOptionalHandle(settings.WrongFlowerSymbol);
            gameStatusHandle = CreateOptionalHandle(settings.GameStatusSymbol);
            exitOutputAvailable = exitPressedHandle != 0;
            wrongFlowerOutputAvailable = wrongFlowerHandle != 0;
            writtenExitValue = true;
            writtenWrongFlowerValue = true;
            WriteIfChanged(exitPressedHandle, false, ref writtenExitValue);
            WriteIfChanged(wrongFlowerHandle, false, ref writtenWrongFlowerValue);
            writtenGameStatus = -1;
            WriteStatusIfChanged((short)gameStatusOutput);
        }

        private void WriteStatusIfChanged(short status)
        {
            if (!isConnected || adsClient == null || gameStatusHandle == 0 || writtenGameStatus == status) return;
            try
            {
                adsClient.WriteAny(gameStatusHandle, status);
                writtenGameStatus = status;
                writeErrorLogged = false;
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                if (!writeErrorLogged)
                {
                    writeErrorLogged = true;
                    Debug.LogWarning($"TwinCAT ADS write error: {exception.Message}", this);
                }
            }
        }

        private int CreateOptionalHandle(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return 0;
            try
            {
                return adsClient.CreateVariableHandle(symbol);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"TwinCAT output symbol '{symbol}' is not available: {exception.Message}. " +
                    "This signal will not be sent.", this);
                return 0;
            }
        }

        private void Update()
        {
            if (exitPulse == null) return;
            float deltaTime = Time.unscaledDeltaTime;
            exitPressedOutput = exitPulse.Tick(deltaTime);
            wrongFlowerOutput = wrongFlowerPulse.Tick(deltaTime);
            gameStatusOutput = GameplayStatus.StatusCode;
            if (!isConnected) return;

            ReadRobotPosition();
            WriteStatusIfChanged((short)gameStatusOutput);
            WriteIfChanged(exitPressedHandle, exitPressedOutput, ref writtenExitValue);
            WriteIfChanged(wrongFlowerHandle, wrongFlowerOutput, ref writtenWrongFlowerValue);
        }

        private void ReadRobotPosition()
        {
            try
            {
                robotX = (float)adsClient.ReadAny(robotXHandle, typeof(float));
                robotY = (float)adsClient.ReadAny(robotYHandle, typeof(float));
                normalizedPosition = settings.NormalizeRobotPosition(robotX, robotY);
                hasRobotPosition = true;
                readErrorLogged = false;
            }
            catch (Exception exception)
            {
                hasRobotPosition = false;
                lastError = exception.Message;
                // Log once per failure streak so a dropped connection doesn't flood the console every frame.
                if (!readErrorLogged)
                {
                    readErrorLogged = true;
                    Debug.LogWarning($"TwinCAT ADS read error: {exception.Message}", this);
                }
            }
        }

        private void WriteIfChanged(int handle, bool value, ref bool writtenValue)
        {
            if (!isConnected || adsClient == null || handle == 0 || writtenValue == value) return;
            try
            {
                adsClient.WriteAny(handle, value);
                writtenValue = value;
                writeErrorLogged = false;
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                if (!writeErrorLogged)
                {
                    writeErrorLogged = true;
                    Debug.LogWarning($"TwinCAT ADS write error: {exception.Message}", this);
                }
            }
        }

        private void HandleExitPressed() => exitPulse?.Trigger();

        private void HandleWrongFlowerSelected() => wrongFlowerPulse?.Trigger();

        private void OnApplicationQuit()
        {
            FlushPulsesBeforeQuit();
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// "Exit Game" quits in the same frame the button is pressed, so finish any pending pulse here:
        /// TRUE for the configured time, then back to FALSE, before the connection closes.
        /// </summary>
        private void FlushPulsesBeforeQuit()
        {
            if (!isConnected || exitPulse == null) return;
            WriteStatusIfChanged(GameplayStatus.NotPlaying);
            Stopwatch clock = Stopwatch.StartNew();
            double previous = 0d;
            while (!(exitPulse.IsIdle && wrongFlowerPulse.IsIdle) && clock.Elapsed.TotalSeconds < QuitFlushTimeoutSeconds)
            {
                double now = clock.Elapsed.TotalSeconds;
                float deltaTime = (float)(now - previous);
                previous = now;
                WriteIfChanged(exitPressedHandle, exitPulse.Tick(deltaTime), ref writtenExitValue);
                WriteIfChanged(wrongFlowerHandle, wrongFlowerPulse.Tick(deltaTime), ref writtenWrongFlowerValue);
                Thread.Sleep(10);
            }
            exitPulse.Reset();
            wrongFlowerPulse.Reset();
            WriteIfChanged(exitPressedHandle, false, ref writtenExitValue);
            WriteIfChanged(wrongFlowerHandle, false, ref writtenWrongFlowerValue);
        }

        private void Disconnect()
        {
            if (adsClient != null)
            {
                try
                {
                    DeleteHandle(ref robotXHandle);
                    DeleteHandle(ref robotYHandle);
                    DeleteHandle(ref exitPressedHandle);
                    DeleteHandle(ref wrongFlowerHandle);
                    DeleteHandle(ref gameStatusHandle);
                    adsClient.Disconnect();
                    adsClient.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"TwinCAT ADS cleanup exception: {exception.Message}");
                }
            }
            adsClient = null;
            robotXHandle = robotYHandle = exitPressedHandle = wrongFlowerHandle = gameStatusHandle = 0;
            isConnected = false;
            hasRobotPosition = false;
            exitOutputAvailable = wrongFlowerOutputAvailable = false;
        }

        private void DeleteHandle(ref int handle)
        {
            if (handle != 0)
            {
                try { adsClient.DeleteVariableHandle(handle); }
                catch (Exception) { }
            }
            handle = 0;
        }
    }
}
