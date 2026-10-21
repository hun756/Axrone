namespace Axrone.Lighting.Tests;

public sealed class LightingIdsTests
{
    [Fact]
    public void LightRange_RejectsNonPositive()
    {
        Action act = () => _ = new LightRange(0.0f);
        act.Should().Throw<LightingValidationException>().WithMessage("*strictly positive*");
    }

    [Fact]
    public void LightIntensity_RejectsNegative()
    {
        Action act = () => _ = new LightIntensity(-1.0f);
        act.Should().Throw<LightingValidationException>();
    }

    [Fact]
    public void ConeCosine_RejectsOutOfRange()
    {
        Action act = () => _ = new ConeCosine(1.5f);
        act.Should().Throw<LightingValidationException>().WithMessage("*[0, 1]*");
    }

    [Fact]
    public void RigVersion_Next_Increments()
    {
        RigVersion.Initial.Next().Value.Should().Be(2UL);
        (RigVersion.Initial < RigVersion.Initial.Next()).Should().BeTrue();
    }

    [Fact]
    public void Guards_RejectBadDirection()
    {
        Action act = () => LightingGuards.ValidateAndNormalize(Vec3.Zero);
        act.Should().Throw<LightingValidationException>().WithMessage("*near-zero*");
    }

    [Fact]
    public void Environment_Defaults_MatchSpec()
    {
        LightingEnvironment.Default.Ambient.Should().Be(new Vec3(0.2f, 0.2f, 0.25f));
        LightingEnvironment.Default.Exposure.Should().Be(1.0f);
        LightingEnvironment.Default.Gamma.Should().Be(2.2f);
    }
}
