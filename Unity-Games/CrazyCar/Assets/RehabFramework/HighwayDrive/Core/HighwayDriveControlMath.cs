namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE CONTROL MATH
    //  Every pure decision in the control path -- how a cursor/robot offset
    //  becomes a lateral road position and a forward speed, how much resistive
    //  force that speed demand is worth, where a lane sits, and whether two
    //  cars are touching -- factored out of the MonoBehaviours so it can be
    //  unit-tested without Play Mode, a GameObject, or a robot.
    //
    //  Plain floats and no UnityEngine types on purpose: this file lives in
    //  Core/ under HighwayDriveCore.asmdef (noEngineReferences: true), the
    //  same arrangement RallyPaddle uses for RallyPaddleWeightedChoice, so the
    //  EditMode test assembly can reference it by name. Float rather than
    //  double matches that precedent -- CLAUDE.md's "all clinical math in
    //  double" rule governs the RehabAnalysis assembly, and none of this is
    //  clinical math; it is gameplay control mapping.
    //
    //  THE CONTROL LAW (Simon's choice, 2026-08-17): direct position on the
    //  lateral axis, direct speed on the forward axis. Neither axis is an
    //  integrator -- holding the input still holds a lane and holds a speed.
    //  That is the whole point: a patient who cannot sustain fine correction
    //  can still park the car in a lane at a steady speed, whereas a rate- or
    //  acceleration-based law would require constant active re-centring and
    //  would read a ROM deficit as a control error.
    // ─────────────────────────────────────────────────────────────────────────

    public static class HighwayDriveControlMath
    {
        /// <summary>Signed position within a symmetric span, as -1..+1.
        /// Used to turn a raw cursor coordinate (screen pixels) or a robot
        /// endpoint offset (world units) into an axis input, without either
        /// caller needing to know the other's units.</summary>
        public static float NormalizedOffsetFromCenter(float value, float center, float halfExtent)
        {
            if (halfExtent <= 0f) return 0f;
            return Clamp((value - center) / halfExtent, -1f, 1f);
        }

        /// <summary>Zeroes a small band around neutral, then rescales what is
        /// left so the full -1..+1 range is still reachable at the edges.
        ///
        /// Only worth applying to the SPEED axis in practice: "hold the
        /// current cruising speed" is a real task demand here, and without a
        /// deadband a patient with tremor or poor proximal stability can never
        /// actually sit at neutral. The lateral axis deliberately has no
        /// deadband -- a dead patch in the middle of the road would put an
        /// invisible notch in the centre lane.</summary>
        public static float ApplyDeadzone(float normalizedInput, float deadzone)
        {
            float dz = Clamp(deadzone, 0f, 0.95f);
            float x = Clamp(normalizedInput, -1f, 1f);
            float magnitude = x < 0f ? -x : x;
            if (magnitude <= dz) return 0f;

            float sign = x < 0f ? -1f : 1f;
            return sign * ((magnitude - dz) / (1f - dz));
        }

        /// <summary>Lateral world position of the player's car for a given
        /// lateral input. Inset by the car's own half-width so full deflection
        /// puts the car's EDGE on the road edge rather than its centre --
        /// otherwise half the car hangs off the road at full reach, which
        /// reads as a bug rather than as a boundary.</summary>
        public static float LateralWorldX(float lateralInput, float roadHalfWidth, float carHalfWidth)
        {
            float usableHalfWidth = roadHalfWidth - carHalfWidth;
            if (usableHalfWidth < 0f) usableHalfWidth = 0f;
            return Clamp(lateralInput, -1f, 1f) * usableHalfWidth;
        }

        /// <summary>The calibrated lateral mapping actually used in play:
        /// full comfortable left lands near the left road boundary, neutral at
        /// road centre, full comfortable right near the right boundary --
        /// with a safety margin so the car can never visually leave the road.
        ///
        /// CONTINUOUS, never snapped to a lane. The lanes exist to structure
        /// the scenery and the traffic; discretising the patient's movement
        /// would destroy the very quantity this instrument measures, which is
        /// the smooth planar excursion of the end effector.
        ///
        /// <paramref name="workspaceRange"/> is the fraction of the patient's
        /// calibrated comfortable range that spans the full road: 1.0 means
        /// full comfortable reach exactly touches the boundary, and LOWERING
        /// it demands a LARGER excursion for the same road -- which is the
        /// knob that makes four and five lanes progressively harder without
        /// shrinking the car or the lanes.
        ///
        /// The edge is a soft limit, not a failure: touching the painted edge
        /// marking must never itself end a run, so the result is simply
        /// clamped into the usable region.</summary>
        public static float MapLateralToRoad(float lateralInput, float roadHalfWidth, float carHalfWidth,
            float safetyMarginWorld, float workspaceRange, float steeringGain)
        {
            float usableHalfWidth = roadHalfWidth - carHalfWidth - safetyMarginWorld;
            if (usableHalfWidth < 0f) usableHalfWidth = 0f;

            float range = workspaceRange <= 0.0001f ? 1f : workspaceRange;
            float commanded = Clamp(lateralInput, -1f, 1f) * steeringGain / range;
            return Clamp(commanded, -1f, 1f) * usableHalfWidth;
        }

        /// <summary>Dead zone with a SMOOTH shoulder rather than a hard step.
        /// <see cref="ApplyDeadzone"/> rescales linearly, which removes the
        /// jump at the band edge but still starts moving at a constant rate
        /// the instant the band is crossed. This applies a smoothstep on top,
        /// so the command eases away from neutral: the difference a patient
        /// feels is that leaving neutral is gradual instead of the car
        /// "unsticking" and setting off.</summary>
        public static float SmoothDeadzone(float normalizedInput, float deadzone)
        {
            float rescaled = ApplyDeadzone(normalizedInput, deadzone);
            float magnitude = rescaled < 0f ? -rescaled : rescaled;
            float eased = magnitude * magnitude * (3f - 2f * magnitude); // smoothstep
            return rescaled < 0f ? -eased : eased;
        }

        /// <summary>Whether the end effector is inside the neutral tolerance on
        /// BOTH axes -- the recentering gate a patient must satisfy before a
        /// run resumes after a collision. Recovery depends on actually
        /// returning to neutral rather than on waiting out a timer, so this is
        /// the predicate the whole recovery state hangs on.</summary>
        public static bool IsWithinNeutralTolerance(float lateralInput, float forwardInput,
            float lateralTolerance, float forwardTolerance)
        {
            float lat = lateralInput < 0f ? -lateralInput : lateralInput;
            float fwd = forwardInput < 0f ? -forwardInput : forwardInput;
            return lat <= lateralTolerance && fwd <= forwardTolerance;
        }

        // ── Three-tier longitudinal motion ────────────────────────────────
        //
        //  Everything used to scroll at one velocity derived from the player's
        //  speed command, which is why the peripheral scenery became visual
        //  noise: at full throttle the grass and rocks flew past too fast to
        //  resolve, so the environment art could not be seen at all. Simon's
        //  instruction (2026-08-25) is to split longitudinal motion into three
        //  systems rather than one:
        //
        //    1. Traffic + lane markings -- FULL speed. These carry the
        //       acceleration cue, and they are the ones that must stay
        //       perfectly coupled to the command, because judging closing speed
        //       against other cars IS the task.
        //    2. Nearby roadside vegetation and rocks -- roughly 30-50%.
        //    3. Large landmarks (houses, ponds, turbines) -- roughly 15-30%,
        //       so they enter, linger for several seconds, and drift down.
        //
        //  This is a deliberate departure from physical correctness. In a true
        //  bird's-eye view every static object shares one ground velocity, so
        //  strictly the grass should move exactly as fast as the lane markings.
        //  Decoupling them trades that consistency for something worth more
        //  here: the participant can actually perceive the environment while
        //  still reading acceleration off the traffic and the lane markers.

        /// <summary>Scenery speed for one parallax tier: a fraction of the
        /// player's true speed, then CAPPED. The cap is what stops hard
        /// acceleration from dragging the scenery back into a blur -- past it,
        /// speeding up changes the traffic and the lane markings but leaves the
        /// background legible. factor is clamped to 0..1 because a tier moving
        /// faster than the road would read as the world sliding apart.</summary>
        public static float ParallaxSpeed(float playerSpeed, float factor, float maxSpeed)
        {
            if (playerSpeed <= 0f) return 0f;
            float scaled = playerSpeed * Clamp(factor, 0f, 1f);
            return maxSpeed > 0f && scaled > maxSpeed ? maxSpeed : scaled;
        }

        /// <summary>Closing speed for a heart pickup, chosen so it takes
        /// approachSeconds to travel the visible distance from the top of the
        /// view down to the player.
        ///
        /// Deliberately INDEPENDENT of how fast the player is driving. A pickup
        /// that closed at the vehicle-relative rate would sweep past in well
        /// under a second at speed, which is not something a rehabilitation
        /// participant can plan a reach around -- and the whole point of the
        /// collectible is that it is anticipatable. Fixing the approach time
        /// makes the window the same every time, whatever the participant is
        /// doing with the throttle.</summary>
        public static float HeartClosingSpeed(float visibleApproachDistance, float approachSeconds)
        {
            if (approachSeconds <= 0.01f) return visibleApproachDistance;
            return visibleApproachDistance / approachSeconds;
        }

        /// <summary>Normalised 0..1 strength of the high-speed streak effect.
        /// Zero at and below the onset point so streaks are simply absent
        /// during normal cruising, then ramping smoothly to full at maximum
        /// speed -- they must appear only at the clear upper end and fade out
        /// smoothly rather than switching off.</summary>
        public static float StreakStrength(float normalizedSpeed, float onset)
        {
            if (normalizedSpeed <= onset) return 0f;
            float span = 1f - onset;
            if (span <= 0.0001f) return 1f;
            return Clamp((normalizedSpeed - onset) / span, 0f, 1f);
        }

        /// <summary>The player car's TRUE forward speed in world units/second
        /// -- not a speed relative to traffic. This is the value the road
        /// surface, lane dashes, and roadside props all scroll at, which is
        /// what sells the scene as genuine forward motion rather than as
        /// everyone else driving backwards; traffic is then drawn at the
        /// DIFFERENCE between its own true speed and this one.
        ///
        /// Neutral input gives cruiseSpeed, which is set slightly above the
        /// traffic band so a patient doing nothing still overtakes gently and
        /// the game keeps presenting obstacles. Floored at minSpeed (strictly
        /// positive) so full deceleration slows you enough for traffic to
        /// stream past, but never stops the car dead -- a stationary car makes
        /// the whole forward-motion illusion collapse and leaves the patient
        /// with nothing to do.</summary>
        public static float PlayerSpeedWorldPerS(float speedInput, float cruiseSpeed, float speedSwing, float minSpeed)
        {
            float speed = cruiseSpeed + Clamp(speedInput, -1f, 1f) * speedSwing;
            return speed < minSpeed ? minSpeed : speed;
        }

        // ── Resistance classes ───────────────────────────────────────────
        //
        //  RESISTANCE CLASSES ARE NEWTONS, ON A 0-10 N SCALE, where 10 N is
        //  the robot's maximum commandable force. Same scheme, same ceiling,
        //  and the same reasoning as Gold Miner's item-weight table in
        //  MineItemSpawner.Weights (diamond 0.4 N ... large gold 10.0 N), and
        //  it is the same wire channel at the far end:
        //  RobotSignalContract's RobotOutputs.ResistiveForceN is documented as
        //  "0 to 10 ... 10 N is the assumed hardware maximum".
        //
        //  The top class sits at EXACTLY that ceiling, exactly as Gold Miner's
        //  heaviest items do, so this table IS the ceiling rather than
        //  something a multiplier can push past. Three ceilings in this
        //  project all read 10 N and are meant to move together if the
        //  hardware limit ever changes: UserWorkspaceProfile.FSafeNewtons,
        //  GameBootstrap.fSafeN, and TwinCatRobotInterface's
        //  _maxResistiveForceN. This table is now a fourth.
        //
        //  DISCRETE, not a continuous ramp: a patient cannot feel the
        //  difference between 6.1 N and 6.3 N, but they can learn four
        //  distinct steps, and a therapist can say which step someone worked
        //  at. It also makes the commanded value legible in a TwinCAT watch
        //  window -- a number that lands on one of five known values is
        //  something you can eyeball as correct, unlike a continuously
        //  drifting one.

        /// <summary>The robot's maximum commandable force. Not a preference --
        /// commanding above this asks for force the hardware cannot give.</summary>
        public const float MaxCommandableNewtons = 10f;

        private static readonly float[] ClassNewtons = { 0f, 2.5f, 5f, 7.5f, 10f };

        /// <summary>How many resistance classes exist, counting the free
        /// neutral class at index 0.</summary>
        public static int ResistanceClassCount => ClassNewtons.Length;

        /// <summary>Newtons for a given class index, before gain and before
        /// the safe-ceiling clamp. Index 0 is always 0 N.</summary>
        public static float ResistanceClassNewtons(int classIndex)
        {
            if (classIndex < 0) classIndex = 0;
            if (classIndex > ClassNewtons.Length - 1) classIndex = ClassNewtons.Length - 1;
            return ClassNewtons[classIndex];
        }

        /// <summary>Which resistance class a speed demand falls in.
        ///
        /// Class 0 is reserved for EXACTLY neutral, so the free class and the
        /// lightest loaded class cannot be confused: any demand at all, however
        /// small, is already class 1. The deadband
        /// (<see cref="ApplyDeadzone"/>) is what decides how much hand tremor
        /// still counts as neutral -- that judgement belongs there, not here.
        /// The remaining classes divide the rest of the range evenly.</summary>
        public static int ResistanceClassIndex(float speedInput)
        {
            float magnitude = speedInput < 0f ? -speedInput : speedInput;
            if (magnitude <= 0f) return 0;
            if (magnitude > 1f) magnitude = 1f;

            int loadedClasses = ClassNewtons.Length - 1;
            int index = 1;
            while (index < loadedClasses && magnitude > (float)index / loadedClasses) index++;
            return index;
        }

        /// <summary>Resistive force to command back to the robot, in newtons,
        /// stepped by how far the speed demand sits from neutral -- SYMMETRIC,
        /// so pushing hard forward and pulling hard back both load the arm
        /// (Simon's choice, 2026-08-17).
        ///
        /// The felt result is a spring pulling the patient back toward
        /// cruising speed: sitting at the traffic-matched neutral is free, and
        /// any sustained departure from it in either direction is a real
        /// strength demand that steps up with the departure. Reads zero at
        /// neutral by construction, which matters -- a non-zero floor would
        /// mean the robot is always pushing on a resting arm.
        ///
        /// <paramref name="gain"/> is CLAMPED TO 1.0, matching Gold Miner's
        /// ResistanceGain parameter and for the same reason: the class table's
        /// top entry is already exactly the robot's maximum, so a gain above 1
        /// could only ask for force the hardware cannot give, and would leave
        /// the safe ceiling as the sole thing standing between a therapist's
        /// slider and the hardware limit. Gain reduces load; it never adds.
        ///
        /// <paramref name="safeMaxNewtons"/> is the patient's own ceiling --
        /// UserWorkspaceProfile.FSafeNewtons in a fully wired scene. Lowering
        /// it is the intended way to load someone less than the full range.
        /// It can only ever lower the result, never raise it above
        /// <see cref="MaxCommandableNewtons"/>.</summary>
        public static float ResistanceNewtons(float speedInput, float gain, float safeMaxNewtons)
        {
            float force = ResistanceClassNewtons(ResistanceClassIndex(speedInput))
                        * Clamp(gain, 0f, 1f);

            float ceiling = safeMaxNewtons < MaxCommandableNewtons ? safeMaxNewtons : MaxCommandableNewtons;
            if (ceiling < 0f) ceiling = 0f;

            if (force < 0f) force = 0f;
            if (force > ceiling) force = ceiling;
            return force;
        }

        /// <summary>Width of one lane, for a road of the given half-width
        /// divided into laneCount equal lanes.</summary>
        public static float LaneWidth(float roadHalfWidth, int laneCount)
        {
            if (laneCount < 1) laneCount = 1;
            return (roadHalfWidth * 2f) / laneCount;
        }

        /// <summary>World X of the centre of lane <paramref name="laneIndex"/>
        /// (0 = leftmost). Shared by the traffic spawner and the lane-dash
        /// builder so the painted lines and the cars that drive in them can
        /// never drift apart.</summary>
        public static float LaneCenterX(int laneIndex, int laneCount, float roadHalfWidth)
        {
            if (laneCount < 1) laneCount = 1;
            if (laneIndex < 0) laneIndex = 0;
            if (laneIndex > laneCount - 1) laneIndex = laneCount - 1;

            float laneWidth = LaneWidth(roadHalfWidth, laneCount);
            return -roadHalfWidth + laneWidth * (laneIndex + 0.5f);
        }

        /// <summary>Axis-aligned box overlap. Every car in this instrument is
        /// an upright rectangle that never rotates (bird's-eye view, all
        /// traffic travelling the same direction), so an exact AABB test is
        /// both correct and cheaper than any collider -- there is no physics
        /// engine in this scaffold, same as Rally Paddle.</summary>
        public static bool RectsOverlap(
            float aCenterX, float aCenterY, float aWidth, float aHeight,
            float bCenterX, float bCenterY, float bWidth, float bHeight)
        {
            float dx = aCenterX - bCenterX;
            if (dx < 0f) dx = -dx;
            float dy = aCenterY - bCenterY;
            if (dy < 0f) dy = -dy;

            return dx < (aWidth + bWidth) * 0.5f
                && dy < (aHeight + bHeight) * 0.5f;
        }

        /// <summary>Screen-space forward offset the player's car is drawn at,
        /// as a small readability cue only: leaning on the throttle nudges the
        /// car up the screen, easing off settles it back. Set shiftWorld to 0
        /// for a strictly anchored car (the literal relative-motion model).
        ///
        /// This is a REAL world-position change, not a decoration -- it moves
        /// the car's collision box too, so pushing to top speed genuinely
        /// shortens the reaction distance to traffic ahead. That is a fair and
        /// self-imposed difficulty cost, which is why it is kept small.</summary>
        public static float PlayerForwardWorldY(float speedInput, float anchorY, float shiftWorld)
        {
            return anchorY + Clamp(speedInput, -1f, 1f) * shiftWorld;
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // ── Environment theme rotation ────────────────────────────────────

        /// <summary>Which of themeCount environment themes (Desert/City/Snow --
        /// see HighwayDriveEnvironmentTheme and HighwayDriveRoad) is active for
        /// a given calendar day, so the palette changes about once a day with
        /// no persisted state: every session started on the same calendar day
        /// computes the same index, and the next day's index is just
        /// dayOfYear+1 wrapped. Takes a plain int rather than reading the
        /// clock itself, so this stays engine-free and testable -- the caller
        /// supplies System.DateTime.Now.DayOfYear.
        ///
        /// Deliberately not a hash or shuffle: a simple day-of-year modulo
        /// means the sequence is Desert, City, Snow, Desert, City, Snow, ...
        /// in calendar order, which is predictable enough that a therapist who
        /// noticed yesterday's theme can tell you today's without checking.</summary>
        public static int ThemeIndexForDay(int dayOfYear, int themeCount)
        {
            if (themeCount <= 0) return 0;
            int day = dayOfYear < 0 ? -dayOfYear : dayOfYear;
            return day % themeCount;
        }

        // ── Hearts (DDA-tuned pickup frequency) ────────────────────────────
        //
        //  Hearts (Simon's request, 2026-08-19) are a SESSION-scoped resource
        //  -- HighwayDriveController owns the count, HighwayDriveHearts owns
        //  spawning the on-road pickups that refill it. The two decisions
        //  that make it "DDA tuning" rather than a fixed timer both live here,
        //  as plain int/float math so they can be pinned by a test independent
        //  of any GameObject or spawn-pool bookkeeping.

        /// <summary>Whether a heart pickup may spawn at all, given the current
        /// count against the cap. False at a full heart count -- a pickup
        /// nobody can meaningfully collect (HighwayDriveController clamps the
        /// gain at maxHearts) would be pure clutter with no clinical purpose,
        /// and Simon's instruction was explicit: "they dont spawn when you
        /// have 5 hearts".</summary>
        public static bool HeartsMaySpawn(int currentHearts, int maxHearts) => currentHearts < maxHearts;

        /// <summary>Seconds until the next heart-pickup spawn attempt --
        /// SHORTER (more frequent) the fewer hearts the patient currently has,
        /// so a patient who has been crashing gets more chances to recover
        /// rather than fewer. Linear interpolation between baseIntervalS (at a
        /// heart count one below the cap -- the least urgent case this
        /// function is ever asked about, since HeartsMaySpawn already excludes
        /// the at-cap case) and minIntervalS (at zero hearts, the most urgent
        /// case). Deliberately not itself gated on the cap -- callers check
        /// HeartsMaySpawn separately, so this stays a pure "how urgent" curve
        /// with no branch duplicating that decision.</summary>
        public static float HeartSpawnIntervalS(int currentHearts, int maxHearts, float baseIntervalS, float minIntervalS)
        {
            if (maxHearts <= 0) return baseIntervalS; // malformed config; never actually reached in practice

            // The highest currentHearts value this function is ever actually
            // asked about, per its own doc comment -- HeartsMaySpawn excludes
            // currentHearts == maxHearts entirely, so anchoring urgency's 0
            // point AT maxHearts (rather than one below it) would mean the
            // "least urgent" case in practice never actually reaches
            // baseIntervalS. maxHearts == 1 has no such case at all (its only
            // non-full state, 0 hearts, IS the most urgent one), so that is
            // the one input where minIntervalS is simply always correct.
            int highestReachable = maxHearts - 1;
            if (highestReachable <= 0) return minIntervalS;

            float fraction = Clamp((float)currentHearts / highestReachable, 0f, 1f);
            float urgency = 1f - fraction; // 0 one below the cap, 1 at zero hearts
            return baseIntervalS + (minIntervalS - baseIntervalS) * urgency;
        }

        // ── Safe-time readout (time since last crash) ─────────────────────
        //
        //  CLAUDE.md's project-wide rule is "No digits, ever, in any
        //  patient-visible string or active text component" -- this readout
        //  is a DELIBERATE, EXPLICIT exception to that rule, Simon's own
        //  instruction (2026-08-19), after an earlier non-digit version (a
        //  sweeping ring, no numbers at all) was built specifically to honour
        //  the rule and then explicitly rejected in favour of a literal
        //  clock. Every OTHER patient-visible string in this instrument
        //  (HighwayDrivePatientStrings) still follows the rule and is still
        //  covered by HighwayDriveDigitFreeTests -- this is a narrow,
        //  intentional carve-out for one component, not a reversal of the
        //  rule itself.

        /// <summary>Formats elapsed seconds as a stopwatch-style clock: M:SS
        /// normally, H:MM:SS once an hour has passed. Truncates rather than
        /// rounds (an elapsed-time readout should never visibly run ahead of
        /// the actual elapsed time by rounding up) and floors negative input
        /// at zero rather than showing a negative time.</summary>
        public static string FormatElapsedClock(float totalSeconds)
        {
            int wholeSeconds = totalSeconds > 0f ? (int)totalSeconds : 0;
            int hours = wholeSeconds / 3600;
            int minutes = (wholeSeconds % 3600) / 60;
            int seconds = wholeSeconds % 60;

            return hours > 0
                ? hours + ":" + Pad2(minutes) + ":" + Pad2(seconds)
                : minutes + ":" + Pad2(seconds);
        }

        private static string Pad2(int value) => value < 10 ? "0" + value : value.ToString();
    }
}
