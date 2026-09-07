using System.Numerics;

namespace SalekhPos.SharedKernel.Money;

/// <summary>
/// Exact monetary amount. Negative values are allowed for accounting reversals.
/// Currency syntax is checked here; supported currencies and settlement rounding
/// belong to a versioned business policy. This type never rounds implicitly.
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        if (currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z'))
        {
            throw new ArgumentException("Currency must contain exactly three uppercase ASCII letters.", nameof(currency));
        }

        Amount = amount;
        Currency = currency;
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        var result = checked(Amount + other.Amount);
        EnsureExactResult(Amount, other.Amount, result, subtract: false);
        return new Money(result, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        var result = checked(Amount - other.Amount);
        EnsureExactResult(Amount, other.Amount, result, subtract: true);
        return new Money(result, Currency);
    }

    // Decimal arithmetic can silently lose low-order digits near its precision
    // limit, even in a checked block. Compare against an exact integer calculation.
    private static void EnsureExactResult(decimal left, decimal right, decimal result, bool subtract)
    {
        var (leftValue, leftScale) = Decompose(left);
        var (rightValue, rightScale) = Decompose(right);
        var (resultValue, resultScale) = Decompose(result);
        var scale = Math.Max(Math.Max(leftScale, rightScale), resultScale);
        var expected = leftValue * BigInteger.Pow(10, scale - leftScale)
            + (subtract ? -rightValue : rightValue) * BigInteger.Pow(10, scale - rightScale);
        var actual = resultValue * BigInteger.Pow(10, scale - resultScale);
        if (expected != actual)
        {
            throw new ArithmeticException("The result cannot be represented exactly as a decimal amount.");
        }
    }

    private static (BigInteger Value, int Scale) Decompose(decimal amount)
    {
        var bits = decimal.GetBits(amount);
        var value = (BigInteger)(uint)bits[0]
            + ((BigInteger)(uint)bits[1] << 32)
            + ((BigInteger)(uint)bits[2] << 64);
        return (bits[3] < 0 ? -value : value, (bits[3] >> 16) & 0xFF);
    }

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException("Amounts in different currencies cannot be combined.");
        }
    }
}
