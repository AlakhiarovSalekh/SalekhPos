using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Application.Offline;

namespace SalekhPos.Desktop.Infrastructure.Sync;

public sealed class HttpRemoteSyncTransport(HttpClient client, IDeviceRequestProofSigner proofSigner)
    : IRemoteSyncTransport
{
    public async Task<RemoteSyncAcknowledgement> SendAsync(Guid organizationId, Guid branchId,
        LocalOutboxMessage message, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Sync scope is required.");

        var path = $"/api/v1/organizations/{organizationId:D}/branches/{branchId:D}/devices/{message.DeviceId:D}/sync/messages";
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            message.MessageId,
            message.Sequence,
            protocolVersion = 1,
            message.MessageType,
            message.Payload,
        }, JsonOptions);
        var proof = await proofSigner.SignSyncMessageAsync(organizationId, branchId, message.DeviceId,
            message.MessageId, path, body, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-SalekhPos-Device-Credential", proof.CredentialId);
        request.Headers.Add("X-SalekhPos-Device-Timestamp", proof.Timestamp);
        request.Headers.Add("X-SalekhPos-Device-Nonce", proof.Nonce);
        request.Headers.Add("X-SalekhPos-Device-Signature", proof.Signature);
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RemoteSyncAcknowledgement>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The sync acknowledgement body is empty.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
