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
