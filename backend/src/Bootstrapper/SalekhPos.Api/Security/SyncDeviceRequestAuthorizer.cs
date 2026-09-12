using SalekhPos.Devices.Application.Devices;
using SalekhPos.Sync.Application.SyncMessages;

namespace SalekhPos.Api.Security;

public sealed class SyncDeviceRequestAuthorizer(IDeviceRequestProofVerifier verifier) : ISyncDeviceRequestAuthorizer
{
    public Task VerifyAsync(SyncDeviceRequestProofContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return verifier.VerifyAsync(new DeviceRequestProofContext(
            new DeviceIdentity(context.Identity.Issuer, context.Identity.Subject),
            context.OrganizationId, context.BranchId, context.DeviceId, context.Method, context.CanonicalPath,
            context.OperationIdentity, context.BodyDigest, new DeviceRequestProofHeaders(
                context.Headers.CredentialId, context.Headers.Timestamp, context.Headers.Nonce, context.Headers.Signature)),
            cancellationToken);
    }
}
