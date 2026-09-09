using Npgsql;
using SalekhPos.ShiftManagement.Application.Shifts;
using SalekhPos.ShiftManagement.Contracts.Shifts;
using SalekhPos.ShiftManagement.Domain.Shifts;

namespace SalekhPos.ShiftManagement.Infrastructure.Shifts;

public sealed class PostgresShiftService(NpgsqlDataSource? source) : IShiftService
{
    public async Task<ShiftWriteResult> OpenAsync(ShiftIdentity identity, OpenShiftCommand command, CancellationToken cancellationToken)
    {
        _ = new OpenShift(command.ShiftId, command.RegisterId, command.Currency, command.OpeningBalance);
        Validate(identity, command.OrganizationId, command.BranchId, command.OperationId);
        var dataSource = source ?? throw new ShiftUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ContextDemand(connection, transaction, identity, command.OrganizationId, command.BranchId, "shifts.open", cancellationToken);
        await using (var advisory = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction))
        {
            advisory.Parameters.AddWithValue($"{command.OrganizationId:D}:{command.RegisterId:D}");
            await advisory.ExecuteNonQueryAsync(cancellationToken);
        }
        var replay = await ByOperation(connection, transaction, command.OrganizationId, command.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.BranchId != command.BranchId || replay.RegisterId != command.RegisterId || replay.Currency != command.Currency || replay.OpeningBalance != command.OpeningBalance) throw new ShiftConflictException();
            await transaction.CommitAsync(cancellationToken);
            return new(replay, false);
        }
        if (!await IsActiveRegister(connection, transaction, command.OrganizationId, command.BranchId, command.RegisterId, cancellationToken)) throw new ShiftConflictException();
        var openedAt = await Time(connection, transaction, cancellationToken);
        try
        {
            await using var insert = new NpgsqlCommand("INSERT INTO shifts.shifts(organization_id,shift_id,operation_id,branch_id,register_id,status,currency,opening_balance,opened_at,opened_by,issuer) VALUES($1,$2,$3,$4,$5,'open',$6,$7,$8,$9,$10)", connection, transaction);
            insert.Parameters.AddWithValue(command.OrganizationId); insert.Parameters.AddWithValue(command.ShiftId); insert.Parameters.AddWithValue(command.OperationId); insert.Parameters.AddWithValue(command.BranchId); insert.Parameters.AddWithValue(command.RegisterId); insert.Parameters.AddWithValue(command.Currency); insert.Parameters.AddWithValue(command.OpeningBalance); insert.Parameters.AddWithValue(openedAt); insert.Parameters.AddWithValue(identity.Subject); insert.Parameters.AddWithValue(identity.Issuer);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation) { throw new ShiftConflictException(); }
        await transaction.CommitAsync(cancellationToken);
        return new(new(command.ShiftId, command.BranchId, command.RegisterId, "open", command.Currency, command.OpeningBalance, openedAt, identity.Subject), true);
    }

    public async Task<ShiftResponse?> ReadOpenAsync(ShiftIdentity identity, Guid organizationId, Guid branchId, Guid registerId, CancellationToken cancellationToken)
    {
        Validate(identity, organizationId, branchId, registerId);
        var dataSource = source ?? throw new ShiftUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ContextDemand(connection, transaction, identity, organizationId, branchId, "shifts.view", cancellationToken);
        ShiftResponse? result;
        await using (var query = new NpgsqlCommand("SELECT shift_id,branch_id,register_id,status,currency,opening_balance,opened_at,opened_by FROM shifts.shifts WHERE organization_id=$1 AND branch_id=$2 AND register_id=$3 AND status='open'", connection, transaction))
        {
            query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(registerId);
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            result = await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<CashMovementWriteResult> RecordCashMovementAsync(ShiftIdentity identity, RecordCashMovementCommand command, CancellationToken cancellationToken)
    {
        Validate(identity, command.OrganizationId, command.BranchId, command.OperationId);
        if (command.ShiftId == Guid.Empty || command.MovementId == Guid.Empty || command.Kind is not ("cash_in" or "cash_out") || command.Amount <= 0 || decimal.Round(command.Amount, 6) != command.Amount || string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length is < 3 or > 500) throw new ArgumentException("Cash movement request is invalid.");
        var dataSource = source ?? throw new ShiftUnavailableException(); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ContextDemand(connection, transaction, identity, command.OrganizationId, command.BranchId, "shifts.cash.manage", cancellationToken);
        await using (var advisory = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction)) { advisory.Parameters.AddWithValue($"{command.OrganizationId:D}:{command.ShiftId:D}"); await advisory.ExecuteNonQueryAsync(cancellationToken); }
        var replay = await MovementByOperation(connection, transaction, command.OrganizationId, command.OperationId, cancellationToken);
        if (replay is not null) { if (replay.ShiftId != command.ShiftId || replay.Kind != command.Kind || replay.Amount != command.Amount || replay.Reason != command.Reason) throw new ShiftConflictException(); await transaction.CommitAsync(cancellationToken); return new(replay, false); }
        string currency; await using (var query = new NpgsqlCommand("SELECT currency FROM shifts.shifts WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3 AND status='open'", connection, transaction)) { query.Parameters.AddWithValue(command.OrganizationId); query.Parameters.AddWithValue(command.BranchId); query.Parameters.AddWithValue(command.ShiftId); currency = (string?)await query.ExecuteScalarAsync(cancellationToken) ?? throw new ShiftConflictException(); }
        var at = await Time(connection, transaction, cancellationToken);
        try { await using var insert = new NpgsqlCommand("INSERT INTO shifts.cash_movements(organization_id,movement_id,operation_id,shift_id,branch_id,kind,currency,amount,reason,recorded_at,issuer,subject) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", connection, transaction); insert.Parameters.AddWithValue(command.OrganizationId); insert.Parameters.AddWithValue(command.MovementId); insert.Parameters.AddWithValue(command.OperationId); insert.Parameters.AddWithValue(command.ShiftId); insert.Parameters.AddWithValue(command.BranchId); insert.Parameters.AddWithValue(command.Kind); insert.Parameters.AddWithValue(currency); insert.Parameters.AddWithValue(command.Amount); insert.Parameters.AddWithValue(command.Reason); insert.Parameters.AddWithValue(at); insert.Parameters.AddWithValue(identity.Issuer); insert.Parameters.AddWithValue(identity.Subject); await insert.ExecuteNonQueryAsync(cancellationToken); } catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation) { throw new ShiftConflictException(); }
        await transaction.CommitAsync(cancellationToken); return new(new(command.MovementId, command.ShiftId, command.Kind, currency, command.Amount, command.Reason, at, identity.Subject), true);
    }

    public async Task<IReadOnlyList<CashMovementResponse>> ListCashMovementsAsync(ShiftIdentity identity, Guid organizationId, Guid branchId, Guid shiftId, CancellationToken cancellationToken)
    {
        Validate(identity, organizationId, branchId, shiftId); var dataSource = source ?? throw new ShiftUnavailableException(); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken); await ContextDemand(connection, transaction, identity, organizationId, branchId, "shifts.view", cancellationToken);
        var items = new List<CashMovementResponse>(); await using (var query = new NpgsqlCommand("SELECT movement_id,shift_id,kind,currency,amount,reason,recorded_at,subject FROM shifts.cash_movements WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3 ORDER BY recorded_at,movement_id LIMIT 101", connection, transaction)) { query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(shiftId); await using var reader = await query.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) items.Add(ReadMovement(reader)); }
        if (items.Count > 100) throw new ShiftConflictException(); await transaction.CommitAsync(cancellationToken); return items.AsReadOnly();
    }

    public async Task<CloseShiftWriteResult> CloseAsync(ShiftIdentity identity, CloseShiftCommand command, CancellationToken cancellationToken)
    {
        Validate(identity, command.OrganizationId, command.BranchId, command.OperationId);
        if (command.ShiftId == Guid.Empty || command.CountedCash < 0 || decimal.Round(command.CountedCash, 6) != command.CountedCash) throw new ArgumentException("Shift closing request is invalid.");
        var dataSource = source ?? throw new ShiftUnavailableException(); await using var connection = await dataSource.OpenConnectionAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ContextDemand(connection, transaction, identity, command.OrganizationId, command.BranchId, "shifts.close", cancellationToken);
        await using (var advisory = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction)) { advisory.Parameters.AddWithValue($"{command.OrganizationId:D}:{command.ShiftId:D}"); await advisory.ExecuteNonQueryAsync(cancellationToken); }
        var replay = await ClosedByOperation(connection, transaction, command.OrganizationId, command.OperationId, cancellationToken);
        if (replay is not null) { if (replay.Id != command.ShiftId || replay.BranchId != command.BranchId || replay.CountedCash != command.CountedCash) throw new ShiftConflictException(); await transaction.CommitAsync(cancellationToken); return new(replay, false); }
        Guid registerId; string currency; decimal opening; DateTimeOffset openedAt; string openedBy;
        await using (var query = new NpgsqlCommand("SELECT register_id,currency,opening_balance,opened_at,opened_by FROM shifts.shifts WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3 AND status='open'", connection, transaction)) { query.Parameters.AddWithValue(command.OrganizationId); query.Parameters.AddWithValue(command.BranchId); query.Parameters.AddWithValue(command.ShiftId); await using var reader = await query.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) throw new ShiftConflictException(); registerId = reader.GetGuid(0); currency = reader.GetString(1); opening = reader.GetDecimal(2); openedAt = reader.GetFieldValue<DateTimeOffset>(3); openedBy = reader.GetString(4); }
        decimal sales; decimal refunds; decimal cashIn; decimal cashOut;
        await using (var totals = new NpgsqlCommand("SELECT COALESCE((SELECT sum(grand_total) FROM sales.completed_sales WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3),0),COALESCE((SELECT sum(amount) FROM returns.completed_returns WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3),0)+COALESCE((SELECT sum(amount) FROM sales.sale_voids WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3),0),COALESCE((SELECT sum(amount) FROM shifts.cash_movements WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3 AND kind='cash_in'),0),COALESCE((SELECT sum(amount) FROM shifts.cash_movements WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3 AND kind='cash_out'),0)", connection, transaction)) { totals.Parameters.AddWithValue(command.OrganizationId); totals.Parameters.AddWithValue(command.BranchId); totals.Parameters.AddWithValue(command.ShiftId); await using var reader = await totals.ExecuteReaderAsync(cancellationToken); await reader.ReadAsync(cancellationToken); sales = reader.GetDecimal(0); refunds = reader.GetDecimal(1); cashIn = reader.GetDecimal(2); cashOut = reader.GetDecimal(3); }
        var expected = opening + sales - refunds + cashIn - cashOut; var variance = command.CountedCash - expected; var closedAt = await Time(connection, transaction, cancellationToken);
        await using (var update = new NpgsqlCommand("UPDATE shifts.shifts SET status='closed',close_operation_id=$4,cash_sales=$5,cash_refunds=$6,cash_in=$7,cash_out=$8,expected_cash=$9,counted_cash=$10,variance=$11,closed_at=$12,closed_by=$13 WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3 AND status='open'", connection, transaction)) { update.Parameters.AddWithValue(command.OrganizationId); update.Parameters.AddWithValue(command.BranchId); update.Parameters.AddWithValue(command.ShiftId); update.Parameters.AddWithValue(command.OperationId); update.Parameters.AddWithValue(sales); update.Parameters.AddWithValue(refunds); update.Parameters.AddWithValue(cashIn); update.Parameters.AddWithValue(cashOut); update.Parameters.AddWithValue(expected); update.Parameters.AddWithValue(command.CountedCash); update.Parameters.AddWithValue(variance); update.Parameters.AddWithValue(closedAt); update.Parameters.AddWithValue(identity.Subject); if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) throw new ShiftConflictException(); }
        var response = new ClosedShiftResponse(command.ShiftId, command.BranchId, registerId, "closed", currency, opening, sales, refunds, cashIn, cashOut, expected, command.CountedCash, variance, openedAt, closedAt, openedBy, identity.Subject);
        await transaction.CommitAsync(cancellationToken); return new(response, true);
    }

    public async Task<ClosedShiftResponse?> ReadClosedAsync(ShiftIdentity identity, Guid organizationId, Guid branchId, Guid shiftId, CancellationToken cancellationToken)
    {
        Validate(identity, organizationId, branchId, shiftId);
        var dataSource = source ?? throw new ShiftUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ContextDemand(connection, transaction, identity, organizationId, branchId, "shifts.view", cancellationToken);
        ClosedShiftResponse? result;
        await using (var query = new NpgsqlCommand($"{ClosedShiftSelect} WHERE organization_id=$1 AND branch_id=$2 AND shift_id=$3 AND status='closed'", connection, transaction))
        {
            query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(shiftId);
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            result = await reader.ReadAsync(cancellationToken) ? ReadClosed(reader) : null;
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ClosedShiftPage> ListClosedAsync(ShiftIdentity identity, Guid organizationId, Guid branchId, int pageSize, Guid? after, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || pageSize is < 1 or > 100 || after == Guid.Empty
            || string.IsNullOrWhiteSpace(identity.Issuer) || string.IsNullOrWhiteSpace(identity.Subject)) throw new ArgumentException("Shift query is invalid.");
        var dataSource = source ?? throw new ShiftUnavailableException();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ContextDemand(connection, transaction, identity, organizationId, branchId, "shifts.view", cancellationToken);
        var items = new List<ClosedShiftResponse>(pageSize + 1);
        await using (var query = new NpgsqlCommand($"{ClosedShiftSelect} WHERE organization_id=$1 AND branch_id=$2 AND status='closed' AND ($3::uuid IS NULL OR shift_id < $3) ORDER BY shift_id DESC LIMIT $4", connection, transaction))
        {
            query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId);
            query.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Uuid, Value = (object?)after ?? DBNull.Value });
            query.Parameters.AddWithValue(pageSize + 1);
            await using var reader = await query.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) items.Add(ReadClosed(reader));
        }
        Guid? next = null;
        if (items.Count > pageSize) { items.RemoveAt(pageSize); next = items[^1].Id; }
        await transaction.CommitAsync(cancellationToken);
        return new(items.AsReadOnly(), next);
    }

    private const string ClosedShiftSelect = "SELECT shift_id,branch_id,register_id,status,currency,opening_balance,cash_sales,cash_refunds,cash_in,cash_out,expected_cash,counted_cash,variance,opened_at,closed_at,opened_by,closed_by FROM shifts.shifts";
    private static ClosedShiftResponse ReadClosed(NpgsqlDataReader r) => new(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetDecimal(5), r.GetDecimal(6), r.GetDecimal(7), r.GetDecimal(8), r.GetDecimal(9), r.GetDecimal(10), r.GetDecimal(11), r.GetDecimal(12), r.GetFieldValue<DateTimeOffset>(13), r.GetFieldValue<DateTimeOffset>(14), r.GetString(15), r.GetString(16));

    private static async Task<ClosedShiftResponse?> ClosedByOperation(NpgsqlConnection c, NpgsqlTransaction t, Guid organizationId, Guid operationId, CancellationToken ct)
    { await using var q = new NpgsqlCommand($"{ClosedShiftSelect} WHERE organization_id=$1 AND close_operation_id=$2", c, t); q.Parameters.AddWithValue(organizationId); q.Parameters.AddWithValue(operationId); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? ReadClosed(r) : null; }

    private static async Task<CashMovementResponse?> MovementByOperation(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId, Guid operationId, CancellationToken cancellationToken)
    { await using var query = new NpgsqlCommand("SELECT movement_id,shift_id,kind,currency,amount,reason,recorded_at,subject FROM shifts.cash_movements WHERE organization_id=$1 AND operation_id=$2", connection, transaction); query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(operationId); await using var reader = await query.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? ReadMovement(reader) : null; }
    private static CashMovementResponse ReadMovement(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7));

    private static async Task<bool> IsActiveRegister(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId, Guid branchId, Guid registerId, CancellationToken cancellationToken)
    { await using var query = new NpgsqlCommand("SELECT EXISTS(SELECT FROM stores.registers WHERE organization_id=$1 AND branch_id=$2 AND register_id=$3 AND is_active)", connection, transaction); query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(branchId); query.Parameters.AddWithValue(registerId); return await query.ExecuteScalarAsync(cancellationToken) is true; }
    private static async Task<ShiftResponse?> ByOperation(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId, Guid operationId, CancellationToken cancellationToken)
    { await using var query = new NpgsqlCommand("SELECT shift_id,branch_id,register_id,status,currency,opening_balance,opened_at,opened_by FROM shifts.shifts WHERE organization_id=$1 AND operation_id=$2", connection, transaction); query.Parameters.AddWithValue(organizationId); query.Parameters.AddWithValue(operationId); await using var reader = await query.ExecuteReaderAsync(cancellationToken); return await reader.ReadAsync(cancellationToken) ? Read(reader) : null; }
    private static ShiftResponse Read(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetDecimal(5), reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7));
    private static async Task<DateTimeOffset> Time(NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    { await using var query = new NpgsqlCommand("SELECT statement_timestamp()", connection, transaction); await using var reader = await query.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) throw new ShiftUnavailableException(); return reader.GetFieldValue<DateTimeOffset>(0); }
    private static void Validate(ShiftIdentity identity, Guid organizationId, Guid branchId, Guid value)
    { if (organizationId == Guid.Empty || branchId == Guid.Empty || value == Guid.Empty || string.IsNullOrWhiteSpace(identity.Issuer) || string.IsNullOrWhiteSpace(identity.Subject)) throw new ArgumentException("Shift request is invalid."); }
    private static async Task ContextDemand(NpgsqlConnection connection, NpgsqlTransaction transaction, ShiftIdentity identity, Guid organizationId, Guid branchId, string permission, CancellationToken cancellationToken)
    {
        await using (var context = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", connection, transaction)) { context.Parameters.AddWithValue(organizationId.ToString()); context.Parameters.AddWithValue(identity.Issuer); context.Parameters.AddWithValue(identity.Subject); await context.ExecuteNonQueryAsync(cancellationToken); }
        await using var demand = new NpgsqlCommand("SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2 JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id JOIN organization.organizations a ON a.organization_id=b.organization_id WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active AND b.is_active AND z.is_active AND a.is_active AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission=$5 AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id) OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id) OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))", connection, transaction);
        demand.Parameters.AddWithValue(organizationId); demand.Parameters.AddWithValue(branchId); demand.Parameters.AddWithValue(identity.Issuer); demand.Parameters.AddWithValue(identity.Subject); demand.Parameters.AddWithValue(permission);
        if (await demand.ExecuteScalarAsync(cancellationToken) is not true) throw new ShiftDeniedException();
    }
}
