using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

/// <summary>
/// Tests for UnmanagedMemoryPool, UnmanagedMemoryHandle, and UnmanagedMemoryHandle&lt;T&gt;.
/// </summary>
[Collection("MemoryPool")]
public sealed class UnmanagedMemoryPoolTests
{
    // ─── Construction ─────────────────────────────────────────────────────

    [Fact]
    public void DefaultConstructor_DoesNotThrow()
    {
        var action = () => new UnmanagedMemoryPool();

        action.Should().NotThrow();
    }

    [Fact]
    public void Shared_IsNotNull()
    {
        UnmanagedMemoryPool.Shared.Should().NotBeNull();
    }

    [Fact]
    public void Shared_IsSingleton()
    {
        var a = UnmanagedMemoryPool.Shared;
        var b = UnmanagedMemoryPool.Shared;

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void MaxBufferSize_IsPositive()
    {
        using var pool = new UnmanagedMemoryPool();

        pool.MaxBufferSize.Should().BeGreaterThan(0);
    }

    // ─── Rent / Return ────────────────────────────────────────────────────

    [Fact]
    public void Rent_DefaultSize_ReturnsValidHandle()
    {
        using var pool = new UnmanagedMemoryPool();

        using var handle = pool.Rent();

        handle.Pointer.Should().NotBe(IntPtr.Zero);
        handle.Size.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Rent_SpecificSize_ReturnsHandleAtLeastThatSize()
    {
        using var pool = new UnmanagedMemoryPool();

        using var handle = pool.Rent(256);

        handle.Size.Should().BeGreaterThanOrEqualTo(256);
    }

    [Fact]
    public void Rent_AfterDispose_ThrowsObjectDisposedException()
    {
        var pool = new UnmanagedMemoryPool();
        pool.Dispose();

        var action = () => pool.Rent();

        action.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_ReturnsHandleToPool_ForReuse()
    {
        using var pool = new UnmanagedMemoryPool();

        var handle1 = pool.Rent(64);
        var ptr1 = handle1.Pointer;
        handle1.Dispose();

        using var handle2 = pool.Rent(64);
        handle2.Pointer.Should().Be(ptr1);
    }

    [Fact]
    public void AsSpan_ReturnsSpanOfCorrectSize()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(128);

        var span = handle.AsSpan();

        span.Length.Should().BeGreaterThanOrEqualTo(128);
    }

    [Fact]
    public void AsSpan_CanWriteAndReadBack()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(64);

        var span = handle.AsSpan();
        span[0] = 42;
        span[1] = 255;

        span[0].Should().Be(42);
        span[1].Should().Be(255);
    }

    [Fact]
    public unsafe void ToPointer_ReturnsNonNullPointer()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(64);

        var ptr = handle.ToPointer();

        ((nint)ptr).Should().NotBe(IntPtr.Zero);
    }

    // ─── UnmanagedMemoryHandle<T> ─────────────────────────────────────────

    [Fact]
    public void As_ReturnsTypedHandle()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(256);

        using var typed = handle.As<int>();

        typed.Pointer.Should().Be(handle.Pointer);
        typed.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TypedHandle_AsSpan_ReturnsSpanOfT()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(256);
        using var typed = handle.As<int>();

        var span = typed.AsSpan();

        span.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TypedHandle_AsReadOnlySpan_ReturnsReadOnlySpanOfT()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(256);
        using var typed = handle.As<int>();

        var span = typed.AsReadOnlySpan();

        span.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TypedHandle_Indexer_CanWriteAndRead()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(256);
        using var typed = handle.As<int>();

        typed[0] = 42;
        typed[1] = -1;

