using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Npgsql;
using SalekhPos.Authorization.Application;
using SalekhPos.Authorization.Infrastructure;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class BranchSecurityTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    private string Path(Guid? org = null) => $"/api/v1/organizations/{org ?? fixture.OrganizationA}/branches";

    private async Task<HttpResponseMessage> GetAsync(string path, string? token)
    {
        using var client = fixture.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null) { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); }
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("signature")]
    [InlineData("id_token")]
    public async Task RealJwtValidationRejectsUntrustedCredentials(string scenario)
    {
        var token = scenario switch
        {
            "missing" => null,
            "malformed" => "not-a-jwt",
            "expired" => fixture.Token(expired: true),
            "issuer" => fixture.Token(issuer: "https://attacker.example"),
            "audience" => fixture.Token(audience: "other-api"),
            "signature" => fixture.Token(badSignature: true),
            "id_token" => fixture.Token(type: "JWT"),
            _ => throw new ArgumentException("Unknown scenario")
        };
        using var response = await GetAsync(Path(), token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, value => value.Scheme == "Bearer");
        Assert.DoesNotContain("Tenant", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("alice", 1)]
    [InlineData("regional", 1)]
    [InlineData("manager", 2)]
    [InlineData("owner", 3)]
    public async Task PersistedScopeLimitsBranchResults(string subject, int count)
    {
        using var response = await GetAsync(Path(), fixture.Token(subject, forgedClaims: true));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(count, json.RootElement.GetProperty("items").GetArrayLength());
        Assert.DoesNotContain("Tenant B secret", json.RootElement.ToString());
    }

    [Theory]
    [InlineData("none")]
    [InlineData("revoked")]
    [InlineData("expired")]
    [InlineData("alice' OR true --")]
    public async Task MissingOrInactiveAuthorityCannotBeReplacedByTokenRoles(string subject)
    {
        using var response = await GetAsync(Path(), fixture.Token(subject, forgedClaims: true));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ForgedTenantAndBranchIdentifiersDoNotExposeData()
    {
        using var tenant = await GetAsync(Path(fixture.OrganizationB), fixture.Token(forgedClaims: true));
        Assert.Equal(HttpStatusCode.Forbidden, tenant.StatusCode);
        using var branch = await GetAsync(Path() + "/" + fixture.BranchA2, fixture.Token());
        using var unknown = await GetAsync(Path() + "/" + Guid.NewGuid(), fixture.Token());
        Assert.Equal(HttpStatusCode.NotFound, branch.StatusCode);
        Assert.Equal(branch.StatusCode, unknown.StatusCode);
        Assert.DoesNotContain("Branch A2", await branch.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=1&pageSize=2")]
    [InlineData("?after=invalid")]
    [InlineData("?permission=branches.view")]
    public async Task UnboundedAndAmbiguousQueriesAreRejected(string query)
    {
        using var response = await GetAsync(Path() + query, fixture.Token("owner"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task KeysetPaginationReturnsEveryAuthorizedBranchExactlyOnce()
    {
        var ids = new HashSet<Guid>();
        string? cursor = null;
        for (var page = 0; page < 4; page++)
        {
            using var response = await GetAsync(Path() + "?pageSize=1" + (cursor is null ? "" : "&after=" + cursor), fixture.Token("owner"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = json.RootElement.GetProperty("items");
            Assert.Single(items.EnumerateArray());
            Assert.True(ids.Add(items[0].GetProperty("id").GetGuid()));
            var next = json.RootElement.GetProperty("nextCursor");
            cursor = next.ValueKind == JsonValueKind.Null ? null : next.GetString();
            if (cursor is null) { break; }
        }
        Assert.Null(cursor);
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task CommittedRevocationAppliesToTheNextRequestWithTheSameToken()
    {
        var subject = "temporary-" + Guid.NewGuid().ToString("N");
        await fixture.AddMembershipAsync(subject, fixture.OrganizationA, "organization");
        var token = fixture.Token(subject);
        using var before = await GetAsync(Path(), token);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        await fixture.ExecuteAsync("UPDATE access.memberships SET is_active=false WHERE organization_id=$1 AND subject=$2", fixture.OrganizationA, subject);
        using var after = await GetAsync(Path(), token);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
    }

    [Fact]
    public async Task ConcurrentTenantRequestsRemainIsolatedThroughASingleConnectionPool()
    {
        var options = new NpgsqlConnectionStringBuilder(fixture.RuntimeConnection) { MaxPoolSize = 1 };
        await using var database = new AccessDatabase(options.ConnectionString, true);
        var reader = new BranchAccessReader(database);
        var tasks = Enumerable.Range(0, 20).Select(async index =>
        {
            var tenantA = index % 2 == 0;
            var identity = new AccessIdentity(AccessFixture.Issuer, tenantA ? "alice" : "bob");
            var page = await reader.ReadAsync(identity, tenantA ? fixture.OrganizationA : fixture.OrganizationB,
                10, null, null, default);
            Assert.Equal(tenantA ? fixture.BranchA : fixture.BranchB, Assert.Single(page.Items).Id);
        });
        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task InactiveBusinessCannotBeReadThroughAnOtherwiseValidGrant()
    {
        await fixture.ExecuteAsync("UPDATE organization.businesses SET is_active=false WHERE organization_id=$1 AND business_id=$2", fixture.OrganizationA, fixture.BusinessA);
        try
        {
            using var response = await GetAsync(Path(), fixture.Token("manager"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Empty(json.RootElement.GetProperty("items").EnumerateArray());
        }
        finally
        {
            await fixture.ExecuteAsync("UPDATE organization.businesses SET is_active=true WHERE organization_id=$1 AND business_id=$2", fixture.OrganizationA, fixture.BusinessA);
        }
    }

    [Fact]
    public async Task HealthReadinessAndSensitiveResponsesHaveExplicitContracts()
    {
        using var health = await GetAsync("/health/ready", null);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using var response = await GetAsync(Path(), fixture.Token());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Matches("^[a-f0-9]{32}$", Assert.Single(response.Headers.GetValues("X-Trace-Id")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        using var missingSale = await GetAsync("/api/sales", fixture.Token());
        Assert.Equal(HttpStatusCode.NotFound, missingSale.StatusCode);
    }

    [Fact]
    public async Task OnePhysicalConnectionDoesNotLeakContextAfterSuccessDenialFailureOrCancellation()
    {
        var options = new NpgsqlConnectionStringBuilder(fixture.RuntimeConnection) { MaxPoolSize = 1 };
        await using var database = new AccessDatabase(options.ConnectionString, true);
        var reader = new BranchAccessReader(database);
        var alice = new AccessIdentity(AccessFixture.Issuer, "alice");
        var bob = new AccessIdentity(AccessFixture.Issuer, "bob");
        var source = database.DataSource!;
        int pid;
        await using (var connection = await source.OpenConnectionAsync()) { pid = connection.ProcessID; }
        for (var cycle = 0; cycle < 3; cycle++)
        {
            var a = await reader.ReadAsync(alice, fixture.OrganizationA, 50, null, null, default);
            Assert.Equal(fixture.BranchA, Assert.Single(a.Items).Id);
            await Assert.ThrowsAsync<AccessDeniedException>(() => reader.ReadAsync(alice, fixture.OrganizationB, 50, null, null, default));
            var b = await reader.ReadAsync(bob, fixture.OrganizationB, 50, null, null, default);
            Assert.Equal(fixture.BranchB, Assert.Single(b.Items).Id);
        }
        foreach (var cancel in new[] { false, true })
        {
            await using (var connection = await source.OpenConnectionAsync())
            {
                await using var transaction = await connection.BeginTransactionAsync();
                await using var context = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true)", connection, transaction);
                context.Parameters.AddWithValue(fixture.OrganizationB.ToString());
                await context.ExecuteNonQueryAsync();
                await using var failing = new NpgsqlCommand(cancel ? "SELECT pg_sleep(5)" : "SELECT 1/0", connection, transaction);
                if (cancel)
                {
                    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => failing.ExecuteNonQueryAsync(cancellation.Token));
                }
                else { await Assert.ThrowsAsync<PostgresException>(() => failing.ExecuteNonQueryAsync()); }
            }
            await using var check = await source.OpenConnectionAsync();
            Assert.Equal(pid, check.ProcessID);
            await using var inspect = new NpgsqlCommand("SELECT nullif(current_setting('app.organization_id',true),''), nullif(current_setting('app.subject',true),''), nullif(current_setting('app.issuer',true),'')", check);
            await using var result = await inspect.ExecuteReaderAsync();
            Assert.True(await result.ReadAsync());
            Assert.True(result.IsDBNull(0) && result.IsDBNull(1) && result.IsDBNull(2));
        }
    }
}
