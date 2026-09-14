namespace SalekhPos.Worker.Schedulers;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public int QueueCapacity { get; set; } = 256;

    public int MaxConcurrency { get; set; } = 4;

    public TimeSpan DefaultJobTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan MaximumJobTimeout { get; set; } = TimeSpan.FromMinutes(15);

    public int RetryMaxAttempts { get; set; } = 3;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(10);

    public double RetryJitterRatio { get; set; } = 0.2;

    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
