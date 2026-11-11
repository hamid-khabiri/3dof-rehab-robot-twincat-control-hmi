using UnityEngine;
using System;
using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════════════
// ENUMS
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Clinical taxonomy of game types from the rehabilitation framework.</summary>
public enum GameType
{
    AnalyticOutcomeFocused,
    FunctionalMicrotask,
    CompositeSeriousGame,
    DualMotorCognitive,
    TherapistAuthored,
    AssessmentCalibration
}

/// <summary>ICF domain the game primarily targets.</summary>
public enum ClinicalDomain
{
    Sensorimotor,
    Cognitive,
    DualMotorCognitive,
    FunctionalADL,
    Assessment
}

/// <summary>Top-level game session lifecycle states.</summary>
public enum GameState { NotInitialized, Ready, Running, Paused, Ended }

/// <summary>
/// Per-trial outcome labels.
/// IndependentSuccess = patient completed the task without robot assistance.
/// AssistedSuccess    = task completed but robot contributed.
/// Dropped            = item was lost during the return phase.
/// RetractedEmpty     = hook retracted before contacting any item.
/// SafetyTerminated   = a safety policy fired and ended the trial.
/// Timeout            = the level timer expired during the trial.
/// </summary>
public enum TrialOutcome
{
    IndependentSuccess,
    AssistedSuccess,
    Dropped,
    RetractedEmpty,
    SafetyTerminated,
    Timeout
}

/// <summary>Robot operating modes (defined for future AAN integration).</summary>
public enum RobotMode { Transparent, Resistive, AngularGuidance, SafetyLimiting }

/// <summary>
/// Barrier type for ice / stone encased items (Level 8+).
/// None   = no barrier, item grasped immediately.
/// Ice    = 1.5 s drill at 4 N resistance.
/// Stone  = 3.0 s drill at 10 N resistance.
/// </summary>
public enum BarrierType { None, Ice, Stone }

/// <summary>Categories of safety events recorded in trial logs.</summary>
public enum SafetyEventType
{
    VelocityExceeded,
    ForceLimitExceeded,
    WorkspaceBoundaryViolation,
    GripDurationExceeded,
    EmergencyStop
}

/// <summary>
/// All collectible item types in the mine.
/// Gold variants have positive value; rocks have low value but high weight;
/// Diamond is high value, low weight; Bag has a random value; TNT wipes all money.
/// </summary>
public enum ItemType
{
    SmallGold,
    MediumGold,
    LargeGold,
    SmallRock,
    MediumRock,
    LargeRock,
    Diamond,
    Bag,
    TNT
}

