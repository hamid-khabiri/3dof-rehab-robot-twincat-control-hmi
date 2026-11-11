namespace FireflyGarden.Core
{
    public enum SingleFlowerState
    {
        WaitingAtHome,
        InitialHomeDwell,
        RememberCueExposure,
        RememberMemoryDelay,
        ActiveReach,
        TargetDwell,
        SuccessFeedback,
        ReturnToHome,
        ReturnDwell,
        CycleComplete
    }
}
