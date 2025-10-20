using System.Text.RegularExpressions;

namespace LeadFinder.Api.Services.SiteAudit;

public class SiteAuditRegex
{
    public static readonly Regex Title =
        new(@"<title[^>]*>(?<t>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static readonly Regex Viewport =
        new(@"<meta[^>]+name\s*=\s*[""']viewport[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex Email =
        new(@"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // TR dâhil geniş numara yakalayıcı (ileride E.164 normalizasyonu ekleriz)
    public static readonly Regex Phone =
        new(@"\+?\d[\d\s\-\(\)]{8,}\d", RegexOptions.Compiled);

    public static readonly Regex AnchorHref =
        new(@"<a[^>]+href\s*=\s*[""'](?<u>[^""'#>]+)[""'][^>]*>(?<t>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
}