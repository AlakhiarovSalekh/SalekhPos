using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using SalekhPos.Identity.Application.Sessions;

namespace SalekhPos.Identity.Infrastructure.Tokens;

// Cookies contain only an opaque reference. Serialized claims/provider tokens are
// encrypted with the deployment's data-protection keys before database storage.
public sealed class PostgresTicketStore(NpgsqlDataSource? source, IDataProtectionProvider protection) : ITicketStore
{
    private readonly IDataProtector protector = protection.CreateProtector("SalekhPos.WebSessions.v1");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await WriteAsync(key, ticket, true);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) => WriteAsync(key, ticket, false);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var hash = HashKey(key);
        if (hash is null) { return null; }
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetKeyAsync(connection, transaction, hash);
        await using var command = new NpgsqlCommand("""
            SELECT protected_ticket FROM identity.web_sessions
            WHERE key_hash=@key AND expires_at > clock_timestamp()
            """, connection, transaction);
        command.Parameters.AddWithValue("key", hash);
        if (await command.ExecuteScalarAsync() is not byte[] bytes) { return null; }
        await transaction.CommitAsync();
        try { return TicketSerializer.Default.Deserialize(protector.Unprotect(bytes)); }
        catch (CryptographicException) { return null; }
    }

    public async Task RemoveAsync(string key)
    {
        var hash = HashKey(key);
        if (hash is null) { return; }
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetKeyAsync(connection, transaction, hash);
        await using var command = new NpgsqlCommand("DELETE FROM identity.web_sessions WHERE key_hash=@key", connection, transaction);
        command.Parameters.AddWithValue("key", hash);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private async Task WriteAsync(string key, AuthenticationTicket ticket, bool insert)
    {
        var hash = HashKey(key) ?? throw new ArgumentException("Invalid session key.", nameof(key));
        var expires = ticket.Properties.ExpiresUtc ?? throw new ArgumentException("Session expiry is required.", nameof(ticket));
        var bytes = protector.Protect(TicketSerializer.Default.Serialize(ticket));
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetKeyAsync(connection, transaction, hash);
        // Never upsert a renewal: logout must not be undone by a concurrent request.
        var sql = insert
            ? "INSERT INTO identity.web_sessions(key_hash,protected_ticket,expires_at) VALUES (@key,@ticket,@expiry)"
            : "UPDATE identity.web_sessions SET protected_ticket=@ticket,expires_at=@expiry WHERE key_hash=@key AND expires_at > clock_timestamp()";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("key", hash);
        command.Parameters.AddWithValue("ticket", bytes);
        command.Parameters.AddWithValue("expiry", expires.UtcDateTime);
        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private async Task<NpgsqlConnection> OpenAsync() => source is null
        ? throw new IdentityUnavailableException()
        : await source.OpenConnectionAsync();

    private static string? HashKey(string key) => key.Length == 64 && key.All(char.IsAsciiHexDigit)
        ? Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(key))) : null;

    private static async Task SetKeyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string hash)
    {
        await using var command = new NpgsqlCommand("SELECT set_config('app.web_session_key',@key,true)", connection, transaction);
        command.Parameters.AddWithValue("key", hash);
        await command.ExecuteNonQueryAsync();
    }
}
