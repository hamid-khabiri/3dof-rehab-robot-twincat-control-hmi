using UnityEngine;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE CONFIG (HD-v1)
    //  Every clinician-facing knob for the instrument, grouped by what it
    //  affects, in the same plain-data style as RallyPaddleConfig. Read LIVE
    //  every frame by Road/Traffic/Controller/Input rather than cached at
    //  Start(), so a mid-session adjustment in the Inspector takes effect
    //  immediately -- that is how this scaffold gets tuned against a real
    //  patient without stopping Play mode. (The exceptions are the LAYOUT
    //  values -- LaneCount, RoadHalfWidth, prop spacing -- which
    //  HighwayDriveRoad reads once in Start() because changing them means
    //  rebuilding GameObjects.)
    //
    //  PARAMETERISED FOR 3/4/5 LANES, ONE IMPLEMENTATION (Simon's spec,
    //  2026-08-24). There is no per-lane-count code path anywhere: lane centre
    //  positions are GENERATED from LaneCount by
    //  HighwayDriveControlMath.LaneCenterX, the road is assembled from
    //  LaneCount-1 dashed dividers, and traffic spawns into those generated
    //  centres. Moving from three lanes to four or five is a change to
    //  LaneCount + RoadHalfWidth + LateralWorkspaceRange here, nothing more.
    //
    //  AND THE POINT OF MORE LANES IS A BIGGER LATERAL EXCURSION, not just
    //  more painted stripes -- widen RoadHalfWidth WITH LaneCount so the
    //  patient has to reach further, rather than shrinking the lanes to fit
    //  more of them into the same road. Three lanes is the introductory
    //  condition; five is the largest reach.
    //
    //  SPEED BUDGET, and why these particular defaults hang together:
    //  CruiseSpeed (neutral input) sits ABOVE the whole traffic band, so a
    //  patient doing nothing at all still overtakes gently and the game keeps
    //  presenting obstacles -- that is the steady state Simon described.
    //  CruiseSpeed - SpeedSwing sits BELOW the traffic band, so full
    //  deceleration genuinely lets traffic stream past rather than merely
    //  overtaking more slowly. The number that actually governs difficulty is
    //  neither of those but the CLOSING speed against the slowest traffic.
    // ─────────────────────────────────────────────────────────────────────────

    public enum HighwayDriveInputSource
    {
        /// <summary>Dev/lab default: the mouse or trackpad cursor's offset from
        /// the middle of the SCREEN drives both axes directly.</summary>
        MouseScreen,

        /// <summary>The planar-motion robot drives both axes. See
        /// HighwayDriveInput.ReadRobotAxes for the mapping and its
        /// caveats.</summary>
        PlanarRobot,

        /// <summary>Crazy Car: the Beckhoff PLC via TwincatConnectorCrazyCar --
        /// wrist angle changes lanes, Y (0-100 %) is the gas pedal. Falls back
        /// to Keyboard while the PLC is not connected. Needs LaneSteering.</summary>
        TwinCAT,

        /// <summary>Crazy Car test input without the robot: Left/Right (A/D)
        /// twist the wrist, Up (W) is the gas pedal. Needs LaneSteering.</summary>
        Keyboard,
    }

    /// <summary>How the wrist picks a lane in lane steering (Crazy Car).</summary>
    public enum LaneSteeringMode
    {
        /// <summary>The wrist angle maps straight to a lane: turned a little =
        /// the next lane, held there = stays there, relaxed = middle.</summary>
        WristPosition,

        /// <summary>One twist past the threshold = one lane; the wrist must
        /// come back near neutral before the next change.</summary>
        TwistPerLane,
    }

    public class HighwayDriveConfig : MonoBehaviour
    {
        // ── Road geometry ────────────────────────────────────────────────

        [Header("Road (parameterised: 3 / 4 / 5 lanes)")]
        [Tooltip("Number of equal traffic lanes. THREE is the reference/introductory condition. Lane centre " +
                 "positions are generated from this (HighwayDriveControlMath.LaneCenterX) and the road is " +
                 "built with LaneCount-1 dashed dividers, so raising this needs no code change -- but raise " +
                 "RoadHalfWidth and LateralWorkspaceRange with it, or you get narrower lanes instead of the " +
                 "larger lateral excursion that is the actual point of the progression.")]
        [Range(2, 6)] public int LaneCount = 3;

        [Tooltip("Half-width of the drivable road surface, world units; the road spans -this..+this in X. " +
                 "Suggested progression that keeps lane width roughly constant while growing the required " +
                 "reach: 3 lanes = 3.9, 4 lanes = 5.2, 5 lanes = 6.5.")]
        public float RoadHalfWidth = 3.9f;

        [Tooltip("How far above and below the camera view the road, markings, and props are built, world " +
                 "units. Needs enough margin that a scrolling element is recycled off-view, never on-view.")]
        public float VerticalBuildMargin = 4f;

        [Tooltip("Width of the painted edge line, world units. Purely visual -- the drivable limit is " +
                 "RoadHalfWidth minus LateralSafetyMarginWorld, see that field.")]
        public float EdgeLineWidthWorld = 0.18f;

        // ── Environment theme ────────────────────────────────────────────

        [Header("Environment theme")]
        [Tooltip("How HighwayDriveRoad picks the environment art set (Green/mountain, Desert, Snow). " +
                 "AutoRotateDaily changes it about once a day on its own " +
                 "(HighwayDriveControlMath.ThemeIndexForDay, keyed off the calendar day so it needs no " +
                 "saved state) -- switch to Fixed to pin one for a demo or a specific session.")]
        public HighwayDriveEnvironmentThemeMode ThemeMode = HighwayDriveEnvironmentThemeMode.Fixed;
        [Tooltip("Used only when ThemeMode is Fixed. Green is the reference environment -- the dark asphalt " +
                 "has the strongest contrast against the verge, so the road reads clearly.")]
        public HighwayDriveEnvironmentTheme FixedTheme = HighwayDriveEnvironmentTheme.Green;

        [Tooltip("Fallback colours, used only when the corresponding real art sprite is unassigned.")]
        public Color RoadColor = new Color(0.16f, 0.17f, 0.19f, 1f);
        public Color ShoulderColor = new Color(0.20f, 0.28f, 0.18f, 1f);
        public Color LaneMarkingColor = new Color(0.92f, 0.92f, 0.88f, 0.85f);
        public Color EdgeLineColor = new Color(0.95f, 0.90f, 0.55f, 0.9f);
        public Color RoadsidePropColor = new Color(0.28f, 0.42f, 0.26f, 1f);

        // ── Player car ───────────────────────────────────────────────────

        [Header("Player car")]
        [Tooltip("World-unit size (width, length) of the player's car. THIS IS THE COLLISION BOX, and it is " +
                 "also what the sprite is stretched to (HighwayDriveSpriteFactory.FitToWorldSize), so the " +
                 "car the patient sees IS the car that can be hit -- no invisible margin producing " +
                 "collisions that visually did not occur. Aspect matches Art/car_player_blue.png.")]
        public Vector2 PlayerCarSize = new Vector2(0.92f, 1.52f);
        [Tooltip("Extra forgiveness on the collision box, as a fraction shaved off each side. Small and " +
                 "POSITIVE-SHRINKS-THE-BOX by design: the box may be slightly smaller than the art, never " +
                 "larger, so a near-miss reads as a near-miss.")]
        [Range(0f, 0.3f)] public float CollisionForgiveness = 0.12f;
        [Tooltip("Tint for the PROCEDURAL fallback car only -- ignored once the real sprite is assigned.")]
        public Color PlayerCarColor = new Color(0.25f, 0.70f, 0.95f, 1f);
        [Tooltip("World Y the player's car sits at. Negative puts it in the lower part of the view, which " +
                 "buys forward visibility -- the patient needs to see traffic coming.")]
        public float PlayerAnchorY = -2.6f;
        [Tooltip("How far the car slides up the screen at full throttle. Kept SMALL: the forward/backward " +
                 "axis is meant to read as a SPEED change (world scroll rate), not as the car moving up the " +
                 "screen. Set 0 for a strictly anchored car.")]
        public float PlayerForwardShiftWorld = 0.35f;

        [Header("Player identification halo")]
        [Tooltip("PERMANENT halo around the player car. OFF by default (Simon, 2026-08-25): the bright cyan " +
                 "player car is already unmistakable against the red/orange/green/white/purple traffic, so a " +
                 "standing ring is clutter that can still read as a collision boundary. The halo asset is " +
                 "not wasted -- HighwayDriveController shows it TEMPORARILY for collision, recentering and " +
                 "Ready, where a transient ring genuinely means something.")]
        public bool ShowPlayerHighlight = false;
        [Tooltip("Halo size as a multiple of the car's own size. Just over 1 -- it should extend only " +
                 "slightly beyond the car and stay visually secondary.")]
        [Range(1f, 2f)] public float PlayerHighlightDiameterFactor = 1.18f;
        [Range(0f, 1f)] public float PlayerHighlightMinAlpha = 0.35f;
        [Range(0f, 1f)] public float PlayerHighlightMaxAlpha = 0.75f;
        public float PlayerHighlightPulseHz = 0.5f;

        // ── Control mapping ──────────────────────────────────────────────

        [Header("Control mapping")]
        public HighwayDriveInputSource InputSource = HighwayDriveInputSource.MouseScreen;

        [Tooltip("CONTINUOUS lateral control -- the car is never snapped to a lane. The lanes structure the " +
                 "environment and the traffic; they must not discretise the patient's movement, because the " +
                 "measured quantity IS the continuous planar excursion (Simon, 2026-08-24).\n\n" +
                 "Fraction of the calibrated comfortable lateral range that maps to the full usable road " +
                 "width. 1.0 means full comfortable left reaches the left boundary. Lower it to demand a " +
                 "larger excursion for the same road.")]
        [Range(0.2f, 1f)] public float LateralWorkspaceRange = 1f;

        [Tooltip("Keeps the car just inside the painted edge, world units, so the patient cannot visually " +
                 "drive off the road -- and so touching the edge marking is not itself a failure event.")]
        public float LateralSafetyMarginWorld = 0.12f;

        [Tooltip("Neutral dead zone on the LATERAL axis, as a fraction of the calibrated half-range. Small " +
                 "sensor fluctuation must not continuously walk the car sideways. The transition out of it " +
                 "is smooth, not a hard threshold -- see HighwayDriveControlMath.SmoothDeadzone.")]
        [Range(0f, 0.3f)] public float LateralDeadzone = 0.06f;

        [Tooltip("Neutral dead zone on the SPEED axis, same reasoning and same smooth transition.")]
        [Range(0f, 0.4f)] public float SpeedDeadzone = 0.08f;

        [Tooltip("Seconds for the car's lateral position to catch up to the commanded position. A short " +
                 "smoothing after the dead zone, so tremor reads as a steady hand rather than as jitter. " +
                 "Deliberately small -- this must not become steering lag.")]
        public float LateralSmoothingTimeS = 0.08f;

        [Tooltip("Multiplier on the mapped lateral command. 1.0 = the calibrated mapping exactly.")]
        public float SteeringGain = 1f;
        [Tooltip("Multiplier on the mapped forward/backward speed command. 1.0 = the calibrated mapping.")]
        public float SpeedGain = 1f;

        [Tooltip("Flips the lateral axis, for a patient working with the mirrored arm.")]
        public bool MirrorLateral = false;

        [Tooltip("Seconds for the drawn speed to catch up to the demanded speed. A rendering nicety so a " +
                 "trackpad jump does not snap the whole world scroll; 0 is strictly direct.")]
        public float SpeedResponseTimeS = 0.15f;

        [Header("Control mapping -- planar robot (InputSource = PlanarRobot)")]
        [Tooltip("Robot lateral (medial-lateral) displacement, world units, for FULL comfortable reach to " +
                 "one side. This is the calibrated workspace half-range: after calibration, full comfortable " +
                 "left maps near the left road boundary, neutral to road centre, full comfortable right near " +
                 "the right boundary.")]
        public float RobotLateralHalfRangeWorld = 12f;
        [Tooltip("Robot forward/backward (reach) displacement, world units, for full throttle or full braking.")]
        public float RobotForwardHalfRangeWorld = 12f;

        [Header("Lane steering (Crazy Car)")]
        [Tooltip("DISCRETE lane control instead of the continuous lateral mapping: the car always ends up " +
                 "in the centre of a lane, never on a line. Y (0-100 %) becomes a one-way gas pedal: 0 % = " +
                 "cruise speed, 100 % = cruise + SpeedSwing. Off for Highway Drive.")]
        public bool LaneSteering = false;
        [Tooltip("WristPosition: the wrist angle picks the lane directly (small turn = next lane, hold it to " +
                 "stay). TwistPerLane: each twist past LaneChangeThresholdDeg moves one lane.")]
        public LaneSteeringMode LaneMode = LaneSteeringMode.WristPosition;
        [Tooltip("WristPosition mode: wrist angle, degrees, that reaches the OUTERMOST lane. With 3 lanes the " +
                 "car moves to a side lane at about two thirds of this. Smaller = less wrist movement needed.")]
        [Range(3f, 60f)] public float WristFullScaleDeg = 12f;
        [Tooltip("WristPosition mode: extra margin, in lanes, before switching back, so a wrist resting " +
                 "right on a boundary does not flicker the car between two lanes.")]
        [Range(0f, 0.45f)] public float LaneHysteresis = 0.15f;
        [Tooltip("Subtracted from the robot's wrist angle, degrees. Set it to the angle TwinCAT shows with the " +
                 "wrist relaxed, so relaxed = middle lane.")]
        public float WristZeroOffsetDeg = 0f;
        [Tooltip("TwistPerLane mode: wrist angle, degrees, that triggers a lane change. + = right lane, " +
                 "- = left lane (MirrorLateral flips it).")]
        public float LaneChangeThresholdDeg = 15f;
        [Tooltip("The wrist must return inside +/- this many degrees before the next lane change is " +
                 "allowed, so one twist is one lane however long it is held.")]
        public float LaneRearmDeg = 5f;
        [Tooltip("Seconds the car takes to slide across one lane.")]
        public float LaneChangeSeconds = 0.25f;
        [Tooltip("After a collision the run resumes once the wrist is back inside LaneRearmDeg AND the " +
                 "gas (Y) is at or below this percentage.")]
        [Range(0f, 50f)] public float GasNeutralPercent = 15f;
        [Tooltip("Wrist angle the Left/Right keys simulate in Keyboard mode (and in TwinCAT mode while " +
                 "the PLC is not connected).")]
        public float KeyboardWristDeg = 25f;

        // ── Speed ────────────────────────────────────────────────────────

        [Header("Speed")]
        [Tooltip("True forward speed at neutral input, world units/second. Above the traffic band so a " +
                 "patient doing nothing still overtakes gently.")]
        public float CruiseSpeedWorldPerS = 11f;
        [Tooltip("How much full deflection adds to (or subtracts from) the cruise speed. Raised deliberately " +
                 "so the forward/backward axis is VISIBLY a speed change -- it drives road scroll, roadside " +
                 "scroll, and traffic-relative motion all at once.")]
        public float SpeedSwingWorldPerS = 7f;
        [Tooltip("Hard floor on true forward speed. Strictly positive: a car stopped dead collapses the " +
                 "forward-motion illusion and leaves the patient with nothing to do.")]
        public float MinSpeedWorldPerS = 2.5f;

        [Header("Speed ramp (Crazy Car)")]
        [Tooltip("Every this many seconds of driving the cruise speed steps up by SpeedRampStepWorldPerS, " +
                 "so the game gets harder the longer the patient survives. 0 = off (Highway Drive).")]
        public float SpeedRampIntervalSeconds = 0f;
        public float SpeedRampStepWorldPerS = 1f;
        [Tooltip("Cap on the number of ramp steps, so the speed stops rising eventually.")]
        [Range(0, 20)] public int SpeedRampMaxSteps = 8;

        [Header("Environment change (Crazy Car)")]
        [Tooltip("Every this many seconds of driving the environment moves on: Green -> Desert -> Snow -> " +
                 "Green ... 0 = never (keeps FixedTheme).")]
        public float ThemeChangeSeconds = 0f;

        [Header("Speed perception -- three parallax tiers")]
        // Longitudinal motion is split into three systems rather than driven
        // from one scroll velocity (Simon, 2026-08-25). Everything moving
        // together is what made the peripheral environment unreadable: at full
        // throttle the scenery flew past too fast to perceive, so the art could
        // not be appreciated at all. Traffic and lane markings stay at FULL
        // speed because judging closing speed against other cars IS the task;
        // the scenery is decoupled and capped so it stays legible.
        //
        // Deliberately not physically consistent -- in a true bird's-eye view
        // every static object shares one ground velocity. That consistency is
        // traded for the participant being able to see the environment while
        // still reading acceleration off the traffic.
        [Tooltip("Nearby roadside vegetation and rocks, as a fraction of the true speed. Around 0.3-0.5: " +
                 "slow enough to resolve individual plants, fast enough to still read as motion.")]
        [Range(0.05f, 1f)] public float RoadsideParallaxFactor = 0.4f;
        [Tooltip("Hard ceiling on roadside scenery speed, world units/second. This is what stops hard " +
                 "acceleration dragging the scenery back into a blur -- past it, speeding up changes the " +
                 "traffic and lane markings but leaves the background readable.")]
        public float RoadsideMaxSpeedWorldPerS = 5.5f;
        [Tooltip("Large landmarks (houses, ponds, turbines), as a fraction of true speed. Around 0.15-0.3 " +
                 "so they enter from the top, stay visible for several seconds, and drift slowly down the " +
                 "side -- which is what makes the three environments feel genuinely different rather than " +
                 "just differently-coloured moving texture.")]
        [Range(0.05f, 1f)] public float LandmarkParallaxFactor = 0.2f;
        [Tooltip("Hard ceiling on landmark speed, world units/second.")]
        public float LandmarkMaxSpeedWorldPerS = 3f;
        [Tooltip("Normalised speed above which the high-speed streaks appear. They must NOT be visible " +
                 "during normal cruising -- only at the clear upper end.")]
        [Range(0f, 1f)] public float StreakOnsetNormalizedSpeed = 0.45f;

        // ── Traffic ──────────────────────────────────────────────────────

        [Header("Traffic")]
        [Tooltip("Slowest and fastest true speed of other cars, world units/second. Both stay below " +
                 "CruiseSpeedWorldPerS or neutral input stops producing overtakes.")]
        public float TrafficSpeedMinWorldPerS = 6f;
        public float TrafficSpeedMaxWorldPerS = 8.5f;
        [Tooltip("Random delay between traffic spawns, seconds. Lower means denser traffic. NOTE this is " +
                 "deliberately INDEPENDENT of the patient's forward command: reaching forward changes " +
                 "relative velocity only. If speed also raised density we could not tell poor performance " +
                 "caused by speed from poor performance caused by traffic (Simon, 2026-08-24).")]
        public float SpawnIntervalMinS = 0.9f;
        public float SpawnIntervalMaxS = 2.1f;
        [Tooltip("Minimum along-road gap, world units, between two cars in the SAME lane at spawn time.")]
        public float MinLaneGapWorld = 6.5f;
        [Tooltip("Lanes that must stay free of newly-spawned traffic in the spawn band. At least 1 " +
                 "guarantees the road is always physically passable.")]
        [Range(0, 3)] public int MinFreeLanes = 1;
        [Tooltip("Fallback world-unit size, used only for the procedural placeholder car. With real art each " +
                 "vehicle is sized from its CLASS width below, with length following that sprite own " +
                 "aspect ratio.")]
        public Vector2 TrafficCarSize = new Vector2(0.92f, 1.5f);

        [Header("Traffic vehicle classes")]
        // The art pack draws every vehicle to roughly fill its own canvas, so
        // relative scale is NOT authored into the sprites -- a motorcycle crops
        // to about the same pixel size as a saloon. Class width is therefore
        // assigned here and length follows each sprite aspect, which keeps
        // every vehicle undistorted while making bikes genuinely narrow and
        // trucks genuinely large.
        [Tooltip("Body width of an ordinary car, world units. Also its collision box width.")]
        public float TrafficCarWidthWorld = 0.92f;
        [Tooltip("Body width of a truck. Wider and, via its sprite aspect, longer than a car.")]
        public float TrafficTruckWidthWorld = 1.08f;
        [Tooltip("Body width of a motorcycle. Genuinely narrow, so a bike is a real chance to slip past in " +
                 "a gap a car could not take.")]
        public float TrafficMotorcycleWidthWorld = 0.5f;
        [Tooltip("Share of spawns that are trucks (0-1).")]
        [Range(0f, 1f)] public float TrafficTruckShare = 0.18f;
        [Tooltip("Share of spawns that are motorcycles (0-1). The remainder are ordinary cars.")]
        [Range(0f, 1f)] public float TrafficMotorcycleShare = 0.16f;
        [Tooltip("PROCEDURAL FALLBACK ONLY -- random body colours used when the real car sprites are " +
                 "unassigned on HighwayDriveTraffic.")]
        public Color[] TrafficCarColors = new[]
        {
            new Color(0.85f, 0.35f, 0.30f, 1f),
            new Color(0.90f, 0.65f, 0.25f, 1f),
            new Color(0.55f, 0.50f, 0.58f, 1f),
            new Color(0.35f, 0.60f, 0.40f, 1f),
        };

        // ── Trial + collision recovery ───────────────────────────────────

        [Header("Trial")]
        [Tooltip("Total assessment duration, seconds. The trial clock counts this ONE quantity and does NOT " +
                 "reset on collision -- it pauses during recentering and resumes. Showing 0:00 after every " +
                 "collision would imply a new trial had begun when it had not (Simon, 2026-08-24). " +
                 "0 = open-ended, ended by the therapist.")]
        public float TrialDurationSeconds = 180f;
        [Tooltip("End the game when the last heart is lost (Crazy Car). Off = a crash at zero hearts " +
                 "just recentres like any other.")]
        public bool EndWhenHeartsRunOut = false;

        [Header("Collision recovery")]
        [Tooltip("Recovery is gated on the patient actually RETURNING TO NEUTRAL, not on waiting out a " +
                 "timer. These two are the neutral tolerance on each axis, as a fraction of full range.")]
        [Range(0.02f, 0.4f)] public float NeutralToleranceLateral = 0.12f;
        [Range(0.02f, 0.4f)] public float NeutralToleranceForward = 0.15f;
        [Tooltip("How long the end effector must REMAIN inside that tolerance before the run resumes. " +
                 "Brief -- this is a recentering check, not a rest period.")]
        public float NeutralHoldSeconds = 0.4f;
        [Tooltip("How long Ready shows after neutral is achieved, before traffic resumes.")]
        public float ReadySeconds = 0.6f;
        [Tooltip("Seconds the world takes to coast to a stop after a collision.")]
        public float CrashStopSeconds = 0.35f;
        [Tooltip("Legacy timed auto-restart. Left at 0 because recovery is now neutral-gated; a positive " +
                 "value restores a pure timer as a fallback if the neutral check is unreachable for a given " +
                 "patient.")]
        public float AutoRestartSeconds = 0f;
        [Tooltip("Crazy Car: skip the 'Collision - Return to center' step entirely. After the crash coast " +
                 "the road is cleared and driving resumes straight after the brief Ready.")]
        public bool SkipRecentering = false;

        // ── Tutorial + feedback ──────────────────────────────────────────

        [Header("Tutorial")]
        [Tooltip("Seconds the planar-control cross (Faster / Move left / Move right / Slower) shows before " +
                 "gameplay. Then it is removed COMPLETELY -- these instructions must not sit over live " +
                 "traffic.")]
        public float TutorialSeconds = 2.5f;

        [Header("Positive feedback")]
        [Tooltip("Seconds of controlled driving -- no collision, no excessive steering correction -- before " +
                 "the Nice control badge appears. Selective by design: permanently-displayed praise is " +
                 "decoration, not feedback.")]
        public float NiceControlAfterSeconds = 8f;
        [Tooltip("How long the badge stays up before fading.")]
        public float NiceControlVisibleSeconds = 1f;
        [Tooltip("Crazy Car: instead of always 'Nice control!', pick a motivating sentence that grows with " +
                 "the drive -- group one in the first PraiseGroupSeconds, group two in the next, group three " +
                 "after that (HighwayDrivePatientStrings.PraiseGroup*).")]
        public bool MotivationalPraise = false;
        [Tooltip("Length of each praise group, seconds of driving.")]
        public float PraiseGroupSeconds = 60f;
        [Tooltip("Lateral command change per second above which steering counts as an excessive correction, " +
                 "resetting the controlled-driving timer.")]
        public float ExcessiveSteeringRatePerS = 2.5f;
        [Tooltip("World units of driving between each steady-driving check (legacy distance milestone).")]
        public float SteadyFeedbackIntervalWorld = 110f;

        // ── Roadside scenery ─────────────────────────────────────────────

        [Header("Roadside scenery")]
        [Tooltip("Along-road spacing of the small verge props (grass, flowers, small rocks), world units.")]
        public float RoadsidePropIntervalWorld = 3.2f;
        [Tooltip("Prop height in world units -- width follows each sprite's own aspect ratio " +
                 "(HighwayDriveRoad.PropSizeForSprite), because the theme sprites are genuinely different " +
                 "shapes and one shared width would squash some of them.")]
        public float RoadsidePropHeightWorld = 0.9f;

        [Header("Landmarks")]
        [Tooltip("Distinctive scenery (ponds, houses, wind turbines, farms) placed OUTSIDE the road, well " +
                 "clear of the carriageway. Sparse on purpose: roughly one every several seconds is enough " +
                 "to communicate forward speed. Filling every gap makes none of them register.")]
        public bool ShowLandmarks = true;
        public float LandmarkIntervalMinS = 4.5f;
        public float LandmarkIntervalMaxS = 9f;
        [Tooltip("Landmark height in world units; width follows the sprite aspect.")]
        public float LandmarkHeightWorld = 2.6f;
        [Tooltip("Gap between the road edge and the nearest landmark edge, world units. Keeps the central " +
                 "roadway clean.")]
        public float LandmarkClearanceWorld = 1.4f;

        // ── Resistance (robot) ───────────────────────────────────────────

        [Header("Resistance (robot)")]
        [Tooltip("Command the robot a resistive force stepped by how far the speed demand sits from " +
                 "neutral. Symmetric: hard acceleration and hard braking both load the arm. Forced to zero " +
                 "whenever a run is not live.")]
        public bool EnableResistance = true;
        [Tooltip("Overall multiplier on every resistance class -- same knob and same range as Gold Miner's " +
                 "ResistanceGain. Capped at 1.0: the class table's top entry is already exactly the robot's " +
                 "10 N maximum, so a gain above 1 could only ask for force the hardware cannot give.")]
        [Range(0f, 1f)] public float ResistanceGain = 1f;
        [Tooltip("The patient's own force ceiling, newtons. Stands in for UserWorkspaceProfile.FSafeNewtons. " +
                 "Can only lower the commanded force, never raise it above the robot's 10 N maximum.")]
        public float SafeMaxForceNewtons = 10f;

        // ── Hearts ───────────────────────────────────────────────────────

        [Header("Hearts")]
        [Tooltip("Starting and maximum heart count, shown top-left. One is lost per collision (floored at " +
                 "0), one regained per on-road pickup (capped here). SESSION-scoped, not per-run.")]
        [Range(1, 10)] public int MaxHearts = 5;
        [Tooltip("Heart colour, for both the on-road pickup and the UI row. Its own colour rather than " +
                 "ThemeColor -- a red heart should read as a heart first.")]
        public Color HeartColor = new Color(0.90f, 0.22f, 0.32f, 1f);
        [Tooltip("World-unit size of the on-road heart pickup. Enlarged (Simon, 2026-08-25) so it reads " +
                 "clearly as a target to aim for well before it arrives.")]
        public Vector2 HeartPickupSize = new Vector2(0.95f, 0.95f);
        [Tooltip("Seconds of VISIBLE approach between a heart entering the top of the view and reaching the " +
                 "car. The pickup closes at a fixed rate to achieve this, deliberately INDEPENDENT of " +
                 "driving speed -- a heart closing at the vehicle-relative rate sweeps past in under a " +
                 "second at speed, which is not something a rehabilitation participant can plan a reach " +
                 "around. Fixing it makes the planning window identical every time.")]
        public float HeartApproachSeconds = 2.5f;
        [Tooltip("How far either side of the car a heart may spawn, world units. Keeps collection within a " +
                 "comfortable reach of the current position: hearts are an OPTIONAL bonus and must never " +
                 "bait a sudden large lateral movement into traffic. Roughly one lane by default.")]
        public float HeartLateralReachWorld = 2.6f;
        [Tooltip("Pulse rate of the heart pickup, Hz. A subtle breathing scale makes it read as a " +
                 "collectible rather than as an obstacle.")]
        public float HeartPulseHz = 1.1f;
        [Tooltip("Peak scale increase during the pulse, as a fraction. Subtle by design.")]
        [Range(0f, 0.4f)] public float HeartPulseAmount = 0.12f;
        [Tooltip("Spawn-check interval when one heart short of the cap -- the least urgent case.")]
        public float HeartSpawnIntervalBaseS = 25f;
        [Tooltip("Spawn-check interval at zero hearts -- the most urgent case, and the DDA part: fewer " +
                 "hearts means more frequent chances to recover.")]
        public float HeartSpawnIntervalMinS = 8f;

        // ── Patient UI ───────────────────────────────────────────────────

        // ── Comparison variant ───────────────────────────────────────────

        [Header("Comparison variant (Crazy Car scene)")]
        // Assets/Scenes/Crazy Car.unity is the 2026-08-24 tuning kept alive
        // side by side with the current one, so the two can be judged against
        // each other rather than from memory.
        //
        // MOST of the difference between them is ordinary field values, set by
        // HighwayDriveSceneBuilder when it writes each scene. These three flags
        // exist only for the differences that are genuinely BEHAVIOURAL and
        // cannot be expressed as a number. They are deliberately narrow and
        // explicitly named after the old behaviour, rather than a general
        // "legacy mode" that would quietly accumulate meaning.
        //
        // Note this is a RECONSTRUCTION, not a restored copy: HighwayDrive has
        // never been committed to git, so the previous files no longer exist
        // anywhere to be recovered.
        [Tooltip("Scenery rides the FULL road speed (slightly faster, in fact) instead of the capped " +
                 "three-tier parallax. This is the single biggest felt difference between the two scenes.")]
        public bool LegacySceneryCoupling = false;
        [Tooltip("Hearts close at the road speed and spawn in a random lane, instead of a fixed 2.5s " +
                 "approach spawned within reach of the car.")]
        public bool LegacyHeartPacing = false;
        [Tooltip("One flat vehicle pool at a single shared size, instead of cars/trucks/motorcycles each " +
                 "sized by class with their own collision boxes.")]
        public bool LegacyTrafficPool = false;

        [Header("Patient UI")]
        [Tooltip("The one accent colour used throughout the patient UI -- the speed gauge indicator, the " +
                 "centre target, and the player halo -- so everything that means 'you' or 'your status' " +
                 "matches. Hearts are deliberately excluded; see HeartColor.")]
        public Color ThemeColor = new Color(0.35f, 0.78f, 0.92f, 1f);
    }
}
