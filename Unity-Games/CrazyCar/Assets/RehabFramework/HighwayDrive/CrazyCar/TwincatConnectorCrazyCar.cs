using System;
using UnityEngine;
using TwinCAT.Ads;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  CRAZY CAR <-> TWINCAT (ADS)
    //  Same pattern as Cargo Sorter's TwincatConnectorSorter: connect once in
    //  Start(), Unity PULLS the robot every frame (the PLC never pushes), and
    //  Unity writes the game state only when it changes.
    //
    //  PLC -> Unity:  fY          REAL  gas pedal, 0..100 % of the workspace
    //                 fWristAngle REAL  wrist rotation, degrees, 0 = neutral
    //  Unity -> PLC:  bGameClosed BOOL  TRUE once Unity closes
    //                 nGameStatus INT   0 = a popup or menu is showing,
    //                                   1 = Taxi, 2 = Van, 3 = Truck (driving)
    //
    //  The write handles live in their own try/catch: if the PLC does not
    //  have the stFromUnity symbols yet, the robot is still read.
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class TwincatConnectorCrazyCar : MonoBehaviour
    {
        [Header("ADS Connection Settings")]
        [SerializeField] private string amsNetId = "127.0.0.1.1.1";
        [SerializeField] private int amsPort = 851;

        [Header("PLC -> Unity Symbols")]
        [SerializeField] private string gasSymbol = "MAIN.fbCrazyCar.stToUnity.fY";            // REAL, 0..100 %
        [SerializeField] private string wristSymbol = "MAIN.fbCrazyCar.stToUnity.fWristAngle"; // REAL, degrees

        [Header("Unity -> PLC Symbols")]
        [SerializeField] private string gameClosedSymbol = "MAIN.fbCrazyCar.stFromUnity.bGameClosed"; // BOOL
        [SerializeField] private string gameStatusSymbol = "MAIN.fbCrazyCar.stFromUnity.nGameStatus"; // INT

        [Header("Live Status (Read Only)")]
        public float currentYPercent;
        public float currentWristDeg;
        public bool isConnected;
        // 0 = popup or menu showing, 1..3 = driving the Taxi / Van / Truck. Set by CrazyCarSessionFlow.
        public int currentGameStatus;

        public float CurrentYPercent => currentYPercent;
        public float CurrentWristDeg => currentWristDeg;
        public bool IsConnected => isConnected;

        private TcAdsClient adsClient;
        private int gasHandle;
        private int wristHandle;
        private int gameClosedHandle;
        private int gameStatusHandle;
        private bool hasLoggedReadError;
        private bool hasLoggedWriteError;
        private bool closedSent;

        private void Start()
        {
            ConnectToPLC();
        }

        public void ConnectToPLC()
        {
            try
            {
                adsClient = new TcAdsClient();
                adsClient.Connect(amsNetId, amsPort);
                gasHandle = adsClient.CreateVariableHandle(gasSymbol);
                wristHandle = adsClient.CreateVariableHandle(wristSymbol);

                isConnected = true;
                Debug.Log($"TwinCAT ADS connected ({amsNetId}:{amsPort}, '{gasSymbol}', '{wristSymbol}').", this);
            }
            catch (Exception ex)
            {
                isConnected = false;
                Debug.LogWarning($"TwinCAT ADS connection failed: {ex.Message}. The keyboard drives the car instead.", this);
                return;
            }

            CreateStatusHandles();
        }

        // Kept separate so missing status symbols in the PLC never stop the robot from being read.
        private void CreateStatusHandles()
        {
            try
            {
                gameClosedHandle = adsClient.CreateVariableHandle(gameClosedSymbol);
                gameStatusHandle = adsClient.CreateVariableHandle(gameStatusSymbol);
                Write(gameClosedHandle, false);
                Write(gameStatusHandle, (short)currentGameStatus);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"TwinCAT status symbols not available ('{gameClosedSymbol}', '{gameStatusSymbol}'): {ex.Message}. " +
                    "The robot is still read; game status is not sent.",
                    this);
                DeleteHandle(ref gameClosedHandle);
                DeleteHandle(ref gameStatusHandle);
            }
        }

        /// <summary>Written only when the value changes.</summary>
        public void SetGameStatus(int status)
        {
            if (status == currentGameStatus) return;
            currentGameStatus = status;
            Write(gameStatusHandle, (short)status);
        }

        private void SendGameClosed()
        {
            if (closedSent) return;
            closedSent = true;
            currentGameStatus = 0;
            Write(gameStatusHandle, (short)0);
            Write(gameClosedHandle, true);
        }

        private void Write(int handle, object value)
        {
            if (!isConnected || adsClient == null || handle == 0) return;
            try
            {
                adsClient.WriteAny(handle, value);
                hasLoggedWriteError = false;
            }
            catch (Exception ex)
            {
                if (!hasLoggedWriteError)
                {
                    Debug.LogWarning($"TwinCAT ADS write error: {ex.Message}", this);
                    hasLoggedWriteError = true;
                }
            }
        }

        private void DeleteHandle(ref int handle)
        {
            if (handle != 0 && adsClient != null)
            {
                try { adsClient.DeleteVariableHandle(handle); } catch (Exception) { }
            }
            handle = 0;
        }

        private void Update()
        {
            if (!isConnected || adsClient == null) return;

            try
            {
                currentYPercent = (float)adsClient.ReadAny(gasHandle, typeof(float));
                currentWristDeg = (float)adsClient.ReadAny(wristHandle, typeof(float));
                hasLoggedReadError = false;
            }
            catch (Exception ex)
            {
                // Log once per failure streak so a dropped connection doesn't flood the console every frame.
                if (!hasLoggedReadError)
                {
                    Debug.LogWarning($"TwinCAT ADS read error: {ex.Message}", this);
                    hasLoggedReadError = true;
                }
            }
        }

        // Exit button, closing the window, or stopping Play mode in the editor.
        private void OnApplicationQuit()
        {
            SendGameClosed();
            DisconnectPLC();
        }

        private void OnDestroy()
        {
            SendGameClosed();
            DisconnectPLC();
        }

        private void DisconnectPLC()
        {
            if (adsClient == null) return;

            try
            {
                DeleteHandle(ref gasHandle);
                DeleteHandle(ref wristHandle);
                DeleteHandle(ref gameClosedHandle);
                DeleteHandle(ref gameStatusHandle);
                adsClient.Disconnect();
                adsClient.Dispose();
                Debug.Log("TwinCAT ADS disconnected.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"TwinCAT ADS cleanup exception: {ex.Message}");
            }
            finally
            {
                adsClient = null;
                isConnected = false;
            }
        }
    }
}
