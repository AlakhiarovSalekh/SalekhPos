using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Offline;

namespace SalekhPos.Desktop.Infrastructure.Sync;

public sealed class HttpRemoteSyncTransport(HttpClient client) : IRemoteSyncTransport
{
    public async Task<RemoteSyncAcknowledgement> SendAsync(Guid organizationId, Guid branchId,
        LocalOutboxMessage message, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Sync scope is required.");

        var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/devices/{message.DeviceId:D}/sync/messages";
        using var response = await client.PostAsJsonAsync(path, new
        {
            message.MessageId,
            message.Sequence,
            protocolVersion = 1,
            message.MessageType,
            message.Payload,
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoteSyncAcknowledgement>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The sync acknowledgement body is empty.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
