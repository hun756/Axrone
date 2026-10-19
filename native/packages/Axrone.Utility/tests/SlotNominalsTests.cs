namespace Axrone.Utility.Tests.Slots;

using Xunit;
using FluentAssertions;
using Axrone.Utility.Slots;

public class SlotNominalTests
{
    [Fact]
    public void SlotIndex_Sentinel_And_Ordering()
    {
        SlotIndex.Invalid.Value.Should().Be(uint.MaxValue);
        SlotIndex.Invalid.IsValid.Should().BeFalse();
        new SlotIndex(0U).IsValid.Should().BeTrue();
        var a = new SlotIndex(0U);
        a.CompareTo(new SlotIndex(1U)).Should().BeNegative();
        var b = new SlotIndex(1U);
        (a < b).Should().BeTrue();
        (b >= a).Should().BeTrue();
        ((uint)a).Should().Be(0U);
        ((SlotIndex)7U).Value.Should().Be(7U);
        SlotIndex.Invalid.ToString().Should().Be("SlotIndex[Invalid]");
    }

    [Fact]
    public void Generation_Wraps_Past_Zero()
    {
        Generation.Initial.Value.Should().Be(1U);
        Generation.Zero.Next().Value.Should().Be(1U);
        new Generation(uint.MaxValue).Next().Value.Should().Be(1U);
        (Generation.Initial > Generation.Zero).Should().BeTrue();
    }
}
