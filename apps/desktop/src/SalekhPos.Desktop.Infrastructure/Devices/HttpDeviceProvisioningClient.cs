using System.Net.Http.Json;
using System.Text.Json;
using SalekhPos.Desktop.Application.Devices;

namespace SalekhPos.Desktop.Infrastructure.Devices;

public sealed class HttpDeviceProvisioningClient(HttpClient client) : IDeviceProvisioningClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ProvisionedDevice> RegisterAsync(DeviceProvisioningRequest request, Guid operationId,
        string publicKey, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post,
            $"api/v1/organizations/{request.OrganizationId:D}/branches/{request.BranchId:D}/devices");
        message.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        message.Content = JsonContent.Create(new
        {
            request.RegisterId,
            request.Code,
            request.Name,
            request.Platform,
            request.SyncProtocolVersion,
            PublicKey = publicKey,
        });
        return await SendAsync(message, cancellationToken);
    }

    public async Task<ProvisionedDevice> TrustAsync(Guid organizationId, Guid branchId, Guid deviceId,
        Guid operationId, Guid credentialId, string proofChallenge, string signature,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post,
            $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/devices/{deviceId:D}/trust");
        message.Headers.Add("Idempotency-Key", operationId.ToString("D"));
        message.Content = JsonContent.Create(new
        {
            CredentialId = credentialId,
            ProofChallenge = proofChallenge,
            Signature = signature
        });
        return await SendAsync(message, cancellationToken);
    }

    private async Task<ProvisionedDevice> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProvisionedDevice>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The device provisioning response is empty.");
    }
}
