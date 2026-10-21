namespace Axrone.Particles.Tests;

public sealed class ParticleSpatialTests : IDisposable
{
    private readonly SpatialHashGrid3D _grid = new(cellSize: 10.0f);
    private readonly NativeSoaParticleStorage _storage = new((ParticleCapacity)64);

    public void Dispose()
    {
        _grid.Dispose();
        _storage.Dispose();
    }

    [Fact]
    public void QueryRadius_FindsInsertedParticles()
    {
        var a = ParticleId.Create(1, 1);
        var b = ParticleId.Create(2, 1);
        _grid.Insert(a, new Vec3(0, 0, 0));
        _grid.Insert(b, new Vec3(100, 0, 0));

        var state = new CountState();
        _grid.QueryRadius<CountingVisitor, CountState>(new Vec3(0, 0, 0), 5.0f, ref state);
        state.Count.Should().Be(1);
    }

    [Fact]
    public void Collision_KillsBelowPlane()
    {
        _storage.TryAllocate(new Vec3(0, -1, 0), Vec3.Zero, 5.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();
        _storage.TryAllocate(new Vec3(0, 5, 0), Vec3.Zero, 5.0f, Vec3.One, Vec4.One, out _).Should().BeTrue();

        var module = new CollisionModule();
        var context = new ModuleExecutionContext(DeltaTime.FromSingle(0.016f), new SimulationTime(0), null);
        CollisionModule.Process(_storage, context, ref module);

        _storage.ActiveCount.Should().Be(1);
    }

    private struct CountState
    {
        public int Count;
    }

    private struct CountingVisitor : ISpatialVisitor<CountState>
    {
        public static bool OnFound(ParticleId id, in Vec3 position, float distanceSquared, scoped ref CountState state)
        {
            state.Count++;
            return true;
        }
    }
}
