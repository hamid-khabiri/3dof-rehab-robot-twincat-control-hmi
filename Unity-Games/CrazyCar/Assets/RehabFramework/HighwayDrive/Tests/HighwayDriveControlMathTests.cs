using System;
using NUnit.Framework;
using RehabFramework.HighwayDrive;

namespace RehabFramework.HighwayDrive.Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE CONTROL MATH TESTS
    //  Covers the pure control-path decisions in Core/HighwayDriveControlMath:
    //  the two axis mappings, the resistance command, lane geometry, and the
    //  collision predicate. EditMode only -- no Play Mode, no GameObject, no
    //  robot.
    //
    //  The tests worth reading first are the INVARIANT ones, which pin
    //  properties the instrument's safety and playability actually rest on
    //  rather than just re-stating the arithmetic:
    //    - Resistance_AtNeutral_IsExactlyZero: the robot never pushes on a
    //      resting arm.
    //    - PlayerSpeed_FullBrake_CanDropBelowSlowestTraffic: every traffic
    //      arrangement is escapable, so no crash is unavoidable.
    //    - LateralWorldX_AtFullDeflection_KeepsCarBodyOnTheRoad: full reach
    //      parks the car ON the road, not half off it.
    //    - AdjacentLaneCars_DoNotOverlap: the painted lanes are actually wide
    //      enough for the cars that drive in them.
    // ─────────────────────────────────────────────────────────────────────────
    public class HighwayDriveControlMathTests
    {
        private const float Tolerance = 1e-5f;

        // ── Deadzone (speed axis) ────────────────────────────────────────

        [Test]
        public void ApplyDeadzone_InsideBand_IsExactlyZero()
        {
            Assert.AreEqual(0f, HighwayDriveControlMath.ApplyDeadzone(0f, 0.1f), Tolerance);
            Assert.AreEqual(0f, HighwayDriveControlMath.ApplyDeadzone(0.09f, 0.1f), Tolerance);
            Assert.AreEqual(0f, HighwayDriveControlMath.ApplyDeadzone(-0.09f, 0.1f), Tolerance);
            Assert.AreEqual(0f, HighwayDriveControlMath.ApplyDeadzone(0.1f, 0.1f), Tolerance);
        }

        [Test]
        public void ApplyDeadzone_AtFullDeflection_StillReachesUnity()
        {
            // The rescale exists so a deadband does not cost the patient any
            // of their range -- full reach must still mean full throttle.
            Assert.AreEqual(1f, HighwayDriveControlMath.ApplyDeadzone(1f, 0.1f), Tolerance);
            Assert.AreEqual(-1f, HighwayDriveControlMath.ApplyDeadzone(-1f, 0.1f), Tolerance);
            Assert.AreEqual(1f, HighwayDriveControlMath.ApplyDeadzone(1f, 0.4f), Tolerance);
        }

        [Test]
        public void ApplyDeadzone_IsContinuousAtTheBandEdge()
        {
            // No step at the boundary: leaving the deadband must ramp from
            // zero, not jump. A discontinuity here would feel like the car
            // lurching the instant the patient leaves neutral.
            float justOutside = HighwayDriveControlMath.ApplyDeadzone(0.1001f, 0.1f);
            Assert.Greater(justOutside, 0f);
            Assert.Less(justOutside, 0.01f);
        }

        [Test]
        public void ApplyDeadzone_WithZeroDeadzone_IsIdentity()
        {
            Assert.AreEqual(0.37f, HighwayDriveControlMath.ApplyDeadzone(0.37f, 0f), Tolerance);
            Assert.AreEqual(-0.62f, HighwayDriveControlMath.ApplyDeadzone(-0.62f, 0f), Tolerance);
        }

        [Test]
        public void ApplyDeadzone_PreservesSign()
        {
            Assert.Greater(HighwayDriveControlMath.ApplyDeadzone(0.5f, 0.1f), 0f);
            Assert.Less(HighwayDriveControlMath.ApplyDeadzone(-0.5f, 0.1f), 0f);
        }

        // ── Normalised offset ────────────────────────────────────────────

        [Test]
        public void NormalizedOffsetFromCenter_MapsCentreAndEdges()
        {
            // A 1920-wide screen: centre reads neutral, either edge reads full.
            Assert.AreEqual(0f, HighwayDriveControlMath.NormalizedOffsetFromCenter(960f, 960f, 960f), Tolerance);
            Assert.AreEqual(1f, HighwayDriveControlMath.NormalizedOffsetFromCenter(1920f, 960f, 960f), Tolerance);
            Assert.AreEqual(-1f, HighwayDriveControlMath.NormalizedOffsetFromCenter(0f, 960f, 960f), Tolerance);
            Assert.AreEqual(0.5f, HighwayDriveControlMath.NormalizedOffsetFromCenter(1440f, 960f, 960f), Tolerance);
        }

        [Test]
        public void NormalizedOffsetFromCenter_ClampsBeyondTheEdges()
        {
            Assert.AreEqual(1f, HighwayDriveControlMath.NormalizedOffsetFromCenter(5000f, 960f, 960f), Tolerance);
            Assert.AreEqual(-1f, HighwayDriveControlMath.NormalizedOffsetFromCenter(-5000f, 960f, 960f), Tolerance);
        }

        [Test]
        public void NormalizedOffsetFromCenter_WithZeroExtent_ReturnsZeroNotInfinity()
        {
            // Guards the real case of reading Screen.width before a window
            // exists, which would otherwise divide by zero and hand a NaN
            // straight into the car's position.
            float result = HighwayDriveControlMath.NormalizedOffsetFromCenter(500f, 0f, 0f);
            Assert.AreEqual(0f, result, Tolerance);
        }

        // ── Lateral mapping ──────────────────────────────────────────────

        [Test]
        public void LateralWorldX_AtFullDeflection_KeepsCarBodyOnTheRoad()
        {
            const float roadHalfWidth = 4.2f;
            const float carHalfWidth = 0.475f;

            float right = HighwayDriveControlMath.LateralWorldX(1f, roadHalfWidth, carHalfWidth);
            float left = HighwayDriveControlMath.LateralWorldX(-1f, roadHalfWidth, carHalfWidth);

            // The car's EDGE lands on the road edge, not its centre.
            Assert.AreEqual(roadHalfWidth, right + carHalfWidth, Tolerance);
            Assert.AreEqual(-roadHalfWidth, left - carHalfWidth, Tolerance);
        }

        [Test]
        public void LateralWorldX_IsDirectPositionControl()
        {
            // Half input means half the usable travel -- the defining property
            // of a position law, as opposed to a rate law where the same input
            // held twice as long would go twice as far.
            float half = HighwayDriveControlMath.LateralWorldX(0.5f, 4.2f, 0.475f);
            float full = HighwayDriveControlMath.LateralWorldX(1f, 4.2f, 0.475f);
            Assert.AreEqual(full * 0.5f, half, Tolerance);
        }

        [Test]
        public void LateralWorldX_WithCarWiderThanRoad_PinsToCentre()
        {
            // Degenerate misconfiguration: never return a negative usable
            // half-width, which would invert the steering.
            float x = HighwayDriveControlMath.LateralWorldX(1f, 0.5f, 2f);
            Assert.AreEqual(0f, x, Tolerance);
        }

        // ── Speed mapping ────────────────────────────────────────────────

        [Test]
        public void PlayerSpeed_AtNeutral_IsExactlyCruise()
        {
            Assert.AreEqual(9f, HighwayDriveControlMath.PlayerSpeedWorldPerS(0f, 9f, 5.5f, 2f), Tolerance);
        }

        [Test]
        public void PlayerSpeed_FullBrake_CanDropBelowSlowestTraffic()
        {
            // THE ESCAPE INVARIANT. Full braking has to put the player below
            // the slowest car on the road, or a traffic formation could close
            // in with no way out -- unacceptable when the failure state ends
            // the run. Tracks the SHIPPED HighwayDriveConfig defaults, which
            // were retuned on 2026-08-24 (cruise 9->11, swing 5.5->7, traffic
            // floor 5.5->6) to make the forward/backward axis visibly change
            // speed. If those are ever retuned again so that
            // cruise - swing >= slowest traffic, this test is what should stop
            // it.
            const float cruise = 11f;
            const float swing = 7f;
            const float slowestTraffic = 6f;
            const float floor = 2.5f;

            float braked = HighwayDriveControlMath.PlayerSpeedWorldPerS(-1f, cruise, swing, floor);
            Assert.Less(braked, slowestTraffic,
                "Full braking must be slower than the slowest traffic, or a formation can become inescapable.");
        }

        [Test]
        public void PlayerSpeed_NeverStopsTheCarDead()
        {
            // A stationary car collapses the forward-motion illusion and leaves
            // the patient with nothing to do, so the floor is a hard one.
            float braked = HighwayDriveControlMath.PlayerSpeedWorldPerS(-1f, 4f, 10f, 2f);
            Assert.AreEqual(2f, braked, Tolerance);
            Assert.Greater(braked, 0f);
        }

        [Test]
        public void PlayerSpeed_FullThrottle_AddsTheWholeSwing()
        {
            Assert.AreEqual(14.5f, HighwayDriveControlMath.PlayerSpeedWorldPerS(1f, 9f, 5.5f, 2f), Tolerance);
        }

        // ── Resistance ───────────────────────────────────────────────────

        [Test]
        public void Resistance_AtNeutral_IsExactlyZero()
        {
            // Safety property, not a rounding nicety: a non-zero force at
            // neutral means the robot is always pushing against a resting arm.
            Assert.AreEqual(0f, HighwayDriveControlMath.ResistanceNewtons(0f, 1f, 10f), Tolerance);
            Assert.AreEqual(0, HighwayDriveControlMath.ResistanceClassIndex(0f));
        }

        [Test]
        public void Resistance_IsSymmetricAboutNeutral()
        {
            // Simon's choice: accelerating hard and braking hard load the arm
            // equally.
            float pushing = HighwayDriveControlMath.ResistanceNewtons(0.6f, 1f, 10f);
            float pulling = HighwayDriveControlMath.ResistanceNewtons(-0.6f, 1f, 10f);
            Assert.AreEqual(pushing, pulling, Tolerance);
            Assert.Greater(pushing, 0f);
        }

        [Test]
        public void ResistanceClasses_TopClassIsExactlyTheHardwareMaximum()
        {
            // The whole point of the class table, matching Gold Miner's weight
            // table: the table IS the ceiling, so full deflection asks for
            // exactly the robot's maximum and never a newton more.
            Assert.AreEqual(10f, HighwayDriveControlMath.MaxCommandableNewtons, Tolerance);

            int topClass = HighwayDriveControlMath.ResistanceClassCount - 1;
            Assert.AreEqual(HighwayDriveControlMath.MaxCommandableNewtons,
                HighwayDriveControlMath.ResistanceClassNewtons(topClass), Tolerance);
            Assert.AreEqual(10f, HighwayDriveControlMath.ResistanceNewtons(1f, 1f, 10f), Tolerance);
            Assert.AreEqual(10f, HighwayDriveControlMath.ResistanceNewtons(-1f, 1f, 10f), Tolerance);
        }

        [Test]
        public void ResistanceClasses_AreZeroBasedAndMonotonic()
        {
            // Class 0 must be free, and each step must load strictly more than
            // the last -- a table that ever went down would make "push harder"
            // mean "work less" somewhere in the range.
            Assert.AreEqual(0f, HighwayDriveControlMath.ResistanceClassNewtons(0), Tolerance);

            for (int i = 1; i < HighwayDriveControlMath.ResistanceClassCount; i++)
            {
                Assert.Greater(HighwayDriveControlMath.ResistanceClassNewtons(i),
                    HighwayDriveControlMath.ResistanceClassNewtons(i - 1),
                    $"Resistance class {i} does not load more than class {i - 1}.");
            }
        }

        [Test]
        public void ResistanceClassIndex_ReservesClassZeroForExactlyNeutral()
        {
            // Any demand at all is already a loaded class -- how much tremor
            // still counts as neutral is the deadband's judgement, not this
            // function's.
            Assert.AreEqual(0, HighwayDriveControlMath.ResistanceClassIndex(0f));
            Assert.AreEqual(1, HighwayDriveControlMath.ResistanceClassIndex(0.001f));
            Assert.AreEqual(1, HighwayDriveControlMath.ResistanceClassIndex(-0.001f));
        }

        [Test]
        public void ResistanceClassIndex_StepsEvenlyAcrossTheRange()
        {
            // Four loaded classes over (0, 1], so each spans a quarter.
            Assert.AreEqual(1, HighwayDriveControlMath.ResistanceClassIndex(0.25f));
            Assert.AreEqual(2, HighwayDriveControlMath.ResistanceClassIndex(0.26f));
            Assert.AreEqual(2, HighwayDriveControlMath.ResistanceClassIndex(0.5f));
            Assert.AreEqual(3, HighwayDriveControlMath.ResistanceClassIndex(0.51f));
            Assert.AreEqual(4, HighwayDriveControlMath.ResistanceClassIndex(0.76f));
            Assert.AreEqual(4, HighwayDriveControlMath.ResistanceClassIndex(1f));
        }

        [Test]
        public void Resistance_NeverExceedsTenNewtonsAnywhereInTheRange()
        {
            // THE HARDWARE-CEILING INVARIANT, swept rather than spot-checked.
            // RobotOutputs.ResistiveForceN is documented as "0 to 10 ... 10 N
            // is the assumed hardware maximum", and this is a real force on a
            // patient's arm, so no combination of input, gain, or a mis-set
            // safe ceiling may put a number above 10 on that channel.
            for (int i = -300; i <= 300; i++)
            {
                float input = i / 100f;                       // -3.0 .. +3.0, deliberately past the legal range
                foreach (float gain in new[] { -5f, 0f, 0.5f, 1f, 40f })
                {
                    foreach (float safeMax in new[] { -1f, 4f, 10f, 250f })
                    {
                        float force = HighwayDriveControlMath.ResistanceNewtons(input, gain, safeMax);
                        Assert.GreaterOrEqual(force, 0f,
                            $"Negative force commanded for input {input}, gain {gain}, safeMax {safeMax}.");
                        Assert.LessOrEqual(force, HighwayDriveControlMath.MaxCommandableNewtons,
                            $"Force above the robot maximum for input {input}, gain {gain}, safeMax {safeMax}.");
                    }
                }
            }
        }

        [Test]
        public void ResistanceGain_OnlyEverReducesLoad()
        {
            // Gold Miner's rule, same reasoning: the table's top entry is
            // already the hardware maximum, so a gain above 1 could only ask
            // for force the robot cannot give. A gain of 40 must behave
            // exactly like a gain of 1, not like a 40x amplifier.
            float full = HighwayDriveControlMath.ResistanceNewtons(1f, 1f, 10f);
            float overdriven = HighwayDriveControlMath.ResistanceNewtons(1f, 40f, 10f);
            Assert.AreEqual(full, overdriven, Tolerance);

            float halved = HighwayDriveControlMath.ResistanceNewtons(1f, 0.5f, 10f);
            Assert.AreEqual(5f, halved, Tolerance);

            Assert.AreEqual(0f, HighwayDriveControlMath.ResistanceNewtons(1f, 0f, 10f), Tolerance);
            Assert.AreEqual(0f, HighwayDriveControlMath.ResistanceNewtons(1f, -10f, 10f), Tolerance);
        }

        [Test]
        public void Resistance_RespectsAPatientCeilingBelowTheHardwareMaximum()
        {
            // Lowering the safe ceiling is the intended way to load a patient
            // less than the full range (the FSafeNewtons role).
            Assert.AreEqual(4f, HighwayDriveControlMath.ResistanceNewtons(1f, 1f, 4f), Tolerance);
            Assert.AreEqual(4f, HighwayDriveControlMath.ResistanceNewtons(0.8f, 1f, 4f), Tolerance);
            // Below the ceiling the class value still governs.
            Assert.AreEqual(2.5f, HighwayDriveControlMath.ResistanceNewtons(0.2f, 1f, 4f), Tolerance);
            // A ceiling above the hardware maximum cannot raise anything.
            Assert.AreEqual(10f, HighwayDriveControlMath.ResistanceNewtons(1f, 1f, 250f), Tolerance);
        }

        // ── Lane geometry ────────────────────────────────────────────────

        [Test]
        public void LaneCentres_AreEvenlySpacedAndInsideTheRoad()
        {
            const float roadHalfWidth = 4.2f;
            const int laneCount = 4;

            float expectedSpacing = HighwayDriveControlMath.LaneWidth(roadHalfWidth, laneCount);
            Assert.AreEqual(2.1f, expectedSpacing, Tolerance);

            float previous = HighwayDriveControlMath.LaneCenterX(0, laneCount, roadHalfWidth);
            Assert.Greater(previous, -roadHalfWidth);

            for (int lane = 1; lane < laneCount; lane++)
            {
                float current = HighwayDriveControlMath.LaneCenterX(lane, laneCount, roadHalfWidth);
                Assert.AreEqual(expectedSpacing, current - previous, Tolerance);
                previous = current;
            }
            Assert.Less(previous, roadHalfWidth);
        }

        [Test]
        public void LaneCentres_AreSymmetricAboutTheRoadCentre()
        {
            const float roadHalfWidth = 4.2f;
            const int laneCount = 4;

            float leftmost = HighwayDriveControlMath.LaneCenterX(0, laneCount, roadHalfWidth);
            float rightmost = HighwayDriveControlMath.LaneCenterX(laneCount - 1, laneCount, roadHalfWidth);
            Assert.AreEqual(0f, leftmost + rightmost, Tolerance);
        }

        [Test]
        public void LaneCenterX_ClampsAnOutOfRangeIndex()
        {
            const float roadHalfWidth = 4.2f;
            const int laneCount = 4;

            Assert.AreEqual(HighwayDriveControlMath.LaneCenterX(0, laneCount, roadHalfWidth),
                HighwayDriveControlMath.LaneCenterX(-3, laneCount, roadHalfWidth), Tolerance);
            Assert.AreEqual(HighwayDriveControlMath.LaneCenterX(laneCount - 1, laneCount, roadHalfWidth),
                HighwayDriveControlMath.LaneCenterX(99, laneCount, roadHalfWidth), Tolerance);
        }

        [Test]
        public void AdjacentLaneCars_DoNotOverlap()
        {
            // Geometry regression guard: the painted lanes must actually be
            // wider than the cars that drive in them, or traffic in adjacent
            // lanes would register as a permanent crash. Swept across ALL
            // THREE shipped lane counts with their matching road widths, since
            // 3/4/5 lanes is now one parameterised implementation rather than
            // three builds -- a road width that failed to grow with LaneCount
            // is exactly the mistake this catches.
            const float carWidth = 0.92f;
            const float carLength = 1.5f;
            var configs = new[]
            {
                new { Lanes = 3, HalfWidth = 3.9f },
                new { Lanes = 4, HalfWidth = 5.2f },
                new { Lanes = 5, HalfWidth = 6.5f },
            };

            foreach (var cfg in configs)
            {
                for (int lane = 0; lane < cfg.Lanes - 1; lane++)
                {
                    float a = HighwayDriveControlMath.LaneCenterX(lane, cfg.Lanes, cfg.HalfWidth);
                    float b = HighwayDriveControlMath.LaneCenterX(lane + 1, cfg.Lanes, cfg.HalfWidth);

                    Assert.IsFalse(
                        HighwayDriveControlMath.RectsOverlap(a, 0f, carWidth, carLength, b, 0f, carWidth, carLength),
                        $"{cfg.Lanes} lanes: cars in lanes {lane} and {lane + 1} register as overlapping.");
                }
            }
        }

        [Test]
        public void LaneWidth_StaysRoughlyConstantAcrossTheShippedProgression()
        {
            // The progression is meant to be a LARGER LATERAL EXCURSION, not
            // narrower lanes crammed into the same road. If a future retune
            // widens LaneCount without widening RoadHalfWidth, lane width
            // collapses and the task changes character silently -- this pins
            // the intent.
            float w3 = HighwayDriveControlMath.LaneWidth(3.9f, 3);
            float w4 = HighwayDriveControlMath.LaneWidth(5.2f, 4);
            float w5 = HighwayDriveControlMath.LaneWidth(6.5f, 5);

            Assert.AreEqual(2.6f, w3, 0.01f);
            Assert.AreEqual(2.6f, w4, 0.01f);
            Assert.AreEqual(2.6f, w5, 0.01f);
        }

        // ── Calibrated lateral mapping ───────────────────────────────────

        [Test]
        public void MapLateralToRoad_FullReach_LandsInsideTheRoadEdge()
        {
            // Full comfortable reach must park the car NEAR the boundary but
            // never past it -- the patient should not be able to visually
            // drive off the road, and touching the edge marking is not itself
            // a failure event.
            const float halfWidth = 3.9f, carHalf = 0.46f, margin = 0.12f;
            float right = HighwayDriveControlMath.MapLateralToRoad(1f, halfWidth, carHalf, margin, 1f, 1f);
            float left = HighwayDriveControlMath.MapLateralToRoad(-1f, halfWidth, carHalf, margin, 1f, 1f);

            Assert.Less(right + carHalf, halfWidth, "Car body crossed the right road edge.");
            Assert.Greater(left - carHalf, -halfWidth, "Car body crossed the left road edge.");
            Assert.AreEqual(halfWidth - carHalf - margin, right, Tolerance);
            Assert.AreEqual(-right, left, Tolerance);
        }

        [Test]
        public void MapLateralToRoad_IsContinuous_NotSnappedToLanes()
        {
            // The defining property of the control law: a small change in
            // input produces a small change in position everywhere, including
            // across lane boundaries. Any snapping would show up as two
            // distinct inputs mapping to the identical position.
            float previous = HighwayDriveControlMath.MapLateralToRoad(-1f, 3.9f, 0.46f, 0.12f, 1f, 1f);
            for (int i = -99; i <= 100; i++)
            {
                float x = HighwayDriveControlMath.MapLateralToRoad(i / 100f, 3.9f, 0.46f, 0.12f, 1f, 1f);
                Assert.Greater(x, previous, $"Position did not advance at input {i / 100f} -- snapping?");
                previous = x;
            }
        }

        [Test]
        public void MapLateralToRoad_SmallerWorkspaceRange_DemandsALargerExcursion()
        {
            // Lowering workspaceRange is how 4- and 5-lane conditions get
            // harder: the SAME physical reach must now produce a LARGER road
            // displacement, so reaching the edge takes more excursion.
            float atFullRange = HighwayDriveControlMath.MapLateralToRoad(0.5f, 3.9f, 0.46f, 0.12f, 1f, 1f);
            float atHalfRange = HighwayDriveControlMath.MapLateralToRoad(0.5f, 3.9f, 0.46f, 0.12f, 0.5f, 1f);
            Assert.Greater(atHalfRange, atFullRange);
        }

        [Test]
        public void MapLateralToRoad_NeverLeavesTheRoad_ForAnyGainOrRange()
        {
            // Swept guard: no combination of gain, workspace range, or
            // out-of-range input may put the car body past the edge. A
            // mis-set therapist gain must degrade to "pinned at the edge",
            // never to "driving on the grass".
            const float halfWidth = 3.9f, carHalf = 0.46f, margin = 0.12f;
            foreach (float gain in new[] { 0f, 0.5f, 1f, 4f })
            {
                foreach (float range in new[] { 0.2f, 0.5f, 1f })
                {
                    for (int i = -300; i <= 300; i += 7)
                    {
                        float x = HighwayDriveControlMath.MapLateralToRoad(i / 100f, halfWidth, carHalf, margin, range, gain);
                        Assert.LessOrEqual(Math.Abs(x) + carHalf, halfWidth,
                            $"Car left the road at input {i / 100f}, gain {gain}, range {range}.");
                    }
                }
            }
        }

        // ── Smooth dead zone ─────────────────────────────────────────────

        [Test]
        public void SmoothDeadzone_InsideBand_IsExactlyZero()
        {
            Assert.AreEqual(0f, HighwayDriveControlMath.SmoothDeadzone(0f, 0.1f), Tolerance);
            Assert.AreEqual(0f, HighwayDriveControlMath.SmoothDeadzone(0.05f, 0.1f), Tolerance);
            Assert.AreEqual(0f, HighwayDriveControlMath.SmoothDeadzone(-0.1f, 0.1f), Tolerance);
        }

        [Test]
        public void SmoothDeadzone_LeavesNeutralGradually_NotAsAStep()
        {
            // The point of the smoothstep over the plain rescale: just outside
            // the band the command must still be very small, so the car eases
            // away from neutral rather than unsticking and setting off.
            float justOut = HighwayDriveControlMath.SmoothDeadzone(0.11f, 0.1f);
            float linear = HighwayDriveControlMath.ApplyDeadzone(0.11f, 0.1f);
            Assert.Greater(justOut, 0f);
            Assert.Less(justOut, linear, "Smoothed command should ease in below the linear rescale.");
        }

        [Test]
        public void SmoothDeadzone_StillReachesFullDeflection()
        {
            Assert.AreEqual(1f, HighwayDriveControlMath.SmoothDeadzone(1f, 0.1f), Tolerance);
            Assert.AreEqual(-1f, HighwayDriveControlMath.SmoothDeadzone(-1f, 0.1f), Tolerance);
        }

        [Test]
        public void SmoothDeadzone_IsMonotonic()
        {
            float previous = -1.001f;
            for (int i = -100; i <= 100; i++)
            {
                float v = HighwayDriveControlMath.SmoothDeadzone(i / 100f, 0.08f);
                Assert.GreaterOrEqual(v, previous, $"Non-monotonic at {i / 100f}.");
                previous = v;
            }
        }

        // ── Neutral tolerance (collision recovery gate) ──────────────────

        [Test]
        public void IsWithinNeutralTolerance_RequiresBothAxes()
        {
            Assert.IsTrue(HighwayDriveControlMath.IsWithinNeutralTolerance(0f, 0f, 0.12f, 0.15f));
            Assert.IsTrue(HighwayDriveControlMath.IsWithinNeutralTolerance(0.1f, -0.1f, 0.12f, 0.15f));
            // Lateral fine, forward out -- still not recentred.
            Assert.IsFalse(HighwayDriveControlMath.IsWithinNeutralTolerance(0f, 0.4f, 0.12f, 0.15f));
            // Forward fine, lateral out.
            Assert.IsFalse(HighwayDriveControlMath.IsWithinNeutralTolerance(-0.5f, 0f, 0.12f, 0.15f));
        }

        // ── Three-tier parallax ──────────────────────────────────────────

        [Test]
        public void ParallaxSpeed_BelowTheCap_IsASimpleFraction()
        {
            Assert.AreEqual(4.4f, HighwayDriveControlMath.ParallaxSpeed(11f, 0.4f, 99f), Tolerance);
            Assert.AreEqual(2.2f, HighwayDriveControlMath.ParallaxSpeed(11f, 0.2f, 99f), Tolerance);
        }

        [Test]
        public void ParallaxSpeed_SaturatesAtTheCap()
        {
            // THE POINT OF THE CAP: past it, accelerating changes the traffic
            // and the lane markings but leaves the scenery legible instead of
            // dragging it back into a blur.
            float atCruise = HighwayDriveControlMath.ParallaxSpeed(11f, 0.4f, 5.5f);
            float atFullThrottle = HighwayDriveControlMath.ParallaxSpeed(18f, 0.4f, 5.5f);
            Assert.AreEqual(4.4f, atCruise, Tolerance);
            Assert.AreEqual(5.5f, atFullThrottle, Tolerance);
            Assert.Less(atFullThrottle / atCruise, 18f / 11f,
                "Scenery must grow more slowly than player speed once capped.");
        }

        [Test]
        public void ParallaxSpeed_TiersStayOrdered_SceneryNeverOutrunsTheRoad()
        {
            // Roadside must always be slower than the road, and landmarks
            // slower than roadside, at every speed -- otherwise the layers
            // read as sliding apart rather than as depth.
            foreach (float speed in new[] { 2.5f, 6f, 11f, 14f, 18f })
            {
                float roadside = HighwayDriveControlMath.ParallaxSpeed(speed, 0.4f, 5.5f);
                float landmark = HighwayDriveControlMath.ParallaxSpeed(speed, 0.2f, 3f);
                Assert.Less(roadside, speed, $"Roadside outran the road at {speed}.");
                Assert.Less(landmark, roadside, $"Landmarks outran the roadside at {speed}.");
            }
        }

        [Test]
        public void ParallaxSpeed_ClampsFactorAboveOne()
        {
            // A tier moving faster than the road would read as the world
            // sliding apart, so a mis-set factor degrades to "same as road".
            Assert.AreEqual(11f, HighwayDriveControlMath.ParallaxSpeed(11f, 3f, 0f), Tolerance);
        }

        [Test]
        public void ParallaxSpeed_StoppedPlayer_StopsTheScenery()
        {
            Assert.AreEqual(0f, HighwayDriveControlMath.ParallaxSpeed(0f, 0.4f, 5.5f), Tolerance);
        }

        // ── Heart approach ───────────────────────────────────────────────

        [Test]
        public void HeartClosingSpeed_GivesTheRequestedApproachTime()
        {
            // 8 units of visible approach in 2.5s -- the participant gets the
            // same planning window every time, whatever the throttle is doing.
            float v = HighwayDriveControlMath.HeartClosingSpeed(8f, 2.5f);
            Assert.AreEqual(3.2f, v, Tolerance);
            Assert.AreEqual(2.5f, 8f / v, Tolerance);
        }

        [Test]
        public void HeartClosingSpeed_IsSlowerThanCruising()
        {
            // The whole complaint being fixed: a pickup closing at the vehicle
            // rate sweeps past in well under a second.
            float approach = HighwayDriveControlMath.HeartClosingSpeed(8f, 2.5f);
            Assert.Less(approach, 11f, "Heart must approach more slowly than the car drives.");
            Assert.Greater(8f / approach, 2f, "Approach window must exceed two seconds.");
        }

        [Test]
        public void HeartClosingSpeed_GuardsZeroApproachTime()
        {
            Assert.AreEqual(8f, HighwayDriveControlMath.HeartClosingSpeed(8f, 0f), Tolerance);
        }

        // ── High-speed streaks ───────────────────────────────────────────

        [Test]
        public void StreakStrength_IsZeroDuringNormalCruising()
        {
            // Streaks must be absent, not merely faint, below the onset --
            // they are the cue that says "you are clearly above neutral".
            Assert.AreEqual(0f, HighwayDriveControlMath.StreakStrength(0f, 0.45f), Tolerance);
            Assert.AreEqual(0f, HighwayDriveControlMath.StreakStrength(0.45f, 0.45f), Tolerance);
        }

        [Test]
        public void StreakStrength_RampsSmoothlyToFullAtTopSpeed()
        {
            Assert.AreEqual(1f, HighwayDriveControlMath.StreakStrength(1f, 0.45f), Tolerance);
            float mid = HighwayDriveControlMath.StreakStrength(0.725f, 0.45f);
            Assert.AreEqual(0.5f, mid, 0.01f);
        }

        // ── Collision predicate ──────────────────────────────────────────

        [Test]
        public void RectsOverlap_DetectsAGenuineOverlap()
        {
            Assert.IsTrue(HighwayDriveControlMath.RectsOverlap(
                0f, 0f, 1f, 2f,
                0.4f, 0.5f, 1f, 2f));
        }

        [Test]
        public void RectsOverlap_ExactlyTouchingEdges_IsNotAnOverlap()
        {
            // Strict inequality on purpose: a car in the next lane whose edge
            // exactly grazes yours must not end the run.
            Assert.IsFalse(HighwayDriveControlMath.RectsOverlap(
                0f, 0f, 1f, 2f,
                1f, 0f, 1f, 2f));
            Assert.IsFalse(HighwayDriveControlMath.RectsOverlap(
                0f, 0f, 1f, 2f,
                0f, 2f, 1f, 2f));
        }

        [Test]
        public void RectsOverlap_SeparatedOnOneAxisOnly_IsNotAnOverlap()
        {
            // Same lane, well apart along the road: overlaps in X, not in Y.
            Assert.IsFalse(HighwayDriveControlMath.RectsOverlap(
                0f, 0f, 1f, 2f,
                0f, 6f, 1f, 2f));

            // Alongside each other, different lanes: overlaps in Y, not in X.
            Assert.IsFalse(HighwayDriveControlMath.RectsOverlap(
                0f, 0f, 1f, 2f,
                3f, 0f, 1f, 2f));
        }

        // ── Player screen position ───────────────────────────────────────

        [Test]
        public void PlayerForwardWorldY_WithZeroShift_StaysAnchored()
        {
            // Shift 0 is the literal relative-motion model Simon described:
            // the car does not move up or down the screen at all.
            Assert.AreEqual(-2.4f, HighwayDriveControlMath.PlayerForwardWorldY(1f, -2.4f, 0f), Tolerance);
            Assert.AreEqual(-2.4f, HighwayDriveControlMath.PlayerForwardWorldY(-1f, -2.4f, 0f), Tolerance);
        }

        [Test]
        public void PlayerForwardWorldY_ShiftsBothWaysAboutTheAnchor()
        {
            Assert.AreEqual(-1.8f, HighwayDriveControlMath.PlayerForwardWorldY(1f, -2.4f, 0.6f), Tolerance);
            Assert.AreEqual(-3f, HighwayDriveControlMath.PlayerForwardWorldY(-1f, -2.4f, 0.6f), Tolerance);
            Assert.AreEqual(-2.4f, HighwayDriveControlMath.PlayerForwardWorldY(0f, -2.4f, 0.6f), Tolerance);
        }

        [Test]
        public void Clamp_BoundsBothDirections()
        {
            Assert.AreEqual(1f, HighwayDriveControlMath.Clamp(5f, -1f, 1f), Tolerance);
            Assert.AreEqual(-1f, HighwayDriveControlMath.Clamp(-5f, -1f, 1f), Tolerance);
            Assert.AreEqual(0.3f, HighwayDriveControlMath.Clamp(0.3f, -1f, 1f), Tolerance);
        }

        // ── Environment theme rotation ────────────────────────────────────

        [Test]
        public void ThemeIndexForDay_CyclesInCalendarOrder()
        {
            // Day 0 is Desert (index 0), then City, then Snow, then back to
            // Desert -- the exact sequence a therapist would see across four
            // consecutive days.
            Assert.AreEqual(0, HighwayDriveControlMath.ThemeIndexForDay(0, 3));
            Assert.AreEqual(1, HighwayDriveControlMath.ThemeIndexForDay(1, 3));
            Assert.AreEqual(2, HighwayDriveControlMath.ThemeIndexForDay(2, 3));
            Assert.AreEqual(0, HighwayDriveControlMath.ThemeIndexForDay(3, 3));
        }

        [Test]
        public void ThemeIndexForDay_SameDayAlwaysGivesTheSameTheme()
        {
            // The determinism property the whole "auto rotate daily" design
            // depends on: two sessions started on the same calendar day (e.g.
            // System.DateTime.Now.DayOfYear read twice, once per session) must
            // never disagree about which theme is active.
            int a = HighwayDriveControlMath.ThemeIndexForDay(214, 3);
            int b = HighwayDriveControlMath.ThemeIndexForDay(214, 3);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void ThemeIndexForDay_HandlesADecemberToJanuaryYearWrap()
        {
            // DayOfYear resets to 1 on New Year's Day -- there is nothing
            // special to do for that, but it is worth pinning that a
            // late-December day and an early-January day both produce a
            // valid, in-range index rather than anything caller code would
            // need to guard against.
            int lateDecember = HighwayDriveControlMath.ThemeIndexForDay(365, 3);
            int earlyJanuary = HighwayDriveControlMath.ThemeIndexForDay(1, 3);
            Assert.GreaterOrEqual(lateDecember, 0);
            Assert.Less(lateDecember, 3);
            Assert.GreaterOrEqual(earlyJanuary, 0);
            Assert.Less(earlyJanuary, 3);
        }

        [Test]
        public void ThemeIndexForDay_NeverThrowsOrGoesOutOfRangeForDegenerateInput()
        {
            // themeCount <= 0 and a negative dayOfYear are both malformed
            // inputs that should never occur (Enum.GetValues(...).Length is
            // always >= 1, and DateTime.DayOfYear is always >= 1), but this is
            // a day-of-year -> array-index function, so a bad result here
            // means an out-of-range enum cast at the call site -- worth a
            // guard, not just a documented precondition.
            Assert.AreEqual(0, HighwayDriveControlMath.ThemeIndexForDay(100, 0));
            int negative = HighwayDriveControlMath.ThemeIndexForDay(-5, 3);
            Assert.GreaterOrEqual(negative, 0);
            Assert.Less(negative, 3);
        }

        // ── Hearts ────────────────────────────────────────────────────────

        [Test]
        public void HeartsMaySpawn_FalseOnlyAtOrAboveTheCap()
        {
            // Simon's explicit requirement: no spawns at 5/5.
            Assert.IsFalse(HighwayDriveControlMath.HeartsMaySpawn(5, 5));
            Assert.IsTrue(HighwayDriveControlMath.HeartsMaySpawn(4, 5));
            Assert.IsTrue(HighwayDriveControlMath.HeartsMaySpawn(0, 5));
        }

        [Test]
        public void HeartsMaySpawn_AboveTheCap_IsStillFalse()
        {
            // Defensive: CurrentHearts should never exceed MaxHearts in
            // practice (HighwayDriveController clamps every gain), but this
            // must not flip back to true if it somehow did.
            Assert.IsFalse(HighwayDriveControlMath.HeartsMaySpawn(6, 5));
        }

        [Test]
        public void HeartSpawnInterval_AtZeroHearts_IsExactlyTheMinimum()
        {
            Assert.AreEqual(8f, HighwayDriveControlMath.HeartSpawnIntervalS(0, 5, 25f, 8f), Tolerance);
        }

        [Test]
        public void HeartSpawnInterval_OneBelowTheCap_IsExactlyTheBase()
        {
            // The least-urgent case HeartSpawnIntervalS is ever actually asked
            // about -- HeartsMaySpawn excludes currentHearts == maxHearts
            // entirely, so 4/5 is as close to "not urgent" as this function
            // sees in practice.
            Assert.AreEqual(25f, HighwayDriveControlMath.HeartSpawnIntervalS(4, 5, 25f, 8f), Tolerance);
        }

        [Test]
        public void HeartSpawnInterval_IsMonotonic_FewerHeartsNeverSpawnsSlower()
        {
            float prev = HighwayDriveControlMath.HeartSpawnIntervalS(5, 5, 25f, 8f);
            for (int hearts = 4; hearts >= 0; hearts--)
            {
                float interval = HighwayDriveControlMath.HeartSpawnIntervalS(hearts, 5, 25f, 8f);
                Assert.LessOrEqual(interval, prev,
                    $"Interval at {hearts} hearts ({interval}) is slower than at {hearts + 1} hearts ({prev}).");
                prev = interval;
            }
        }

        [Test]
        public void HeartSpawnInterval_DegenerateMaxHearts_ReturnsBaseRatherThanDividingByZero()
        {
            Assert.AreEqual(25f, HighwayDriveControlMath.HeartSpawnIntervalS(1, 0, 25f, 8f), Tolerance);
        }

        [Test]
        public void HeartSpawnInterval_MaxHeartsOfOne_AlwaysUsesTheMinimum()
        {
            // At a single-heart cap, the only non-full state (0 hearts) IS the
            // most urgent one -- there is no "one below the cap" to anchor
            // baseIntervalS to, so this must not divide by (maxHearts - 1 == 0).
            Assert.AreEqual(8f, HighwayDriveControlMath.HeartSpawnIntervalS(0, 1, 25f, 8f), Tolerance);
        }

        // ── Safe-time readout ────────────────────────────────────────────

        [Test]
        public void FormatElapsedClock_AtZero_IsZeroColonZeroZero()
        {
            Assert.AreEqual("0:00", HighwayDriveControlMath.FormatElapsedClock(0f));
        }

        [Test]
        public void FormatElapsedClock_PadsSecondsButNotMinutes()
        {
            // Seconds are always two digits (":07", not ":7"), but the leading
            // minutes field is never padded -- a stopwatch reads "1:05", not
            // "01:05", until it actually reaches double-digit minutes.
            Assert.AreEqual("1:05", HighwayDriveControlMath.FormatElapsedClock(65f));
            Assert.AreEqual("0:09", HighwayDriveControlMath.FormatElapsedClock(9f));
            Assert.AreEqual("12:03", HighwayDriveControlMath.FormatElapsedClock(723f));
        }

        [Test]
        public void FormatElapsedClock_Truncates_NeverRoundsUp()
        {
            // An elapsed-time readout must never visibly run ahead of the
            // actual elapsed time -- 59.9 seconds is still "0:59", not "1:00".
            Assert.AreEqual("0:59", HighwayDriveControlMath.FormatElapsedClock(59.9f));
            Assert.AreEqual("1:00", HighwayDriveControlMath.FormatElapsedClock(60f));
        }

        [Test]
        public void FormatElapsedClock_AddsAnHoursFieldPastOneHour()
        {
            Assert.AreEqual("1:00:00", HighwayDriveControlMath.FormatElapsedClock(3600f));
            Assert.AreEqual("1:02:03", HighwayDriveControlMath.FormatElapsedClock(3723f));
            // One second short of an hour: still no hours field at all (not
            // "0:59:59") -- the gate is hours > 0, matching the same "never
            // pad a leading zero field that isn't needed" rule the minutes
            // field already follows below one hour.
            Assert.AreEqual("59:59", HighwayDriveControlMath.FormatElapsedClock(3599f));
        }

        [Test]
        public void FormatElapsedClock_NegativeInput_FloorsAtZeroRatherThanShowingNegativeTime()
        {
            Assert.AreEqual("0:00", HighwayDriveControlMath.FormatElapsedClock(-5f));
        }
    }
}
