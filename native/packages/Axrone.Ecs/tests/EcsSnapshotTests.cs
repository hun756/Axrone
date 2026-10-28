namespace Axrone.Ecs.Tests;

using System.Buffers;

internal struct PositionStableId : IComponentStableId
{
    public static ushort Value => 101;
}

internal struct VelocityStableId : IComponentStableId
{
    public static ushort Value => 102;
}

internal struct HealthStableId : IComponentStableId
{
    public static ushort Value => 103;
}

internal struct DuplicatePositionStableId : IComponentStableId
{
    public static ushort Value => 101;
}

public sealed class EcsSnapshotTests
{
    public EcsSnapshotTests()
    {
        ComponentCodecRegistry.Clear();
        ComponentCodecRegistry.Register<EcsTestPosition, BlittableCodec<EcsTestPosition, PositionStableId>>();
        ComponentCodecRegistry.Register<EcsTestVelocity, BlittableCodec<EcsTestVelocity, VelocityStableId>>();
        ComponentCodecRegistry.Register<EcsTestHealth, BlittableCodec<EcsTestHealth, HealthStableId>>();
    }

    [Fact]
    public void RoundTripPreservesComponentsHandlesAndTick()
    {
        using var source = new World();
        Entity complete = source.BeginEntity()
            .With(new EcsTestPosition(1.0f, 2.0f))
            .With(new EcsTestVelocity(3.0f))
            .With(new EcsTestHealth(10.0f))
            .Commit();
        Entity partial = source.BeginEntity()
            .With(new EcsTestPosition(4.0f, 5.0f))
            .With(new EcsTestHealth(20.0f))
            .Commit();
        Entity empty = source.BeginEntity().Commit();
        Entity velocityOnly = source.BeginEntity().With(new EcsTestVelocity(6.0f)).Commit();
        source.Step();
        source.Step();

        byte[] snapshot = WorldSnapshotCodec.Save(source);
        using var target = new World();
        WorldSnapshotRestoreResult result = WorldSnapshotCodec.Restore(target, snapshot);

        result.EntityCount.Should().Be(4);
        result.ArchetypeCount.Should().Be(4);
        result.Tick.Should().Be(2);
        target.Tick.Should().Be(2);
        target.Entities.AliveCount.Should().Be(4);

        Entity restoredComplete = RestoredFor(result, complete);
        target.GetComponent<EcsTestPosition>(restoredComplete).X.Should().BeApproximately(1.0f, 1e-4f);
        target.GetComponent<EcsTestPosition>(restoredComplete).Y.Should().BeApproximately(2.0f, 1e-4f);
        target.GetComponent<EcsTestVelocity>(restoredComplete).VX.Should().BeApproximately(3.0f, 1e-4f);
        target.GetComponent<EcsTestHealth>(restoredComplete).Hp.Should().BeApproximately(10.0f, 1e-4f);

        Entity restoredPartial = RestoredFor(result, partial);
        target.HasComponent<EcsTestVelocity>(restoredPartial).Should().BeFalse();
        target.GetComponent<EcsTestPosition>(restoredPartial).X.Should().BeApproximately(4.0f, 1e-4f);
        target.GetComponent<EcsTestHealth>(restoredPartial).Hp.Should().BeApproximately(20.0f, 1e-4f);

        Entity restoredEmpty = RestoredFor(result, empty);
        target.HasComponent<EcsTestPosition>(restoredEmpty).Should().BeFalse();
        target.HasComponent<EcsTestVelocity>(restoredEmpty).Should().BeFalse();
        target.HasComponent<EcsTestHealth>(restoredEmpty).Should().BeFalse();

        Entity restoredVelocity = RestoredFor(result, velocityOnly);
        target.GetComponent<EcsTestVelocity>(restoredVelocity).VX.Should().BeApproximately(6.0f, 1e-4f);
    }

    [Fact]
    public void SaveIsByteDeterministic()
    {
        using var world = new World();
        for (int index = 0; index < 20; index++)
        {
            _ = world.BeginEntity()
                .With(new EcsTestPosition(index, 0.0f))
                .With(new EcsTestHealth(10.0f + index))
                .Commit();
        }

        byte[] first = WorldSnapshotCodec.Save(world);
        byte[] second = WorldSnapshotCodec.Save(world);
        second.Should().Equal(first);
    }

