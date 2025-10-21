namespace LeadFinder.Api.Services.Discovery.Providers;

public sealed class DummyWebSearchProvider : IWebSearchProvider
{
   public Task<IReadOnlyList<(string Title, string Url)>> SearchAsync(string cityOrDistrict, string keyword, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<(string, string)>>(Array.Empty<(string, string)>());
}