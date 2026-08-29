using System.Net.ServerSentEvents;
using System.Threading.Channels;

namespace Deucalion.Api.Services;

/// <summary>
/// Fans monitor events out to every connected SSE client as pre-rendered
/// <c>text/event-stream</c> frames, and emits a keep-alive comment on a fixed
/// cadence so idle streams are not dropped by proxies.
/// </summary>
internal sealed class MonitorEventBroadcaster : IDisposable
{
    /// <summary>
    /// Frames buffered per subscriber before the oldest is dropped. This is a live-status
    /// stream: a client that cannot keep up is better served by the newest frames than by
    /// an ever-growing backlog, and the publisher must never block on a wedged socket.
    /// </summary>
    internal const int ChannelCapacity = 128;

    /// <summary>
    /// Cadence of the <c>: keep-alive</c> comment. Below the 30-60 s idle timeouts of
    /// common reverse proxies, and cheap enough that it is sent regardless of traffic.
    /// </summary>
    internal static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    internal const string KeepAliveFrame = ": keep-alive\n\n";

    private readonly HashSet<ChannelWriter<string>> _writers = [];
    private readonly Lock _lock = new();
    private readonly ITimer _keepAliveTimer;

    public MonitorEventBroadcaster(TimeProvider timeProvider)
    {
        _keepAliveTimer = timeProvider.CreateTimer(
            static state => ((MonitorEventBroadcaster)state!).BroadcastFrame(KeepAliveFrame),
            this,
            KeepAliveInterval,
            KeepAliveInterval);
    }

    /// <summary>Number of live subscriptions. Exposed for tests.</summary>
    internal int SubscriberCount
    {
        get
        {
            lock (_lock)
                return _writers.Count;
        }
    }

    /// <summary>Raised (outside the lock) after every Subscribe/Unsubscribe. Exposed for tests.</summary>
    internal event Action? SubscriptionsChanged;

    public (ChannelReader<string> Reader, ChannelWriter<string> Writer) Subscribe()
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        lock (_lock)
            _writers.Add(channel.Writer);

        SubscriptionsChanged?.Invoke();
        return (channel.Reader, channel.Writer);
    }

    public void Unsubscribe(ChannelWriter<string> writer)
    {
        lock (_lock)
            _writers.Remove(writer);
        writer.TryComplete();

        SubscriptionsChanged?.Invoke();
    }

    public void Broadcast(SseItem<string> item) => BroadcastFrame(Render(item));

    /// <summary>
    /// Renders one event as a <c>text/event-stream</c> block. <see cref="SseItem{T}.EventType"/>
    /// falls back to <c>message</c> on its own, so there is always an <c>event:</c> line.
    /// </summary>
    internal static string Render(SseItem<string> item) =>
        $"event: {item.EventType}\ndata: {item.Data}\n\n";

    private void BroadcastFrame(string frame)
    {
        lock (_lock)
        {
            // TryWrite never blocks and, with DropOldest, never fails on a full channel.
            foreach (var writer in _writers)
                writer.TryWrite(frame);
        }
    }

    public void Dispose()
    {
        _keepAliveTimer.Dispose();
        lock (_lock)
        {
            foreach (var writer in _writers)
                writer.TryComplete();
            _writers.Clear();
        }
    }
}
