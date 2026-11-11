using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using TwinCAT.Ads;

public class TwinCATManager : MonoBehaviour
{
    public static TwinCATManager Instance { get; private set; }

    [Header("── ADS Connection Settings ──")]
    [Tooltip("Target AmsNetId (e.g. '127.0.0.1.1.1' for local testing)")]
    [SerializeField] private string amsNetId = "127.0.0.1.1.1";
    [SerializeField] private int amsPort = 851; // Default PLC Port for TwinCAT 3 (TC3)

    [Header("── Struct Memory Offsets (TwinCAT FB_PeelingGame) ──")]
    [Tooltip("Symbol path for stToUnity structure in TwinCAT")]
    [SerializeField] private string toUnitySymbol = "MAIN.fbPeelingGame.stToUnity";

    [Tooltip("Symbol path for stFromUnity structure in TwinCAT")]
    [SerializeField] private string fromUnitySymbol = "MAIN.fbPeelingGame.stFromUnity";

    [Header("── Menu Overlays ──")]
    [Tooltip("Optional reference to the 'Choose Exercise Level' UI panel.")]
    [SerializeField] private GameObject levelSelectMenuPanel;

    [Header("── Live Status (Read Only) ──")]
    public float currentYPercent = 0f;
    public int currentFruitType = 0;
    public bool isGameActiveInPLC = false;
    public bool isConnected = false;
    public bool isLevelSelectOpen = false; // Toggleable manually or via UI buttons

    private TcAdsClient adsClient;
    private int handleToUnity;
    private int handleFromUnity;

    private PeelerController cachedPeeler;

    // ── ADS Memory Layouts (Working ADS Structs) ──
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ST_PeelerToUnity
    {
        public float fY;                     // REAL (0.0 to 100.0 %)
        [MarshalAs(UnmanagedType.I1)]
        public bool GameIsActive;            // BOOL
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ST_PeelerFromUnity
    {
        [MarshalAs(UnmanagedType.I1)]
        public bool bInContact;              // BOOL (1st in TwinCAT)

        public short nObjectType;            // INT  (2nd in TwinCAT)

        [MarshalAs(UnmanagedType.I1)]
        public bool bGameFinished;           // BOOL (3rd in TwinCAT)
    }

    private ST_PeelerFromUnity outgoingDataFromUnity;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        outgoingDataFromUnity = new ST_PeelerFromUnity
        {
            bInContact = false,
            nObjectType = 0,
            bGameFinished = false
        };
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedPeeler = FindFirstObjectByType<PeelerController>();
    }

    private void Start()
    {
        ConnectToPLC();
    }

    /// <summary>
    /// Checks if any level selection UI menu is active on screen.
    /// </summary>
    private bool IsLevelSelectUIActive()
    {
        // 1. Manual code override toggle
        if (isLevelSelectOpen) return true;

        // 2. Direct Inspector reference
        if (levelSelectMenuPanel != null && levelSelectMenuPanel.activeInHierarchy) return true;

        // 3. Session menus and popups (main menu, settings, pause, result)
        if (PeelerSessionFlow.Instance != null && !PeelerSessionFlow.Instance.IsPlaying) return true;

        return false;
    }

