using SalekhPos.Sync.Contracts.SyncMessages;
namespace SalekhPos.Sync.Application.SyncMessages;

public sealed record SyncIdentity(string Issuer, string Subject);
public sealed record IngestSyncMessageCommand(Guid OrganizationId, Guid BranchId, Guid DeviceId, Guid MessageId,
    long Sequence, int ProtocolVersion, string MessageType, string Payload);
public interface ISyncIngestion
{
    Task<SyncAcknowledgementResponse> IngestAsync(SyncIdentity identity, IngestSyncMessageCommand command, CancellationToken cancellationToken);
    Task<SyncCheckpointResponse?> ReadCheckpointAsync(SyncIdentity identity, Guid organizationId, Guid branchId, Guid deviceId, CancellationToken cancellationToken);
    Task<SyncMessageResponse?> ReadMessageAsync(SyncIdentity identity, Guid organizationId, Guid branchId, Guid deviceId, Guid messageId, CancellationToken cancellationToken);
    Task<SyncMessageHistoryResponse> ReadHistoryAsync(SyncIdentity identity, Guid organizationId, Guid branchId, Guid deviceId, int pageSize, long? afterSequence, CancellationToken cancellationToken);
}
public sealed class SyncDeniedException : Exception;
public sealed class SyncConflictException : Exception;
public sealed class SyncUnavailableException : Exception;
