public sealed class BlockSummary
{
    public GameMode GameMode { get; }
    public int TotalTrials { get; }
    public int Correct { get; }
    public int WrongBin { get; }
    public int Misses { get; }
    public int CompletedTrials => Correct + WrongBin + Misses;
    public float Accuracy => CompletedTrials > 0 ? (float)Correct / CompletedTrials * 100f : 0f;

    public BlockSummary(GameMode gameMode, int totalTrials, int correct, int wrongBin, int misses)
    {
        GameMode = gameMode;
        TotalTrials = totalTrials;
        Correct = correct;
        WrongBin = wrongBin;
        Misses = misses;
    }
}
