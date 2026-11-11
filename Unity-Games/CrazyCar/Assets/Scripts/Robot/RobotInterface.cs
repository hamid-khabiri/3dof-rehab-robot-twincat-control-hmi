using UnityEngine;
using RehabFramework.Analysis;

public abstract class RobotInterface : MonoBehaviour
{
    // ── Balloon Journey additions (design §3.1) ───────────────
    private readonly SignalFilters.CausalMovingAverage _velSmoother
        = new SignalFilters.CausalMovingAverage(5);
    private float _lastPosCm; private double _lastPosT; private bool _havePos;

    /// <summary>Radial velocity in cm/s. Real robot overrides with
    /// hardware-derived velocity; the default derives it from polled position.</summary>
    public virtual float GetRadialVelocityCmPerS()
    {
        float pos = GetRadialExtension();
        double now = Time.timeAsDouble;
        float v = 0f;
        if (_havePos && now > _lastPosT)
            v = (float)((pos - _lastPosCm) / (now - _lastPosT));
        _lastPosCm = pos; _lastPosT = now; _havePos = true;
        return _velSmoother.Push(v);
    }

    /// <summary>Interaction force in N. ~0 when transparent; logged for the
    /// transparency verification (spec Table 7). Default: no sensor, so 0.</summary>
    public virtual float GetInteractionForceN() => 0f;

    /// <summary>Command the hard safe-reach limit (cm from home). Real robot
    /// enforces it in hardware; MKB/Joystick clamp their own output.</summary>
    public virtual void SetSafeReachLimitCm(float limitCm) { }

    /// <summary>Lock the lateral (X) axis so the patient reaches straight out.</summary>
    public virtual void SetLateralLock(bool locked) { }

    // ── Clear the View additions (Rotational DOF 3 — wrist rotation) ──
    // Virtual with safe defaults so GM-v1 and BJ-v1 code and their robot
    // interfaces compile untouched, same paste-block pattern as the Balloon
    // Journey additions above. Deliberately separate from the existing
    // abstract GetWristAngle() (which every current interface already stubs
    // to 0f) rather than repurposing it, so nothing that implements that
    // abstract contract needs to change.

    /// <summary>Wrist rotation angle, degrees, raw robot convention
    /// (Rotational DOF 3). Sign/zero-point meaning is calibration-learned by
    /// the consuming assessment, not defined here.</summary>
    public virtual float GetWristAngleDeg() => 0f;

    /// <summary>Wrist angular velocity, deg/s. Default: no sensor, so 0.</summary>
    public virtual float GetWristAngularVelocityDegS() => 0f;

    /// <summary>Command the hard wrist-rotation limit (raw robot degrees, not
    /// clamped/mirrored). Real robot enforces it in hardware; dev interfaces
    /// clamp their own output.</summary>
    public virtual void SetWristAngleLimits(float minDeg, float maxDeg) { }

    /// <summary>Remove any wrist-rotation limit set by SetWristAngleLimits.</summary>
    public virtual void ClearWristAngleLimits() { }

    // ── Rally Paddle addition ──────────────────────────────────
    /// <summary>Signed forward/backward offset, world units, for a Depth-
    /// mode paddle exercise (RallyPaddle) -- unlike GetRadialExtension()/
    /// GetRawWorldDistance(), which are non-negative "distance from home"
    /// by design (a claw reaching out), this can go negative to represent
    /// pulling back past centre. Virtual with a safe 0f default so
    /// existing interfaces compile untouched; override where a genuinely
    /// signed axis exists (see JoystickRobotInterface).</summary>
    public virtual float GetSignedDepthOffset() => 0f;

    // ── Awake state (TwinCAT link) ────────────────────────────
    // The two halves of the "awake" handshake with the robot controller.
    // Virtual with defaults that make every existing dev interface (MKB,
    // Joystick, Replay, MKBAssessmentRobot) behave exactly as before:
    // SetGameAwake is a no-op and IsRobotAwake is always true, so no game
    // gates itself out when there is no real controller behind the
    // interface. TwinCatRobotInterface is the one that maps them onto real
    // PLC variables.

    /// <summary>Tell the robot controller whether a game session is live.
    /// True means the patient is expected to be moving the robot now (the
    /// PLC may enable drives/compensation accordingly); false means paused,
    /// finished, halted, or not started. Every game must drive this -- it is
    /// the controller's only indication that the game is running.</summary>
    public virtual void SetGameAwake(bool awake) { }

    /// <summary>Whether the robot controller reports itself awake and ready
    /// to be moved. Games treat false as "do not run": Gold Miner pauses,
    /// the assessments halt with HaltReason.RobotFault. Default true so
    /// simulated interfaces never gate anything out.</summary>
    public virtual bool IsRobotAwake() => true;

    public abstract float GetClawAngleDeg();
    public abstract float GetRadialExtension();
    public abstract float GetWristAngle();
    public abstract float GetGripForce();
    public abstract float GetVelocityMagnitude();
    public abstract void  SetResistiveForce(float newtons);
    public abstract void  Vibrate(int waveform, float durationSeconds);
    public abstract void  EmergencyStop();

    // ── Calibration support ───────────────────────────────────
    // Override in each concrete interface. Defaults are no-ops so existing
    // interfaces that don't implement calibration continue to compile.

    /// <summary>
    /// Put the interface into calibration mode. largeScale is a generous
    /// upper-bound world distance so raw readings are not clamped to 0-1.
    /// </summary>
    public virtual void StartCalibration(float largeScale) { }

    /// <summary>
    /// Returns the current reach in world units, unclamped.
    /// Used by RobotCalibrator to record the patient's peak distance.
    /// </summary>
    public virtual float GetRawWorldDistance() => GetRadialExtension() * 10f;

    /// <summary>Restore normal clamped operation after calibration.</summary>
    public virtual void EndCalibration() { }
}