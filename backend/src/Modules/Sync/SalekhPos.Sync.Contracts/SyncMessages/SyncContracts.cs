namespace SalekhPos.Sync.Contracts.SyncMessages;

public sealed record IngestSyncMessageRequest(Guid MessageId, long Sequence, int ProtocolVersion, string MessageType, string Payload);
public sealed record SyncAcknowledgementResponse(Guid MessageId, Guid DeviceId, Guid? SaleId, long Sequence, int ProtocolVersion,
    string MessageType, string Status, string ResultCode, string PayloadDigest, DateTimeOffset AcceptedAt, bool Replay);
public sealed record SyncCheckpointResponse(Guid DeviceId, long LastAcceptedSequence, DateTimeOffset? LastAcceptedAt);
public sealed record SyncMessageResponse(Guid MessageId, Guid DeviceId, Guid? SaleId, long Sequence, int ProtocolVersion,
    string MessageType, string Status, string ResultCode, string PayloadDigest, DateTimeOffset AcceptedAt);
public sealed record SyncMessageHistoryResponse(IReadOnlyList<SyncMessageResponse> Items, long? NextAfterSequence);
