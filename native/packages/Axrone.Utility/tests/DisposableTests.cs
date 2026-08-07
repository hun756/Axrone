using Axrone.Utility.Disposable;

namespace Axrone.Utility.Tests.Disposable;

public class DisposableTests
{
    [Fact]
    public void ActionDisposable_Dispose_InvokesAction()
    {
        var disposed = false;
        var d = new ActionDisposable(() => disposed = true);
        d.Dispose();
        disposed.Should().BeTrue();
    }

    [Fact]
    public void ActionDisposable_DoubleDispose_InvokesActionOnce()
    {
        var count = 0;
        var d = new ActionDisposable(() => count++);
        d.Dispose();
        d.Dispose();
        count.Should().Be(1);
    }

    [Fact]
    public void ActionDisposable_IsDisposed_ReflectsState()
    {
        var d = new ActionDisposable(() => { });
        d.IsDisposed.Should().BeFalse();
        d.Dispose();
        d.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void CompositeDisposable_Dispose_DisposesInReverseOrder()
    {
        var order = new List<int>();
        var composite = new CompositeDisposable();
        composite.Add(new ActionDisposable(() => order.Add(1)));
        composite.Add(new ActionDisposable(() => order.Add(2)));
        composite.Add(new ActionDisposable(() => order.Add(3)));

        composite.Dispose();

        order.Should().Equal(3, 2, 1);
    }

    [Fact]
    public void CompositeDisposable_DoubleDispose_NoOp()
    {
        var count = 0;
        var composite = new CompositeDisposable();
        composite.Add(new ActionDisposable(() => count++));
        composite.Dispose();
        composite.Dispose();
        count.Should().Be(1);
    }

    [Fact]
    public void DisposalTracker_TryDispose_FirstCallReturnsTrue()
    {
        var tracker = new DisposalTracker();
        tracker.TryDispose().Should().BeTrue();
        tracker.TryDispose().Should().BeFalse();
    }

    [Fact]
    public void DisposalTracker_ThrowIfDisposed_ThrowsAfterDispose()
    {
        var tracker = new DisposalTracker();
        tracker.ThrowIfDisposed();
        tracker.TryDispose();
        var act = () => tracker.ThrowIfDisposed();
        act.Should().Throw<ObjectDisposedException>();
    }
}
