using SalekhPos.Sales.Contracts.CompleteSale;

namespace SalekhPos.Sales.Application.CompleteSale;

public sealed record SalesIdentity
{
    public string Issuer { get; }
    public string Subject { get; }
    public SalesIdentity(string issuer, string subject)
    {
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 2048 || issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(subject) || subject.Length > 256 || subject.Any(char.IsControl))
            throw new ArgumentException("Identity is invalid.");
        Issuer = issuer;
        Subject = subject;
    }
}

public sealed record CompleteCashSaleCommand(Guid OrganizationId, Guid BranchId, Guid SaleId, Guid OperationId,
    IReadOnlyList<CompleteCashSaleLineRequest> Lines, decimal CashReceived);
public sealed record CashSaleWriteResult(CompletedSaleResponse Sale, bool Created);
public interface ICashSaleCompletion
{
    Task<CashSaleWriteResult> CompleteAsync(SalesIdentity identity, CompleteCashSaleCommand command,
        CancellationToken cancellationToken);
}
public interface ISaleReader
{
    Task<CompletedSaleResponse?> ReadAsync(SalesIdentity identity, Guid organizationId, Guid branchId,
        Guid saleId, CancellationToken cancellationToken);
    Task<SalePage> ListAsync(SalesIdentity identity, Guid organizationId, Guid branchId, int pageSize,
        Guid? after, CancellationToken cancellationToken);
}
public sealed class SalesDeniedException : Exception;
public sealed class SalesConflictException : Exception;
public sealed class InsufficientStockException : Exception;
public sealed class SalePriceUnavailableException : Exception;
public sealed class SalesUnavailableException : Exception;
