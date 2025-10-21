namespace LeadFinder.Api.Services.Discovery.Providers;

public sealed class DummyPlacesProvider : IPlacesProvider
{
    public Task<IReadOnlyList<PlaceResult>> FindPlacesAsync(string cityOrDistrict, string keyword, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PlaceResult>>(Array.Empty<PlaceResult>());
}