using System;

namespace FireflyGarden.Session
{
    [Serializable]
    public sealed class SessionRuntimeSummary
    {
        public string sessionDefinitionId;
        public string sessionDisplayName;
        public int configuredBlockCount;
        public int blocksCompleted;
        public int trialsCompleted;
        public int successfulTrials;
        public int incompleteTimeouts;
        public int abortedAttempts;
        public bool endedNormally;
        public bool endedEarly;

        public SessionRuntimeSummary Copy() => (SessionRuntimeSummary)MemberwiseClone();
        public void Add(BlockCompletionReport report)
        {
            if (report == null || !report.blockCompletedNormally) return;
            blocksCompleted++;
            trialsCompleted += report.formalTrialsCompleted;
            successfulTrials += report.successfulTrials;
            incompleteTimeouts += report.incompleteTimeouts;
            abortedAttempts += report.abortedAttempts;
        }
    }
}
