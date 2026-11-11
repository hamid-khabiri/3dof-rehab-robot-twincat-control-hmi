using System.Collections.Generic;

namespace RehabFramework.HighwayDrive
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE PATIENT-VISIBLE STRING CATALOGUE
    //  Every string HighwayDriveInstructionUI can ever display, factored into
    //  its own plain-C# file (no UnityEngine dependency) SPECIFICALLY so the
    //  digit-free/ASCII-only test can reference it from a separate asmdef --
    //  same reasoning, same constraint, as RallyPaddle's
    //  Core/RallyPaddlePatientStrings.cs and ClearTheView's
    //  PatientStrings/PatientVisibleStrings.cs. (Assembly-CSharp, where
    //  HighwayDriveInstructionUI.cs itself lives, compiles last and can't be
    //  referenced BY NAME from a custom asmdef, so this dependency-free slice
    //  gets its own tiny asmdef instead of moving the whole HighwayDrive
    //  folder into one.)
    //
    //  CLAUDE.md rules that apply to every const below, without exception:
    //  no digits, ever, in any patient-visible string; ASCII only (no
    //  em-dashes, no ellipsis characters, no smart quotes). Both are checked
    //  by Tests/HighwayDriveDigitFreeTests.cs -- but only for strings that
    //  actually appear in AllPatientVisibleStrings() below, so anything added
    //  here must be added to that method too or the test simply won't see it.
    // ─────────────────────────────────────────────────────────────────────────

    public static class HighwayDrivePatientStrings
    {
        /// <summary>Shown in the brief countdown-free settling moment before
        /// traffic starts arriving at the top of a run.</summary>
        public const string GetReady = "Get ready to drive";

        /// <summary>The standing instruction while a run is live.</summary>
        public const string KeepClear = "Keep clear of the other cars";

        /// <summary>Milestone feedback, fired as distance lamps light up.
        /// Deliberately about steadiness rather than speed or distance --
        /// there is no score in this instrument and no numeric readout.</summary>
        public const string SteadyDriving = "Nice steady driving";

        /// <summary>Fired when a heart pickup actually raises the heart count
        /// (HighwayDriveController.CheckHeartPickup) -- not shown if the
        /// pickup was collected at an already-full heart count, since nothing
        /// was actually gained.</summary>
        public const string ExtraHeart = "Extra heart";

        /// <summary>Shown the moment a collision happens, alongside the centre
        /// target. Recovery is gated on the patient ACTUALLY RETURNING TO
        /// NEUTRAL, not on waiting out a timer, so the message is an
        /// instruction rather than a reassurance -- it names the one thing
        /// that will resume the run. This replaced a passive
        /// "Take a moment, the road starts again soon" (Simon, 2026-08-24),
        /// which told the patient to do nothing during what is actually an
        /// active recentering task.</summary>
        public const string CollisionReturnToCenter = "Collision - Return to center";

        /// <summary>Shown once neutral has been held long enough, just before
        /// traffic resumes. Brief by design.</summary>
        public const string Ready = "Ready";

        /// <summary>Selective praise, fired only after a sustained stretch of
        /// controlled driving and faded shortly after. Permanently-displayed
        /// praise is decoration; this is feedback.</summary>
        public const string NiceControl = "Nice control!";

        /// <summary>Crazy Car praise, shown after a stretch of clean driving
        /// (HighwayDriveConfig.MotivationalPraise). The group follows how long
        /// the patient has been driving, so the words grow with the effort:
        /// group one in the first minute, group two in the second, group three
        /// from then on. One sentence is picked at random, never the same one
        /// twice in a row.</summary>
        public static readonly string[] PraiseGroup1 =
        {
            NiceControl,
            "Good start!",
            "Smooth driving!",
            "You are doing well!",
        };

        public static readonly string[] PraiseGroup2 =
        {
            "Great focus!",
            "Steady hands, great job!",
            "You are getting stronger!",
        };

        public static readonly string[] PraiseGroup3 =
        {
            "Excellent driving!",
            "Amazing control, keep going!",
            "You are a champion driver!",
            "Fantastic, you are on fire!",
        };

        /// <summary>Crazy Car: shown briefly each time the speed ramp steps
        /// the cruise speed up, so the change is announced, not a surprise.</summary>
        public const string SpeedingUp = "Speeding up!";

        /// <summary>Shown when the fixed-duration assessment finishes, after
        /// all gameplay has already been stopped.</summary>
        public const string AssessmentComplete = "Assessment complete - Great job!";

        /// <summary>Legacy timed-restart copy, kept only for the fallback path
        /// where HighwayDriveConfig.AutoRestartSeconds is positive (neutral
        /// gating unreachable for a given patient).</summary>
        public const string CrashedRestartingSoon = "Take a moment. The road starts again soon.";

        /// <summary>Legacy: run resumes only on an explicit therapist action.</summary>
        public const string CrashedWaitingForTherapist = "Take a moment. Your therapist will start the next drive.";

        /// <summary>Shown while the robot controller reports itself not awake,
        /// which is the ONLY thing that can hold a run in this instrument --
        /// there is no self-pausing safety heuristic (Simon's call,
        /// 2026-08-17). Wording matched to Rally Paddle's equivalent so a
        /// patient who has used both instruments sees the same calm,
        /// non-alarming sentence.</summary>
        public const string Paused = "Paused. Please wait for your therapist.";

        /// <summary>Shown once the therapist ends the open-ended session.</summary>
        public const string SessionFinished = "Well done. That is the end of the drive.";

        /// <summary>Every string this catalogue can produce, for the
        /// digit-free/ASCII-only test to walk. Kept in sync by hand with the
        /// consts above -- deliberately not reflection-based, so the test
        /// stays simple and doesn't need Play Mode or a GameObject.</summary>
        public static IEnumerable<string> AllPatientVisibleStrings()
        {
            yield return GetReady;
            yield return KeepClear;
            yield return SteadyDriving;
            yield return ExtraHeart;
            yield return CollisionReturnToCenter;
            yield return Ready;
            yield return NiceControl;
            yield return SpeedingUp;
            foreach (string s in PraiseGroup1) yield return s;
            foreach (string s in PraiseGroup2) yield return s;
            foreach (string s in PraiseGroup3) yield return s;
            yield return AssessmentComplete;
            yield return CrashedRestartingSoon;
            yield return CrashedWaitingForTherapist;
            yield return Paused;
            yield return SessionFinished;
        }
    }
}
