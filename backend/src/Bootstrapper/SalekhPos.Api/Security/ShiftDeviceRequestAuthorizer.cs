using SalekhPos.Devices.Application.Devices;
using SalekhPos.ShiftManagement.Application.Shifts;

namespace SalekhPos.Api.Security;

public sealed class ShiftDeviceRequestAuthorizer(IDeviceRequestProofVerifier verifier) : IShiftDeviceRequestAuthorizer
{
    public Task VerifyAsync(ShiftDeviceRequestProofContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return verifier.VerifyAsync(new DeviceRequestProofContext(
            new DeviceIdentity(context.Identity.Issuer, context.Identity.Subject),
            context.OrganizationId, context.BranchId, context.DeviceId, context.Method, context.CanonicalPath,
            context.OperationIdentity, context.BodyDigest, new DeviceRequestProofHeaders(
                context.Headers.CredentialId, context.Headers.Timestamp, context.Headers.Nonce, context.Headers.Signature),
            context.RegisterId, RequireCredential: true), cancellationToken);
    }
}
