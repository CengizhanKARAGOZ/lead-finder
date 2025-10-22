using System.Text.RegularExpressions;

namespace LeadFinder.Api.Services.SiteAudit;

public class SiteAuditRegex
{
    public static readonly Regex Title =
        new(@"<title[^>]*>(?<t>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static readonly Regex Viewport =
        new(@"<meta[^>]+name\s*=\s*[""']viewport[""'][^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Daha sıkı email regex - yaygın uzantıları kontrol et
    public static readonly Regex Email =
        new(@"\b[A-Z0-9._%+\-]{1,64}@[A-Z0-9.\-]{1,255}\.(com|net|org|tr|edu|gov|info|co|io|me|biz|xyz)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // TR telefon formatları: 0XXX XXX XX XX, +90 XXX XXX XX XX, (0XXX) XXX XX XX
    // Minimum 10 rakam, maksimum 15 rakam (uluslararası formatlar için)
    public static readonly Regex Phone =
        new(@"(?:\+90|0)?[\s\-\(]?\d{3}[\s\-\)]?\d{3}[\s\-]?\d{2}[\s\-]?\d{2}\b", RegexOptions.Compiled);

    public static readonly Regex AnchorHref =
        new(@"<a[^>]+href\s*=\s*[""'](?<u>[^""'#>]+)[""'][^>]*>(?<t>.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
}