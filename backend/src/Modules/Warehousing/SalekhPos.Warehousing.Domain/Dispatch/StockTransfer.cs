namespace SalekhPos.Warehousing.Domain.Dispatch;

public sealed record StockTransferLine
{
    public Guid ProductId { get; }
    public decimal Quantity { get; }

    public StockTransferLine(Guid productId, decimal quantity)
    {
        if (productId == Guid.Empty || quantity <= 0 || decimal.Round(quantity, 6) != quantity)
            throw new ArgumentException("Stock transfer line is invalid.");
        ProductId = productId;
        Quantity = quantity;
    }
}

public sealed class StockTransfer
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public Guid SourceBranchId { get; }
    public Guid DestinationBranchId { get; }
    public string? Reference { get; }
    public IReadOnlyList<StockTransferLine> Lines { get; }

    public StockTransfer(Guid organizationId, Guid id, Guid sourceBranchId, Guid destinationBranchId,
        string? reference, IEnumerable<StockTransferLine> lines)
    {
        if (organizationId == Guid.Empty || id == Guid.Empty || sourceBranchId == Guid.Empty
            || destinationBranchId == Guid.Empty || sourceBranchId == destinationBranchId)
            throw new ArgumentException("Stock transfer identifiers are invalid.");
        reference = NormalizeReference(reference);
        var materialized = lines?.ToArray() ?? throw new ArgumentNullException(nameof(lines));
        if (materialized.Length is < 1 or > 500
            || materialized.Select(line => line.ProductId).Distinct().Count() != materialized.Length)
            throw new ArgumentException("Stock transfer lines are invalid.");
        OrganizationId = organizationId;
        Id = id;
        SourceBranchId = sourceBranchId;
        DestinationBranchId = destinationBranchId;
        Reference = reference;
        Lines = Array.AsReadOnly(materialized);
    }

    private static string? NormalizeReference(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 120 || value.Any(char.IsControl))
            throw new ArgumentException("Stock transfer reference is invalid.");
        return value;
    }
}
