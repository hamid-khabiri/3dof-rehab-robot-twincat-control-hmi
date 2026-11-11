using System;
using UnityEngine;
using TwinCAT.Ads;

public sealed class TwincatConnectorSorter : MonoBehaviour
{
    [Header("ADS Connection Settings")]
    [SerializeField] private string amsNetId = "127.0.0.1.1.1";
    [SerializeField] private int amsPort = 851;
    [SerializeField] private string positionSymbol = "MAIN.fbSorterGame.stToUnity.fX";

    [Header("Unity -> PLC Symbols")]
    [SerializeField] private string gameClosedSymbol = "MAIN.fbSorterGame.stFromUnity.bGameClosed";   // BOOL
    [SerializeField] private string gameStatusSymbol = "MAIN.fbSorterGame.stFromUnity.nGameStatus";   // INT

    public TrolleyController trolleyController;

    [Header("Live Status (Read Only)")]
    public float currentXPercent = 0f;
    public bool isConnected;
    // 0 = not playing, 1..3 = playing with that bin weight. Set by SessionFlowController.
    public int currentGameStatus;

    private TcAdsClient adsClient;
    private int positionHandle;
    private int gameClosedHandle;
    private int gameStatusHandle;
    private bool hasLoggedReadError;
    private bool hasLoggedWriteError;

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
            positionHandle = adsClient.CreateVariableHandle(positionSymbol);

            isConnected = true;
            Debug.Log($"TwinCAT ADS connected ({amsNetId}:{amsPort}, '{positionSymbol}').", this);
        }
        catch (Exception ex)
        {
            isConnected = false;
            Debug.LogError($"TwinCAT ADS connection failed: {ex.Message}", this);
            return;
        }

        CreateStatusHandles();
    }

    // Kept separate so missing status symbols in the PLC never stop the robot position from being read.
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
                "Robot position is still read; game status is not sent.",
                this);
            DeleteHandle(ref gameClosedHandle);
            DeleteHandle(ref gameStatusHandle);
        }
    }

    public void SetGameStatus(int status)
    {
        currentGameStatus = status;
        Write(gameStatusHandle, (short)status);
    }

    private void SendGameClosed()
    {
        SetGameStatus(0);
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
        if (!isConnected || adsClient == null)
        {
            return;
        }

        try
        {
            currentXPercent = (float)adsClient.ReadAny(positionHandle, typeof(float));
            hasLoggedReadError = false;

            if (trolleyController != null)
            {
                trolleyController.Robot_Input_X = currentXPercent;
            }
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

    private void OnDestroy()
    {
        DisconnectPLC();
    }

    private void OnApplicationQuit()
    {
        DisconnectPLC();
    }

    private void DisconnectPLC()
    {
        if (adsClient == null)
        {
            return;
        }

        try
        {
            if (positionHandle != 0) adsClient.DeleteVariableHandle(positionHandle);
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
            positionHandle = 0;
            isConnected = false;
        }
    }
}
