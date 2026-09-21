namespace Axrone.Event.Tests;

public class EventSourcingTests
{
    private sealed class Counter : EventSourcedAggregate<string, int>
    {
        public int Total { get; private set; }

        public Counter()
        {
        }

        public Counter(string id, int first)
        {
            SetIdentity(id);
            Emit(first);
        }

        public void Add(int amount) => Emit(amount);

        public void AddVersioned(int amount, int version) => Emit(amount, version);

        protected override void Apply(in int @event) => Total += @event;
    }

    private sealed class DoublingUpgrader : IEventUpgrader<int>
    {
        public bool CanUpgrade(int version) => version == 1;

        public int Upgrade(in int oldEvent, int oldVersion) => oldEvent * 2;
    }

    private static EventSourcedRepository<Counter, string, int> CreateRepository(
        EventBus bus,
        out InMemoryEventStore<string, int> store,
        out InMemorySnapshotStore<Counter, string> snapshots,
        int snapshotThreshold = 100)
    {
        store = new InMemoryEventStore<string, int>();
        snapshots = new InMemorySnapshotStore<Counter, string>();
        return new EventSourcedRepository<Counter, string, int>(store, bus, snapshots, snapshotThreshold);
    }

    [Fact]
    public async Task SaveLoad_RoundTripsStateAndIdentity()
    {
        using var bus = new EventBus(64);
        var repository = CreateRepository(bus, out _, out _);

        var counter = new Counter("player-1", 10);
        counter.Add(5);
        await repository.SaveAsync(counter);

        Counter? loaded = await repository.LoadAsync("player-1");

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be("player-1");
        loaded.Total.Should().Be(15);
        loaded.Version.Should().Be(2);
    }

    [Fact]
    public async Task Load_MissingStream_ReturnsNull()
    {
        using var bus = new EventBus(64);
        var repository = CreateRepository(bus, out _, out _);

        Counter? loaded = await repository.LoadAsync("ghost");

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Save_ConflictingVersion_Throws()
    {
        using var bus = new EventBus(64);
        var repository = CreateRepository(bus, out var store, out _);

        var first = new Counter("order-1", 1);
        await repository.SaveAsync(first);

        var stale = new Counter("order-1", 100);
        var act = () => repository.SaveAsync(stale).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*concurrency*");
        (await store.GetStreamVersionAsync("order-1")).Should().Be(1);
    }

    [Fact]
    public async Task Save_PublishesEnvelopes()
    {
        using var bus = new EventBus(64);
        var repository = CreateRepository(bus, out _, out _);

        var published = 0;
        using var sub = bus.Subscribe<int>((_, _) => Interlocked.Increment(ref published));

        var counter = new Counter("shop-1", 3);
        await repository.SaveAsync(counter);

        SpinWait.SpinUntil(() => Volatile.Read(ref published) == 1, TimeSpan.FromSeconds(5)).Should().BeTrue();
    }

    [Fact]
    public async Task SnapshotThreshold_SavesAndSkipsHistory()
    {
        using var bus = new EventBus(64);
        var repository = CreateRepository(bus, out var store, out var snapshots, snapshotThreshold: 2);

        var counter = new Counter("snap-1", 1);
        counter.Add(2);
        await repository.SaveAsync(counter);

        (await snapshots.TryGetSnapshotAsync("snap-1")).Version.Should().Be(2);

        Counter? loaded = await repository.LoadAsync("snap-1");
        loaded.Should().NotBeNull();
        loaded!.Total.Should().Be(3);

        (await store.GetStreamVersionAsync("snap-1")).Should().Be(2);
    }

    [Fact]
    public void UpgradePipeline_StepsVersions()
    {
        var pipeline = new EventUpgradePipeline<int>();
        pipeline.Register(new DoublingUpgrader());

        pipeline.Transform(21, version: 1).Should().Be(42);
        pipeline.Transform(21, version: 2).Should().Be(21);
    }

    [Fact]
    public async Task EmitVersion_IsStored()
    {
        using var bus = new EventBus(64);
        var store = new InMemoryEventStore<string, int>();
        var snapshots = new InMemorySnapshotStore<Counter, string>();
        var repository = new EventSourcedRepository<Counter, string, int>(store, bus, snapshots);

        var counter = new Counter("v-1", 1);
        counter.AddVersioned(5, 2);
        await repository.SaveAsync(counter);

        var history = await store.ReadStreamAsync("v-1", 1, 10);
        history.Should().HaveCount(2);
        history[0].Metadata.Version.Should().Be(1);
        history[1].Metadata.Version.Should().Be(2);
        history[1].Metadata.Sequence.Should().Be(2);
    }

    [Fact]
    public async Task Load_AppliesUpgrades()
    {
        using var bus = new EventBus(64);
        var store = new InMemoryEventStore<string, int>();
        var snapshots = new InMemorySnapshotStore<Counter, string>();
        var plain = new EventSourcedRepository<Counter, string, int>(store, bus, snapshots);

        var counter = new Counter("up-1", 21);
        await plain.SaveAsync(counter);

        var pipeline = new EventUpgradePipeline<int>();
        pipeline.Register(new DoublingUpgrader());
        var upgrading = new EventSourcedRepository<Counter, string, int>(store, bus, snapshots, 100, pipeline);

        Counter? loaded = await upgrading.LoadAsync("up-1");
        loaded.Should().NotBeNull();
        loaded!.Total.Should().Be(42);
    }
}
