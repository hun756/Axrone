namespace Axrone.Event.Tests;

using Axrone.Utility.Builders;

public class RouterOptionsBuilderTests
{
    [Fact]
    public void Defaults_MatchHouseTuning()
    {
        var options = new RouterOptionsBuilder().Build();

        options.Should().Be(RouterOptions.Default);
        options.Capacity.Should().Be(65536);
        options.DispatchBatchSize.Should().Be(256);
    }

    [Fact]
    public void FluentChain_ConfiguresOptions()
    {
        var options = new RouterOptionsBuilder()
            .WithCapacity(1024)
            .WithDispatchBatchSize(64)
            .Build();

        options.Capacity.Should().Be(1024);
        options.DispatchBatchSize.Should().Be(64);
    }

    [Fact]
    public void TryBuild_NonPowerOfTwo_ReportsDiagnostic()
    {
        var builder = new RouterOptionsBuilder().WithCapacity(100);

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Code.Should().Be(BuilderStatusCode.ValidationFailed);
        diagnostic.Message.Should().Contain("power of two");
    }

    [Fact]
    public void Build_Invalid_ThrowsDiagnostic()
    {
        var act = () => new RouterOptionsBuilder().WithDispatchBatchSize(0).Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*DispatchBatchSize*");
    }

    [Fact]
    public void State_ValidatesWithoutBuilder()
    {
        var state = RouterOptionsState.Default;
        state.DispatchBatchSize = 8192;

        RouterOptionsState.TryValidate(in state, out var diagnostic).Should().BeFalse();
    }

    [Fact]
    public void Fork_DivergesIndependently()
    {
        var original = new RouterOptionsBuilder().WithCapacity(512);
        var fork = original.Fork().WithCapacity(2048);

        original.Build().Capacity.Should().Be(512);
        fork.Build().Capacity.Should().Be(2048);
    }

    [Fact]
    public void Reset_RestoresDefaults()
    {
        var builder = new RouterOptionsBuilder().WithCapacity(512);
        builder.Reset();

        builder.Build().Should().Be(RouterOptions.Default);
    }

    [Fact]
    public void Router_HonorsOptions()
    {
        var options = new RouterOptionsBuilder()
            .WithCapacity(128)
            .WithDispatchBatchSize(16)
            .Build();

        using var router = new EventRouter<int>(options);
        var count = 0;
        using var sub = router.Subscribe((_, _) => Interlocked.Increment(ref count));
        router.Publish(1);

        SpinWait.SpinUntil(() => Volatile.Read(ref count) == 1, TimeSpan.FromSeconds(5)).Should().BeTrue();
    }

    [Fact]
    public void Router_RejectsZeroBatchSize()
    {
        var act = () => new EventRouter<int>(new RouterOptions(64, 0, 16));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Bus_AcceptsOptions()
    {
        var options = new RouterOptionsBuilder().WithCapacity(128).Build();
        using var bus = new EventBus(options);

        bus.Router<int>().TryPublish(3).Should().BeTrue();
    }

    [Fact]
    public void DeadLetterCapacity_Validation()
    {
        var builder = new RouterOptionsBuilder().WithDeadLetterCapacity(0);

        builder.TryBuild(out _, out BuilderDiagnostic diagnostic).Should().BeFalse();
        diagnostic.Message.Should().Contain("DeadLetterCapacity");
    }

    [Fact]
    public void DeadLetterBound_DropsOldestFirst()
    {
        var options = new RouterOptionsBuilder()
            .WithCapacity(64)
            .WithDeadLetterCapacity(4)
            .Build();
        using var router = new EventRouter<int>(options);
        using var bad = router.Subscribe((_, _) => throw new InvalidOperationException("poison"));

        for (int i = 0; i < 6; i++)
        {
            router.Publish(i);
        }

        SpinWait.SpinUntil(() => router.DroppedDeadLetters == 2, TimeSpan.FromSeconds(15)).Should().BeTrue();
        router.DeadLetterCount.Should().Be(4);

        var dead = router.DrainDeadLetters();
        dead.Select(e => e.Envelope.Payload).Should().Equal(2, 3, 4, 5);
        router.DroppedDeadLetters.Should().Be(2);
    }
}
