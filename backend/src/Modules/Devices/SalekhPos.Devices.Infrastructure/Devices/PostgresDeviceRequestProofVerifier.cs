using System.Globalization;
using System.Security.Cryptography;
using Npgsql;
using SalekhPos.Devices.Application.Devices;

namespace SalekhPos.Devices.Infrastructure.Devices;

public sealed class PostgresDeviceRequestProofVerifier(NpgsqlDataSource? source) : IDeviceRequestProofVerifier
{
    private const string Algorithm = "ecdsa-p256-sha256";
    private const string P256Oid = "1.2.840.10045.3.1.7";
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan NonceRetention = (AllowedClockSkew * 2) + TimeSpan.FromMinutes(1);

    public async Task VerifyAsync(DeviceRequestProofContext x, CancellationToken ct)
    {
        ValidateContext(x);
        var ds = source ?? throw new DeviceUnavailableException();
        await using var connection = await ds.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetContext(connection, transaction, x, ct);

        var credential = await ReadCredential(connection, transaction, x, ct)
            ?? throw new DeviceRequestAuthenticationException();
        if (x.ExpectedRegisterId is Guid expectedRegisterId && credential.RegisterId != expectedRegisterId)
        {
            throw new DeviceRequestAuthenticationException();
        }

        if (!credential.HasCredential)
        {
            if (x.RequireCredential || credential.DeviceStatus != "active" || !x.Headers.IsEmpty
                || !string.Equals(credential.RegisteredIssuer, x.Identity.Issuer, StringComparison.Ordinal)
                || !string.Equals(credential.RegisteredBy, x.Identity.Subject, StringComparison.Ordinal))
            {
                throw new DeviceRequestAuthenticationException();
            }

            await transaction.CommitAsync(ct);
            return;
        }

        if (credential.DeviceStatus != "active" || credential.CredentialId is null
            || credential.Algorithm != Algorithm || credential.PublicKey is null || credential.Fingerprint is null)
        {
            throw new DeviceRequestAuthenticationException();
        }

        var suppliedCredentialId = ParseCredentialId(x.Headers.CredentialId);
        var timestamp = ParseTimestamp(x.Headers.Timestamp);
        var nonce = ParseNonce(x.Headers.Nonce);
        var signature = ParseSignature(x.Headers.Signature);
        if (suppliedCredentialId != credential.CredentialId)
        {
            throw new DeviceRequestAuthenticationException();
        }

        var now = await DatabaseTime(connection, transaction, ct);
        if (timestamp < now - AllowedClockSkew || timestamp > now + AllowedClockSkew)
        {
            throw new DeviceRequestAuthenticationException();
        }

        var canonical = DeviceRequestProofCanonicalizer.Create(x.Method, x.CanonicalPath, x.OrganizationId,
            x.BranchId, x.DeviceId, suppliedCredentialId, x.OperationIdentity, x.BodyDigest,
            x.Headers.Timestamp!, x.Headers.Nonce!, credential.Fingerprint);
        if (!Verify(credential.PublicKey, canonical, signature))
        {
            throw new DeviceRequestAuthenticationException();
        }

        var nonceHash = SHA256.HashData(nonce);
        try
        {
            await using (var command = new NpgsqlCommand("SELECT devices.purge_expired_request_proof_nonces()", connection, transaction))
            {
                await command.ExecuteScalarAsync(ct);
            }

            await using (var command = new NpgsqlCommand("INSERT INTO devices.request_proof_nonces(organization_id,device_id,credential_id,nonce_hash,request_timestamp,accepted_at,expires_at) VALUES($1,$2,$3,$4,$5,$6,$7)", connection, transaction))
            {
                command.Parameters.AddWithValue(x.OrganizationId);
                command.Parameters.AddWithValue(x.DeviceId);
                command.Parameters.AddWithValue(suppliedCredentialId);
                command.Parameters.AddWithValue(nonceHash);
                command.Parameters.AddWithValue(timestamp);
                command.Parameters.AddWithValue(now);
                command.Parameters.AddWithValue(now + NonceRetention);
                await command.ExecuteNonQueryAsync(ct);
            }
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DeviceRequestAuthenticationException();
        }

        await transaction.CommitAsync(ct);
    }

