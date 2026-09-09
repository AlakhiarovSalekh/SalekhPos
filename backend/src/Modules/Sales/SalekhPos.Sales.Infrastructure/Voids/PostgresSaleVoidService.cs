using System.Data;
using Npgsql;
using SalekhPos.Sales.Application.Voids;
using SalekhPos.Sales.Contracts.Voids;
using SalekhPos.Sales.Domain.Voids;

namespace SalekhPos.Sales.Infrastructure.Voids;

public sealed class PostgresSaleVoidService(NpgsqlDataSource? source) : ISaleVoidService, ISaleVoidReader
{
    public async Task<VoidWriteResult> VoidAsync(string issuer, string subject, VoidSaleCommand command,
        CancellationToken cancellationToken)
    {
        var reason = command.Reason?.Trim() ?? ""; _ = new SaleVoid(command.VoidId, command.SaleId, reason);
        if (command.OrganizationId == Guid.Empty || command.BranchId == Guid.Empty || command.OperationId == Guid.Empty
            || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Sale void is invalid.");
        var dataSource = source ?? throw new SaleVoidUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await Context(connection, transaction, command.OrganizationId, issuer, subject, cancellationToken);
        await Demand(connection, transaction, command.OrganizationId, command.BranchId, issuer, subject, cancellationToken);
        await Lock(connection, transaction, $"sale-reversal:{command.OrganizationId:D}:{command.SaleId:D}", cancellationToken);
        var replay = await ReadByOperation(connection, transaction, command.OrganizationId, command.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.SaleId != command.SaleId || replay.BranchId != command.BranchId || replay.Reason != reason)
                throw new SaleVoidConflictException();
            await transaction.CommitAsync(cancellationToken); return new(replay, false);
        }
        if (await HasReturns(connection, transaction, command.OrganizationId, command.SaleId, cancellationToken))
            throw new SaleVoidConflictException();
        var sale = await Sale(connection, transaction, command, cancellationToken) ?? throw new SaleVoidNotFoundException();
        await Lock(connection, transaction, $"{command.OrganizationId:D}:{sale.ShiftId:D}", cancellationToken);
        if (!await ShiftIsOpen(connection, transaction, command.OrganizationId, command.BranchId, sale.ShiftId, cancellationToken)) throw new SaleVoidConflictException();
        var at = await Time(connection, transaction, cancellationToken);
        try
        {
            await using var insert = new NpgsqlCommand("INSERT INTO sales.sale_voids(organization_id,void_id,operation_id,sale_id,branch_id,shift_id,register_id,currency,amount,reason,voided_at,issuer,subject) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", connection, transaction);
            insert.Parameters.AddWithValue(command.OrganizationId); insert.Parameters.AddWithValue(command.VoidId); insert.Parameters.AddWithValue(command.OperationId);
            insert.Parameters.AddWithValue(command.SaleId); insert.Parameters.AddWithValue(command.BranchId); insert.Parameters.AddWithValue(sale.ShiftId); insert.Parameters.AddWithValue(sale.RegisterId); insert.Parameters.AddWithValue(sale.Currency);
            insert.Parameters.AddWithValue(sale.Amount); insert.Parameters.AddWithValue(reason); insert.Parameters.AddWithValue(at);
            insert.Parameters.AddWithValue(issuer); insert.Parameters.AddWithValue(subject); await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation) { throw new SaleVoidConflictException(); }
        var responses = new List<VoidedSaleLineResponse>();
        foreach (var line in sale.Lines)
        {
            var movementId = Guid.NewGuid(); var operationId = Guid.NewGuid();
            await using (var movement = new NpgsqlCommand("INSERT INTO inventory.stock_movements(organization_id,movement_id,operation_id,branch_id,product_id,kind,direction,quantity,reason,occurred_at,issuer,subject) VALUES($1,$2,$3,$4,$5,'return',1,$6,'Voided cash sale',$7,$8,$9)", connection, transaction))
            { movement.Parameters.AddWithValue(command.OrganizationId); movement.Parameters.AddWithValue(movementId); movement.Parameters.AddWithValue(operationId); movement.Parameters.AddWithValue(command.BranchId); movement.Parameters.AddWithValue(line.ProductId); movement.Parameters.AddWithValue(line.Quantity); movement.Parameters.AddWithValue(at); movement.Parameters.AddWithValue(issuer); movement.Parameters.AddWithValue(subject); await movement.ExecuteNonQueryAsync(cancellationToken); }
            await using var insertLine = new NpgsqlCommand("INSERT INTO sales.sale_void_lines(organization_id,void_id,line_number,product_id,quantity,inventory_movement_id) VALUES($1,$2,$3,$4,$5,$6)", connection, transaction);
            insertLine.Parameters.AddWithValue(command.OrganizationId); insertLine.Parameters.AddWithValue(command.VoidId); insertLine.Parameters.AddWithValue(line.Number); insertLine.Parameters.AddWithValue(line.ProductId); insertLine.Parameters.AddWithValue(line.Quantity); insertLine.Parameters.AddWithValue(movementId); await insertLine.ExecuteNonQueryAsync(cancellationToken);
            responses.Add(new(line.Number, line.ProductId, line.Quantity, movementId));
        }
        await transaction.CommitAsync(cancellationToken);
        return new(new(command.VoidId, command.SaleId, command.BranchId, sale.Currency, sale.Amount, reason, at, responses.AsReadOnly()), true);
    }

