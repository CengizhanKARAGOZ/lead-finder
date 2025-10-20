using System.Text.RegularExpressions;

namespace LeadFinder.Api.Services.SiteAudit;

public class SiteAuditHelpers
{
    

    public static string? NormalizeUrl(string raw)
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

    public static string GetHostOrUnknown(string url)
    {
        try
        {
            return new Uri(url).Host;
        }
        catch
        {
            return "unknown";
        }
    }

    public static bool IsTextHtml(HttpResponseMessage resp)
        => resp.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true
           || resp.Content.Headers.ContentType?.MediaType?.Contains("text", StringComparison.OrdinalIgnoreCase) == true;

    public static string? TryGetTitle(string? html)
    {
        if (string.IsNullOrEmpty(html)) return null;
        var m = SiteAuditRegex.Title.Match(html);
        if (!m.Success) return null;
        var t = System.Net.WebUtility.HtmlDecode(m.Groups["t"].Value).Trim();
        return string.IsNullOrWhiteSpace(t) ? null : (t.Length > 256 ? t[..256] : t);
    }

    public static string ToAbsoluteUrl(string baseUrl, string href)
    {
        if (string.IsNullOrWhiteSpace(href)) return baseUrl;
        try
        {
            var b = new Uri(baseUrl);
            var u = new Uri(b, href);
            return u.ToString();
        }
        catch
        {
            return href;
        }
    }

    public static bool LooksLikeContact(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.ToLowerInvariant();
        return s.Contains("contact") || s.Contains("iletisim") || s.Contains("bize-ulas");
    }

    public static string? NormalizePhone(string s)
    {
        var digits = new string(s.Where(char.IsDigit).ToArray());
        if (digits.Length < 9) return null;
        return s.Trim();
    }
}