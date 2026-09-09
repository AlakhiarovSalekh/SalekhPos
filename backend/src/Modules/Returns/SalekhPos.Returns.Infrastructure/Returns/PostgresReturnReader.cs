using Npgsql;
using NpgsqlTypes;
using SalekhPos.Returns.Application.Returns;
using SalekhPos.Returns.Contracts.Returns;

namespace SalekhPos.Returns.Infrastructure.Returns;

public sealed class PostgresReturnReader(NpgsqlDataSource? source) : IReturnReader
{
    public async Task<ReturnPage> ListAsync(ReturnIdentity identity, Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        Validate(organizationId, branchId, after, pageSize);
        var dataSource = source ?? throw new ReturnsUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Context(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity, cancellationToken);
        await using var query = new NpgsqlCommand("SELECT return_id,sale_id,branch_id,currency,amount,reason,completed_at FROM returns.completed_returns WHERE organization_id=$1 AND branch_id=$2 AND ($3::uuid IS NULL OR return_id>$3) ORDER BY return_id LIMIT $4", connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)after ?? DBNull.Value });
        query.Parameters.AddWithValue(pageSize + 1);
        var items = new List<ReturnSummaryResponse>(pageSize + 1);
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1),
                reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4), reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        Guid? next = null;
        if (items.Count > pageSize) { items.RemoveAt(pageSize); next = items[^1].Id; }
        await transaction.CommitAsync(cancellationToken);
        return new(items.AsReadOnly(), next);
    }

    public async Task<CompletedReturnResponse?> ReadAsync(ReturnIdentity identity, Guid organizationId, Guid branchId,
        Guid returnId, CancellationToken cancellationToken)
    {
        Validate(organizationId, branchId, returnId, 1);
        var dataSource = source ?? throw new ReturnsUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Context(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, identity, cancellationToken);
        var result = await Read(connection, transaction, organizationId, branchId, returnId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static void Validate(Guid organizationId, Guid branchId, Guid? value, int pageSize)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || value == Guid.Empty
            || pageSize is < 1 or > 100) throw new ArgumentException("Return query is invalid.");
    }

    private static async Task Context(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId,
        ReturnIdentity identity, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identity.Issuer) || string.IsNullOrWhiteSpace(identity.Subject))
            throw new ArgumentException("Identity is invalid.");
        await using var query = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t);
        query.Parameters.AddWithValue(organizationId.ToString()); query.Parameters.AddWithValue(identity.Issuer);
        query.Parameters.AddWithValue(identity.Subject); await query.ExecuteNonQueryAsync(ct);
    }

    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid branchId,
        ReturnIdentity identity, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2 JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id JOIN organization.organizations o ON o.organization_id=b.organization_id WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active AND b.is_active AND z.is_active AND o.is_active AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission='sales.refund' AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id) OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id) OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))", c, t);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        query.Parameters.AddWithValue(identity.Issuer); query.Parameters.AddWithValue(identity.Subject);
        if (await query.ExecuteScalarAsync(ct) is not true) throw new ReturnDeniedException();
    }

    private static async Task<CompletedReturnResponse?> Read(NpgsqlConnection c, NpgsqlTransaction t, Guid org,
        Guid branch, Guid id, CancellationToken ct)
    {
        Guid sale; string currency; decimal amount; string reason; DateTimeOffset at;
        await using (var query = new NpgsqlCommand("SELECT sale_id,currency,amount,reason,completed_at FROM returns.completed_returns WHERE organization_id=$1 AND branch_id=$2 AND return_id=$3", c, t))
        {
            query.Parameters.AddWithValue(org); query.Parameters.AddWithValue(branch); query.Parameters.AddWithValue(id);
            await using var reader = await query.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) return null;
            sale = reader.GetGuid(0); currency = reader.GetString(1); amount = reader.GetDecimal(2);
            reason = reader.GetString(3); at = reader.GetFieldValue<DateTimeOffset>(4);
        }
        var lines = new List<ReturnedLineResponse>();
        await using (var query = new NpgsqlCommand("SELECT line_number,product_id,quantity,amount FROM returns.return_lines WHERE organization_id=$1 AND return_id=$2 ORDER BY line_number", c, t))
        {
            query.Parameters.AddWithValue(org); query.Parameters.AddWithValue(id); await using var reader = await query.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) lines.Add(new(reader.GetInt32(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetDecimal(3)));
        }
        return new(id, sale, branch, currency, amount, reason, at, lines.AsReadOnly());
    }
}
