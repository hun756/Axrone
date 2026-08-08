using System;
using System.Buffers;
using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

[Collection("MemoryPool")]
public sealed class CoreContractTests
{
    // ── Part 1: MemoryPoolFlags enum ──────────────────────────────────

    [Fact]
    public void MemoryPoolFlags_None_IsZero()
    {
        // Arrange & Act
        var flags = MemoryPoolFlags.None;

        // Assert
        ((int)flags).Should().Be(0);
    }

    [Fact]
    public void MemoryPoolFlags_CanCombine()
    {
        // Arrange & Act
        var combined = MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.UseTlsCache;

        // Assert
        combined.HasFlag(MemoryPoolFlags.EnableStatistics).Should().BeTrue();
        combined.HasFlag(MemoryPoolFlags.UseTlsCache).Should().BeTrue();
    }

    [Fact]
    public void MemoryPoolFlags_HasFlag_Works()
    {
        // Arrange
        var combined = MemoryPoolFlags.EnableStatistics
            | MemoryPoolFlags.EnableProfiling
            | MemoryPoolFlags.ZeroMemory;

        // Act & Assert
        combined.HasFlag(MemoryPoolFlags.EnableStatistics).Should().BeTrue();
        combined.HasFlag(MemoryPoolFlags.EnableProfiling).Should().BeTrue();
        combined.HasFlag(MemoryPoolFlags.ZeroMemory).Should().BeTrue();
        combined.HasFlag(MemoryPoolFlags.UseCompression).Should().BeFalse();
    }

    // ── Part 2: IMemoryPool<T> interface ──────────────────────────────

    [Fact]
    public void MemoryPool_ImplementsIMemoryPool()
    {
        // Arrange & Act
        using var pool = new MemoryPool<byte>();

        // Assert
        pool.Should().BeAssignableTo<IMemoryPool<byte>>();
        pool.Should().BeAssignableTo<IDisposable>();
    }

    [Fact]
    public void TryRentExact_ReturnsExactSize()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var result = pool.TryRentExact(256, out var owner);

        // Assert
        result.Should().BeTrue();
        owner.Should().NotBeNull();
        owner!.Length.Should().Be(256);
        owner.Dispose();
    }

    [Fact]
    public void TryRentExact_WhenDisposed_ReturnsFalse()
    {
        // Arrange
        var pool = new MemoryPool<byte>();
        pool.Dispose();

        // Act
        var result = pool.TryRentExact(256, out var owner);

        // Assert
        result.Should().BeFalse();
        owner.Should().BeNull();
    }

    [Fact]
    public void RentMultiple_ReturnsCorrectCount()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owners = pool.RentMultiple(5, 64);

        // Assert
        owners.Should().HaveCount(5);
        foreach (var owner in owners)
        {
            owner.Length.Should().BeGreaterThanOrEqualTo(64);
            owner.Dispose();
        }
    }

    [Fact]
    public void ReturnMultiple_ReturnsAllBuffers()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();
        var owners = pool.RentMultiple(3, 128);

        // Act
        var result = pool.ReturnMultiple(owners);

        // Assert
        result.Should().BeTrue();
        var metrics = pool.GetMetrics();
        metrics.PooledBuffers.Should().Be(3);
    }

    [Fact]
    public void TotalAllocated_IncreasesAfterRent()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();
        var before = pool.TotalAllocated;

        // Act
        using var owner = pool.Rent(512);

        // Assert
        pool.TotalAllocated.Should().BeGreaterThan(before);
        pool.TotalAllocated.Should().BeGreaterThan(0);
    }

    // ── Part 3: Extended IMemoryOwner<T> ──────────────────────────────

    [Fact]
    public void ReadOnlySpan_ReturnsSameDataAsSpan()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(128);

        // Act
        owner.Span[0] = 42;
        owner.Span[1] = 99;

        // Assert
        owner.ReadOnlySpan[0].Should().Be(42);
        owner.ReadOnlySpan[1].Should().Be(99);
    }

    [Fact]
    public void ReferenceCount_Initial_IsOne()
    {
        // Arrange & Act
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(64);

        // Assert
        owner.ReferenceCount.Should().Be(1);
    }

    [Fact]
    public void ReferenceCount_AfterSlice_IsTwo()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(128);

        // Act
        using var slice = owner.Slice(0, 64);

        // Assert
        owner.ReferenceCount.Should().Be(2);
    }

    [Fact]
    public void ReferenceCount_AfterSliceDispose_BackToOne()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(128);
        var slice = owner.Slice(0, 64);

        // Act
        slice.Dispose();

        // Assert
        owner.ReferenceCount.Should().Be(1);
    }

    [Fact]
    public void Pin_ReturnsValidHandle()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(64);

        // Act
        var handle = owner.Pin();

        // Assert
        handle.Should().NotBeNull();

        // Cleanup — unpin via Dispose should not throw
        var act = () => handle.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void TryGetArray_ReturnsTrueForManagedBuffer()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();
        using var owner = pool.Rent(256);

        // Act
        var result = owner.TryGetArray(out var segment);

        // Assert
        result.Should().BeTrue();
        segment.Array.Should().NotBeNull();
        segment.Count.Should().BeGreaterThanOrEqualTo(256);
    }
}
