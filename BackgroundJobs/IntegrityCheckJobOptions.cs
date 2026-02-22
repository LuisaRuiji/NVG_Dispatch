namespace NVGInventory.BackgroundJobs;

public sealed class IntegrityCheckJobOptions
{
    public bool Enabled { get; set; } = false;
    public int IntervalMinutes { get; set; } = 60;
}