// ═══════════════════════════════════════════════════════════════════════════
// GAME IDENTITY
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Static metadata describing this game instance.
/// Used by the logger to tag session data and by the therapist dashboard
/// to display game information.
/// </summary>
[Serializable]
public class GameIdentity
{
    public string         GameName       = "Gold Miner";
    public string         GameID         = "GM-v1";
    public string         GameVersion    = "1.0.0";
    public GameType       GameType       = GameType.CompositeSeriousGame;
    public ClinicalDomain ClinicalDomain = ClinicalDomain.Sensorimotor;
    public string         PrimaryOutcome = "Shoulder-elbow coordination via directional reaching";
    public string[]       SecondaryOutcomes = {
        "Strength under graded resistance",
        "Grip-force modulation",
        "Decision-making: gold vs rock vs diamond"
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// USER WORKSPACE PROFILE
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Stores the patient's calibrated workspace limits and robot parameters.
/// Set once at session start (or loaded from a saved calibration file)
/// and passed to all game components via GoldMinerGame.Initialise().
///
/// Key design decision — two separate reach scales:
///   MaxReachWorld   : how far the hook physically travels (drives claw visual)
///   SpawnScaleWorld : how far apart items are placed (drives spawner)
/// Decoupling these lets DDA increase MovementGain (making the patient work
/// harder) without changing where items appear on screen.
/// </summary>
[Serializable]
public class UserWorkspaceProfile
{
    // ── Patient identification ────────────────────────────────
    public string PatientID     = "TEST_01";
    public string CalibrationID = "CAL_DEFAULT";

    // ── DDA movement gain ─────────────────────────────────────
    /// <summary>
    /// Multiplier applied to the radial input by the robot interface.
    /// 1.0 = normal reach; 2.0 = patient must move twice as far to reach
    /// the same screen position. Increased by DDA as the primary difficulty axis.
    /// </summary>
    public float MovementGain = 1.0f;

    /// <summary>Scales patient's leftward input to design angle space. Set by calibration.</summary>
public float AngleGainLeft  = 1f;

/// <summary>Scales patient's rightward input to design angle space. Set by calibration.</summary>
public float AngleGainRight = 1f;

    /// <summary>
/// Half-angle in degrees that the fixed level layouts were designed for.
/// PolarToWorld remaps item angles from this design range into the patient's
/// calibrated ROM so items are always within their reachable cone.
/// </summary>
public float DesignAngleRange = 40f;

    // ── Angular workspace limits ──────────────────────────────
    public float AngleMinDeg = -40f;   // maximum left deflection in degrees
    public float AngleMaxDeg =  40f;   // maximum right deflection in degrees

    // ── Radial workspace limits ───────────────────────────────
    public float RadialMinM     =  0.05f;  // minimum safe reach (metres)
    public float RadialMaxSafeM =  0.30f;  // therapist-set safe maximum
    public float RadialComfortM =  0.25f;  // comfortable training reach

    // ── Force limits ──────────────────────────────────────────
    // 10 N = the robot's maximum commandable force, and the ceiling the Gold
    // Miner item-weight table (MineItemSpawner.Weights) is scaled against.
    // Lower it per patient; raising it above 10 asks for force the hardware
    // cannot deliver.
    public float FSafeNewtons = 10f;   // maximum safe resistive force from robot

    // ── Grip limits ───────────────────────────────────────────
    public float GripMinN = 2f;        // minimum detectable grip force
    public float GripMaxN = 25f;       // maximum voluntary grip force

    // ── Safety ───────────────────────────────────────────────
    public float MaxSafeVelocity = 999f;   // m/s — set low on real hardware

    // ── Vibrotactile ──────────────────────────────────────────
    public float VibrationDetectionThreshold = 0.2f;

    // ── World-space mapping ───────────────────────────────────
    /// <summary>World position of the miner / hoist anchor point.</summary>
    public Vector2 AnchorWorld     = new Vector2(0f, 4f);

    /// <summary>Maximum distance the hook travels in world units.</summary>
    public float   MaxReachWorld   = 12f;

    /// <summary>
    /// Scale factor for item spawn positions — independent of hook reach.
    /// Increasing MaxReachWorld (DDA) does not move items on screen.
    /// </summary>
    public float   SpawnScaleWorld = 5f;

    // ── Coordinate conversion ─────────────────────────────────

    /// <summary>
/// Converts polar level coordinates to Unity world space.
/// angleDeg and radial01 are in the level's design space.
///
/// This is a straight polar-to-cartesian conversion -- it does NOT remap
/// anything into the patient's measured ROM. The angular remap happens on
/// the robot side (AngleGainLeft/AngleGainRight scale the patient's physical
/// sweep up to DesignAngleRange), and the radial axis is not remapped at all:
/// items are laid out against SpawnScaleWorld while the hook tip travels
/// against MaxReachWorld, which are independent by design. So a position this
/// returns is NOT automatically reachable -- run it through ClampToHookReach()
/// when that matters.
/// </summary>
public Vector3 PolarToWorld(float angleDeg, float radial01)
{
    float angleRad = angleDeg * Mathf.Deg2Rad;
    float reach    = radial01 * SpawnScaleWorld;
    return new Vector3(
         Mathf.Sin(angleRad) * reach + AnchorWorld.x,
        -Mathf.Cos(angleRad) * reach + AnchorWorld.y,
        0f);
}

    // -- Hook reach envelope ----------------------------------
    //
    // The hook tip sits at AnchorWorld + (sin, -cos) * radial * MaxReachWorld,
    // with radial clamped to 0-1 (see GoldMinerGame.UpdateClawVisual). Items
    // are placed by PolarToWorld, which scales by SpawnScaleWorld instead.
    // Nothing ties those two scales together, so an item can end up outside
    // the cone the hook can be driven through -- most easily on the radial
    // axis, because calibration overwrites MaxReachWorld with the patient's
    // measured reach (as little as 3 world units) while SpawnScaleWorld stays
    // where the layouts were authored. These members define that cone and
    // project stray positions onto it.

    /// <summary>
    /// Half-angle of the cone the hook can be steered through, in the same
    /// design space PolarToWorld() consumes.
    ///
    /// Deliberately NOT AngleMinDeg/AngleMaxDeg: those are the patient's
    /// measured physical ROM, which calibration maps ONTO this design cone via
    /// AngleGainLeft/AngleGainRight, so they are not level-space angles. Every
    /// simulated interface clamps the commanded claw angle at or beyond this
    /// (MKB at DesignAngleRange + 15 deg, the joystick at its own fixed 55 deg),
    /// which is what makes anything inside this cone steerable to.
    /// </summary>
    public float ReachAngleLimitDeg => Mathf.Abs(DesignAngleRange);

    /// <summary>
    /// Farthest the hook tip can travel from the anchor, in world units --
    /// i.e. MaxReachWorld, reached when the radial input saturates at 1.
    /// </summary>
    public float ReachRadiusWorld => Mathf.Max(0f, MaxReachWorld);

    /// <summary>
    /// Inverse of PolarToWorld. distWorld comes back in world units (divide by
    /// SpawnScaleWorld for a radial01); angleDeg is measured from straight
    /// down, positive to the right, matching PolarToWorld's convention.
    /// </summary>
    public void WorldToPolar(Vector3 world, out float angleDeg, out float distWorld)
    {
        float dx = world.x - AnchorWorld.x;
        float dy = world.y - AnchorWorld.y;

        distWorld = Mathf.Sqrt(dx * dx + dy * dy);
        angleDeg  = distWorld < 1e-5f ? 0f : Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Projects a world position onto the region the hook can actually be
    /// driven to. A position already inside comes back untouched; one outside
    /// lands on the nearest edge, keeping its angle wherever possible.
    /// </summary>
    /// <param name="edgeMargin01">
    /// Fraction of full reach to inset the outer radius by, so a position
    /// pulled in does not sit at literally 100% extension. 0 = exactly the edge.
    /// </param>
    /// <param name="minRadius01">
    /// Fraction of full reach that a position may not come closer than, which
    /// keeps a pulled-in item out of the hook's home region, where it would be
    /// collected without the patient reaching at all.
    /// </param>
    public Vector3 ClampToHookReach(Vector3 world,
                                    float edgeMargin01 = 0f,
                                    float minRadius01  = 0f)
    {
        WorldToPolar(world, out float angleDeg, out float distWorld);

        float limit = ReachAngleLimitDeg;
        angleDeg = Mathf.Clamp(angleDeg, -limit, limit);

        float maxDist = ReachRadiusWorld * (1f - Mathf.Clamp01(edgeMargin01));
        float minDist = Mathf.Min(ReachRadiusWorld * Mathf.Clamp01(minRadius01), maxDist);
        distWorld = Mathf.Clamp(distWorld, minDist, maxDist);

        float angleRad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(
             Mathf.Sin(angleRad) * distWorld + AnchorWorld.x,
            -Mathf.Cos(angleRad) * distWorld + AnchorWorld.y,
            world.z);
    }

    /// <summary>
    /// True if ClampToHookReach() would leave this position where it is, under
    /// the same margins.
    /// </summary>
    public bool IsWithinHookReach(Vector3 world,
                                  float edgeMargin01 = 0f,
                                  float minRadius01  = 0f)
        => (ClampToHookReach(world, edgeMargin01, minRadius01) - world).sqrMagnitude < 1e-6f;

    /// <summary>
    /// Converts a pixel position from the original 1920×1080 Construct 3
    /// level layout into Unity world space. Used by the fixed level spawner
    /// to place items at their original designed positions.
    /// Original anchor was at approximately (960, 180) in pixel space.
    /// </summary>
    public Vector3 PixelToWorld(float px, float py)
    {
        float nx = (px - 960f) / 600f;
        float ny = (py - 180f) / 400f;

        return new Vector3(
            nx  * (SpawnScaleWorld * 0.9f)  + AnchorWorld.x,
            AnchorWorld.y - ny * SpawnScaleWorld * 0.75f,
            0f);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GAME PARAMETERS
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// A single tunable game parameter with clinical bounds, safety limits,
/// DDA eligibility, and difficulty direction.
/// Value is always clamped to [SafeMin, SafeMax] on assignment.
/// </summary>
[Serializable]
public class GameParameter
{
    public string Name;
    public string ClinicalMeaning;
    public string Unit;

    public float Min, Max;         // absolute allowed range
    public float SafeMin, SafeMax; // enforced range (may be narrower than Min/Max)
    public float Default;

    public bool  DDAAllowed;         // true = DDA may change this parameter
    public bool  TherapistOnly;      // true = only a therapist can change this
    public bool  IncreaseIsHarder;   // true = higher value = more difficult
    public float MaxStepPerProgression; // maximum change per DDA event

    private float _value;

    /// <summary>Current value, clamped to [SafeMin, SafeMax] on every set.</summary>
    public float Value
    {
        get => _value;
        set => _value = Mathf.Clamp(value, SafeMin, SafeMax);
    }

    public void Reset() => _value = Default;
}

/// <summary>
/// Collection of all game parameters, keyed by name.
/// Provides safe null-tolerant accessors for read and write.
/// </summary>
public class GameParameterSet
{
    private readonly Dictionary<string, GameParameter> _p = new();

    /// <summary>Add a parameter. Calls Reset() to initialise it to its Default value.</summary>
    public void Add(GameParameter p) { p.Reset(); _p[p.Name] = p; }

    /// <summary>Returns the parameter object, or null if not found.</summary>
    public GameParameter Get(string n) =>
        _p.TryGetValue(n, out var v) ? v : null;

    /// <summary>Returns the current value, or 0 if the parameter does not exist.</summary>
    public float Val(string n) => Get(n)?.Value ?? 0f;

    /// <summary>Sets a parameter value. Silently does nothing if the key is not found.</summary>
    public void Set(string n, float v)
    {
        if (_p.TryGetValue(n, out var p)) p.Value = v;
    }

    /// <summary>Returns a name → value snapshot of all current parameter values.</summary>
    public Dictionary<string, float> Snapshot()
    {
        var s = new Dictionary<string, float>();
        foreach (var kv in _p) s[kv.Key] = kv.Value.Value;
        return s;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// PERFORMANCE METRICS
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// All performance metrics recorded for a single trial (one complete
/// reach-and-return cycle). Logged by GameLogger and used by the
/// post-session summary and DDA evaluation.
/// </summary>
[Serializable]
public class TrialMetrics
{
    // ── Temporal ──────────────────────────────────────────────
    /// <summary>Time from BeginTrial() to the grasp event (seconds).</summary>
    public float TimeToGraspSeconds;

    /// <summary>Time from grasp to successful return at home (seconds).</summary>
    public float ReturnDurationSeconds;

    // ── Movement quality ──────────────────────────────────────
    /// <summary>
    /// Actual path length / Euclidean distance for the full reach + return.
    /// 1.0 = perfectly straight. Values below ~0.7 suggest motor planning difficulty.
    /// </summary>
    public float PathEfficiency;

    /// <summary>∫ grip × dt during the return phase (N·s). Proxy for sustained pull force.</summary>
    public float PullForceTotalNs;

    /// <summary>Root-mean-square error of grip force relative to the threshold (N).</summary>
    public float GripForceRMSError;

    // ── Decision-making ───────────────────────────────────────
    /// <summary>True if the item was gold, diamond, or bag (a valuable target).</summary>
    public bool WasValueTarget;

    /// <summary>True if the patient chose to grasp a valuable item (monetaryValue > 0).</summary>
    public bool GraspedCorrectly;

    // ── Reach parameters at grasp moment ─────────────────────
    public float ReachAngleDeg;   // hook angle at the moment of grasp
    public float ReachRadial01;   // normalised depth at the moment of grasp

    // ── Assistance ────────────────────────────────────────────
    /// <summary>True if the trial completed without robot assistance (pure DDA input).</summary>
    public bool  IndependentSuccess;

    /// <summary>Percentage of the task estimated to have been completed by the robot (0–100).</summary>
    public float AssistancePercent;

    // ── Safety ───────────────────────────────────────────────
    public int SafetyEventCount;

    // ── Item info ─────────────────────────────────────────────
    public ItemType ItemType;
    public float    ItemValue;    // dollars, may be negative for TNT
    public float    ItemWeight;   // Newtons of resistance applied during return
}

/// <summary>
/// Aggregated metrics for the current level session.
/// Reset at the start of each level by GoldMinerGame.StartLevel().
/// </summary>
[Serializable]
public class SessionMetrics
{
    public int   TotalReaches;
    public float LevelCompletionTimeSeconds;   // set in EndLevel() — primary time PM
    public int   SuccessfulReturns;
    public int   Drops;
    public float TotalValue;
    public float MeanPathEfficiency;
    public float MeanGripRMSE;

    /// <summary>
    /// Primary performance metric: total value earned divided by session duration
    /// in minutes. Captures both reach quantity and item selection quality.
    /// </summary>
    public float ValueWeightedReachPerMin;

    public List<TrialMetrics> Trials = new();
}

// ═══════════════════════════════════════════════════════════════════════════
// LOGGING
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Complete record for a single trial, written to disk by GameLogger.
/// Contains the outcome, all performance metrics, a parameter snapshot
/// (so the DDA state at the time of the trial is preserved), and any
/// safety events that occurred.
/// </summary>
[Serializable]
public class TrialLog
{
    public int                       TrialIndex;
    public DateTime                  Timestamp;
    public TrialOutcome              Outcome;
    public TrialMetrics              Metrics;
    public Dictionary<string, float> ParameterSnapshot;  // DDA state at trial time
    public float                     AssistancePercent;
    public List<SafetyEvent>         SafetyEvents = new();
    public float                     DurationSeconds;
}

/// <summary>
/// A single safety violation event recorded during a trial.
/// Attached to TrialLog.SafetyEvents so the full safety history is preserved.
/// </summary>
[Serializable]
public class SafetyEvent
{
    public float          Timestamp;
    public SafetyEventType Type;
    public float          Value;   // the offending measurement (e.g. velocity in m/s)
}

// ═══════════════════════════════════════════════════════════════════════════
// LEVEL DATA  (legacy Construct 3 item format — used by PixelToWorld path)
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// A single item entry parsed from the original Construct 3 level code string.
/// Pixel positions are converted to world space via UserWorkspaceProfile.PixelToWorld().
/// </summary>
[Serializable]
public class LevelItemData
{
    public string typeName;
    public float  px, py;    // original pixel position on the 1920×1080 canvas
    public float  pw, ph;    // original pixel size (used to select small/medium/large variant)
    public float  weight;    // resistive force in Newtons
    public float  value;     // monetary value in dollars
}

/// <summary>
/// Parsed level data from a Construct 3 level code string.
/// Contains the money goal, original timer, and the full list of item placements.
/// </summary>
[Serializable]
public class LevelData
{
    public float              moneyGoal;
    public float              timerSeconds;
    public List<LevelItemData> items = new();
}