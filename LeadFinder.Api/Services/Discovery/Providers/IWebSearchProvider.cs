namespace LeadFinder.Api.Services.Discovery.Providers;

public interface IWebSearchProvider
{
    Task<IReadOnlyList<(string Title, string Url)>> SearchAsync(string cityOrDistrict, string keyword, CancellationToken ct = default);
}