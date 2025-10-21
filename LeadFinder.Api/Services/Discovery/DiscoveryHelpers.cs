namespace LeadFinder.Api.Services.Discovery;

public static class DiscoveryHelpers
{
    public static string? GetFirstNonEmpty(params string?[] arr)
        => arr.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

    public static string? NormalizeUrlOrNull(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (!s.StartsWith("http://") && !s.StartsWith("https://"))
            s = "https://" + s;
        try { _ = new Uri(s); return s; } catch { return null; }
    }

    public static string? BuildAddress(Dictionary<string,string> tags)
    {
        var parts = new[]
        {
            tags.TryGetValue("addr:street", out var st) ? st : null,
            tags.TryGetValue("addr:housenumber", out var no) ? no : null,
            tags.TryGetValue("addr:neighbourhood", out var nb) ? nb : null,
            tags.TryGetValue("addr:suburb", out var sb) ? sb : null,
            tags.TryGetValue("addr:city", out var city) ? city : null,
            tags.TryGetValue("addr:postcode", out var pc) ? pc : null
        }.Where(x => !string.IsNullOrWhiteSpace(x));

        var addr = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(addr) ? null : addr;
    }
}