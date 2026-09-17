using SalekhPos.Accounting.Contracts.Journals;
using SalekhPos.Accounting.Domain.Journals;

namespace SalekhPos.Accounting.Application.Journals;

public sealed record AccountingIdentity(string Issuer, string Subject)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer.Length > 2048 || Issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(Subject) || Subject.Length > 256 || Subject.Any(char.IsControl))
            throw new ArgumentException("Accounting identity is invalid.");
    }
}

public interface IAccountingReader
{
    Task<AccountingSummaryResponse> ReadSummaryAsync(AccountingIdentity identity,
        Guid organizationId, Guid branchId, AccountingWindow window, CancellationToken cancellationToken);

    Task<AccountingJournalPage> ReadJournalAsync(AccountingIdentity identity,
        Guid organizationId, Guid branchId, AccountingWindow window, int pageSize,
        string? cursor, CancellationToken cancellationToken);
}

public sealed class AccountingDeniedException : Exception;
public sealed class AccountingUnavailableException : Exception;
