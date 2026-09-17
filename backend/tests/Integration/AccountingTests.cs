using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace SalekhPos.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class AccountingTests(AccessFixture fixture)
{
    [Fact]
    public async Task SummaryAndJournalAreTenantScopedAndFinanciallyConsistent()
    {
        var branchId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        await fixture.ExecuteAsync("""
            INSERT INTO organization.branches(
              organization_id,business_id,branch_id,code,name,time_zone_id)
            VALUES($1,$2,$3,$4,'Accounting Branch','Etc/UTC')
            """, fixture.OrganizationA, fixture.BusinessA, branchId,
            $"ACC{branchId:N}"[..20]);
        await fixture.ExecuteAsync("""
            INSERT INTO sales.completed_sales(
              organization_id,sale_id,operation_id,branch_id,currency,net_total,tax_total,
              grand_total,cash_received,change_due,completed_at,issuer,subject)
            VALUES($1,$2,$3,$4,'GEL',10.000000,2.000000,12.000000,15.000000,3.000000,
              statement_timestamp(),$5,'owner')
            """, fixture.OrganizationA, saleId, Guid.NewGuid(), branchId, AccessFixture.Issuer);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.Token("owner"));
        var prefix = $"/api/v1/organizations/{fixture.OrganizationA:D}/branches/{branchId:D}/accounting";
        var query = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

        using var summaryResponse = await client.GetAsync($"{prefix}/summary?{query}");
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        using var summary = JsonDocument.Parse(await summaryResponse.Content.ReadAsStringAsync());
        var root = summary.RootElement;
        Assert.Equal(branchId, root.GetProperty("branchId").GetGuid());
        Assert.Equal("GEL", root.GetProperty("currency").GetString());
        Assert.Equal(1, root.GetProperty("completedSales").GetInt32());
        Assert.Equal(10m, root.GetProperty("salesNet").GetDecimal());
        Assert.Equal(2m, root.GetProperty("salesTax").GetDecimal());
        Assert.Equal(12m, root.GetProperty("salesGross").GetDecimal());
        Assert.Equal(12m, root.GetProperty("netReceipts").GetDecimal());

        using var journalResponse = await client.GetAsync($"{prefix}/journal?{query}&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, journalResponse.StatusCode);
        using var journal = JsonDocument.Parse(await journalResponse.Content.ReadAsStringAsync());
        var items = journal.RootElement.GetProperty("items");
        Assert.Single(items.EnumerateArray());
        var item = items[0];
        Assert.Equal(saleId, item.GetProperty("sourceId").GetGuid());
        Assert.Equal("sale", item.GetProperty("kind").GetString());
        Assert.Equal(10m, item.GetProperty("netAmount").GetDecimal());
        Assert.Equal(2m, item.GetProperty("taxAmount").GetDecimal());
        Assert.Equal(12m, item.GetProperty("grossAmount").GetDecimal());
        Assert.Equal(12m, item.GetProperty("cashEffect").GetDecimal());

        using var denied = fixture.Factory.CreateClient();
        denied.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", fixture.Token("none"));
        using var deniedResponse = await denied.GetAsync($"{prefix}/summary?{query}");
        Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
    }
}
