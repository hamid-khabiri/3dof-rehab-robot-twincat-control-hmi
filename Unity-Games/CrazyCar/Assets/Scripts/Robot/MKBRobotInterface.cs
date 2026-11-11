using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse + Keyboard simulator for the polar robot interface.
/// Used during development and lab demonstrations in place of real hardware.
///
/// The mouse position is converted from screen space to world space and then
/// to polar coordinates (angle, radial) relative to the calibrated anchor point,
/// exactly mirroring the conical ROM the real robot would use.
///
/// Controls:
///   Mouse position  →  claw angle and radial extension (mirrors world position)
///   LMB / Space     →  grip force (hold = squeeze, Space = toggle)
///
/// When the real robot is available, swap this component for RealRobotInterface
/// on the RobotInterface GameObject — no other changes needed.
/// </summary>
[AddComponentMenu("RehabGame/Robot/MKB Robot Simulator")]
public class MKBRobotInterface : RobotInterface
{
    // ═══════════════════════════════════════════════════════════
    // INSPECTOR FIELDS
    // ═══════════════════════════════════════════════════════════

    [Header("Grip simulation")]
    [Tooltip("Force value (N) reported when grip is fully activated")]
    public float gripMaxForce = 20f;

    [Tooltip("Time (s) for grip force to rise from 0 to max — simulates muscle onset")]
    public float gripRiseTime = 0.15f;

    [Header("Debug overlay")]
    [Tooltip("Show the on-screen debug readout during Play mode")]
    public bool showOnScreenDebug = false;

    [Header("Testing")]
    [Tooltip("Apply MovementGain to the MKB radial. " +
             "Leave OFF for normal mouse testing; enable only to simulate DDA gain on hardware.")]
    public bool applyGainInMKB = false;

    [Header("Wrist rotation (Clear the View)")]
    [Tooltip("Rate (deg/s) the wrist rotates while A/D or Left/Right is held.")]
    public float wristRateDegPerS = 45f;
    [Tooltip("If true, releasing both keys springs the wrist back toward 0 over " +
             "wristSpringReturnTimeS. If false (default), releasing holds the current angle.")]
    public bool wristSpringReturn = false;
    [Tooltip("Seconds for a full spring-return from the current angle to 0, when enabled.")]
    public float wristSpringReturnTimeS = 0.5f;

    // ═══════════════════════════════════════════════════════════
    // PRIVATE STATE
    // ═══════════════════════════════════════════════════════════

    // ── Polar robot outputs ───────────────────────────────────
    private float _angleDeg;       // current claw angle in degrees (negative = left)
    private float _radial;         // normalised reach 0–1
    private float _gripForce;      // current grip force in Newtons
    private float _velocity;       // rate of radial change (proxy for end-effector speed)
    private float _rawDist = 0f;   // unclamped world distance from anchor, updated every frame
    private float _angleGainLeft   = 1f;
private float _angleGainRight  = 1f;
private float _angleDesignRange = 40f;
    // ── Grip state ────────────────────────────────────────────
    private bool  _toggleGrip;     // Space key toggles grip on/off for one-handed testing

    // ── Robot output state ────────────────────────────────────
    private float _resistiveForce; // stored for debug display (not applied in simulation)
    private float _vibrationTimer; // countdown timer for vibration logging

    // ── Velocity estimation ───────────────────────────────────
    private float _prevRadial;

    // ── Wrist rotation (Clear the View) ───────────────────────
    private float _wristAngleDeg;
    private float _prevWristAngleDeg;
    private float _wristVelocityDegS;
    private float _wristMinDeg = float.NegativeInfinity;
    private float _wristMaxDeg = float.PositiveInfinity;

    // ── Cached workspace profile values ──────────────────────
    // Set by GoldMinerGame.Initialise() via SetProfile()
    private Vector2 _anchorWorld = new Vector2(0f, 4f);
    private float   _maxReach   = 10f;
    private float   _angleMin   = -45f;
    private float   _angleMax   =  45f;
    private float   _movementGain = 1.0f;

