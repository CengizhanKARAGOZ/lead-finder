using LeadFinder.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeadFinder.Api.Endpoints;

public static class CleanupEndpoints
{
    public static void MapCleanupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/cleanup/invalid-contacts", async (
            IDbContextFactory<AppDbContext> dbFactory,
            ILogger<Program> logger) =>
        {
            await using var db = await dbFactory.CreateDbContextAsync();

            var websites = await db.Websites.ToListAsync();
            int cleaned = 0;

            foreach (var website in websites)
            {
                bool modified = false;

                // Clean emails
                if (!string.IsNullOrWhiteSpace(website.EmailsCsv))
                {
                    var emails = website.EmailsCsv.Split(',')
                        .Select(e => e.Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e) && IsValidEmail(e))
                        .Distinct()
                        .ToList();

                    var newCsv = emails.Count > 0 ? string.Join(",", emails) : null;
                    if (newCsv != website.EmailsCsv)
                    {
                        website.EmailsCsv = newCsv;
                        modified = true;
                    }
                }

                // Clean phones
                if (!string.IsNullOrWhiteSpace(website.PhonesCsv))
                {
                    var phones = website.PhonesCsv.Split(',')
                        .Select(p => p.Trim())
                        .Select(p => NormalizePhone(p))
                        .Where(p => p != null)
                        .Distinct()
                        .ToList();

                    var newCsv = phones.Count > 0 ? string.Join(",", phones) : null;
                    if (newCsv != website.PhonesCsv)
                    {
                        website.PhonesCsv = newCsv;
                        modified = true;
                    }
                }

                if (modified) cleaned++;
            }

            await db.SaveChangesAsync();

            logger.LogInformation("Cleaned {Count} websites", cleaned);

            return Results.Ok(new
            {
                totalWebsites = websites.Count,
                cleanedWebsites = cleaned,
                message = $"{cleaned} web sitesinin iletişim bilgileri temizlendi"
            });
        })
        .WithTags("Cleanup");
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        if (email.Length < 5 || email.Length > 254) return false;

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1) return false;

        var domain = email.Substring(atIndex + 1);
        if (!domain.Contains('.')) return false;

        // Blacklist common false matches
        var blacklist = new[] { ".png", ".jpg", ".jpeg", ".gif", ".svg", ".css", ".js", ".woff", ".ttf", ".eot" };
        if (blacklist.Any(ext => email.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            return false;

        // Must end with valid TLD
        var validTlds = new[] { "com", "net", "org", "tr", "edu", "gov", "info", "co", "io", "me", "biz", "xyz" };
        if (!validTlds.Any(tld => email.EndsWith($".{tld}", StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private static string? NormalizePhone(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        var digits = new string(s.Where(char.IsDigit).ToArray());

        // Must be between 10-14 digits
        if (digits.Length < 10 || digits.Length > 14) return null;

        // Turkish phone number format validation
        if (digits.Length == 11 && digits.StartsWith("0"))
            return s.Trim();
        if (digits.Length == 12 && digits.StartsWith("90"))
            return s.Trim();
        if (digits.Length == 10)
            return "0" + s.Trim();

        // Other formats are suspicious
        return null;
    }
}