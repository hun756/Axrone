namespace Axrone.Utility.Tests.Descriptors;

using System.Threading;
using Xunit;
using FluentAssertions;
using Axrone.Utility.Backoff.SpinPolicies;
using Axrone.Utility.Descriptors;

public class DescriptorAcquisitionTests
{
    private struct Record
    {
        public int Value;

        public Record(int value) => Value = value;
    }

    private struct CountingSink : IDescriptorMetricsSink
    {
        public static int Exhausted;

        public void OnAllocated(uint slotIndex)
        {
        }

        public void OnFreed(uint slotIndex)
        {
        }

        public void OnStatusChanged(uint slotIndex, DescriptorStatus newStatus)
        {
        }

        public void OnFaulted(Exception error)
        {
        }

        public void OnExhausted() => Interlocked.Increment(ref Exhausted);
    }

    private static DescriptorTable<Record, AdaptiveSpinBackoff, CountingSink> CreateCounted(int capacity = 4)
    {
        CountingSink.Exhausted = 0;
        return new DescriptorTable<Record, AdaptiveSpinBackoff, CountingSink>(
            new DescriptorTableOptions { Capacity = (uint)capacity });
    }

    [Fact]
    public void Acquire_Returns_Ok_And_Converts_True()
    {
        using var table = CreateCounted();
        AcquisitionResult<Record> result = table.TryAcquire(new Record(7));

        try
        {
            result.IsSuccess.Should().BeTrue();
            ((bool)result).Should().BeTrue();
            result.Handle.IsValid.Should().BeTrue();
            result.ToString().Should().Contain("Acquired");
        }
        finally
        {
            DescriptorHandle<Record> acquired = result.Handle;
            if (acquired.IsValid)
            {
                table.TryFree(in acquired);
            }
        }
    }

    [Fact]
    public void Full_Table_Returns_Exhausted_And_Signals_Sink()
    {
        using var table = CreateCounted(2);
        AcquisitionResult<Record> first = table.TryAcquire(new Record(1));
        AcquisitionResult<Record> second = table.TryAcquire(new Record(2));
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();

        AcquisitionResult<Record> rejected = table.TryAcquire(new Record(3));
        DescriptorHandle<Record> firstHandle = first.Handle;
        DescriptorHandle<Record> secondHandle = second.Handle;

        try
        {
            rejected.IsSuccess.Should().BeFalse();
            ((bool)rejected).Should().BeFalse();
            rejected.Status.Should().Be(AcquisitionStatus.Exhausted);
            rejected.ToString().Should().Contain("Rejected");
            CountingSink.Exhausted.Should().BeGreaterThanOrEqualTo(1);
        }
        finally
        {
            table.TryFree(in firstHandle);
            table.TryFree(in secondHandle);
        }
    }

    [Fact]
    public void Drained_Table_Returns_Terminated()
    {
        using var table = CreateCounted();
        table.Complete();

        AcquisitionResult<Record> result = table.TryAcquire(new Record(1));
        result.Status.Should().Be(AcquisitionStatus.Terminated);
        ((bool)result).Should().BeFalse();
    }

    [Fact]
    public void Mask_Matches_Power_Of_Two_Capacity()
    {
        using var table = new DescriptorTable<Record>(new DescriptorTableOptions { Capacity = 8 });
        table.Mask.Should().Be(7U);
        table.Capacity.Should().Be(8U);
    }
}
