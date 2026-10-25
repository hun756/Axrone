namespace Axrone.Scene.Core.Tests;

using Axrone.Scene;
using FluentAssertions;
using Xunit;

public sealed class SceneActorTableTests
{
    [Fact]
    public void AllocateFreeReuse_BumpsGeneration()
    {
        var table = new SceneActorTable(4);

        SceneActor first = table.Allocate(SceneStringId.Invalid, SceneStringId.Invalid, 0, true, false, false);
        int slot = first.Slot;
        table.LiveCount.Should().Be(1);
        table.Free(slot);
        table.LiveCount.Should().Be(0);
        table.TryResolve(first, out _).Should().BeFalse();

        SceneActor second = table.Allocate(SceneStringId.Invalid, SceneStringId.Invalid, 1, true, false, false);

        second.Slot.Should().Be(slot);
        second.Generation.Should().NotBe(first.Generation);
        table.TryResolve(second, out int resolvedSlot).Should().BeTrue();
        resolvedSlot.Should().Be(slot);
    }

    [Fact]
    public void Grow_PreservesLiveActors()
    {
        var table = new SceneActorTable(2);
        SceneActor first = table.Allocate(SceneStringId.Invalid, SceneStringId.Invalid, 0, true, false, false);
        SceneActor second = table.Allocate(SceneStringId.Invalid, SceneStringId.Invalid, 0, true, false, false);
        SceneActor third = table.Allocate(SceneStringId.Invalid, SceneStringId.Invalid, 0, true, false, false);

        table.Capacity.Should().BeGreaterThanOrEqualTo(4);
        table.TryResolve(first, out _).Should().BeTrue();
        table.TryResolve(second, out _).Should().BeTrue();
        table.TryResolve(third, out _).Should().BeTrue();
        table.LiveCount.Should().Be(3);
    }

    [Fact]
    public void Clear_InvalidatesEveryHandle()
    {
        var table = new SceneActorTable(4);
        SceneActor first = table.Allocate(SceneStringId.Invalid, SceneStringId.Invalid, 0, true, false, false);
        SceneActor second = table.Allocate(SceneStringId.Invalid, SceneStringId.Invalid, 0, true, false, false);

        table.Clear();

        table.LiveCount.Should().Be(0);
        table.TryResolve(first, out _).Should().BeFalse();
        table.TryResolve(second, out _).Should().BeFalse();
    }

    [Fact]
    public void StaleHandle_IsRejected()
    {
        var table = new SceneActorTable(4);
        SceneActor actor = table.Allocate(SceneStringId.Invalid, SceneStringId.Invalid, 0, true, false, false);
        table.Free(actor.Slot);

        Action resolve = () => table.Resolve(actor);

        resolve.Should().Throw<SceneStaleHandleException>();
    }
}
