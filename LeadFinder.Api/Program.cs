using LeadFinder.Api.Data;
using LeadFinder.Api.Services;
using LeadFinder.Api.Services.Queue;
using LeadFinder.Api.Services.SiteAudit;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IBackgroundQueue, BackgroundQueue>();
builder.Services.AddSingleton<ISiteAuditService, SiteAuditService>();
builder.Services.AddDbContextFactory<AppDbContext>();

builder.Services.AddHostedService<ScanWorker>();
app.MapGet("/", () => "Hello World!");

app.Run();
