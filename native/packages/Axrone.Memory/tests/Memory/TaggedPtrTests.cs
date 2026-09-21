namespace Axrone.Memory.Tests.Memory;

using Axrone.Memory;

public unsafe class TaggedPtrTests
{
    [Fact]
    public void PackUnpack_RoundTripsPointerAndTag()
    {
        byte* block = (byte*)NativeMemory.AlignedAlloc(64, 8);
        try
        {
            var tagged = new TaggedPtr<byte, byte, LowBit3TagPolicy>(block, 5);

            ((nuint)tagged.Pointer).Should().Be((nuint)block);
            tagged.Tag.Should().Be((byte)5);
            tagged.IsNull.Should().BeFalse();
        }
        finally
        {
            NativeMemory.AlignedFree(block);
        }
    }

    [Fact]
    public void UnalignedPointer_Throws()
    {
        byte* block = (byte*)NativeMemory.AlignedAlloc(64, 8);
        try
        {
            var act = () => new TaggedPtr<byte, byte, LowBit3TagPolicy>(block + 1, 0);

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            NativeMemory.AlignedFree(block);
        }
    }

    [Fact]
    public void WithTag_PreservesPointer()
    {
        byte* block = (byte*)NativeMemory.AlignedAlloc(64, 8);
        try
        {
            var tagged = new TaggedPtr<byte, byte, LowBit3TagPolicy>(block, 1);
            var retagged = tagged.WithTag(6);

            ((nuint)retagged.Pointer).Should().Be((nuint)block);
            retagged.Tag.Should().Be((byte)6);
            tagged.Tag.Should().Be((byte)1);
        }
        finally
        {
            NativeMemory.AlignedFree(block);
        }
    }

    [Fact]
    public void Raw_Rewrap_PreservesWord()
    {
        byte* block = (byte*)NativeMemory.AlignedAlloc(64, 8);
        try
        {
            var tagged = new TaggedPtr<byte, byte, LowBit3TagPolicy>(block, 3);
            var rewrapped = new TaggedPtr<byte, byte, LowBit3TagPolicy>(tagged.Raw);

            rewrapped.Should().Be(tagged);
            ((nuint)rewrapped).Should().Be(tagged.Raw);
        }
        finally
        {
            NativeMemory.AlignedFree(block);
        }
    }

    [Fact]
    public void HighBit_RoundTripsRealAddress()
    {
        byte* block = (byte*)NativeMemory.AlignedAlloc(64, 8);
        try
        {
            var tagged = new TaggedPtr<byte, ushort, HighBit16TagPolicy>(block, 0xABCD);

            ((nuint)tagged.Pointer).Should().Be((nuint)block);
            tagged.Tag.Should().Be((ushort)0xABCD);
        }
        finally
        {
            NativeMemory.AlignedFree(block);
        }
    }

    [Fact]
    public void NullPointer_IsNull()
    {
        var tagged = new TaggedPtr<byte, byte, LowBit3TagPolicy>((byte*)null, 0);

        tagged.IsNull.Should().BeTrue();
    }
}
