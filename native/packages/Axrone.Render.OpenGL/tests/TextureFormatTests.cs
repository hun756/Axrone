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

    [Fact]
    public void Formats_Get_R8_ReturnsValidFormatInfo()
    {
        TextureFormatInfo info = TextureFormats.Get(TextureFormat.R8);

        info.InternalFormat.Should().BeGreaterThan(0u);
        info.BytesPerPixel.Should().Be(1);
        info.IsCompressed.Should().BeFalse();
    }

    [Fact]
    public void Formats_Get_Rgba8_HasCorrectBytesPerPixel()
    {
        TextureFormats.Get(TextureFormat.Rgba8).BytesPerPixel.Should().Be(4);
    }

    [Fact]
    public void Formats_Get_Depth24Stencil8_IsDepthAndStencil()
    {
        TextureFormatInfo info = TextureFormats.Get(TextureFormat.Depth24Stencil8);

        info.IsDepth.Should().BeTrue();
        info.IsStencil.Should().BeTrue();
    }

    [Fact]
    public void Formats_Get_Rgba16f_IsFloatFormat()
    {
        TextureFormats.Get(TextureFormat.Rgba16f).IsFloat.Should().BeTrue();
    }

    [Fact]
    public void Formats_Get_UnknownFormat_ThrowsCodedException()
    {
        Action get = () => TextureFormats.Get((TextureFormat)9999);

        get.Should().Throw<GLTextureException>()
            .Where(ex => ex.TextureCode == TextureErrorCode.FormatNotSupported);
    }

    [Fact]
    public void Formats_MaxMipLevels_ScalesWithSize()
    {
        TextureFormats.MaxMipLevels(1024, 1024).Should().Be(11);
        TextureFormats.MaxMipLevels(256, 256).Should().Be(9);
        TextureFormats.MaxMipLevels(1, 1).Should().Be(1);
    }

    [Fact]
    public void FormatInfo_R8_IsFilterableRenderTarget()
    {
        TextureFormatInfo info = TextureFormats.Get(TextureFormat.R8);

        info.IsFilterable.Should().BeTrue();
        info.IsRenderTarget.Should().BeTrue();
    }

    [Fact]
    public void FormatInfo_Rgba8_IsFilterable()
    {
        TextureFormats.Get(TextureFormat.Rgba8).IsFilterable.Should().BeTrue();
    }

    [Fact]
    public void FormatInfo_Rgba16f_IsRenderTarget()
    {
        TextureFormats.Get(TextureFormat.Rgba16f).IsRenderTarget.Should().BeTrue();
    }

    [Fact]
    public void FormatInfo_ComputeMipByteSize_Rgba8_CorrectSize()
    {
        TextureFormats.Get(TextureFormat.Rgba8).ComputeMipByteSize(256, 256).Should().Be(256 * 256 * 4);
    }

    [Fact]
    public void Formats_Get_R16f_IsFloatFormat()
    {
        TextureFormats.Get(TextureFormat.R16f).IsFloat.Should().BeTrue();
    }

    [Fact]
    public void Formats_Get_Depth16_IsDepthOnly()
    {
        TextureFormatInfo info = TextureFormats.Get(TextureFormat.Depth16);

        info.IsDepth.Should().BeTrue();
        info.IsStencil.Should().BeFalse();
    }
}
