using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

public sealed class OrganizationAccessTests(AccessFixture fixture) : IClassFixture<AccessFixture>
{
    private static string Path(string query = "") => "/api/v1/access/organizations" + query;

    private async Task<HttpResponseMessage> GetAsync(string? token, string query = "")
    {
        using var client = fixture.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Path(query));
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task MissingCredentialIsRejectedWithoutDetails()
    {
        using var response = await GetAsync(null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("organization", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("owner", 1)]
    [InlineData("bob", 1)]
    [InlineData("revoked", 0)]
    [InlineData("expired", 0)]
    public async Task OnlyActiveCurrentlyValidMembershipsAreReturned(string subject, int expected)
    {
        using var response = await GetAsync(fixture.Token(subject, forgedClaims: true));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = json.RootElement.GetProperty("items");
        Assert.Equal(expected, items.GetArrayLength());
        if (expected == 1)
        {
            var expectedId = subject == "bob" ? fixture.OrganizationB : fixture.OrganizationA;
            Assert.Equal(expectedId, items[0].GetProperty("id").GetGuid());
        }
    }

    [Fact]
    public async Task ResultsUseStableBoundedKeysetPaginationAcrossTenants()
    {
        var subject = "multi-org-" + Guid.NewGuid().ToString("N");
        await fixture.AddMembershipAsync(subject, fixture.OrganizationA, null);
        await fixture.AddMembershipAsync(subject, fixture.OrganizationB, null);
        var ids = new List<Guid>();
        string? after = null;
        for (var pageNumber = 0; pageNumber < 2; pageNumber++)
        {
            using var response = await GetAsync(fixture.Token(subject), "?pageSize=1" + (after is null ? "" : "&after=" + after));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
            ids.Add(item.GetProperty("id").GetGuid());
            var cursor = json.RootElement.GetProperty("nextCursor");
            after = cursor.ValueKind == JsonValueKind.Null ? null : cursor.GetString();
        }
        Assert.Equal(ids.Order(), ids);
        Assert.Equal(new[] { fixture.OrganizationA, fixture.OrganizationB }.Order(), ids.Order());
        Assert.Null(after);
    }

    [Theory]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=1&pageSize=2")]
    [InlineData("?after=not-a-uuid")]
    [InlineData("?organizationId=00000000-0000-0000-0000-000000000001")]
    public async Task MalformedOrClientTenantQueriesAreRejected(string query)
    {
        using var response = await GetAsync(fixture.Token("owner"), query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
