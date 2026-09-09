using System.Text.Json;
namespace SalekhPos.Sync.Domain.SyncMessages;

public sealed record SyncEnvelope
{
    public SyncEnvelope(Guid messageId, Guid deviceId, long sequence, int protocolVersion, string messageType, string payload)
    {
        if (messageId == Guid.Empty || deviceId == Guid.Empty || sequence < 1) throw new ArgumentException("Sync envelope identity is invalid.");
        if (protocolVersion != 1) throw new ArgumentException("Sync protocol version is unsupported.");
        if (messageType is not "sale.completed.v1") throw new ArgumentException("Sync message type is unsupported.");
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > 49152) throw new ArgumentException("Sync payload is invalid.");
        try { using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 }); if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Sync payload is invalid."); }
        catch (JsonException exception) { throw new ArgumentException("Sync payload is invalid.", exception); }
        MessageId = messageId; DeviceId = deviceId; Sequence = sequence; ProtocolVersion = protocolVersion; MessageType = messageType; Payload = payload;
    }
    public Guid MessageId { get; }
    public Guid DeviceId { get; }
    public long Sequence { get; }
    public int ProtocolVersion { get; }
    public string MessageType { get; }
    public string Payload { get; }
}
