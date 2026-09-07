using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Npgsql;
using SalekhPos.SystemAdministration.Contracts;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class PlatformAuthorityTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    private const string AdminPath = "/api/v1/platform/super-admins";
    private string RootToken(long? authTime = null) => fixture.Token("platform-root",
        acr: "urn:salekhpos:test:mfa", authTime: authTime ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token, object? body = null)
    {
        using var client = fixture.Factory.CreateClient();
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); }
        if (body is not null) { request.Content = JsonContent.Create(body); }
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("alice")]
    [InlineData("owner")]
    [InlineData("manager")]
    [InlineData("bob")]
    public async Task TenantRolesAndForgedClaimsNeverGrantPlatformAuthority(string subject)
    {
        var token = fixture.Token(subject, forgedClaims: true, acr: "urn:salekhpos:test:mfa",
            authTime: DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var authority = await SendAsync(HttpMethod.Get, "/api/v1/platform/authority", token);
        Assert.Equal(new PlatformAuthority(false, false), await authority.Content.ReadFromJsonAsync<PlatformAuthority>());
        using var response = await SendAsync(HttpMethod.Post, AdminPath, token,
            new RegisterSuperAdminRequest(Guid.NewGuid(), "attacker", "Attempt platform elevation"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("id-token")]
    public async Task AuthenticationValidationStillProtectsPlatformCommands(string scenario)
    {
        var token = scenario switch
        {
            "missing" => null,
            "expired" => fixture.Token("platform-root", expired: true),
            "signature" => fixture.Token("platform-root", badSignature: true),
            _ => fixture.Token("platform-root", type: "JWT")
        };
        using var response = await SendAsync(HttpMethod.Post, AdminPath, token,
            new RegisterSuperAdminRequest(Guid.NewGuid(), "attacker", "Attempt invalid authentication"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RootRequiresRecentMfaAndDoesNotGainTenantMembership()
    {
        using var missing = await SendAsync(HttpMethod.Post, AdminPath, fixture.Token("platform-root"),
            new RegisterSuperAdminRequest(Guid.NewGuid(), "support", "Missing assurance"));
        using var stale = await SendAsync(HttpMethod.Post, AdminPath, RootToken(DateTimeOffset.UtcNow.AddMinutes(-6).ToUnixTimeSeconds()),
            new RegisterSuperAdminRequest(Guid.NewGuid(), "support", "Old assurance"));
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, stale.StatusCode);
        using var tenant = await SendAsync(HttpMethod.Get, $"/api/v1/organizations/{fixture.OrganizationA}/branches", RootToken());
        Assert.Equal(HttpStatusCode.Forbidden, tenant.StatusCode);
    }

    [Fact]
    public async Task RootCanRegisterAndRevokeButAdditionalAdminsCannotDelegate()
    {
        var subject = "support-" + Guid.NewGuid().ToString("N");
        using var created = await SendAsync(HttpMethod.Post, AdminPath, RootToken(),
            new { operationId = Guid.NewGuid(), subject, reason = "Authorize support administrator", isRoot = true });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var admin = (await created.Content.ReadFromJsonAsync<SuperAdminResponse>())!;
        Assert.False(admin.IsRoot);
        Assert.True(admin.IsActive);
        var supportToken = fixture.Token(subject, acr: "urn:salekhpos:test:mfa", authTime: DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var authority = await SendAsync(HttpMethod.Get, "/api/v1/platform/authority", supportToken);
        Assert.Equal(new PlatformAuthority(false, true), await authority.Content.ReadFromJsonAsync<PlatformAuthority>());
        using var denied = await SendAsync(HttpMethod.Post, AdminPath, supportToken,
            new RegisterSuperAdminRequest(Guid.NewGuid(), "unauthorized", "Attempt delegation"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var revoke = new RevokeSuperAdminRequest(Guid.NewGuid(), "End support authorization");
        using var revoked = await SendAsync(HttpMethod.Post, $"{AdminPath}/{admin.Id}/revoke", RootToken(), revoke);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        Assert.False((await revoked.Content.ReadFromJsonAsync<SuperAdminResponse>())!.IsActive);
        using var replay = await SendAsync(HttpMethod.Post, $"{AdminPath}/{admin.Id}/revoke", RootToken(), revoke);
        Assert.Equal(await revoked.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        using var after = await SendAsync(HttpMethod.Get, "/api/v1/platform/authority", supportToken);
        Assert.Equal(new PlatformAuthority(false, false), await after.Content.ReadFromJsonAsync<PlatformAuthority>());
    }

    [Fact]
    public async Task ConcurrentIdenticalOperationsHaveOneResultAndConflictingReuseIsRejected()
    {
        var request = new RegisterSuperAdminRequest(Guid.NewGuid(), "concurrent-" + Guid.NewGuid().ToString("N"), "Authorize one administrator");
        var token = RootToken();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => SendAsync(HttpMethod.Post, AdminPath, token, request)));
        try
        {
            var ids = new HashSet<Guid>();
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                ids.Add((await response.Content.ReadFromJsonAsync<SuperAdminResponse>())!.Id);
            }
            Assert.Single(ids);
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        using var conflict = await SendAsync(HttpMethod.Post, AdminPath, token, request with { Subject = "different" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        await using var source = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("SALEKHPOS_TEST_ADMIN_CONNECTION")!);
        await using var audit = source.CreateCommand("SELECT count(*) FROM system_administration.authority_audit WHERE operation_id=$1 AND action='super_admin.registered' AND actor_subject='platform-root'");
        audit.Parameters.AddWithValue(request.OperationId);
        Assert.Equal(1L, await audit.ExecuteScalarAsync());
    }

    [Fact]
    public async Task OriginalRootCannotBeRevokedOrReplaced()
    {
        await using var source = NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("SALEKHPOS_TEST_ADMIN_CONNECTION")!);
        await using var query = source.CreateCommand("SELECT admin_id FROM system_administration.super_admins WHERE is_root");
        var rootId = (Guid)(await query.ExecuteScalarAsync())!;
        using var revoke = await SendAsync(HttpMethod.Post, $"{AdminPath}/{rootId}/revoke", RootToken(),
            new RevokeSuperAdminRequest(Guid.NewGuid(), "Attempt root revocation"));
        Assert.Equal(HttpStatusCode.Conflict, revoke.StatusCode);
        await using var replace = source.CreateCommand("SELECT system_administration.bootstrap_root($1,$2,'replacement','Attempt replacement',$3)");
        replace.Parameters.AddWithValue(Guid.NewGuid());
        replace.Parameters.AddWithValue(AccessFixture.Issuer);
        replace.Parameters.AddWithValue(new string('a', 32));
        var exception = await Assert.ThrowsAsync<PostgresException>(() => replace.ExecuteScalarAsync());
        Assert.Equal("P0001", exception.SqlState);
    }

    [Fact]
    public async Task MissingMfaConfigurationFailsClosedAndBootstrapHasNoPublicRoute()
    {
        await using var closed = fixture.Factory.WithWebHostBuilder(builder => builder.UseSetting("Authentication:PrivilegedAcr", null));
        using var client = closed.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", RootToken());
        using var response = await client.PostAsJsonAsync(AdminPath, new RegisterSuperAdminRequest(Guid.NewGuid(), "closed", "Closed configuration"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var bootstrap = await SendAsync(HttpMethod.Post, "/api/v1/platform/bootstrap-root", RootToken());
        Assert.Equal(HttpStatusCode.NotFound, bootstrap.StatusCode);
    }
}
