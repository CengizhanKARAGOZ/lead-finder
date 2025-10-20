namespace LeadFinder.Api.Services.Queue;

public sealed record ScanRequest(string City, string Keyword, List<string>? Urls);
