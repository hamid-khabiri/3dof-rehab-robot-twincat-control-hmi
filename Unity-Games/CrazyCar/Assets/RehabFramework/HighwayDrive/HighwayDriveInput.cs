using UnityEngine;
using UnityEngine.InputSystem;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE INPUT (HD-v1)
    //  Turns whichever input source is selected into the two axis values the
    //  rest of the instrument consumes -- LateralInput and SpeedInput, both
    //  -1..+1 -- and drives the proportional resistance back out to the robot.
    //  All the actual arithmetic lives in Core/HighwayDriveControlMath.cs so
    //  it is unit-testable; this class only supplies live device readings.
    //
    //  WHY THE MOUSE PATH DOES NOT GO THROUGH MKBRobotInterface: that class
    //  maps the cursor to POLAR coordinates about a workspace anchor and, by
    //  design for a claw reaching out, zeroes its radial output entirely once
    //  the cursor goes above the anchor (see its "Only allow extension
    //  downward" branch). This instrument needs the exact opposite -- the top
    //  of the screen is full throttle -- so routing through it would make the
    //  whole upper half of the screen read as neutral. Screen-relative
    //  Cartesian offsets, which is literally how Simon described the control
    //  ("based on how far you are from middle of screen"), are read here
    //  directly instead. Same isolation as RallyPaddleRobotInput's dev
    //  override: MKBRobotInterface itself is untouched, so BJ/CV/GM dev
    //  testing is unaffected.
    //
    //  DELIBERATELY ADDS NOTHING TO RobotInterface. The planar path below is
    //  built entirely from members that already exist (GetClawAngleDeg,
    //  GetRawWorldDistance, GetSignedDepthOffset, SetResistiveForce,
    //  SetGameAwake, IsRobotAwake). CLAUDE.md's standing warning is that any
    //  new RobotInterface member must also be forwarded in
    //  SelectableRobotInterface, and that a missed override is SILENT -- no
    //  compiler error, just a channel mysteriously reading 0, which has
    //  already bitten this project once (GetSignedDepthOffset). Adding a
    //  signed-lateral accessor would be the natural-looking move here and is
    //  exactly the trap; the polar reconstruction below gets the same signed
    //  value out of members every interface already implements.
    //
    //  LANE STEERING (Crazy Car, HighwayDriveConfig.LaneSteering). The wrist
    //  angle no longer maps to a position: a twist past the threshold moves
    //  TargetLane by one, and the wrist must come back near neutral before the
    //  next change. Y (0-100 %) is a one-way gas pedal. Both come from
    //  TwincatConnectorCrazyCar, or from the keyboard for testing.
    // ─────────────────────────────────────────────────────────────────────────

    public class HighwayDriveInput : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private RobotInterface _robot;
        [SerializeField] private HighwayDriveConfig _config;
        [Tooltip("Crazy Car only: the PLC link read when InputSource is TwinCAT.")]
        [SerializeField] private TwincatConnectorCrazyCar _twincat;

        [Header("Debug")]
        [Tooltip("Logs the input source, raw axis readings, and the resulting resistance command to the " +
                 "Console periodically (throttled, not every frame).")]
        [SerializeField] private bool _showConsoleDebug = false;
        [SerializeField] private float _consoleDebugIntervalS = 0.5f;

        /// <summary>Lateral road position demand, -1 (left edge) to +1 (right
        /// edge). Direct position: holding the input still holds a lane.</summary>
        public float LateralInput { get; private set; }

        /// <summary>Forward speed demand, -1 (full braking) to +1 (full
        /// throttle), with the neutral deadband already applied. Direct speed:
        /// holding the input still holds a speed.</summary>
        public float SpeedInput { get; private set; }

        /// <summary>The RAW normalised axis readings, before dead zone,
        /// before smoothing, before clamping. These -- not LateralInput /
        /// SpeedInput -- are what gets logged as the patient's trajectory:
        /// the filtering that makes the car pleasant to drive must not be
        /// what later analysis sees. See Poll().</summary>
        public float RawLateral { get; private set; }
        public float RawForward { get; private set; }

        /// <summary>The lateral reading BEFORE it was clamped into -1..+1.
        /// Diagnostic only -- nothing in the game reads it. Kept because it is
        /// the one value that distinguishes "parked in the outermost lane"
        /// (reads exactly 1.0) from "pushing well past the calibrated
        /// workspace and getting clamped" (reads above 1.0), which is worth
        /// seeing in the debug log when the planar robot mapping is set up.</summary>
        public float LastUnclampedLateral { get; private set; }

        /// <summary>Whether the robot controller is awake and the car may be
        /// driven. True when there is no robot to gate on, and true whenever
        /// InputSource is MouseScreen: under the mouse path the robot is
        /// bypassed entirely, so a robot that is asleep -- or a TwinCAT
        /// component pointed at a PLC that isn't running -- must not block dev
        /// testing.</summary>
        public bool IsRobotAwake
            => _config == null || _config.InputSource != HighwayDriveInputSource.PlanarRobot
            || _robot == null || _robot.IsRobotAwake();

        /// <summary>Lane steering is active (Crazy Car).</summary>
        public bool UsesLaneSteering => _config != null && _config.LaneSteering;

        /// <summary>Lane the car drives toward, 0 = leftmost. Lane steering only.</summary>
        public int TargetLane { get; private set; }

        /// <summary>Raw wrist angle in degrees, after MirrorLateral. Lane steering only.</summary>
        public float WristAngleDeg { get; private set; }

        /// <summary>Raw gas pedal (Y), 0..100 %. Lane steering only.</summary>
        public float GasPercent { get; private set; }

        /// <summary>Set by the controller every frame: a twist only changes
        /// lane while actually driving, never behind a popup or while
        /// recentering after a crash.</summary>
        public bool LaneChangesEnabled { get; set; } = true;

        private float _consoleDebugTimer;
        private float _lastCommandedResistanceN;
        private bool _laneArmed;
        private bool _loggedKeyboardFallback;

        /// <summary>Call once per frame, in every state. Updates LateralInput,
        /// SpeedInput, and LastUnclampedLateral from the live device.</summary>
        public void Poll()
        {
            if (UsesLaneSteering)
            {
                PollLaneSteering();
                LogDebug();
                return;
            }

            float rawLateral;
            float rawForward;
            if (SourceIsMouse()) ReadMouseAxes(out rawLateral, out rawForward);
            else ReadRobotAxes(out rawLateral, out rawForward);

            if (_config != null && _config.MirrorLateral) rawLateral = -rawLateral;

            // RAW FIRST, AND UNTOUCHED. The dead zone and the smoothing below
            // exist to make the car pleasant to control; they must never be
            // what gets recorded as the patient's trajectory. RawLateral /
            // RawForward are the values HighwayDriveDataLogger writes, so the
            // analysis sees the actual end-effector motion including the
            // tremor the visual filtering deliberately hides.
            RawLateral = rawLateral;
            RawForward = rawForward;
            LastUnclampedLateral = rawLateral;

            float lateralDeadzone = _config != null ? _config.LateralDeadzone : 0.06f;
            float speedDeadzone = _config != null ? _config.SpeedDeadzone : 0.08f;

            float lateralTarget = HighwayDriveControlMath.SmoothDeadzone(
                HighwayDriveControlMath.Clamp(rawLateral, -1f, 1f), lateralDeadzone);

            // A short first-order smoothing AFTER the dead zone, so residual
            // tremor just outside the band reads as a steady hand rather than
            // as a twitching car. Kept small on purpose -- any more and it
            // stops being smoothing and becomes steering lag, which would
            // corrupt the very thing being assessed.
            float smoothing = _config != null ? Mathf.Max(0f, _config.LateralSmoothingTimeS) : 0.08f;
            float dt = Time.deltaTime;
            LateralInput = (smoothing <= 0f || dt <= 0f)
                ? lateralTarget
                : Mathf.Lerp(LateralInput, lateralTarget, 1f - Mathf.Exp(-dt / smoothing));

            SpeedInput = HighwayDriveControlMath.SmoothDeadzone(
                HighwayDriveControlMath.Clamp(rawForward, -1f, 1f), speedDeadzone);

            LogDebug();
        }

        /// <summary>Neutralises both axes without touching the device -- used
        /// when a run is not live, so a cursor left parked at the top of the
        /// screen during a pause does not read as full throttle the instant
        /// the run resumes.</summary>
        public void Neutralize()
        {
            LateralInput = 0f;
            SpeedInput = 0f;
            LastUnclampedLateral = 0f;
        }

        /// <summary>Back to the middle lane (the left-middle one for an even
        /// lane count). Disarmed until the wrist next returns to neutral, so a
        /// wrist already twisted when a game starts does not change lane.</summary>
        public void ResetLane()
        {
            int laneCount = _config != null ? Mathf.Max(1, _config.LaneCount) : 3;
            TargetLane = (laneCount - 1) / 2;
            _laneArmed = false;
        }

        /// <summary>The recentering gate after a collision. Lane steering:
        /// wrist back inside LaneRearmDeg and gas released. Otherwise: both
        /// planar axes inside their tolerances.</summary>
        public bool IsAtNeutral(float lateralTolerance, float forwardTolerance)
        {
            if (!UsesLaneSteering)
                return HighwayDriveControlMath.IsWithinNeutralTolerance(
                    LateralInput, SpeedInput, lateralTolerance, forwardTolerance);

            return Mathf.Abs(WristAngleDeg) <= _config.LaneRearmDeg
                && GasPercent <= _config.GasNeutralPercent;
        }

        private void PollLaneSteering()
        {
            ReadLaneAxes(out float wristDeg, out float gasPercent);
            if (_config.InputSource == HighwayDriveInputSource.TwinCAT && _twincat != null && _twincat.IsConnected)
                wristDeg -= _config.WristZeroOffsetDeg;
            if (_config.MirrorLateral) wristDeg = -wristDeg;

            WristAngleDeg = wristDeg;
            GasPercent = Mathf.Clamp(gasPercent, 0f, 100f);

            bool positionMode = _config.LaneMode == LaneSteeringMode.WristPosition;
            float threshold = positionMode
                ? Mathf.Max(1f, _config.WristFullScaleDeg)
                : Mathf.Max(1f, _config.LaneChangeThresholdDeg);
            int laneCount = Mathf.Max(1, _config.LaneCount);
            TargetLane = Mathf.Clamp(TargetLane, 0, laneCount - 1);

            if (positionMode)
            {
                // The wrist angle IS the lane: -FullScale..+FullScale spans the
                // leftmost..rightmost lane centre. The car only ever targets a
                // whole lane, and only leaves the current one once the wrist is
                // past the boundary by LaneHysteresis, so it never parks on a line.
                if (LaneChangesEnabled && laneCount > 1)
                {
                    float n = Mathf.Clamp(wristDeg / threshold, -1f, 1f);
                    float laneCoord = (n + 1f) * 0.5f * (laneCount - 1);
                    if (Mathf.Abs(laneCoord - TargetLane) > 0.5f + _config.LaneHysteresis)
                        TargetLane = Mathf.Clamp(Mathf.RoundToInt(laneCoord), 0, laneCount - 1);
                }
            }
            // One twist = one lane. Disarm on every change and re-arm only
            // once the wrist is back near neutral, however long it is held.
            else if (!_laneArmed)
            {
                if (Mathf.Abs(wristDeg) <= _config.LaneRearmDeg) _laneArmed = true;
            }
            else if (LaneChangesEnabled && wristDeg >= threshold)
            {
                TargetLane = Mathf.Min(TargetLane + 1, laneCount - 1);
                _laneArmed = false;
            }
            else if (LaneChangesEnabled && wristDeg <= -threshold)
            {
                TargetLane = Mathf.Max(TargetLane - 1, 0);
                _laneArmed = false;
            }

            // Logged as fractions so the CSV columns keep their -1..+1 / 0..1
            // shape: lateral = wrist / threshold, forward = gas / 100. The
            // exact degrees and percent are logged in their own columns.
            RawLateral = wristDeg / threshold;
            RawForward = GasPercent / 100f;
            LastUnclampedLateral = RawLateral;
            LateralInput = HighwayDriveControlMath.Clamp(RawLateral, -1f, 1f);

            float speedDeadzone = _config.SpeedDeadzone;
            SpeedInput = HighwayDriveControlMath.SmoothDeadzone(RawForward, speedDeadzone);
        }

        private void ReadLaneAxes(out float wristDeg, out float gasPercent)
        {
            switch (_config.InputSource)
            {
                case HighwayDriveInputSource.TwinCAT:
                    if (_twincat != null && _twincat.IsConnected)
                    {
                        wristDeg = _twincat.CurrentWristDeg;
                        gasPercent = _twincat.CurrentYPercent;
                        return;
                    }
                    if (!_loggedKeyboardFallback)
                    {
                        Debug.LogWarning("[HighwayDriveInput] TwinCAT is not connected -- using the keyboard " +
                                         "(Left/Right = wrist, Up = gas).", this);
                        _loggedKeyboardFallback = true;
                    }
                    ReadKeyboardLaneAxes(out wristDeg, out gasPercent);
                    return;

                case HighwayDriveInputSource.Keyboard:
                    ReadKeyboardLaneAxes(out wristDeg, out gasPercent);
                    return;

                case HighwayDriveInputSource.MouseScreen:
                {
                    // Half the screen width = twice the lane threshold, bottom = no gas, top = full gas.
                    Mouse mouse = Mouse.current;
                    if (mouse == null || Screen.width <= 0 || Screen.height <= 0)
                    {
                        wristDeg = 0f;
                        gasPercent = 0f;
                        return;
                    }
                    Vector2 cursor = mouse.position.ReadValue();
                    float halfW = Screen.width * 0.5f;
                    wristDeg = HighwayDriveControlMath.NormalizedOffsetFromCenter(cursor.x, halfW, halfW)
                             * 2f * _config.LaneChangeThresholdDeg;
                    gasPercent = Mathf.Clamp01(cursor.y / Screen.height) * 100f;
                    return;
                }

                default:
                {
                    if (_robot == null)
                    {
                        wristDeg = 0f;
                        gasPercent = 0f;
                        return;
                    }
                    float forwardRange = _config.RobotForwardHalfRangeWorld;
                    wristDeg = _robot.GetWristAngleDeg();
                    gasPercent = forwardRange > 0f
                        ? Mathf.Clamp01(_robot.GetSignedDepthOffset() / forwardRange) * 100f
                        : 0f;
                    return;
                }
            }
        }

        private void ReadKeyboardLaneAxes(out float wristDeg, out float gasPercent)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                wristDeg = 0f;
                gasPercent = 0f;
                return;
            }

            float twist = 0f;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) twist += 1f;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) twist -= 1f;
            // In WristPosition mode a held key must reach the outermost lane.
            float keyDeg = _config.LaneMode == LaneSteeringMode.WristPosition
                ? Mathf.Max(_config.KeyboardWristDeg, _config.WristFullScaleDeg)
                : _config.KeyboardWristDeg;
            wristDeg = twist * keyDeg;
            gasPercent = kb.upArrowKey.isPressed || kb.wKey.isPressed ? 100f : 0f;
        }

        /// <summary>Forwards the session-live flag to the robot controller.
        /// Forwarded even on the mouse path: telling the controller a session
        /// is running is correct regardless of which input drives the car.</summary>
        public void SetGameAwake(bool awake) => _robot?.SetGameAwake(awake);

        /// <summary>Commands resistance stepped by the current speed demand's
        /// distance from neutral (Simon's request, 2026-08-17). Symmetric --
        /// hard acceleration and hard braking both load the arm, and neutral is
        /// free. The class table and both clamps live in
        /// HighwayDriveControlMath; this only supplies the live inputs.
        ///
        /// This is the same wire path Gold Miner's item weights travel down:
        /// RobotInterface.SetResistiveForce, which TwinCatRobotInterface clamps
        /// again to its own Max Resistive Force N and to non-negative before
        /// writing rResistiveForceN. That second clamp is deliberate
        /// redundancy, not belt-and-braces to lean on -- and the PLC is
        /// expected to enforce its own hard limit beyond both, since neither of
        /// these is a safety system.
        ///
        /// Call this ONLY while a run is actually live. Every other state must
        /// call ClearResistance() instead: leaving a force commanded into a
        /// crashed, paused, or finished session means the robot keeps pushing
        /// on an arm whose owner has been told to stop and wait.</summary>
        public void ApplyResistance()
        {
            if (_robot == null) return;
            if (_config != null && !_config.EnableResistance)
            {
                ClearResistance();
                return;
            }

            float gain = _config != null ? _config.ResistanceGain : 1f;
            float safeMax = _config != null ? _config.SafeMaxForceNewtons
                                            : HighwayDriveControlMath.MaxCommandableNewtons;
            _lastCommandedResistanceN = HighwayDriveControlMath.ResistanceNewtons(SpeedInput, gain, safeMax);
            _robot.SetResistiveForce(_lastCommandedResistanceN);
        }

        /// <summary>Drops commanded resistance to zero. Safe to call every
        /// frame and safe to call redundantly.</summary>
        public void ClearResistance()
        {
            if (_robot == null) return;
            _lastCommandedResistanceN = 0f;
            _robot.SetResistiveForce(0f);
        }

        private void OnDisable()
        {
            // Scene unload, component disable, or quitting: as far as the robot
            // controller is concerned the session is over, and nothing should
            // be left pushing against the patient.
            ClearResistance();
            _robot?.SetGameAwake(false);
        }

        private bool SourceIsMouse()
            => _config == null || _config.InputSource == HighwayDriveInputSource.MouseScreen;

        /// <summary>Cursor offset from the middle of the screen, each axis
        /// normalised against that half of the screen. Exactly the control
        /// Simon described. Falls back to dead neutral rather than to the last
        /// reading when no mouse is present -- a stuck throttle is a worse
        /// failure than no input.</summary>
        private void ReadMouseAxes(out float rawLateral, out float rawForward)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Screen.width <= 0 || Screen.height <= 0)
            {
                rawLateral = 0f;
                rawForward = 0f;
                return;
            }

            Vector2 cursor = mouse.position.ReadValue();
            float halfW = Screen.width * 0.5f;
            float halfH = Screen.height * 0.5f;

            rawLateral = HighwayDriveControlMath.NormalizedOffsetFromCenter(cursor.x, halfW, halfW);
            rawForward = HighwayDriveControlMath.NormalizedOffsetFromCenter(cursor.y, halfH, halfH);
        }

        /// <summary>Planar-robot mapping, for when the real hardware arrives.
        ///
        /// LATERAL comes from the polar reconstruction, not from a dedicated
        /// accessor: MKBRobotInterface defines its claw angle as
        /// Atan2(dx, -dy) -- zero means "straight down" from the anchor -- so
        /// the inverse for the X component is dx = radial * sin(angle), the
        /// same inverse RallyPaddleRobotInput uses for its Planar mode. The
        /// angle's own sign carries left/right, so this is genuinely signed
        /// even though the radial term is not. GetRawWorldDistance() rather
        /// than GetRadialExtension() for the radial term, because CLAUDE.md is
        /// explicit that GetRadialExtension() is NOT unit-bearing (0-1 in
        /// MKBRobotInterface, centimetres in MKBAssessmentRobot/
        /// ReplayRobotInterface) while GetRawWorldDistance() is documented as
        /// world units across every implementation.
        ///
        /// FORWARD comes from GetSignedDepthOffset(), which exists precisely
        /// because GetRadialExtension()/GetRawWorldDistance() are non-negative
        /// "distance from home" by design and can never represent pulling back
        /// past centre -- and this axis must be bidirectional, since braking
        /// is half of it. Only JoystickRobotInterface implements it
        /// non-trivially so far, so on MKB this axis reads a flat zero; that
        /// is why MouseScreen, not this, is the dev default.</summary>
        private void ReadRobotAxes(out float rawLateral, out float rawForward)
        {
            if (_robot == null)
            {
                rawLateral = 0f;
                rawForward = 0f;
                return;
            }

            float angleRad = _robot.GetClawAngleDeg() * Mathf.Deg2Rad;
            float radial = _robot.GetRawWorldDistance();
            float lateralWorld = Mathf.Sin(angleRad) * radial;
            float forwardWorld = _robot.GetSignedDepthOffset();

            float lateralRange = _config != null ? _config.RobotLateralHalfRangeWorld : 12f;
            float forwardRange = _config != null ? _config.RobotForwardHalfRangeWorld : 12f;

            rawLateral = lateralRange > 0f ? lateralWorld / lateralRange : 0f;
            rawForward = forwardRange > 0f ? forwardWorld / forwardRange : 0f;
        }

        private void LogDebug()
        {
            if (!_showConsoleDebug) return;

            _consoleDebugTimer += Time.deltaTime;
            if (_consoleDebugTimer < Mathf.Max(0.05f, _consoleDebugIntervalS)) return;
            _consoleDebugTimer = 0f;

            if (UsesLaneSteering)
            {
                Debug.Log($"[HighwayDriveInput] source={_config.InputSource}  " +
                          $"twincat={(_twincat != null && _twincat.IsConnected ? "connected" : "off")}  " +
                          $"wrist={WristAngleDeg:F1} deg  gas={GasPercent:F0} %  lane={TargetLane}  " +
                          $"armed={_laneArmed}");
                return;
            }

            string source = SourceIsMouse() ? "MouseScreen" : "PlanarRobot";
            string robotType = _robot != null ? _robot.GetType().Name : "NULL";
            Debug.Log($"[HighwayDriveInput] source={source}  robot={robotType}  " +
                      $"lateral={LateralInput:F2} (raw {LastUnclampedLateral:F2})  speed={SpeedInput:F2}  " +
                      $"resistance={_lastCommandedResistanceN:F1} N " +
                      $"(class {HighwayDriveControlMath.ResistanceClassIndex(SpeedInput)} of " +
                      $"{HighwayDriveControlMath.ResistanceClassCount - 1})");
        }
    }
}
