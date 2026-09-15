using Npgsql;
using SalekhPos.Reporting.Application.Reports;
using SalekhPos.Reporting.Contracts.Reports;
using SalekhPos.Reporting.Domain.SalesReports;

namespace SalekhPos.Reporting.Infrastructure.Reports;

public sealed class PostgresOperationalReportService(NpgsqlDataSource? source) : IOperationalReportService
{
    public async Task<OperationalSummaryResponse> ReadSummaryAsync(ReportingIdentity identity,
        Guid organizationId, Guid branchId, ReportWindow window, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(window);
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Reporting scope is invalid.");
        var dataSource = source ?? throw new ReportingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await SetContext(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity, cancellationToken);
        var currency = await ReadCurrency(connection, transaction, organizationId, branchId,
            window, cancellationToken);
        await using var query = new NpgsqlCommand("""
            SELECT
              (SELECT count(*)::int FROM sales.completed_sales
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),
              COALESCE((SELECT sum(grand_total) FROM sales.completed_sales
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),0),
              (SELECT count(*)::int FROM returns.completed_returns
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),
              COALESCE((SELECT sum(amount) FROM returns.completed_returns
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),0),
              (SELECT count(*)::int FROM purchasing.purchase_orders
                WHERE organization_id=$1 AND branch_id=$2 AND created_at >= $3 AND created_at < $4),
              COALESCE((SELECT sum(total) FROM purchasing.purchase_orders
                WHERE organization_id=$1 AND branch_id=$2 AND created_at >= $3 AND created_at < $4),0),
              (SELECT count(*)::int FROM shifts.shifts
                WHERE organization_id=$1 AND branch_id=$2 AND status='open')
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(window.From);
        query.Parameters.AddWithValue(window.To);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new ReportingUnavailableException();
        var completedSales = reader.GetInt32(0);
        var grossSales = reader.GetDecimal(1);
        var completedReturns = reader.GetInt32(2);
        var refunds = reader.GetDecimal(3);
        var purchaseOrders = reader.GetInt32(4);
        var purchaseValue = reader.GetDecimal(5);
        var openShifts = reader.GetInt32(6);
        await transaction.CommitAsync(cancellationToken);
        return new(branchId, window.From, window.To, currency, completedSales, grossSales,
            completedReturns, refunds, grossSales - refunds, purchaseOrders, purchaseValue, openShifts);
    }

    private static async Task SetContext(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, ReportingIdentity identity, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        command.Parameters.AddWithValue(organizationId.ToString());
        command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, ReportingIdentity identity, CancellationToken cancellationToken)
    {
        await using var demand = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m
              JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id
              JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2
              JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id
              JOIN organization.organizations o ON o.organization_id=b.organization_id
              WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active
                AND b.is_active AND z.is_active AND o.is_active
                AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                AND g.permission='reports.view'
                AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id)
                  OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id)
                  OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))
            """, connection, transaction);
        demand.Parameters.AddWithValue(organizationId);
        demand.Parameters.AddWithValue(branchId);
        demand.Parameters.AddWithValue(identity.Issuer);
        demand.Parameters.AddWithValue(identity.Subject);
        if (await demand.ExecuteScalarAsync(cancellationToken) is not true) throw new ReportingDeniedException();
    }
    private static async Task<string?> ReadCurrency(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, ReportWindow window, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT DISTINCT currency FROM (
              SELECT currency FROM sales.completed_sales
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4
              UNION SELECT currency FROM returns.completed_returns
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4
              UNION SELECT currency FROM purchasing.purchase_orders
                WHERE organization_id=$1 AND branch_id=$2 AND created_at >= $3 AND created_at < $4
            ) currencies LIMIT 2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(window.From);
        query.Parameters.AddWithValue(window.To);
        var currencies = new List<string>(2);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) currencies.Add(reader.GetString(0));
        if (currencies.Count > 1) throw new ReportingUnavailableException();
        return currencies.Count == 0 ? null : currencies[0];
    }
}
