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
                return Results.BadRequest(new {message = "City and Keyword are required."});
            
            if (req.Urls is null || req.Urls.Count == 0)
                return Results.BadRequest(new { message = "At least one URL is required." });

            await queue.QueueAsync(req);
            log.LogInformation("Queued scan: {City} / {Keyword} ({Count} urls)", req.City, req.Keyword, req.Urls.Count);
            
            return Results.Accepted($"/results/{req.Keyword}", new
            {
                status = "queued",
                req.City,
                req.Keyword,
                urls = req.Urls
            });
        });
    }
}