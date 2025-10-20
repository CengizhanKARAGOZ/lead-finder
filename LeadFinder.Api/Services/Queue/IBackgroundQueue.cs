namespace LeadFinder.Api.Services.Queue;

public interface IBackgroundQueue
{
    ValueTask QueueAsync(ScanRequest item);
    IAsyncEnumerable<ScanRequest> DequeueAllAsync(CancellationToken ct);
}