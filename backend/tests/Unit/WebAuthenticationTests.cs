using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SalekhPos.Tests;

public sealed class WebAuthenticationTests
{
    [Fact]
    public async Task UnconfiguredSignInIsUnavailableWithoutWeakeningBearerAuthorization()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var session = await client.GetAsync("/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        Assert.Contains("\"configured\":false", await session.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/auth/login", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/platform/authority")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/bff/v1/organizations/11111111-1111-4111-8111-111111111111/inventory/access")).StatusCode);
    }

    [Theory]
    [InlineData("http://identity.example", "https://web.example")]
    [InlineData("https://identity.example", "https://web.example/untrusted")]
    [InlineData("https://identity.example?query=1", "https://web.example")]
    public void InvalidProviderOrOriginFailsStartup(string authority, string origin)
    {
        using var factory = Configured(authority, origin);
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    [Fact]
    public async Task SignInRejectsMissingCsrfAndForeignOriginsBeforeProviderDiscovery()
    {
        await using var factory = Configured("https://identity.example", "https://web.example");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://web.example") });
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.example");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/auth/login", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", "https://web.example");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/auth/login", null)).StatusCode);
    }

    [Fact]
    public async Task BusinessBffFailsClosedWithoutAWebSession()
    {
        await using var unavailable = new WebApplicationFactory<Program>();
        using var unavailableClient = unavailable.CreateClient();
        using var unavailableResponse = await unavailableClient.GetAsync(
            "/bff/api/v1/organizations/10000000-0000-0000-0000-000000000001/branches");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailableResponse.StatusCode);

        await using var configured = Configured("https://identity.example", "https://web.example");
        using var configuredClient = configured.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://web.example")
        });
        using var unauthorizedResponse = await configuredClient.GetAsync(
            "/bff/api/v1/organizations/10000000-0000-0000-0000-000000000001/branches");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
    }

    [Theory]
    [InlineData("/bff/api/v1/organizations/10000000-0000-0000-0000-000000000001/branches/20000000-0000-0000-0000-000000000002/registers?pageSize=25")]
    [InlineData("/bff/api/v1/organizations/10000000-0000-0000-0000-000000000001/pricing/resolve?branchId=20000000-0000-0000-0000-000000000002&productId=30000000-0000-0000-0000-000000000003&at=2026-09-15T08%3A00%3A00.0000000%2B00%3A00")]
    [InlineData("/bff/api/v1/organizations/10000000-0000-0000-0000-000000000001/branches/20000000-0000-0000-0000-000000000002/shifts/open?registerId=40000000-0000-0000-0000-000000000004")]
    [InlineData("/bff/api/v1/organizations/10000000-0000-0000-0000-000000000001/branches/20000000-0000-0000-0000-000000000002/shifts/closed?pageSize=25")]
    [InlineData("/bff/api/v1/organizations/10000000-0000-0000-0000-000000000001/branches/20000000-0000-0000-0000-000000000002/payment-events?pageSize=25")]
    public async Task OperationsBffFailsClosedWithoutAWebSession(string route)
    {
        await using var unavailable = new WebApplicationFactory<Program>();
        using var unavailableClient = unavailable.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await unavailableClient.GetAsync(route)).StatusCode);

        await using var configured = Configured("https://identity.example", "https://web.example");
        using var configuredClient = configured.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://web.example")
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await configuredClient.GetAsync(route)).StatusCode);
    }

    private static WebApplicationFactory<Program> Configured(string authority, string origin) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
            .UseSetting("WebAuthentication:Authority", authority)
            .UseSetting("WebAuthentication:ClientId", "test-client")
            .UseSetting("WebAuthentication:ClientSecret", "test-configuration-value")
            .UseSetting("WebAuthentication:PublicOrigin", origin)
            .UseSetting("AllowedHosts", "web.example"));
}
