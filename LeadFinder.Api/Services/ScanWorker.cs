using LeadFinder.Api.Data;
using LeadFinder.Api.Data.Entities;
using LeadFinder.Api.Services.Discovery;
using LeadFinder.Api.Services.Queue;
using LeadFinder.Api.Services.SiteAudit;
using Microsoft.EntityFrameworkCore;

namespace LeadFinder.Api.Services;

public sealed class ScanWorker(
    IBackgroundQueue queue,
    ISiteAuditService audit,
    IDiscoveryService discoveryService,
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<ScanWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ScanWorker started.");

        await foreach (var req in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await HandleRequestAsync(req, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "ScanWorker error for {City}/{Keyword}", req.City, req.Keyword);
            }
        }

        logger.LogInformation("ScanWorker stopped.");
    }

    private async Task HandleRequestAsync(ScanRequest req, CancellationToken ct)
    {
        var urls = (req.Urls ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (urls.Count == 0)
        {
            var discovered = await discoveryService.DiscoverAsync(req.City, req.Keyword, ct);
            urls = discovered
                .Where(x => !string.IsNullOrWhiteSpace(x.WebsiteUrl))
                .Select(x => x.WebsiteUrl!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            logger.LogInformation("Discovered {Count} candidate sites for {City}/{Keyword}", urls.Count, req.City, req.Keyword);
        }
        
        if (urls.Count == 0)
        {
            logger.LogInformation("No URLs found or discovered for {City}/{Keyword}", req.City, req.Keyword);
            return;
        }

        foreach (var raw in urls)
        {
            ct.ThrowIfCancellationRequested();

            SiteAuditResult? auditResult = null;
            try
            {
                auditResult = await audit.AuditAsync(raw, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Audit failed for {Url}", raw);
                continue;
            }

            await UpsertAsync(req, auditResult, ct);
        }
    }

    private async Task UpsertAsync(ScanRequest req, SiteAuditResult ar, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var website = await db.Websites
            .Include(w => w.Business)
            .FirstOrDefaultAsync(w => w.Domain == ar.Host || w.HomepageUrl == ar.FinalUrl, ct);

        if (website is null)
        {
            var businessName = !string.IsNullOrWhiteSpace(ar.Title) ? ar.Title! : ar.Host;
            var business = await db.Businesses
                .FirstOrDefaultAsync(b => b.Name == businessName && b.City == req.City, ct);

            if (business is null)
            {
                business = new Business
                {
                    Name = businessName,
                    City = req.City,
                    Keyword = req.Keyword
                };
                db.Businesses.Add(business);
            }

            website = new Website
            {
                Domain = ar.Host,
                HomepageUrl = ar.FinalUrl,
                BusinessId = business.Id
            };
            db.Websites.Add(website);

            await db.SaveChangesAsync(ct);
        }
        else
        {
            if (website.HomepageUrl != ar.FinalUrl)
                website.HomepageUrl = ar.FinalUrl;
        }

        var leadScore = new LeadScore
        {
            BusinessId = website.BusinessId,
            WebsiteId = website.Id,
            Score = ar.Score,
            ReasonsJson = System.Text.Json.JsonSerializer.Serialize(ar.Notes),
            ComputedAt = DateTime.UtcNow
        };
        db.LeadScores.Add(leadScore);

        await db.SaveChangesAsync(ct);
    }
}
