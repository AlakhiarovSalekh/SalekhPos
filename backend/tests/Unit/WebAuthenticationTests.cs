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

    private static WebApplicationFactory<Program> Configured(string authority, string origin) => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.UseEnvironment("Testing")
            .UseSetting("WebAuthentication:Authority", authority)
            .UseSetting("WebAuthentication:ClientId", "test-client")
            .UseSetting("WebAuthentication:ClientSecret", "test-configuration-value")
            .UseSetting("WebAuthentication:PublicOrigin", origin)
            .UseSetting("AllowedHosts", "web.example"));
}
