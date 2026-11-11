using FireflyGarden.Configuration;
using FireflyGarden.Session;

namespace FireflyGarden.Results
{
    public static class SessionResultsContext
    {
        private static SessionRuntimeSummary snapshot;

        /// <summary>The level that produced the stored summary, used for Play Again / Next Level.</summary>
        public static SessionDefinition CompletedSession { get; private set; }

        public static void Store(SessionRuntimeSummary value, SessionDefinition completedSession = null)
        {
            snapshot = value?.Copy();
            CompletedSession = completedSession;
        }

        public static SessionRuntimeSummary Read() => snapshot?.Copy() ?? new SessionRuntimeSummary();

        public static void Clear()
        {
            snapshot = null;
            CompletedSession = null;
        }
    }
}
