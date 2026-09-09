using Npgsql;
using SalekhPos.Sales.Application.Carts;
using SalekhPos.Sales.Contracts.Carts;
using SalekhPos.Sales.Domain.Carts;

namespace SalekhPos.Sales.Infrastructure.Carts;

public sealed class PostgresSuspendedCartService(NpgsqlDataSource? source) : ISuspendedCartService
{
    public async Task<CartWriteResult> SuspendAsync(string issuer, string subject, SuspendCartCommand command,
        CancellationToken cancellationToken)
    {
        var items = Validate(issuer, subject, command);
        var dataSource = source ?? throw new SalesCartUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Context(connection, transaction, command.OrganizationId, issuer, subject, cancellationToken);
        await Demand(connection, transaction, command.OrganizationId, command.BranchId, issuer, subject, cancellationToken);
        await Lock(connection, transaction, $"cart:{command.OrganizationId:D}:{command.OperationId:D}", cancellationToken);
        var replay = await ReadByOperation(connection, transaction, command.OrganizationId, command.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.BranchId != command.BranchId || replay.Note != Normalize(command.Note) || replay.Lines.Count != items.Count
                || replay.Lines.Any(x => !items.TryGetValue(x.ProductId, out var item) || item.Quantity != x.Quantity))
                throw new CartConflictException();
            await transaction.CommitAsync(cancellationToken); return new(replay, false);
        }
        var now = await Time(connection, transaction, cancellationToken); var expires = now.AddHours(24);
        await using (var insert = new NpgsqlCommand("INSERT INTO sales.suspended_carts(organization_id,cart_id,operation_id,branch_id,note,suspended_at,expires_at,issuer,subject) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)", connection, transaction))
        {
            insert.Parameters.AddWithValue(command.OrganizationId); insert.Parameters.AddWithValue(command.CartId);
            insert.Parameters.AddWithValue(command.OperationId); insert.Parameters.AddWithValue(command.BranchId);
            insert.Parameters.AddWithValue((object?)Normalize(command.Note) ?? DBNull.Value); insert.Parameters.AddWithValue(now);
            insert.Parameters.AddWithValue(expires); insert.Parameters.AddWithValue(issuer); insert.Parameters.AddWithValue(subject);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        var lines = new List<SuspendedCartLineResponse>(); var number = 0;
        foreach (var item in items.Values)
        {
            number++;
            await using var insert = new NpgsqlCommand("INSERT INTO sales.suspended_cart_lines(organization_id,cart_id,line_number,product_id,quantity) VALUES($1,$2,$3,$4,$5)", connection, transaction);
            insert.Parameters.AddWithValue(command.OrganizationId); insert.Parameters.AddWithValue(command.CartId);
            insert.Parameters.AddWithValue(number); insert.Parameters.AddWithValue(item.ProductId); insert.Parameters.AddWithValue(item.Quantity);
            await insert.ExecuteNonQueryAsync(cancellationToken); lines.Add(new(number, item.ProductId, item.Quantity));
        }
        await transaction.CommitAsync(cancellationToken);
        return new(new(command.CartId, command.BranchId, Normalize(command.Note), now, expires, null, lines.AsReadOnly()), true);
    }

