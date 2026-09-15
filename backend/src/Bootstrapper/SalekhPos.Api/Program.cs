using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using SalekhPos.Authorization.Infrastructure;
using SalekhPos.Authorization.Application;
using SalekhPos.Authorization.Api.Organizations;
using SalekhPos.Catalog.Api.Products;
using SalekhPos.Catalog.Application.Products;
using SalekhPos.Catalog.Infrastructure.Products;
using SalekhPos.Inventory.Api.Stock;
using SalekhPos.Inventory.Application.Stock;
using SalekhPos.Inventory.Infrastructure.Stock;
using SalekhPos.Pricing.Api.Prices;
using SalekhPos.Pricing.Application.Prices;
using SalekhPos.Pricing.Infrastructure.Prices;
using SalekhPos.Payments.Api.Payments;
using SalekhPos.Payments.Application.Payments;
using SalekhPos.Payments.Infrastructure.Payments;
using SalekhPos.Returns.Api.Returns;
using SalekhPos.Returns.Application.Returns;
using SalekhPos.Returns.Infrastructure.Returns;
using SalekhPos.Sales.Api.Carts;
using SalekhPos.Sales.Api.CompleteSale;
using SalekhPos.Sales.Api.Receipts;
using SalekhPos.Sales.Api.Voids;
using SalekhPos.Sales.Application.Carts;
using SalekhPos.Sales.Application.CompleteSale;
using SalekhPos.Sales.Application.Voids;
using SalekhPos.Sales.Infrastructure.Carts;
using SalekhPos.Sales.Infrastructure.CompleteSale;
using SalekhPos.Sales.Infrastructure.Voids;
using SalekhPos.Api.Authentication;
using SalekhPos.Api.Endpoints;
using SalekhPos.Api.ExceptionHandling;
using SalekhPos.Api.Security;
using SalekhPos.Identity.Api.Endpoints;
using SalekhPos.Identity.Infrastructure.Tokens;
using SalekhPos.SystemAdministration.Api;
using SalekhPos.SystemAdministration.Application;
using SalekhPos.SystemAdministration.Infrastructure;
using SalekhPos.Stores.Api.Registers;
using SalekhPos.Stores.Application.Registers;
using SalekhPos.Stores.Infrastructure.Registers;
using SalekhPos.ShiftManagement.Api.Shifts;
using SalekhPos.ShiftManagement.Application.Shifts;
using SalekhPos.ShiftManagement.Infrastructure.Shifts;
using SalekhPos.Devices.Api.Devices;
using SalekhPos.Devices.Application.Devices;
using SalekhPos.Devices.Infrastructure.Devices;
using SalekhPos.Sync.Api.SyncMessages;
using SalekhPos.Sync.Application.SyncMessages;
using SalekhPos.Sync.Infrastructure.SyncMessages;
using SalekhPos.Customers.Api.Customers;
using SalekhPos.Customers.Application.Customers;
using SalekhPos.Customers.Infrastructure.Customers;
using SalekhPos.Suppliers.Api.Suppliers;
using SalekhPos.Suppliers.Application.Suppliers;
using SalekhPos.Suppliers.Infrastructure.Suppliers;
using SalekhPos.Purchasing.Api.PurchaseOrders;
using SalekhPos.Purchasing.Application.PurchaseOrders;
using SalekhPos.Purchasing.Infrastructure.PurchaseOrders;
using SalekhPos.Employees.Api.Employees;
using SalekhPos.Employees.Application.Employees;
using SalekhPos.Employees.Infrastructure.Employees;
using SalekhPos.Reporting.Api.Reports;
using SalekhPos.Reporting.Application.Reports;
using SalekhPos.Reporting.Infrastructure.Reports;
using SalekhPos.Warehousing.Api.Transfers;
using SalekhPos.Warehousing.Application.Transfers;
using SalekhPos.Warehousing.Infrastructure.Transfers;
using SalekhPos.Promotions.Api.Campaigns;
using SalekhPos.Promotions.Application.Campaigns;
using SalekhPos.Promotions.Infrastructure.Campaigns;
using SalekhPos.Loyalty.Api.Accounts;
using SalekhPos.Loyalty.Application.Accounts;
using SalekhPos.Loyalty.Infrastructure.Accounts;
using SalekhPos.Audit.Api.AuditTrail;
using SalekhPos.Audit.Application.AuditTrail;
using SalekhPos.Audit.Infrastructure.AuditTrail;

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
builder.Services.AddWebAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(new AccessDatabase(builder.Configuration.GetConnectionString("Application"),
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")));
builder.Services.AddSingleton<BranchAccessReader>();
builder.Services.AddSingleton<IAccessibleOrganizationReader, OrganizationAccessReader>();
builder.Services.AddSingleton<IProductCatalog>(provider => new PostgresProductCatalog(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IInventoryLedger>(provider => new PostgresInventoryLedger(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IPriceBook>(provider => new PostgresPriceBook(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton(provider => new PostgresCashSaleCompletion(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<ISuspendedCartService>(provider => new PostgresSuspendedCartService(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton(provider => new PostgresSaleVoidService(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<ISaleVoidService>(provider => provider.GetRequiredService<PostgresSaleVoidService>());
builder.Services.AddSingleton<ISaleVoidReader>(provider => provider.GetRequiredService<PostgresSaleVoidService>());
builder.Services.AddSingleton<ICashSaleCompletion>(provider => provider.GetRequiredService<PostgresCashSaleCompletion>());
builder.Services.AddSingleton<ISaleReader>(provider => provider.GetRequiredService<PostgresCashSaleCompletion>());
builder.Services.AddSingleton<IPaymentReader>(provider => new PostgresPaymentReader(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IReturnCompletion>(provider => new PostgresReturnCompletion(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IReturnReader>(provider => new PostgresReturnReader(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IRegisterCatalog>(provider => new PostgresRegisterCatalog(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IShiftService>(provider => new PostgresShiftService(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IDeviceRegistry>(provider => new PostgresDeviceRegistry(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IDeviceRequestProofVerifier>(provider => new PostgresDeviceRequestProofVerifier(
    provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<ISyncDeviceRequestAuthorizer, SyncDeviceRequestAuthorizer>();
builder.Services.AddSingleton<IShiftDeviceRequestAuthorizer, ShiftDeviceRequestAuthorizer>();
builder.Services.AddSingleton<ISyncIngestion>(provider => new PostgresSyncIngestion(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<ICustomerDirectory>(provider => new PostgresCustomerDirectory(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<ISupplierDirectory>(provider => new PostgresSupplierDirectory(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IPurchaseOrderService>(provider => new PostgresPurchaseOrderService(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IEmployeeDirectory>(provider => new PostgresEmployeeDirectory(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IOperationalReportService>(provider => new PostgresOperationalReportService(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IStockTransferService>(provider => new PostgresStockTransferService(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IPromotionService>(provider => new PostgresPromotionService(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<ILoyaltyAccountService>(provider => new PostgresLoyaltyAccountService(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<IAuditTrail>(provider => new PostgresAuditTrail(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton(provider => new TokenRevocations(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<SalekhPos.Identity.Application.Sessions.ITokenRevocations>(provider => provider.GetRequiredService<TokenRevocations>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new PlatformMfaPolicy(builder.Configuration["Authentication:PrivilegedAcr"], TimeProvider.System));
builder.Services.AddSingleton<ISuperAdminRegistry>(provider => new PostgresSuperAdminRegistry(provider.GetRequiredService<AccessDatabase>().DataSource));
builder.Services.AddSingleton<SuperAdminAdministration>();
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
app.UseAuditCapture();
app.UseSyncRequestBodyDigest();
app.UseShiftRequestBodyDigest();

app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.MapGet("/health/ready", async (AuthenticationState authentication, BranchAccessReader reader,
    TokenRevocations revocations, CancellationToken cancellationToken) =>
    authentication.IsConfigured && await reader.IsReadyAsync(cancellationToken) && await revocations.IsReadyAsync(cancellationToken)
        ? Results.Ok(new { status = "ready", capability = "authorized_branch_reads" })
        : Results.Problem(statusCode: 503, title: "Service is not ready",
            extensions: new Dictionary<string, object?> { ["code"] = "dependencies_unavailable" })).AllowAnonymous();
app.MapBranchEndpoints();
app.MapOrganizationAccessEndpoints();
app.MapProductEndpoints();
app.MapInventoryEndpoints();
app.MapPricingEndpoints();
app.MapCashSaleEndpoints();
app.MapSuspendedCartEndpoints();
app.MapSaleVoidEndpoints();
app.MapRegisterEndpoints();
app.MapShiftEndpoints();
app.MapDeviceEndpoints();
app.MapSyncEndpoints();
app.MapReceiptEndpoints();
app.MapPaymentEndpoints();
app.MapReturnEndpoints();
app.MapCustomerEndpoints();
app.MapSupplierEndpoints();
app.MapPurchaseOrderEndpoints();
app.MapEmployeeEndpoints();
app.MapOperationalReportEndpoints();
app.MapStockTransferEndpoints();
app.MapPromotionEndpoints();
app.MapLoyaltyEndpoints();
app.MapAuditEndpoints();
app.MapIdentityEndpoints();
app.MapWebAuthentication();
app.MapWebBusinessEndpoints();
app.MapWebOperationsEndpoints();
app.MapWebManagementEndpoints();
app.MapWebCommerceExtensionsEndpoints();
app.MapWebAuditEndpoints();
app.MapPlatformEndpoints();

app.Run();
