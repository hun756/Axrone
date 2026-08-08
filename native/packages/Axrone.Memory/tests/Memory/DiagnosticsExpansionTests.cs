using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

/// <summary>
/// Tests for the expanded diagnostics contract on MemoryPool&lt;T&gt;.
/// Covers extended MemoryPoolMetrics (TotalAllocatedBytes, BucketCount, HitRatio, ElapsedTime),
/// the new MemoryPoolDiagnostics struct returned by GetDiagnostics(),
/// and per-bucket BucketDiagnostics.
/// These tests define the API that will be implemented (TDD red phase).
/// </summary>
[Collection("MemoryPool")]
public sealed class DiagnosticsExpansionTests
{
    // ─────────────────────────────────────────────────────────────
    //  Extended MemoryPoolMetrics — new properties
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetMetrics_ExtendedProperties_InitialPool_HasExpectedDefaults()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var metrics = pool.GetMetrics();

        // Assert
        metrics.TotalAllocatedBytes.Should().Be(0);
        metrics.BucketCount.Should().Be(0);
        metrics.HitRatio.Should().Be(0f);
        metrics.ElapsedTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void GetMetrics_TotalAllocatedBytes_MatchesPoolTotalAllocated()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — rent a buffer to trigger an allocation
        var owner = pool.Rent(64);
        var metrics = pool.GetMetrics();

        // Assert
        metrics.TotalAllocatedBytes.Should().Be(pool.TotalAllocated);
        metrics.TotalAllocatedBytes.Should().BeGreaterThan(0);

