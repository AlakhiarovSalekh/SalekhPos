namespace SalekhPos.Purchasing.Domain.SupplierInvoices;

public enum SupplierInvoiceStatus
{
    Draft,
    Posted,
    Cancelled
}

public sealed record SupplierInvoiceLine(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost,
    decimal LineTotal)
{
    public static SupplierInvoiceLine Create(Guid productId, decimal quantity, decimal unitCost)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Product is required.", nameof(productId));
        if (quantity <= 0m || decimal.Round(quantity, 6) != quantity)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive with at most 6 decimals.");
        if (unitCost < 0m || decimal.Round(unitCost, 6) != unitCost)
            throw new ArgumentOutOfRangeException(nameof(unitCost), "Unit cost cannot be negative and must have at most 6 decimals.");

        var lineTotal = decimal.Round(quantity * unitCost, 6, MidpointRounding.AwayFromZero);
        return new(productId, quantity, unitCost, lineTotal);
    }
}

public sealed class SupplierInvoice
{
    private readonly List<SupplierInvoiceLine> lines;

    private SupplierInvoice(
        Guid id,
        Guid branchId,
        Guid supplierId,
        Guid? purchaseOrderId,
        string invoiceNumber,
        string currency,
        DateOnly invoiceDate,
        DateOnly? dueDate,
        IReadOnlyList<SupplierInvoiceLine> lines)
    {
        Id = id;
        BranchId = branchId;
        SupplierId = supplierId;
        PurchaseOrderId = purchaseOrderId;
        InvoiceNumber = invoiceNumber;
        Currency = currency;
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        this.lines = [.. lines];
        Status = SupplierInvoiceStatus.Draft;
        Version = 1;
    }

    public Guid Id { get; }
    public Guid BranchId { get; }
    public Guid SupplierId { get; }
    public Guid? PurchaseOrderId { get; }
    public string InvoiceNumber { get; }
    public string Currency { get; }
    public DateOnly InvoiceDate { get; }
    public DateOnly? DueDate { get; }
    public SupplierInvoiceStatus Status { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyList<SupplierInvoiceLine> Lines => lines;
    public decimal Total => lines.Sum(line => line.LineTotal);

    public static SupplierInvoice CreateDraft(
        Guid id,
        Guid branchId,
        Guid supplierId,
        Guid? purchaseOrderId,
        string invoiceNumber,
        string currency,
        DateOnly invoiceDate,
        DateOnly? dueDate,
        IEnumerable<SupplierInvoiceLine> lines)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invoice id is required.", nameof(id));
        if (branchId == Guid.Empty) throw new ArgumentException("Branch is required.", nameof(branchId));
        if (supplierId == Guid.Empty) throw new ArgumentException("Supplier is required.", nameof(supplierId));
        if (purchaseOrderId == Guid.Empty) throw new ArgumentException("Purchase order id is invalid.", nameof(purchaseOrderId));
        if (string.IsNullOrWhiteSpace(invoiceNumber) || invoiceNumber.Trim().Length > 100)
            throw new ArgumentException("Invoice number is required and cannot exceed 100 characters.", nameof(invoiceNumber));

        var normalizedCurrency = currency?.Trim().ToUpperInvariant();
        if (normalizedCurrency is null || normalizedCurrency.Length != 3
            || normalizedCurrency.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Currency must be a three-letter ISO-style code.", nameof(currency));

        if (dueDate.HasValue && dueDate.Value < invoiceDate)
            throw new ArgumentException("Due date cannot be before invoice date.", nameof(dueDate));

        var materializedLines = lines?.ToArray() ?? throw new ArgumentNullException(nameof(lines));
        if (materializedLines.Length is < 1 or > 500)
            throw new ArgumentException("Supplier invoice must contain between 1 and 500 lines.", nameof(lines));
        if (materializedLines.Select(line => line.ProductId).Distinct().Count() != materializedLines.Length)
            throw new ArgumentException("Supplier invoice cannot contain duplicate products.", nameof(lines));

        return new(
            id,
            branchId,
            supplierId,
            purchaseOrderId,
            invoiceNumber.Trim(),
            normalizedCurrency,
            invoiceDate,
            dueDate,
            materializedLines);
    }
}
