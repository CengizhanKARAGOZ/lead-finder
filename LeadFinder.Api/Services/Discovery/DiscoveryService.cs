using LeadFinder.Api.Services.Discovery.Providers;

namespace LeadFinder.Api.Services.Discovery;

public sealed class DiscoveryService(
    IEnumerable<IWebSearchProvider> webSearchProviders,
    IEnumerable<IPlacesProvider> placesProviders,
    ILogger<DiscoveryService> logger) : IDiscoveryService
{
    public async Task<IReadOnlyList<DiscoveryCandidate>> DiscoverAsync(string cityOrDistrict, string keyword,
        CancellationToken ct = default)
    {
        var webResults = new List<(string Title, string Url)>();
        foreach (var p in webSearchProviders)
        {
            try
            {
                var r = await p.SearchAsync(cityOrDistrict, keyword, ct);
                if (r is { Count: > 0 }) webResults.AddRange(r);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "WebSearchProvider error: {Provider}", p.GetType().Name);
            }
        }

        var placeResults = new List<PlaceResult>();
        foreach (var p in placesProviders)
        {
            try
            {
                var r = await p.FindPlacesAsync(cityOrDistrict, keyword, ct);
                if (r is { Count: > 0 }) placeResults.AddRange(r);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PlacesProvider error: {Provider}", p.GetType().Name);
            }
        }

        var candidates = new List<DiscoveryCandidate>();

        foreach (var (title, url) in webResults)
        {
            var domain = TryExtractDomain(url);
            if (domain is null) continue;

            candidates.Add(new DiscoveryCandidate
            {
                Source = "web-search",
                Name = title,
                WebsiteUrl = NormalizeUrl(url),
                Domain = domain,
                QueryCity = cityOrDistrict,
                QueryKeyword = keyword,
            });
        }

        foreach (var pr in placeResults)
        {
            var domain = pr.WebsiteUrl is null ? null : TryExtractDomain(pr.WebsiteUrl);

            candidates.Add(new DiscoveryCandidate
                {
                    Source = "maps",
                    SourceId = pr.PlaceId,
                    Name = pr.Name,
                    Address = pr.Address,
                    Phones = pr.Phones,
                    WebsiteUrl = NormalizeUrl(pr.WebsiteUrl),
                    Domain = domain,
                    QueryCity = cityOrDistrict,
                    QueryKeyword = keyword
                }
            );
        }
        
        var uniq = candidates
            .GroupBy(c => c.Domain ?? c.WebsiteUrl ?? c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        return uniq;
    }

    private static string? NormalizeUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (!s.StartsWith("http://") && !s.StartsWith("https://"))
            s = "https://" + s;
        try
        {
            _ = new Uri(s);
            return s;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractDomain(string url)
    {
        try
        {
            return new Uri(NormalizeUrl(url)!).Host;
        }
        catch
        {
            return null;
        }
    }
}