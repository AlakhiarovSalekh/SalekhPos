using System.Security.Cryptography;
using System.Text;

namespace SalekhPos.Desktop.Application.Devices;

public interface IDeviceSigningKey : IDisposable
{
    string KeyReference { get; }
    byte[] GetSubjectPublicKeyInfo();
    byte[] Sign(ReadOnlySpan<byte> data);
}

public interface IDeviceSigningKeyProvider
{
    string CreateKeyReference();
    IDeviceSigningKey Open(string keyReference, bool createIfMissing);
}

public sealed record DeviceProvisioningRequest(Guid OrganizationId, Guid BranchId, Guid RegisterId,
    string Code, string Name, string Platform, int SyncProtocolVersion = 1);

public sealed record DeviceCredential(Guid Id, string Algorithm, string ProofChallenge,
    DateTimeOffset ProofExpiresAt, string Status);

public sealed record ProvisionedDevice(Guid Id, Guid BranchId, Guid RegisterId, string Code, string Name,
    string Platform, string Status, int SyncProtocolVersion, DateTimeOffset RegisteredAt, string RegisteredBy,
    DeviceCredential? Credential, DateTimeOffset? RevokedAt, string? RevokedBy, string? RevocationReason);

public sealed record DeviceProvisioningState(DeviceProvisioningRequest Request, Guid RegistrationOperationId,
    Guid TrustOperationId, string KeyReference, string? PublicKeyFingerprint, ProvisionedDevice? Device);

public interface IDeviceProvisioningStateStore
{
    Task<DeviceProvisioningState> GetOrCreateAsync(DeviceProvisioningRequest request,
        Guid registrationOperationId, Guid trustOperationId, string keyReference, CancellationToken cancellationToken);
    Task<DeviceProvisioningState> BindPublicKeyAsync(DeviceProvisioningState state, string fingerprint,
        CancellationToken cancellationToken);
    Task<DeviceProvisioningState> RecordRegistrationAsync(DeviceProvisioningState state, ProvisionedDevice device,
        CancellationToken cancellationToken);
    Task<DeviceProvisioningState> RecordTrustAsync(DeviceProvisioningState state, ProvisionedDevice device,
        CancellationToken cancellationToken);
}

public interface IDeviceProvisioningClient
{
    Task<ProvisionedDevice> RegisterAsync(DeviceProvisioningRequest request, Guid operationId,
        string publicKey, CancellationToken cancellationToken);
    Task<ProvisionedDevice> TrustAsync(Guid organizationId, Guid branchId, Guid deviceId, Guid operationId,
        Guid credentialId, string proofChallenge, string signature, CancellationToken cancellationToken);
}

public interface IDeviceProvisioner
{
    Task<ProvisionedDevice> ProvisionAsync(DeviceProvisioningRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DeviceProvisioner(IDeviceSigningKeyProvider keys, IDeviceProvisioningStateStore states,
    IDeviceProvisioningClient client, TimeProvider? timeProvider = null) : IDeviceProvisioner
{
    private const string Algorithm = "ecdsa-p256-sha256";
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<ProvisionedDevice> ProvisionAsync(DeviceProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var state = await states.GetOrCreateAsync(request, Guid.NewGuid(), Guid.NewGuid(),
            keys.CreateKeyReference(), cancellationToken);
        using var key = keys.Open(state.KeyReference, state.PublicKeyFingerprint is null);
        var publicKey = key.GetSubjectPublicKeyInfo();
        var fingerprint = Convert.ToBase64String(SHA256.HashData(publicKey));
        if (state.PublicKeyFingerprint is null)
            state = await states.BindPublicKeyAsync(state, fingerprint, cancellationToken);
        else if (!FixedBase64Equals(state.PublicKeyFingerprint, fingerprint, 32))
            throw Changed("The persisted device key does not match the provisioning state.");

        if (state.Device?.Status == "active")
        {
            ValidateActive(state, state.Device);
            return state.Device;
        }

        if (state.Device is null)
        {
            var registered = await client.RegisterAsync(request, state.RegistrationOperationId,
                Convert.ToBase64String(publicKey), cancellationToken);
            ValidatePending(request, registered);
            state = await states.RecordRegistrationAsync(state, registered, cancellationToken);
        }
        else
        {
            ValidatePending(request, state.Device);
        }

        var pending = state.Device!;
        var credential = pending.Credential!;
        var challenge = CanonicalBase64(credential.ProofChallenge, 32);
        if (credential.ProofExpiresAt <= clock.GetUtcNow())
            throw Changed("The device proof challenge has expired.");
        var proof = BuildProof(request.OrganizationId, request.BranchId, pending.Id, credential.Id,
            state.TrustOperationId, challenge, fingerprint);
        var signature = key.Sign(proof);
        if (signature.Length != 64)
            throw Changed("The device key returned an invalid signature.");

        var trusted = await client.TrustAsync(request.OrganizationId, request.BranchId, pending.Id,
            state.TrustOperationId, credential.Id, challenge, Convert.ToBase64String(signature), cancellationToken);
        ValidateActive(state, trusted);
        state = await states.RecordTrustAsync(state, trusted, cancellationToken);
        return state.Device!;
    }

    public static byte[] BuildProof(Guid organizationId, Guid branchId, Guid deviceId, Guid credentialId,
        Guid operationId, string proofChallenge, string publicKeyFingerprint)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || deviceId == Guid.Empty
            || credentialId == Guid.Empty || operationId == Guid.Empty)
            throw new ArgumentException("Device proof identities are required.");
        var challenge = CanonicalBase64(proofChallenge, 32);
        var fingerprint = CanonicalBase64(publicKeyFingerprint, 32);
        return Encoding.UTF8.GetBytes($"salekhpos-device-trust-v1\n{organizationId:D}\n{branchId:D}\n" +
            $"{deviceId:D}\n{credentialId:D}\n{operationId:D}\n{challenge}\n{fingerprint}");
    }

