namespace Axrone.Render.OpenGL.Tests;

using Xunit;
using FluentAssertions;

public class TextureFormatTests
{
    [Fact]
    public void CompressedFormats_ResolveWithBlockGeometry()
    {
        TextureFormatInfo bc1 = TextureFormats.Get(TextureFormat.Bc1Rgb);
        bc1.IsCompressed.Should().BeTrue();
        bc1.BlockWidth.Should().Be(4);
        bc1.BlockHeight.Should().Be(4);
        bc1.BlockSize.Should().Be(8);

        TextureFormatInfo bc3 = TextureFormats.Get(TextureFormat.Bc3Rgba);
        bc3.BlockSize.Should().Be(16);

        TextureFormatInfo astc = TextureFormats.Get(TextureFormat.Astc8x8);
        astc.BlockWidth.Should().Be(8);
        astc.BlockHeight.Should().Be(8);
        astc.BlockSize.Should().Be(16);
        astc.IsCompressed.Should().BeTrue();

        TextureFormatInfo etc = TextureFormats.Get(TextureFormat.Etc2Rgba8);
        etc.IsCompressed.Should().BeTrue();
    }

    [Fact]
    public void CompressedMipSize_UsesBlockMath()
    {
        TextureFormatInfo bc1 = TextureFormats.Get(TextureFormat.Bc1Rgb);
        bc1.ComputeMipByteSize(7, 7).Should().Be(2 * 2 * 8);

        TextureFormatInfo rgba8 = TextureFormats.Get(TextureFormat.Rgba8);
        rgba8.IsCompressed.Should().BeFalse();
        rgba8.ComputeMipByteSize(4, 4).Should().Be(4 * 4 * 4);
    }

    [Fact]
    public void FormatInfo_HasValueEquality()
    {
        TextureFormats.Get(TextureFormat.Rgba8).Should().Be(TextureFormats.Get(TextureFormat.Rgba8));
        TextureFormats.Get(TextureFormat.Rgba8).Should().NotBe(TextureFormats.Get(TextureFormat.Bc1Rgb));
    }
}
