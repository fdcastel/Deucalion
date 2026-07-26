using System.Net.ServerSentEvents;
using Deucalion.Api.Services;
using Xunit;

namespace Deucalion.Tests.Api;

public class MonitorEventBroadcasterTests
{
    private static SseItem<string> Item(string data) => new(data, "MonitorChecked");

    private static async Task<string> ReadOneAsync(System.Threading.Channels.ChannelReader<SseItem<string>> reader, CancellationToken cancellationToken)
    {
        var item = await reader.ReadAsync(cancellationToken);
        return item.Data;
    }

    [Fact]
    public async Task Broadcast_ReachesEverySubscriber()
    {
        using var broadcaster = new MonitorEventBroadcaster();
        var (readerA, _) = broadcaster.Subscribe();
        var (readerB, _) = broadcaster.Subscribe();

        broadcaster.Broadcast(Item("hello"));

        Assert.Equal("hello", await ReadOneAsync(readerA, TestContext.Current.CancellationToken));
        Assert.Equal("hello", await ReadOneAsync(readerB, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Broadcast_WithNoSubscribers_IsANoOp()
    {
        using var broadcaster = new MonitorEventBroadcaster();

        // Must not throw -- the engine broadcasts on every probe whether or not
        // a browser is connected.
        broadcaster.Broadcast(Item("nobody-listening"));
    }

    [Fact]
    public async Task Broadcast_PreservesOrderAndEventType()
    {
        using var broadcaster = new MonitorEventBroadcaster();
        var (reader, _) = broadcaster.Subscribe();

        broadcaster.Broadcast(new SseItem<string>("first", "MonitorChecked"));
        broadcaster.Broadcast(new SseItem<string>("second", "MonitorStateChanged"));

        var first = await reader.ReadAsync(TestContext.Current.CancellationToken);
        var second = await reader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("first", first.Data);
        Assert.Equal("MonitorChecked", first.EventType);
        Assert.Equal("second", second.Data);
        Assert.Equal("MonitorStateChanged", second.EventType);
    }

    [Fact]
    public async Task Unsubscribe_StopsDeliveryAndCompletesTheChannel()
    {
        using var broadcaster = new MonitorEventBroadcaster();
        var (readerA, writerA) = broadcaster.Subscribe();
        var (readerB, _) = broadcaster.Subscribe();

        broadcaster.Unsubscribe(writerA);
        broadcaster.Broadcast(Item("after-unsubscribe"));

        // A completes with nothing; B still receives.
        await Assert.ThrowsAsync<System.Threading.Channels.ChannelClosedException>(
            async () => await readerA.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("after-unsubscribe", await ReadOneAsync(readerB, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unsubscribe_LeavesAlreadyQueuedItemsReadable()
    {
        // A disconnecting client should still be able to drain what it was sent;
        // TryComplete finishes the channel rather than discarding its buffer.
        using var broadcaster = new MonitorEventBroadcaster();
        var (reader, writer) = broadcaster.Subscribe();

        broadcaster.Broadcast(Item("queued"));
        broadcaster.Unsubscribe(writer);

        Assert.Equal("queued", await ReadOneAsync(reader, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Unsubscribe_IsIdempotent()
    {
        // The SSE endpoint registers Unsubscribe on RequestAborted; a second call
        // after the stream already ended must not throw.
        using var broadcaster = new MonitorEventBroadcaster();
        var (_, writer) = broadcaster.Subscribe();

        broadcaster.Unsubscribe(writer);
        broadcaster.Unsubscribe(writer);
    }

    [Fact]
    public async Task Dispose_CompletesEverySubscriber()
    {
        var broadcaster = new MonitorEventBroadcaster();
        var (readerA, _) = broadcaster.Subscribe();
        var (readerB, _) = broadcaster.Subscribe();

        broadcaster.Dispose();

        await Assert.ThrowsAsync<System.Threading.Channels.ChannelClosedException>(
            async () => await readerA.ReadAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<System.Threading.Channels.ChannelClosedException>(
            async () => await readerB.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Broadcast_IsSafeWhileSubscribersComeAndGo()
    {
        // Subscribe/Unsubscribe run on request threads while the engine broadcasts
        // from its own loop, so the lock has to hold up under concurrency.
        using var broadcaster = new MonitorEventBroadcaster();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var churn = Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                var (_, writer) = broadcaster.Subscribe();
                broadcaster.Unsubscribe(writer);
            }
        }, cts.Token);

        var broadcasting = Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                broadcaster.Broadcast(Item($"event-{i}"));
            }
        }, cts.Token);

        await Task.WhenAll(churn, broadcasting);

        // A subscriber added afterwards still works.
        var (reader, _) = broadcaster.Subscribe();
        broadcaster.Broadcast(Item("still-alive"));
        Assert.Equal("still-alive", await ReadOneAsync(reader, cts.Token));
    }
}
