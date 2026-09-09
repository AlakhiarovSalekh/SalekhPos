using System.Data;
using Npgsql;
using SalekhPos.Returns.Application.Returns;
using SalekhPos.Returns.Contracts.Returns;
using SalekhPos.Returns.Domain.Returns;

namespace SalekhPos.Returns.Infrastructure.Returns;

public sealed class PostgresReturnCompletion(NpgsqlDataSource? source) : IReturnCompletion
{
    public async Task<ReturnWriteResult> CompleteAsync(ReturnIdentity identity, CompleteReturnCommand command,
        CancellationToken cancellationToken)
    {
        var reason = command.Reason?.Trim() ?? "";
        if (command.OrganizationId == Guid.Empty || command.BranchId == Guid.Empty || command.ReturnId == Guid.Empty
            || command.OperationId == Guid.Empty || command.SaleId == Guid.Empty || string.IsNullOrWhiteSpace(identity.Issuer)
            || string.IsNullOrWhiteSpace(identity.Subject)) throw new ArgumentException("Return request is invalid.");
        var dataSource = source ?? throw new ReturnsUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await Context(connection, transaction, command.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, command, identity, cancellationToken);
        await using (var locking = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction))
        { locking.Parameters.AddWithValue($"return:{command.OrganizationId:D}:{command.SaleId:D}"); await locking.ExecuteNonQueryAsync(cancellationToken); }
        var replay = await Read(connection, transaction, command.OrganizationId, command.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.SaleId != command.SaleId || replay.BranchId != command.BranchId || replay.Reason != reason)
                throw new ReturnConflictException();
            await transaction.CommitAsync(cancellationToken); return new(replay, false);
        }
        var (currency, amount, completedAt, saleLines) = await Sale(connection, transaction, command,
            cancellationToken) ?? throw new ReturnSaleNotFoundException();
        _ = new CompletedReturn(command.ReturnId, command.SaleId, reason, amount, completedAt);
        try
        {
            await using var insert = new NpgsqlCommand("INSERT INTO returns.completed_returns(organization_id,return_id,operation_id,sale_id,branch_id,currency,amount,reason,completed_at,issuer,subject) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)", connection, transaction);
            insert.Parameters.AddWithValue(command.OrganizationId); insert.Parameters.AddWithValue(command.ReturnId);
            insert.Parameters.AddWithValue(command.OperationId); insert.Parameters.AddWithValue(command.SaleId);
            insert.Parameters.AddWithValue(command.BranchId); insert.Parameters.AddWithValue(currency);
            insert.Parameters.AddWithValue(amount); insert.Parameters.AddWithValue(reason); insert.Parameters.AddWithValue(completedAt);
            insert.Parameters.AddWithValue(identity.Issuer); insert.Parameters.AddWithValue(identity.Subject);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation) { throw new ReturnConflictException(); }
        var lines = new List<ReturnedLineResponse>();
        foreach (var (number, productId, quantity, lineAmount) in saleLines)
        {
            var movementId = Guid.NewGuid(); var operationId = Guid.NewGuid();
            await using var movement = new NpgsqlCommand("INSERT INTO inventory.stock_movements(organization_id,movement_id,operation_id,branch_id,product_id,kind,direction,quantity,reason,occurred_at,issuer,subject) VALUES($1,$2,$3,$4,$5,'return',1,$6,'Completed sale return',$7,$8,$9)", connection, transaction);
            movement.Parameters.AddWithValue(command.OrganizationId); movement.Parameters.AddWithValue(movementId); movement.Parameters.AddWithValue(operationId);
            movement.Parameters.AddWithValue(command.BranchId); movement.Parameters.AddWithValue(productId); movement.Parameters.AddWithValue(quantity);
            movement.Parameters.AddWithValue(completedAt); movement.Parameters.AddWithValue(identity.Issuer); movement.Parameters.AddWithValue(identity.Subject);
            await movement.ExecuteNonQueryAsync(cancellationToken);
            await using var insertLine = new NpgsqlCommand("INSERT INTO returns.return_lines(organization_id,return_id,line_number,product_id,quantity,amount,inventory_movement_id,inventory_operation_id) VALUES($1,$2,$3,$4,$5,$6,$7,$8)", connection, transaction);
            insertLine.Parameters.AddWithValue(command.OrganizationId); insertLine.Parameters.AddWithValue(command.ReturnId); insertLine.Parameters.AddWithValue(number);
            insertLine.Parameters.AddWithValue(productId); insertLine.Parameters.AddWithValue(quantity); insertLine.Parameters.AddWithValue(lineAmount);
            insertLine.Parameters.AddWithValue(movementId); insertLine.Parameters.AddWithValue(operationId); await insertLine.ExecuteNonQueryAsync(cancellationToken);
            lines.Add(new(number, productId, quantity, lineAmount));
        }
        await transaction.CommitAsync(cancellationToken);
        return new(new(command.ReturnId, command.SaleId, command.BranchId, currency, amount, reason,
            completedAt, lines.AsReadOnly()), true);
    }

    private static async Task Context(NpgsqlConnection c, NpgsqlTransaction t, Guid org, ReturnIdentity id, CancellationToken ct)
    { await using var q = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t); q.Parameters.AddWithValue(org.ToString()); q.Parameters.AddWithValue(id.Issuer); q.Parameters.AddWithValue(id.Subject); await q.ExecuteNonQueryAsync(ct); }
    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, CompleteReturnCommand x, ReturnIdentity id, CancellationToken ct)
    { await using var q = new NpgsqlCommand("SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2 JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id JOIN organization.organizations o ON o.organization_id=b.organization_id WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active AND b.is_active AND z.is_active AND o.is_active AND g.permission='sales.refund' AND (g.scope_kind='organization' OR g.branch_id=b.branch_id))", c, t); q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.BranchId); q.Parameters.AddWithValue(id.Issuer); q.Parameters.AddWithValue(id.Subject); if (await q.ExecuteScalarAsync(ct) is not true) throw new ReturnDeniedException(); }
    private static async Task<(string Currency, decimal Amount, DateTimeOffset At,
        IReadOnlyList<(int Number, Guid ProductId, decimal Quantity, decimal Amount)> Lines)?> Sale(
        NpgsqlConnection c, NpgsqlTransaction t, CompleteReturnCommand x, CancellationToken ct)
    { string currency; decimal amount; await using (var q = new NpgsqlCommand("SELECT currency,grand_total FROM sales.completed_sales WHERE organization_id=$1 AND branch_id=$2 AND sale_id=$3", c, t)) { q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.BranchId); q.Parameters.AddWithValue(x.SaleId); await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null; currency = r.GetString(0); amount = r.GetDecimal(1); } var lines = new List<(int, Guid, decimal, decimal)>(); await using (var q = new NpgsqlCommand("SELECT line_number,product_id,quantity,gross_amount FROM sales.sale_lines WHERE organization_id=$1 AND sale_id=$2 ORDER BY line_number", c, t)) { q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.SaleId); await using var r = await q.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) lines.Add((r.GetInt32(0), r.GetGuid(1), r.GetDecimal(2), r.GetDecimal(3))); } return (currency, amount, await Time(c, t, ct), lines); }
    private static async Task<DateTimeOffset> Time(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct)
    {
        await using var q = new NpgsqlCommand("SELECT statement_timestamp()", c, t);
        await using var reader = await q.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ReturnsUnavailableException();
        return reader.GetFieldValue<DateTimeOffset>(0);
    }
    private static async Task<CompletedReturnResponse?> Read(NpgsqlConnection c, NpgsqlTransaction t,
        Guid org, Guid op, CancellationToken ct)
    {
        Guid id; Guid sale; Guid branch; string currency; decimal amount; string reason; DateTimeOffset at;
        await using (var q = new NpgsqlCommand("SELECT return_id,sale_id,branch_id,currency,amount,reason,completed_at FROM returns.completed_returns WHERE organization_id=$1 AND operation_id=$2", c, t))
        {
            q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(op);
            await using var reader = await q.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            id = reader.GetGuid(0); sale = reader.GetGuid(1); branch = reader.GetGuid(2);
            currency = reader.GetString(3); amount = reader.GetDecimal(4); reason = reader.GetString(5);
            at = reader.GetFieldValue<DateTimeOffset>(6);
        }
        var lines = new List<ReturnedLineResponse>();
        await using (var q = new NpgsqlCommand("SELECT line_number,product_id,quantity,amount FROM returns.return_lines WHERE organization_id=$1 AND return_id=$2 ORDER BY line_number", c, t))
        {
            q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(id);
            await using var reader = await q.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) lines.Add(new(reader.GetInt32(0), reader.GetGuid(1),
                reader.GetDecimal(2), reader.GetDecimal(3)));
        }
        return new(id, sale, branch, currency, amount, reason, at, lines.AsReadOnly());
    }
}
