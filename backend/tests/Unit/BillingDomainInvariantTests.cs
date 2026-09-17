using SalekhPos.Billing.Domain.Charges;
using SalekhPos.Billing.Domain.Invoices;

namespace SalekhPos.Tests;

public sealed class BillingDomainInvariantTests
{
    [Fact]
    public void Invoice_calculates_tax_and_payment_state_exactly()
    {
        var invoice = new Invoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-1", "gel",
            DateTimeOffset.Parse("2026-09-17T00:00:00Z"), DateTimeOffset.Parse("2026-10-17T00:00:00Z"));
        invoice.AddLine(new InvoiceLine(Guid.NewGuid(), "Subscription", 3, 10.01m, 18m));
        invoice.Open();
        Assert.Equal(30.03m, invoice.NetAmount);
        Assert.Equal(5.41m, invoice.TaxAmount);
        Assert.Equal(35.44m, invoice.GrossAmount);
        invoice.ApplyPayment(15.44m);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(20m, invoice.Balance);
        invoice.ApplyPayment(20m);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public void Invoice_rejects_overpayment_and_mutation_after_open()
    {
        var invoice = BuildOpenInvoice();
        Assert.Throws<InvalidOperationException>(() => invoice.ApplyPayment(invoice.GrossAmount + .01m));
        Assert.Throws<InvalidOperationException>(() => invoice.AddLine(new InvoiceLine(Guid.NewGuid(), "Later", 1, 1m, 0m)));
    }

    [Fact]
    public void Charge_is_final_and_refund_requires_success()
    {
        var charge = new Charge(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 12.50m, "GEL", DateTimeOffset.UtcNow);
        charge.Succeed("provider-1");
        Assert.Throws<InvalidOperationException>(() => charge.Fail("late_failure"));
        charge.Refund();
        Assert.Equal(ChargeStatus.Refunded, charge.Status);
    }

    private static Invoice BuildOpenInvoice()
    {
        var invoice = new Invoice(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "INV-2", "GEL",
            DateTimeOffset.Parse("2026-09-17T00:00:00Z"), DateTimeOffset.Parse("2026-10-17T00:00:00Z"));
        invoice.AddLine(new InvoiceLine(Guid.NewGuid(), "Plan", 1, 25m, 18m)); invoice.Open(); return invoice;
    }
}
