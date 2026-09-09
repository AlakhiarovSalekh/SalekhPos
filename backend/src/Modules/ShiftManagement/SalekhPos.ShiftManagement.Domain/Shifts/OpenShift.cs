namespace SalekhPos.ShiftManagement.Domain.Shifts;

public sealed record OpenShift
{
    public OpenShift(Guid id, Guid registerId, string currency, decimal openingBalance)
    { if (id == Guid.Empty || registerId == Guid.Empty) throw new ArgumentException("Shift identifiers are required."); if (currency is null || currency.Length != 3 || currency.Any(c => !char.IsAsciiLetterUpper(c))) throw new ArgumentException("Shift currency is invalid."); if (openingBalance < 0 || decimal.Round(openingBalance, 6) != openingBalance) throw new ArgumentOutOfRangeException(nameof(openingBalance)); Id = id; RegisterId = registerId; Currency = currency; OpeningBalance = openingBalance; }
    public Guid Id { get; }
    public Guid RegisterId { get; }
    public string Currency { get; }
    public decimal OpeningBalance { get; }
}
