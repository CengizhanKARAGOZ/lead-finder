namespace LeadFinder.Api.Services.Discovery.Providers;

public interface IPlacesProvider
{
    Task<IReadOnlyList<PlaceResult>> FindPlacesAsync(string cityOrDistrict, string keyword, CancellationToken ct = default);
}

public sealed class PlaceResult
{
    public string Name { get; init; } = "";
    public string? Address { get; init; }
    public List<string> Phones { get; init; } = new();
    public string? WebsiteUrl { get; init; }
    public string? PlaceId { get; init; }
}