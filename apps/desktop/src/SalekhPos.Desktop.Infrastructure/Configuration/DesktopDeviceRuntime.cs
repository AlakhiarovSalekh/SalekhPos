using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Application.POS;
using SalekhPos.Desktop.Infrastructure.Devices;
using SalekhPos.Desktop.Infrastructure.LocalDatabase;

namespace SalekhPos.Desktop.Infrastructure.Configuration;

public sealed record DesktopBranch(Guid Id, Guid BusinessId, Guid? RegionId, string Code, string Name,
    string TimeZoneId)
{
    public override string ToString() => $"{Code} — {Name}";
}

public sealed record DesktopRegister(Guid Id, Guid BranchId, string Code, string Name, bool IsActive,
    DateTimeOffset CreatedAt)
{
    public override string ToString() => $"{Code} — {Name}";
}

public interface IDesktopDeviceRuntime
{
    Task<PosWorkspaceScope?> TryResumeAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DesktopBranch>> GetBranchesAsync(Guid organizationId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<DesktopRegister>> GetActiveRegistersAsync(Guid organizationId, Guid branchId,
        CancellationToken cancellationToken);
    Task<ProvisionedDevice> ProvisionAsync(DeviceProvisioningRequest request,
        CancellationToken cancellationToken);
}

public interface IDesktopDeviceRuntimeFactory
{
    IDesktopDeviceRuntime Create(string databasePath, HttpClient authenticatedClient);
}

public sealed class DesktopDeviceRuntimeFactory : IDesktopDeviceRuntimeFactory
{
    public IDesktopDeviceRuntime Create(string databasePath, HttpClient authenticatedClient) =>
        new DesktopDeviceRuntime(databasePath, authenticatedClient, DeviceSigningKeyProvider.CreateForCurrentPlatform());
}

public sealed class DesktopDeviceRuntime : IDesktopDeviceRuntime
{
    private const int PageSize = 100;
    private const int MaximumPages = 1000;
    private const int MaximumResponseBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly IOptionalActiveDeviceProvisioningProofMaterialReader assignmentReader;
    private readonly IDeviceSigningKeyProvider keys;
    private readonly IDeviceProvisioner provisioner;
    private readonly HttpClient client;

    public DesktopDeviceRuntime(string databasePath, HttpClient authenticatedClient,
        IDeviceSigningKeyProvider keys)
        : this(new SqliteDeviceProvisioningStateStore(databasePath), keys,
            new DeviceProvisioner(keys, new SqliteDeviceProvisioningStateStore(databasePath),
                new HttpDeviceProvisioningClient(authenticatedClient)), authenticatedClient)
    {
    }

    public DesktopDeviceRuntime(IOptionalActiveDeviceProvisioningProofMaterialReader assignmentReader,
        IDeviceSigningKeyProvider keys, IDeviceProvisioner provisioner, HttpClient client)
    {
        this.assignmentReader = assignmentReader;
        this.keys = keys;
        this.provisioner = provisioner;
        this.client = client;
    }

    public async Task<PosWorkspaceScope?> TryResumeAsync(Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        var material = await assignmentReader.TryReadActiveAsync(cancellationToken);
        if (material is null) return null;
        if (material.OrganizationId != organizationId || material.BranchId == Guid.Empty
            || material.DeviceId == Guid.Empty || material.CredentialId == Guid.Empty
            || string.IsNullOrWhiteSpace(material.KeyReference))
            throw new InvalidOperationException("The local device assignment does not match this deployment.");

        var expectedFingerprint = DecodeFingerprint(material.PublicKeyFingerprint);
        using var key = keys.Open(material.KeyReference, createIfMissing: false);
        var publicKey = ValidatePublicKey(key.GetSubjectPublicKeyInfo());
        if (!CryptographicOperations.FixedTimeEquals(expectedFingerprint, SHA256.HashData(publicKey)))
            throw new InvalidOperationException("The local device key does not match its assignment.");
        return new PosWorkspaceScope(organizationId, material.BranchId, material.DeviceId);
    }

    public async Task<IReadOnlyList<DesktopBranch>> GetBranchesAsync(Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        var results = new List<DesktopBranch>();
        var ids = new HashSet<Guid>();
        await ReadAllPages(async cursor =>
        {
            var path = $"api/v1/organizations/{organizationId:D}/branches?pageSize={PageSize}" +
                (cursor is null ? string.Empty : $"&after={cursor.Value:D}");
            var page = await GetAsync<BranchPage>(path, cancellationToken);
            if (page.Items is null || page.Items.Count > PageSize) throw InvalidResponse();
            foreach (var branch in page.Items)
            {
                ValidateBranch(branch);
                if (!ids.Add(branch.Id)) throw InvalidResponse();
                results.Add(branch);
            }
            return page.NextCursor;
        });
        return results;
    }

