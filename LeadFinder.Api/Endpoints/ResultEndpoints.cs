using LeadFinder.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeadFinder.Api.Endpoints;

public static class ResultEndpoints
{
    public static void MapResultEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/results").WithTags("Results");

        grp.MapGet("/", async (
            IDbContextFactory<AppDbContext> dbf,
            int page = 1,
            int pageSize = 50,
            int poorThreshold = 20,
            CancellationToken ct = default) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            await using var db = await dbf.CreateDbContextAsync(ct);

            var latestScoresPerWebsite = await db.LeadScores
                .AsNoTracking()
                .GroupBy(s => s.WebsiteId)
                .Select(g => g.OrderByDescending(s => s.Score).First())
                .ToListAsync(ct);

            var latestMap = latestScoresPerWebsite.ToDictionary(x => x.WebsiteId);

            var query = db.Websites.AsNoTracking()
                .Join(db.Businesses.AsNoTracking(),
                    w => w.BusinessId,
                    b => b.Id,
                    (w, b) => new { w, b });

            var total = await query.CountAsync(ct);
            var rows = await query
                .OrderByDescending(x => x.w.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var data = rows.Select(x =>
            {
                latestMap.TryGetValue(x.w.Id, out var ls);
                var score = ls?.Score;
                var quality = score is null ? "Unknown"
                    : score < poorThreshold ? "Poor"
                    : "Ok";

                return new ResultDto
                {
                    BusinessId = x.b.Id,
                    Business = x.b.Name,
                    City = x.b.City,
                    Keyword = x.b.Keyword,
                    Address = x.b.Address,
                    Phone = x.b.Phone,
                    Email = x.b.Email,
                    WebsiteId = x.w.Id,
                    Domain = x.w.Domain,
                    HomepageUrl = x.w.HomepageUrl,
                    Emails = x.w.EmailsCsv,
                    Phones = x.w.PhonesCsv,
                    Score = score,
                    Quality = quality,
                    ReasonsJson = ls?.ReasonsJson,
                    ComputedAt = ls?.ComputedAt
                };
            }).ToList();
            return Results.Ok(new PagedResult<ResultDto>(total, page, pageSize, data));
        });

        grp.MapGet("/summary",
            async (IDbContextFactory<AppDbContext> dbf, int poorThreshold = 20, CancellationToken ct = default) =>
            {
                await using var db = await dbf.CreateDbContextAsync(ct);

                var latest = await db.LeadScores.AsNoTracking()
                    .GroupBy(s => s.WebsiteId)
                    .Select(g => g.OrderByDescending(x => x.ComputedAt).First())
                    .ToListAsync(ct);

                var totalWebsites = await db.Websites.CountAsync(ct);
                var withScore = latest.Count;
                var poor = latest.Count(x => x.Score < poorThreshold);
                var ok = latest.Count(x => x.Score >= poorThreshold);
                var unknown = totalWebsites - withScore;

                return Results.Ok(new
                {
                    totalWebsites,
                    withScore,
                    ok,
                    poor,
                    unknown,
                    poorThreshold
                });
            });
    }

    public sealed class ResultDto
    {
        public long BusinessId { get; set; }
        public string Business { get; set; } = default!;
        public string? City { get; set; }
        public string? Keyword { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }

        public long WebsiteId { get; set; }
        public string Domain { get; set; } = default!;
        public string? HomepageUrl { get; set; }
        public string? Emails { get; set; }
        public string? Phones { get; set; }

        public int? Score { get; set; }
        public string Quality { get; set; } = "Unknown"; // "Poor" | "Ok" | "Unknown"
        public string? ReasonsJson { get; set; }
        public DateTime? ComputedAt { get; set; }
    }

    public sealed class PagedResult<T>(int total, int page, int pageSize, IReadOnlyList<T> items)
    {
        public int Total { get; } = total;
        public int Page { get; } = page;
        public int PageSize { get; } = pageSize;
        public IReadOnlyList<T> Items { get; } = items;
    }
}