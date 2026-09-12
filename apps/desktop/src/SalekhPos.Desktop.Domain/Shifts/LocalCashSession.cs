namespace SalekhPos.Desktop.Domain.Shifts;

public sealed record LocalCashSession
{
    public Guid OrganizationId { get; }
    public Guid BranchId { get; }
    public Guid DeviceId { get; }
    public Guid RegisterId { get; }
    public Guid ShiftId { get; }
    public string Currency { get; }
    public decimal OpeningBalance { get; }
    public DateTimeOffset OpenedAt { get; }
    public DateTimeOffset RefreshedAt { get; }

    public LocalCashSession(Guid organizationId, Guid branchId, Guid deviceId, Guid registerId, Guid shiftId,
        string currency, decimal openingBalance, DateTimeOffset openedAt, DateTimeOffset refreshedAt)
    {
        if (new[] { organizationId, branchId, deviceId, registerId, shiftId }.Any(x => x == Guid.Empty))
            throw new ArgumentException("Cash-session identity is invalid.");
        if (currency is null || currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Cash-session currency is invalid.");
        if (openingBalance < 0 || decimal.Round(openingBalance, 6) != openingBalance)
            throw new ArgumentOutOfRangeException(nameof(openingBalance));
        if (openedAt == default || openedAt.Offset != TimeSpan.Zero || refreshedAt == default
            || refreshedAt.Offset != TimeSpan.Zero || refreshedAt < openedAt)
            throw new ArgumentException("Cash-session timestamps are invalid.");
        OrganizationId = organizationId; BranchId = branchId; DeviceId = deviceId; RegisterId = registerId;
        ShiftId = shiftId; Currency = currency; OpeningBalance = openingBalance; OpenedAt = openedAt;
        RefreshedAt = refreshedAt;
    }
}
