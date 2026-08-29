using System.Net.ServerSentEvents;
using System.Threading.Channels;
using Deucalion.Api.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Deucalion.Tests.Api;

public class MonitorEventBroadcasterTests
{
    private static SseItem<string> Item(string data) => new(data, "MonitorChecked");

    private static string Frame(string data) => $"event: MonitorChecked\ndata: {data}\n\n";

    private static MonitorEventBroadcaster Create(TimeProvider? timeProvider = null) =>
        new(timeProvider ?? TimeProvider.System);

    [Fact]
    public async Task Broadcast_ReachesEverySubscriber()
    {
        using var broadcaster = Create();
        var (readerA, _) = broadcaster.Subscribe();
        var (readerB, _) = broadcaster.Subscribe();

        broadcaster.Broadcast(Item("hello"));

        Assert.Equal(Frame("hello"), await readerA.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Frame("hello"), await readerB.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Broadcast_WithNoSubscribers_IsANoOp()
    {
        using var broadcaster = Create();

        // Must not throw -- the engine broadcasts on every probe whether or not
        // a browser is connected.
        broadcaster.Broadcast(Item("nobody-listening"));
    }

    [Fact]
    public async Task Broadcast_PreservesOrderAndRendersEventType()
    {
        using var broadcaster = Create();
        var (reader, _) = broadcaster.Subscribe();

        broadcaster.Broadcast(new SseItem<string>("first", "MonitorChecked"));
        broadcaster.Broadcast(new SseItem<string>("second", "MonitorStateChanged"));
        broadcaster.Broadcast(new SseItem<string>("third"));

        Assert.Equal("event: MonitorChecked\ndata: first\n\n", await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal("event: MonitorStateChanged\ndata: second\n\n", await reader.ReadAsync(TestContext.Current.CancellationToken));
        // SseItem defaults the event type to "message" (the EventSource default).
        Assert.Equal("event: message\ndata: third\n\n", await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Broadcast_WhenSubscriberIsFull_DropsOldestAndNeverBlocks()
    {
        // Regression for #18: the per-subscriber channel was unbounded, so a wedged client
        // accumulated every event forever. With a bounded DropOldest channel the publisher
        // still returns immediately and the reader sees only the newest frames.
        using var broadcaster = Create();
        var (reader, _) = broadcaster.Subscribe();

        const int overflow = 10;
        var total = MonitorEventBroadcaster.ChannelCapacity + overflow;

        // Synchronous by construction: were the channel in Wait mode this would either
        // hang or, with TryWrite, silently drop the *newest* frames instead.
        for (var i = 0; i < total; i++)
            broadcaster.Broadcast(Item($"event-{i}"));

        Assert.Equal(MonitorEventBroadcaster.ChannelCapacity, reader.Count);

        var first = await reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Frame($"event-{overflow}"), first);

        string last = first;
        while (reader.TryRead(out var frame))
            last = frame;
        Assert.Equal(Frame($"event-{total - 1}"), last);
    }

    [Fact]
    public async Task KeepAlive_IsBroadcastOnTheClockWithNoEvents()
    {
        // Regression for #18: an idle stream never carried any bytes after ": connected",
        // so proxies dropped it. The keep-alive is driven by the injected clock.
        var time = new FakeTimeProvider();
        using var broadcaster = Create(time);
        var (reader, _) = broadcaster.Subscribe();

        time.Advance(MonitorEventBroadcaster.KeepAliveInterval - TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, reader.Count);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(MonitorEventBroadcaster.KeepAliveFrame, await reader.ReadAsync(TestContext.Current.CancellationToken));

        time.Advance(MonitorEventBroadcaster.KeepAliveInterval);
        Assert.Equal(MonitorEventBroadcaster.KeepAliveFrame, await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void KeepAlive_StopsAfterDispose()
    {
        var time = new FakeTimeProvider();
        var broadcaster = Create(time);
        var (reader, _) = broadcaster.Subscribe();

        broadcaster.Dispose();
        time.Advance(MonitorEventBroadcaster.KeepAliveInterval * 3);

        // Channel is complete and holds nothing: no timer fired after Dispose.
        Assert.True(reader.Completion.IsCompleted);
        Assert.Equal(0, reader.Count);
    }

    [Fact]
    public void SubscriberCount_TracksSubscribeAndUnsubscribe()
    {
        using var broadcaster = Create();
        var changes = 0;
        broadcaster.SubscriptionsChanged += () => changes++;

        Assert.Equal(0, broadcaster.SubscriberCount);

        var (_, writerA) = broadcaster.Subscribe();
        var (_, writerB) = broadcaster.Subscribe();
        Assert.Equal(2, broadcaster.SubscriberCount);

        broadcaster.Unsubscribe(writerA);
        Assert.Equal(1, broadcaster.SubscriberCount);

        broadcaster.Unsubscribe(writerB);
        Assert.Equal(0, broadcaster.SubscriberCount);
        Assert.Equal(4, changes);
    }

    [Fact]
    public async Task Unsubscribe_StopsDeliveryAndCompletesTheChannel()
    {
        using var broadcaster = Create();
        var (readerA, writerA) = broadcaster.Subscribe();
        var (readerB, _) = broadcaster.Subscribe();

        broadcaster.Unsubscribe(writerA);
        broadcaster.Broadcast(Item("after-unsubscribe"));

        // A completes with nothing; B still receives.
        await Assert.ThrowsAsync<ChannelClosedException>(
            async () => await readerA.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Frame("after-unsubscribe"), await readerB.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unsubscribe_LeavesAlreadyQueuedItemsReadable()
    {
        // A disconnecting client should still be able to drain what it was sent;
        // TryComplete finishes the channel rather than discarding its buffer.
        using var broadcaster = Create();
        var (reader, writer) = broadcaster.Subscribe();

        broadcaster.Broadcast(Item("queued"));
        broadcaster.Unsubscribe(writer);

        Assert.Equal(Frame("queued"), await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Unsubscribe_IsIdempotent()
    {
        // The SSE endpoint unsubscribes in a finally block; a second call after the
        // stream already ended must not throw.
        using var broadcaster = Create();
        var (_, writer) = broadcaster.Subscribe();

        broadcaster.Unsubscribe(writer);
        broadcaster.Unsubscribe(writer);
        Assert.Equal(0, broadcaster.SubscriberCount);
    }

    [Fact]
    public async Task Dispose_CompletesEverySubscriber()
    {
        var broadcaster = Create();
        var (readerA, _) = broadcaster.Subscribe();
        var (readerB, _) = broadcaster.Subscribe();

        broadcaster.Dispose();

        await Assert.ThrowsAsync<ChannelClosedException>(
            async () => await readerA.ReadAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ChannelClosedException>(
            async () => await readerB.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Broadcast_IsSafeWhileSubscribersComeAndGo()
    {
        // Subscribe/Unsubscribe run on request threads while the engine broadcasts
        // from its own loop, so the lock has to hold up under concurrency.
        using var broadcaster = Create();
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
        Assert.Equal(Frame("still-alive"), await reader.ReadAsync(cts.Token));
    }
}
