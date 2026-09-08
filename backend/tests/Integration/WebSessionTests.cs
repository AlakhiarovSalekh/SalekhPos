using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using SalekhPos.Identity.Infrastructure.Tokens;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class WebSessionTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    [Fact]
    public async Task DurableEncryptedTicketSurvivesStoreReplacementAndLogoutCannotBeUndoneByRenewal()
    {
        await using var source = NpgsqlDataSource.Create(fixture.RuntimeConnection);
        var protection = new EphemeralDataProtectionProvider();
        var store = new PostgresTicketStore(source, protection);
        var ticket = Ticket(DateTimeOffset.UtcNow.AddMinutes(10));
        var key = await store.StoreAsync(ticket);
        try
        {
            var replacement = new PostgresTicketStore(source, protection);
            Assert.Equal("session-user", (await replacement.RetrieveAsync(key))?.Principal.FindFirst("sub")?.Value);
            Assert.Null(await new PostgresTicketStore(source, new EphemeralDataProtectionProvider()).RetrieveAsync(key));
            Assert.Null(await store.RetrieveAsync(new string('0', 64)));
            await replacement.RemoveAsync(key);
            await store.RenewAsync(key, ticket);
            Assert.Null(await replacement.RetrieveAsync(key));
        }
        finally { await store.RemoveAsync(key); }
    }

    [Fact]
    public async Task ExpiredAndMalformedSessionReferencesAreRejected()
    {
        await using var source = NpgsqlDataSource.Create(fixture.RuntimeConnection);
        var store = new PostgresTicketStore(source, new EphemeralDataProtectionProvider());
        Assert.Null(await store.RetrieveAsync("not-a-session"));
        var key = await store.StoreAsync(Ticket(DateTimeOffset.UtcNow.AddMinutes(-1)));
        try { Assert.Null(await store.RetrieveAsync(key)); }
        finally { await store.RemoveAsync(key); }
    }

    private static AuthenticationTicket Ticket(DateTimeOffset expires) => new(
        new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "session-user")], "WebSession")),
        new AuthenticationProperties { ExpiresUtc = expires }, "WebSession");
}
