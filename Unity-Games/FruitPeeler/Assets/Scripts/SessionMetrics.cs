using UnityEngine;

[System.Serializable]
public class SessionMetrics
{
    [SerializeField] private bool isActive;
    [SerializeField] private bool hasCompletedSession;
    [SerializeField] private int startingLevel;
    [SerializeField] private int finalLevelReached;
    [SerializeField] private int successfulPeels;
    [SerializeField] private int failedOrMissedAttempts;
    [SerializeField] private int completedSublevels;
    [SerializeField] private int expectedSublevels;
    [SerializeField] private float sessionStartTime;
    [SerializeField] private float sessionEndTime;
    [SerializeField] private float durationSeconds;

    public bool IsActive => isActive;
    public bool HasCompletedSession => hasCompletedSession;
    public int StartingLevel => startingLevel;
    public int FinalLevelReached => finalLevelReached;
    public int SuccessfulPeels => successfulPeels;
    public int FailedOrMissedAttempts => failedOrMissedAttempts;
    public int TotalAttempts => successfulPeels + failedOrMissedAttempts;
    public int CompletedSublevels => completedSublevels;
    public int ExpectedSublevels => expectedSublevels;
    public float SessionStartTime => sessionStartTime;
    public float SessionEndTime => sessionEndTime;
    public float DurationSeconds => isActive
        ? Mathf.Max(0f, Time.realtimeSinceStartup - sessionStartTime)
        : durationSeconds;

    public float PeelSuccessRate => TotalAttempts > 0
        ? (float)successfulPeels / TotalAttempts
        : 0f;

    public float SessionCompletionRate => expectedSublevels > 0
        ? (float)completedSublevels / expectedSublevels
        : 0f;

    public float MissRate => TotalAttempts > 0
        ? (float)failedOrMissedAttempts / TotalAttempts
        : 0f;

    public void StartSession(int selectedStartingLevel, int selectedExpectedSublevels, int configuredLevelCount, float startTime)
    {
        Reset();

        isActive = true;
        hasCompletedSession = false;
        startingLevel = ClampLevel(selectedStartingLevel, configuredLevelCount);
        finalLevelReached = startingLevel;
        expectedSublevels = Mathf.Max(0, selectedExpectedSublevels);
        sessionStartTime = startTime;
        sessionEndTime = 0f;
        durationSeconds = 0f;
    }

    public void RecordSuccessfulPeel()
    {
        if (!isActive) return;
        successfulPeels++;
    }

    public void RecordFailedOrMissedAttempt()
    {
        if (!isActive) return;
        failedOrMissedAttempts++;
    }

    public void RecordCompletedSublevel()
    {
        if (!isActive) return;
        completedSublevels++;
    }

    public void UpdateFinalLevelReached(int level, int configuredLevelCount)
    {
        if (!isActive && !hasCompletedSession) return;
        finalLevelReached = ClampLevel(level, configuredLevelCount);
    }

    public void EndSession(float endTime)
    {
        if (!isActive) return;

        sessionEndTime = endTime;
        durationSeconds = Mathf.Max(0f, sessionEndTime - sessionStartTime);
        isActive = false;
        hasCompletedSession = true;
    }

    public void Reset()
    {
        isActive = false;
        hasCompletedSession = false;
        startingLevel = 0;
        finalLevelReached = 0;
        successfulPeels = 0;
        failedOrMissedAttempts = 0;
        completedSublevels = 0;
        expectedSublevels = 0;
        sessionStartTime = 0f;
        sessionEndTime = 0f;
        durationSeconds = 0f;
    }

    private int ClampLevel(int level, int configuredLevelCount)
    {
        if (configuredLevelCount <= 0) return 0;
        return Mathf.Clamp(level, 1, configuredLevelCount);
    }
}
