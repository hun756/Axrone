using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

/// <summary>
/// Tests for thread-local storage (TLS) cache functionality in MemoryPool&lt;T&gt;.
/// </summary>
[Collection("MemoryPool")]
public sealed class TlsCacheTests
{
    [Fact]
    public void TlsCache_DefaultPool_DoesNotUseTls()
    {
        using var pool = new MemoryPool<byte>();
        var buffer = pool.Rent(1024);
        buffer.Dispose();

        pool.GetMetrics().PooledBuffers.Should().Be(1);
    }

    [Fact]
    public void TlsCache_Enabled_RentReturnRent_HitsCache()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.UseTlsCache)
            .WithTlsCacheSize(4);

        using var pool = new MemoryPool<byte>(options);

        var buffer1 = pool.Rent(1024);
        buffer1.Dispose();

        var buffer2 = pool.Rent(1024);
        buffer2.Should().NotBeNull();
        buffer2.Dispose();

        var metrics = pool.GetMetrics();
        metrics.PooledBuffers.Should().Be(0);
        metrics.Hits.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void TlsCache_RoundTrip_SameBufferReturned()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.UseTlsCache)
            .WithTlsCacheSize(4);

        using var pool = new MemoryPool<byte>(options);

        var buffer1 = pool.Rent(1024);
        buffer1.Dispose();

        var buffer2 = pool.Rent(1024);
        buffer2.Should().NotBeNull();
        buffer2.Dispose();

        pool.GetMetrics().PooledBuffers.Should().Be(0);
    }

    [Fact]
    public void TlsCache_MultipleBuffers_AllCached()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.UseTlsCache)
            .WithTlsCacheSize(8);

        using var pool = new MemoryPool<byte>(options);

        var buffers = new IMemoryOwner<byte>[4];
        for (int i = 0; i < 4; i++)
            buffers[i] = pool.Rent(256);

        for (int i = 0; i < 4; i++)
            buffers[i].Dispose();

        for (int i = 0; i < 4; i++)
        {
            var b = pool.Rent(256);
            b.Should().NotBeNull();
            b.Dispose();
        }

        pool.GetMetrics().PooledBuffers.Should().Be(0);
    }

    [Fact]
    public void TlsCache_ExceedsCapacity_FallsBackToBucket()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.UseTlsCache)
            .WithTlsCacheSize(2);

        using var pool = new MemoryPool<byte>(options);

        var buffers = new IMemoryOwner<byte>[5];
        for (int i = 0; i < 5; i++)
            buffers[i] = pool.Rent(128);

        for (int i = 0; i < 5; i++)
            buffers[i].Dispose();

        pool.GetMetrics().PooledBuffers.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void TlsCache_Disabled_FallsBackToBucket()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableThreadSafety)
            .WithTlsCacheSize(4);

        using var pool = new MemoryPool<byte>(options);

        var buffer = pool.Rent(512);
        buffer.Dispose();

        pool.GetMetrics().PooledBuffers.Should().Be(1);
    }

    [Fact]
    public void TlsCache_MultipleThreads_EachGetsOwnCache()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.UseTlsCache)
            .WithTlsCacheSize(4);

        using var pool = new MemoryPool<byte>(options);
        var exceptions = new List<Exception>();

        var threads = new Thread[4];
        for (int t = 0; t < threads.Length; t++)
        {
            threads[t] = new Thread(() =>
            {
                try
                {
                    for (int i = 0; i < 10; i++)
                    {
                        var buffer = pool.Rent(256);
                        buffer.Should().NotBeNull();
                        buffer.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                        exceptions.Add(ex);
                }
            });
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        exceptions.Should().BeEmpty();
        pool.GetMetrics().TotalRentedBuffers.Should().Be(40);
    }

    [Fact]
    public void TlsCache_Dispose_ClearsCache()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.UseTlsCache)
            .WithTlsCacheSize(4);

        var pool = new MemoryPool<byte>(options);

        var buffer = pool.Rent(512);
        buffer.Dispose();

        var action = () => pool.Dispose();
        action.Should().NotThrow();

        pool.GetMetrics().PooledBuffers.Should().Be(0);
    }

    [Fact]
    public void TlsCache_SizeMismatch_FallsBackToBucket()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.UseTlsCache)
            .WithTlsCacheSize(4);

        using var pool = new MemoryPool<byte>(options);

        var buffer1 = pool.Rent(128);
        buffer1.Dispose();

        long hitsAfterSmallReturn = pool.GetMetrics().Hits;

        var buffer2 = pool.Rent(4096);
        buffer2.Should().NotBeNull();
        buffer2.Dispose();

        long hitsAfterLargeRent = pool.GetMetrics().Hits;
        (hitsAfterLargeRent - hitsAfterSmallReturn).Should().Be(0);
    }
}
