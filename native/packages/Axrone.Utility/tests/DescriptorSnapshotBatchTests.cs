namespace Axrone.Utility.Tests.Descriptors;

using System;
using Xunit;
using FluentAssertions;
using Axrone.Utility.Descriptors;

public class DescriptorSnapshotBatchTests
{
    private struct Record
    {
        public int Value;

        public Record(int value) => Value = value;
    }

    private static DescriptorTable<Record> Create(int capacity = 4) =>
        new(new DescriptorTableOptions { Capacity = (uint)capacity });

    [Fact]
    public void Snapshot_RoundTrips_Live_Payload()
    {
        using var table = Create();
        table.TryAllocate(new Record(11), out DescriptorHandle<Record> allocated).Should().BeTrue();
        DescriptorHandle<Record> handle = allocated;

        try
        {
            table.TrySnapshot(in handle, out DescriptorSnapshot<Record> snapshot).Should().BeTrue();
            snapshot.IsLive.Should().BeTrue();
            snapshot.Descriptor.Value.Should().Be(11);
            snapshot.Status.Should().Be(DescriptorStatus.Allocated);
        }
        finally
        {
            table.TryFree(in handle);
        }
    }

    [Fact]
    public void Snapshot_Rejects_Stale_Handles()
    {
        using var table = Create();
        table.TryAllocate(new Record(1), out DescriptorHandle<Record> allocated).Should().BeTrue();
        DescriptorHandle<Record> handle = allocated;
        table.TryFree(in handle).Should().BeTrue();

        table.TrySnapshot(in handle, out _).Should().BeFalse();
        DescriptorHandle<Record> invalid = DescriptorHandle<Record>.Invalid;
        table.TrySnapshot(in invalid, out _).Should().BeFalse();
    }

    [Fact]
    public void Batch_Stops_At_Capacity()
    {
        using var table = Create(4);
        Span<DescriptorHandle<Record>> handles = stackalloc DescriptorHandle<Record>[4];
        Span<Record> payloads = stackalloc Record[6];
        for (int i = 0; i < payloads.Length; i++)
        {
            payloads[i] = new Record(i);
        }

        int acquired = table.AllocateBatch(handles, payloads);
        try
        {
            acquired.Should().Be(4);
            table.ActiveCount.Should().Be(4);
            DescriptorHandle<Record> probe = handles[3];
            table.TryGet(in probe, out Record last).Should().BeTrue();
            last.Value.Should().Be(3);
        }
        finally
        {
            table.FreeBatch(handles).Should().Be(4);
        }
    }
}
