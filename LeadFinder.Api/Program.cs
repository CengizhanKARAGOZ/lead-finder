using LeadFinder.Api.Endpoints;
using LeadFinder.Api.Services;
using LeadFinder.Api.Data;
using LeadFinder.Api.Services.Queue;
using LeadFinder.Api.Services.SiteAudit;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddDbContextFactory<AppDbContext>();
builder.Services.AddSingleton<IBackgroundQueue, BackgroundQueue>();
builder.Services.AddSingleton<ISiteAuditService, SiteAuditService>();
builder.Services.AddHostedService<ScanWorker>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapScanEndpoints();

app.Run();