    private static async Task<CredentialRow?> ReadCredential(NpgsqlConnection connection, NpgsqlTransaction transaction,
        DeviceRequestProofContext x, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT d.status,d.issuer,d.registered_by,d.register_id,c.credential_id,c.algorithm,c.public_key_spki,c.public_key_fingerprint,EXISTS(SELECT FROM devices.device_credentials c0 WHERE c0.organization_id=d.organization_id AND c0.device_id=d.device_id) FROM devices.registered_devices d LEFT JOIN LATERAL(SELECT credential_id,algorithm,public_key_spki,public_key_fingerprint FROM devices.device_credentials c1 WHERE c1.organization_id=d.organization_id AND c1.device_id=d.device_id AND c1.status='active' ORDER BY c1.created_at DESC,c1.credential_id DESC LIMIT 1)c ON true WHERE d.organization_id=$1 AND d.branch_id=$2 AND d.device_id=$3", connection, transaction);
        command.Parameters.AddWithValue(x.OrganizationId);
        command.Parameters.AddWithValue(x.BranchId);
        command.Parameters.AddWithValue(x.DeviceId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetGuid(3),
            reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : (byte[])reader[6], reader.IsDBNull(7) ? null : (byte[])reader[7], reader.GetBoolean(8));
    }

    private static void ValidateContext(DeviceRequestProofContext x)
    {
        if (x.OrganizationId == Guid.Empty || x.BranchId == Guid.Empty || x.DeviceId == Guid.Empty
            || x.ExpectedRegisterId == Guid.Empty
            || string.IsNullOrWhiteSpace(x.Identity.Issuer) || string.IsNullOrWhiteSpace(x.Identity.Subject)
            || x.Method.Length is < 3 or > 16 || x.Method.Any(c => c is < 'A' or > 'Z')
            || string.IsNullOrWhiteSpace(x.CanonicalPath) || x.CanonicalPath[0] != '/' || x.CanonicalPath.Contains('?')
            || string.IsNullOrWhiteSpace(x.OperationIdentity) || x.OperationIdentity.Length > 128
            || x.BodyDigest.Length != 64 || x.BodyDigest.Any(c => !(char.IsAsciiDigit(c) || c is >= 'A' and <= 'F')))
        {
            throw new DeviceRequestAuthenticationException();
        }
    }

    private static Guid ParseCredentialId(string? value)
    {
        if (value is null || !Guid.TryParseExact(value, "D", out var result) || result == Guid.Empty
            || !string.Equals(value, result.ToString("D"), StringComparison.Ordinal))
            throw new DeviceRequestAuthenticationException();
        return result;
    }

    private static DateTimeOffset ParseTimestamp(string? value)
    {
        if (value is null || value.Length != 28
            || !DateTimeOffset.TryParseExact(value, TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result)
            || !string.Equals(value, result.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture), StringComparison.Ordinal))
            throw new DeviceRequestAuthenticationException();
        return result;
    }

    private static byte[] ParseNonce(string? value)
    {
        if (value is null || value.Length != 43 || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new DeviceRequestAuthenticationException();
        try
        {
            var bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + "=");
            if (bytes.Length != 32 || !string.Equals(value, Base64Url(bytes), StringComparison.Ordinal))
                throw new DeviceRequestAuthenticationException();
            return bytes;
        }
        catch (FormatException)
        {
            throw new DeviceRequestAuthenticationException();
        }
    }

    private static byte[] ParseSignature(string? value)
    {
        if (value is null || value.Length > 128) throw new DeviceRequestAuthenticationException();
        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length != 64 || !string.Equals(value, Convert.ToBase64String(bytes), StringComparison.Ordinal))
                throw new DeviceRequestAuthenticationException();
            return bytes;
        }
        catch (FormatException)
        {
            throw new DeviceRequestAuthenticationException();
        }
    }

    private static bool Verify(byte[] publicKey, byte[] proof, byte[] signature)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(publicKey, out var read);
            var parameters = key.ExportParameters(false);
            return read == publicKey.Length && parameters.Curve.IsNamed
                && string.Equals(parameters.Curve.Oid.Value, P256Oid, StringComparison.Ordinal)
                && parameters.Q.X?.Length == 32 && parameters.Q.Y?.Length == 32
                && key.VerifyData(proof, signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static async Task SetContext(NpgsqlConnection connection, NpgsqlTransaction transaction,
        DeviceRequestProofContext x, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),set_config('app.subject',$3,true)", connection, transaction);
        command.Parameters.AddWithValue(x.OrganizationId.ToString("D"));
        command.Parameters.AddWithValue(x.Identity.Issuer);
        command.Parameters.AddWithValue(x.Identity.Subject);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<DateTimeOffset> DatabaseTime(NpgsqlConnection connection, NpgsqlTransaction transaction,
        CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT statement_timestamp()", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new DeviceUnavailableException();
        return reader.GetFieldValue<DateTimeOffset>(0);
    }

    private static string Base64Url(ReadOnlySpan<byte> value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record CredentialRow(string DeviceStatus, string RegisteredIssuer, string RegisteredBy, Guid RegisterId,
        Guid? CredentialId, string? Algorithm, byte[]? PublicKey, byte[]? Fingerprint, bool HasCredential);
}
