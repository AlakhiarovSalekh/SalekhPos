using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Domain.LocalSales;

namespace SalekhPos.Desktop.Infrastructure.LocalDatabase;

public sealed class SqliteLocalSaleStore
{
    private readonly string connectionString;
    private readonly SemaphoreSlim initialization = new(1, 1);
    private bool initialized;
    public SqliteLocalSaleStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath)) throw new ArgumentException("Database path is required.");
        var path = Path.GetFullPath(databasePath); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();
    }
    public async Task<ILocalSaleStore> OpenAsync(CancellationToken ct = default) { await Initialize(ct); return new Store(connectionString); }
    private async Task Initialize(CancellationToken ct)
    {
        if (initialized) return; await initialization.WaitAsync(ct);
        try
        {
            if (initialized) return; await using var connection = new SqliteConnection(connectionString); await connection.OpenAsync(ct);
            await Execute(connection, "PRAGMA journal_mode=WAL;", ct); await Execute(connection, "PRAGMA synchronous=FULL;", ct);
            await Execute(connection, "PRAGMA foreign_keys=ON;", ct); await Execute(connection, "PRAGMA busy_timeout=10000;", ct);
            await Execute(connection, Schema, ct); initialized = true;
        }
        finally { initialization.Release(); }
    }
    private static async Task Execute(SqliteConnection connection, string sql, CancellationToken ct) { await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(ct); }

    private sealed class Store(string connectionString) : ILocalSaleStore
    {
        public async Task<LocalSaleWriteResult> CompleteAsync(CompleteLocalSaleCommand command, CancellationToken ct)
        {
            var sale = new LocalSale(command.OrganizationId, command.BranchId, command.DeviceId, command.SaleId,
                command.ShiftId, command.RegisterId, command.CompletedAt, command.CashReceived, command.Lines);
            var payload = Payload(sale); var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
            await using var connection = await Open(connectionString, ct); await using var transaction = connection.BeginTransaction(deferred: false);
            await using (var replay = Command(connection, transaction, "SELECT outbox_messages.message_id,outbox_messages.sequence,outbox_messages.payload_digest,outbox_messages.status,outbox_messages.result_code,outbox_messages.created_at FROM local_sales JOIN outbox_messages USING(sale_id) WHERE local_sales.sale_id=$1", ("$1", sale.SaleId.ToString("D"))))
            await using (var reader = await replay.ExecuteReaderAsync(ct)) if (await reader.ReadAsync(ct))
            {
                if (reader.GetString(2) != digest) throw new InvalidOperationException("Changed local sale replay.");
                var existing = Message(reader.GetString(0), sale.SaleId, sale.DeviceId, reader.GetInt64(1), payload, digest,
                    reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5));
                await transaction.CommitAsync(ct); return new(sale, existing, false);
            }
            await using (var seed = Command(connection, transaction, "INSERT INTO device_state(device_id,last_sequence) VALUES($1,0) ON CONFLICT(device_id) DO NOTHING", ("$1", sale.DeviceId.ToString("D")))) await seed.ExecuteNonQueryAsync(ct);
            long sequence; await using (var next = Command(connection, transaction, "SELECT last_sequence+1 FROM device_state WHERE device_id=$1", ("$1", sale.DeviceId.ToString("D")))) sequence = (long)(await next.ExecuteScalarAsync(ct) ?? throw new InvalidOperationException("Device sequence unavailable."));
            var messageId = Guid.NewGuid(); var createdAt = DateTimeOffset.UtcNow;
            await using (var insert = Command(connection, transaction, "INSERT INTO local_sales(sale_id,organization_id,branch_id,device_id,shift_id,register_id,completed_at,currency,cash_received,net_total,tax_total,grand_total,change_due,payload_digest) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)",
                ("$1", sale.SaleId.ToString("D")), ("$2", sale.OrganizationId.ToString("D")), ("$3", sale.BranchId.ToString("D")), ("$4", sale.DeviceId.ToString("D")), ("$5", sale.ShiftId.ToString("D")), ("$6", sale.RegisterId.ToString("D")), ("$7", sale.CompletedAt.ToString("O")), ("$8", sale.Currency), ("$9", Number(sale.CashReceived)), ("$10", Number(sale.NetTotal)), ("$11", Number(sale.TaxTotal)), ("$12", Number(sale.GrandTotal)), ("$13", Number(sale.ChangeDue)), ("$14", digest))) await insert.ExecuteNonQueryAsync(ct);
            for (var index = 0; index < sale.Lines.Count; index++)
            {
                var line = sale.Lines[index]; await using var insert = Command(connection, transaction, "INSERT INTO local_sale_lines(sale_id,line_number,product_id,price_id,quantity,unit_amount,currency,tax_mode,tax_rate,net_amount,tax_amount,gross_amount) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)",
                    ("$1", sale.SaleId.ToString("D")), ("$2", index + 1), ("$3", line.ProductId.ToString("D")), ("$4", line.PriceId.ToString("D")), ("$5", Number(line.Quantity)), ("$6", Number(line.UnitAmount)), ("$7", line.Currency), ("$8", line.TaxMode), ("$9", Number(line.TaxRate)), ("$10", Number(line.NetAmount)), ("$11", Number(line.TaxAmount)), ("$12", Number(line.GrossAmount))); await insert.ExecuteNonQueryAsync(ct);
            }
            await using (var outbox = Command(connection, transaction, "INSERT INTO outbox_messages(message_id,sale_id,device_id,sequence,message_type,payload,payload_digest,status,created_at) VALUES($1,$2,$3,$4,'sale.completed.v1',$5,$6,'pending',$7)", ("$1", messageId.ToString("D")), ("$2", sale.SaleId.ToString("D")), ("$3", sale.DeviceId.ToString("D")), ("$4", sequence), ("$5", payload), ("$6", digest), ("$7", createdAt.ToString("O")))) await outbox.ExecuteNonQueryAsync(ct);
            await using (var advance = Command(connection, transaction, "UPDATE device_state SET last_sequence=$2 WHERE device_id=$1 AND last_sequence=$2-1", ("$1", sale.DeviceId.ToString("D")), ("$2", sequence))) if (await advance.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Concurrent local sequence conflict.");
            await transaction.CommitAsync(ct); return new(sale, new(messageId, sale.SaleId, sale.DeviceId, sequence, "sale.completed.v1", payload, digest, "pending", null, createdAt), true);
        }
        public async Task<IReadOnlyList<LocalOutboxMessage>> ReadPendingAsync(Guid deviceId, int limit, CancellationToken ct)
        {
            if (deviceId == Guid.Empty || limit is < 1 or > 100) throw new ArgumentException("Pending query is invalid.");
            await using var connection = await Open(connectionString, ct); await using var query = Command(connection, null, "SELECT message_id,sale_id,device_id,sequence,message_type,payload,payload_digest,status,result_code,created_at FROM outbox_messages WHERE device_id=$1 AND status='pending' ORDER BY sequence LIMIT $2", ("$1", deviceId.ToString("D")), ("$2", limit));
            var rows = new List<LocalOutboxMessage>(); await using var reader = await query.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) rows.Add(Read(reader)); return rows.AsReadOnly();
        }
        public async Task MarkResultAsync(Guid messageId, string payloadDigest, string status, string resultCode, DateTimeOffset acceptedAt, CancellationToken ct)
        {
            if (messageId == Guid.Empty || payloadDigest?.Length != 64 || payloadDigest.Any(c => !Uri.IsHexDigit(c)) || status is not ("applied" or "rejected")
                || resultCode is not ("applied" or "shift_conflict" or "price_conflict" or "insufficient_stock" or "sale_conflict") || acceptedAt == default || acceptedAt.Offset != TimeSpan.Zero) throw new ArgumentException("Sync result is invalid.");
            await using var connection = await Open(connectionString, ct); await using var transaction = connection.BeginTransaction(deferred: false);
            await using var update = Command(connection, transaction, "UPDATE outbox_messages SET status=$3,result_code=$4,accepted_at=$5 WHERE message_id=$1 AND payload_digest=$2 AND status='pending'", ("$1", messageId.ToString("D")), ("$2", payloadDigest), ("$3", status), ("$4", resultCode), ("$5", acceptedAt.ToString("O")));
            if (await update.ExecuteNonQueryAsync(ct) != 1)
            {
                await using var replay = Command(connection, transaction, "SELECT count(*) FROM outbox_messages WHERE message_id=$1 AND payload_digest=$2 AND status=$3 AND result_code=$4 AND accepted_at=$5", ("$1", messageId.ToString("D")), ("$2", payloadDigest), ("$3", status), ("$4", resultCode), ("$5", acceptedAt.ToString("O")));
                if ((long)(await replay.ExecuteScalarAsync(ct) ?? 0L) != 1) throw new InvalidOperationException("Changed or unknown sync result.");
            }
            await transaction.CommitAsync(ct);
        }
        private static async Task<SqliteConnection> Open(string value, CancellationToken ct) { var connection = new SqliteConnection(value); await connection.OpenAsync(ct); await Execute(connection, "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=10000;", ct); return connection; }
        private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object Value)[] values) { var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value); return command; }
        private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
        private static LocalOutboxMessage Read(SqliteDataReader r) => Message(r.GetString(0), Guid.Parse(r.GetString(1)), Guid.Parse(r.GetString(2)), r.GetInt64(3), r.GetString(5), r.GetString(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.GetString(9), r.GetString(4));
        private static LocalOutboxMessage Message(string message, Guid sale, Guid device, long sequence, string payload, string digest, string status, string? code, string created, string type = "sale.completed.v1") => new(Guid.Parse(message), sale, device, sequence, type, payload, digest, status, code, DateTimeOffset.Parse(created, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        private static string Payload(LocalSale sale) => JsonSerializer.Serialize(new { saleId = sale.SaleId, shiftId = sale.ShiftId, registerId = sale.RegisterId, completedAt = sale.CompletedAt, currency = sale.Currency, cashReceived = sale.CashReceived, netTotal = sale.NetTotal, taxTotal = sale.TaxTotal, grandTotal = sale.GrandTotal, changeDue = sale.ChangeDue, lines = sale.Lines.Select((line, index) => new { lineNumber = index + 1, line.ProductId, line.PriceId, line.Quantity, line.UnitAmount, taxMode = line.TaxMode, line.TaxRate, line.NetAmount, line.TaxAmount, line.GrossAmount }) });
    }
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS device_state(device_id TEXT PRIMARY KEY,last_sequence INTEGER NOT NULL CHECK(last_sequence>=0)) STRICT;
        CREATE TABLE IF NOT EXISTS local_sales(sale_id TEXT PRIMARY KEY,organization_id TEXT NOT NULL,branch_id TEXT NOT NULL,device_id TEXT NOT NULL,shift_id TEXT NOT NULL,register_id TEXT NOT NULL,completed_at TEXT NOT NULL,currency TEXT NOT NULL,cash_received TEXT NOT NULL,net_total TEXT NOT NULL,tax_total TEXT NOT NULL,grand_total TEXT NOT NULL,change_due TEXT NOT NULL,payload_digest TEXT NOT NULL UNIQUE) STRICT;
        CREATE TABLE IF NOT EXISTS local_sale_lines(sale_id TEXT NOT NULL,line_number INTEGER NOT NULL,product_id TEXT NOT NULL,price_id TEXT NOT NULL,quantity TEXT NOT NULL,unit_amount TEXT NOT NULL,currency TEXT NOT NULL,tax_mode TEXT NOT NULL,tax_rate TEXT NOT NULL,net_amount TEXT NOT NULL,tax_amount TEXT NOT NULL,gross_amount TEXT NOT NULL,PRIMARY KEY(sale_id,line_number),UNIQUE(sale_id,product_id),FOREIGN KEY(sale_id) REFERENCES local_sales(sale_id)) STRICT;
        CREATE TABLE IF NOT EXISTS outbox_messages(message_id TEXT PRIMARY KEY,sale_id TEXT NOT NULL UNIQUE,device_id TEXT NOT NULL,sequence INTEGER NOT NULL CHECK(sequence>0),message_type TEXT NOT NULL CHECK(message_type='sale.completed.v1'),payload TEXT NOT NULL,payload_digest TEXT NOT NULL,status TEXT NOT NULL CHECK(status IN('pending','applied','rejected')),result_code TEXT,created_at TEXT NOT NULL,accepted_at TEXT,UNIQUE(device_id,sequence),FOREIGN KEY(sale_id) REFERENCES local_sales(sale_id),CHECK((status='pending' AND result_code IS NULL AND accepted_at IS NULL) OR (status<>'pending' AND result_code IS NOT NULL AND accepted_at IS NOT NULL))) STRICT;
        CREATE TRIGGER IF NOT EXISTS immutable_local_sale_update BEFORE UPDATE ON local_sales BEGIN SELECT RAISE(ABORT,'local sale is immutable'); END;
        CREATE TRIGGER IF NOT EXISTS immutable_local_sale_delete BEFORE DELETE ON local_sales BEGIN SELECT RAISE(ABORT,'local sale is immutable'); END;
        CREATE TRIGGER IF NOT EXISTS immutable_local_line_update BEFORE UPDATE ON local_sale_lines BEGIN SELECT RAISE(ABORT,'local sale line is immutable'); END;
        CREATE TRIGGER IF NOT EXISTS immutable_local_line_delete BEFORE DELETE ON local_sale_lines BEGIN SELECT RAISE(ABORT,'local sale line is immutable'); END;
        """;
}
