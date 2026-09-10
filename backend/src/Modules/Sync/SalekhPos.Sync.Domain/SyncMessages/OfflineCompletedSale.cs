using System.Globalization;
using System.Text.Json;

namespace SalekhPos.Sync.Domain.SyncMessages;

public sealed record OfflineSaleLine(Guid ProductId, Guid PriceId, decimal Quantity, decimal UnitAmount,
    string TaxMode, decimal TaxRate, decimal NetAmount, decimal TaxAmount, decimal GrossAmount);

public sealed record OfflineCompletedSale(Guid SaleId, Guid ShiftId, Guid RegisterId, DateTimeOffset CompletedAt,
    string Currency, decimal CashReceived, decimal NetTotal, decimal TaxTotal, decimal GrandTotal,
    decimal ChangeDue, IReadOnlyList<OfflineSaleLine> Lines)
{
    private static readonly HashSet<string> RootNames = ["saleId", "shiftId", "registerId", "completedAt", "currency",
        "cashReceived", "netTotal", "taxTotal", "grandTotal", "changeDue", "lines"];
    private static readonly HashSet<string> LineNames = ["lineNumber", "productId", "priceId", "quantity", "unitAmount",
        "taxMode", "taxRate", "netAmount", "taxAmount", "grossAmount"];

    public static OfflineCompletedSale Parse(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            ExactObject(root, RootNames);
            var saleId = GuidValue(root, "saleId"); var shiftId = GuidValue(root, "shiftId");
            var registerId = GuidValue(root, "registerId"); var completedAt = UtcTime(root, "completedAt");
            var currency = Text(root, "currency");
            if (currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z')) Invalid();
            var cash = Amount(root, "cashReceived"); var net = Amount(root, "netTotal");
            var tax = Amount(root, "taxTotal"); var grand = Amount(root, "grandTotal");
            var change = Amount(root, "changeDue");
            var array = root.GetProperty("lines");
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > 500) Invalid();
            var lines = new List<OfflineSaleLine>(array.GetArrayLength()); var number = 0;
            foreach (var element in array.EnumerateArray())
            {
                ExactObject(element, LineNames); number++;
                if (!element.GetProperty("lineNumber").TryGetInt32(out var supplied) || supplied != number) Invalid();
                var productId = GuidValue(element, "productId"); var priceId = GuidValue(element, "priceId");
                var quantity = Positive(element, "quantity", 6); var unit = Positive(element, "unitAmount", 6);
                var mode = Text(element, "taxMode"); if (mode is not ("inclusive" or "exclusive")) Invalid();
                var rate = Amount(element, "taxRate", 4); if (rate > 100) Invalid();
                var lineNet = Amount(element, "netAmount"); var lineTax = Amount(element, "taxAmount");
                var lineGross = Amount(element, "grossAmount");
                var extended = Round(checked(quantity * unit));
                var expectedNet = mode == "exclusive" ? extended : rate == 0 ? extended : Round(extended / (1m + rate / 100m));
                var expectedTax = mode == "exclusive" ? Round(checked(extended * rate / 100m)) : checked(extended - expectedNet);
                var expectedGross = mode == "exclusive" ? checked(expectedNet + expectedTax) : extended;
                if (lineNet != expectedNet || lineTax != expectedTax || lineGross != expectedGross) Invalid();
                lines.Add(new(productId, priceId, quantity, unit, mode, rate, lineNet, lineTax, lineGross));
            }
            if (lines.Select(x => x.ProductId).Distinct().Count() != lines.Count
                || net != Sum(lines.Select(x => x.NetAmount)) || tax != Sum(lines.Select(x => x.TaxAmount))
                || grand != Sum(lines.Select(x => x.GrossAmount)) || cash < grand || change != Round(cash - grand)) Invalid();
            return new(saleId, shiftId, registerId, completedAt, currency, cash, net, tax, grand, change, lines.AsReadOnly());
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException
            or FormatException or OverflowException)
        { throw new ArgumentException("Offline sale payload is invalid.", exception); }
    }

    private static void ExactObject(JsonElement element, HashSet<string> names)
    {
        if (element.ValueKind != JsonValueKind.Object) Invalid();
        var seen = new HashSet<string>(); foreach (var property in element.EnumerateObject())
            if (!names.Contains(property.Name) || !seen.Add(property.Name)) Invalid();
        if (seen.Count != names.Count) Invalid();
    }
    private static Guid GuidValue(JsonElement element, string name) => element.GetProperty(name).ValueKind == JsonValueKind.String
        && Guid.TryParseExact(element.GetProperty(name).GetString(), "D", out var value) && value != Guid.Empty ? value : throw InvalidException();
    private static string Text(JsonElement element, string name) => element.GetProperty(name).ValueKind == JsonValueKind.String
        ? element.GetProperty(name).GetString()! : throw InvalidException();
    private static DateTimeOffset UtcTime(JsonElement element, string name) => element.GetProperty(name).ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParseExact(element.GetProperty(name).GetString(), "O", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var value) && value.Offset == TimeSpan.Zero ? value : throw InvalidException();
    private static decimal Positive(JsonElement element, string name, int scale) { var value = Amount(element, name, scale); if (value <= 0) Invalid(); return value; }
    private static decimal Amount(JsonElement element, string name, int scale = 6)
    {
        var property = element.GetProperty(name); if (property.ValueKind != JsonValueKind.Number || !property.TryGetDecimal(out var value))
            throw InvalidException();
        if (value < 0 || value > 99999999999999.999999m || decimal.Round(value, scale) != value) Invalid(); return value;
    }
    private static decimal Round(decimal value) => decimal.Round(value, 6, MidpointRounding.ToEven);
    private static decimal Sum(IEnumerable<decimal> values) { var total = 0m; foreach (var value in values) total = checked(total + value); return total; }
    private static void Invalid() => throw InvalidException();
    private static ArgumentException InvalidException() => new("Offline sale payload is invalid.");
}