    public async Task<SuspendedCartResponse?> ResumeAsync(string issuer, string subject, Guid organizationId,
        Guid branchId, Guid cartId, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || cartId == Guid.Empty
            || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Cart query is invalid.");
        var dataSource = source ?? throw new SalesCartUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Context(connection, transaction, organizationId, issuer, subject, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, issuer, subject, cancellationToken);
        await Lock(connection, transaction, $"cart:{organizationId:D}:{cartId:D}", cancellationToken);
        await using (var update = new NpgsqlCommand("UPDATE sales.suspended_carts SET resumed_at=statement_timestamp() WHERE organization_id=$1 AND branch_id=$2 AND cart_id=$3 AND issuer=$4 AND subject=$5 AND resumed_at IS NULL AND expires_at>statement_timestamp()", connection, transaction))
        {
            update.Parameters.AddWithValue(organizationId); update.Parameters.AddWithValue(branchId); update.Parameters.AddWithValue(cartId);
            update.Parameters.AddWithValue(issuer); update.Parameters.AddWithValue(subject);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) { await transaction.CommitAsync(cancellationToken); return null; }
        }
        var result = await ReadById(connection, transaction, organizationId, cartId, cancellationToken)
            ?? throw new SalesCartUnavailableException();
        await transaction.CommitAsync(cancellationToken); return result;
    }

    private static Dictionary<Guid, CartItem> Validate(string issuer, string subject, SuspendCartCommand command)
    {
        var note = Normalize(command.Note);
        if (command.OrganizationId == Guid.Empty || command.BranchId == Guid.Empty || command.CartId == Guid.Empty
            || command.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject)
            || note?.Length > 500 || note?.Any(char.IsControl) == true || command.Lines is null
            || command.Lines.Count is < 1 or > 500) throw new ArgumentException("Cart request is invalid.");
        var result = new Dictionary<Guid, CartItem>();
        foreach (var line in command.Lines)
        { var item = new CartItem(line.ProductId, line.Quantity); if (!result.TryAdd(item.ProductId, item)) throw new ArgumentException("Cart products must be unique."); }
        return result;
    }
    private static string? Normalize(string? value) { var result = value?.Trim(); return string.IsNullOrEmpty(result) ? null : result; }
    private static async Task Context(NpgsqlConnection c, NpgsqlTransaction t, Guid org, string issuer, string subject, CancellationToken ct)
    { await using var q = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t); q.Parameters.AddWithValue(org.ToString()); q.Parameters.AddWithValue(issuer); q.Parameters.AddWithValue(subject); await q.ExecuteNonQueryAsync(ct); }
    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid branch, string issuer, string subject, CancellationToken ct)
    { await using var q = new NpgsqlCommand("SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2 JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id JOIN organization.organizations o ON o.organization_id=b.organization_id WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active AND b.is_active AND z.is_active AND o.is_active AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission='sales.complete' AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id) OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id) OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(branch); q.Parameters.AddWithValue(issuer); q.Parameters.AddWithValue(subject); if (await q.ExecuteScalarAsync(ct) is not true) throw new CartDeniedException(); }
    private static async Task Lock(NpgsqlConnection c, NpgsqlTransaction t, string key, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", c, t); q.Parameters.AddWithValue(key); await q.ExecuteNonQueryAsync(ct); }
    private static async Task<DateTimeOffset> Time(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT statement_timestamp()", c, t); await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new SalesCartUnavailableException(); return r.GetFieldValue<DateTimeOffset>(0); }
    private static async Task<SuspendedCartResponse?> ReadByOperation(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid op, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT cart_id FROM sales.suspended_carts WHERE organization_id=$1 AND operation_id=$2", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(op); var id = await q.ExecuteScalarAsync(ct); return id is Guid cartId ? await ReadById(c, t, org, cartId, ct) : null; }
    private static async Task<SuspendedCartResponse?> ReadById(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid id, CancellationToken ct)
    { Guid branch; string? note; DateTimeOffset suspended; DateTimeOffset expires; DateTimeOffset? resumed; await using (var q = new NpgsqlCommand("SELECT branch_id,note,suspended_at,expires_at,resumed_at FROM sales.suspended_carts WHERE organization_id=$1 AND cart_id=$2", c, t)) { q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(id); await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null; branch = r.GetGuid(0); note = r.IsDBNull(1) ? null : r.GetString(1); suspended = r.GetFieldValue<DateTimeOffset>(2); expires = r.GetFieldValue<DateTimeOffset>(3); resumed = r.IsDBNull(4) ? null : r.GetFieldValue<DateTimeOffset>(4); } var lines = new List<SuspendedCartLineResponse>(); await using (var q = new NpgsqlCommand("SELECT line_number,product_id,quantity FROM sales.suspended_cart_lines WHERE organization_id=$1 AND cart_id=$2 ORDER BY line_number", c, t)) { q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(id); await using var r = await q.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) lines.Add(new(r.GetInt32(0), r.GetGuid(1), r.GetDecimal(2))); } return new(id, branch, note, suspended, expires, resumed, lines.AsReadOnly()); }
}
