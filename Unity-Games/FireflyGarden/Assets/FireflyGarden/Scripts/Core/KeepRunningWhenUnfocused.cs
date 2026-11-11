using UnityEngine;

namespace FireflyGarden.Core
{
    /// <summary>
    /// The therapist works on the TwinCAT HMI while the patient plays. Clicking the HMI takes focus
    /// away from the game window; without this Unity would freeze the game (firefly, timers, music,
    /// robot link) until the game window is clicked again. Also set in Player Settings
    /// (Run In Background); this covers the Editor's Play mode as well.
    /// </summary>
    public static class KeepRunningWhenUnfocused
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply() => Application.runInBackground = true;
    }
}
