using Axrone.Memory.Arena;
using Axrone.Utility.Backoff.SpinPolicies;

namespace Axrone.Memory.Tests;

public sealed class RingStreamerTests : IDisposable
{
    private readonly ArenaMemoryRing<int, AdaptiveSpinBackoff> _ring =
        new(new ArenaCapacity(16));

    public void Dispose()
    {
        _ring.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task WaitToReadAsync_True_WhenDataAvailable()
    {
        _ring.Producer.TryWrite(1).Should().BeTrue();
        (await _ring.Streamer.WaitToReadAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task WaitToReadAsync_False_WhenCompletedEmpty()
    {
        _ring.Admin.Complete();
        (await _ring.Streamer.WaitToReadAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task WaitToWriteAsync_True_WhenSpaceAvailable()
    {
        (await _ring.Streamer.WaitToWriteAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task WaitToWriteAsync_False_WhenCompleted()
    {
        _ring.Admin.Complete();
        (await _ring.Streamer.WaitToWriteAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ReadAllAsync_Streams_UntilCompleted()
    {
        _ring.Producer.TryWrite(1);
        _ring.Producer.TryWrite(2);
        _ring.Producer.TryWrite(3);
        _ring.Admin.Complete();

        var seen = new List<int>();
        await foreach (int item in _ring.Streamer.ReadAllAsync())
        {
            seen.Add(item);
        }

        seen.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ProducerConsumer_Async_RoundTrip()
    {
        Task writer = Task.Run(async () =>
        {
            await _ring.Producer.WriteAsync(42);
        });
        int value = await _ring.Consumer.ReadAsync();
        await writer;
        value.Should().Be(42);
    }
}
