using SalekhPos.Desktop.Domain.LocalSales;

namespace SalekhPos.Desktop.Application.Offline;

public sealed record CompleteLocalSaleCommand(Guid OrganizationId, Guid BranchId, Guid DeviceId, Guid SaleId,
    Guid ShiftId, Guid RegisterId, DateTimeOffset CompletedAt, decimal CashReceived, IReadOnlyList<LocalSaleLine> Lines);
public sealed record LocalOutboxMessage(Guid MessageId, Guid SaleId, Guid DeviceId, long Sequence, string MessageType,
    string Payload, string PayloadDigest, string Status, string? ResultCode, DateTimeOffset CreatedAt);
public sealed record LocalSaleWriteResult(LocalSale Sale, LocalOutboxMessage Message, bool Created);
public interface ILocalSaleStore
{
    Task<LocalSaleWriteResult> CompleteAsync(CompleteLocalSaleCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocalOutboxMessage>> ReadPendingAsync(Guid deviceId, int limit, CancellationToken cancellationToken);
    Task MarkResultAsync(Guid messageId, string payloadDigest, string status, string resultCode, DateTimeOffset acceptedAt, CancellationToken cancellationToken);
}

public sealed record RemoteSyncAcknowledgement(Guid MessageId, Guid DeviceId, Guid? SaleId, long Sequence,
    int ProtocolVersion, string MessageType, string Status, string ResultCode, string PayloadDigest,
    DateTimeOffset AcceptedAt, bool Replay);

public interface IRemoteSyncTransport
{
    Task<RemoteSyncAcknowledgement> SendAsync(Guid organizationId, Guid branchId, LocalOutboxMessage message,
        CancellationToken cancellationToken);
}

public interface IAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}

public interface ISyncRetryDelay
{
    Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class PendingSaleSyncDispatcher(ILocalSaleStore store, IRemoteSyncTransport transport)
{
    public async Task<int> DispatchAsync(Guid organizationId, Guid branchId, Guid deviceId, int limit,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || deviceId == Guid.Empty)
            throw new ArgumentException("Sync scope is required.");

        var pending = await store.ReadPendingAsync(deviceId, limit, cancellationToken);
        var completed = 0;
        foreach (var message in pending)
        {
            var acknowledgement = await transport.SendAsync(organizationId, branchId, message, cancellationToken);
            Validate(message, acknowledgement);
            await store.MarkResultAsync(message.MessageId, message.PayloadDigest, acknowledgement.Status,
                acknowledgement.ResultCode, acknowledgement.AcceptedAt, cancellationToken);
            completed++;
        }

        return completed;
    }

    private static void Validate(LocalOutboxMessage message, RemoteSyncAcknowledgement acknowledgement)
    {
        if (acknowledgement.MessageId != message.MessageId || acknowledgement.DeviceId != message.DeviceId
            || acknowledgement.SaleId != message.SaleId || acknowledgement.Sequence != message.Sequence
            || acknowledgement.ProtocolVersion != 1 || acknowledgement.MessageType != message.MessageType
            || !string.Equals(acknowledgement.PayloadDigest, message.PayloadDigest, StringComparison.OrdinalIgnoreCase)
            || acknowledgement.Status is not ("applied" or "rejected")
            || (acknowledgement.Status == "applied") != (acknowledgement.ResultCode == "applied")
            || acknowledgement.ResultCode is not ("applied" or "shift_conflict" or "price_conflict"
                or "insufficient_stock" or "sale_conflict")
            || acknowledgement.AcceptedAt == default || acknowledgement.AcceptedAt.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("The sync acknowledgement does not match the pending message.");
    }
}

public sealed class PendingSaleSyncRunner(PendingSaleSyncDispatcher dispatcher, ISyncRetryDelay delay)
{
    public async Task<int> RunAsync(Guid organizationId, Guid branchId, Guid deviceId, int batchSize,
        int maximumAttempts, TimeSpan initialDelay, CancellationToken cancellationToken)
    {
        if (maximumAttempts is < 1 or > 6 || initialDelay < TimeSpan.FromMilliseconds(100)
            || initialDelay > TimeSpan.FromSeconds(30))
            throw new ArgumentException("Sync retry policy is invalid.");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await dispatcher.DispatchAsync(organizationId, branchId, deviceId, batchSize,
                    cancellationToken);
            }
            catch (HttpRequestException exception) when (attempt < maximumAttempts && IsTransient(exception))
            {
                var backoff = TimeSpan.FromMilliseconds(Math.Min(initialDelay.TotalMilliseconds
                    * Math.Pow(2, attempt - 1), 30_000));
                await delay.WaitAsync(backoff, cancellationToken);
            }
        }
    }

    private static bool IsTransient(HttpRequestException exception) => exception.StatusCode is null
        or System.Net.HttpStatusCode.RequestTimeout
        or System.Net.HttpStatusCode.TooManyRequests
        || (int)exception.StatusCode >= 500;
}
