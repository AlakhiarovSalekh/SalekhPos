namespace SalekhPos.Devices.Contracts.Devices;

public sealed record RegisterDeviceRequest(Guid RegisterId, string Code, string Name, string Platform, int SyncProtocolVersion, string PublicKey);
public sealed record TrustDeviceRequest(Guid CredentialId, string ProofChallenge, string Signature);
public sealed record RevokeDeviceRequest(string Reason);
public sealed record DeviceCredentialResponse(Guid Id, string Algorithm, string ProofChallenge, DateTimeOffset ProofExpiresAt, string Status);
public sealed record DeviceResponse(Guid Id, Guid BranchId, Guid RegisterId, string Code, string Name, string Platform,
    string Status, int SyncProtocolVersion, DateTimeOffset RegisteredAt, string RegisteredBy,
    DeviceCredentialResponse? Credential, DateTimeOffset? RevokedAt, string? RevokedBy, string? RevocationReason);
public sealed record DevicePage(IReadOnlyList<DeviceResponse> Items, Guid? NextCursor);
