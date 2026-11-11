using System;

namespace FireflyGarden.Robot
{
    /// <summary>
    /// UI actions the robot may react to. UI code raises them without knowing whether a robot is
    /// connected; the TwinCAT connector listens. In-play events (e.g. wrong flower) come from
    /// <see cref="FireflyGarden.Core.GameplayEvents"/>.
    /// </summary>
    public static class RobotOutputSignals
    {
        /// <summary>An Exit button was pressed (gameplay "Exit to Main Menu" or main menu "Exit Game").</summary>
        public static event Action ExitPressed;

        public static void RaiseExitPressed() => ExitPressed?.Invoke();
    }
}
