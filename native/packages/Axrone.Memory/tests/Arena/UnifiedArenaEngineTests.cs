namespace Axrone.Memory.Tests.Arena;

using Axrone.Memory.Arena;

public sealed class UnifiedArenaEngineTests : IDisposable
{
    private Arena? _arena;

    public void Dispose() => _arena?.Dispose();

    private Arena NewArena(ByteSize? initial = null)
    {
        _arena?.Dispose();
        _arena = new Arena(new ArenaOptions
        {
            InitialChunkSize = initial ?? ByteSize.FromKilobytes(16),
            MaxChunkSize = ByteSize.FromMegabytes(64)
        });
        return _arena;
    }

    [Fact]
    public void Allocate_ReturnsAlignedWritableMemory()
    {
        unsafe
        {
            using var arena = NewArena();
            _arena = null;

            Span<int> span = arena.AllocateSpan<int>(64);

            span.Length.Should().Be(64);
            ((nuint)System.Runtime.CompilerServices.Unsafe.AsPointer(ref span[0]) % 4).Should().Be((nuint)0);
            span.Fill(7);
            span[63].Should().Be(7);
        }
    }

    [Fact]
    public void Allocate_GrowsAcrossChunks()
    {
        using var arena = NewArena(ByteSize.FromBytes(4096));
        _arena = null;

        for (int i = 0; i < 64; i++)
        {
            arena.AllocateSpan<byte>(512);
        }

        int chunks = 0;
        foreach (ChunkInfo chunk in arena.Chunks)
        {
            chunks++;
            chunk.Capacity.Value.Should().BeGreaterThan((nuint)0);
        }
        chunks.Should().BeGreaterThan(1);
        arena.TotalAllocatedBytes.Value.Should().BeGreaterThan((nuint)0);
        arena.TotalCommittedBytes.Value.Should().BeGreaterThanOrEqualTo(arena.TotalAllocatedBytes.Value);
    }

    [Fact]
    public void TryAllocate_Zero_And_BadAlignment()
    {
        using var arena = NewArena();
        _arena = null;

        arena.TryAllocate(ByteSize.Zero, Alignment.Byte).IsSuccess.Should().BeTrue();
        arena.TryAllocate(ByteSize.FromBytes(8), default).Status.Should().Be(AllocationStatus.InvalidAlignment);
    }

    [Fact]
    public void Marker_Rewind_RestoresPosition()
    {
        using var arena = NewArena();
        _arena = null;

        arena.AllocateSpan<int>(16);
        ArenaMarker marker = arena.CreateMarker();
        arena.AllocateSpan<int>(16);
        int before = 0;
        foreach (ChunkInfo chunk in arena.Chunks)
        {
            before += (int)chunk.Allocated.Value;
        }

        arena.Rewind(marker);

        int after = 0;
        foreach (ChunkInfo chunk in arena.Chunks)
        {
            after += (int)chunk.Allocated.Value;
        }
        after.Should().BeLessThan(before);

        arena.AllocateSpan<int>(16).Length.Should().Be(16);
    }

    [Fact]
    public void Reset_ClearsAllocations()
    {
        using var arena = NewArena();
        _arena = null;

        arena.AllocateSpan<int>(32);
        arena.Reset();

        arena.TotalAllocatedBytes.Should().Be(ByteSize.Zero);
        arena.AllocateSpan<int>(32).Length.Should().Be(32);
    }

    [Fact]
    public void Scope_RewindsOnDispose()
    {
        using var arena = NewArena();
        _arena = null;

        arena.AllocateSpan<int>(8);
        using (arena.CreateScope())
        {
            arena.AllocateSpan<int>(8);
        }

        arena.TotalAllocatedBytes.Should().Be(ByteSize.FromBytes(32));
    }

    [Fact]
    public void Complete_BlocksFurtherAllocations()
    {
        var arena = NewArena();
        _arena = arena;

        arena.Complete();

        Action act = () => arena.AllocateSpan<int>(4);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_WithError_RethrowsFault()
    {
        var arena = NewArena();
        _arena = arena;
        var failure = new InvalidOperationException("boom");

        arena.Complete(failure);

        Action act = () => arena.AllocateSpan<int>(4);
        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task DrainAsync_ReturnsWhenIdle()
    {
        using var arena = NewArena();
        _arena = null;

        await arena.DrainAsync();
    }

    [Fact]
    public void Dispose_IsIdempotent_AndPoisonsUse()
    {
        var arena = NewArena();
        _arena = arena;

        arena.Dispose();
        Action again = () => arena.Dispose();
        Action read = () => arena.AllocateSpan<int>(1);

        again.Should().NotThrow();
        read.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Builder_BuildsDefaultArena()
    {
        using var arena = ArenaBuilder<UnconfiguredArenaState>.Create()
            .WithInitialChunkSize(ByteSize.FromKilobytes(8))
            .Build();
        _arena = null;

        arena.AllocateSpan<int>(4).Length.Should().Be(4);
    }

    [Fact]
    public void ExecuteScoped_RunsActionOverScratch()
    {
        using var arena = NewArena();
        _arena = null;

        int seen = 0;
        var action = new FillAction();
        arena.ExecuteScoped(ByteSize.FromBytes(16), Alignment.Byte, ref seen, action);

        seen.Should().Be(16);
    }

    [Fact]
    public void FixedPolicy_KeepsUniformChunks()
    {
        using var arena = new Arena<FixedGrowthPolicy, AdaptiveSpinBackoff, NullMetricsSink>(new ArenaOptions
        {
            InitialChunkSize = ByteSize.FromBytes(4096),
            MaxChunkSize = ByteSize.FromBytes(4096)
        });
        _arena = null;

        for (int i = 0; i < 8; i++)
        {
            arena.AllocateSpan<byte>(1024);
        }

        var capacities = new List<nuint>();
        foreach (ChunkInfo chunk in arena.Chunks)
        {
            capacities.Add(chunk.Capacity.Value);
        }

        capacities.Count.Should().BeGreaterThan(1);
        capacities.Should().AllSatisfy(cap => cap.Should().Be((nuint)4096));
    }

    private readonly struct FillAction : ISpanAction<int>
    {
        public void Execute(Span<byte> buffer, ref int state)
        {
            buffer.Fill(0xAB);
            state = buffer.Length;
        }
    }
}
