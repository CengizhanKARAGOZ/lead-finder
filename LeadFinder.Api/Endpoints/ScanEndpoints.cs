using LeadFinder.Api.Services.Queue;
using Microsoft.AspNetCore.Mvc;

namespace LeadFinder.Api.Endpoints;

public static class ScanEndpoints
{
    public static void MapScanEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/scan", async (
                [FromBody] ScanRequest req,
                IBackgroundQueue queue,
                ILoggerFactory loggerFactory) =>
            {
                var log = loggerFactory.CreateLogger("ScanEndpoint");

                if (string.IsNullOrWhiteSpace(req.City) || string.IsNullOrWhiteSpace(req.Keyword))
                    return Results.BadRequest(new { message = "City and Keyword are required." });

                if (req.Urls is null)
                    req = req with { Urls = new List<string>() };

                await queue.QueueAsync(req);
                log.LogInformation("Queued scan: {City} / {Keyword} ({Count} urls)", req.City, req.Keyword,
                    req.Urls.Count);

                return Results.Accepted(
                    $"/results/search?city={Uri.EscapeDataString(req.City)}&keyword={Uri.EscapeDataString(req.Keyword)}",
                    new
                    {
                        status = "queued",
                        req.City,
                        req.Keyword,
                        urlCount = req.Urls.Count,
                        mode = req.Urls.Count == 0 ? "discovery" : "direct"
                    });
            })
            .WithTags("Scan");
    }
}