    public async Task<SaleVoidPage> ListAsync(string issuer, string subject, Guid organizationId, Guid branchId,
        int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        ValidateRead(issuer, subject, organizationId, branchId, after, pageSize);
        var dataSource = source ?? throw new SaleVoidUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Context(connection, transaction, organizationId, issuer, subject, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, issuer, subject, cancellationToken);
        await using var query = new NpgsqlCommand("SELECT void_id,sale_id,branch_id,currency,amount,reason,voided_at FROM sales.sale_voids WHERE organization_id=$1 AND branch_id=$2 AND ($3::uuid IS NULL OR void_id>$3) ORDER BY void_id LIMIT $4", connection, transaction);
        query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
        query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Uuid, Value = (object?)after ?? DBNull.Value });
        query.Parameters.AddWithValue(pageSize + 1);
        var items = new List<VoidedSaleSummaryResponse>(pageSize + 1);
        await using (var reader = await query.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) items.Add(new(reader.GetGuid(0), reader.GetGuid(1),
                reader.GetGuid(2), reader.GetString(3), reader.GetDecimal(4), reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        Guid? next = null;
        if (items.Count > pageSize) { items.RemoveAt(pageSize); next = items[^1].Id; }
        await transaction.CommitAsync(cancellationToken);
        return new(items.AsReadOnly(), next);
    }

    public async Task<VoidedSaleResponse?> ReadAsync(string issuer, string subject, Guid organizationId,
        Guid branchId, Guid voidId, CancellationToken cancellationToken)
    {
        ValidateRead(issuer, subject, organizationId, branchId, voidId, 1);
        var dataSource = source ?? throw new SaleVoidUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Context(connection, transaction, organizationId, issuer, subject, cancellationToken);
        await Demand(connection, transaction, organizationId, branchId, issuer, subject, cancellationToken);
        var result = await ReadById(connection, transaction, organizationId, branchId, voidId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static void ValidateRead(string issuer, string subject, Guid organizationId, Guid branchId,
        Guid? value, int pageSize)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || value == Guid.Empty
            || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject) || pageSize is < 1 or > 100)
            throw new ArgumentException("Sale void query is invalid.");
    }

    private static async Task Context(NpgsqlConnection c, NpgsqlTransaction t, Guid org, string issuer, string subject, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t); q.Parameters.AddWithValue(org.ToString()); q.Parameters.AddWithValue(issuer); q.Parameters.AddWithValue(subject); await q.ExecuteNonQueryAsync(ct); }
    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid branch, string issuer, string subject, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2 JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id JOIN organization.organizations o ON o.organization_id=b.organization_id WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active AND b.is_active AND z.is_active AND o.is_active AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission='sales.void' AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id) OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id) OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(branch); q.Parameters.AddWithValue(issuer); q.Parameters.AddWithValue(subject); if (await q.ExecuteScalarAsync(ct) is not true) throw new SaleVoidDeniedException(); }
    private static async Task Lock(NpgsqlConnection c, NpgsqlTransaction t, string key, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", c, t); q.Parameters.AddWithValue(key); await q.ExecuteNonQueryAsync(ct); }
    private static async Task<bool> HasReturns(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid sale, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT EXISTS(SELECT FROM returns.completed_returns WHERE organization_id=$1 AND sale_id=$2)", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(sale); return await q.ExecuteScalarAsync(ct) is true; }
    private static async Task<bool> ShiftIsOpen(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid branch, Guid shift, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT EXISTS(SELECT FROM shifts.shifts WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3 AND status='open')", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(branch); q.Parameters.AddWithValue(shift); return await q.ExecuteScalarAsync(ct) is true; }
    private static async Task<SaleData?> Sale(NpgsqlConnection c, NpgsqlTransaction t, VoidSaleCommand x, CancellationToken ct) { string currency; decimal amount; Guid shiftId; Guid registerId; await using (var q = new NpgsqlCommand("SELECT currency,grand_total,shift_id,register_id FROM sales.completed_sales WHERE organization_id=$1 AND branch_id=$2 AND sale_id=$3 AND shift_id IS NOT NULL AND register_id IS NOT NULL", c, t)) { q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.BranchId); q.Parameters.AddWithValue(x.SaleId); await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null; currency = r.GetString(0); amount = r.GetDecimal(1); shiftId = r.GetGuid(2); registerId = r.GetGuid(3); } var lines = new List<SaleLine>(); await using (var q = new NpgsqlCommand("SELECT line_number,product_id,quantity FROM sales.sale_lines WHERE organization_id=$1 AND sale_id=$2 ORDER BY line_number", c, t)) { q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.SaleId); await using var r = await q.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) lines.Add(new(r.GetInt32(0), r.GetGuid(1), r.GetDecimal(2))); } return new(currency, amount, shiftId, registerId, lines); }
    private static async Task<DateTimeOffset> Time(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT statement_timestamp()", c, t); await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new SaleVoidUnavailableException(); return r.GetFieldValue<DateTimeOffset>(0); }
    private static async Task<VoidedSaleResponse?> ReadByOperation(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid op, CancellationToken ct) { Guid id; Guid sale; Guid branch; string currency; decimal amount; string reason; DateTimeOffset at; await using (var q = new NpgsqlCommand("SELECT void_id,sale_id,branch_id,currency,amount,reason,voided_at FROM sales.sale_voids WHERE organization_id=$1 AND operation_id=$2", c, t)) { q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(op); await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null; id = r.GetGuid(0); sale = r.GetGuid(1); branch = r.GetGuid(2); currency = r.GetString(3); amount = r.GetDecimal(4); reason = r.GetString(5); at = r.GetFieldValue<DateTimeOffset>(6); } var lines = new List<VoidedSaleLineResponse>(); await using (var q = new NpgsqlCommand("SELECT line_number,product_id,quantity,inventory_movement_id FROM sales.sale_void_lines WHERE organization_id=$1 AND void_id=$2 ORDER BY line_number", c, t)) { q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(id); await using var r = await q.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) lines.Add(new(r.GetInt32(0), r.GetGuid(1), r.GetDecimal(2), r.GetGuid(3))); } return new(id, sale, branch, currency, amount, reason, at, lines.AsReadOnly()); }
    private static async Task<VoidedSaleResponse?> ReadById(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid branch, Guid id, CancellationToken ct) { Guid sale; string currency; decimal amount; string reason; DateTimeOffset at; await using (var q = new NpgsqlCommand("SELECT sale_id,currency,amount,reason,voided_at FROM sales.sale_voids WHERE organization_id=$1 AND branch_id=$2 AND void_id=$3", c, t)) { q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(branch); q.Parameters.AddWithValue(id); await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null; sale = r.GetGuid(0); currency = r.GetString(1); amount = r.GetDecimal(2); reason = r.GetString(3); at = r.GetFieldValue<DateTimeOffset>(4); } var lines = new List<VoidedSaleLineResponse>(); await using (var q = new NpgsqlCommand("SELECT line_number,product_id,quantity,inventory_movement_id FROM sales.sale_void_lines WHERE organization_id=$1 AND void_id=$2 ORDER BY line_number", c, t)) { q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(id); await using var r = await q.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) lines.Add(new(r.GetInt32(0), r.GetGuid(1), r.GetDecimal(2), r.GetGuid(3))); } return new(id, sale, branch, currency, amount, reason, at, lines.AsReadOnly()); }
    private sealed record SaleLine(int Number, Guid ProductId, decimal Quantity);
    private sealed record SaleData(string Currency, decimal Amount, Guid ShiftId, Guid RegisterId, IReadOnlyList<SaleLine> Lines);
}
