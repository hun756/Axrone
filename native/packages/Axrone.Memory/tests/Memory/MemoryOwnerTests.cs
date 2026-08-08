using System;
using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

public sealed class MemoryOwnerTests
{
    [Fact]
    public void Slice_ReturnsCorrectSubRange()
    {
        // Arrange
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(100);

        for (var i = 0; i < 100; i++)
            owner.Span[i] = i;

        // Act
        using var slice = owner.Slice(10, 20);

        // Assert
        slice.Length.Should().Be(20);
        for (var i = 0; i < 20; i++)
            slice.Span[i].Should().Be(10 + i);
    }

    [Fact]
    public void Slice_WriteThrough_ReflectedInParent()
    {
        // Arrange
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(100);
        using var slice = owner.Slice(10, 20);

        // Act
        for (var i = 0; i < 20; i++)
            slice.Span[i] = 999 + i;

        // Assert
        for (var i = 0; i < 20; i++)
            owner.Span[10 + i].Should().Be(999 + i);
    }

    [Fact]
    public void Slice_Dispose_DoesNotReturnBufferToPool()
    {
        // Arrange
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(100);
        var slice = owner.Slice(10, 20);

        // Act
        slice.Dispose();

        // Assert — parent remains fully usable (Length may exceed requested size due to bucket rounding)
        owner.Length.Should().BeGreaterThanOrEqualTo(100);
        owner.Span[0] = 42;
        owner.Span[0].Should().Be(42);
    }

    [Fact]
    public void Slice_AfterParentDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        using var pool = new MemoryPool<int>();
        var owner = pool.Rent(100);
        var slice = owner.Slice(10, 20);

        // Act
        owner.Dispose();

        // Assert
        var act = () => slice.Memory;
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void NestedSlice_OffsetsAccumulate()
    {
        // Arrange
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(100);

        for (var i = 0; i < 100; i++)
            owner.Span[i] = i;

        // Act — first slice starts at offset 10, second slice starts 5 into that (absolute offset 15)
        using var slice1 = owner.Slice(10, 50);
        using var slice2 = slice1.Slice(5, 10);

        // Assert
        slice2.Length.Should().Be(10);
        for (var i = 0; i < 10; i++)
            slice2.Span[i].Should().Be(15 + i);
    }

    [Fact]
    public void Slice_InvalidBounds_ThrowsArgumentOutOfRange()
    {
        using var pool = new MemoryPool<int>();
        using var owner = pool.Rent(100);

        var negativeStart = () => owner.Slice(-1, 10);
        negativeStart.Should().Throw<ArgumentOutOfRangeException>();

        // Bucket rounding makes Length >= 100; use bounds that exceed the actual buffer length
        var lengthExceedingBounds = () => owner.Slice(owner.Length - 5, 20);
        lengthExceedingBounds.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Dispose_Idempotent()
    {
        // Arrange
        using var pool = new MemoryPool<int>();
        var owner = pool.Rent(100);

        // Act
        owner.Dispose();

        // Assert — second dispose must not throw
        var act = () => owner.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void AccessAfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        using var pool = new MemoryPool<int>();
        var owner = pool.Rent(100);
        owner.Dispose();

        // Assert
        var accessMemory = () => owner.Memory;
        accessMemory.Should().Throw<ObjectDisposedException>();

        var accessSpan = () => { _ = owner.Span[0]; };
        accessSpan.Should().Throw<ObjectDisposedException>();

        var accessReadOnlyMemory = () => owner.ReadOnlyMemory;
        accessReadOnlyMemory.Should().Throw<ObjectDisposedException>();
    }
}
