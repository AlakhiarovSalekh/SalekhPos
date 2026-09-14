using Microsoft.Extensions.Options;

namespace SalekhPos.Worker.Schedulers;

public sealed class WorkerOptionsValidator : IValidateOptions<WorkerOptions>
{
    private static readonly TimeSpan MaximumSupportedJobTimeout = TimeSpan.FromDays(1);
    private static readonly TimeSpan MaximumSupportedRetryDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaximumSupportedDrainTimeout = TimeSpan.FromMinutes(10);

    public ValidateOptionsResult Validate(string? name, WorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        AddIfOutside(options.QueueCapacity, 1, 100_000, nameof(options.QueueCapacity), failures);
        AddIfOutside(options.MaxConcurrency, 1, 256, nameof(options.MaxConcurrency), failures);
        AddIfOutside(options.RetryMaxAttempts, 1, 10, nameof(options.RetryMaxAttempts), failures);

        if (options.DefaultJobTimeout <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(options.DefaultJobTimeout)} must be positive.");
        }

        if (options.MaximumJobTimeout < options.DefaultJobTimeout)
        {
            failures.Add($"{nameof(options.MaximumJobTimeout)} must be at least {nameof(options.DefaultJobTimeout)}.");
        }

        if (options.MaximumJobTimeout > MaximumSupportedJobTimeout)
        {
            failures.Add($"{nameof(options.MaximumJobTimeout)} cannot exceed {MaximumSupportedJobTimeout}.");
        }

        if (options.RetryBaseDelay < TimeSpan.Zero)
        {
            failures.Add($"{nameof(options.RetryBaseDelay)} cannot be negative.");
        }

        if (options.RetryMaxDelay < options.RetryBaseDelay)
        {
            failures.Add($"{nameof(options.RetryMaxDelay)} must be at least {nameof(options.RetryBaseDelay)}.");
        }

        if (options.RetryMaxDelay > MaximumSupportedRetryDelay)
        {
            failures.Add($"{nameof(options.RetryMaxDelay)} cannot exceed {MaximumSupportedRetryDelay}.");
        }

        if (options.RetryJitterRatio is < 0 or > 1)
        {
            failures.Add($"{nameof(options.RetryJitterRatio)} must be between 0 and 1.");
        }

        if (options.ShutdownDrainTimeout <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(options.ShutdownDrainTimeout)} must be positive.");
        }

        else if (options.ShutdownDrainTimeout > MaximumSupportedDrainTimeout)
        {
            failures.Add($"{nameof(options.ShutdownDrainTimeout)} cannot exceed {MaximumSupportedDrainTimeout}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddIfOutside(int value, int minimum, int maximum, string property, List<string> failures)
    {
        if (value < minimum || value > maximum)
        {
            failures.Add($"{property} must be between {minimum} and {maximum}.");
        }
    }
}
