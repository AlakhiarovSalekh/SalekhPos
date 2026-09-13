using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SalekhPos.ShiftManagement.Api.Shifts;

public sealed class ShiftRequestBodyDigestMiddleware(RequestDelegate next)
{
    public const string DigestItemKey = "SalekhPos.ShiftManagement.ShiftWriteRequestBodySha256";

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) && IsProtectedShiftWritePath(context.Request.Path.Value))
        {
            context.Request.EnableBuffering(bufferThreshold: 65536, bufferLimit: 65536);
            var digest = await SHA256.HashDataAsync(context.Request.Body, context.RequestAborted);
            context.Request.Body.Position = 0;
            context.Items[DigestItemKey] = Convert.ToHexString(digest);
        }

        await next(context);
    }

    private static bool IsProtectedShiftWritePath(string? path)
    {
        if (path is null) return false;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var hasShiftPrefix = parts.Length >= 8
            && string.Equals(parts[0], "api", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[1], "v1", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[2], "organizations", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(parts[3], "D", out _)
            && string.Equals(parts[4], "branches", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(parts[5], "D", out _)
            && string.Equals(parts[6], "shifts", StringComparison.OrdinalIgnoreCase);
        return hasShiftPrefix
            && ((parts.Length == 8 && string.Equals(parts[7], "open", StringComparison.OrdinalIgnoreCase))
                || (parts.Length == 9 && Guid.TryParseExact(parts[7], "D", out _)
                    && (string.Equals(parts[8], "cash-movements", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(parts[8], "close", StringComparison.OrdinalIgnoreCase))));
    }
}

public static class ShiftRequestBodyDigestMiddlewareExtensions
{
    public static IApplicationBuilder UseShiftRequestBodyDigest(this IApplicationBuilder app) =>
        app.UseMiddleware<ShiftRequestBodyDigestMiddleware>();
}
