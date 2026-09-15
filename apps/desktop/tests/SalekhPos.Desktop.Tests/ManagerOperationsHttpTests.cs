using System.Net;
using System.Text;
using SalekhPos.Desktop.Infrastructure.Operations;
using Xunit;

namespace SalekhPos.Desktop.Tests;

public sealed class ManagerOperationsHttpTests
{
    [Fact]
    public async Task ListsRegistersWithExactScopeAndValidatesBranch()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var register = Guid.NewGuid();
        var captured = "";
        using var client = Client(request =>
        {
            captured = request.RequestUri!.PathAndQuery;
            return Json($$"""{"items":[{"id":"{{register:D}}","branchId":"{{branch:D}}","code":"POS-01","name":"Front desk","isActive":true,"createdAt":"2026-09-15T08:00:00Z"}],"nextCursor":null}""");
        });
        var result = await new HttpManagerOperations(client).ListRegistersAsync(organization, branch, 25, null, default);
        Assert.Single(result.Items); Assert.Equal(register, result.Items[0].Id);
        Assert.Equal($"/api/v1/organizations/{organization:D}/branches/{branch:D}/registers?pageSize=25", captured);
    }
    [Fact]
    public async Task ResolvesEffectivePriceAndRejectsWrongProduct()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var product = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);
        using var client = Client(_ => Json($$"""{"priceId":"{{Guid.NewGuid():D}}","productId":"{{product:D}}","branchId":"{{branch:D}}","amount":12.5,"currency":"GEL","taxMode":"inclusive","taxRate":18,"validFrom":"2026-09-15T07:00:00Z","validUntil":null}"""));
        var value = await new HttpManagerOperations(client).ResolvePriceAsync(organization, branch, product, at, default);
        Assert.NotNull(value); Assert.Equal(12.5m, value.Amount);

        using var bad = Client(_ => Json($$"""{"priceId":"{{Guid.NewGuid():D}}","productId":"{{Guid.NewGuid():D}}","branchId":null,"amount":12.5,"currency":"GEL","taxMode":"inclusive","taxRate":18,"validFrom":"2026-09-15T07:00:00Z","validUntil":null}"""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpManagerOperations(bad)
            .ResolvePriceAsync(organization, branch, product, at, default));
    }

    [Fact]
    public async Task OpenShiftTreatsOnly404AsMissing()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var register = Guid.NewGuid();
        using var missing = Client(_ => new(HttpStatusCode.NotFound));
        Assert.Null(await new HttpManagerOperations(missing).ReadOpenShiftAsync(organization, branch, register, default));
        using var denied = Client(_ => new(HttpStatusCode.Forbidden));
        await Assert.ThrowsAsync<HttpRequestException>(() => new HttpManagerOperations(denied)
            .ReadOpenShiftAsync(organization, branch, register, default));
    }
    [Fact]
    public async Task ValidatesClosedShiftReconciliationEvidence()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var shift = Guid.NewGuid(); var register = Guid.NewGuid();
        var valid = $$"""{"id":"{{shift:D}}","branchId":"{{branch:D}}","registerId":"{{register:D}}","status":"closed","currency":"GEL","openingBalance":10,"cashSales":30,"cashRefunds":5,"cashIn":2,"cashOut":3,"expectedCash":34,"countedCash":33,"variance":-1,"openedAt":"2026-09-15T08:00:00Z","closedAt":"2026-09-15T10:00:00Z","openedBy":"cashier","closedBy":"manager"}""";
        using var client = Client(_ => Json(valid));
        var result = await new HttpManagerOperations(client).ReadClosedShiftAsync(organization, branch, shift, default);
        Assert.NotNull(result); Assert.Equal(-1m, result.Variance);

        using var invalid = Client(_ => Json(valid.Replace("\"expectedCash\":34", "\"expectedCash\":35")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpManagerOperations(invalid)
            .ReadClosedShiftAsync(organization, branch, shift, default));
    }

    [Fact]
    public async Task ListsPaymentEventsWithOpaqueCursor()
    {
        var organization = Guid.NewGuid(); var branch = Guid.NewGuid(); var payment = Guid.NewGuid();
        var id = Guid.NewGuid(); var source = Guid.NewGuid(); var cursor = "opaque-safe_cursor-123";
        using var client = Client(_ => Json($$"""{"items":[{"id":"{{id:D}}","paymentId":"{{payment:D}}","branchId":"{{branch:D}}","kind":"capture","sourceId":"{{source:D}}","method":"cash","status":"completed","currency":"GEL","amount":9.5,"completedAt":"2026-09-15T09:00:00Z"}],"nextCursor":"{{cursor}}"}"""));
        var result = await new HttpManagerOperations(client).ListPaymentEventsAsync(organization, branch, 50, null, default);
        Assert.Single(result.Items); Assert.Equal(cursor, result.NextCursor);
    }
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) => new(new Handler(response))
    { BaseAddress = new("https://pos.test/") };

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    { Content = new StringContent(value, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
}
