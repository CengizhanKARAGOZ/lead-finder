using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using static LeadFinder.Api.Services.SiteAudit.SiteAuditHelpers;

namespace LeadFinder.Api.Services.SiteAudit;

public sealed class SiteAuditService(IHttpClientFactory httpClientFactory, ILogger<SiteAuditService> logger)
    : ISiteAuditService
{
    public async Task<SiteAuditResult> AuditAsync(string inputUrl, CancellationToken ct = default)
    {
        var normalized = NormalizeUrl(inputUrl);
        if (normalized is null)
            throw new ArgumentNullException("The URL is empty or invalid.",nameof(inputUrl));
        
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(12);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("LeadFinder", "1.0"));
        
        string finalUrl = normalized;
        string host = GetHostOrUnknown(finalUrl);
        string? html = null;

        try
        {
            using var resp = await http.GetAsync(finalUrl, ct);
            finalUrl = resp.RequestMessage?.RequestUri?.ToString() ?? finalUrl;
            host = GetHostOrUnknown(finalUrl);

            if (resp.IsSuccessStatusCode && IsTextHtml(resp))
                html = await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Homepage request failed: {Url}", finalUrl);
        }
        
        var isHttps = finalUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var title = TryGetTitle(html);
        var hasViewport = html is not null && SiteAuditRegex.Viewport.IsMatch(html);
        
        var (hasContact, emails, phones) = await FindContactsAsync(http, finalUrl, html, ct);
        var (score, notes) = ScoreFromSignals(isHttps, title, hasViewport, hasContact, emails, phones);

        return new SiteAuditResult
        {
            InputUrl = inputUrl,
            FinalUrl = finalUrl,
            Host = host,
            IsHttps = isHttps,
            Title = title,
            HasViewPortMeta = hasViewport,
            HasContactPage = hasContact,
            Emails = emails.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            Phones = phones.Select(NormalizePhone).Where(p => p is not null)!.Distinct()!.Take(20)!.ToList()!,
            Score = score,
            Notes = notes,
            HtmlLength = html?.Length
        };
    }
    
    private async Task<(bool hasContact, List<string> emails, List<string> phones)>
        FindContactsAsync(HttpClient http, string baseUrl, string? html, CancellationToken ct)
    {
        var emails = new List<string>();
        var phones = new List<string>();
        var hasContact = false;

        void scan(string? h)
        {
            if (string.IsNullOrEmpty(h)) return;
            foreach (Match m in SiteAuditRegex.Email.Matches(h)) emails.Add(m.Value);
            foreach (Match m in SiteAuditRegex.Phone.Matches(h)) phones.Add(Regex.Replace(m.Value, @"\s+", " ").Trim());
        }

        // a) Scan the homepage
        scan(html);

        // b) Collect links that appear as contact/iletisim within <a href>
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(html))
        {
            foreach (Match m in SiteAuditRegex.AnchorHref.Matches(html))
            {
                var href = m.Groups["u"].Value;
                var text = m.Groups["t"].Value;
                if (LooksLikeContact(href) || LooksLikeContact(text))
                    candidates.Add(ToAbsoluteUrl(baseUrl, href));
            }
        }

        // c) Heuristic methods
        var baseUri = new Uri(baseUrl);
        candidates.Add(new Uri(baseUri, "/contact").ToString());
        candidates.Add(new Uri(baseUri, "/iletisim").ToString());
        candidates.Add(new Uri(baseUri, "/contact-us").ToString());
        candidates.Add(new Uri(baseUri, "/bize-ulasin").ToString());

        // Uniq + upper limit
        candidates = candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();

        foreach (var u in candidates)
        {
            try
            {
                using var resp = await http.GetAsync(u, ct);
                if (!resp.IsSuccessStatusCode || !IsTextHtml(resp)) continue;

                var sub = await resp.Content.ReadAsStringAsync(ct);
                scan(sub);

                // Communication page signal
                if (SiteAuditRegex.Email.IsMatch(sub) || sub.Contains("tel:", StringComparison.OrdinalIgnoreCase))
                    hasContact = true;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "The contact page could not be read: {Url}", u);
            }
        }

        // d) If there is an open signal on the main page
        if (!hasContact && html is not null)
        {
            hasContact = html.Contains("mailto:", StringComparison.OrdinalIgnoreCase)
                      || html.Contains("tel:", StringComparison.OrdinalIgnoreCase)
                      || html.Contains(">İletişim<", StringComparison.OrdinalIgnoreCase)
                      || html.Contains(">Contact<", StringComparison.OrdinalIgnoreCase);
        }

        return (hasContact, emails, phones);
    }

    private static (int score, List<string> notes) ScoreFromSignals(
        bool isHttps, string? title, bool hasViewport, bool hasContact,
        List<string> emails, List<string> phones)
    {
        var score = 0;
        var notes = new List<string>();

        if (isHttps)                      { score += 10; notes.Add("HTTPS detected (+10)"); }
        if (!string.IsNullOrWhiteSpace(title)) { score += 5;  notes.Add("Title detected (+5)"); }
        if (hasViewport)                  { score += 3;  notes.Add("Meta viewport detected (+3)"); }
        if (hasContact)                   { score += 10; notes.Add("İletişim sayfası/sinyali detected (+10)"); }
        if (emails.Count > 0)             { score += 4;  notes.Add("E-posta detected (+4)"); }
        if (phones.Count > 0)             { score += 3;  notes.Add("Telefon detected (+3)"); }

        return (Math.Clamp(score, 0, 100), notes);
    }
    
    
}