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

    [Fact]
    public void QueryBounds_FindsInsideOnly()
    {
        _grid.Insert(ParticleId.Create(1, 1), new Vec3(1, 1, 1));
        _grid.Insert(ParticleId.Create(2, 1), new Vec3(100, 100, 100));

        var state = new CountState();
        _grid.QueryBounds<CountingVisitor, CountState>(new ParticleBounds(Vec3.Zero, new Vec3(10, 10, 10)), ref state);
        state.Count.Should().Be(1);
    }

    [Fact]
    public void QueryNearest_ReturnsSorted()
    {
        _grid.Insert(ParticleId.Create(1, 1), new Vec3(5, 0, 0));
        _grid.Insert(ParticleId.Create(2, 1), new Vec3(1, 0, 0));
        _grid.Insert(ParticleId.Create(3, 1), new Vec3(50, 0, 0));

        Span<ParticleId> results = stackalloc ParticleId[2];
        int found = _grid.QueryNearest(new Vec3(0, 0, 0), 2, 100.0f, results);

        found.Should().Be(2);
        results[0].Index.Should().Be(2u);
        results[1].Index.Should().Be(1u);
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
