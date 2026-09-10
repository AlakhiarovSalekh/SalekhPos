using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SalekhPos.Desktop.Application.Offline;
using SalekhPos.Desktop.Domain.LocalCatalog;

namespace SalekhPos.Desktop.Infrastructure.LocalDatabase;

public sealed class SqliteSellableCatalog(string databasePath) : ILocalSellableCatalog
{
    private readonly string connectionString = Connection(databasePath);

    public async Task<bool> ApplyAsync(SellableCatalogSnapshot snapshot, CancellationToken cancellationToken)
    {
        Validate(snapshot);
        var ordered = snapshot.Items.OrderBy(x => x.ProductId).ToArray();
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ordered))));
        await using var connection = await Open(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await Schema(connection, transaction, cancellationToken);
        await using (var table = Command(connection, transaction,
            "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='local_stock_reservations'"))
            if ((long)(await table.ExecuteScalarAsync(cancellationToken) ?? 0L) == 1)
            {
                await using var pending = Command(connection, transaction, """
                SELECT count(*) FROM local_stock_reservations
                WHERE organization_id=$1 AND branch_id=$2 AND status='pending'
                """, ("$1", snapshot.OrganizationId), ("$2", snapshot.BranchId));
                if ((long)(await pending.ExecuteScalarAsync(cancellationToken) ?? 0L) != 0)
                    throw new InvalidOperationException("Pending sales must be reconciled before replacing stock.");
            }

        await using (var state = Command(connection, transaction,
            "SELECT captured_at,digest FROM sellable_catalog_state WHERE organization_id=$1 AND branch_id=$2",
            ("$1", snapshot.OrganizationId), ("$2", snapshot.BranchId)))
        await using (var reader = await state.ExecuteReaderAsync(cancellationToken))
            if (await reader.ReadAsync(cancellationToken))
            {
                var captured = DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                if (captured > snapshot.CapturedAt || captured == snapshot.CapturedAt && reader.GetString(1) != digest)
                    throw new InvalidOperationException("The catalog snapshot is stale or changed.");
                if (captured == snapshot.CapturedAt)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return false;
                }
            }

        await using (var clear = Command(connection, transaction,
            "DELETE FROM local_sellable_items WHERE organization_id=$1 AND branch_id=$2",
            ("$1", snapshot.OrganizationId), ("$2", snapshot.BranchId)))
            await clear.ExecuteNonQueryAsync(cancellationToken);

        foreach (var item in ordered)
        {
            await using var insert = Command(connection, transaction, """
                INSERT INTO local_sellable_items(organization_id,branch_id,product_id,price_id,sku,name,unit_code,
                barcode,stock_quantity,unit_amount,currency,tax_mode,tax_rate,price_valid_from,price_valid_until)
                VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
                """, ("$1", item.OrganizationId), ("$2", item.BranchId), ("$3", item.ProductId),
                ("$4", item.PriceId), ("$5", item.Sku), ("$6", item.Name), ("$7", item.UnitCode),
                ("$8", (object?)item.Barcode ?? DBNull.Value), ("$9", Number(item.StockQuantity)),
                ("$10", Number(item.UnitAmount)), ("$11", item.Currency), ("$12", item.TaxMode),
                ("$13", Number(item.TaxRate)), ("$14", item.PriceValidFrom.ToString("O")),
                ("$15", (object?)item.PriceValidUntil?.ToString("O") ?? DBNull.Value));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var save = Command(connection, transaction, """
            INSERT INTO sellable_catalog_state(organization_id,branch_id,captured_at,digest)
            VALUES($1,$2,$3,$4) ON CONFLICT(organization_id,branch_id) DO UPDATE
            SET captured_at=excluded.captured_at,digest=excluded.digest
            """, ("$1", snapshot.OrganizationId), ("$2", snapshot.BranchId),
            ("$3", snapshot.CapturedAt.ToString("O")), ("$4", digest)))
            await save.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task<LocalSellableItem?> FindByProductAsync(Guid organizationId, Guid branchId, Guid productId,
        DateTimeOffset at, CancellationToken cancellationToken) => Find(organizationId, branchId,
            "product_id=$3", productId.ToString("D"), at, cancellationToken);

    public Task<LocalSellableItem?> FindByBarcodeAsync(Guid organizationId, Guid branchId, string barcode,
        DateTimeOffset at, CancellationToken cancellationToken) => Find(organizationId, branchId,
            "barcode=$3", barcode, at, cancellationToken);

    private async Task<LocalSellableItem?> Find(Guid organizationId, Guid branchId, string predicate,
        string value, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || branchId == Guid.Empty || string.IsNullOrWhiteSpace(value)
            || at == default || at.Offset != TimeSpan.Zero) throw new ArgumentException("Catalog lookup is invalid.");
        await using var connection = await Open(cancellationToken); await Schema(connection, null, cancellationToken);
        await using var query = Command(connection, null, $"""
            SELECT organization_id,branch_id,product_id,price_id,sku,name,unit_code,barcode,stock_quantity,
            unit_amount,currency,tax_mode,tax_rate,price_valid_from,price_valid_until FROM local_sellable_items
            WHERE organization_id=$1 AND branch_id=$2 AND {predicate}
            """, ("$1", organizationId), ("$2", branchId), ("$3", value));
        await using var reader = await query.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var item = Read(reader);
        return item.PriceValidFrom <= at && (item.PriceValidUntil is null || item.PriceValidUntil > at) ? item : null;
    }

    private static void Validate(SellableCatalogSnapshot snapshot)
    {
        if (snapshot.OrganizationId == Guid.Empty || snapshot.BranchId == Guid.Empty || snapshot.CapturedAt == default
            || snapshot.CapturedAt.Offset != TimeSpan.Zero || snapshot.Items.Count > 100_000
            || snapshot.Items.Any(x => x.OrganizationId != snapshot.OrganizationId || x.BranchId != snapshot.BranchId)
            || snapshot.Items.Select(x => x.ProductId).Distinct().Count() != snapshot.Items.Count
            || snapshot.Items.Where(x => x.Barcode is not null).Select(x => x.Barcode).Distinct(StringComparer.Ordinal).Count()
                != snapshot.Items.Count(x => x.Barcode is not null))
            throw new ArgumentException("The catalog snapshot is invalid.");
    }

    private async Task<SqliteConnection> Open(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString); await connection.OpenAsync(cancellationToken);
        await using var pragma = connection.CreateCommand(); pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=10000;";
        await pragma.ExecuteNonQueryAsync(cancellationToken); return connection;
    }
    private static async Task Schema(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, """
            CREATE TABLE IF NOT EXISTS sellable_catalog_state(organization_id TEXT NOT NULL,branch_id TEXT NOT NULL,
            captured_at TEXT NOT NULL,digest TEXT NOT NULL,PRIMARY KEY(organization_id,branch_id)) STRICT;
            CREATE TABLE IF NOT EXISTS local_sellable_items(organization_id TEXT NOT NULL,branch_id TEXT NOT NULL,
            product_id TEXT NOT NULL,price_id TEXT NOT NULL,sku TEXT NOT NULL,name TEXT NOT NULL,unit_code TEXT NOT NULL,
            barcode TEXT,stock_quantity TEXT NOT NULL,unit_amount TEXT NOT NULL,currency TEXT NOT NULL,tax_mode TEXT NOT NULL,
            tax_rate TEXT NOT NULL,price_valid_from TEXT NOT NULL,price_valid_until TEXT,
            PRIMARY KEY(organization_id,branch_id,product_id),UNIQUE(organization_id,branch_id,barcode)) STRICT;
            """); await command.ExecuteNonQueryAsync(ct);
    }
    private static string Connection(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Database path is required.");
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return new SqliteConnectionStringBuilder { DataSource = full, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();
    }
    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object Value)[] values)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        foreach (var (name, value) in values)
            command.Parameters.AddWithValue(name, value is Guid id ? id.ToString("D") : value);
        return command;
    }
    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static LocalSellableItem Read(SqliteDataReader r) => new(Guid.Parse(r.GetString(0)), Guid.Parse(r.GetString(1)),
        Guid.Parse(r.GetString(2)), Guid.Parse(r.GetString(3)), r.GetString(4), r.GetString(5), r.GetString(6),
        r.IsDBNull(7) ? null : r.GetString(7), decimal.Parse(r.GetString(8), CultureInfo.InvariantCulture),
        decimal.Parse(r.GetString(9), CultureInfo.InvariantCulture), r.GetString(10), r.GetString(11),
        decimal.Parse(r.GetString(12), CultureInfo.InvariantCulture), DateTimeOffset.Parse(r.GetString(13),
        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), r.IsDBNull(14) ? null : DateTimeOffset.Parse(
        r.GetString(14), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
}
