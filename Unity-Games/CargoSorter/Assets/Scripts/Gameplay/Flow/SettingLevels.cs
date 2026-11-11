public static class SettingLevels
{
    public const int MinSpeed = 1;
    public const int MaxSpeed = 5;

    // Bin weight = resistive force the robot applies: 1 = no resistance, 2 = some, 3 = most.
    public const int MinWeight = 1;
    public const int MaxWeight = 3;

    public static string SpeedName(int level)
    {
        switch (level)
        {
            case 1: return "Very Slow";
            case 2: return "Slow";
            case 3: return "Normal";
            case 4: return "Fast";
            default: return "Very Fast";
        }
    }

    public static string WeightName(int level)
    {
        switch (level)
        {
            case 1: return "Light";
            case 2: return "Medium";
            default: return "Heavy";
        }
    }
}
