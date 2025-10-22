using System.Text.RegularExpressions;

namespace LeadFinder.Api.Services.Discovery.Providers;

/// <summary>
/// Free web search using DuckDuckGo HTML scraping (no API key required)
/// </summary>
public sealed class DuckDuckGoWebSearchProvider(
    IHttpClientFactory httpFactory,
    ILogger<DuckDuckGoWebSearchProvider> logger) : IWebSearchProvider
{
    public async Task<IReadOnlyList<(string Title, string Url)>> SearchAsync(
        string cityOrDistrict,
        string keyword,
        CancellationToken ct = default)
    {
        try
        {
            var query = $"{keyword} {cityOrDistrict} site:.tr";
            var http = httpFactory.CreateClient();
            http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";

            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("DuckDuckGo search failed with status {Status}", response.StatusCode);
                return Array.Empty<(string, string)>();
            }

            var html = await response.Content.ReadAsStringAsync(ct);

            return ParseDuckDuckGoResults(html);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DuckDuckGo search error for {City}/{Keyword}", cityOrDistrict, keyword);
            return Array.Empty<(string, string)>();
        }
    }

    private List<(string Title, string Url)> ParseDuckDuckGoResults(string html)
    {
        var results = new List<(string, string)>();

        // DuckDuckGo HTML result pattern
        var resultPattern = new Regex(
            @"<a[^>]+class=""[^""]*result__a[^""]*""[^>]+href=""([^""]+)""[^>]*>([^<]+)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var matches = resultPattern.Matches(html);

        foreach (Match match in matches.Take(10)) // Limit to 10 results
        {
            var url = match.Groups[1].Value;
            var title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value).Trim();

            // DuckDuckGo uses redirect URLs, extract real URL
            if (url.StartsWith("/l/?"))
            {
                var uddgMatch = Regex.Match(url, @"uddg=([^&]+)");
                if (uddgMatch.Success)
                {
                    url = Uri.UnescapeDataString(uddgMatch.Groups[1].Value);
                }
            }

            if (!string.IsNullOrWhiteSpace(url) &&
                !string.IsNullOrWhiteSpace(title) &&
                url.StartsWith("http"))
            {
                results.Add((title, url));
            }
        }

        logger.LogInformation("DuckDuckGo returned {Count} results", results.Count);
        return results;
    }
}