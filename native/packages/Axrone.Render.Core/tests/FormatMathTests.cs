namespace Axrone.Render.Core.Tests;

using Xunit;
using FluentAssertions;

public class FormatMathTests
{
    [Fact]
    public void Registry_ResolvesKnownFormats()
    {
        GLFormatRegistry.IsFormatSupported("rgba8").Should().BeTrue();
        GLFormatRegistry.IsFormatSupported("nope").Should().BeFalse();
        GLFormatRegistry.GetFormat("rgba8").Should().NotBeNull();
        GLFormatRegistry.SupportedFormats.Should().NotBeEmpty();

        Action unknown = () => GLFormatRegistry.GetFormat("nope");
        unknown.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BufferMath_AlignsUp()
    {
        BufferMath.AlignTo(0, 16).Should().Be(0);
        BufferMath.AlignTo(1, 16).Should().Be(16);
        BufferMath.AlignTo(16, 16).Should().Be(16);
        BufferMath.AlignTo(17, 16).Should().Be(32);
        BufferMath.IsAligned(32, 16).Should().BeTrue();
        BufferMath.IsPowerOf2(16).Should().BeTrue();
        BufferMath.NextPowerOf2(17).Should().Be(32);
    }
}
