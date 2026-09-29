namespace Axrone.Utility.Tests.Descriptors;

using Xunit;
using FluentAssertions;
using Axrone.Utility.Descriptors;

public class DescriptorTableTests
{
    private struct Record
    {
        public int Value;

        public Record(int value) => Value = value;
    }

    private readonly struct AddTen : IDescriptorMutator<Record, int>
    {
        public static void Mutate(ref Record descriptor, scoped ref int context)
        {
            descriptor.Value += context;
        }
    }

    private readonly struct ReadInto : IDescriptorAccessor<Record, int>
    {
        public static void Access(ref readonly Record descriptor, scoped ref int context)
        {
            context = descriptor.Value;
        }
    }

    private static DescriptorTable<Record> Create(int capacity = 8) =>
        new(new DescriptorTableOptions { Capacity = (uint)capacity });

    [Fact]
    public void FreshTable_AllocatesAllSlots()
    {
        using var table = Create();
        var handles = new DescriptorHandle<Record>[8];
        for (int i = 0; i < handles.Length; i++)
        {
            table.TryAllocate(new Record(i), out handles[i]).Should().BeTrue();
            handles[i].IsValid.Should().BeTrue();
        }

        table.ActiveCount.Should().Be(8);
        table.TryAllocate(new Record(99), out _).Should().BeFalse();
        table.FreeBatch(handles).Should().Be(8);
    }

    [Fact]
    public void FreeRecycles_WithGenerationBump()
    {
        using var table = Create(4);
        Span<DescriptorHandle<Record>> handles = stackalloc DescriptorHandle<Record>[4];
        for (int i = 0; i < handles.Length; i++)
        {
            table.TryAllocate(new Record(i), out handles[i]).Should().BeTrue();
            handles[i].SlotIndex.Should().Be((uint)i);
        }

        DescriptorHandle<Record> first = handles[0];
        table.TryFree(first).Should().BeTrue();
        table.TryFree(first).Should().BeFalse();
        table.TryGet(first, out _).Should().BeFalse();

        table.TryAllocate(new Record(99), out DescriptorHandle<Record> recycled).Should().BeTrue();
        recycled.SlotIndex.Should().Be(first.SlotIndex);
        recycled.Generation.Should().Be(first.Generation + 1);
        table.TryGet(recycled, out Record payload).Should().BeTrue();
        payload.Value.Should().Be(99);

        table.TryFree(recycled).Should().BeTrue();
        for (int i = 1; i < handles.Length; i++)
        {
            table.TryFree(handles[i]).Should().BeTrue();
        }
    }

    [Fact]
    public void UpdateGetRefMutateAccess_RoundTrip()
    {
        using var table = Create();
        table.TryAllocate(new Record(10), out DescriptorHandle<Record> handle).Should().BeTrue();

        table.TryUpdate(handle, new Record(20)).Should().BeTrue();
        table.GetRef(handle).Value.Should().Be(20);

        int delta = 5;
        table.TryMutate<AddTen, int>(handle, ref delta).Should().BeTrue();
        table.TryGet(handle, out Record updated).Should().BeTrue();
        updated.Value.Should().Be(25);

        int seen = 0;
        table.TryAccess<ReadInto, int>(handle, ref seen).Should().BeTrue();
        seen.Should().Be(25);
        table.TryFree(handle).Should().BeTrue();
    }

    [Fact]
    public void Status_TransitionsAndStaleReadsFree()
    {
        using var table = Create();
        table.TryAllocate(new Record(1), out DescriptorHandle<Record> handle).Should().BeTrue();

        table.GetStatus(handle).Should().Be(DescriptorStatus.Allocated);
        table.TrySetStatus(handle, DescriptorStatus.Active).Should().BeTrue();
        table.GetStatus(handle).Should().Be(DescriptorStatus.Active);
        table.TrySetStatus(handle, DescriptorStatus.Free).Should().BeFalse();

        table.GetStatus(DescriptorHandle<Record>.Invalid).Should().Be(DescriptorStatus.Free);
        table.TryFree(handle).Should().BeTrue();
    }

    [Fact]
    public void FreeBatch_ReclaimsAll()
    {
        using var table = Create();
        Span<DescriptorHandle<Record>> handles = stackalloc DescriptorHandle<Record>[4];
        for (int i = 0; i < handles.Length; i++)
        {
            table.TryAllocate(new Record(i), out handles[i]).Should().BeTrue();
        }

        table.FreeBatch(handles).Should().Be(4);
        table.ActiveCount.Should().Be(0);
        table.FreeBatch(handles).Should().Be(0);
    }

    [Fact]
    public async Task DrainAsync_EmptyCompletes()
    {
        using var table = Create();
        table.TryAllocate(new Record(1), out DescriptorHandle<Record> handle).Should().BeTrue();
        table.TryFree(handle).Should().BeTrue();
        await table.DrainAsync();
    }

    [Fact]
    public void CompleteWithError_FaultsTable()
    {
        using var table = Create();
        table.TryAllocate(new Record(1), out DescriptorHandle<Record> handle).Should().BeTrue();
        table.Complete(new InvalidOperationException("terminal"));

        table.TryAllocate(new Record(2), out _).Should().BeFalse();
        table.TryFree(handle).Should().BeTrue();
    }
}
