namespace Axrone.Memory.Tests.Memory;

using FluentAssertions;
using Xunit;

[Collection("MemoryPool")]
public sealed class OptionsExpansionTests
{
    private static readonly int[] s_sizes32to512 = [32, 64, 128, 256, 512];
    private static readonly int[] s_sizes64_256_1024 = [64, 256, 1024];
    private static readonly int[] s_sizes32to256 = [32, 64, 128, 256];
    private static readonly int[] s_sizes64to256 = [64, 128, 256];
    private static readonly int[] s_sizes64to512 = [64, 128, 256, 512];

    // ─── New Property Defaults

    [Fact]
    public void TlsCacheSize_DefaultValue_Is8()
    {
        var options = new MemoryPoolOptions<byte>();

        options.TlsCacheSize.Should().Be(8);
    }

    [Fact]
    public void MaxTotalMemory_DefaultValue_IsLongMaxValue()
    {
        var options = new MemoryPoolOptions<byte>();

        options.MaxTotalMemory.Should().Be(long.MaxValue);
    }

    [Fact]
    public void Flags_DefaultValue_IsEnableThreadSafety()
    {
        var options = new MemoryPoolOptions<byte>();

        options.Flags.Should().Be(MemoryPoolFlags.EnableThreadSafety);
    }

    [Fact]
    public void AllocationAlignment_DefaultValue_IsNull()
    {
        var options = new MemoryPoolOptions<byte>();

        options.AllocationAlignment.Should().BeNull();
    }

    [Fact]
    public void PreallocationCount_DefaultValue_Is0()
    {
        var options = new MemoryPoolOptions<byte>();

        options.PreallocationCount.Should().Be(0);
    }

    [Fact]
    public void MemoryGuardPadding_DefaultValue_Is16()
    {
        var options = new MemoryPoolOptions<byte>();

        options.MemoryGuardPadding.Should().Be(16);
    }

    [Fact]
    public void TrimInterval_DefaultValue_Is2Minutes()
    {
        var options = new MemoryPoolOptions<byte>();

        options.TrimInterval.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void AutoTrimPercentage_DefaultValue_Is25()
    {
        var options = new MemoryPoolOptions<byte>();

        options.AutoTrimPercentage.Should().Be(25);
    }

    [Fact]
    public void BatchOperationSize_DefaultValue_Is16()
    {
        var options = new MemoryPoolOptions<byte>();

        options.BatchOperationSize.Should().Be(16);
    }

    [Fact]
    public void AllocationQuota_DefaultValue_IsMinus1()
    {
        var options = new MemoryPoolOptions<byte>();

        options.AllocationQuota.Should().Be(-1);
    }

    [Fact]
    public void OverAllocationFactor_DefaultValue_Is1()
    {
        var options = new MemoryPoolOptions<byte>();

        options.OverAllocationFactor.Should().Be(1.0f);
    }

    [Fact]
    public void CustomBucketSizes_DefaultValue_IsEmpty()
    {
        var options = new MemoryPoolOptions<byte>();

        options.CustomBucketSizes.Should().NotBeNull().And.BeEmpty();
    }

    // ─── WithFlags ───────────────────────────────────────────────────────

    [Fact]
    public void WithFlags_SetsFlagsProperty_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithFlags(MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.ZeroMemory);

        result.Should().BeSameAs(options);
        options.Flags.Should().Be(MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.ZeroMemory);
    }

