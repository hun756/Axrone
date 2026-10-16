namespace Axrone.Ecs.Tests;

using Axrone.Collections;

internal readonly struct MoveJob : IJobPolicy<EcsTestPosition, EcsTestVelocity>
{
    public static void Execute(Entity entity, ref EcsTestPosition c1, ref EcsTestVelocity c2)
    {
        _ = entity;
        c1 = new EcsTestPosition(c1.X + c2.VX, c1.Y);
    }
}

internal readonly struct SumPositions : ISpanVisitor<EcsTestPosition, float>
{
    public void Visit(ReadOnlySpan<EcsTestPosition> batch, ref float state)
    {
        foreach (EcsTestPosition p in batch)
        {
            state += p.X;
        }
    }
}

internal readonly struct CountPositions : ISlotConsumer<EcsTestPosition, int>
{
    public void Consume(in EcsTestPosition item, ref int state)
    {
        _ = item;
        state++;
    }
}

internal readonly struct CollectEntities : IEntityVisitor<List<Entity>>
{
    public static void Visit(Entity entity, ref List<Entity> context) => context.Add(entity);
}

public sealed class EcsQueryTests : IDisposable
{
    private readonly World _world;

    public EcsQueryTests()
    {
        ComponentRegistry.Register<EcsTestPosition>();
        ComponentRegistry.Register<EcsTestVelocity>();
        _world = new World();

        for (int i = 0; i < 5; i++)
        {
            _world.BeginEntity()
                .With(new EcsTestPosition(i, 0.0f))
                .With(new EcsTestVelocity(1.0f))
                .Commit();
        }

        _world.BeginEntity().With(new EcsTestPosition(100.0f, 0.0f)).Commit();
    }

    public void Dispose()
    {
        _world.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Query1_Iterates_MatchingChunks()
    {
        int rows = 0;
        foreach (Chunk1<EcsTestPosition> chunk in _world.Query<EcsTestPosition>())
        {
            rows += chunk.Length;
        }

        rows.Should().Be(6);
    }

    [Fact]
    public void Query2_Filters_BothComponents()
    {
        int rows = 0;
        foreach (Chunk2<EcsTestPosition, EcsTestVelocity> chunk in _world.Query<EcsTestPosition, EcsTestVelocity>())
        {
            rows += chunk.Length;
            chunk.Column2.Length.Should().BeGreaterThanOrEqualTo(chunk.Length);
        }

        rows.Should().Be(5);
    }

    [Fact]
    public void Run_Executes_JobPolicy()
    {
        _world.Run<MoveJob, EcsTestPosition, EcsTestVelocity>();

        float sum = 0.0f;
        foreach (Chunk1<EcsTestPosition> chunk in _world.Query<EcsTestPosition>())
        {
            foreach (EcsTestPosition p in chunk.Column1[..chunk.Length])
            {
                sum += p.X;
            }
        }

        sum.Should().BeApproximately(115.0f, 1e-4f);
    }

    [Fact]
    public void RunSpan_Drives_SharedVisitor()
    {
        var visitor = new SumPositions();
        float sum = 0.0f;
        _world.RunSpan<SumPositions, float, EcsTestPosition>(ref visitor, ref sum);
        sum.Should().BeApproximately(110.0f, 1e-4f);
    }

    [Fact]
    public void RunItem_Drives_SlotConsumer()
    {
        var consumer = new CountPositions();
        int count = 0;
        _world.RunItem<CountPositions, int, EcsTestPosition>(ref consumer, ref count);
        count.Should().Be(6);
    }

    [Fact]
    public void QueryEntities_Collects_WithContext()
    {
        var found = new List<Entity>();
        _world.QueryEntities<CollectEntities, List<Entity>>(ComponentType<EcsTestVelocity>.Mask, ref found);
        found.Should().HaveCount(5);
    }
}
