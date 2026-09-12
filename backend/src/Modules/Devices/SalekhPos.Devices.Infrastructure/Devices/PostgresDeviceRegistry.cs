using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using SalekhPos.Devices.Application.Devices;
using SalekhPos.Devices.Contracts.Devices;
using SalekhPos.Devices.Domain.Devices;
namespace SalekhPos.Devices.Infrastructure.Devices;

public sealed class PostgresDeviceRegistry(NpgsqlDataSource? source) : IDeviceRegistry
{
    private const string Algorithm = "ecdsa-p256-sha256";
    private const string P256Oid = "1.2.840.10045.3.1.7";
    private const string Select = "SELECT d.device_id,d.branch_id,d.register_id,d.code,d.name,d.platform,d.status,d.sync_protocol_version,d.registered_at,d.registered_by,d.revoked_at,d.revoked_by,d.revocation_reason,d.revocation_operation_id,c.credential_id,c.algorithm,c.proof_challenge,c.proof_expires_at,c.status,c.public_key_spki,c.public_key_fingerprint,c.activation_operation_id FROM devices.registered_devices d LEFT JOIN LATERAL(SELECT credential_id,algorithm,proof_challenge,proof_expires_at,status,public_key_spki,public_key_fingerprint,activation_operation_id FROM devices.device_credentials WHERE organization_id=d.organization_id AND device_id=d.device_id ORDER BY created_at DESC,credential_id DESC LIMIT 1)c ON true";

