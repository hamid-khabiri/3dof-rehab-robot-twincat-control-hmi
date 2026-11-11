using System;

namespace FireflyGarden.Core
{
    /// <summary>
    /// Moments in play that feedback systems react to (sound, robot haptics). Gameplay raises them
    /// without knowing who listens, so audio and robot code stay separate from the gameplay rules.
    /// </summary>
    public static class GameplayEvents
    {
        /// <summary>The target was reached: the dwell on the correct flower completed (all levels).</summary>
        public static event Action CorrectFlowerReached;

        /// <summary>A wrong flower was selected: the dwell on a non-matching flower completed.</summary>
        public static event Action WrongFlowerSelected;

        /// <summary>Every trial of the level is done; the results pop-up is about to open.</summary>
        public static event Action LevelCompleted;

        public static void RaiseCorrectFlowerReached() => CorrectFlowerReached?.Invoke();
        public static void RaiseWrongFlowerSelected() => WrongFlowerSelected?.Invoke();
        public static void RaiseLevelCompleted() => LevelCompleted?.Invoke();
    }
}
