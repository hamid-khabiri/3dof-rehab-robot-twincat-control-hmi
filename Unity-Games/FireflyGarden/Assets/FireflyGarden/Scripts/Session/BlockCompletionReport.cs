using System;
using FireflyGarden.Configuration;

namespace FireflyGarden.Session
{
    [Serializable]
    public sealed class BlockCompletionReport
    {
        public string blockDefinitionId;
        public GameMode gameMode;
        public int formalTrialsCompleted;
        public int successfulTrials;
        public int incompleteTimeouts;
        public int abortedAttempts;
        public bool blockCompletedNormally;
        public int scheduleIndex;
    }
}
