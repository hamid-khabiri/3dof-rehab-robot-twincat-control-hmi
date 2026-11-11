using System;
using UnityEngine;

[Serializable]
public sealed class SpeedLevelSettings
{
    [SerializeField, Range(1, 5)] private int currentSpeedLevel = 3;
    // Seconds a product takes to travel the belt. Each level is 20% of Normal's speed away from its neighbour:
    // 60%, 80%, 100% (Normal, 7.5 s), 120%, 140%  ->  duration = 7.5 / speed factor.
    [SerializeField, Min(0.01f)] private float level1Duration = 12.5f;
    [SerializeField, Min(0.01f)] private float level2Duration = 9.375f;
    [SerializeField, Min(0.01f)] private float level3Duration = 7.5f;
    [SerializeField, Min(0.01f)] private float level4Duration = 6.25f;
    [SerializeField, Min(0.01f)] private float level5Duration = 5.357f;

    public int CurrentSpeedLevel => Mathf.Clamp(currentSpeedLevel, 1, 5);

    public void SetLevel(int level) { currentSpeedLevel = Mathf.Clamp(level, 1, 5); }

    public float GetSelectedDuration()
    {
        switch (CurrentSpeedLevel)
        {
            case 1: return level1Duration;
            case 2: return level2Duration;
            case 3: return level3Duration;
            case 4: return level4Duration;
            default: return level5Duration;
        }
    }

    public void Validate()
    {
        currentSpeedLevel = Mathf.Clamp(currentSpeedLevel, 1, 5);
        level1Duration = Mathf.Max(0.01f, level1Duration);
        level2Duration = Mathf.Max(0.01f, level2Duration);
        level3Duration = Mathf.Max(0.01f, level3Duration);
        level4Duration = Mathf.Max(0.01f, level4Duration);
        level5Duration = Mathf.Max(0.01f, level5Duration);
    }
}
