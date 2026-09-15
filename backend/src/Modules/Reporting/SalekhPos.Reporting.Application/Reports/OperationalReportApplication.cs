using SalekhPos.Reporting.Contracts.Reports;
using SalekhPos.Reporting.Domain.SalesReports;

namespace SalekhPos.Reporting.Application.Reports;

public sealed record ReportingIdentity
{
    public string Issuer { get; }
    public string Subject { get; }

    public ReportingIdentity(string issuer, string subject)
    {
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 2048 || issuer.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(subject) || subject.Length > 256 || subject.Any(char.IsControl))
            throw new ArgumentException("Reporting identity is invalid.");
        Issuer = issuer;
        Subject = subject;
    }
}

public interface IOperationalReportService
{
    Task<OperationalSummaryResponse> ReadSummaryAsync(ReportingIdentity identity, Guid organizationId,
        Guid branchId, ReportWindow window, CancellationToken cancellationToken);
}

public sealed class ReportingDeniedException : Exception;
public sealed class ReportingUnavailableException : Exception;
