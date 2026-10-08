namespace Axrone.Render.Core.Tests;

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Axrone.Numeric;

public class ClearColorValueTests
{
    [Fact]
    public void Packet_Is20Bytes()
    {
        Marshal.SizeOf<ClearColorValue>().Should().Be(20);
    }

    [Fact]
    public void Default_EqualsZeroFloatClear()
    {
        ClearColorValue none = default;
        ClearColorValue zero = new(0f, 0f, 0f, 0f);

        none.Format.Should().Be(ClearColorFormat.Float32);
        none.UR.Should().Be(0u);
        none.IR.Should().Be(0);

        none.Should().Be(zero);
        zero.Should().Be(none);
        (none == zero).Should().BeTrue();
        (none != zero).Should().BeFalse();
        none.GetHashCode().Should().Be(zero.GetHashCode());
    }

    [Fact]
    public void FloatCtor_SanitizesNonFiniteLanes()
    {
        ClearColorValue value = new(float.NaN, float.PositiveInfinity, float.NegativeInfinity, -float.NaN);

        value.Format.Should().Be(ClearColorFormat.Float32);
        value.R.Should().Be(0f);
        value.G.Should().Be(float.MaxValue);
        value.B.Should().Be(-float.MaxValue);
        value.A.Should().Be(0f);
    }

    [Fact]
    public void VectorCtor_SanitizesLikeTheFloatCtor()
    {
        var vector = new Vec4(float.PositiveInfinity, float.NaN, -float.PositiveInfinity, 0.5f);

        ClearColorValue value = new(vector);

        value.Format.Should().Be(ClearColorFormat.Float32);
        value.R.Should().Be(float.MaxValue);
        value.G.Should().Be(0f);
        value.B.Should().Be(-float.MaxValue);
        value.A.Should().Be(0.5f);
    }

    [Fact]
    public void FloatCtor_KeepsFiniteLaneBits()
    {
        ClearColorValue value = new(-0.0f, float.MinValue, float.Epsilon, float.MaxValue);

        // -0 is not 0 bit-for-bit, and both survive the sanitizer unchanged.
        BitConverter.SingleToInt32Bits(value.R).Should().Be(BitConverter.SingleToInt32Bits(-0.0f));
        value.IR.Should().Be(unchecked((int)0x80000000u));
        value.G.Should().Be(float.MinValue);
        value.B.Should().Be(float.Epsilon);
        value.A.Should().Be(float.MaxValue);
    }

    [Fact]
    public void IntegerFactories_KeepExactBits()
    {
        ClearColorValue signed = ClearColorValue.FromInt32(int.MinValue, int.MaxValue, -1, 0);
        signed.Format.Should().Be(ClearColorFormat.Int32);
        signed.IR.Should().Be(int.MinValue);
        signed.IG.Should().Be(int.MaxValue);
        signed.IB.Should().Be(-1);
        signed.IA.Should().Be(0);

        ClearColorValue unsigned = ClearColorValue.FromUInt32(uint.MaxValue, 0x80000000u, 1u, 0u);
        unsigned.Format.Should().Be(ClearColorFormat.Uint32);
        unsigned.UR.Should().Be(uint.MaxValue);
        unsigned.UG.Should().Be(0x80000000u);
        unsigned.UB.Should().Be(1u);
        unsigned.UA.Should().Be(0u);

        // Integer views cross-read as the same raw bits, not as converted numbers.
        signed.UR.Should().Be(unchecked((uint)int.MinValue));
        unsigned.IR.Should().Be(unchecked((int)uint.MaxValue));
    }

    [Fact]
    public void Views_ShareTheSameBytes()
    {
        ClearColorValue value = ClearColorValue.FromInt32(0x01020304, 0, 0, 0);

        value.IR.Should().Be(0x01020304);
        value.UR.Should().Be(0x01020304u);
        value.R.Should().Be(BitConverter.Int32BitsToSingle(0x01020304));
        value.R.Should().NotBe(0f);
    }

    [Fact]
    public void Spans_ExposeTheSamePayloadInEveryView()
    {
        ClearColorValue value = ClearColorValue.FromInt32(1, -2, 3, int.MinValue);

        ReadOnlySpan<int> ints = value.AsInt32Span();
        ints.Length.Should().Be(4);
        ints[0].Should().Be(1);
        ints[1].Should().Be(-2);
        ints[2].Should().Be(3);
        ints[3].Should().Be(int.MinValue);

        ReadOnlySpan<uint> uints = value.AsUInt32Span();
        uints.Length.Should().Be(4);
        uints[0].Should().Be(1u);
        uints[1].Should().Be(unchecked((uint)-2));
        uints[2].Should().Be(3u);
        uints[3].Should().Be(unchecked((uint)int.MinValue));

        ReadOnlySpan<float> floats = value.AsFloatSpan();
        floats.Length.Should().Be(4);
        floats[0].Should().Be(BitConverter.Int32BitsToSingle(1));
        floats[1].Should().Be(BitConverter.Int32BitsToSingle(-2));
        floats[3].Should().Be(BitConverter.Int32BitsToSingle(int.MinValue));
    }

