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

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        // Uzunluk kontrolü
        if (email.Length < 5 || email.Length > 254) return false;

        // @ kontrolü
        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex == email.Length - 1) return false;

        // Domain kontrolü
        var domain = email.Substring(atIndex + 1);
        if (!domain.Contains('.')) return false;

        // Yasaklı pattern'ler (genelde hatalı eşleşmeler)
        var blacklist = new[] { ".png", ".jpg", ".jpeg", ".gif", ".svg", ".css", ".js", ".woff" };
        if (blacklist.Any(ext => email.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    public static string? NormalizePhone(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        var digits = new string(s.Where(char.IsDigit).ToArray());

        // En az 10 rakam, en fazla 14 rakam olmalı (Türk telefon numaraları)
        if (digits.Length < 10 || digits.Length > 14) return null;

        // Türk telefon numarası formatı kontrolü
        // 0 ile başlayan 11 haneli veya +90 ile başlayan 12 haneli
        if (digits.Length == 11 && digits.StartsWith("0"))
            return s.Trim();
        if (digits.Length == 12 && digits.StartsWith("90"))
            return s.Trim();
        if (digits.Length == 10) // Başında 0 olmadan
            return "0" + s.Trim();

        // Diğer formatlar şüpheli, atla
        return null;
    }
}