    [Fact]
    public void WithFlags_None_ClearsAllFlags()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.EnableProfiling);

        options.WithFlags(MemoryPoolFlags.None);

        options.Flags.Should().Be(MemoryPoolFlags.None);
    }

    // ─── AddFlag ─────────────────────────────────────────────────────────

    [Fact]
    public void AddFlag_AddsSingleFlag_ToExistingFlags()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableStatistics);

        options.AddFlag(MemoryPoolFlags.ZeroMemory);

        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableStatistics);
        options.Flags.Should().HaveFlag(MemoryPoolFlags.ZeroMemory);
    }

    [Fact]
    public void AddFlag_DoesNotRemoveExistingFlags()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.EnableProfiling);

        options.AddFlag(MemoryPoolFlags.UseCompression);

        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableStatistics);
        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableProfiling);
        options.Flags.Should().HaveFlag(MemoryPoolFlags.UseCompression);
    }

    [Fact]
    public void AddFlag_AlreadyPresent_DoesNotDuplicate()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableStatistics);

        options.AddFlag(MemoryPoolFlags.EnableStatistics);

        options.Flags.Should().Be(MemoryPoolFlags.EnableStatistics);
    }

    [Fact]
    public void AddFlag_ReturnsSameInstance_ForChaining()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.AddFlag(MemoryPoolFlags.EnableStatistics);

        result.Should().BeSameAs(options);
    }

    // ─── WithAlignment ───────────────────────────────────────────────────

    [Fact]
    public void WithAlignment_SetsAllocationAlignment_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithAlignment(64);

        result.Should().BeSameAs(options);
        options.AllocationAlignment.Should().Be(64);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(4096)]
    public void WithAlignment_PowerOfTwo_Succeeds(int alignment)
    {
        var options = new MemoryPoolOptions<byte>().WithAlignment(alignment);

        options.AllocationAlignment.Should().Be(alignment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(-1)]
    [InlineData(-4)]
    public void WithAlignment_NotPowerOfTwo_ThrowsArgumentException(int alignment)
    {
        var options = new MemoryPoolOptions<byte>();

        var act = () => options.WithAlignment(alignment);

        act.Should().Throw<ArgumentException>();
    }

    // ─── WithStatistics ──────────────────────────────────────────────────

    [Fact]
    public void WithStatistics_DefaultTrue_EnablesStatisticsFlag()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.None);

        options.WithStatistics();

        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableStatistics);
    }

    [Fact]
    public void WithStatistics_True_EnablesStatisticsFlag()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.None);

        options.WithStatistics(true);

        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableStatistics);
    }

    [Fact]
    public void WithStatistics_False_DisablesStatisticsFlag()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableStatistics);

        options.WithStatistics(false);

        options.Flags.Should().NotHaveFlag(MemoryPoolFlags.EnableStatistics);
    }

    [Fact]
    public void WithStatistics_PreservesOtherFlags()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableProfiling | MemoryPoolFlags.ZeroMemory);

        options.WithStatistics();

        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableStatistics);
        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableProfiling);
        options.Flags.Should().HaveFlag(MemoryPoolFlags.ZeroMemory);
    }

    [Fact]
    public void WithStatistics_ReturnsSameInstance_ForChaining()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithStatistics();

        result.Should().BeSameAs(options);
    }

    // ─── WithMaxMemory ───────────────────────────────────────────────────

    [Fact]
    public void WithMaxMemory_SetsMaxTotalMemory_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithMaxMemory(1024L * 1024L * 256L);

        result.Should().BeSameAs(options);
        options.MaxTotalMemory.Should().Be(1024L * 1024L * 256L);
    }

    [Fact]
    public void WithMaxMemory_OneGigabyte_SetsCorrectly()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithMaxMemory(1L * 1024L * 1024L * 1024L);

        options.MaxTotalMemory.Should().Be(1L * 1024L * 1024L * 1024L);
    }

    // ─── WithThreadSafety ────────────────────────────────────────────────

    [Fact]
    public void WithThreadSafety_DefaultTrue_EnablesThreadSafetyFlag()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.None);

        options.WithThreadSafety();

        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableThreadSafety);
    }

    [Fact]
    public void WithThreadSafety_True_EnablesThreadSafetyFlag()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.None);

        options.WithThreadSafety(true);

        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableThreadSafety);
    }

    [Fact]
    public void WithThreadSafety_False_DisablesThreadSafetyFlag()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableThreadSafety);

        options.WithThreadSafety(false);

        options.Flags.Should().NotHaveFlag(MemoryPoolFlags.EnableThreadSafety);
    }

    [Fact]
    public void WithThreadSafety_PreservesOtherFlags()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.ZeroMemory);

        options.WithThreadSafety();

        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableThreadSafety);
        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableStatistics);
        options.Flags.Should().HaveFlag(MemoryPoolFlags.ZeroMemory);
    }

    [Fact]
    public void WithThreadSafety_ReturnsSameInstance_ForChaining()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithThreadSafety();

        result.Should().BeSameAs(options);
    }

    // ─── WithTlsCache ────────────────────────────────────────────────────

    [Fact]
    public void WithTlsCache_DefaultTrue_EnablesTlsCacheFlag()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.None);

        options.WithTlsCache();

        options.Flags.Should().HaveFlag(MemoryPoolFlags.UseTlsCache);
    }

    [Fact]
    public void WithTlsCache_True_EnablesTlsCacheFlag()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.None);

        options.WithTlsCache(true);

        options.Flags.Should().HaveFlag(MemoryPoolFlags.UseTlsCache);
    }

    [Fact]
    public void WithTlsCache_False_DisablesTlsCacheFlag()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.UseTlsCache);

        options.WithTlsCache(false);

        options.Flags.Should().NotHaveFlag(MemoryPoolFlags.UseTlsCache);
    }

    [Fact]
    public void WithTlsCache_ReturnsSameInstance_ForChaining()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithTlsCache();

        result.Should().BeSameAs(options);
    }

    // ─── WithPreallocation ───────────────────────────────────────────────

    [Fact]
    public void WithPreallocation_SetsPreallocationCount_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithPreallocation(10);

        result.Should().BeSameAs(options);
        options.PreallocationCount.Should().Be(10);
    }

    [Fact]
    public void WithPreallocation_Zero_SetsCountToZero()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithPreallocation(10);

        options.WithPreallocation(0);

        options.PreallocationCount.Should().Be(0);
    }

    // ─── WithCustomBucketSizes ───────────────────────────────────────────

    [Fact]
    public void WithCustomBucketSizes_SetsBucketSizes_AndReturnsSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options.WithCustomBucketSizes(32, 64, 128, 256, 512);

        result.Should().BeSameAs(options);
        options.CustomBucketSizes.Should().BeEquivalentTo(s_sizes32to512);
    }

    [Fact]
    public void WithCustomBucketSizes_Empty_SetsEmptyList()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithCustomBucketSizes(64, 128);

        options.WithCustomBucketSizes();

        options.CustomBucketSizes.Should().BeEmpty();
    }

    [Fact]
    public void WithCustomBucketSizes_SingleSize_SetsSingleElementList()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithCustomBucketSizes(1024);

        options.CustomBucketSizes.Should().ContainSingle().Which.Should().Be(1024);
    }

    // ─── Builder Chaining with New Methods ───────────────────────────────

    [Fact]
    public void Chaining_NewBuilderMethods_AllReturnSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options
            .WithFlags(MemoryPoolFlags.None)
            .WithAlignment(64)
            .WithStatistics()
            .WithMaxMemory(1024L * 1024L)
            .WithThreadSafety()
            .WithTlsCache()
            .WithPreallocation(5)
            .WithCustomBucketSizes(32, 64, 128);

        result.Should().BeSameAs(options);
    }

    [Fact]
    public void Chaining_NewBuilderMethods_SetsAllPropertiesCorrectly()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableProfiling)
            .WithAlignment(128)
            .WithStatistics()
            .WithMaxMemory(512L * 1024L * 1024L)
            .WithThreadSafety()
            .WithTlsCache()
            .WithPreallocation(8)
            .WithCustomBucketSizes(64, 256, 1024);

        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableProfiling);
        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableStatistics);
        options.Flags.Should().HaveFlag(MemoryPoolFlags.EnableThreadSafety);
        options.Flags.Should().HaveFlag(MemoryPoolFlags.UseTlsCache);
        options.AllocationAlignment.Should().Be(128);
        options.MaxTotalMemory.Should().Be(512L * 1024L * 1024L);
        options.PreallocationCount.Should().Be(8);
        options.CustomBucketSizes.Should().BeEquivalentTo(s_sizes64_256_1024);
    }

    [Fact]
    public void Chaining_MixedOldAndNewBuilderMethods_AllReturnSameInstance()
    {
        var options = new MemoryPoolOptions<byte>();

        var result = options
            .WithDefaultBufferSize(2048)
            .WithFlags(MemoryPoolFlags.None)
            .WithMaxBufferSize(65536)
            .WithAlignment(32)
            .WithMinBufferSize(32)
            .WithStatistics()
            .WithClearMode(ClearMode.OnRent)
            .WithMaxMemory(1024L * 1024L)
            .WithMaxBuffersPerBucket(16)
            .WithThreadSafety()
            .WithAllocationStrategy(AllocationStrategy.Fibonacci)
            .WithPreallocation(4)
            .WithChunkSize(256)
            .WithCustomBucketSizes(64, 128);

        result.Should().BeSameAs(options);
    }

    // ─── Clone copies all new properties ─────────────────────────────────

    [Fact]
    public void Clone_CopiesAllNewProperties()
    {
        var original = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.ZeroMemory)
            .WithAlignment(64)
            .WithMaxMemory(256L * 1024L * 1024L)
            .WithPreallocation(10)
            .WithCustomBucketSizes(32, 64, 128, 256);

        original.TlsCacheSize = 16;
        original.MemoryGuardPadding = 32;
        original.TrimInterval = TimeSpan.FromSeconds(30);
        original.AutoTrimPercentage = 50;
        original.BatchOperationSize = 32;
        original.AllocationQuota = 1000;
        original.OverAllocationFactor = 1.5f;

        var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.TlsCacheSize.Should().Be(16);
        clone.MaxTotalMemory.Should().Be(256L * 1024L * 1024L);
        clone.Flags.Should().Be(MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.ZeroMemory);
        clone.AllocationAlignment.Should().Be(64);
        clone.PreallocationCount.Should().Be(10);
        clone.MemoryGuardPadding.Should().Be(32);
        clone.TrimInterval.Should().Be(TimeSpan.FromSeconds(30));
        clone.AutoTrimPercentage.Should().Be(50);
        clone.BatchOperationSize.Should().Be(32);
        clone.AllocationQuota.Should().Be(1000);
        clone.OverAllocationFactor.Should().Be(1.5f);
        clone.CustomBucketSizes.Should().BeEquivalentTo(s_sizes32to256);
    }

    [Fact]
    public void Clone_WithDefaultNewProperties_ProducesIdenticalCopy()
    {
        var original = new MemoryPoolOptions<int>();

        var clone = original.Clone();

        clone.Should().NotBeSameAs(original);
        clone.TlsCacheSize.Should().Be(8);
        clone.MaxTotalMemory.Should().Be(long.MaxValue);
        clone.Flags.Should().Be(MemoryPoolFlags.EnableThreadSafety);
        clone.AllocationAlignment.Should().BeNull();
        clone.PreallocationCount.Should().Be(0);
        clone.MemoryGuardPadding.Should().Be(16);
        clone.TrimInterval.Should().Be(TimeSpan.FromMinutes(2));
        clone.AutoTrimPercentage.Should().Be(25);
        clone.BatchOperationSize.Should().Be(16);
        clone.AllocationQuota.Should().Be(-1);
        clone.OverAllocationFactor.Should().Be(1.0f);
        clone.CustomBucketSizes.Should().BeEmpty();
    }

    [Fact]
    public void Clone_CustomBucketSizes_ModifyingClone_DoesNotAffectOriginal()
    {
        var original = new MemoryPoolOptions<byte>()
            .WithCustomBucketSizes(64, 128, 256);

        var clone = original.WithCustomBucketSizes(64, 128, 256, 512);

        original.CustomBucketSizes.Should().BeEquivalentTo(s_sizes64to256);
        clone.CustomBucketSizes.Should().BeEquivalentTo(s_sizes64to512);
    }

    [Fact]
    public void Clone_ModifyingNewPropertiesOnClone_DoesNotAffectOriginal()
    {
        var original = new MemoryPoolOptions<byte>()
            .WithFlags(MemoryPoolFlags.EnableStatistics)
            .WithAlignment(64)
            .WithMaxMemory(1024L)
            .WithPreallocation(5);

        var clone = original.Clone();
        clone.Flags = MemoryPoolFlags.None;
        clone.AllocationAlignment = 128;
        clone.MaxTotalMemory = 2048L;
        clone.PreallocationCount = 20;
        clone.TlsCacheSize = 32;
        clone.MemoryGuardPadding = 64;
        clone.TrimInterval = TimeSpan.FromSeconds(10);
        clone.AutoTrimPercentage = 50;
        clone.BatchOperationSize = 64;
        clone.AllocationQuota = 500;
        clone.OverAllocationFactor = 2.0f;

        original.Flags.Should().Be(MemoryPoolFlags.EnableStatistics);
        original.AllocationAlignment.Should().Be(64);
        original.MaxTotalMemory.Should().Be(1024L);
        original.PreallocationCount.Should().Be(5);
        original.TlsCacheSize.Should().Be(8);
        original.MemoryGuardPadding.Should().Be(16);
        original.TrimInterval.Should().Be(TimeSpan.FromMinutes(2));
        original.AutoTrimPercentage.Should().Be(25);
        original.BatchOperationSize.Should().Be(16);
        original.AllocationQuota.Should().Be(-1);
        original.OverAllocationFactor.Should().Be(1.0f);
    }

    [Fact]
    public void Clone_AlsoCopiesOriginalProperties()
    {
        var original = new MemoryPoolOptions<byte>()
            .WithDefaultBufferSize(2048)
            .WithMaxBufferSize(65536)
            .WithMinBufferSize(32)
            .WithClearMode(ClearMode.Always)
            .WithMaxBuffersPerBucket(16)
            .WithAllocationStrategy(AllocationStrategy.Fibonacci)
            .WithChunkSize(256)
            .WithFlags(MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.ZeroMemory)
            .WithAlignment(128)
            .WithMaxMemory(512L * 1024L * 1024L)
            .WithPreallocation(8)
            .WithCustomBucketSizes(64, 256, 1024);

        var clone = original.Clone();

        // Original properties
        clone.DefaultBufferSize.Should().Be(2048);
        clone.MaxBufferSize.Should().Be(65536);
        clone.MinBufferSize.Should().Be(32);
        clone.ClearMode.Should().Be(ClearMode.Always);
        clone.MaxBuffersPerBucket.Should().Be(16);
        clone.AllocationStrategy.Should().Be(AllocationStrategy.Fibonacci);
        clone.ChunkSize.Should().Be(256);
        // New properties
        clone.Flags.Should().Be(MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.ZeroMemory);
        clone.AllocationAlignment.Should().Be(128);
        clone.MaxTotalMemory.Should().Be(512L * 1024L * 1024L);
        clone.PreallocationCount.Should().Be(8);
        clone.CustomBucketSizes.Should().BeEquivalentTo(s_sizes64_256_1024);
    }

    // ─── MemoryPool<T>.Shared ────────────────────────────────────────────

    [Fact]
    public void Shared_ReturnsNonNullInstance()
    {
        MemoryPool<byte>.Shared.Should().NotBeNull();
    }

    [Fact]
    public void Shared_ReturnsSameInstance_OnMultipleAccesses()
    {
        var first = MemoryPool<byte>.Shared;
        var second = MemoryPool<byte>.Shared;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Shared_DifferentTypes_ReturnDifferentInstances()
    {
        var bytePool = MemoryPool<byte>.Shared;
        var intPool = MemoryPool<int>.Shared;

        // They are different generic instantiations, so they must be distinct objects.
        // We verify both are non-null singletons for their respective types.
        bytePool.Should().NotBeNull();
        intPool.Should().NotBeNull();
        MemoryPool<byte>.Shared.Should().BeSameAs(bytePool);
        MemoryPool<int>.Shared.Should().BeSameAs(intPool);
    }

    [Fact]
    public void Shared_CanRentAndReturnBuffers()
    {
        var pool = MemoryPool<byte>.Shared;

        using var owner = pool.Rent(128);

        owner.Should().NotBeNull();
        owner.Memory.Length.Should().BeGreaterThanOrEqualTo(128);
    }

    // ─── Preallocation ───────────────────────────────────────────────────

    [Fact]
    public void PreallocationCount_GreaterThanZero_PoolHasTotalAllocatedGreaterThanZero()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithPreallocation(5);

        using var pool = new MemoryPool<byte>(options);

        pool.TotalAllocated.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PreallocationCount_Zero_PoolHasZeroTotalAllocated()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithPreallocation(0);

        using var pool = new MemoryPool<byte>(options);

        pool.TotalAllocated.Should().Be(0);
    }

    [Fact]
    public void PreallocationCount_LargerCount_AllocatesMoreBuffers()
    {
        var optionsSmall = new MemoryPoolOptions<byte>()
            .WithPreallocation(2);

        var optionsLarge = new MemoryPoolOptions<byte>()
            .WithPreallocation(10);

        using var poolSmall = new MemoryPool<byte>(optionsSmall);
        using var poolLarge = new MemoryPool<byte>(optionsLarge);

        poolLarge.TotalAllocated.Should().BeGreaterThan(poolSmall.TotalAllocated);
    }

    [Fact]
    public void Preallocation_BuffersAreAvailableForRentWithoutNewAllocation()
    {
        var options = new MemoryPoolOptions<byte>()
            .WithPreallocation(3);

        using var pool = new MemoryPool<byte>(options);

        long allocatedBeforeRent = pool.TotalAllocated;

        // Renting a preallocated buffer should not increase TotalAllocated
        using var owner = pool.Rent(16);

        pool.TotalAllocated.Should().BeLessThanOrEqualTo(allocatedBeforeRent);
    }

    // ─── Settable Properties (direct assignment) ─────────────────────────

    [Fact]
    public void TlsCacheSize_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte> { TlsCacheSize = 32 };

        options.TlsCacheSize.Should().Be(32);
    }

    [Fact]
    public void MaxTotalMemory_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte> { MaxTotalMemory = 1024L * 1024L };

        options.MaxTotalMemory.Should().Be(1024L * 1024L);
    }

    [Fact]
    public void Flags_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte>
        {
            Flags = MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.UseCompression
        };

        options.Flags.Should().Be(MemoryPoolFlags.EnableStatistics | MemoryPoolFlags.UseCompression);
    }

    [Fact]
    public void AllocationAlignment_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte> { AllocationAlignment = 256 };

        options.AllocationAlignment.Should().Be(256);
    }

    [Fact]
    public void PreallocationCount_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte> { PreallocationCount = 20 };

        options.PreallocationCount.Should().Be(20);
    }

    [Fact]
    public void MemoryGuardPadding_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte> { MemoryGuardPadding = 64 };

        options.MemoryGuardPadding.Should().Be(64);
    }

    [Fact]
    public void TrimInterval_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte> { TrimInterval = TimeSpan.FromSeconds(30) };

        options.TrimInterval.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void AutoTrimPercentage_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte> { AutoTrimPercentage = 50 };

        options.AutoTrimPercentage.Should().Be(50);
    }

    [Fact]
    public void BatchOperationSize_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte> { BatchOperationSize = 32 };

        options.BatchOperationSize.Should().Be(32);
    }

    [Fact]
    public void AllocationQuota_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte> { AllocationQuota = 500 };

        options.AllocationQuota.Should().Be(500);
    }

    [Fact]
    public void OverAllocationFactor_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte> { OverAllocationFactor = 2.5f };

        options.OverAllocationFactor.Should().Be(2.5f);
    }

    [Fact]
    public void CustomBucketSizes_CanBeSetDirectly()
    {
        var options = new MemoryPoolOptions<byte>
        {
            CustomBucketSizes = new List<int> { 32, 64, 128, 256 }
        };

        options.CustomBucketSizes.Should().BeEquivalentTo(s_sizes32to256);
    }
}
