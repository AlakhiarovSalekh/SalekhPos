namespace SalekhPos.Purchasing.Domain.PurchaseOrders;

public sealed record PurchaseOrderLine
{
    public Guid ProductId { get; }
    public decimal Quantity { get; }
    public decimal UnitCost { get; }

    public PurchaseOrderLine(Guid productId, decimal quantity, decimal unitCost)
    {
        if (productId == Guid.Empty || quantity <= 0 || unitCost < 0
            || decimal.Round(quantity, 6) != quantity || decimal.Round(unitCost, 6) != unitCost)
            throw new ArgumentException("Purchase order line is invalid.");
        ProductId = productId;
        Quantity = quantity;
        UnitCost = unitCost;
    }

    public decimal LineTotal
    {
        get
        {
            try { return checked(Quantity * UnitCost); }
            catch (OverflowException) { throw new ArgumentException("Purchase order line total is invalid."); }
        }
    }
}

public sealed class PurchaseOrder
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public Guid BranchId { get; }
    public Guid SupplierId { get; }
    public string Currency { get; }
    public string? Reference { get; }
    public IReadOnlyList<PurchaseOrderLine> Lines { get; }
    public decimal Total { get; }

    public PurchaseOrder(Guid organizationId, Guid id, Guid branchId, Guid supplierId,
        string currency, string? reference, IEnumerable<PurchaseOrderLine> lines)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty || branchId == Guid.Empty || supplierId == Guid.Empty)
            throw new ArgumentException("Purchase order identifiers are required.");
        if (currency is null || currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Purchase order currency is invalid.");
        reference = NormalizeReference(reference);
        var materialized = lines?.ToArray() ?? throw new ArgumentNullException(nameof(lines));
        if (materialized.Length is < 1 or > 500 || materialized.Select(x => x.ProductId).Distinct().Count() != materialized.Length)
            throw new ArgumentException("Purchase order lines are invalid.");
        decimal total = 0;
        try { foreach (var line in materialized) total = checked(total + line.LineTotal); }
        catch (OverflowException) { throw new ArgumentException("Purchase order total is invalid."); }
        OrganizationId = organizationId; Id = id; BranchId = branchId; SupplierId = supplierId;
        Currency = currency; Reference = reference; Lines = Array.AsReadOnly(materialized); Total = total;
    }

    private static string? NormalizeReference(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        if (trimmed.Length is < 1 or > 100 || trimmed.Any(char.IsControl))
            throw new ArgumentException("Purchase order reference is invalid.");
        return trimmed;
    }
}
