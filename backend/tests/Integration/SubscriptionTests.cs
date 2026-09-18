using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class SubscriptionTests(AccessFixture fixture)
{
    [Fact]
    public async Task SubscriptionLifecycleIsTenantScopedIdempotentAndEntitled()
    {
        await fixture.GrantAsync("owner", fixture.OrganizationA, "subscriptions.view");
        await fixture.GrantAsync("owner", fixture.OrganizationA, "subscriptions.manage");
        var planA = Guid.NewGuid(); var planB = Guid.NewGuid();
        await fixture.ExecuteAsync("""
            INSERT INTO subscriptions.plans(plan_id,code,name,price,currency,billing_interval,entitlements)
            VALUES($1,'starter','Starter',19.00,'GEL','monthly','{"stores":1,"registers":3}'::jsonb),
                  ($2,'growth','Growth',49.00,'GEL','monthly','{"stores":5,"registers":20}'::jsonb)
            """, planA, planB);
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token("owner"));
        var root = $"/api/v1/organizations/{fixture.OrganizationA:D}/subscriptions";
        using var plans = await client.GetAsync(root + "/plans"); Assert.Equal(HttpStatusCode.OK, plans.StatusCode);
        var operation = Guid.NewGuid(); var subscription = Guid.NewGuid(); var start = DateTimeOffset.UtcNow.AddMinutes(1); var end = start.AddDays(30);
        var createBody = new { OperationId = operation, SubscriptionId = subscription, PlanId = planA, PeriodStart = start, PeriodEnd = end, Trial = false };
        using var created = await client.PostAsJsonAsync(root + "/", createBody); Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var replay = await client.PostAsJsonAsync(root + "/", createBody); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var secondActive = await client.PostAsJsonAsync(root + "/", new
        {
            OperationId = Guid.NewGuid(),
            SubscriptionId = Guid.NewGuid(),
            PlanId = planA,
            PeriodStart = start,
            PeriodEnd = end,
            Trial = false
        });
        Assert.Equal(HttpStatusCode.Conflict, secondActive.StatusCode);
        using var entitlement = await client.GetAsync($"{root}/{subscription:D}/entitlements"); Assert.Equal(HttpStatusCode.OK, entitlement.StatusCode);
        using var json = JsonDocument.Parse(await entitlement.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("limits").GetProperty("stores").GetInt64());

        var changeOperation = Guid.NewGuid();
        using var changed = await client.PutAsJsonAsync($"{root}/{subscription:D}/plan", new { OperationId = changeOperation, PlanId = planB });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var entitlement2 = await client.GetAsync($"{root}/{subscription:D}/entitlements");
        using var json2 = JsonDocument.Parse(await entitlement2.Content.ReadAsStringAsync());
        Assert.Equal(5, json2.RootElement.GetProperty("limits").GetProperty("stores").GetInt64());

        using var denied = fixture.Factory.CreateClient();
        denied.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token("none"));
        using var deniedResponse = await denied.GetAsync($"{root}/{subscription:D}");
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
    }
}
