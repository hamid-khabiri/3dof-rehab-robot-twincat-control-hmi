using FireflyGarden.Configuration;

namespace FireflyGarden.Session
{
    public static class SessionLaunchContext
    {
        public static SessionDefinition SelectedSession { get; private set; }
        public static void Select(SessionDefinition definition) => SelectedSession = definition;
        public static SessionDefinition Consume()
        {
            SessionDefinition selected = SelectedSession;
            SelectedSession = null;
            return selected;
        }
        public static void Clear() => SelectedSession = null;
    }
}
