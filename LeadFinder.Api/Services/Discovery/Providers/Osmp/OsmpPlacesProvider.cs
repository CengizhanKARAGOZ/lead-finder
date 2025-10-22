using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeadFinder.Api.Services.Discovery.Providers.Osmp;

public sealed class OsmpPlacesProvider(IHttpClientFactory httpFactory, ILogger<OsmpPlacesProvider> logger)
    : IPlacesProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<PlaceResult>> FindPlacesAsync(string cityOrDistrict, string keyword,
        CancellationToken ct = default)
    {
        // bbox + areaId çöz (idari sınır içinde arama için)
        var geo = await ResolveGeoAsync(cityOrDistrict, ct);
        if (geo is null)
        {
            logger.LogInformation("OSM: geo not found for {City}", cityOrDistrict);
            return Array.Empty<PlaceResult>();
        }

        var bbox   = (geo.Value.south, geo.Value.west, geo.Value.north, geo.Value.east);
        var areaId = geo.Value.areaId;

        // bbox’u %15 genişlet (ilçe sınırı dar olabilir)
        var exp = Expand(bbox, 0.15);

        var filters  = KeywordToOverpassFilters(keyword);
        var elements = await QueryOverpassAsync(exp, areaId, keyword, filters, ct);

        logger.LogInformation("OSM returned {Count} elements for '{City}' / '{Keyword}'",
            elements.Count, cityOrDistrict, keyword);

        if (elements.Count == 0) return Array.Empty<PlaceResult>();

        var results = new List<PlaceResult>(elements.Count);
        var kwLower = (keyword ?? "").ToLowerInvariant();

        logger.LogInformation("Processing {Count} OSM elements. Filters: shop={ShopCount}, amenity={AmenityCount}, craft={CraftCount}",
            elements.Count, filters.ShopValues.Count, filters.AmenityValues.Count, filters.CraftValues.Count);

        foreach (var e in elements)
        {
            var tags = e.Tags ?? new Dictionary<string, string>();

            string name = tags.TryGetValue("name", out var n) ? n
                : tags.TryGetValue("brand", out var b) ? b
                : tags.TryGetValue("operator", out var op) ? op
                : "";

            var nameLower = name.ToLowerInvariant();
            var description = Tag(tags, "description")?.ToLowerInvariant() ?? "";

            // Gevşek filtreleme: Kategori eşleşmeli VEYA isim/açıklamada keyword olmalı
            var categoryMatch = MatchesCategory(tags, filters);
            var nameMatch = !string.IsNullOrWhiteSpace(name) && (nameLower.Contains(kwLower) || description.Contains(kwLower));

            // Kategori filtresi varsa ve eşleşiyorsa direkt ekle
            if (categoryMatch)
            {
                // OK, kategori eşleşti
            }
            // Kategori filtresi yoksa ve isim eşleşiyorsa ekle
            else if (filters.ShopValues.Count == 0 && filters.AmenityValues.Count == 0 && filters.CraftValues.Count == 0 && nameMatch)
            {
                // OK, kategori filtresi yok, isim eşleşti
            }
            // Kategori filtresi var ama eşleşmedi, isim eşleşmeli ve genel kategori olmamalı
            else if (nameMatch)
            {
                var genericCategories = new[] { "cafe", "restaurant", "fast_food", "bar", "pub", "bank", "atm" };
                if (tags.TryGetValue("amenity", out var amenity) && genericCategories.Contains(amenity))
                {
                    logger.LogDebug("Skipping generic category: {Name} (amenity={Amenity})", name, amenity);
                    continue;
                }
                // İsim eşleşti ve genel kategori değil, ekle
            }
            else
            {
                // Hiçbir şey eşleşmedi
                continue;
            }

            string? website = GetFirstNonEmpty(
                Tag(tags, "website"),
                Tag(tags, "contact:website"),
                Tag(tags, "url"));

            var phones = new List<string>();
            AddIfNotEmpty(phones, Tag(tags, "contact:phone"));
            AddIfNotEmpty(phones, Tag(tags, "phone"));

            var address = BuildAddress(tags);

            results.Add(new PlaceResult
            {
                Name       = name,
                Address    = address,
                Phones     = phones,
                WebsiteUrl = NormalizeUrlOrNull(website),
                PlaceId    = e.Id?.ToString()
            });
        }

        logger.LogInformation("Filtered down to {ResultCount} results from {ElementCount} elements", results.Count, elements.Count);
        return results;
    }

    // ----------------- helpers -----------------

    public static string? GetFirstNonEmpty(params string?[] arr)
        => arr.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

    private static void AddIfNotEmpty(List<string> list, string? v)
    {
        if (!string.IsNullOrWhiteSpace(v)) list.Add(v);
    }

    private static string? Tag(Dictionary<string, string> tags, string key) =>
        tags.TryGetValue(key, out var v) ? v : null;

    private static string? NormalizeUrlOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (!s.StartsWith("http://") && !s.StartsWith("https://")) s = "https://" + s;
        try { _ = new Uri(s); return s; } catch { return null; }
    }

    private static string? BuildAddress(Dictionary<string, string> tags)
    {
        var parts = new[]
        {
            Tag(tags, "addr:street"),
            Tag(tags, "addr:housenumber"),
            Tag(tags, "addr:neighbourhood"),
            Tag(tags, "addr:suburb"),
            Tag(tags, "addr:city"),
            Tag(tags, "addr:postcode")
        }.Where(x => !string.IsNullOrWhiteSpace(x));
        var addr = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(addr) ? null : addr;
    }

    private static (double south, double west, double north, double east) Expand(
        (double south, double west, double north, double east) b, double ratio)
    {
        var h = b.north - b.south;
        var w = b.east  - b.west;
        var dh = h * ratio;
        var dw = w * ratio;
        return (b.south - dh, b.west - dw, b.north + dh, b.east + dw);
    }

    // ── Nominatim: bbox + areaId (idari alan)
    private async Task<(double south, double west, double north, double east, long? areaId)?> ResolveGeoAsync(
        string query, CancellationToken ct)
    {
        var http = httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(20);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LeadFinder", "1.0"));

        var url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(query)}";
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json  = await resp.Content.ReadAsStringAsync(ct);
        var arr   = JsonSerializer.Deserialize<List<NominatimResult>>(json, JsonOpts) ?? [];
        var first = arr.FirstOrDefault();
        if (first?.BoundingBox is null || first.BoundingBox.Length != 4) return null;

        // Nominatim: [south, north, west, east]
        if (!double.TryParse(first.BoundingBox[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var south)) return null;
        if (!double.TryParse(first.BoundingBox[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var north)) return null;
        if (!double.TryParse(first.BoundingBox[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var west )) return null;
        if (!double.TryParse(first.BoundingBox[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var east )) return null;

        long? areaId = ToOverpassAreaId(first);
        return (south, west, north, east, areaId);
    }

    private static long? ToOverpassAreaId(NominatimResult nr)
    {
        if (nr.OsmId is null || string.IsNullOrWhiteSpace(nr.OsmType)) return null;
        return nr.OsmType switch
        {
            "relation" => 3600000000L + nr.OsmId.Value,
            "way"      => 2400000000L + nr.OsmId.Value,
            _          => null // node için area yok
        };
    }

    private async Task<List<OverpassElement>> QueryOverpassAsync(
    (double south, double west, double north, double east) bbox,
    long? areaId,
    string keyword,
    OverpassFilters filters,
    CancellationToken ct)
{
    var http = httpFactory.CreateClient();
    http.Timeout = TimeSpan.FromSeconds(60); // 35 → 60
    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LeadFinder", "1.0"));

    var kw = (keyword ?? "").Replace("\"", "").Trim();

    // Invariant sayı formatı
    var s = bbox.south.ToString("G", CultureInfo.InvariantCulture);
    var w = bbox.west .ToString("G", CultureInfo.InvariantCulture);
    var n = bbox.north.ToString("G", CultureInfo.InvariantCulture);
    var e = bbox.east .ToString("G", CultureInfo.InvariantCulture);

    var scopeDecl = areaId is not null ? $"area({areaId}) -> .searchArea;" : "";
    var scope     = areaId is not null ? "(area.searchArea)" : $"({s},{w},{n},{e})";

    // ---- bloklar
    var tagBlock = new StringBuilder();
    if (filters.ShopValues.Count > 0)
    {
        var rx = string.Join("|", filters.ShopValues);
        tagBlock.AppendLine($"""
            node["shop"~"^({rx})$"]{scope};
            way["shop"~"^({rx})$"]{scope};
            relation["shop"~"^({rx})$"]{scope};
            """);
    }
    if (filters.AmenityValues.Count > 0)
    {
        var rx = string.Join("|", filters.AmenityValues);
        tagBlock.AppendLine($"""
            node["amenity"~"^({rx})$"]{scope};
            way["amenity"~"^({rx})$"]{scope};
            """);
    }
    if (filters.CraftValues.Count > 0)
    {
        var rx = string.Join("|", filters.CraftValues);
        tagBlock.AppendLine($"""
            node["craft"~"^({rx})$"]{scope};
            way["craft"~"^({rx})$"]{scope};
            """);
    }

    var nameBlock = $"""
        node["name"~"{kw}", i]{scope};
        way["name"~"{kw}", i]{scope};
        relation["name"~"{kw}", i]{scope};
        node["brand"~"{kw}", i]{scope};
        way["brand"~"{kw}", i]{scope};
        node["operator"~"{kw}", i]{scope};
        """;

    // ---- iki aşamalı sorgu: 1) kategori 2) isim. İkisi de başarısızsa GET fallback
    string BuildQuery(string inner) => $"""
        [out:json][timeout:55];
        {scopeDecl}
        (
          {inner}
        );
        out tags center 150;
        """;

    var endpoints = new[]
    {
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.openstreetmap.ru/api/interpreter"
    };

    async Task<List<OverpassElement>?> TrySendAsync(string ep, string q, int attempt, CancellationToken token)
    {
        // küçük backoff: 0s, 1s, 3s
        var delay = attempt switch { 0 => 0, 1 => 1000, _ => 3000 };
        if (delay > 0) await Task.Delay(delay, token);

        // A) POST form-urlencoded
        try
        {
            using var postContent = new StringContent($"data={Uri.EscapeDataString(q)}", Encoding.UTF8, "application/x-www-form-urlencoded");
            using var postResp = await http.PostAsync(ep, postContent, token);
            if (postResp.IsSuccessStatusCode)
            {
                var json = await postResp.Content.ReadAsStringAsync(token);
                try
                {
                    var payload = JsonSerializer.Deserialize<OverpassResponse>(json, JsonOpts);
                    if (payload?.Elements is { Count: > 0 }) return payload.Elements;
                }
                catch (JsonException jex)
                {
                    logger.LogWarning(jex, "Overpass POST JSON parse error at {Ep}", ep);
                }
            }
            else
            {
                var err = await postResp.Content.ReadAsStringAsync(token);
                logger.LogWarning("Overpass POST {Code} at {Ep}: {Err}", (int)postResp.StatusCode, ep, Trunc(err, 300));
            }
        }
        catch (TaskCanceledException tce)
        {
            logger.LogWarning(tce, "Overpass POST timeout at {Ep}", ep);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Overpass POST exception at {Ep}", ep);
        }

        // B) GET fallback
        try
        {
            using var getResp = await http.GetAsync(ep + "?data=" + Uri.EscapeDataString(q), token);
            if (getResp.IsSuccessStatusCode)
            {
                var json = await getResp.Content.ReadAsStringAsync(token);
                try
                {
                    var payload = JsonSerializer.Deserialize<OverpassResponse>(json, JsonOpts);
                    if (payload?.Elements is { Count: > 0 }) return payload.Elements;
                }
                catch (JsonException jex)
                {
                    logger.LogWarning(jex, "Overpass GET JSON parse error at {Ep}", ep);
                }
            }
            else
            {
                var err = await getResp.Content.ReadAsStringAsync(token);
                logger.LogWarning("Overpass GET {Code} at {Ep}: {Err}", (int)getResp.StatusCode, ep, Trunc(err, 300));
            }
        }
        catch (TaskCanceledException tce)
        {
            logger.LogWarning(tce, "Overpass GET timeout at {Ep}", ep);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Overpass GET exception at {Ep}", ep);
        }

        return null;
    }

    // 1) Önce kategori (daha hafif); 2) boşsa isim araması
    var queries = new List<string>();
    if (tagBlock.Length > 0) queries.Add(BuildQuery(tagBlock.ToString()));
    queries.Add(BuildQuery(nameBlock));

    foreach (var q in queries)
    {
        foreach (var ep in endpoints)
        {
            // her endpoint için birkaç deneme (3)
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var res = await TrySendAsync(ep, q, attempt, ct);
                if (res is { Count: > 0 }) return res;
            }
        }
    }

    return [];
}

    private static bool MatchesCategory(Dictionary<string, string> tags, OverpassFilters f)
    {
        if (tags.TryGetValue("shop", out var shop) && f.ShopValues.Contains(shop)) return true;
        if (tags.TryGetValue("amenity", out var amen) && f.AmenityValues.Contains(amen)) return true;
        if (tags.TryGetValue("craft", out var craft) && f.CraftValues.Contains(craft)) return true;
        return false;
    }

    private static string Trunc(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";

    // --------- Dynamic filter builder ---------

    private sealed class OverpassFilters
    {
        public HashSet<string> ShopValues    { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AmenityValues { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> CraftValues   { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static OverpassFilters KeywordToOverpassFilters(string keyword)
    {
        var f  = new OverpassFilters();
        var kw = (keyword ?? "").ToLowerInvariant();

        var map = new (string needle, Action<OverpassFilters> add)[]
        {
            ("parke",     a => { a.ShopValues.UnionWith(new[] { "flooring", "doityourself", "hardware" }); a.CraftValues.Add("floorer"); }),
            ("zemin",     a => { a.ShopValues.UnionWith(new[] { "flooring", "doityourself" }); a.CraftValues.Add("floorer"); }),
            ("laminat",   a => { a.ShopValues.UnionWith(new[] { "flooring", "doityourself" }); }),
            ("parkeci",   a => { a.CraftValues.Add("floorer"); a.ShopValues.Add("flooring"); }),
            ("pvc",       a => { a.ShopValues.UnionWith(new[] { "flooring", "doityourself" }); a.CraftValues.Add("floorer"); }),
            ("epoksi",    a => { a.ShopValues.UnionWith(new[] { "flooring","doityourself","paint" }); }),
            ("süpürgelik",a => { a.ShopValues.UnionWith(new[] { "flooring","hardware" }); }),

            ("kuaför",    a => { a.ShopValues.Add("hairdresser"); a.AmenityValues.Add("hairdresser"); }),
            ("berber",    a => { a.ShopValues.Add("hairdresser"); a.AmenityValues.Add("hairdresser"); }),
            ("eczane",    a => { a.AmenityValues.Add("pharmacy"); }),
            ("restoran",  a => { a.AmenityValues.Add("restaurant"); }),
            ("kafe",      a => { a.AmenityValues.Add("cafe"); }),
            ("mobilya",   a => { a.ShopValues.Add("furniture"); }),
            ("tesisat",   a => { a.ShopValues.UnionWith(new[] { "hardware", "doityourself" }); }),
            ("yapı",      a => { a.ShopValues.UnionWith(new[] { "hardware", "doityourself", "interior" }); }),
        };

        foreach (var (needle, add) in map)
            if (kw.Contains(needle)) add(f);

        return f; // eşleşme yoksa sadece name/brand/operator araması yapılacak
    }

    // ---------- DTOs ----------
    private sealed class NominatimResult
    {
        [JsonPropertyName("boundingbox")] public string[]? BoundingBox { get; set; }
        [JsonPropertyName("osm_type")]    public string?   OsmType     { get; set; } // "relation" | "way" | "node"
        [JsonPropertyName("osm_id")]      public long?     OsmId       { get; set; }
    }

    private sealed class OverpassResponse
    {
        public List<OverpassElement> Elements { get; set; } = [];
    }

    private sealed class OverpassElement
    {
        public long? Id { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }
}
