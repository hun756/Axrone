namespace Axrone.Ecs.Tests;

public sealed class EcsCommandTests : IDisposable
{
    private readonly World _world;

    public EcsCommandTests()
    {
        ComponentRegistry.Register<EcsTestPosition>();
        _world = new World();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Playback_Despawns_AndDrains()
    {
        Entity keep = _world.BeginEntity().With(new EcsTestPosition(1.0f, 0.0f)).Commit();
        Entity gone = _world.BeginEntity().With(new EcsTestPosition(2.0f, 0.0f)).Commit();

        var buffer = new EntityCommandBuffer();
        buffer.Enqueue(EntityCommand.Spawn(keep));
        buffer.Enqueue(EntityCommand.Despawn(gone));
        buffer.Count.Should().Be(2);

        buffer.Playback(_world);
        buffer.Count.Should().Be(0);

        _world.Entities.AliveCount.Should().Be(1);
        _world.HasComponent<EcsTestPosition>(keep).Should().BeTrue();
        _world.HasComponent<EcsTestPosition>(gone).Should().BeFalse();
    }

    [Fact]
    public void PaddedCounter_Increments_AndResets()
    {
        var counter = new PaddedAtomicLong();
        counter.Value.Should().Be(0);
        counter.Increment().Should().Be(1);
        counter.Value.Should().Be(1);
        counter.Reset();
        counter.Value.Should().Be(0);
    }
}
