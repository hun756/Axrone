namespace Axrone.Ecs.Tests;

public sealed class EcsWorldTests : IDisposable
{
    private readonly World _world;

    public EcsWorldTests()
    {
        ComponentRegistry.Register<EcsTestPosition>();
        ComponentRegistry.Register<EcsTestVelocity>();
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Begin_With_Commit_Creates_Entity()
    {
        Entity entity = _world.BeginEntity()
            .With(new EcsTestPosition(1.0f, 2.0f))
            .With(new EcsTestVelocity(3.0f))
            .Commit();

        entity.IsNull.Should().BeFalse();
        _world.HasComponent<EcsTestPosition>(entity).Should().BeTrue();
        _world.HasComponent<EcsTestVelocity>(entity).Should().BeTrue();
        _world.GetComponent<EcsTestPosition>(entity).X.Should().Be(1.0f);
        _world.FrameCount.Should().Be(0);
        _world.Step();
        _world.FrameCount.Should().Be(1);
    }

    [Fact]
    public void Add_Replace_Moves_Archetype()
    {
        Entity entity = _world.BeginEntity().With(new EcsTestPosition(1.0f, 0.0f)).Commit();
        _world.Archetypes.Count.Should().Be(2);

        _world.AddComponent(entity, new EcsTestVelocity(5.0f));
        _world.Archetypes.Count.Should().Be(3);
        _world.GetComponent<EcsTestVelocity>(entity).VX.Should().Be(5.0f);
        _world.GetComponent<EcsTestPosition>(entity).X.Should().Be(1.0f);

        _world.AddComponent(entity, new EcsTestPosition(9.0f, 0.0f));
        _world.Archetypes.Count.Should().Be(3);
        _world.GetComponent<EcsTestPosition>(entity).X.Should().Be(9.0f);
    }

    [Fact]
    public void Remove_Moves_Back()
    {
        Entity entity = _world.BeginEntity()
            .With(new EcsTestPosition(1.0f, 0.0f))
            .With(new EcsTestVelocity(2.0f))
            .Commit();

        _world.RemoveComponent<EcsTestVelocity>(entity);
        _world.HasComponent<EcsTestVelocity>(entity).Should().BeFalse();
        _world.HasComponent<EcsTestPosition>(entity).Should().BeTrue();
        _world.GetComponent<EcsTestPosition>(entity).X.Should().Be(1.0f);

        _world.RemoveComponent<EcsTestVelocity>(entity);
    }

    [Fact]
    public void Destroy_Removes_AndIgnores_Unknown()
    {
        Entity entity = _world.BeginEntity().With(new EcsTestPosition(1.0f, 0.0f)).Commit();
        _world.Entities.AliveCount.Should().Be(1);

        _world.DestroyEntity(entity);
        _world.Entities.AliveCount.Should().Be(0);
        _world.HasComponent<EcsTestPosition>(entity).Should().BeFalse();
        _world.DestroyEntity(entity);
    }

    [Fact]
    public void Unknown_Entity_Throws_Coded_Error()
    {
        var ghost = new Entity(new EntityId(999), new EntityVersion(1));
        Action get = () => _ = _world.GetComponent<EcsTestPosition>(ghost);
        get.Should().Throw<EcsEntityException>().WithMessage("*stale*");
    }

    [Fact]
    public void Diagnostics_Observe_Lifecycle()
    {
        var recorder = new LifecycleRecorder();
        using var world = new World(diagnostics: recorder);
        Entity entity = world.BeginEntity().Commit();
        world.DestroyEntity(entity);

        recorder.Created.Should().ContainSingle();
        recorder.Destroyed.Should().ContainSingle();
    }

    private sealed class LifecycleRecorder : IDiagnosticsSink
    {
        public readonly List<Entity> Created = new();
        public readonly List<Entity> Destroyed = new();

        public void OnArchetypeCreated(ArchetypeId id, in ComponentMask mask)
        {
        }

        public void OnEntityCreated(Entity entity) => Created.Add(entity);
        public void OnEntityDestroyed(Entity entity) => Destroyed.Add(entity);
    }
}
