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

        List<DiscoveryCandidate> discovered = new();
        if (urls.Count == 0)
        {
            discovered = (await discoveryService.DiscoverAsync(req.City, req.Keyword, ct)).ToList();
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

        // Create a lookup for discovery candidates by URL
        var candidateByUrl = discovered
            .Where(c => !string.IsNullOrWhiteSpace(c.WebsiteUrl))
            .ToDictionary(c => c.WebsiteUrl!, StringComparer.OrdinalIgnoreCase);

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

            // Get discovery info if available
            candidateByUrl.TryGetValue(raw, out var candidate);

            await UpsertAsync(req, auditResult, candidate, ct);
        }
    }

    private async Task UpsertAsync(ScanRequest req, SiteAuditResult ar, DiscoveryCandidate? candidate, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var website = await db.Websites
            .Include(w => w.Business)
            .FirstOrDefaultAsync(w => w.Domain == ar.Host || w.HomepageUrl == ar.FinalUrl, ct);

        if (website is null)
        {
            // Use candidate name if available, otherwise use title or host
            var businessName = candidate?.Name ?? (!string.IsNullOrWhiteSpace(ar.Title) ? ar.Title! : ar.Host);
            var business = await db.Businesses
                .FirstOrDefaultAsync(b => b.Name == businessName && b.City == req.City, ct);

            if (business is null)
            {
                business = new Business
                {
                    Name = businessName,
                    City = req.City,
                    Keyword = req.Keyword,
                    Address = candidate?.Address,
                    Phone = candidate?.Phones.FirstOrDefault(),
                    Email = ar.Emails.FirstOrDefault()
                };
                db.Businesses.Add(business);
                // Save to get the Business.Id
                await db.SaveChangesAsync(ct);
            }
            else
            {
                // Update business contact info if not set
                if (string.IsNullOrWhiteSpace(business.Address) && !string.IsNullOrWhiteSpace(candidate?.Address))
                    business.Address = candidate.Address;
                if (string.IsNullOrWhiteSpace(business.Phone) && candidate?.Phones.Count > 0)
                    business.Phone = candidate.Phones.First();
                if (string.IsNullOrWhiteSpace(business.Email) && ar.Emails.Count > 0)
                    business.Email = ar.Emails.First();
            }

            website = new Website
            {
                Domain = ar.Host,
                HomepageUrl = ar.FinalUrl,
                Business = business,  // Use navigation property instead of BusinessId
                EmailsCsv = ar.Emails.Count > 0 ? string.Join(",", ar.Emails) : null,
                PhonesCsv = ar.Phones.Count > 0 ? string.Join(",", ar.Phones) : null
            };
            db.Websites.Add(website);

            await db.SaveChangesAsync(ct);
        }
        else
        {
            // Update existing website
            if (website.HomepageUrl != ar.FinalUrl)
                website.HomepageUrl = ar.FinalUrl;

            // Update contact info from audit
            if (ar.Emails.Count > 0)
                website.EmailsCsv = string.Join(",", ar.Emails);
            if (ar.Phones.Count > 0)
                website.PhonesCsv = string.Join(",", ar.Phones);

            // Update business contact info if available
            if (website.Business is not null)
            {
                if (string.IsNullOrWhiteSpace(website.Business.Address) && !string.IsNullOrWhiteSpace(candidate?.Address))
                    website.Business.Address = candidate.Address;
                if (string.IsNullOrWhiteSpace(website.Business.Phone) && candidate?.Phones.Count > 0)
                    website.Business.Phone = candidate.Phones.First();
                if (string.IsNullOrWhiteSpace(website.Business.Email) && ar.Emails.Count > 0)
                    website.Business.Email = ar.Emails.First();
            }
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
