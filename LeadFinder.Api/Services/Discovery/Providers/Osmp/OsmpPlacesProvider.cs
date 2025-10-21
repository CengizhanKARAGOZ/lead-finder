using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static LeadFinder.Api.Services.Discovery.DiscoveryHelpers;

namespace LeadFinder.Api.Services.Discovery.Providers.Osmp;

public sealed class OsmpPlacesProvider(IHttpClientFactory httpClientFactory, ILogger<OsmpPlacesProvider> logger)
    : IPlacesProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly Regex RxDigits = new(@"`\d", RegexOptions.Compiled);

    public async Task<IReadOnlyList<PlaceResult>> FindPlacesAsync(string cityOrDistrict, string keyword,
        CancellationToken ct = default)
    {
        var bbox = await ResolveBoundingBoxAsync(cityOrDistrict, ct);
        if (bbox is null)
        {
            logger.LogInformation("OSM: bbox not found for {City}", cityOrDistrict);
            return Array.Empty<PlaceResult>();
        }
        
        var elements = await QueryOverpassAsync(bbox.Value, keyword, ct);
        if (elements.Count == 0) return Array.Empty<PlaceResult>();
        
        var results = new List<PlaceResult>(elements.Count);
        foreach (var e in elements)
        {
            var tags = e.Tags ?? new Dictionary<string, string>();

            string name = tags.GetValueOrDefault("name", "");
            string? website = GetFirstNonEmpty(
                    tags.GetValueOrDefault("website"),
                    tags.GetValueOrDefault("contact:website"),
                    tags.GetValueOrDefault("url"));

            var phones = new List<string>();
            var p1 = tags.GetValueOrDefault("contact:phone");
            var p2 = tags.GetValueOrDefault("phone");
            if (!string.IsNullOrWhiteSpace(p1)) phones.Add(p1);
            if (!string.IsNullOrWhiteSpace(p2)) phones.Add(p2);

            var address = BuildAddress(tags);
            
            results.Add(new PlaceResult
            {
                Name = name,
                Address = address,
                Phones = phones,
                WebsiteUrl = NormalizeUrlOrNull(website),
                PlaceId = e.Id?.ToString() 
            });
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLowerInvariant();
            results = results
                .Where(r => r.Name.ToLowerInvariant().Contains(kw))
                .ToList();
        }
        
        return results;
    }

    private async Task<(double south, double west, double north, double east)?> ResolveBoundingBoxAsync(string query,
        CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(20);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LeadFinder", "1.0"));

        var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(query)}";
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        var arr = JsonSerializer.Deserialize<List<NominatimResult>>(json, JsonOpts) ?? [];
        var first = arr.FirstOrDefault();
        if (first?.BoundingBox is null || first.BoundingBox.Length != 4) return null;
        if (!double.TryParse(first.BoundingBox[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var south)) return null;
        if (!double.TryParse(first.BoundingBox[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var north)) return null;
        if (!double.TryParse(first.BoundingBox[2], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var west)) return null;
        if (!double.TryParse(first.BoundingBox[3], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var east)) return null;

        return (south, west, north, east);
    }

    private async Task<List<OverpassElement>> QueryOverpassAsync(
        (double south, double west, double north, double east) bbox, string keyword, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LeadFinder", "1.0"));

        // Overpass QL: bbox = south,west,north,east
        var qKeyword = keyword.Replace("\"", ""); // basit kaçış
        var data = $"""
                    [out:json][timeout:30];
                    (
                      node["name"~"{qKeyword}", i]({bbox.south},{bbox.west},{bbox.north},{bbox.east});
                      way["name"~"{qKeyword}", i]({bbox.south},{bbox.west},{bbox.north},{bbox.east});
                      relation["name"~"{qKeyword}", i]({bbox.south},{bbox.west},{bbox.north},{bbox.east});
                    );
                    out tags center 50;
                    """;

        using var resp = await http.PostAsync("https://overpass-api.de/api/interpreter", new StringContent(data), ct);
        if (!resp.IsSuccessStatusCode) return [];

        var json = await resp.Content.ReadAsStringAsync(ct);
        var payload = JsonSerializer.Deserialize<OverpassResponse>(json, JsonOpts);
        return payload?.Elements ?? [];
    }

    private sealed class NominatimResult
    {
        public string? PlaceId { get; set; }
        public string? DisplayName { get; set; }
        [JsonPropertyName("boundingbox")]
        public string[]? BoundingBox { get; set; }
    }

    private sealed class OverpassResponse
    {
        public List<OverpassElement> Elements { get; set; } = [];
    }

    private sealed class OverpassElement
    {
        public long? Id { get; set; }
        public string? Type { get; set; }
        public Dictionary<string, string> Tags { get; set; }
    }
}