using SalekhPos.Purchasing.Contracts.SupplierInvoices;
using SalekhPos.Purchasing.Domain.SupplierInvoices;

namespace SalekhPos.Purchasing.Application.SupplierInvoices;

public sealed record CreateSupplierInvoiceCommand(
    Guid OrganizationId,
    Guid BranchId,
    Guid InvoiceId,
    CreateSupplierInvoiceRequest Request);

public static class SupplierInvoiceApplication
{
    public static SupplierInvoice BuildDraft(CreateSupplierInvoiceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Request);
        if (command.OrganizationId == Guid.Empty)
            throw new ArgumentException("Organization is required.", nameof(command));
        if (command.BranchId == Guid.Empty)
            throw new ArgumentException("Branch is required.", nameof(command));
        if (command.InvoiceId == Guid.Empty)
            throw new ArgumentException("Invoice id is required.", nameof(command));

        var lines = command.Request.Lines?.Select(line =>
            SupplierInvoiceLine.Create(line.ProductId, line.Quantity, line.UnitCost)).ToArray()
            ?? throw new ArgumentException("Supplier invoice lines are required.", nameof(command));

        return SupplierInvoice.CreateDraft(
            command.InvoiceId,
            command.BranchId,
            command.Request.SupplierId,
            command.Request.PurchaseOrderId,
            command.Request.InvoiceNumber,
            command.Request.Currency,
            command.Request.InvoiceDate,
            command.Request.DueDate,
            lines);
    }

    public static SupplierInvoiceResponse ToResponse(SupplierInvoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        return new(
            invoice.Id,
            invoice.BranchId,
            invoice.SupplierId,
            invoice.PurchaseOrderId,
            invoice.InvoiceNumber,
            invoice.Currency,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.Status.ToString().ToLowerInvariant(),
            invoice.Total,
            invoice.Version,
            [.. invoice.Lines.Select(line => new SupplierInvoiceLineResponse(
                line.ProductId,
                line.Quantity,
                line.UnitCost,
                line.LineTotal))]);
    }
}
