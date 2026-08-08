using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

/// <summary>
/// Tests for the expected diagnostics contract on MemoryPool&lt;T&gt;.
/// These tests define the API that GetMetrics() and ResetStatistics() should satisfy.
/// They will not compile until those methods are implemented (TDD red phase).
/// </summary>
[Collection("MemoryPool")]
public sealed class DiagnosticsTests
{
    [Fact]
    public void GetMetrics_InitialPool_AllZero()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var metrics = pool.GetMetrics();

        // Assert
        metrics.TotalRentedBuffers.Should().Be(0);
        metrics.TotalReturnedBuffers.Should().Be(0);
        metrics.ActiveBuffers.Should().Be(0);
        metrics.PooledBuffers.Should().Be(0);
        metrics.Hits.Should().Be(0);
        metrics.Misses.Should().Be(0);
    }

    [Fact]
    public void GetMetrics_AfterRent_ActiveBuffersIsOne()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner = pool.Rent(64);
        var metrics = pool.GetMetrics();

        // Assert
        metrics.ActiveBuffers.Should().Be(1);
        metrics.TotalRentedBuffers.Should().Be(1);

        // Cleanup
        owner.Dispose();
    }

    [Fact]
    public void GetMetrics_AfterRentAndReturn_PooledBuffersIsOne()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner = pool.Rent(64);
        owner.Dispose();
        var metrics = pool.GetMetrics();

        // Assert
        metrics.PooledBuffers.Should().Be(1);
        metrics.ActiveBuffers.Should().Be(0);
    }

    [Fact]
    public void GetMetrics_MultipleRents_TracksCorrectly()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();
        var owners = new IMemoryOwner<byte>[5];

        // Act
        for (int i = 0; i < 5; i++)
        {
            owners[i] = pool.Rent(64);
        }

        var metrics = pool.GetMetrics();

        // Assert
        metrics.ActiveBuffers.Should().Be(5);
        metrics.TotalRentedBuffers.Should().Be(5);

        // Cleanup
        foreach (var owner in owners)
        {
            owner.Dispose();
        }
    }

    [Fact]
    public void ResetStatistics_ClearsAllCounters()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();
        var owner = pool.Rent(64);
        owner.Dispose();

        // Act
        pool.ResetStatistics();
        var metrics = pool.GetMetrics();

        // Assert — throughput counters are reset
        metrics.TotalRentedBuffers.Should().Be(0);
        metrics.TotalReturnedBuffers.Should().Be(0);
        metrics.Hits.Should().Be(0);
        metrics.Misses.Should().Be(0);

        // State counters are preserved (not reset) to prevent negative counter corruption
        metrics.ActiveBuffers.Should().Be(0); // No active buffers after Dispose
        metrics.PooledBuffers.Should().Be(1); // Buffer returned to pool, preserved by ResetStatistics
    }

    [Fact]
    public void GetMetrics_HitMiss_AfterPoolReuse()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act - Rent and return a buffer to populate the pool
        var owner1 = pool.Rent(64);
        owner1.Dispose();

        // Rent the same size again - should be a pool hit
        var owner2 = pool.Rent(64);
        var metrics = pool.GetMetrics();

        // Assert
        metrics.Hits.Should().Be(1);
        metrics.Misses.Should().Be(1); // First rent was a miss

        // Cleanup
        owner2.Dispose();
    }
}
