namespace LeadFinder.Api.Services.SiteAudit;

public interface ISiteAuditService
{
    Task<SiteAuditResult> AuditAsync(string inputUrl, CancellationToken ct = default);
}

public sealed class SiteAuditResult
{
    //Source Information
    public required string InputUrl { get; init; }
    public required string FinalUrl { get; init; } //URL reached after the redirect
    public required string Host { get; init; }
    
    //Technical Signals
    public bool IsHttps { get; init; }
    public string? Title { get; init; }
    public bool HasViewPortMeta { get; init; }
    
    //Communication Signals
    public bool HasContactPage { get; init; }
    public List<string> Emails { get; init; } = new();
    public List<string> Phones { get; init; } = new();
    
    //Score + notes
    public int Score { get; init; }
    public List<string> Notes { get; init; } = new();
    
    //Optional: raw HTML length (debug)
    public int? HtmlLength { get; init; }
}