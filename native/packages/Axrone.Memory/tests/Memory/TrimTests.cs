using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

/// <summary>
/// Tests for MemoryPool&lt;T&gt; trim functionality (TDD red phase).
/// These tests define the expected Trim() and Trim(double pressure) API.
/// </summary>
[Collection("MemoryPool")]
public sealed class TrimTests
{
    [Fact]
    public void Trim_EmptyPool_DoesNotThrow()
    {
        using var pool = new MemoryPool<byte>();

        var action = () => pool.Trim();

        action.Should().NotThrow();
    }

    [Fact]
    public void Trim_AfterReturn_RemovesPooledBuffers()
    {
        using var pool = new MemoryPool<byte>();
        var buffer = pool.Rent(1024);
        buffer.Dispose();

        pool.Trim(1.0);

        pool.GetMetrics().PooledBuffers.Should().Be(0);
    }

    [Fact]
    public void Trim_DoesNotAffectActiveBuffers()
    {
        using var pool = new MemoryPool<byte>();
        var buffer1 = pool.Rent(1024);
        var buffer2 = pool.Rent(1024);
        var buffer3 = pool.Rent(1024);
        buffer1.Dispose();

        pool.Trim(1.0);

        pool.GetMetrics().ActiveBuffers.Should().Be(2);
        pool.GetMetrics().PooledBuffers.Should().Be(0);
    }

    [Fact]
    public void Trim_WithPressure_Half_RemovesApproxHalf()
    {
        using var pool = new MemoryPool<byte>();
        var buffers = new IMemoryOwner<byte>[10];

        for (int i = 0; i < 10; i++)
            buffers[i] = pool.Rent(1024);

        for (int i = 0; i < 10; i++)
            buffers[i].Dispose();

        pool.Trim(0.5);

        pool.GetMetrics().PooledBuffers.Should().BeInRange(3, 7);
    }

    [Fact]
    public void Trim_WithPressure_Zero_RemovesNone()
    {
        using var pool = new MemoryPool<byte>();
        var buffers = new IMemoryOwner<byte>[5];

        for (int i = 0; i < 5; i++)
            buffers[i] = pool.Rent(1024);

        for (int i = 0; i < 5; i++)
            buffers[i].Dispose();

        pool.Trim(0.0);

        pool.GetMetrics().PooledBuffers.Should().Be(5);
    }

    [Fact]
    public void Trim_WithPressure_One_RemovesAll()
    {
        using var pool = new MemoryPool<byte>();
        var buffers = new IMemoryOwner<byte>[5];

        for (int i = 0; i < 5; i++)
            buffers[i] = pool.Rent(1024);

        for (int i = 0; i < 5; i++)
            buffers[i].Dispose();

        pool.Trim(1.0);

        pool.GetMetrics().PooledBuffers.Should().Be(0);
    }

    [Fact]
    public void Trim_AfterDispose_ThrowsObjectDisposedException()
    {
        var pool = new MemoryPool<byte>();
        pool.Dispose();

        var action = () => pool.Trim();

        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Trim_Idempotent()
    {
        using var pool = new MemoryPool<byte>();
        var buffer = pool.Rent(1024);
        buffer.Dispose();

        var action1 = () => pool.Trim();
        var action2 = () => pool.Trim();

        action1.Should().NotThrow();
        action2.Should().NotThrow();
    }
}
