using SalekhPos.Worker.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddWorkerRuntime(builder.Configuration);

await builder.Build().RunAsync().ConfigureAwait(false);