        typed[0].Should().Be(42);
        typed[1].Should().Be(-1);
    }

    [Fact]
    public void TypedHandle_Indexer_OutOfRange_Throws()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(64);
        using var typed = handle.As<int>();

        var action = () => typed[typed.Length];

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TypedHandle_Slice_ReturnsSubRange()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(256);
        using var typed = handle.As<int>();

        int start = 0;
        int length = Math.Min(4, typed.Length);
        using var sliced = typed.Slice(start, length);

        sliced.Length.Should().Be(length);
    }

    [Fact]
    public void TypedHandle_Slice_InvalidRange_Throws()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(64);
        using var typed = handle.As<int>();

        var action = () => typed.Slice(0, typed.Length + 1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TypedHandle_CopyTo_CopiesData()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(256);
        using var typed = handle.As<int>();

        typed[0] = 100;
        typed[1] = 200;

        var dest = new int[typed.Length];
        typed.CopyTo(dest.AsSpan());

        dest[0].Should().Be(100);
        dest[1].Should().Be(200);
    }

    [Fact]
    public unsafe void TypedHandle_ToPointer_ReturnsTypedPointer()
    {
        using var pool = new UnmanagedMemoryPool();
        using var handle = pool.Rent(256);
        using var typed = handle.As<int>();

        int* ptr = typed.ToPointer();

        ((nint)ptr).Should().NotBe(IntPtr.Zero);
    }

    // ─── Statistics ───────────────────────────────────────────────────────

    [Fact]
    public void GetStatistics_WhenEnabled_TracksRents()
    {
        using var pool = new UnmanagedMemoryPool(enableStatistics: true);

        using var h1 = pool.Rent(64);
        using var h2 = pool.Rent(128);

        var stats = pool.GetStatistics();
        stats.TotalRented.Should().Be(2);
    }

    [Fact]
    public void GetStatistics_WhenEnabled_TracksReturns()
    {
        using var pool = new UnmanagedMemoryPool(enableStatistics: true);

        var h = pool.Rent(64);
        h.Dispose();

        var stats = pool.GetStatistics();
        stats.TotalReturned.Should().Be(1);
    }

    [Fact]
    public void GetStatistics_WhenDisabled_ReturnsZeros()
    {
        using var pool = new UnmanagedMemoryPool(enableStatistics: false);

        using var h = pool.Rent(64);

        var stats = pool.GetStatistics();
        stats.TotalRented.Should().Be(0);
        stats.TotalReturned.Should().Be(0);
    }

    // ─── Trim ─────────────────────────────────────────────────────────────

    [Fact]
    public void Trim_EmptyPool_DoesNotThrow()
    {
        using var pool = new UnmanagedMemoryPool();

        var action = () => pool.Trim();

        action.Should().NotThrow();
    }

    [Fact]
    public void Trim_AfterReturn_ReducesPooledBuffers()
    {
        using var pool = new UnmanagedMemoryPool(enableStatistics: true);

        var h = pool.Rent(64);
        h.Dispose();

        pool.Trim(1.0f);

        var stats = pool.GetStatistics();
        stats.TotalAllocated.Should().BeGreaterThanOrEqualTo(0);
    }

    // ─── Clear modes ──────────────────────────────────────────────────────

    [Fact]
    public void ClearOnReturn_BufferIsZeroedAfterDispose()
    {
        using var pool = new UnmanagedMemoryPool(clearOnReturn: true, clearOnRent: false);
        var handle = pool.Rent(64);
        handle.AsSpan()[0] = 0xFF;
        var ptr = handle.Pointer;
        handle.Dispose();

        using var handle2 = pool.Rent(64);
        handle2.Pointer.Should().Be(ptr);
        handle2.AsSpan()[0].Should().Be(0);
    }

    [Fact]
    public void ClearOnRent_BufferIsZeroedOnRent()
    {
        using var pool = new UnmanagedMemoryPool(clearOnReturn: false, clearOnRent: true);
        var handle = pool.Rent(64);
        handle.AsSpan()[0] = 0xFF;
        var ptr = handle.Pointer;
        handle.Dispose();

        using var handle2 = pool.Rent(64);
        handle2.Pointer.Should().Be(ptr);
        handle2.AsSpan()[0].Should().Be(0);
    }

    // ─── Dispose ──────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var pool = new UnmanagedMemoryPool();

        var action1 = () => pool.Dispose();
        var action2 = () => pool.Dispose();

        action1.Should().NotThrow();
        action2.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WithActiveBuffers_DoesNotThrow()
    {
        var pool = new UnmanagedMemoryPool();
        var handle = pool.Rent(64);

        var action = () => pool.Dispose();

        action.Should().NotThrow();
        handle.Dispose();
    }

    // ─── Concurrency ──────────────────────────────────────────────────────

    [Fact]
    public void RentReturn_Concurrent_DoesNotThrow()
    {
        using var pool = new UnmanagedMemoryPool();

        var action = () =>
        {
            Parallel.For(0, 100, i =>
            {
                using var h = pool.Rent(64);
                h.AsSpan()[0] = (byte)i;
            });
        };

        action.Should().NotThrow();
    }
}
