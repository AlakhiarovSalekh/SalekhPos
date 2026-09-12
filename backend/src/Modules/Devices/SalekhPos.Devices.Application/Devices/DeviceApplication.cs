using SalekhPos.Devices.Contracts.Devices;
namespace SalekhPos.Devices.Application.Devices;

public sealed record DeviceIdentity(string Issuer, string Subject);
public sealed record RegisterDeviceCommand(Guid OrganizationId, Guid BranchId, Guid DeviceId, Guid OperationId,
    Guid RegisterId, string Code, string Name, string Platform, int SyncProtocolVersion, string PublicKey);
public sealed record TrustDeviceCommand(Guid OrganizationId, Guid BranchId, Guid DeviceId, Guid OperationId,
    Guid CredentialId, string ProofChallenge, string Signature);
public sealed record RevokeDeviceCommand(Guid OrganizationId, Guid BranchId, Guid DeviceId, Guid OperationId, string Reason);
public sealed record DeviceWriteResult(DeviceResponse Device, bool Created);
public interface IDeviceRegistry
{
    Task<DeviceWriteResult> RegisterAsync(DeviceIdentity identity, RegisterDeviceCommand command, CancellationToken cancellationToken);
    Task<DeviceResponse> TrustAsync(DeviceIdentity identity, TrustDeviceCommand command, CancellationToken cancellationToken);
    Task<DeviceResponse> RevokeAsync(DeviceIdentity identity, RevokeDeviceCommand command, CancellationToken cancellationToken);
    Task<DeviceResponse?> ReadAsync(DeviceIdentity identity, Guid organizationId, Guid branchId, Guid deviceId, CancellationToken cancellationToken);
    Task<DevicePage> ListAsync(DeviceIdentity identity, Guid organizationId, Guid branchId, int pageSize, Guid? after, CancellationToken cancellationToken);
}
public sealed class DeviceDeniedException : Exception;
public sealed class DeviceConflictException : Exception;
public sealed class DeviceProofException : Exception;
public sealed class DeviceUnavailableException : Exception;
