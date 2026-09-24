namespace Axrone.Tween.Tests;

public class TweenAwaitTests
{
    private static (TweenEngine Engine, ManualTweenClock Clock) Create()
    {
        var clock = new ManualTweenClock();
        return (new TweenEngine(16, clock), clock);
    }

    private static TweenSpec Quick() =>
        new TweenBuilder().From(0f).To(1f).DurationSeconds(1f).Build();

    private static void Step((TweenEngine Engine, ManualTweenClock Clock) ctx, float seconds)
    {
        ctx.Clock.Advance(DurationNs.FromSeconds(seconds));
        ctx.Engine.Update();
    }

    [Fact]
    public async Task Await_CompletesTrueOnNaturalFinish()
    {
        var ctx = Create();
        using var engine = ctx.Engine;
        var handle = engine.Play(Quick());

        var pending = engine.AwaitAsync(handle.Id);
        Step(ctx, 1f);

        (await pending.WaitAsync(TimeSpan.FromSeconds(15))).Should().BeTrue();
    }

    [Fact]
    public async Task Await_CompletesFalseOnCancel()
    {
        var ctx = Create();
        using var engine = ctx.Engine;
        var handle = engine.Play(Quick());

        var pending = engine.AwaitAsync(handle.Id);
        handle.Cancel();

        (await pending.WaitAsync(TimeSpan.FromSeconds(15))).Should().BeFalse();
    }

    [Fact]
    public async Task Await_AfterSettle_ReadsRecentRecord()
    {
        var ctx = Create();
        using var engine = ctx.Engine;
        var handle = engine.Play(Quick());
        Step(ctx, 1f);

        (await engine.AwaitAsync(handle.Id).WaitAsync(TimeSpan.FromSeconds(15))).Should().BeTrue();
    }

    [Fact]
    public async Task Await_StaleIdentity_CompletesFalse()
    {
        var ctx = Create();
        using var engine = ctx.Engine;

        (await engine.AwaitAsync(TweenId.Invalid).WaitAsync(TimeSpan.FromSeconds(15))).Should().BeFalse();
    }

    [Fact]
    public async Task AwaitSyntax_WorksOnHandle()
    {
        var ctx = Create();
        using var engine = ctx.Engine;
        var handle = engine.Play(Quick());
        Step(ctx, 1f);

        bool finished = await handle;

        finished.Should().BeTrue();
    }
}
