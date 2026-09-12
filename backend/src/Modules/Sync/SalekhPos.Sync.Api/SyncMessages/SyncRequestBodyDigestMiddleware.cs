using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SalekhPos.Sync.Api.SyncMessages;

public sealed class SyncRequestBodyDigestMiddleware(RequestDelegate next)
{
    public const string DigestItemKey = "SalekhPos.Sync.RequestBodySha256";

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (path is not null && path.StartsWith("/api/v1/organizations/", StringComparison.OrdinalIgnoreCase)
            && path.Contains("/sync", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.EnableBuffering(bufferThreshold: 65536, bufferLimit: 65536);
            var digest = await SHA256.HashDataAsync(context.Request.Body, context.RequestAborted);
            context.Request.Body.Position = 0;
            context.Items[DigestItemKey] = Convert.ToHexString(digest);
        }

        await next(context);
    }
}

public static class SyncRequestBodyDigestMiddlewareExtensions
{
    public static IApplicationBuilder UseSyncRequestBodyDigest(this IApplicationBuilder app) =>
        app.UseMiddleware<SyncRequestBodyDigestMiddleware>();
}