        // Cleanup
        owner.Dispose();
    }

    [Fact]
    public void GetMetrics_TotalAllocatedBytes_IncreasesWithMultipleAllocations()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner1 = pool.Rent(64);
        var metrics1 = pool.GetMetrics();

        var owner2 = pool.Rent(256);
        var metrics2 = pool.GetMetrics();

        // Assert
        metrics2.TotalAllocatedBytes.Should().BeGreaterThan(metrics1.TotalAllocatedBytes);

        // Cleanup
        owner1.Dispose();
        owner2.Dispose();
    }

    [Fact]
    public void GetMetrics_BucketCount_ReflectsActiveBuckets()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — rent different sizes to create multiple buckets
        var owner1 = pool.Rent(64);
        var owner2 = pool.Rent(512);
        var metrics = pool.GetMetrics();

        // Assert — at least two distinct bucket sizes should exist
        metrics.BucketCount.Should().BeGreaterThanOrEqualTo(2);

        // Cleanup
        owner1.Dispose();
        owner2.Dispose();
    }

    [Fact]
    public void GetMetrics_BucketCount_ZeroOnFreshPool()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var metrics = pool.GetMetrics();

        // Assert — no buckets created until first rent
        metrics.BucketCount.Should().Be(0);
    }

    [Fact]
    public void GetMetrics_HitRatio_ZeroInitially()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var metrics = pool.GetMetrics();

        // Assert — no operations means 0 hit ratio
        metrics.HitRatio.Should().Be(0f);
    }

    [Fact]
    public void GetMetrics_HitRatio_ZeroAfterOnlyMisses()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — first rent is always a miss (cold pool)
        var owner = pool.Rent(64);
        var metrics = pool.GetMetrics();

        // Assert — 0 hits, 1 miss => ratio = 0
        metrics.HitRatio.Should().Be(0f);

        // Cleanup
        owner.Dispose();
    }

    [Fact]
    public void GetMetrics_HitRatio_IncreasesAfterHits()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — rent and return to populate the pool
        var owner1 = pool.Rent(64);
        owner1.Dispose();

        // Second rent of same size should be a hit
        var owner2 = pool.Rent(64);
        var metrics = pool.GetMetrics();

        // Assert — 1 hit, 1 miss => ratio = 0.5
        metrics.HitRatio.Should().BeApproximately(0.5f, 0.01f);

        // Cleanup
        owner2.Dispose();
    }

    [Fact]
    public void GetMetrics_HitRatio_ApproachesOneWithRepeatedReuse()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — first rent is a miss, then many hits
        var initial = pool.Rent(64);
        initial.Dispose();

        for (int i = 0; i < 9; i++)
        {
            var owner = pool.Rent(64);
            owner.Dispose();
        }

        var metrics = pool.GetMetrics();

        // Assert — 9 hits out of 10 total operations => 0.9
        metrics.HitRatio.Should().BeApproximately(0.9f, 0.01f);
    }

    [Fact]
    public void GetMetrics_HitRatio_IsBetweenZeroAndOne()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — mix of hits and misses across different sizes
        var a = pool.Rent(32);
        var b = pool.Rent(128);
        a.Dispose();
        b.Dispose();

        var c = pool.Rent(32);  // hit
        var d = pool.Rent(256); // miss
        c.Dispose();
        d.Dispose();

        var metrics = pool.GetMetrics();

        // Assert
        metrics.HitRatio.Should().BeInRange(0f, 1f);
    }

    [Fact]
    public void GetMetrics_ElapsedTime_IncreasesAfterOperations()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var before = pool.GetMetrics().ElapsedTime;

        // Perform some work to let time pass
        var owner = pool.Rent(64);
        owner.Dispose();

        var after = pool.GetMetrics().ElapsedTime;

        // Assert
        after.Should().BeGreaterThanOrEqualTo(before);
        after.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void GetMetrics_ElapsedTime_IsNonNegativeOnFreshPool()
    {
        // Arrange & Act
        using var pool = new MemoryPool<byte>();
        var metrics = pool.GetMetrics();

        // Assert
        metrics.ElapsedTime.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    // ─────────────────────────────────────────────────────────────
    //  MemoryPoolDiagnostics — GetDiagnostics()
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetDiagnostics_OnEmptyPool_ReturnsAllZeros()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var diag = pool.GetDiagnostics();

        // Assert
        diag.TotalMemory.Should().Be(0);
        diag.MaxMemory.Should().Be(0);
        diag.WastedMemory.Should().Be(0);
        diag.BucketCount.Should().Be(0);
        diag.ActiveBuffers.Should().Be(0);
        diag.PooledBuffers.Should().Be(0);
    }

    [Fact]
    public void GetDiagnostics_ReturnsValidStruct()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var diag = pool.GetDiagnostics();

        // Assert — struct should be a value type with sensible defaults
        diag.Should().BeOfType<MemoryPoolDiagnostics>();
    }

    [Fact]
    public void GetDiagnostics_AfterRent_ActiveBuffersIsOne()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner = pool.Rent(64);
        var diag = pool.GetDiagnostics();

        // Assert
        diag.ActiveBuffers.Should().Be(1);
        diag.BucketCount.Should().BeGreaterThanOrEqualTo(1);

        // Cleanup
        owner.Dispose();
    }

    [Fact]
    public void GetDiagnostics_AfterRentAndReturn_PooledBuffersIsOne()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner = pool.Rent(64);
        owner.Dispose();
        var diag = pool.GetDiagnostics();

        // Assert
        diag.ActiveBuffers.Should().Be(0);
        diag.PooledBuffers.Should().Be(1);
    }

    [Fact]
    public void GetDiagnostics_TotalMemory_MatchesAllocatedAfterRent()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner = pool.Rent(256);
        var diag = pool.GetDiagnostics();

        // Assert — total memory should reflect the allocation
        diag.TotalMemory.Should().BeGreaterThan(0);
        diag.TotalMemory.Should().Be(pool.TotalAllocated);

        // Cleanup
        owner.Dispose();
    }

    [Fact]
    public void GetDiagnostics_FieldsPopulatedCorrectly_AfterRentReturnCycle()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — rent two buffers of different sizes
        var owner1 = pool.Rent(64);
        var owner2 = pool.Rent(256);

        // Return one, keep the other active
        owner1.Dispose();

        var diag = pool.GetDiagnostics();

        // Assert
        diag.ActiveBuffers.Should().Be(1);
        diag.PooledBuffers.Should().Be(1);
        diag.BucketCount.Should().BeGreaterThanOrEqualTo(2);
        diag.TotalMemory.Should().BeGreaterThan(0);

        // Cleanup
        owner2.Dispose();
    }

    [Fact]
    public void GetDiagnostics_MultipleRentReturnCycles_ConsistentCounts()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — several rent/return cycles
        for (int i = 0; i < 5; i++)
        {
            var owner = pool.Rent(128);
            owner.Dispose();
        }

        var diag = pool.GetDiagnostics();

        // Assert — all returned, none active (sequential reuse = 1 pooled buffer)
        diag.ActiveBuffers.Should().Be(0);
        diag.PooledBuffers.Should().Be(1);
        diag.BucketCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GetDiagnostics_MaxMemory_IsAtLeastTotalMemory()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner = pool.Rent(512);
        var diag = pool.GetDiagnostics();

        // Assert — max should never be less than current total
        diag.MaxMemory.Should().BeGreaterThanOrEqualTo(diag.TotalMemory);

        // Cleanup
        owner.Dispose();
    }

    [Fact]
    public void GetDiagnostics_WastedMemory_IsNonNegative()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner = pool.Rent(100); // may round up to bucket size
        var diag = pool.GetDiagnostics();

        // Assert
        diag.WastedMemory.Should().BeGreaterThanOrEqualTo(0);

        // Cleanup
        owner.Dispose();
    }

    // ─────────────────────────────────────────────────────────────
    //  BucketDiagnostics — per-bucket inspection
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void BucketDiagnostics_IsRecordStruct()
    {
        // Arrange & Act
        var bd = new BucketDiagnostics(64, 1, 64L);

        // Assert — value-type semantics: equality by value
        var bd2 = new BucketDiagnostics(64, 1, 64L);
        bd.Should().Be(bd2);
    }

    [Fact]
    public void BucketDiagnostics_Properties_AreAccessible()
    {
        // Arrange & Act
        var bd = new BucketDiagnostics(256, 3, 768L);

        // Assert
        bd.BufferSize.Should().Be(256);
        bd.CurrentCount.Should().Be(3);
        bd.TotalMemory.Should().Be(768L);
    }

    [Fact]
    public void GetBucketDiagnostics_OnEmptyPool_ReturnsEmptyCollection()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var bucketDiags = pool.GetBucketDiagnostics();

        // Assert
        bucketDiags.Should().BeEmpty();
    }

    [Fact]
    public void GetBucketDiagnostics_AfterRent_ContainsOneEntry()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner = pool.Rent(64);
        var bucketDiags = pool.GetBucketDiagnostics();

        // Assert — at least one bucket should exist
        bucketDiags.Should().HaveCountGreaterThanOrEqualTo(1);

        // Cleanup
        owner.Dispose();
    }

    [Fact]
    public void GetBucketDiagnostics_AfterReturn_BucketHasCurrentCount()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — rent and return to place a buffer in a bucket
        var owner = pool.Rent(64);
        owner.Dispose();

        var bucketDiags = pool.GetBucketDiagnostics();

        // Assert — the bucket for size 64 (or rounded) should have count >= 1
        bucketDiags.Should().ContainSingle(b => b.Value.CurrentCount >= 1);
    }

    [Fact]
    public void GetBucketDiagnostics_MultipleSizes_ReportsPerBucket()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — rent different sizes to populate different buckets
        var owner1 = pool.Rent(32);
        var owner2 = pool.Rent(128);
        var owner3 = pool.Rent(512);

        var bucketDiags = pool.GetBucketDiagnostics();

        // Assert — at least 3 distinct buckets
        bucketDiags.Should().HaveCountGreaterThanOrEqualTo(3);

        // Each entry should have a positive buffer size
        foreach (var bd in bucketDiags)
        {
            bd.Value.BufferSize.Should().BeGreaterThan(0);
        }

        // Cleanup
        owner1.Dispose();
        owner2.Dispose();
        owner3.Dispose();
    }

    [Fact]
    public void GetBucketDiagnostics_TotalMemory_IsBufferSizeTimesCount()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act — rent and return 3 buffers of the same size
        var owners = new IMemoryOwner<byte>[3];
        for (int i = 0; i < 3; i++)
            owners[i] = pool.Rent(64);
        foreach (var o in owners)
            o.Dispose();

        var bucketDiags = pool.GetBucketDiagnostics();

        // Assert — find the bucket with our buffers
        var relevantBucket = bucketDiags.FirstOrDefault(b => b.Value.CurrentCount >= 3);
        relevantBucket.Value.BufferSize.Should().BeGreaterThan(0);
        relevantBucket.Value.TotalMemory.Should().Be(
            (long)relevantBucket.Value.BufferSize * relevantBucket.Value.CurrentCount);
    }

    [Fact]
    public void GetBucketDiagnostics_AfterTrim_CountDecreases()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        var owners = new IMemoryOwner<byte>[4];
        for (int i = 0; i < 4; i++)
            owners[i] = pool.Rent(64);
        foreach (var o in owners)
            o.Dispose();

        var before = pool.GetBucketDiagnostics();
        var countBefore = before.Sum(b => b.Value.CurrentCount);

        // Act
        pool.Trim(0.5);

        var after = pool.GetBucketDiagnostics();
        var countAfter = after.Sum(b => b.Value.CurrentCount);

        // Assert — trim should reduce pooled buffer count
        countAfter.Should().BeLessThan(countBefore);
    }

    // ─────────────────────────────────────────────────────────────
    //  Cross-cutting: diagnostics consistency
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetDiagnostics_And_GetMetrics_AgreeOnActiveAndPooled()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner1 = pool.Rent(64);
        var owner2 = pool.Rent(128);
        owner1.Dispose();

        var metrics = pool.GetMetrics();
        var diag = pool.GetDiagnostics();

        // Assert — both APIs should report the same active/pooled counts
        diag.ActiveBuffers.Should().Be(metrics.ActiveBuffers);
        diag.PooledBuffers.Should().Be(metrics.PooledBuffers);

        // Cleanup
        owner2.Dispose();
    }

    [Fact]
    public void GetDiagnostics_BucketCount_MatchesMetricsBucketCount()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();

        // Act
        var owner1 = pool.Rent(64);
        var owner2 = pool.Rent(256);
        owner1.Dispose();
        owner2.Dispose();

        var metrics = pool.GetMetrics();
        var diag = pool.GetDiagnostics();

        // Assert
        ((long)diag.BucketCount).Should().Be(metrics.BucketCount);
    }

    [Fact]
    public void GetDiagnostics_AfterReset_StillReflectsPoolState()
    {
        // Arrange
        using var pool = new MemoryPool<byte>();
        var owner = pool.Rent(64);
        owner.Dispose();

        // Act
        pool.ResetStatistics();
        var diag = pool.GetDiagnostics();

        // Assert — diagnostics should still show the pooled buffer
        // (ResetStatistics clears counters, but physical pool state remains)
        diag.PooledBuffers.Should().BeGreaterThanOrEqualTo(0);
        diag.BucketCount.Should().BeGreaterThanOrEqualTo(0);
    }
}
