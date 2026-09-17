using System.Globalization;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Accounting.Application.Journals;
using SalekhPos.Accounting.Contracts.Journals;
using SalekhPos.Accounting.Domain.Journals;

namespace SalekhPos.Accounting.Infrastructure.Journals;

public sealed class PostgresAccountingReader(NpgsqlDataSource? source) : IAccountingReader
{
    private sealed record JournalCursor(DateTimeOffset At, string Kind, Guid Id);

    public async Task<AccountingSummaryResponse> ReadSummaryAsync(AccountingIdentity identity,
        Guid organizationId, Guid branchId, AccountingWindow window, CancellationToken cancellationToken)
    {
        Validate(identity, organizationId, branchId);
        var dataSource = source ?? throw new AccountingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity, cancellationToken);
        var currency = await ReadCurrency(connection, transaction, organizationId, branchId,
            window, cancellationToken);
        var result = await ReadSummaryCore(connection, transaction, organizationId, branchId,
            window, currency, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<AccountingJournalPage> ReadJournalAsync(AccountingIdentity identity,
        Guid organizationId, Guid branchId, AccountingWindow window, int pageSize,
        string? cursor, CancellationToken cancellationToken)
    {
        Validate(identity, organizationId, branchId);
        if (pageSize is < 1 or > 100) throw new ArgumentException("Accounting page size is invalid.");
        var parsedCursor = ParseCursor(cursor);
        var dataSource = source ?? throw new AccountingUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity, cancellationToken);
        var items = await ReadJournalCore(connection, transaction, organizationId, branchId,
            window, pageSize + 1, parsedCursor, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        string? next = null;
        if (items.Count > pageSize)
        {
            items.RemoveAt(pageSize);
            var last = items[^1];
            next = EncodeCursor(new(last.OccurredAt, last.Kind, last.SourceId));
        }
        return new(items.AsReadOnly(), next);
    }

    private static async Task<AccountingSummaryResponse> ReadSummaryCore(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId,
        Guid branchId, AccountingWindow window, string? currency, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT
              (SELECT count(*)::int FROM sales.completed_sales
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),
              COALESCE((SELECT sum(net_total) FROM sales.completed_sales
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),0),
              COALESCE((SELECT sum(tax_total) FROM sales.completed_sales
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),0),
              COALESCE((SELECT sum(grand_total) FROM sales.completed_sales
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),0),
              (SELECT count(*)::int FROM returns.completed_returns
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),
              COALESCE((SELECT sum(amount) FROM returns.completed_returns
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4),0)
              + COALESCE((SELECT sum(amount) FROM sales.sale_voids
                WHERE organization_id=$1 AND branch_id=$2 AND voided_at >= $3 AND voided_at < $4),0),
              COALESCE((SELECT sum(amount) FROM shifts.cash_movements
                WHERE organization_id=$1 AND branch_id=$2 AND kind='cash_in'
                  AND recorded_at >= $3 AND recorded_at < $4),0),
              COALESCE((SELECT sum(amount) FROM shifts.cash_movements
                WHERE organization_id=$1 AND branch_id=$2 AND kind='cash_out'
                  AND recorded_at >= $3 AND recorded_at < $4),0),
              (SELECT count(*)::int FROM purchasing.purchase_orders
                WHERE organization_id=$1 AND branch_id=$2 AND status='approved'
                  AND updated_at >= $3 AND updated_at < $4),
              COALESCE((SELECT sum(total) FROM purchasing.purchase_orders
                WHERE organization_id=$1 AND branch_id=$2 AND status='approved'
                  AND updated_at >= $3 AND updated_at < $4),0),
              (SELECT count(*)::int FROM shifts.shifts
                WHERE organization_id=$1 AND branch_id=$2 AND status='closed'
                  AND closed_at >= $3 AND closed_at < $4),
              COALESCE((SELECT sum(variance) FROM shifts.shifts
                WHERE organization_id=$1 AND branch_id=$2 AND status='closed'
                  AND closed_at >= $3 AND closed_at < $4),0)
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(window.From);
        query.Parameters.AddWithValue(window.To);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new AccountingUnavailableException();
        var salesGross = reader.GetDecimal(3);
        var refunds = reader.GetDecimal(5);
        return new(branchId, window.From, window.To, currency,
            reader.GetInt32(0), reader.GetDecimal(1), reader.GetDecimal(2), salesGross,
            reader.GetInt32(4), refunds, salesGross - refunds,
            reader.GetDecimal(6), reader.GetDecimal(7), reader.GetInt32(8),
            reader.GetDecimal(9), reader.GetInt32(10), reader.GetDecimal(11));
    }

    private static async Task<List<AccountingJournalItemResponse>> ReadJournalCore(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId,
        Guid branchId, AccountingWindow window, int limit, JournalCursor? cursor,
        CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            WITH events AS (
              SELECT sale_id source_id,'sale'::text kind,completed_at occurred_at,currency,
                     net_total::numeric net_amount,tax_total::numeric tax_amount,
                     grand_total::numeric gross_amount,grand_total::numeric cash_effect
              FROM sales.completed_sales
              WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4
              UNION ALL
              SELECT return_id,'return',completed_at,currency,
                     NULL::numeric,NULL::numeric,amount::numeric,-amount::numeric
              FROM returns.completed_returns
              WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4
              UNION ALL
              SELECT void_id,'sale_void',voided_at,currency,
                     NULL::numeric,NULL::numeric,amount::numeric,-amount::numeric
              FROM sales.sale_voids
              WHERE organization_id=$1 AND branch_id=$2 AND voided_at >= $3 AND voided_at < $4
              UNION ALL
              SELECT movement_id,kind,recorded_at,currency,
                     NULL::numeric,NULL::numeric,amount::numeric,
                     CASE WHEN kind='cash_in' THEN amount ELSE -amount END
              FROM shifts.cash_movements
              WHERE organization_id=$1 AND branch_id=$2 AND recorded_at >= $3 AND recorded_at < $4
              UNION ALL
              SELECT order_id,'purchase_commitment',updated_at,currency,
                     NULL::numeric,NULL::numeric,total::numeric,0::numeric
              FROM purchasing.purchase_orders
              WHERE organization_id=$1 AND branch_id=$2 AND status='approved'
                AND updated_at >= $3 AND updated_at < $4
            )
            SELECT source_id,kind,occurred_at,currency,net_amount,tax_amount,gross_amount,cash_effect
            FROM events
            WHERE $5::timestamptz IS NULL
              OR (occurred_at,kind,source_id) < ($5::timestamptz,$6::text,$7::uuid)
            ORDER BY occurred_at DESC,kind DESC,source_id DESC
            LIMIT $8
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(window.From);
        query.Parameters.AddWithValue(window.To);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.TimestampTz, Value = (object?)cursor?.At ?? DBNull.Value });
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)cursor?.Kind ?? DBNull.Value });
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)cursor?.Id ?? DBNull.Value });
        query.Parameters.AddWithValue(limit);
        var items = new List<AccountingJournalItemResponse>(limit);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7)));
        return items;
    }

    private static async Task<string?> ReadCurrency(NpgsqlConnection connection,
        NpgsqlTransaction transaction, Guid organizationId, Guid branchId,
        AccountingWindow window, CancellationToken cancellationToken)
    {
        await using var query = new NpgsqlCommand("""
            SELECT DISTINCT currency FROM (
              SELECT currency FROM sales.completed_sales
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4
              UNION SELECT currency FROM returns.completed_returns
                WHERE organization_id=$1 AND branch_id=$2 AND completed_at >= $3 AND completed_at < $4
              UNION SELECT currency FROM sales.sale_voids
                WHERE organization_id=$1 AND branch_id=$2 AND voided_at >= $3 AND voided_at < $4
              UNION SELECT currency FROM shifts.cash_movements
                WHERE organization_id=$1 AND branch_id=$2 AND recorded_at >= $3 AND recorded_at < $4
              UNION SELECT currency FROM purchasing.purchase_orders
                WHERE organization_id=$1 AND branch_id=$2 AND updated_at >= $3 AND updated_at < $4
            ) currencies LIMIT 2
            """, connection, transaction);
        query.Parameters.AddWithValue(organizationId);
        query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(window.From);
        query.Parameters.AddWithValue(window.To);
        var currencies = new List<string>(2);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) currencies.Add(reader.GetString(0));
        if (currencies.Count > 1) throw new AccountingUnavailableException();
        return currencies.Count == 0 ? null : currencies[0];
    }

    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, AccountingIdentity identity, CancellationToken cancellationToken)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime' AND NOT EXISTS(
              SELECT 1 FROM (VALUES
                ('sales','completed_sales'),('sales','sale_voids'),('returns','completed_returns'),
                ('shifts','cash_movements'),('purchasing','purchase_orders')) required(schema_name,table_name)
              LEFT JOIN pg_namespace n ON n.nspname=required.schema_name
              LEFT JOIN pg_class c ON c.relnamespace=n.oid AND c.relname=required.table_name
              WHERE c.oid IS NULL OR NOT c.relrowsecurity OR NOT c.relforcerowsecurity
                OR c.relowner=(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(cancellationToken) is not true)
            throw new AccountingUnavailableException();
        await using var context = new NpgsqlCommand(
            "SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)",
            connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString());
        context.Parameters.AddWithValue(identity.Issuer);
        context.Parameters.AddWithValue(identity.Subject);
        await context.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid branchId, AccountingIdentity identity, CancellationToken cancellationToken)
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
                AND g.permission='accounting.view'
                AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id)
                  OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id)
                  OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))
            """, connection, transaction);
        demand.Parameters.AddWithValue(organizationId);
        demand.Parameters.AddWithValue(branchId);
        demand.Parameters.AddWithValue(identity.Issuer);
        demand.Parameters.AddWithValue(identity.Subject);
        if (await demand.ExecuteScalarAsync(cancellationToken) is not true)
            throw new AccountingDeniedException();
    }

    private static string EncodeCursor(JournalCursor cursor)
    {
        var text = string.Create(CultureInfo.InvariantCulture,
            $"{cursor.At:O}|{cursor.Kind}|{cursor.Id:D}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static JournalCursor? ParseCursor(string? cursor)
    {
        if (cursor is null) return null;
        if (cursor.Length is < 8 or > 256 || cursor.Any(char.IsControl))
            throw new ArgumentException("Accounting cursor is invalid.");
        try
        {
            var normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized += new string('=', (4 - normalized.Length % 4) % 4);
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            var parts = text.Split('|');
            if (parts.Length != 3
                || !DateTimeOffset.TryParseExact(parts[0], "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var at)
                || at.Offset != TimeSpan.Zero
                || parts[1] is not ("sale" or "return" or "sale_void" or "cash_in" or "cash_out" or "purchase_commitment")
                || !Guid.TryParseExact(parts[2], "D", out var id) || id == Guid.Empty)
                throw new ArgumentException("Accounting cursor is invalid.");
            return new(at, parts[1], id);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Accounting cursor is invalid.");
        }
    }

    private static void Validate(AccountingIdentity identity, Guid organizationId, Guid branchId)
    {
        ArgumentNullException.ThrowIfNull(identity);
        identity.Validate();
        if (organizationId == Guid.Empty || branchId == Guid.Empty)
            throw new ArgumentException("Accounting scope is invalid.");
    }
}