    private static void ValidateRequest(DeviceProvisioningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OrganizationId == Guid.Empty || request.BranchId == Guid.Empty || request.RegisterId == Guid.Empty
            || request.SyncProtocolVersion != 1 || InvalidText(request.Code, 64)
            || InvalidText(request.Name, 160) || InvalidText(request.Platform, 64))
            throw new ArgumentException("Device provisioning metadata is invalid.");
    }

    private static void ValidatePending(DeviceProvisioningRequest request, ProvisionedDevice device)
    {
        ValidateAssignment(request, device);
        var credential = device.Credential;
        if (device.Status != "pending" || credential is null || credential.Id == Guid.Empty
            || credential.Algorithm != Algorithm || credential.Status != "pending"
            || credential.ProofExpiresAt == default || credential.ProofExpiresAt.Offset != TimeSpan.Zero
            || device.RevokedAt is not null || device.RevokedBy is not null || device.RevocationReason is not null)
            throw Changed("The device registration response is invalid.");
        _ = CanonicalBase64(credential.ProofChallenge, 32);
    }

    private static void ValidateActive(DeviceProvisioningState state, ProvisionedDevice device)
    {
        ValidateAssignment(state.Request, device);
        var expected = state.Device?.Credential;
        var actual = device.Credential;
        if (device.Status != "active" || expected is null || actual is null || actual.Id != expected.Id
            || actual.Algorithm != Algorithm || actual.Status != "active"
            || actual.ProofExpiresAt != expected.ProofExpiresAt
            || !FixedBase64Equals(actual.ProofChallenge, expected.ProofChallenge, 32)
            || device.RegisteredAt != state.Device!.RegisteredAt || device.RegisteredBy != state.Device.RegisteredBy
            || device.RevokedAt is not null || device.RevokedBy is not null || device.RevocationReason is not null)
            throw Changed("The trusted device response changed registration evidence.");
    }

    private static void ValidateAssignment(DeviceProvisioningRequest request, ProvisionedDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (device.Id == Guid.Empty || device.BranchId != request.BranchId || device.RegisterId != request.RegisterId
            || device.Code != request.Code || device.Name != request.Name || device.Platform != request.Platform
            || device.SyncProtocolVersion != request.SyncProtocolVersion || device.RegisteredAt == default
            || device.RegisteredAt.Offset != TimeSpan.Zero || InvalidText(device.RegisteredBy, 512))
            throw Changed("The device response does not match the requested assignment.");
    }

    private static bool InvalidText(string? value, int maximum) => string.IsNullOrWhiteSpace(value)
        || value != value.Trim() || value.Length > maximum || value.Any(char.IsControl);

    private static string CanonicalBase64(string value, int length)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length != length || Convert.ToBase64String(bytes) != value) throw new FormatException();
            return value;
        }
        catch (FormatException)
        {
            throw Changed("Device proof material is invalid.");
        }
    }

    private static bool FixedBase64Equals(string left, string right, int length)
    {
        try
        {
            var a = Convert.FromBase64String(left); var b = Convert.FromBase64String(right);
            return a.Length == length && b.Length == length && CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch (FormatException) { return false; }
    }

    private static InvalidOperationException Changed(string message) => new(message);
}