    // ── Input system references ───────────────────────────────
    private Mouse    _mouse;
    private Keyboard _keyboard;

    // ── GUI style cache ───────────────────────────────────────
    private GUIStyle _debugStyle;

    // ═══════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    private void Awake()
    {
        _mouse    = Mouse.current;
        _keyboard = Keyboard.current;
    }

    private void Update()
    {
        // Re-acquire devices if they were not connected at Awake
        if (_mouse    == null) _mouse    = Mouse.current;
        if (_keyboard == null) _keyboard = Keyboard.current;
        if (_mouse    == null || _keyboard == null) return;

        // ── Convert mouse screen position → world position ────
        Vector3 mouseScreen = new Vector3(
            _mouse.position.x.ReadValue(),
            _mouse.position.y.ReadValue(),
            10f);   // z = distance from camera plane
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouseScreen);

        // ── Polar conversion: anchor → mouse ──────────────────
        float dx = mouseWorld.x - _anchorWorld.x;
        float dy = mouseWorld.y - _anchorWorld.y;

        // Angle measured from vertical (straight down = 0°)
        // Before gain:
float angleRad = Mathf.Atan2(dx, -dy);
float angleDeg = angleRad * Mathf.Rad2Deg;

// Scale patient's physical angle into design space asymmetrically.
// e.g. patient ±30° physical → ±40° design output
float gainedAngle = angleDeg >= 0f
    ? angleDeg * _angleGainRight
    : angleDeg * _angleGainLeft;

// Clamp to design range, not physical ROM
_angleDeg = Mathf.Clamp(gainedAngle, -(_angleDesignRange + 15f), _angleDesignRange + 15f);

        // Radial = distance from anchor normalised to maxReach
        // applyGainInMKB scales it to simulate DDA MovementGain on real hardware
        float dist   = Mathf.Sqrt(dx * dx + dy * dy);
_rawDist     = dist;                                          // ← store before clamp
float gain   = applyGainInMKB ? _movementGain : 1.0f;
_radial      = Mathf.Clamp01((dist / _maxReach) * gain);

        // Only allow extension downward — mouse above anchor = hook at home
        if (dy > 0f) _radial = 0f;

        // ── Velocity estimation ───────────────────────────────
        _velocity   = Mathf.Abs(_radial - _prevRadial) / Time.deltaTime;
        _prevRadial = _radial;

        // ── Grip ──────────────────────────────────────────────
// Space toggles grip on/off (useful for one-handed testing at a desk)
if (_keyboard.spaceKey.wasPressedThisFrame) _toggleGrip = !_toggleGrip;

bool  wantGrip = /* _mouse.leftButton.isPressed || */ _toggleGrip;
float target   = wantGrip ? gripMaxForce : 0f;
        _gripForce = Mathf.MoveTowards(
            _gripForce, target,
            (gripMaxForce / gripRiseTime) * Time.deltaTime);

        // Vibration countdown (real hardware would fire a waveform here)
        if (_vibrationTimer > 0f) _vibrationTimer -= Time.deltaTime;

        // ── Wrist rotation (Clear the View): hold-to-rotate, independent of
        //    the mouse/grip controls above. A/Left = negative, D/Right = positive.
        float wristDir = 0f;
        if (_keyboard.aKey.isPressed || _keyboard.leftArrowKey.isPressed)  wristDir -= 1f;
        if (_keyboard.dKey.isPressed || _keyboard.rightArrowKey.isPressed) wristDir += 1f;

        if (wristDir != 0f)
        {
            _wristAngleDeg += wristDir * wristRateDegPerS * Time.deltaTime;
        }
        else if (wristSpringReturn)
        {
            float rate = Mathf.Abs(_wristAngleDeg) / Mathf.Max(0.01f, wristSpringReturnTimeS);
            _wristAngleDeg = Mathf.MoveTowards(_wristAngleDeg, 0f, rate * Time.deltaTime);
        }
        _wristAngleDeg = Mathf.Clamp(_wristAngleDeg, _wristMinDeg, _wristMaxDeg);

