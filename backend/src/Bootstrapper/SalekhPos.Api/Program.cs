using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using SalekhPos.Access.Infrastructure;
using SalekhPos.Api.Authentication;
using SalekhPos.Api.Endpoints;
using SalekhPos.Api.Errors;
using SalekhPos.Identity.Api;
using SalekhPos.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 65536;
    options.Limits.MaxRequestHeadersTotalSize = 32768;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});
builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Extensions.TryAdd("code", "http_" + context.ProblemDetails.Status);
});
builder.Services.AddExceptionHandler<SafeExceptionHandler>();
builder.Services.Configure<ExceptionHandlerOptions>(options => options.SuppressDiagnosticsCallback = _ => true);
builder.Services.AddPlatformAuthentication(builder.Configuration);
builder.Services.AddSingleton(new AccessDatabase(builder.Configuration.GetConnectionString("Application"),
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")));
builder.Services.AddSingleton<BranchAccessReader>();
builder.Services.AddSingleton(provider => new TokenRevocations(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddConcurrencyLimiter("business", limiter =>
    {
        limiter.PermitLimit = 64;
        limiter.QueueLimit = 0;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.Request.Path.StartsWithSegments("/health")
            ? RateLimitPartition.GetNoLimiter("health")
            : RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await Results.Problem(statusCode: 429, title: "Request capacity exceeded",
            extensions: new Dictionary<string, object?> { ["code"] = "rate_limited" }).ExecuteAsync(context.HttpContext);
    };
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    context.TraceIdentifier = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Trace-Id"] = context.TraceIdentifier;
    await next(context);
});
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
// No forwarded headers are trusted by default. Deployment must explicitly
// configure trusted proxies and a distributed edge limiter for multiple nodes.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.MapGet("/health/ready", async (AuthenticationState authentication, BranchAccessReader reader,
    TokenRevocations revocations, CancellationToken cancellationToken) =>
    authentication.IsConfigured && await reader.IsReadyAsync(cancellationToken) && await revocations.IsReadyAsync(cancellationToken)
        ? Results.Ok(new { status = "ready", capability = "authorized_branch_reads" })
        : Results.Problem(statusCode: 503, title: "Service is not ready",
            extensions: new Dictionary<string, object?> { ["code"] = "dependencies_unavailable" })).AllowAnonymous();
app.MapBranchEndpoints();
app.MapIdentityEndpoints();

app.Run();
