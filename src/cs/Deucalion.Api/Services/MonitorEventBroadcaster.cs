using System.Net.ServerSentEvents;
using System.Threading.Channels;

namespace Deucalion.Api.Services;

internal sealed class MonitorEventBroadcaster : IDisposable
{
    private readonly HashSet<ChannelWriter<SseItem<string>>> _writers = [];
    private readonly Lock _lock = new();

    public (ChannelReader<SseItem<string>> Reader, ChannelWriter<SseItem<string>> Writer) Subscribe()
    {
        var channel = Channel.CreateUnbounded<SseItem<string>>();
        lock (_lock)
            _writers.Add(channel.Writer);
        return (channel.Reader, channel.Writer);
    }

    public void Unsubscribe(ChannelWriter<SseItem<string>> writer)
    {
        lock (_lock)
            _writers.Remove(writer);
        writer.TryComplete();
    }

    public void Broadcast(SseItem<string> item)
    {
        lock (_lock)
        {
            foreach (var writer in _writers)
                writer.TryWrite(item);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var writer in _writers)
                writer.TryComplete();
            _writers.Clear();
        }
    }
}
