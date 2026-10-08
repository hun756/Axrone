namespace Axrone.Geometry.Tests;

using Xunit;
using FluentAssertions;

public class TreeOptionsTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        var options = new TreeOptions();

        options.FatteningMargin.Should().Be(0.1F);
        options.VelocityMultiplier.Should().Be(2.0F);
        options.InitialCapacity.Should().Be(new TreeCapacity(256));
        options.InitialCapacity.Value.Should().Be(256);
    }

    [Fact]
    public void NegativeMargin_Throws()
    {
        Action act = () => _ = new TreeOptions { FatteningMargin = -0.5F };

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithParameterName("FatteningMargin")
            .WithMessage("Margin value must be non-negative. (Parameter 'FatteningMargin')");
    }

    [Fact]
    public void NegativeMultiplier_Throws()
    {
        Action act = () => _ = new TreeOptions { VelocityMultiplier = -1F };

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithParameterName("VelocityMultiplier")
            .WithMessage("Velocity multiplier must be non-negative. (Parameter 'VelocityMultiplier')");
    }

    [Fact]
    public void SmallCapacity_Throws()
    {
        Action act = () => _ = new TreeCapacity(8);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithParameterName("InitialCapacity");
    }

    [Fact]
    public void ExplicitConstructor_RoundTripsValues()
    {
        var capacity = new TreeCapacity(64);
        var options = new TreeOptions(0.25F, 3.5F, capacity);

        options.FatteningMargin.Should().Be(0.25F);
        options.VelocityMultiplier.Should().Be(3.5F);
        options.InitialCapacity.Should().Be(capacity);
        options.InitialCapacity.Value.Should().Be(64);
    }
}
