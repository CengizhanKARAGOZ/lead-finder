using System.Threading.Channels;

namespace LeadFinder.Api.Services.Queue;

public class BackgroundQueue : IBackgroundQueue
{
    private readonly Channel<ScanRequest> _ch = Channel.CreateUnbounded<ScanRequest>();
    
    public ValueTask QueueAsync(ScanRequest item)  => _ch.Writer.WriteAsync(item);
    
    public async IAsyncEnumerable<ScanRequest> DequeueAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (await _ch.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while(_ch.Reader.TryRead(out var item))
                yield return item;
        }
    }
}