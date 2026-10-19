namespace Axrone.Utility.Tests.Descriptors;

using System;
using Xunit;
using FluentAssertions;
using Axrone.Utility.Descriptors;

public class DescriptorLeaseTests
{
    private struct Record
    {
        public int Value;

        public Record(int value) => Value = value;
    }

    private static DescriptorTable<Record> Create(int capacity = 4) =>
        new(new DescriptorTableOptions { Capacity = (uint)capacity });

    [Fact]
    public void Borrow_Sees_Live_Payload_And_Releases()
    {
        using var table = Create();
        table.TryAllocate(new Record(21), out DescriptorHandle<Record> allocated).Should().BeTrue();
        DescriptorHandle<Record> handle = allocated;

        try
        {
            table.TryBorrow(in handle, out var lease).Should().BeTrue();
            try
            {
                lease.IsActive.Should().BeTrue();
                lease.Handle.Should().Be(handle);
                lease.Payload.Value.Should().Be(21);
            }
            finally
            {
                lease.Dispose();
            }

            table.ActiveCount.Should().Be(1);
        }
        finally
        {
            table.TryFree(in handle);
        }
    }

    [Fact]
    public void Free_While_Borrowed_Retires_Until_Last_Release()
    {
        using var table = Create();
        Span<DescriptorHandle<Record>> handles = stackalloc DescriptorHandle<Record>[4];
        for (int i = 0; i < handles.Length; i++)
        {
            table.TryAllocate(new Record(i), out handles[i]).Should().BeTrue();
        }

        DescriptorHandle<Record> handle = handles[0];
        table.TryBorrow(in handle, out var lease).Should().BeTrue();
        try
        {
            table.TryFree(in handle).Should().BeTrue();
            table.ActiveCount.Should().Be(4);

            DescriptorHandle<Record> probe = handle;
            table.TryGet(in probe, out Record visible).Should().BeTrue();
            visible.Value.Should().Be(0);

            table.TryFree(in handle).Should().BeFalse();
            table.TryBorrow(in handle, out _).Should().BeFalse();
        }
        finally
        {
            lease.Dispose();
        }

        table.ActiveCount.Should().Be(3);
        DescriptorHandle<Record> stale = handle;
        table.TryGet(in stale, out _).Should().BeFalse();

        table.TryAllocate(new Record(6), out DescriptorHandle<Record> recycled).Should().BeTrue();
        try
        {
            recycled.SlotIndex.Should().Be(handle.SlotIndex);
            recycled.Generation.Should().Be(handle.Generation + 1);
        }
        finally
        {
            DescriptorHandle<Record> cleanup = recycled;
            table.TryFree(in cleanup);

            for (int i = 1; i < handles.Length; i++)
            {
                DescriptorHandle<Record> rest = handles[i];
                table.TryFree(in rest);
            }
        }
    }

    [Fact]
    public void Borrow_Stale_Handle_Fails()
    {
        using var table = Create();
        DescriptorHandle<Record> invalid = DescriptorHandle<Record>.Invalid;
        table.TryBorrow(in invalid, out _).Should().BeFalse();
    }

    [Fact]
    public void Lease_Double_Dispose_Throws()
    {
        using var table = Create();
        table.TryAllocate(new Record(1), out DescriptorHandle<Record> allocated).Should().BeTrue();
        DescriptorHandle<Record> handle = allocated;

        try
        {
            table.TryBorrow(in handle, out var lease).Should().BeTrue();
            lease.Dispose();

            bool threw = false;
            try
            {
                lease.Dispose();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            threw.Should().BeTrue();
        }
        finally
        {
            table.TryFree(in handle);
        }
    }
}