    public async Task<IReadOnlyList<DesktopRegister>> GetActiveRegistersAsync(Guid organizationId, Guid branchId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Organization and branch are required.");
        var results = new List<DesktopRegister>();
        var ids = new HashSet<Guid>();
        await ReadAllPages(async cursor =>
        {
            var path = $"api/v1/organizations/{organizationId:D}/branches/{branchId:D}/registers?pageSize={PageSize}" +
                (cursor is null ? string.Empty : $"&after={cursor.Value:D}");
            var page = await GetAsync<RegisterPage>(path, cancellationToken);
            if (page.Items is null || page.Items.Count > PageSize) throw InvalidResponse();
            foreach (var register in page.Items)
            {
                ValidateRegister(register, branchId);
                if (!ids.Add(register.Id)) throw InvalidResponse();
                if (register.IsActive) results.Add(register);
            }
            return page.NextCursor;
        });
        return results;
    }

    public Task<ProvisionedDevice> ProvisionAsync(DeviceProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        DeviceProvisioner.ValidateRequest(request);
        return provisioner.ProvisionAsync(request, cancellationToken);
    }

    private static async Task ReadAllPages(Func<Guid?, Task<Guid?>> read)
    {
        var cursors = new HashSet<Guid>();
        Guid? cursor = null;
        for (var page = 0; page < MaximumPages; page++)
        {
            cursor = await read(cursor);
            if (cursor is null) return;
            if (cursor == Guid.Empty || !cursors.Add(cursor.Value)) throw InvalidResponse();
        }
        throw InvalidResponse();
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
            throw new HttpRequestException("Device discovery failed.", null, response.StatusCode);
        if (response.Content.Headers.ContentLength is > MaximumResponseBytes) throw InvalidResponse();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(block, cancellationToken);
            if (read == 0) break;
            if (buffer.Length + read > MaximumResponseBytes) throw InvalidResponse();
            buffer.Write(block, 0, read);
        }
        try
        {
            return JsonSerializer.Deserialize<T>(buffer.ToArray(), JsonOptions) ?? throw InvalidResponse();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The device discovery response is invalid.", exception);
        }
    }

    private static void ValidateBranch(DesktopBranch branch)
    {
        if (branch.Id == Guid.Empty || branch.BusinessId == Guid.Empty
            || branch.RegionId == Guid.Empty || InvalidText(branch.Code, 64) || InvalidText(branch.Name, 160)
            || InvalidText(branch.TimeZoneId, 128)) throw InvalidResponse();
    }

    private static void ValidateRegister(DesktopRegister register, Guid branchId)
    {
        if (register.Id == Guid.Empty || register.BranchId != branchId || InvalidText(register.Code, 64)
            || InvalidText(register.Name, 160) || register.CreatedAt == default
            || register.CreatedAt.Offset != TimeSpan.Zero) throw InvalidResponse();
    }

    private static bool InvalidText(string? value, int maximum) => string.IsNullOrWhiteSpace(value)
        || value != value.Trim() || value.Length > maximum || value.Any(char.IsControl);

    private static byte[] DecodeFingerprint(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length != 32 || Convert.ToBase64String(bytes) != value) throw new FormatException();
            return bytes;
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("The local device fingerprint is invalid.", exception);
        }
    }

    private static byte[] ValidatePublicKey(byte[]? value)
    {
        if (value is null) throw new InvalidOperationException("The local device key is invalid.");
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(value, out var read);
            var parameters = key.ExportParameters(false);
            if (read != value.Length || key.KeySize != 256 || !parameters.Curve.IsNamed
                || parameters.Curve.Oid.Value != "1.2.840.10045.3.1.7"
                || parameters.Q.X?.Length != 32 || parameters.Q.Y?.Length != 32
                || !value.AsSpan().SequenceEqual(key.ExportSubjectPublicKeyInfo()))
                throw new InvalidOperationException("The local device key is invalid.");
            return value;
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("The local device key is invalid.", exception);
        }
    }

    private static InvalidOperationException InvalidResponse() =>
        new("The device discovery response is invalid.");

    private sealed record BranchPage(IReadOnlyList<DesktopBranch>? Items, Guid? NextCursor);
    private sealed record RegisterPage(IReadOnlyList<DesktopRegister>? Items, Guid? NextCursor);
}
