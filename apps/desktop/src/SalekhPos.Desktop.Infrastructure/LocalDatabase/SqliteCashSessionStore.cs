using System.Globalization;
using Microsoft.Data.Sqlite;
using SalekhPos.Desktop.Application.Shifts;
using SalekhPos.Desktop.Domain.Shifts;

namespace SalekhPos.Desktop.Infrastructure.LocalDatabase;

public sealed class SqliteCashSessionStore(string databasePath) : ILocalCashSessionStore
{
    private readonly string connectionString = BuildConnection(databasePath);

    public async Task<LocalCashSession?> ApplyAsync(Guid organizationId, Guid branchId, Guid deviceId,
        RemoteCashSessionSnapshot snapshot, CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId, deviceId);
        ValidateSnapshot(organizationId, branchId, deviceId, snapshot);
        await using var connection = await Open(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await Execute(connection, transaction, Schema, cancellationToken);
        var assignmentWritten = await Execute(connection, transaction, """
            INSERT INTO local_device_assignments(device_id,organization_id,branch_id,register_id,status,sync_protocol_version,refreshed_at)
            VALUES($1,$2,$3,$4,$5,$6,$7)
            ON CONFLICT(device_id) DO UPDATE SET
            refreshed_at=excluded.refreshed_at
            WHERE organization_id=excluded.organization_id AND branch_id=excluded.branch_id
              AND register_id=excluded.register_id AND status=excluded.status
              AND sync_protocol_version=excluded.sync_protocol_version AND refreshed_at<=excluded.refreshed_at
            """, cancellationToken, ("$1", deviceId), ("$2", organizationId), ("$3", branchId),
            ("$4", snapshot.Device.RegisterId), ("$5", snapshot.Device.Status),
            ("$6", snapshot.Device.SyncProtocolVersion), ("$7", snapshot.CapturedAt.ToString("O")));
        if (assignmentWritten != 1) throw new InvalidOperationException("Changed or stale device assignment.");
        await Execute(connection, transaction, """
            UPDATE local_cash_sessions SET status='superseded' WHERE organization_id=$1 AND branch_id=$2
            AND device_id=$3 AND status='active' AND ($4 IS NULL OR shift_id<>$4)
            """, cancellationToken, ("$1", organizationId), ("$2", branchId), ("$3", deviceId),
            ("$4", (object?)snapshot.Shift?.ShiftId ?? DBNull.Value));
        if (snapshot.Shift is not null)
        {
            var written = await Execute(connection, transaction, """
                INSERT INTO local_cash_sessions(shift_id,organization_id,branch_id,device_id,register_id,currency,
                opening_balance,opened_at,refreshed_at,status)
                VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,'active')
                ON CONFLICT(shift_id) DO UPDATE SET refreshed_at=excluded.refreshed_at
                WHERE organization_id=excluded.organization_id AND branch_id=excluded.branch_id
                  AND device_id=excluded.device_id AND register_id=excluded.register_id
                  AND currency=excluded.currency AND opening_balance=excluded.opening_balance
                  AND opened_at=excluded.opened_at AND status='active' AND refreshed_at<=excluded.refreshed_at
                """, cancellationToken, ("$1", snapshot.Shift.ShiftId), ("$2", organizationId),
                ("$3", branchId), ("$4", deviceId), ("$5", snapshot.Shift.RegisterId),
                ("$6", snapshot.Shift.Currency), ("$7", Number(snapshot.Shift.OpeningBalance)),
                ("$8", snapshot.Shift.OpenedAt.ToString("O")), ("$9", snapshot.CapturedAt.ToString("O")));
            if (written != 1) throw new InvalidOperationException("Changed cash-session replay.");
        }
        var result = await Read(connection, transaction, organizationId, branchId, deviceId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<LocalCashSession?> ReadActiveAsync(Guid organizationId, Guid branchId, Guid deviceId,
        CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId, deviceId);
        await using var connection = await Open(cancellationToken);
        await Execute(connection, null, Schema, cancellationToken);
        return await Read(connection, null, organizationId, branchId, deviceId, cancellationToken);
    }

    public async Task ConfirmClosedAsync(Guid organizationId, Guid branchId, Guid deviceId, Guid shiftId,
        CancellationToken cancellationToken)
    {
        ValidateScope(organizationId, branchId, deviceId);
        if (shiftId == Guid.Empty) throw new ArgumentException("Shift identity is required.");
        await using var connection = await Open(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await Execute(connection, transaction, Schema, cancellationToken);
        var changed = await Execute(connection, transaction, """
            UPDATE local_cash_sessions SET status='superseded' WHERE organization_id=$1 AND branch_id=$2
            AND device_id=$3 AND shift_id=$4 AND status='active'
            """, cancellationToken, ("$1", organizationId), ("$2", branchId), ("$3", deviceId), ("$4", shiftId));
        if (changed != 1)
        {
            await using var replay = Command(connection, transaction, """
                SELECT count(*) FROM local_cash_sessions WHERE organization_id=$1 AND branch_id=$2
                AND device_id=$3 AND shift_id=$4 AND status='superseded'
                """, ("$1", organizationId), ("$2", branchId), ("$3", deviceId), ("$4", shiftId));
            if ((long)(await replay.ExecuteScalarAsync(cancellationToken) ?? 0L) != 1)
                throw new InvalidOperationException("The closed cash session is unknown or changed.");
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidateSnapshot(Guid organizationId, Guid branchId, Guid deviceId,
        RemoteCashSessionSnapshot snapshot)
    {
        if (snapshot is null || snapshot.Device.DeviceId != deviceId || snapshot.Device.BranchId != branchId
            || snapshot.Device.RegisterId == Guid.Empty || snapshot.Device.Status != "active"
            || snapshot.Device.SyncProtocolVersion != 1 || snapshot.CapturedAt == default
            || snapshot.CapturedAt.Offset != TimeSpan.Zero || snapshot.Shift is not null
            && (snapshot.Shift.BranchId != branchId || snapshot.Shift.RegisterId != snapshot.Device.RegisterId
                || snapshot.Shift.Status != "open" || snapshot.CapturedAt < snapshot.Shift.OpenedAt))
            throw new InvalidOperationException("The cash-session snapshot is invalid.");
        if (snapshot.Shift is not null)
            _ = new LocalCashSession(organizationId, branchId, deviceId, snapshot.Device.RegisterId,
                snapshot.Shift.ShiftId, snapshot.Shift.Currency, snapshot.Shift.OpeningBalance,
                snapshot.Shift.OpenedAt, snapshot.CapturedAt);
    }

    private static async Task<LocalCashSession?> Read(SqliteConnection c, SqliteTransaction? t, Guid organization,
        Guid branch, Guid device, CancellationToken ct)
    {
        await using var query = Command(c, t, """
            SELECT shift_id,register_id,currency,opening_balance,opened_at,refreshed_at FROM local_cash_sessions
            WHERE organization_id=$1 AND branch_id=$2 AND device_id=$3 AND status='active'
            """, ("$1", organization), ("$2", branch), ("$3", device));
        await using var reader = await query.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(organization, branch, device, Guid.Parse(reader.GetString(1)), Guid.Parse(reader.GetString(0)),
            reader.GetString(2), decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }

    private async Task<SqliteConnection> Open(CancellationToken ct) { var c = new SqliteConnection(connectionString); await c.OpenAsync(ct); await Execute(c, null, "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=10000;", ct); return c; }
    private static async Task<int> Execute(SqliteConnection c, SqliteTransaction? t, string sql, CancellationToken ct, params (string, object)[] values) { await using var q = Command(c, t, sql, values); return await q.ExecuteNonQueryAsync(ct); }
    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction? t, string sql, params (string, object)[] values) { var q = c.CreateCommand(); q.Transaction = t; q.CommandText = sql; foreach (var (name, value) in values) q.Parameters.AddWithValue(name, value is Guid id ? id.ToString("D") : value); return q; }
    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static void ValidateScope(Guid organization, Guid branch, Guid device) { if (organization == Guid.Empty || branch == Guid.Empty || device == Guid.Empty) throw new ArgumentException("Cash-session scope is required."); }
    private static string BuildConnection(string path) { if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Database path is required."); var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); return new SqliteConnectionStringBuilder { DataSource = full, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString(); }
    internal const string Schema = """
        CREATE TABLE IF NOT EXISTS local_device_assignments(device_id TEXT PRIMARY KEY,organization_id TEXT NOT NULL,
        branch_id TEXT NOT NULL,register_id TEXT NOT NULL,status TEXT NOT NULL CHECK(status='active'),
        sync_protocol_version INTEGER NOT NULL CHECK(sync_protocol_version=1),refreshed_at TEXT NOT NULL) STRICT;
        CREATE TABLE IF NOT EXISTS local_cash_sessions(shift_id TEXT PRIMARY KEY,organization_id TEXT NOT NULL,
        branch_id TEXT NOT NULL,device_id TEXT NOT NULL,register_id TEXT NOT NULL,currency TEXT NOT NULL,
        opening_balance TEXT NOT NULL,opened_at TEXT NOT NULL,refreshed_at TEXT NOT NULL,status TEXT NOT NULL
        CHECK(status IN('active','superseded','invalidated')),FOREIGN KEY(device_id) REFERENCES local_device_assignments(device_id)) STRICT;
        CREATE UNIQUE INDEX IF NOT EXISTS ux_local_active_cash_session ON local_cash_sessions(device_id) WHERE status='active';
        """;
}
