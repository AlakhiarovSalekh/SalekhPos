namespace SalekhPos.Worker.Jobs;

public interface IBackgroundJob<in TPayload>
{
    ValueTask ExecuteAsync(
        TPayload payload,
        BackgroundJobContext context,
        CancellationToken cancellationToken);
}
