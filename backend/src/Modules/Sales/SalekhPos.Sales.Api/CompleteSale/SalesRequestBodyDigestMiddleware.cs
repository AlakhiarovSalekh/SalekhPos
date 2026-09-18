using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SalekhPos.Sales.Api.CompleteSale;

public sealed class SalesRequestBodyDigestMiddleware(RequestDelegate next)
{
    public const string DigestItemKey = "SalekhPos.Sales.CashSaleWriteRequestBodySha256";

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) && IsProtectedCashSalePath(context.Request.Path.Value))
        {
            context.Request.EnableBuffering(bufferThreshold: 65536, bufferLimit: 65536);
            var digest = await SHA256.HashDataAsync(context.Request.Body, context.RequestAborted);
            context.Request.Body.Position = 0;
            context.Items[DigestItemKey] = Convert.ToHexString(digest);
        }

        await next(context);
    }

    private static bool IsProtectedCashSalePath(string? path)
    {
        if (path is null) return false;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 8
            && string.Equals(parts[0], "api", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[1], "v1", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[2], "organizations", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(parts[3], "D", out _)
            && string.Equals(parts[4], "branches", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(parts[5], "D", out _)
            && string.Equals(parts[6], "sales", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[7], "cash", StringComparison.OrdinalIgnoreCase);
    }
}

public static class SalesRequestBodyDigestMiddlewareExtensions
{
    public static IApplicationBuilder UseSalesRequestBodyDigest(this IApplicationBuilder app) =>
        app.UseMiddleware<SalesRequestBodyDigestMiddleware>();
}
