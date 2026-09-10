using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Domain.LocalSales;

namespace SalekhPos.Desktop.Infrastructure.LocalDatabase;

public sealed class SqliteProjectedSaleCheckout(string databasePath) : IProjectedSaleCheckout
{
    private readonly string connectionString = BuildConnection(databasePath);

    public async Task<LocalSaleWriteResult> CompleteAsync(CompleteProjectedSaleCommand command,
        CancellationToken cancellationToken)
    {
        Validate(command);
        await using var connection = await Open(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await EnsureSchema(connection, transaction, cancellationToken);

        var replay = await ReadReplay(connection, transaction, command.SaleId, cancellationToken);
        if (replay is not null)
        {
            var replaySale = await BuildSale(connection, transaction, command, reserve: false, cancellationToken);
            var replayPayload = Payload(replaySale);
            var replayDigest = Digest(replayPayload);
            if (replay.Value.Digest != replayDigest) throw new InvalidOperationException("Changed projected sale replay.");
            await transaction.CommitAsync(cancellationToken);
            return new(replaySale, new(replay.Value.MessageId, replaySale.SaleId, replaySale.DeviceId,
                replay.Value.Sequence, "sale.completed.v1", replayPayload, replayDigest, replay.Value.Status,
                replay.Value.ResultCode, replay.Value.CreatedAt), false);
        }

        var sale = await BuildSale(connection, transaction, command, reserve: true, cancellationToken);
        var payload = Payload(sale); var digest = Digest(payload); var messageId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        await Execute(connection, transaction, "INSERT INTO device_state(device_id,last_sequence) VALUES($1,0) ON CONFLICT(device_id) DO NOTHING", cancellationToken, ("$1", sale.DeviceId));
        var sequence = Convert.ToInt64(await Scalar(connection, transaction,
            "SELECT last_sequence+1 FROM device_state WHERE device_id=$1", cancellationToken, ("$1", sale.DeviceId)), CultureInfo.InvariantCulture);
        await Execute(connection, transaction, """
            INSERT INTO local_sales(sale_id,organization_id,branch_id,device_id,shift_id,register_id,completed_at,currency,
            cash_received,net_total,tax_total,grand_total,change_due,payload_digest)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)
            """, cancellationToken, ("$1", sale.SaleId), ("$2", sale.OrganizationId), ("$3", sale.BranchId),
            ("$4", sale.DeviceId), ("$5", sale.ShiftId), ("$6", sale.RegisterId), ("$7", sale.CompletedAt.ToString("O")),
            ("$8", sale.Currency), ("$9", Number(sale.CashReceived)), ("$10", Number(sale.NetTotal)),
            ("$11", Number(sale.TaxTotal)), ("$12", Number(sale.GrandTotal)), ("$13", Number(sale.ChangeDue)), ("$14", digest));
        for (var index = 0; index < sale.Lines.Count; index++)
        {
            var line = sale.Lines[index];
            await Execute(connection, transaction, """
                INSERT INTO local_sale_lines(sale_id,line_number,product_id,price_id,quantity,unit_amount,currency,
                tax_mode,tax_rate,net_amount,tax_amount,gross_amount) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
                """, cancellationToken, ("$1", sale.SaleId), ("$2", index + 1), ("$3", line.ProductId),
                ("$4", line.PriceId), ("$5", Number(line.Quantity)), ("$6", Number(line.UnitAmount)),
                ("$7", line.Currency), ("$8", line.TaxMode), ("$9", Number(line.TaxRate)),
                ("$10", Number(line.NetAmount)), ("$11", Number(line.TaxAmount)), ("$12", Number(line.GrossAmount)));
        }
        await Execute(connection, transaction, """
            INSERT INTO outbox_messages(message_id,sale_id,device_id,sequence,message_type,payload,payload_digest,status,created_at)
            VALUES($1,$2,$3,$4,'sale.completed.v1',$5,$6,'pending',$7)
            """, cancellationToken, ("$1", messageId), ("$2", sale.SaleId), ("$3", sale.DeviceId),
            ("$4", sequence), ("$5", payload), ("$6", digest), ("$7", createdAt.ToString("O")));
        if (await Execute(connection, transaction,
            "UPDATE device_state SET last_sequence=$2 WHERE device_id=$1 AND last_sequence=$2-1", cancellationToken,
            ("$1", sale.DeviceId), ("$2", sequence)) != 1) throw new InvalidOperationException("Concurrent local sequence conflict.");
        await transaction.CommitAsync(cancellationToken);
        return new(sale, new(messageId, sale.SaleId, sale.DeviceId, sequence, "sale.completed.v1", payload,
            digest, "pending", null, createdAt), true);
    }

    private static async Task<LocalSale> BuildSale(SqliteConnection connection, SqliteTransaction transaction,
        CompleteProjectedSaleCommand command, bool reserve, CancellationToken ct)
    {
        var lines = new List<LocalSaleLine>();
        foreach (var requested in command.Items)
        {
            await using var query = Command(connection, transaction, """
                SELECT price_id,stock_quantity,unit_amount,currency,tax_mode,tax_rate,price_valid_from,price_valid_until
                FROM local_sellable_items WHERE organization_id=$1 AND branch_id=$2 AND product_id=$3
                """, ("$1", command.OrganizationId), ("$2", command.BranchId), ("$3", requested.ProductId));
            await using var reader = await query.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new InvalidOperationException("Projected product is unavailable.");
            var stock = decimal.Parse(reader.GetString(1), CultureInfo.InvariantCulture);
            var validFrom = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            DateTimeOffset? validUntil = reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (validFrom > command.CompletedAt || validUntil is not null && validUntil <= command.CompletedAt)
                throw new InvalidOperationException("Projected price is not effective.");
            if (reserve && stock < requested.Quantity) throw new InvalidOperationException("Projected stock is insufficient.");
            var line = new LocalSaleLine(requested.ProductId, Guid.Parse(reader.GetString(0)), requested.Quantity,
                decimal.Parse(reader.GetString(2), CultureInfo.InvariantCulture), reader.GetString(3), reader.GetString(4),
                decimal.Parse(reader.GetString(5), CultureInfo.InvariantCulture));
            await reader.DisposeAsync(); lines.Add(line);
            if (!reserve) continue;
            var remaining = stock - requested.Quantity;
            if (await Execute(connection, transaction, """
                UPDATE local_sellable_items SET stock_quantity=$4 WHERE organization_id=$1 AND branch_id=$2
                AND product_id=$3 AND stock_quantity=$5
                """, ct, ("$1", command.OrganizationId), ("$2", command.BranchId), ("$3", requested.ProductId),
                ("$4", Number(remaining)), ("$5", Number(stock))) != 1) throw new InvalidOperationException("Projected stock changed concurrently.");
            await Execute(connection, transaction, """
                INSERT INTO local_stock_reservations(sale_id,organization_id,branch_id,product_id,quantity,status)
                VALUES($1,$2,$3,$4,$5,'pending')
                """, ct, ("$1", command.SaleId), ("$2", command.OrganizationId), ("$3", command.BranchId),
                ("$4", requested.ProductId), ("$5", Number(requested.Quantity)));
        }
        return new(command.OrganizationId, command.BranchId, command.DeviceId, command.SaleId, command.ShiftId,
            command.RegisterId, command.CompletedAt, command.CashReceived, lines);
    }

    private static void Validate(CompleteProjectedSaleCommand command)
    {
        if (command.Items is null || command.Items.Count is < 1 or > 500
            || command.Items.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0 || decimal.Round(x.Quantity, 6) != x.Quantity)
            || command.Items.Select(x => x.ProductId).Distinct().Count() != command.Items.Count)
            throw new ArgumentException("Projected sale items are invalid.");
    }
    private static async Task<(Guid MessageId, long Sequence, string Digest, string Status, string? ResultCode, DateTimeOffset CreatedAt)?> ReadReplay(SqliteConnection c, SqliteTransaction t, Guid saleId, CancellationToken ct)
    {
        await using var q = Command(c, t, "SELECT message_id,sequence,payload_digest,status,result_code,created_at FROM outbox_messages WHERE sale_id=$1", ("$1", saleId));
        await using var r = await q.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) return null;
        return (Guid.Parse(r.GetString(0)), r.GetInt64(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), DateTimeOffset.Parse(r.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
    }
    private async Task<SqliteConnection> Open(CancellationToken ct) { var c = new SqliteConnection(connectionString); await c.OpenAsync(ct); await Execute(c, null, "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=10000;", ct); return c; }
    private static async Task EnsureSchema(SqliteConnection c, SqliteTransaction t, CancellationToken ct) => await Execute(c, t, Schema, ct);
    private static async Task<int> Execute(SqliteConnection c, SqliteTransaction? t, string sql, CancellationToken ct, params (string, object)[] values) { await using var q = Command(c, t, sql, values); return await q.ExecuteNonQueryAsync(ct); }
    private static async Task<object?> Scalar(SqliteConnection c, SqliteTransaction t, string sql, CancellationToken ct, params (string, object)[] values) { await using var q = Command(c, t, sql, values); return await q.ExecuteScalarAsync(ct); }
    private static SqliteCommand Command(SqliteConnection c, SqliteTransaction? t, string sql, params (string, object)[] values) { var q = c.CreateCommand(); q.Transaction = t; q.CommandText = sql; foreach (var (name, value) in values) q.Parameters.AddWithValue(name, value is Guid id ? id.ToString("D") : value); return q; }
    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Digest(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    private static string Payload(LocalSale sale) => JsonSerializer.Serialize(new { saleId = sale.SaleId, shiftId = sale.ShiftId, registerId = sale.RegisterId, completedAt = sale.CompletedAt, currency = sale.Currency, cashReceived = sale.CashReceived, netTotal = sale.NetTotal, taxTotal = sale.TaxTotal, grandTotal = sale.GrandTotal, changeDue = sale.ChangeDue, lines = sale.Lines.Select((line, index) => new { lineNumber = index + 1, line.ProductId, line.PriceId, line.Quantity, line.UnitAmount, taxMode = line.TaxMode, line.TaxRate, line.NetAmount, line.TaxAmount, line.GrossAmount }) });
    private static string BuildConnection(string path) { if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Database path is required."); var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); return new SqliteConnectionStringBuilder { DataSource = full, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString(); }
    private const string Schema = """
        CREATE TABLE IF NOT EXISTS device_state(device_id TEXT PRIMARY KEY,last_sequence INTEGER NOT NULL CHECK(last_sequence>=0)) STRICT;
        CREATE TABLE IF NOT EXISTS local_sales(sale_id TEXT PRIMARY KEY,organization_id TEXT NOT NULL,branch_id TEXT NOT NULL,device_id TEXT NOT NULL,shift_id TEXT NOT NULL,register_id TEXT NOT NULL,completed_at TEXT NOT NULL,currency TEXT NOT NULL,cash_received TEXT NOT NULL,net_total TEXT NOT NULL,tax_total TEXT NOT NULL,grand_total TEXT NOT NULL,change_due TEXT NOT NULL,payload_digest TEXT NOT NULL UNIQUE) STRICT;
        CREATE TABLE IF NOT EXISTS local_sale_lines(sale_id TEXT NOT NULL,line_number INTEGER NOT NULL,product_id TEXT NOT NULL,price_id TEXT NOT NULL,quantity TEXT NOT NULL,unit_amount TEXT NOT NULL,currency TEXT NOT NULL,tax_mode TEXT NOT NULL,tax_rate TEXT NOT NULL,net_amount TEXT NOT NULL,tax_amount TEXT NOT NULL,gross_amount TEXT NOT NULL,PRIMARY KEY(sale_id,line_number),UNIQUE(sale_id,product_id),FOREIGN KEY(sale_id) REFERENCES local_sales(sale_id)) STRICT;
        CREATE TABLE IF NOT EXISTS outbox_messages(message_id TEXT PRIMARY KEY,sale_id TEXT NOT NULL UNIQUE,device_id TEXT NOT NULL,sequence INTEGER NOT NULL CHECK(sequence>0),message_type TEXT NOT NULL,payload TEXT NOT NULL,payload_digest TEXT NOT NULL,status TEXT NOT NULL,result_code TEXT,created_at TEXT NOT NULL,accepted_at TEXT,UNIQUE(device_id,sequence),FOREIGN KEY(sale_id) REFERENCES local_sales(sale_id)) STRICT;
        CREATE TABLE IF NOT EXISTS local_stock_reservations(sale_id TEXT NOT NULL,organization_id TEXT NOT NULL,branch_id TEXT NOT NULL,product_id TEXT NOT NULL,quantity TEXT NOT NULL,status TEXT NOT NULL CHECK(status IN('pending','committed','released')),PRIMARY KEY(sale_id,product_id)) STRICT;
        """;
}
