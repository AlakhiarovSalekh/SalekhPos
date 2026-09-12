using Microsoft.Data.Sqlite;
using SalekhPos.Desktop.Application.Devices;

namespace SalekhPos.Desktop.Infrastructure.LocalDatabase;

public sealed class SqliteDeviceProvisioningStateStore(string databasePath) : IDeviceProvisioningStateStore
{
    private readonly string connectionString = BuildConnection(databasePath);

    public async Task<DeviceProvisioningState> GetOrCreateAsync(DeviceProvisioningRequest request,
        Guid registrationOperationId, Guid trustOperationId, string keyReference, CancellationToken cancellationToken)
    {
        if (registrationOperationId == Guid.Empty || trustOperationId == Guid.Empty
            || string.IsNullOrWhiteSpace(keyReference)) throw Invalid();
        await using var connection = await Open(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await Execute(connection, transaction, Schema, cancellationToken);
        await Execute(connection, transaction, """
            INSERT OR IGNORE INTO device_provisioning_state(singleton,organization_id,branch_id,register_id,code,name,
            platform,sync_protocol_version,registration_operation_id,trust_operation_id,key_reference)
            VALUES(1,$1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
            """, cancellationToken, ("$1", request.OrganizationId), ("$2", request.BranchId),
            ("$3", request.RegisterId), ("$4", request.Code), ("$5", request.Name), ("$6", request.Platform),
            ("$7", request.SyncProtocolVersion), ("$8", registrationOperationId), ("$9", trustOperationId),
            ("$10", keyReference));
        var state = await Read(connection, transaction, cancellationToken) ?? throw Invalid();
        if (state.Request != request) throw new InvalidOperationException(
            "A different terminal assignment is already provisioned in this database.");
        await transaction.CommitAsync(cancellationToken);
        return state;
    }

    public async Task<DeviceProvisioningState> BindPublicKeyAsync(DeviceProvisioningState state, string fingerprint,
        CancellationToken cancellationToken)
    {
        ValidateFingerprint(fingerprint);
        return await Mutate(state, async (connection, transaction, current, ct) =>
        {
            if (current.PublicKeyFingerprint is not null && current.PublicKeyFingerprint != fingerprint)
                throw Changed();
            var written = await Execute(connection, transaction, """
                UPDATE device_provisioning_state SET public_key_fingerprint=$1
                WHERE singleton=1 AND (public_key_fingerprint IS NULL OR public_key_fingerprint=$1)
                """, ct, ("$1", fingerprint));
            if (written != 1) throw Changed();
        }, cancellationToken);
    }

    public async Task<DeviceProvisioningState> RecordRegistrationAsync(DeviceProvisioningState state,
        ProvisionedDevice device, CancellationToken cancellationToken) =>
        await Mutate(state, async (connection, transaction, current, ct) =>
        {
            if (current.Device is not null)
            {
                if (current.Device != device) throw Changed();
                return;
            }
            var credential = device.Credential ?? throw Invalid();
            var written = await Execute(connection, transaction, """
                UPDATE device_provisioning_state SET device_id=$1,device_status=$2,registered_at=$3,registered_by=$4,
                credential_id=$5,credential_algorithm=$6,proof_challenge=$7,proof_expires_at=$8,credential_status=$9
                WHERE singleton=1 AND device_id IS NULL
                """, ct, ("$1", device.Id), ("$2", device.Status), ("$3", device.RegisteredAt.ToString("O")),
                ("$4", device.RegisteredBy), ("$5", credential.Id), ("$6", credential.Algorithm),
                ("$7", credential.ProofChallenge), ("$8", credential.ProofExpiresAt.ToString("O")),
                ("$9", credential.Status));
            if (written != 1) throw Changed();
        }, cancellationToken);

    public async Task<DeviceProvisioningState> RecordTrustAsync(DeviceProvisioningState state,
        ProvisionedDevice device, CancellationToken cancellationToken) =>
        await Mutate(state, async (connection, transaction, current, ct) =>
        {
            if (current.Device?.Status == "active")
            {
                if (current.Device != device) throw Changed();
                return;
            }
            if (current.Device is null || device.Credential is null || !SameRegistration(current.Device, device))
                throw Changed();
            var written = await Execute(connection, transaction, """
                UPDATE device_provisioning_state SET device_status='active',credential_status='active'
                WHERE singleton=1 AND device_id=$1 AND device_status='pending' AND credential_id=$2
                """, ct, ("$1", device.Id), ("$2", device.Credential.Id));
            if (written != 1) throw Changed();
        }, cancellationToken);

    private async Task<DeviceProvisioningState> Mutate(DeviceProvisioningState expected,
        Func<SqliteConnection, SqliteTransaction, DeviceProvisioningState, CancellationToken, Task> mutation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        await using var connection = await Open(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await Execute(connection, transaction, Schema, cancellationToken);
        var current = await Read(connection, transaction, cancellationToken) ?? throw Invalid();
        if (current != expected) throw Changed();
        await mutation(connection, transaction, current, cancellationToken);
        var result = await Read(connection, transaction, cancellationToken) ?? throw Invalid();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static bool SameRegistration(ProvisionedDevice left, ProvisionedDevice right) =>
        left with { Status = right.Status, Credential = left.Credential! with { Status = right.Credential!.Status } }
        == right;

    private static async Task<DeviceProvisioningState?> Read(SqliteConnection connection,
        SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var query = Command(connection, transaction, """
            SELECT organization_id,branch_id,register_id,code,name,platform,sync_protocol_version,
            registration_operation_id,trust_operation_id,key_reference,public_key_fingerprint,device_id,device_status,
            registered_at,registered_by,credential_id,credential_algorithm,proof_challenge,proof_expires_at,
            credential_status FROM device_provisioning_state WHERE singleton=1
            """);
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        try
        {
            var request = new DeviceProvisioningRequest(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)), reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.GetInt32(6));
            ProvisionedDevice? device = null;
            if (!reader.IsDBNull(11))
            {
                var credential = new DeviceCredential(Guid.Parse(reader.GetString(15)), reader.GetString(16),
                    reader.GetString(17), DateTimeOffset.Parse(reader.GetString(18)), reader.GetString(19));
                device = new ProvisionedDevice(Guid.Parse(reader.GetString(11)), request.BranchId, request.RegisterId,
                    request.Code, request.Name, request.Platform, reader.GetString(12), request.SyncProtocolVersion,
                    DateTimeOffset.Parse(reader.GetString(13)), reader.GetString(14), credential, null, null, null);
            }
            var state = new DeviceProvisioningState(request, Guid.Parse(reader.GetString(7)),
                Guid.Parse(reader.GetString(8)), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
                device);
            ValidateState(state);
            return state;
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidOperationException("The local device provisioning state is corrupt.", exception);
        }
    }

    private static void ValidateState(DeviceProvisioningState state)
    {
        if (state.Request.OrganizationId == Guid.Empty || state.Request.BranchId == Guid.Empty
            || state.Request.RegisterId == Guid.Empty || state.Request.SyncProtocolVersion != 1
            || state.RegistrationOperationId == Guid.Empty || state.TrustOperationId == Guid.Empty
            || string.IsNullOrWhiteSpace(state.KeyReference)) throw Invalid();
        if (state.PublicKeyFingerprint is not null) ValidateFingerprint(state.PublicKeyFingerprint);
        if (state.Device is not null && (state.Device.Id == Guid.Empty || state.Device.Credential is null
            || state.Device.Credential.Id == Guid.Empty || state.Device.Status is not ("pending" or "active")
            || state.Device.Credential.Status != state.Device.Status)) throw Invalid();
    }

    private static void ValidateFingerprint(string value)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length != 32 || Convert.ToBase64String(bytes) != value) throw Invalid();
        }
        catch (FormatException) { throw Invalid(); }
    }

    private async Task<SqliteConnection> Open(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await Execute(connection, null, "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=10000;", cancellationToken);
        return connection;
    }

    private static async Task<int> Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        CancellationToken cancellationToken, params (string Name, object Value)[] values)
    {
        await using var command = Command(connection, transaction, sql, values);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object Value)[] values)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        foreach (var (name, value) in values)
            command.Parameters.AddWithValue(name, value is Guid id ? id.ToString("D") : value);
        return command;
    }

    private static string BuildConnection(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Database path is required.");
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return new SqliteConnectionStringBuilder
        {
            DataSource = full,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();
    }

    private static InvalidOperationException Invalid() => new("The local device provisioning state is invalid.");
    private static InvalidOperationException Changed() => new("The local device provisioning state changed.");

    internal const string Schema = """
        CREATE TABLE IF NOT EXISTS device_provisioning_state(
        singleton INTEGER PRIMARY KEY CHECK(singleton=1),organization_id TEXT NOT NULL,branch_id TEXT NOT NULL,
        register_id TEXT NOT NULL,code TEXT NOT NULL,name TEXT NOT NULL,platform TEXT NOT NULL,
        sync_protocol_version INTEGER NOT NULL CHECK(sync_protocol_version=1),registration_operation_id TEXT NOT NULL,
        trust_operation_id TEXT NOT NULL,key_reference TEXT NOT NULL,public_key_fingerprint TEXT NULL,
        device_id TEXT NULL,device_status TEXT NULL CHECK(device_status IN('pending','active')),registered_at TEXT NULL,
        registered_by TEXT NULL,credential_id TEXT NULL,credential_algorithm TEXT NULL,proof_challenge TEXT NULL,
        proof_expires_at TEXT NULL,credential_status TEXT NULL CHECK(credential_status IN('pending','active')),
        CHECK((device_id IS NULL AND device_status IS NULL AND registered_at IS NULL AND registered_by IS NULL
          AND credential_id IS NULL AND credential_algorithm IS NULL AND proof_challenge IS NULL
          AND proof_expires_at IS NULL AND credential_status IS NULL) OR
         (device_id IS NOT NULL AND device_status IS NOT NULL AND registered_at IS NOT NULL AND registered_by IS NOT NULL
          AND credential_id IS NOT NULL AND credential_algorithm IS NOT NULL AND proof_challenge IS NOT NULL
          AND proof_expires_at IS NOT NULL AND credential_status=device_status))) STRICT;
        """;
}
