using System.Linq;
using NUnit.Framework;
using RehabFramework.HighwayDrive;

namespace RehabFramework.HighwayDrive.Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    //  HIGHWAY DRIVE PATIENT-STRING RULES (CLAUDE.md: "No digits, ever, in any
    //  patient-visible string" AND, separately, "UI strings are ASCII-only")
    //  Walks every string HighwayDriveInstructionUI can ever display and checks
    //  both rules. Same shape as RallyPaddleDigitFreeTests.
    //
    //  EditMode only -- checks the static catalogue
    //  HighwayDrivePatientStrings.AllPatientVisibleStrings() exposes, not live
    //  TMP_Text components, so this runs instantly without Play Mode or a
    //  GameObject.
    //
    //  NOTE ON COVERAGE, so this is not mistaken for more than it is: this
    //  proves the CATALOGUE is clean, not that the UI only ever draws from the
    //  catalogue. HighwayDriveInstructionUI.ShowFeedback takes an arbitrary
    //  string, and its own doc comment carries that caveat. Every current
    //  caller passes a const from here or the empty string.
    // ─────────────────────────────────────────────────────────────────────────
    public class HighwayDriveDigitFreeTests
    {
        [Test]
        public void AllPatientVisibleStrings_ContainNoDigits()
        {
            foreach (string s in HighwayDrivePatientStrings.AllPatientVisibleStrings())
            {
                Assert.IsFalse(ContainsDigit(s),
                    $"Patient-visible string contains a digit, which is never allowed in the patient view: \"{s}\"");
            }
        }

        [Test]
        public void AllPatientVisibleStrings_AreAsciiOnly()
        {
            foreach (string s in HighwayDrivePatientStrings.AllPatientVisibleStrings())
            {
                foreach (char c in s)
                {
                    Assert.Less((int)c, 128,
                        $"Patient-visible string contains a non-ASCII character (U+{(int)c:X4}): \"{s}\"");
                }
            }
        }

        [Test]
        public void AllPatientVisibleStrings_AreNotEmpty()
        {
            // A blank string sneaking into the catalogue would trivially "pass"
            // both checks above while actually being a real bug -- guard against
            // the test being gamed by an empty/blank entry.
            Assert.IsTrue(HighwayDrivePatientStrings.AllPatientVisibleStrings().Any(),
                "HighwayDrivePatientStrings.AllPatientVisibleStrings() returned nothing -- catalogue is empty.");
            foreach (string s in HighwayDrivePatientStrings.AllPatientVisibleStrings())
                Assert.IsFalse(string.IsNullOrWhiteSpace(s), "Patient-visible string catalogue contains a blank entry.");
        }

        [Test]
        public void BothCrashMessages_AreInTheCatalogue()
        {
            // HighwayDriveInstructionUI.ShowCrashed picks between these two at
            // runtime based on whether the run auto-restarts. It would be easy
            // to add a third branch later and display a string this test never
            // sees, so pin the two that exist now.
            var all = HighwayDrivePatientStrings.AllPatientVisibleStrings().ToList();
            CollectionAssert.Contains(all, HighwayDrivePatientStrings.CrashedRestartingSoon);
            CollectionAssert.Contains(all, HighwayDrivePatientStrings.CrashedWaitingForTherapist);
        }

        private static bool ContainsDigit(string s)
        {
            foreach (char c in s)
                if (c >= '0' && c <= '9') return true;
            return false;
        }
    }
}
