using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SalekhPos.Identity.Application;
using SalekhPos.Identity.Infrastructure;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class TokenRevocationTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    private string BranchPath => $"/api/v1/organizations/{fixture.OrganizationA}/branches";
    private const string RevokePath = "/api/v1/identity/revoke-current-token";

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? token)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); }
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task RevocationSurvivesHostRestartAndDoesNotRevokeAnotherCredential()
    {
        var token = fixture.Token();
        using var client = fixture.Factory.CreateClient();
        using var before = await SendAsync(client, HttpMethod.Get, BranchPath, token);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using var revoked = await SendAsync(client, HttpMethod.Post, RevokePath, token);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal("no-store", revoked.Headers.CacheControl?.ToString());
        await using var restarted = fixture.Factory.WithWebHostBuilder(_ => { });
        using var nextClient = restarted.CreateClient();
        using var after = await SendAsync(nextClient, HttpMethod.Get, BranchPath, token);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        using var retry = await SendAsync(nextClient, HttpMethod.Post, RevokePath, token);
        Assert.Equal(HttpStatusCode.Unauthorized, retry.StatusCode);
        using var other = await SendAsync(nextClient, HttpMethod.Get, BranchPath, fixture.Token());
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrForgedCredentialsCannotRevoke(bool forged)
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await SendAsync(client, HttpMethod.Post, RevokePath,
            forged ? fixture.Token(badSignature: true) : null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AlternateSignatureEncodingCannotBypassRevocation()
    {
        using var client = fixture.Factory.CreateClient();
        var token = fixture.Token();
        using var revoked = await SendAsync(client, HttpMethod.Post, RevokePath, token);
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        // RSA-2048 signatures end with two significant bits and four padding bits.
        var index = alphabet.IndexOf(token[^1], StringComparison.Ordinal);
        var alternate = token[..^1] + alphabet[(index & 48) | 1];
        Assert.NotEqual(token, alternate);
        using var response = await SendAsync(client, HttpMethod.Get, BranchPath, alternate);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RequestBodyCannotSelectAnotherUsersCredential()
    {
        using var client = fixture.Factory.CreateClient();
        var alice = fixture.Token();
        var bob = fixture.Token("bob");
        using var request = new HttpRequestMessage(HttpMethod.Post, RevokePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice);
        request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { subject = "bob", token = bob }));
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var other = await SendAsync(client, HttpMethod.Get,
            $"/api/v1/organizations/{fixture.OrganizationB}/branches", bob);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task ConcurrentDuplicatesPersistExactlyOneAuditWithValidatedActor()
    {
        var credential = new AuthenticatedCredential(AccessFixture.Issuer, "concurrent-user",
            fixture.Token("concurrent-user"), DateTime.UtcNow.AddMinutes(5));
        await using var source = NpgsqlDataSource.Create(fixture.RuntimeConnection);
        var store = new TokenRevocations(source);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            store.RevokeAsync(credential, new string('a', 32), default)));
        Assert.True(await store.IsRevokedAsync(credential, default));
        await using var connection = await source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var context = new NpgsqlCommand("SELECT set_config('app.issuer',$1,true),set_config('app.subject',$2,true)", connection, transaction);
        context.Parameters.AddWithValue(credential.Issuer);
        context.Parameters.AddWithValue(credential.Subject);
        await context.ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM identity.revoked_tokens WHERE fingerprint=$1
              AND action='identity.token_revoked' AND trace_id=$2
              AND revoked_at <= statement_timestamp() AND expires_at > revoked_at
            """, connection, transaction);
        command.Parameters.AddWithValue(Convert.FromHexString(credential.Fingerprint));
        command.Parameters.AddWithValue(new string('a', 32));
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task UnavailableRevocationStoreFailsClosedAndCannotReportReady()
    {
        await using var unavailable = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton(new TokenRevocations(null))));
        using var client = unavailable.CreateClient();
        using var response = await SendAsync(client, HttpMethod.Get, BranchPath, fixture.Token());
        Assert.False(response.IsSuccessStatusCode);
        Assert.DoesNotContain("Branch A1", await response.Content.ReadAsStringAsync());
        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task SingleConnectionPoolClearsIdentityAfterRevocationAndCancellation()
    {
        var options = new NpgsqlConnectionStringBuilder(fixture.RuntimeConnection) { MaxPoolSize = 1 };
        await using var source = NpgsqlDataSource.Create(options.ConnectionString);
        var store = new TokenRevocations(source);
        var credential = new AuthenticatedCredential(AccessFixture.Issuer, "pool-user",
            fixture.Token("pool-user"), DateTime.UtcNow.AddMinutes(5));
        await store.RevokeAsync(credential, new string('b', 32), default);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.IsRevokedAsync(credential, cancellation.Token));
        await using var connection = await source.OpenConnectionAsync();
        await using var command = new NpgsqlCommand("""
            SELECT nullif(current_setting('app.issuer',true),''), nullif(current_setting('app.subject',true),'')
            """, connection);
        await using var result = await command.ExecuteReaderAsync();
        Assert.True(await result.ReadAsync());
        Assert.True(result.IsDBNull(0) && result.IsDBNull(1));
    }
}
