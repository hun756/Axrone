using Axrone.Memory.Arena;

namespace Axrone.Memory.Tests.Arena;

public sealed class LifecycleDrainCoordinatorTests
{
    [Fact]
    public void NewCoordinator_IsActive()
    {
        var coord = new LifecycleDrainCoordinator();
        coord.IsActive.Should().BeTrue();
        coord.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void RequestCompletion_TransitionsToCompleting()
    {
        var coord = new LifecycleDrainCoordinator();
        coord.RequestCompletion().Should().BeTrue();
        coord.IsCompleting.Should().BeTrue();
        coord.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RequestCompletion_Twice_ReturnsFalse()
    {
        var coord = new LifecycleDrainCoordinator();
        coord.RequestCompletion().Should().BeTrue();
        coord.RequestCompletion().Should().BeFalse();
    }

    [Fact]
    public void MarkDrained_AfterCompletion_Succeeds()
    {
        var coord = new LifecycleDrainCoordinator();
        coord.RequestCompletion();
        coord.MarkDrained().Should().BeTrue();
        coord.IsDrained.Should().BeTrue();
    }

    [Fact]
    public void MarkDrained_WhileActive_Fails()
    {
        var coord = new LifecycleDrainCoordinator();
        coord.MarkDrained().Should().BeFalse();
    }

    [Fact]
    public void MarkDisposed_SetsDisposedState()
    {
        var coord = new LifecycleDrainCoordinator();
        coord.MarkDisposed().Should().BeTrue();
        coord.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void MarkDisposed_Twice_ReturnsFalse()
    {
        var coord = new LifecycleDrainCoordinator();
        coord.MarkDisposed().Should().BeTrue();
        coord.MarkDisposed().Should().BeFalse();
    }

    [Fact]
    public async Task DrainCompleted_CompletesOnDrain()
    {
        var coord = new LifecycleDrainCoordinator();
        coord.RequestCompletion();

        var task = coord.DrainCompleted;
        coord.MarkDrained();

        await task.WaitAsync(TimeSpan.FromSeconds(5));
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void MarkFaulted_TransitionsFromActive()
    {
        var coord = new LifecycleDrainCoordinator();
        coord.MarkFaulted();
        coord.IsActive.Should().BeFalse();
    }
}