    [Fact]
    public void RestoreIsDeterministicAcrossTargets()
    {
        using var source = new World();
        _ = source.BeginEntity().With(new EcsTestPosition(1.0f, 0.0f)).With(new EcsTestVelocity(2.0f)).Commit();
        _ = source.BeginEntity().With(new EcsTestHealth(3.0f)).Commit();
        byte[] snapshot = WorldSnapshotCodec.Save(source);

        using var first = new World();
        using var second = new World();
        WorldSnapshotRestoreResult firstResult = WorldSnapshotCodec.Restore(first, snapshot);
        WorldSnapshotRestoreResult secondResult = WorldSnapshotCodec.Restore(second, snapshot);

        firstResult.RestoredEntities.Span.ToArray().Should().Equal(secondResult.RestoredEntities.Span.ToArray());
        firstResult.SourceEntities.Span.ToArray().Should().Equal(secondResult.SourceEntities.Span.ToArray());
    }

    [Fact]
    public void RestoreRejectsNonEmptyWorld()
    {
        using var source = new World();
        _ = source.BeginEntity().With(new EcsTestPosition(1.0f, 0.0f)).Commit();
        byte[] snapshot = WorldSnapshotCodec.Save(source);

        using var target = new World();
        _ = target.BeginEntity().Commit();
        Action restore = () => WorldSnapshotCodec.Restore(target, snapshot);
        restore.Should().Throw<EcsConfigurationException>().WithMessage("*empty world*");
    }

    [Fact]
    public void MissingCodecFailsSave()
    {
        ComponentRegistry.Register<EcsTestArmor>();
        using var world = new World();
        _ = world.BeginEntity().With(new EcsTestArmor(1.0f)).Commit();

        Action save = () => WorldSnapshotCodec.Save(world);
        save.Should().Throw<EcsConfigurationException>().WithMessage("*no registered snapshot codec*");
    }

    [Fact]
    public void UnknownStableCodecFailsRestore()
    {
        using var source = new World();
        _ = source.BeginEntity().With(new EcsTestPosition(1.0f, 0.0f)).Commit();
        byte[] snapshot = WorldSnapshotCodec.Save(source);

        ComponentCodecRegistry.Clear();
        using var target = new World();
        Action restore = () => WorldSnapshotCodec.Restore(target, snapshot);
        restore.Should().Throw<EcsConfigurationException>().WithMessage("*no registered codec*");
    }

    [Fact]
    public void ChecksumMismatchFailsRestore()
    {
        using var source = new World();
        _ = source.BeginEntity().With(new EcsTestPosition(1.0f, 0.0f)).Commit();
        byte[] snapshot = WorldSnapshotCodec.Save(source);
        snapshot[^8] ^= 0xFF;

        using var target = new World();
        Action restore = () => WorldSnapshotCodec.Restore(target, snapshot);
        restore.Should().Throw<EcsConfigurationException>().WithMessage("*checksum*");
    }

    [Fact]
    public void TruncatedPayloadFailsRestore()
    {
        using var source = new World();
        _ = source.BeginEntity().With(new EcsTestPosition(1.0f, 0.0f)).Commit();
        byte[] snapshot = WorldSnapshotCodec.Save(source);
        byte[] truncated = snapshot.AsSpan(0, snapshot.Length - 8).ToArray();

        using var target = new World();
        Action restore = () => WorldSnapshotCodec.Restore(target, truncated);
        restore.Should().Throw<EcsConfigurationException>();
    }

    [Fact]
    public void DuplicateStableIdFailsRegistration()
    {
        Action register = () =>
            ComponentCodecRegistry.Register<EcsTestMana, BlittableCodec<EcsTestMana, DuplicatePositionStableId>>();
        register.Should().Throw<EcsConfigurationException>().WithMessage("*already registered*");
    }

    [Fact]
    public void BlittableCodecUsesTheDeclaredStableId()
    {
        BlittableCodec<EcsTestPosition, PositionStableId>.StableId.Should().Be((ushort)101);
        ArrayBufferWriter<byte> writer = new();
        BlittableCodec<EcsTestPosition, PositionStableId>.Write(writer, new EcsTestPosition(2.0f, 3.0f));
        BlittableCodec<EcsTestPosition, PositionStableId>
            .TryRead(writer.WrittenSpan, out EcsTestPosition value, out int consumed)
            .Should()
            .BeTrue();
        consumed.Should().Be(writer.WrittenCount);
        value.X.Should().BeApproximately(2.0f, 1e-4f);
        value.Y.Should().BeApproximately(3.0f, 1e-4f);
    }

    private static Entity RestoredFor(WorldSnapshotRestoreResult result, Entity source)
    {
        ReadOnlySpan<Entity> sources = result.SourceEntities.Span;
        ReadOnlySpan<Entity> restored = result.RestoredEntities.Span;
        for (int index = 0; index < sources.Length; index++)
        {
            if (sources[index] == source)
            {
                return restored[index];
            }
        }

        throw new InvalidOperationException("Source entity was not present in the snapshot.");
    }
}