    public async Task<DeviceWriteResult> RegisterAsync(DeviceIdentity identity, RegisterDeviceCommand x, CancellationToken ct)
    {
        _ = new RegisteredDevice(x.DeviceId, x.RegisterId, x.Code, x.Name, x.Platform, x.SyncProtocolVersion, x.PublicKey);
        Validate(identity, x.OrganizationId, x.BranchId, x.OperationId); var publicKey = PublicKey(x.PublicKey);
        var ds = source ?? throw new DeviceUnavailableException(); await using var c = await ds.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Demand(c, t, identity, x.OrganizationId, x.BranchId, "devices.manage", ct);
        var replay = await ByOperation(c, t, x.OrganizationId, x.OperationId, ct);
        if (replay is not null)
        {
            if (replay.Response.BranchId != x.BranchId || replay.Response.RegisterId != x.RegisterId || replay.Response.Code != x.Code || replay.Response.Name != x.Name || replay.Response.Platform != x.Platform || replay.Response.SyncProtocolVersion != x.SyncProtocolVersion || replay.PublicKey is null || replay.Fingerprint is null || !CryptographicOperations.FixedTimeEquals(replay.PublicKey, publicKey.SubjectPublicKeyInfo) || !CryptographicOperations.FixedTimeEquals(replay.Fingerprint, publicKey.Fingerprint)) throw new DeviceConflictException();
            await t.CommitAsync(ct); return new(replay.Response, false);
        }
        if (!await RegisterExists(c, t, x.OrganizationId, x.BranchId, x.RegisterId, ct)) throw new DeviceConflictException();
        var at = await Time(c, t, ct); var credentialId = Guid.NewGuid(); var challenge = RandomNumberGenerator.GetBytes(32); var expires = at.AddMinutes(15);
        try
        {
            await using (var q = new NpgsqlCommand("INSERT INTO devices.registered_devices(organization_id,device_id,operation_id,branch_id,register_id,code,name,platform,status,sync_protocol_version,registered_at,issuer,registered_by) VALUES($1,$2,$3,$4,$5,$6,$7,$8,'pending',$9,$10,$11,$12)", c, t)) { q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.DeviceId); q.Parameters.AddWithValue(x.OperationId); q.Parameters.AddWithValue(x.BranchId); q.Parameters.AddWithValue(x.RegisterId); q.Parameters.AddWithValue(x.Code); q.Parameters.AddWithValue(x.Name); q.Parameters.AddWithValue(x.Platform); q.Parameters.AddWithValue(x.SyncProtocolVersion); q.Parameters.AddWithValue(at); q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject); await q.ExecuteNonQueryAsync(ct); }
            await using (var q = new NpgsqlCommand("INSERT INTO devices.device_credentials(organization_id,device_id,credential_id,created_operation_id,algorithm,public_key_spki,public_key_fingerprint,status,proof_challenge,proof_expires_at,created_at,created_issuer,created_by) VALUES($1,$2,$3,$4,$5,$6,$7,'pending',$8,$9,$10,$11,$12)", c, t)) { q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.DeviceId); q.Parameters.AddWithValue(credentialId); q.Parameters.AddWithValue(x.OperationId); q.Parameters.AddWithValue(Algorithm); q.Parameters.AddWithValue(publicKey.SubjectPublicKeyInfo); q.Parameters.AddWithValue(publicKey.Fingerprint); q.Parameters.AddWithValue(challenge); q.Parameters.AddWithValue(expires); q.Parameters.AddWithValue(at); q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject); await q.ExecuteNonQueryAsync(ct); }
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation) { throw new DeviceConflictException(); }
        await t.CommitAsync(ct); var credential = new DeviceCredentialResponse(credentialId, Algorithm, Convert.ToBase64String(challenge), expires, "pending");
        return new(new(x.DeviceId, x.BranchId, x.RegisterId, x.Code, x.Name, x.Platform, "pending", x.SyncProtocolVersion, at, identity.Subject, credential, null, null, null), true);
    }

    public async Task<DeviceResponse> TrustAsync(DeviceIdentity identity, TrustDeviceCommand x, CancellationToken ct)
    {
        Validate(identity, x.OrganizationId, x.BranchId, x.DeviceId); if (x.OperationId == Guid.Empty || x.CredentialId == Guid.Empty) throw new ArgumentException("Device trust request is invalid.");
        var challenge = Base64(x.ProofChallenge, 32, 32); var signature = Base64(x.Signature, 64, 64);
        var ds = source ?? throw new DeviceUnavailableException(); await using var c = await ds.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Demand(c, t, identity, x.OrganizationId, x.BranchId, "devices.manage", ct); await Lock(c, t, x.OrganizationId, x.DeviceId, ct);
        var row = await ById(c, t, x.OrganizationId, x.BranchId, x.DeviceId, ct) ?? throw new DeviceConflictException();
        if (row.CredentialId != x.CredentialId || row.PublicKey is null || row.Fingerprint is null || row.Challenge is null) throw new DeviceConflictException();
        if (!CryptographicOperations.FixedTimeEquals(row.Challenge, challenge) || !Verify(row.PublicKey, Proof(x, row.Challenge, row.Fingerprint), signature)) throw new DeviceProofException();
        if (row.Response.Status == "active") { if (row.ActivationOperationId != x.OperationId) throw new DeviceConflictException(); await t.CommitAsync(ct); return row.Response; }
        var now = await Time(c, t, ct); if (row.Response.Status != "pending" || row.CredentialStatus != "pending" || row.ProofExpiresAt <= now) throw new DeviceConflictException();
        try
        {
            await using (var q = new NpgsqlCommand("UPDATE devices.device_credentials SET status='active',activated_at=$4,activated_issuer=$5,activated_by=$6,activation_operation_id=$7 WHERE organization_id=$1 AND device_id=$2 AND credential_id=$3 AND status='pending'", c, t)) { q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.DeviceId); q.Parameters.AddWithValue(x.CredentialId); q.Parameters.AddWithValue(now); q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject); q.Parameters.AddWithValue(x.OperationId); if (await q.ExecuteNonQueryAsync(ct) != 1) throw new DeviceConflictException(); }
            await using (var q = new NpgsqlCommand("UPDATE devices.registered_devices SET status='active' WHERE organization_id=$1 AND branch_id=$2 AND device_id=$3 AND status='pending'", c, t)) { q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.BranchId); q.Parameters.AddWithValue(x.DeviceId); if (await q.ExecuteNonQueryAsync(ct) != 1) throw new DeviceConflictException(); }
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation) { throw new DeviceConflictException(); }
        var result = (await ById(c, t, x.OrganizationId, x.BranchId, x.DeviceId, ct))!.Response; await t.CommitAsync(ct); return result;
    }

    public async Task<DeviceResponse> RevokeAsync(DeviceIdentity identity, RevokeDeviceCommand x, CancellationToken ct)
    {
        Validate(identity, x.OrganizationId, x.BranchId, x.DeviceId); if (x.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(x.Reason) || x.Reason != x.Reason.Trim() || x.Reason.Length > 256 || x.Reason.Any(char.IsControl)) throw new ArgumentException("Device revocation request is invalid.");
        var ds = source ?? throw new DeviceUnavailableException(); await using var c = await ds.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct);
        await Demand(c, t, identity, x.OrganizationId, x.BranchId, "devices.manage", ct); await Lock(c, t, x.OrganizationId, x.DeviceId, ct);
        var row = await ById(c, t, x.OrganizationId, x.BranchId, x.DeviceId, ct) ?? throw new DeviceConflictException();
        if (row.Response.Status == "revoked") { if (row.RevocationOperationId != x.OperationId || row.Response.RevocationReason != x.Reason) throw new DeviceConflictException(); await t.CommitAsync(ct); return row.Response; }
        var at = await Time(c, t, ct);
        try
        {
            await using (var q = new NpgsqlCommand("UPDATE devices.device_credentials SET status='revoked',revoked_at=$3,revoked_issuer=$4,revoked_by=$5,revocation_operation_id=$6 WHERE organization_id=$1 AND device_id=$2 AND status IN('pending','active')", c, t)) { q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.DeviceId); q.Parameters.AddWithValue(at); q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject); q.Parameters.AddWithValue(x.OperationId); await q.ExecuteNonQueryAsync(ct); }
            await using (var q = new NpgsqlCommand("UPDATE devices.registered_devices SET status='revoked',revoked_at=$4,revoked_issuer=$5,revoked_by=$6,revocation_operation_id=$7,revocation_reason=$8 WHERE organization_id=$1 AND branch_id=$2 AND device_id=$3 AND status IN('pending','active')", c, t)) { q.Parameters.AddWithValue(x.OrganizationId); q.Parameters.AddWithValue(x.BranchId); q.Parameters.AddWithValue(x.DeviceId); q.Parameters.AddWithValue(at); q.Parameters.AddWithValue(identity.Issuer); q.Parameters.AddWithValue(identity.Subject); q.Parameters.AddWithValue(x.OperationId); q.Parameters.AddWithValue(x.Reason); if (await q.ExecuteNonQueryAsync(ct) != 1) throw new DeviceConflictException(); }
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation) { throw new DeviceConflictException(); }
        var result = (await ById(c, t, x.OrganizationId, x.BranchId, x.DeviceId, ct))!.Response; await t.CommitAsync(ct); return result;
    }

    public async Task<DeviceResponse?> ReadAsync(DeviceIdentity identity, Guid org, Guid branch, Guid id, CancellationToken ct)
    { Validate(identity, org, branch, id); var ds = source ?? throw new DeviceUnavailableException(); await using var c = await ds.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct); await Demand(c, t, identity, org, branch, "devices.view", ct); var result = (await ById(c, t, org, branch, id, ct))?.Response; await t.CommitAsync(ct); return result; }
    public async Task<DevicePage> ListAsync(DeviceIdentity identity, Guid org, Guid branch, int size, Guid? after, CancellationToken ct)
    { if (size is < 1 or > 100 || after == Guid.Empty) throw new ArgumentException("Device query is invalid."); ValidateList(identity, org, branch); var ds = source ?? throw new DeviceUnavailableException(); await using var c = await ds.OpenConnectionAsync(ct); await using var t = await c.BeginTransactionAsync(ct); await Demand(c, t, identity, org, branch, "devices.view", ct); var items = new List<DeviceResponse>(); await using (var q = new NpgsqlCommand($"{Select} WHERE d.organization_id=$1 AND d.branch_id=$2 AND ($3::uuid IS NULL OR d.device_id>$3) ORDER BY d.device_id LIMIT $4", c, t)) { q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(branch); q.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Uuid, Value = (object?)after ?? DBNull.Value }); q.Parameters.AddWithValue(size + 1); await using var r = await q.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) items.Add(Read(r).Response); } Guid? next = null; if (items.Count > size) { items.RemoveAt(size); next = items[^1].Id; } await t.CommitAsync(ct); return new(items.AsReadOnly(), next); }

    private static async Task<DeviceRow?> ById(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid branch, Guid id, CancellationToken ct) { await using var q = new NpgsqlCommand($"{Select} WHERE d.organization_id=$1 AND d.branch_id=$2 AND d.device_id=$3", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(branch); q.Parameters.AddWithValue(id); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Read(r) : null; }
    private static async Task<DeviceRow?> ByOperation(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid op, CancellationToken ct) { await using var q = new NpgsqlCommand($"{Select} WHERE d.organization_id=$1 AND d.operation_id=$2", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(op); await using var r = await q.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Read(r) : null; }
    private static DeviceRow Read(NpgsqlDataReader r)
    {
        var credential = r.IsDBNull(14) ? null : new DeviceCredentialResponse(r.GetGuid(14), r.GetString(15), Convert.ToBase64String((byte[])r[16]), r.GetFieldValue<DateTimeOffset>(17), r.GetString(18));
        var response = new DeviceResponse(r.GetGuid(0), r.GetGuid(1), r.GetGuid(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetInt32(7), r.GetFieldValue<DateTimeOffset>(8), r.GetString(9), credential, r.IsDBNull(10) ? null : r.GetFieldValue<DateTimeOffset>(10), r.IsDBNull(11) ? null : r.GetString(11), r.IsDBNull(12) ? null : r.GetString(12));
        return new(response, r.IsDBNull(14) ? null : r.GetGuid(14), r.IsDBNull(18) ? null : r.GetString(18), r.IsDBNull(19) ? null : (byte[])r[19], r.IsDBNull(20) ? null : (byte[])r[20], r.IsDBNull(16) ? null : (byte[])r[16], r.IsDBNull(17) ? null : r.GetFieldValue<DateTimeOffset>(17), r.IsDBNull(21) ? null : r.GetGuid(21), r.IsDBNull(13) ? null : r.GetGuid(13));
    }
    private static ParsedPublicKey PublicKey(string value) { var bytes = Base64(value, 64, 256); try { using var key = ECDsa.Create(); key.ImportSubjectPublicKeyInfo(bytes, out var read); var parameters = key.ExportParameters(false); if (read != bytes.Length || !parameters.Curve.IsNamed || !string.Equals(parameters.Curve.Oid.Value, P256Oid, StringComparison.Ordinal) || parameters.Q.X?.Length != 32 || parameters.Q.Y?.Length != 32) throw new ArgumentException("Device public key is invalid."); var canonical = key.ExportSubjectPublicKeyInfo(); return new(canonical, SHA256.HashData(canonical)); } catch (CryptographicException) { throw new ArgumentException("Device public key is invalid."); } }
    private static byte[] Base64(string value, int minimum, int maximum) { if (string.IsNullOrWhiteSpace(value) || value.Length > 4096) throw new ArgumentException("Device proof is invalid."); try { var bytes = Convert.FromBase64String(value); if (bytes.Length < minimum || bytes.Length > maximum) throw new ArgumentException("Device proof is invalid."); return bytes; } catch (FormatException) { throw new ArgumentException("Device proof is invalid."); } }
    private static byte[] Proof(TrustDeviceCommand x, byte[] challenge, byte[] fingerprint) => Encoding.UTF8.GetBytes($"salekhpos-device-trust-v1\n{x.OrganizationId:D}\n{x.BranchId:D}\n{x.DeviceId:D}\n{x.CredentialId:D}\n{x.OperationId:D}\n{Convert.ToBase64String(challenge)}\n{Convert.ToBase64String(fingerprint)}");
    private static bool Verify(byte[] publicKey, byte[] proof, byte[] signature) { try { using var key = ECDsa.Create(); key.ImportSubjectPublicKeyInfo(publicKey, out var read); var parameters = key.ExportParameters(false); return read == publicKey.Length && parameters.Curve.IsNamed && string.Equals(parameters.Curve.Oid.Value, P256Oid, StringComparison.Ordinal) && parameters.Q.X?.Length == 32 && parameters.Q.Y?.Length == 32 && key.VerifyData(proof, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation); } catch (CryptographicException) { return false; } }
    private static async Task Lock(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid device, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended($1,0))", c, t); q.Parameters.AddWithValue($"device-trust:{org:D}:{device:D}"); await q.ExecuteNonQueryAsync(ct); }
    private static async Task<bool> RegisterExists(NpgsqlConnection c, NpgsqlTransaction t, Guid org, Guid branch, Guid register, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT EXISTS(SELECT FROM stores.registers WHERE organization_id=$1 AND branch_id=$2 AND register_id=$3 AND is_active)", c, t); q.Parameters.AddWithValue(org); q.Parameters.AddWithValue(branch); q.Parameters.AddWithValue(register); return await q.ExecuteScalarAsync(ct) is true; }
    private static void Validate(DeviceIdentity i, Guid org, Guid branch, Guid value) { ValidateList(i, org, branch); if (value == Guid.Empty) throw new ArgumentException("Device request is invalid."); }
    private static void ValidateList(DeviceIdentity i, Guid org, Guid branch) { if (org == Guid.Empty || branch == Guid.Empty || string.IsNullOrWhiteSpace(i.Issuer) || string.IsNullOrWhiteSpace(i.Subject)) throw new ArgumentException("Device request is invalid."); }
    private static async Task<DateTimeOffset> Time(NpgsqlConnection c, NpgsqlTransaction t, CancellationToken ct) { await using var q = new NpgsqlCommand("SELECT statement_timestamp()", c, t); await using var reader = await q.ExecuteReaderAsync(ct); if (!await reader.ReadAsync(ct)) throw new DeviceUnavailableException(); return reader.GetFieldValue<DateTimeOffset>(0); }
    private static async Task Demand(NpgsqlConnection c, NpgsqlTransaction t, DeviceIdentity i, Guid org, Guid branch, string permission, CancellationToken ct) { await using (var q = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", c, t)) { q.Parameters.AddWithValue(org.ToString()); q.Parameters.AddWithValue(i.Issuer); q.Parameters.AddWithValue(i.Subject); await q.ExecuteNonQueryAsync(ct); } await using var d = new NpgsqlCommand("SELECT EXISTS(SELECT FROM access.memberships m JOIN access.permission_grants g ON g.organization_id=m.organization_id AND g.membership_id=m.membership_id JOIN organization.branches b ON b.organization_id=m.organization_id AND b.branch_id=$2 JOIN organization.businesses z ON z.organization_id=b.organization_id AND z.business_id=b.business_id JOIN organization.organizations a ON a.organization_id=b.organization_id WHERE m.organization_id=$1 AND m.issuer=$3 AND m.subject=$4 AND m.is_active AND b.is_active AND z.is_active AND a.is_active AND m.valid_from<=statement_timestamp() AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp()) AND g.permission=$5 AND (g.scope_kind='organization' OR (g.scope_kind='business' AND g.business_id=b.business_id) OR (g.scope_kind='region' AND g.business_id=b.business_id AND g.region_id=b.region_id) OR (g.scope_kind='branch' AND g.business_id=b.business_id AND g.branch_id=b.branch_id)))", c, t); d.Parameters.AddWithValue(org); d.Parameters.AddWithValue(branch); d.Parameters.AddWithValue(i.Issuer); d.Parameters.AddWithValue(i.Subject); d.Parameters.AddWithValue(permission); if (await d.ExecuteScalarAsync(ct) is not true) throw new DeviceDeniedException(); }
    private sealed record ParsedPublicKey(byte[] SubjectPublicKeyInfo, byte[] Fingerprint);
    private sealed record DeviceRow(DeviceResponse Response, Guid? CredentialId, string? CredentialStatus, byte[]? PublicKey, byte[]? Fingerprint, byte[]? Challenge, DateTimeOffset? ProofExpiresAt, Guid? ActivationOperationId, Guid? RevocationOperationId);
}
