using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class PurchaseOrderReceivingTests(AccessFixture fixture)
{
    private string OrdersPath =>
        $"/api/v1/organizations/{fixture.OrganizationA:D}/branches/{fixture.BranchA:D}/purchase-orders";

    [Fact]
    public async Task Approved_order_can_be_partially_then_fully_received_idempotently()
    {
        var productId = await CreateProduct();
        var supplierId = await CreateSupplier();
        using var client = Client();

        using var created = await SendIdempotent(
            client,
            HttpMethod.Post,
            OrdersPath,
            Guid.NewGuid(),
            new
            {
                supplierId,
                currency = "GEL",
                reference = "PO-RECEIVE-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                lines = new[] { new { productId, quantity = 10m, unitCost = 2.5m } }
            });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await Purchase(created);
        var orderId = draft.GetProperty("id").GetGuid();
        Assert.Equal("draft", draft.GetProperty("status").GetString());
        Assert.Equal(1L, draft.GetProperty("version").GetInt64());

        var submitted = await Transition(client, orderId, "submit", 1);
        Assert.Equal("submitted", submitted.GetProperty("status").GetString());
        Assert.Equal(2L, submitted.GetProperty("version").GetInt64());

        var approved = await Transition(client, orderId, "approve", 2);
        Assert.Equal("approved", approved.GetProperty("status").GetString());
        Assert.Equal(3L, approved.GetProperty("version").GetInt64());

        var firstOperation = Guid.NewGuid();
        var firstReceivedAt = DateTimeOffset.UtcNow;
        var firstBody = new
        {
            expectedVersion = 3,
            reference = "DOCK-A",
            receivedAt = firstReceivedAt,
            lines = new[] { new { productId, quantity = 4m } }
        };
        using var first = await SendIdempotent(
            client,
            HttpMethod.Post,
            $"{OrdersPath}/{orderId:D}/receipts",
            firstOperation,
            firstBody);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstResult = await Root(first);
        var firstReceiptId = firstResult.GetProperty("receipt").GetProperty("id").GetGuid();
        Assert.Equal("partially_received",
            firstResult.GetProperty("order").GetProperty("status").GetString());
        Assert.Equal(4L, firstResult.GetProperty("order").GetProperty("version").GetInt64());

        using var replay = await SendIdempotent(
            client,
            HttpMethod.Post,
            $"{OrdersPath}/{orderId:D}/receipts",
            firstOperation,
            firstBody);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayResult = await Root(replay);
        Assert.Equal(firstReceiptId,
            replayResult.GetProperty("receipt").GetProperty("id").GetGuid());

        using var receiving = await client.GetAsync($"{OrdersPath}/{orderId:D}/receiving");
        Assert.Equal(HttpStatusCode.OK, receiving.StatusCode);
        var state = await Root(receiving);
        Assert.Equal("partially_received", state.GetProperty("status").GetString());
        var line = Assert.Single(state.GetProperty("lines").EnumerateArray());
        Assert.Equal(10m, line.GetProperty("orderedQuantity").GetDecimal());
        Assert.Equal(4m, line.GetProperty("receivedQuantity").GetDecimal());
        Assert.Equal(6m, line.GetProperty("remainingQuantity").GetDecimal());

        using var overReceipt = await SendIdempotent(
            client,
            HttpMethod.Post,
            $"{OrdersPath}/{orderId:D}/receipts",
            Guid.NewGuid(),
            new
            {
                expectedVersion = 4,
                reference = "TOO-MUCH",
                receivedAt = DateTimeOffset.UtcNow,
                lines = new[] { new { productId, quantity = 7m } }
            });
        Assert.Equal(HttpStatusCode.Conflict, overReceipt.StatusCode);

        using var final = await SendIdempotent(
            client,
            HttpMethod.Post,
            $"{OrdersPath}/{orderId:D}/receipts",
            Guid.NewGuid(),
            new
            {
                expectedVersion = 4,
                reference = "DOCK-B",
                receivedAt = DateTimeOffset.UtcNow,
                lines = new[] { new { productId, quantity = 6m } }
            });
        Assert.Equal(HttpStatusCode.Created, final.StatusCode);
        var finalResult = await Root(final);
        Assert.Equal("received",
            finalResult.GetProperty("order").GetProperty("status").GetString());
        Assert.Equal(5L, finalResult.GetProperty("order").GetProperty("version").GetInt64());

        using var stock = await client.GetAsync(
            $"/api/v1/organizations/{fixture.OrganizationA:D}/branches/{fixture.BranchA:D}/inventory/stock");
        Assert.Equal(HttpStatusCode.OK, stock.StatusCode);
        var stockRoot = await Root(stock);
        var stockItem = stockRoot.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("productId").GetGuid() == productId);
        Assert.Equal(10m, stockItem.GetProperty("quantity").GetDecimal());

        Assert.Equal(2L, await fixture.ScalarAsync<long>(
            """
            SELECT count(*) FROM purchasing.purchase_receipts
            WHERE organization_id=$1 AND order_id=$2
            """,
            fixture.OrganizationA,
            orderId));
        Assert.Equal(2L, await fixture.ScalarAsync<long>(
            """
            SELECT count(*) FROM purchasing.purchase_receipt_lines
            WHERE organization_id=$1 AND order_id=$2
            """,
            fixture.OrganizationA,
            orderId));
    }

    private HttpClient Client()
    {
        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.Token("owner"));
        return client;
    }

    private async Task<Guid> CreateProduct()
    {
        using var client = Client();
        using var response = await SendIdempotent(
            client,
            HttpMethod.Post,
            $"/api/v1/organizations/{fixture.OrganizationA:D}/products",
            Guid.NewGuid(),
            new
            {
                sku = "PORCV." + Guid.NewGuid().ToString("N").ToUpperInvariant(),
                name = "Receiving integration item",
                unitCode = "EA",
                barcode = (string?)null
            });
        response.EnsureSuccessStatusCode();
        var root = await Root(response);
        return root.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateSupplier()
    {
        using var client = Client();
        using var response = await SendIdempotent(
            client,
            HttpMethod.Post,
            $"/api/v1/organizations/{fixture.OrganizationA:D}/suppliers",
            Guid.NewGuid(),
            new
            {
                code = "SUP" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                name = "Receiving supplier",
                taxId = (string?)null,
                email = (string?)null,
                phone = (string?)null
            });
        response.EnsureSuccessStatusCode();
        var root = await Root(response);
        return root.GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> Transition(
        HttpClient client,
        Guid orderId,
        string action,
        long expectedVersion)
    {
        using var response = await client.PostAsJsonAsync(
            $"{OrdersPath}/{orderId:D}/{action}",
            new { expectedVersion });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Root(response)).Clone();
    }

    private static async Task<HttpResponseMessage> SendIdempotent(
        HttpClient client,
        HttpMethod method,
        string path,
        Guid operationId,
        object body)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            operationId.ToString("D"));
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> Purchase(HttpResponseMessage response) =>
        (await Root(response)).Clone();

    private static async Task<JsonElement> Root(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }
}
