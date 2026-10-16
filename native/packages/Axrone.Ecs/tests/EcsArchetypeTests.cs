namespace Axrone.Ecs.Tests;

public sealed class EcsArchetypeTests : IDisposable
{
    private readonly ArchetypeGraph _graph = new();

    public void Dispose()
    {
        _graph.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Root_IsEmpty()
    {
        _graph.Count.Should().Be(1);
        Archetype root = _graph.GetArchetype(new ArchetypeId(0));
        root.Mask.IsEmpty.Should().BeTrue();
        root.Count.Should().Be(0);
    }

    [Fact]
    public void GetOrCreate_Deduplicates_ByMask()
    {
        ComponentRegistry.Register<EcsTestPosition>();
        ComponentMask mask = ComponentType<EcsTestPosition>.Mask;

        Archetype first = _graph.GetOrCreate(mask, m => [ComponentRegistry.Create(ComponentType<EcsTestPosition>.Id, 4)]);
        Archetype second = _graph.GetOrCreate(mask, m => [ComponentRegistry.Create(ComponentType<EcsTestPosition>.Id, 4)]);

        first.Id.Should().Be(second.Id);
        _graph.Count.Should().Be(2);
    }

    [Fact]
    public void Allocate_RemoveRow_MovesLast()
    {
        ComponentRegistry.Register<EcsTestPosition>();
        Archetype arch = _graph.GetOrCreate(
            ComponentType<EcsTestPosition>.Mask,
            m => [ComponentRegistry.Create(ComponentType<EcsTestPosition>.Id, 4)]);

        var e0 = new Entity(new EntityId(1), new EntityVersion(1));
        var e1 = new Entity(new EntityId(2), new EntityVersion(1));
        arch.AllocateRow(e0).Should().Be(0);
        arch.AllocateRow(e1).Should().Be(1);

        arch.GetColumn<EcsTestPosition>()!.Set(0, new EcsTestPosition(1.0f, 0.0f));
        arch.GetColumn<EcsTestPosition>()!.Set(1, new EcsTestPosition(2.0f, 0.0f));

        arch.RemoveRow(0, out Entity moved).Should().BeTrue();
        moved.Should().Be(e1);
        arch.Count.Should().Be(1);
        arch.Entities[0].Should().Be(e1);
        arch.GetColumn<EcsTestPosition>()!.Get(0).X.Should().Be(2.0f);

        arch.GetColumn<EcsTestVelocity>().Should().BeNull();
        arch.GetColumn(new ComponentId(200)).Should().BeNull();
    }
}