    [Fact]
    public void Spans_AliasTheValueInsteadOfCopyingIt()
    {
        ClearColorValue[] storage = new ClearColorValue[1];
        storage[0] = ClearColorValue.FromInt32(1, 2, 3, 4);

        ReadOnlySpan<int> ints = storage[0].AsInt32Span();

        // Same address, so the future glClearBufferiv path hands GL the caller's own
        // payload instead of a compiler-inserted defensive copy.
        unsafe
        {
            nint spanAddress = (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(ints));
            nint valueAddress = (nint)Unsafe.AsPointer(ref storage[0]);
            spanAddress.Should().Be(valueAddress);
        }
    }

    [Fact]
    public void FloatRoundTrip_IsBitIdentical()
    {
        ClearColorValue value = new(0.1f, -0.2f, 1f / 3f, float.Epsilon);

        BitConverter.SingleToInt32Bits(value.R).Should().Be(BitConverter.SingleToInt32Bits(0.1f));
        BitConverter.SingleToInt32Bits(value.B).Should().Be(BitConverter.SingleToInt32Bits(1f / 3f));
        BitConverter.SingleToInt32Bits(value.A).Should().Be(BitConverter.SingleToInt32Bits(float.Epsilon));
        value.AsFloatSpan()[1].Should().Be(-0.2f);

        // Rebuilding from the read-back lanes reproduces the same value bit for bit.
        new ClearColorValue(value.R, value.G, value.B, value.A).Should().Be(value);
    }

    [Fact]
    public void CrossTag_SameBitsHashEqualButAreNotEqual()
    {
        ClearColorValue signed = ClearColorValue.FromInt32(1, 2, 3, 4);
        ClearColorValue unsigned = ClearColorValue.FromUInt32(1u, 2u, 3u, 4u);

        signed.UR.Should().Be(unsigned.UR);
        signed.UG.Should().Be(unsigned.UG);
        signed.UB.Should().Be(unsigned.UB);
        signed.UA.Should().Be(unsigned.UA);

        // The hash is a pure function of the 16 payload bytes; the tag is not part of it.
        signed.GetHashCode().Should().Be(unsigned.GetHashCode());

        // Equality is stricter than the hash: the differing tag still separates them.
        (signed == unsigned).Should().BeFalse();
        (signed != unsigned).Should().BeTrue();
        signed.Equals(unsigned).Should().BeFalse();
        signed.Equals((object)unsigned).Should().BeFalse();
    }

    [Fact]
    public void Equality_IsBitwiseSoNaNLanesCompareEqual()
    {
        ClearColorValue quiet = ClearColorValue.FromUInt32(0x7FC00000u, 0u, 0u, 0u);
        ClearColorValue sameQuiet = ClearColorValue.FromUInt32(0x7FC00000u, 0u, 0u, 0u);
        ClearColorValue negative = ClearColorValue.FromUInt32(0xFFC00000u, 0u, 0u, 0u);

        quiet.R.Should().BeNaN();
        negative.R.Should().BeNaN();

        // A float comparison would call this pair unequal; a bitwise one does not.
        (quiet == sameQuiet).Should().BeTrue();
        quiet.GetHashCode().Should().Be(sameQuiet.GetHashCode());

        // Different NaN payload, still a different value.
        (quiet == negative).Should().BeFalse();
    }

    [Fact]
    public void ToString_UsesTheTaggedView()
    {
        new ClearColorValue(0.1f, 0.2f, 0.3f, 0.4f).ToString().Should().Be("Float32(0.1, 0.2, 0.3, 0.4)");
        ClearColorValue.FromInt32(-1, 2, -3, 4).ToString().Should().Be("Int32(-1, 2, -3, 4)");
        ClearColorValue.FromUInt32(uint.MaxValue, 0u, 0u, 1u).ToString().Should().Be("Uint32(4294967295, 0, 0, 1)");
    }

    [Fact]
    public void Equals_RejectsNullAndForeignTypes()
    {
        ClearColorValue value = new(1f, 2f, 3f, 4f);

        value.Equals((object)value).Should().BeTrue();
        value.Equals(null).Should().BeFalse();
        value.Equals("Float32(1, 2, 3, 4)").Should().BeFalse();
        value.GetHashCode().Should().Be(new ClearColorValue(1f, 2f, 3f, 4f).GetHashCode());
    }

    [Fact]
    public void PayloadAndTag_AreNotCallerSettable()
    {
        typeof(ClearColorValue).GetFields(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();

        PropertyInfo format = typeof(ClearColorValue).GetProperty(nameof(ClearColorValue.Format))!;
        format.SetMethod.Should().BeNull();
        format.CanWrite.Should().BeFalse();
    }
}
