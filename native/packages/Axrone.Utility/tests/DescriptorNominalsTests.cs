namespace Axrone.Utility.Tests.Descriptors;

using Xunit;
using FluentAssertions;
using Axrone.Utility.Descriptors;

public class DescriptorNominalTests
{
    [Fact]
    public void Flags_Bitwise_RoundTrip()
    {
        var flags = new DescriptorFlags(0xC9UL);
        flags.HasFlag(new DescriptorFlags(0xC8UL)).Should().BeTrue();
        flags.HasFlag(new DescriptorFlags(0x02UL)).Should().BeFalse();
        ((flags | new DescriptorFlags(0x02UL)) & flags).Should().Be(flags);
        ((ulong)DescriptorFlags.None).Should().Be(0UL);
    }

    [Fact]
    public void TableCapacity_Requires_PowerOfTwo()
    {
        TableCapacity capacity = 64U;
        capacity.Value.Should().Be(64U);
        capacity.Mask.Should().Be(63U);
        ((uint)capacity).Should().Be(64U);

        Action zero = () => { _ = new TableCapacity(0U); };
        zero.Should().Throw<ArgumentException>();
        Action odd = () => { _ = new TableCapacity(24U); };
        odd.Should().Throw<ArgumentException>();
    }
}
