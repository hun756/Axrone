namespace Axrone.Memory.Tests.Memory;

using Axrone.Memory;

public class TaggedPointerPolicyTests
{
    [Fact]
    public void LowBit3_PartitionsTheWord()
    {
        LowBit3TagPolicy.TagMask.Should().Be((nuint)0x7);
        (LowBit3TagPolicy.TagMask & LowBit3TagPolicy.PointerMask).Should().Be((nuint)0);
        (LowBit3TagPolicy.TagMask | LowBit3TagPolicy.PointerMask).Should().Be(nuint.MaxValue);
        LowBit3TagPolicy.TagShift.Should().Be(0);
        LowBit3TagPolicy.AlignmentRequirement.Should().Be((nuint)8);
    }

    [Fact]
    public void LowBit3_PackMasksOverflow()
    {
        LowBit3TagPolicy.Pack(0xFF).Should().Be((nuint)0x7);
        LowBit3TagPolicy.Pack(5).Should().Be((nuint)5);
        LowBit3TagPolicy.Unpack(0xFFF5).Should().Be((byte)0x5);
    }

    [Fact]
    public void LowBit4_PartitionsTheWord()
    {
        LowBit4TagPolicy.TagMask.Should().Be((nuint)0xF);
        (LowBit4TagPolicy.TagMask & LowBit4TagPolicy.PointerMask).Should().Be((nuint)0);
        (LowBit4TagPolicy.TagMask | LowBit4TagPolicy.PointerMask).Should().Be(nuint.MaxValue);
        LowBit4TagPolicy.AlignmentRequirement.Should().Be((nuint)16);
        LowBit4TagPolicy.Pack(0xFF).Should().Be((nuint)0xF);
        LowBit4TagPolicy.Unpack(0xABCD).Should().Be((byte)0xD);
    }

    [Fact]
    public void HighBit16_PartitionsTheWord()
    {
        HighBit16TagPolicy.TagMask.Should().Be(unchecked((nuint)0xFFFF_0000_0000_0000UL));
        (HighBit16TagPolicy.TagMask & HighBit16TagPolicy.PointerMask).Should().Be((nuint)0);
        (HighBit16TagPolicy.TagMask | HighBit16TagPolicy.PointerMask).Should().Be(nuint.MaxValue);
        HighBit16TagPolicy.TagShift.Should().Be(48);
        HighBit16TagPolicy.AlignmentRequirement.Should().Be((nuint)1);
    }

    [Fact]
    public void HighBit16_PackUnpackRoundTrips()
    {
        HighBit16TagPolicy.Pack(0xABCD).Should().Be(unchecked((nuint)0xABCD_0000_0000_0000UL));
        HighBit16TagPolicy.Unpack(unchecked((nuint)0xABCD_0000_0000_0042UL)).Should().Be((ushort)0xABCD);
        HighBit16TagPolicy.Unpack(HighBit16TagPolicy.Pack(0)).Should().Be((ushort)0);
    }
}
