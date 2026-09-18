using SalekhPos.Devices.Application.Devices;
using SalekhPos.Sales.Application.CompleteSale;

namespace SalekhPos.Api.Security;

public sealed class SalesDeviceRequestAuthorizer(IDeviceRequestProofVerifier verifier) : ISalesDeviceRequestAuthorizer
{
    public Task VerifyAsync(SalesDeviceRequestProofContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return verifier.VerifyAsync(new DeviceRequestProofContext(
            new DeviceIdentity(context.Identity.Issuer, context.Identity.Subject),
            context.OrganizationId, context.BranchId, context.DeviceId, context.Method, context.CanonicalPath,
            context.OperationIdentity, context.BodyDigest, new DeviceRequestProofHeaders(
                context.Headers.CredentialId, context.Headers.Timestamp, context.Headers.Nonce,
                context.Headers.Signature), RequireCredential: true), cancellationToken);
    }
}