        _wristVelocityDegS = (_wristAngleDeg - _prevWristAngleDeg) / Time.deltaTime;
        _prevWristAngleDeg = _wristAngleDeg;
    }

    // ═══════════════════════════════════════════════════════════
    // PROFILE INJECTION
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Called by GoldMinerGame after Initialise() so the MKB knows the
    /// patient's anchor point, reach limits, and current DDA movement gain.
    /// Must be called again whenever DDA updates MovementGain mid-session.
    /// </summary>
    public void SetProfile(UserWorkspaceProfile profile)
{
    _anchorWorld      = profile.AnchorWorld;
    _maxReach         = profile.MaxReachWorld;
    _movementGain     = profile.MovementGain;
    _angleGainLeft    = profile.AngleGainLeft;
    _angleGainRight   = profile.AngleGainRight;
    _angleDesignRange = profile.DesignAngleRange;
}

    // ═══════════════════════════════════════════════════════════
    // ROBOT INTERFACE IMPLEMENTATION
    // ═══════════════════════════════════════════════════════════

    public override float GetClawAngleDeg()      => _angleDeg;
    public override float GetRadialExtension()   => _radial;
    public override float GetWristAngle()        => 0f;   // not simulated in MKB
    public override float GetGripForce()         => _gripForce;
    public override float GetVelocityMagnitude() => _velocity;

    public override float GetWristAngleDeg()             => _wristAngleDeg;
    public override float GetWristAngularVelocityDegS()   => _wristVelocityDegS;
    public override void  SetWristAngleLimits(float minDeg, float maxDeg)
    {
        _wristMinDeg = minDeg;
        _wristMaxDeg = maxDeg;
        _wristAngleDeg = Mathf.Clamp(_wristAngleDeg, _wristMinDeg, _wristMaxDeg);
    }
    public override void ClearWristAngleLimits()
    {
        _wristMinDeg = float.NegativeInfinity;
        _wristMaxDeg = float.PositiveInfinity;
    }

    public override void SetResistiveForce(float newtons)
    {
        // In simulation the resistance is stored for display only.
        // On real hardware this would command the robot's force controller.
        _resistiveForce = newtons;
    }

    public override void Vibrate(int waveform, float durationSeconds)
    {
        _vibrationTimer = durationSeconds;
        Debug.Log($"[MKB] Vibrate waveform={waveform} " +
                  $"(would fire vibrotactile on real robot)");
    }

    public override void EmergencyStop()
    {
        _gripForce      = 0f;
        _resistiveForce = 0f;
        _toggleGrip     = false;
        Debug.LogError("[MKB] EmergencyStop — all outputs zeroed.");
    }

    public override void StartCalibration(float largeScale)
{
    // Set a large maxReach so the mouse can travel freely without clamping
    _maxReach = largeScale;
}

public override float GetRawWorldDistance() => _rawDist;

public override void EndCalibration()
{
    // SetProfile() will restore the correct maxReach once calibration results
    // are written back into the profile by RobotCalibrator
}

    // ═══════════════════════════════════════════════════════════
    // DEBUG OVERLAY
    // ═══════════════════════════════════════════════════════════

    private void OnGUI()
    {
        if (!showOnScreenDebug) return;

        if (_debugStyle == null)
        {
            _debugStyle = new GUIStyle(GUI.skin.box) { fontSize = 13 };
            _debugStyle.normal.textColor = Color.white;
        }

        GUI.Box(new Rect(10, 10, 230, 140),
            "[MKB — Mouse Mirror]\n"         +
            $"Angle:  {_angleDeg:+0.0;-0.0}°\n" +
            $"Radial: {_radial:F2}\n"             +
            $"Grip:   {_gripForce:F1} N  (LMB / Space)\n" +
            $"Resist: {_resistiveForce:F1} N\n"   +
            $"Wrist:  {_wristAngleDeg:+0.0;-0.0}°  (A/D or ←/→)\n" +
            $"Vibe:   {(_vibrationTimer > 0 ? "YES" : "no")}",
            _debugStyle);
    }
}