namespace Axrone.Ecs.Tests;

public class EcsEntityStoreTests
{
    [Fact]
    public void Allocate_Resolve_RoundTrip()
    {
        var store = new EntityStore(8);
        store.AliveCount.Should().Be(0);

        Entity entity = store.Allocate(new ArchetypeId(0), 0);
        entity.Id.Value.Should().BeGreaterThan(0);
        store.AliveCount.Should().Be(1);

        EntityLookupResult resolved = store.Resolve(entity);
        resolved.Kind.Should().Be(EntityLookupResultKind.Found);
        resolved.Row.Should().Be(0);
        resolved.ArchetypeId.Should().Be(new ArchetypeId(0));
    }

    [Fact]
    public void Free_Recycles_WithBumpedVersion()
    {
        var store = new EntityStore(8);
        Entity first = store.Allocate(new ArchetypeId(0), 0);

        store.TryFree(first, out EntityRecord deleted).Should().BeTrue();
        deleted.Row.Should().Be(0);
        store.AliveCount.Should().Be(0);

        store.Resolve(first).Kind.Should().Be(EntityLookupResultKind.NotFound);

        Entity second = store.Allocate(new ArchetypeId(0), 0);
        second.Id.Should().Be(first.Id);
        second.Version.Value.Should().Be(first.Version.Value + 1);
        store.Resolve(second).Kind.Should().Be(EntityLookupResultKind.Found);
    }

    [Fact]
    public void Free_Rejects_Stale_Unknown_AndDouble()
    {
        var store = new EntityStore(8);
        Entity entity = store.Allocate(new ArchetypeId(0), 0);
        store.TryFree(entity, out _).Should().BeTrue();
        store.TryFree(entity, out _).Should().BeFalse();

        store.Resolve(new Entity(new EntityId(999), new EntityVersion(1))).Kind
            .Should().Be(EntityLookupResultKind.NotFound);
    }

    [Fact]
    public void Stale_Handle_Resolves_StaleVersion()
    {
        var store = new EntityStore(8);
        Entity first = store.Allocate(new ArchetypeId(0), 0);
        store.TryFree(first, out _).Should().BeTrue();
        Entity second = store.Allocate(new ArchetypeId(0), 0);

        store.Resolve(first).Kind.Should().Be(EntityLookupResultKind.StaleVersion);
        store.Resolve(second).Kind.Should().Be(EntityLookupResultKind.Found);
    }

    [Fact]
    public void UpdateRecord_Repoints_Row()
    {
        var store = new EntityStore(8);
        Entity entity = store.Allocate(new ArchetypeId(0), 0);
        store.UpdateRecord(entity.Id, new ArchetypeId(1), 5);

        EntityLookupResult resolved = store.Resolve(entity);
        resolved.ArchetypeId.Should().Be(new ArchetypeId(1));
        resolved.Row.Should().Be(5);
    }
}
