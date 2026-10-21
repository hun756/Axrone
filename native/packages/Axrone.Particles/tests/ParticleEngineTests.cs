namespace Axrone.Particles.Tests;

public sealed class ParticleEngineTests
{
    [Fact]
    public void Curve_Evaluate_ConstantAndBaked()
    {
        using var constant = new Curve(CurveMode.Constant, 2.0f);
        constant.Evaluate(0.3f).Should().BeApproximately(2.0f, 1e-6f);

        using var baked = new Curve(CurveMode.Curve, 1.0f);
        baked.BakeCatmullRom([0f, 1f]);
        baked.Evaluate(0.5f).Should().BeApproximately(0.5f, 1e-2f);
    }

    [Fact]
    public void Gradient_Evaluate_Linear()
    {
        using var gradient = new Gradient(GradientMode.Gradient);
        gradient.BakeLinearGradient(Vec4.Zero, Vec4.One);
        Vec4 mid = gradient.Evaluate(0.5f);
        mid.X.Should().BeApproximately(0.5f, 1e-2f);
        mid.W.Should().BeApproximately(0.5f, 1e-2f);
    }

    [Fact]
    public void Engine_EmitsAgesAndKillsByLifetime()
    {
        var sink = new DelegateEventSink();
        int births = 0;
        int deaths = 0;
        sink.Birth = _ => births++;
        sink.Death = _ => deaths++;

        using var engine = ParticleSystemBuilder<UnconfiguredPhase>.Create()
            .WithCapacity((ParticleCapacity)64)
            .Build(
                new PipelineStage<VelocityModule, PipelineEnd>(new VelocityModule(), new PipelineEnd()),
                sink);

        engine.Play();
        engine.Emit(Vec3.Zero, Vec3.UnitY).IsValid.Should().BeTrue();
        births.Should().Be(1);

        engine.Update(DeltaTime.FromSingle(6.0f));
        deaths.Should().Be(1);
        engine.ParticleCount.Should().Be(0);
    }

    [Fact]
    public void Engine_KillParticle_RemovesImmediately()
    {
        var sink = new DelegateEventSink();
        var reasons = new List<ParticleDeathReason>();
        sink.Death = e => reasons.Add(e.Reason);

        using var engine = ParticleSystemBuilder<UnconfiguredPhase>.Create()
            .WithCapacity((ParticleCapacity)64)
            .Build(
                new PipelineStage<VelocityModule, PipelineEnd>(new VelocityModule(), new PipelineEnd()),
                sink);

        engine.Play();
        ParticleId a = engine.Emit(Vec3.Zero, Vec3.Zero);
        engine.Emit(new Vec3(5, 0, 0), Vec3.Zero);
        engine.ParticleCount.Should().Be(2);

        engine.KillParticle(a);
        engine.ParticleCount.Should().Be(1);
        reasons.Should().ContainSingle().Which.Should().Be(ParticleDeathReason.Killed);
    }

    [Fact]
    public void Engine_OutOfBounds_KillsParticle()
    {
        var sink = new DelegateEventSink();
        var reasons = new List<ParticleDeathReason>();
        sink.Death = e => reasons.Add(e.Reason);

        using var engine = ParticleSystemBuilder<UnconfiguredPhase>.Create()
            .WithCapacity((ParticleCapacity)64)
            .Build(
                new PipelineStage<VelocityModule, PipelineEnd>(new VelocityModule(), new PipelineEnd()),
                sink);

        engine.Play();
        engine.Emit(new Vec3(1000, 0, 0), Vec3.Zero);
        engine.Update(DeltaTime.FromSingle(0.016f));

        engine.ParticleCount.Should().Be(0);
        reasons.Should().ContainSingle().Which.Should().Be(ParticleDeathReason.Bounds);
    }
}
