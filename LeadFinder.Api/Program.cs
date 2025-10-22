using LeadFinder.Api.Endpoints;
using LeadFinder.Api.Services;
using LeadFinder.Api.Data;
using LeadFinder.Api.Services.Discovery;
using LeadFinder.Api.Services.Discovery.Providers;
using LeadFinder.Api.Services.Discovery.Providers.Osmp;
using LeadFinder.Api.Services.Queue;
using LeadFinder.Api.Services.SiteAudit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(conn))
    throw new InvalidOperationException("ConnectionStrings:Default missing.");

builder.Services.AddDbContextFactory<AppDbContext>(opt =>
{
    opt.UseMySql(conn, ServerVersion.AutoDetect(conn));
});

builder.Services.AddHttpClient();

builder.Services.AddSingleton<IBackgroundQueue, BackgroundQueue>();
builder.Services.AddSingleton<ISiteAuditService, SiteAuditService>();
builder.Services.AddSingleton<IDiscoveryService, DiscoveryService>();
builder.Services.AddSingleton<IWebSearchProvider, DuckDuckGoWebSearchProvider>();
builder.Services.AddSingleton<IPlacesProvider, OsmpPlacesProvider>();
builder.Services.AddHostedService<ScanWorker>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapScanEndpoints();
app.MapResultEndpoints();
app.MapCleanupEndpoints();

app.Run();