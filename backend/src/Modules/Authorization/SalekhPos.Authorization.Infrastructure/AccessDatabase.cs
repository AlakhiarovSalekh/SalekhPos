using Npgsql;

namespace SalekhPos.Authorization.Infrastructure;

public sealed class AccessDatabase : IAsyncDisposable
{
    public NpgsqlDataSource? DataSource { get; }
    public bool IsConfigured => DataSource is not null;

    public AccessDatabase(string? connectionString, bool allowLocalInsecureTransport)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        NpgsqlConnectionStringBuilder options;
        try
        {
            options = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("The application database configuration is invalid.");
        }

        var local = options.Host is "localhost" or "127.0.0.1" or "::1";
        if (options.Username != "salekhpos_runtime" || string.IsNullOrWhiteSpace(options.Database)
            || (!allowLocalInsecureTransport || !local) && options.SslMode != SslMode.VerifyFull
            || options.MaxPoolSize is < 1 or > 100 || options.MinPoolSize > options.MaxPoolSize
            || options.Timeout is < 1 or > 30 || options.CommandTimeout is < 1 or > 30
            || options.NoResetOnClose || !options.Pooling || options.Multiplexing)
        {
            throw new InvalidOperationException("The application database requires a restricted runtime user, verified TLS outside local development, and bounded pooling/timeouts.");
        }

        options.IncludeErrorDetail = false;
        options.LogParameters = false;
        options.PersistSecurityInfo = false;
        DataSource = NpgsqlDataSource.Create(options.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }
    }
}
