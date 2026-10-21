namespace Axrone.Particles.Tests;

public sealed class ParticleIdsTests
{
    [Fact]
    public void ParticleId_Create_RoundTripsIndexAndGeneration()
    {
        var id = ParticleId.Create(17, 3);
        id.IsValid.Should().BeTrue();
        id.Index.Should().Be(17u);
        id.Generation.Should().Be(3u);
        ParticleId.Invalid.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ParticleCapacity_Converts()
    {
        var cap = ParticleCapacity.FromInt32(1024);
        cap.ToInt32().Should().Be(1024);
        ((int)cap).Should().Be(1024);
        ((ParticleCapacity)512).Value.Should().Be(512);
    }

    [Fact]
    public void SimulationTime_Add_Advances()
    {
        var t = new SimulationTime(1.0f);
        var dt = DeltaTime.FromSingle(0.5f);
        (t + dt).Seconds.Should().BeApproximately(1.5f, 1e-6f);
        SimulationTime.Add(t, dt).Should().Be(t + dt);
        dt.ToSingle().Should().BeApproximately(0.5f, 1e-6f);
    }

    [Fact]
    public void ParticleIds_Order()
    {
        var a = ParticleId.Create(1, 1);
        var b = ParticleId.Create(2, 1);
        (a < b).Should().BeTrue();
        (b > a).Should().BeTrue();
    }
}
