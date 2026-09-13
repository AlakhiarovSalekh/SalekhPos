using System.Net;
using System.Security.Cryptography;
using System.Text;
using SalekhPos.Desktop.Application.Devices;
using SalekhPos.Desktop.Infrastructure.Configuration;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class DesktopDeviceRuntimeTests
{
    private static readonly Guid Organization = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid BranchOne = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid BranchTwo = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task DiscoveryReadsEveryPageAndReturnsOnlyActiveRegisters()
    {
        var branchCursor = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var registerCursor = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        using var client = Client(request => request.RequestUri!.Query.Contains("after=", StringComparison.Ordinal)
            ? Json(request.RequestUri.AbsolutePath.Contains("registers", StringComparison.Ordinal)
                ? RegisterPage(BranchOne, Guid.NewGuid(), true, null)
                : BranchPage(BranchTwo, null))
            : Json(request.RequestUri.AbsolutePath.Contains("registers", StringComparison.Ordinal)
                ? RegisterPage(BranchOne, Guid.NewGuid(), false, registerCursor)
                : BranchPage(BranchOne, branchCursor)));
        var runtime = Runtime(client);

        var branches = await runtime.GetBranchesAsync(Organization, default);
        var registers = await runtime.GetActiveRegistersAsync(Organization, BranchOne, default);

        Assert.Equal([BranchOne, BranchTwo], branches.Select(branch => branch.Id));
        Assert.Single(registers);
        Assert.True(registers[0].IsActive);
    }

    [Fact]
    public async Task BranchDiscoveryAcceptsSeparateBusinessAndNullableRegion()
    {
        var businessId = Guid.NewGuid();
        var json = $$"""
            {"items":[{"id":"{{BranchOne:D}}","businessId":"{{businessId:D}}","regionId":null,"code":"B","name":"Branch","timeZoneId":"Asia/Tbilisi"}],"nextCursor":null}
            """;
        using var client = Client(_ => Json(json));

        var branches = await Runtime(client).GetBranchesAsync(Organization, default);

        var branch = Assert.Single(branches);
        Assert.Equal(businessId, branch.BusinessId);
        Assert.Null(branch.RegionId);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("cycle")]
    [InlineData("malformed")]
    [InlineData("unexpected")]
    public async Task BranchDiscoveryRejectsUnsafePagingAndPayloads(string scenario)
    {
        var cursor = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var calls = 0;
        using var client = Client(_ =>
        {
            calls++;
            return scenario switch
            {
                "duplicate" => Json(BranchPage(BranchOne, calls == 1 ? cursor : null)),
                "cycle" => Json(BranchPage(calls == 1 ? BranchOne : BranchTwo, cursor)),
                "malformed" => Json("{\"items\":[{\"id\":\"not-a-guid\",\"businessId\":\"" +
                    Organization + "\",\"regionId\":\"" + Guid.NewGuid() +
                    "\",\"code\":\"B1\",\"name\":\"Branch\",\"timeZoneId\":\"Asia/Tbilisi\"}],\"nextCursor\":null}"),
                _ => Json(BranchPage(BranchOne, null).Replace("\"nextCursor\"", "\"extra\":1,\"nextCursor\"")),
            };
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runtime(client).GetBranchesAsync(Organization, default));
    }

    [Fact]
    public async Task RegisterDiscoveryRejectsCrossBranchAndMalformedActiveStatus()
    {
        using var crossBranch = Client(_ => Json(RegisterPage(BranchTwo, Guid.NewGuid(), true, null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runtime(crossBranch).GetActiveRegistersAsync(Organization, BranchOne, default));

        var malformed = RegisterPage(BranchOne, Guid.NewGuid(), true, null)
            .Replace("\"isActive\":true", "\"isActive\":\"active\"");
        using var malformedClient = Client(_ => Json(malformed));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runtime(malformedClient).GetActiveRegistersAsync(Organization, BranchOne, default));
    }

    [Fact]
    public async Task DiscoveryRejectsNonSuccessAndOversizedResponses()
    {
        using var denied = Client(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Runtime(denied).GetBranchesAsync(Organization, default));

        using var oversized = Client(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[(1024 * 1024) + 1]),
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runtime(oversized).GetBranchesAsync(Organization, default));
    }

    [Fact]
    public async Task ResumeVerifiesOrganizationKeyPresenceAndFingerprintWithoutCreatingKey()
    {
        using var keys = new MemoryKeyProvider();
        var reference = keys.Add();
        var fingerprint = Convert.ToBase64String(SHA256.HashData(keys.PublicKey(reference)));
        var material = new ActiveDeviceProvisioningProofMaterial(Organization, BranchOne, Guid.NewGuid(),
            Guid.NewGuid(), reference, fingerprint);
        using var client = Client(_ => throw new InvalidOperationException("Discovery must not run."));
        var runtime = new DesktopDeviceRuntime(new AssignmentReader(material), keys, new NoProvisioner(), client);

        var scope = await runtime.TryResumeAsync(Organization, default);

        Assert.Equal(BranchOne, scope!.BranchId);
        Assert.Equal(material.DeviceId, scope.DeviceId);
        Assert.Equal(0, keys.CreatedDuringOpen);

        var missing = new DesktopDeviceRuntime(new AssignmentReader(material with { KeyReference = "missing" }),
            keys, new NoProvisioner(), client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => missing.TryResumeAsync(Organization, default));
        var mismatch = new DesktopDeviceRuntime(new AssignmentReader(material with
        {
            PublicKeyFingerprint = Convert.ToBase64String(new byte[32]),
        }), keys, new NoProvisioner(), client);
        await Assert.ThrowsAsync<InvalidOperationException>(() => mismatch.TryResumeAsync(Organization, default));
        Assert.Equal(0, keys.CreatedDuringOpen);
    }

    private static DesktopDeviceRuntime Runtime(HttpClient client) => new(new AssignmentReader(null),
        new MemoryKeyProvider(), new NoProvisioner(), client);

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> send) => new(new Handler(send))
    {
        BaseAddress = new Uri("https://pos.test/"),
    };

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static string BranchPage(Guid id, Guid? cursor) => $$"""
        {"items":[{"id":"{{id:D}}","businessId":"{{Organization:D}}","regionId":"{{Guid.NewGuid():D}}","code":"B","name":"Branch","timeZoneId":"Asia/Tbilisi"}],"nextCursor":{{(cursor is null ? "null" : $"\"{cursor:D}\"")}}}
        """;

    private static string RegisterPage(Guid branchId, Guid id, bool active, Guid? cursor) => $$"""
        {"items":[{"id":"{{id:D}}","branchId":"{{branchId:D}}","code":"R","name":"Register","isActive":{{active.ToString().ToLowerInvariant()}},"createdAt":"2026-09-13T08:00:00Z"}],"nextCursor":{{(cursor is null ? "null" : $"\"{cursor:D}\"")}}}
        """;

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(send(request));
    }

    private sealed class AssignmentReader(ActiveDeviceProvisioningProofMaterial? material)
        : IOptionalActiveDeviceProvisioningProofMaterialReader
    {
        public Task<ActiveDeviceProvisioningProofMaterial?> TryReadActiveAsync(
            CancellationToken cancellationToken) => Task.FromResult(material);
    }

    private sealed class NoProvisioner : IDeviceProvisioner
    {
        public Task<ProvisionedDevice> ProvisionAsync(DeviceProvisioningRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MemoryKeyProvider : IDeviceSigningKeyProvider, IDisposable
    {
        private readonly Dictionary<string, ECDsa> keys = [];
        public int CreatedDuringOpen { get; private set; }
        public string Add()
        {
            var reference = Guid.NewGuid().ToString("N");
            keys.Add(reference, ECDsa.Create(ECCurve.NamedCurves.nistP256));
            return reference;
        }
        public byte[] PublicKey(string reference) => keys[reference].ExportSubjectPublicKeyInfo();
        public string CreateKeyReference() => Guid.NewGuid().ToString("N");
        public IDeviceSigningKey Open(string keyReference, bool createIfMissing)
        {
            if (!keys.TryGetValue(keyReference, out var key))
            {
                if (!createIfMissing) throw new InvalidOperationException("Missing key.");
                CreatedDuringOpen++;
                key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                keys.Add(keyReference, key);
            }
            return new MemorySigningKey(key);
        }
        public void Dispose()
        {
            foreach (var key in keys.Values) key.Dispose();
        }
    }

    private sealed class MemorySigningKey(ECDsa key) : IDeviceSigningKey
    {
        public byte[] GetSubjectPublicKeyInfo() => key.ExportSubjectPublicKeyInfo();
        public byte[] Sign(ReadOnlySpan<byte> data) => key.SignData(data, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        public void Dispose() { }
    }
}
