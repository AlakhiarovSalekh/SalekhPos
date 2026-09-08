namespace SalekhPos.Inventory.Domain.StockMovements;

public enum StockMovementKind { Receipt, AdjustmentIn, AdjustmentOut, Sale, Return }

public sealed record StockMovement
{
    public Guid OrganizationId { get; }
    public Guid BranchId { get; }
    public Guid ProductId { get; }
    public Guid Id { get; }
    public StockMovementKind Kind { get; }
    public decimal Quantity { get; }
    public string? Reason { get; }
    public DateTimeOffset OccurredAt { get; }
    public int Direction => Kind is StockMovementKind.AdjustmentOut or StockMovementKind.Sale ? -1 : 1;

    public StockMovement(Guid organizationId, Guid branchId, Guid productId, Guid id,
        StockMovementKind kind, decimal quantity, string? reason, DateTimeOffset occurredAt)
    {
        OrganizationId = Required(organizationId, nameof(organizationId));
        BranchId = Required(branchId, nameof(branchId));
        ProductId = Required(productId, nameof(productId));
        Id = Required(id, nameof(id));
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Movement kind is invalid.", nameof(kind));
        if (quantity <= 0 || quantity > 99999999999999.999999m || decimal.Round(quantity, 6) != quantity)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive with at most six decimal places.");
        if (reason is not null && (reason.Length is < 1 or > 200 || reason != reason.Trim()
            || reason.Any(char.IsControl) || reason.Any(char.IsSurrogate)))
            throw new ArgumentException("Movement reason is invalid.", nameof(reason));
        if (occurredAt == default || occurredAt.Offset != TimeSpan.Zero || occurredAt > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new ArgumentException("Movement time must be a valid UTC instant.", nameof(occurredAt));
        Quantity = quantity;
        Kind = kind;
        Reason = reason;
        // PostgreSQL timestamptz stores microseconds. Canonicalize before persistence so
        // an idempotent replay compares the same instant that the database returns.
        OccurredAt = new DateTimeOffset(occurredAt.Ticks - occurredAt.Ticks % 10, TimeSpan.Zero);
    }

    private static Guid Required(Guid value, string parameter) => value == Guid.Empty
        ? throw new ArgumentException("ID must not be empty.", parameter) : value;
}
