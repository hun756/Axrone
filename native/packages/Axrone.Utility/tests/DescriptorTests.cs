namespace Axrone.Utility.Tests.Descriptors;

using Xunit;
using FluentAssertions;
using Axrone.Utility.Descriptors;

public class DescriptorValueTests
{
    private struct Payload
    {
        public int Value;

        public Payload(int value) => Value = value;
    }

    [Fact]
    public void Handle_InvalidSentinelAndOrdering()
    {
        DescriptorHandle<Payload>.Invalid.IsValid.Should().BeFalse();
        var a = new DescriptorHandle<Payload>(0, 1);
        a.IsValid.Should().BeTrue();
        a.CompareTo(new DescriptorHandle<Payload>(1, 1)).Should().BeNegative();
        a.CompareTo(new DescriptorHandle<Payload>(0, 2)).Should().BeNegative();
        a.CompareTo(a).Should().Be(0);
        a.ToString().Should().Contain("Slot=0");
    }

    [Fact]
    public void Options_RejectNonPowerOfTwo()
    {
        Action zero = () => { _ = new DescriptorTableOptions { Capacity = 0 }; };
        zero.Should().Throw<ArgumentException>();

        Action odd = () => { _ = new DescriptorTableOptions { Capacity = 24 }; };
        odd.Should().Throw<ArgumentException>();

        var ok = new DescriptorTableOptions { Capacity = 64 };
        ok.Capacity.Should().Be(64u);
        new DescriptorTableOptions().Capacity.Should().Be(1024u);
    }

    [Fact]
    public void NullSink_DropsEvents()
    {
        var sink = new NullMetricsSink();
        Action act = () =>
        {
            sink.OnAllocated(1);
            sink.OnFreed(1);
            sink.OnStatusChanged(1, DescriptorStatus.Active);
            sink.OnFaulted(new InvalidOperationException());
        };
        act.Should().NotThrow();
    }
}
