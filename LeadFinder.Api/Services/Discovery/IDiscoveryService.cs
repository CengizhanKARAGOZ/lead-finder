namespace LeadFinder.Api.Services.Discovery;

public interface IDiscoveryService
{
    Task<IReadOnlyList<DiscoveryCandidate>> DiscoverAsync(string cityOrDistrict, string keyword, CancellationToken ct = default);
}

public sealed class DiscoveryCandidate
{
    public string Source { get; init; } = "";
    public string? SourceId { get; init; }
    
    public string Name { get; init; }
    public string? Address { get; init; }
    public List<string> Phones { get; init; } = new();
    
    public string? WebsiteUrl { get; init; }
    public string? Domain { get; init; }
    
    public string QueryCity { get; init; } = "";
    public string QueryKeyword { get; init; } = "";
}