    /// <summary>
    /// Returns 0 for Menus / Level Selection, or 1-5 for active gameplay.
    /// </summary>
    private short GetActiveFruitID()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        // 1. Send 0 if in MainMenu or CompletionScreen
        if (sceneName.Equals("MainMenu", StringComparison.OrdinalIgnoreCase) ||
            sceneName.Equals("CompletionScreen", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        // 2. Send 0 if 'Choose Level' UI menu overlay is open
        if (IsLevelSelectUIActive())
        {
            return 0;
        }

        // 3. Priority check: GlobalLevelManager level selection
        if (GlobalLevelManager.Instance != null && GlobalLevelManager.Instance.CurrentFruitLevelId > 0)
        {
            return (short)GlobalLevelManager.Instance.CurrentFruitLevelId;
        }

        // 4. Scene Name detection fallback
        if (sceneName.Contains("Kiwi")) return 1;
        if (sceneName.Contains("Orange")) return 2;
        if (sceneName.Contains("Grapefruit")) return 3;
        if (sceneName.Contains("Melon")) return 4;
        if (sceneName.Contains("Pineapple")) return 5;
        if (sceneName.Equals("GameScreen", StringComparison.OrdinalIgnoreCase)) return 1;

        return 0;
    }

    public void ConnectToPLC()
    {
        try
        {
            adsClient = new TcAdsClient();
            adsClient.Connect(amsNetId, amsPort);

            handleToUnity = adsClient.CreateVariableHandle(toUnitySymbol);
            handleFromUnity = adsClient.CreateVariableHandle(fromUnitySymbol);

            isConnected = true;
            Debug.Log("✅ TwinCAT ADS Connected successfully!");
        }
        catch (Exception ex)
        {
            isConnected = false;
            Debug.LogError($"❌ TwinCAT ADS Connection Failed: {ex.Message}");
        }
    }

    private void Update()
    {
        short fruitId = GetActiveFruitID();
        outgoingDataFromUnity.nObjectType = fruitId;
        currentFruitType = fruitId;

        if (!isConnected || adsClient == null) return;

        try
        {
            // 1. READ Position (fY) and Active state FROM TwinCAT
            ST_PeelerToUnity incomingData = (ST_PeelerToUnity)adsClient.ReadAny(handleToUnity, typeof(ST_PeelerToUnity));
            currentYPercent = incomingData.fY;
            isGameActiveInPLC = incomingData.GameIsActive;

            if (cachedPeeler == null)
            {
                cachedPeeler = FindFirstObjectByType<PeelerController>();
            }

            if (cachedPeeler != null)
            {
                cachedPeeler.robotYPercent = currentYPercent;
            }

            // 2. WRITE Contact, Fruit Type (0-5), and Game Finish state TO TwinCAT
            adsClient.WriteAny(handleFromUnity, outgoingDataFromUnity);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"⚠️ ADS Communication Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Call this from UI buttons or GlobalLevelManager when opening/closing the level selection panel.
    /// </summary>
    public void SetLevelSelectUIOpen(bool isOpen)
    {
        isLevelSelectOpen = isOpen;
    }

    public void SetFruitContactState(bool inContact)
    {
        outgoingDataFromUnity.bInContact = inContact;
    }

    public void SetGameFinished(bool finished)
    {
        outgoingDataFromUnity.bGameFinished = finished;
    }

    private void OnDestroy()
    {
        DisconnectPLC();
    }

    private void OnApplicationQuit()
    {
        // Exit button, closing the window, or stopping Play mode:
        // tell the PLC there is no fruit (no resistance) and the game is finished.
        outgoingDataFromUnity.bInContact = false;
        outgoingDataFromUnity.nObjectType = 0;
        outgoingDataFromUnity.bGameFinished = true;

        if (isConnected && adsClient != null)
        {
            try
            {
                adsClient.WriteAny(handleFromUnity, outgoingDataFromUnity);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ADS final write failed: {ex.Message}");
            }
        }

        DisconnectPLC();
    }

    private void DisconnectPLC()
    {
        if (adsClient != null)
        {
            isConnected = false;
            try
            {
                if (handleToUnity != 0) adsClient.DeleteVariableHandle(handleToUnity);
                if (handleFromUnity != 0) adsClient.DeleteVariableHandle(handleFromUnity);
                adsClient.Disconnect();
                adsClient.Dispose();
                Debug.Log("🔌 TwinCAT ADS Disconnected.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ADS Cleanup Exception: {ex.Message}");
            }
            adsClient = null; // OnApplicationQuit and OnDestroy both call this
        }
    }
}