using Npgsql;
using SalekhPos.SystemAdministration.Domain;

// Out-of-band deployment tool. There is intentionally no HTTP bootstrap route.
// Connection credentials come only from the operator environment, never argv.
if (args.Length != 5 || args[0] != "bootstrap-root" || !Guid.TryParse(args[1], out var operationId))
{
    Console.Error.WriteLine("Usage: SalekhPos.Cli bootstrap-root <operation-id> <https-issuer> <verified-subject> <reason>");
    return 2;
}
try
{
    PlatformInput.Identifier(operationId);
    var identity = new PlatformIdentity(args[2], args[3]);
    var reason = PlatformInput.Text(args[4], 1000);
    var connectionString = Environment.GetEnvironmentVariable("SALEKHPOS_BOOTSTRAP_CONNECTION");
    if (string.IsNullOrWhiteSpace(connectionString)) { throw new ArgumentException("Missing bootstrap connection."); }
    var options = new NpgsqlConnectionStringBuilder(connectionString);
    var development = Environment.GetEnvironmentVariable("SALEKHPOS_BOOTSTRAP_LOCAL_DEVELOPMENT") == "true"
        && options.Host is "127.0.0.1" or "localhost" or "::1";
    if (options.Username == "salekhpos_runtime" || options.SslMode != SslMode.VerifyFull && !development)
    {
        throw new ArgumentException("Bootstrap requires separate deployment credentials and verified TLS.");
    }
    options.IncludeErrorDetail = false;
    options.LogParameters = false;
    options.Timeout = 10;
    options.CommandTimeout = 15;
    options.PersistSecurityInfo = false;
    await using var source = NpgsqlDataSource.Create(options.ConnectionString);
    await using var command = source.CreateCommand("SELECT system_administration.bootstrap_root($1,$2,$3,$4,$5)");
    command.Parameters.AddWithValue(operationId);
    command.Parameters.AddWithValue(identity.Issuer);
    command.Parameters.AddWithValue(identity.Subject);
    command.Parameters.AddWithValue(reason);
    command.Parameters.AddWithValue(Guid.NewGuid().ToString("N"));
    await command.ExecuteScalarAsync();
    Console.WriteLine("Root authority bootstrap committed. Keep the operation identifier for safe retries.");
    return 0;
}
catch (Exception exception) when (exception is NpgsqlException or ArgumentException or TimeoutException)
{
    // Provider exception messages can contain connection details or SQL input.
    Console.Error.WriteLine("Root bootstrap failed. Verify deployment permissions, identity, operation identifier and database availability.");
    return 1;
}
