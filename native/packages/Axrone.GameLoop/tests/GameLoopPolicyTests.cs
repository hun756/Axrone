namespace Axrone.GameLoop.Tests;

internal readonly struct TestCountingPolicy : IErrorRecoveryPolicy<TestLoopState>
{
    public static int Calls;

    public static ErrorDisposition OnError(
        Exception exception,
        SystemId systemId,
        GameLoopPhase phase,
        int attempt,
        ref TestLoopState state)
    {
        Calls++;
        _ = exception;
        _ = systemId;
        _ = phase;
        _ = attempt;
        _ = state;
        return ErrorDisposition.Continue;
    }
}

internal struct TestLoopState
{
    public int Value;

    public TestLoopState(int value) => Value = value;
}

public class GameLoopPolicyTests
{
    [Fact]
    public void Spiral_Mitigates_OverBudget()
    {
        double accumulator = 100.0;
        ModuloSpiralMitigation.Mitigate(ref accumulator, 16.666, 2, out uint steps, out double dropped);

        steps.Should().Be(2);
        dropped.Should().BeApproximately(100.0 - 2 * 16.666, 0.01);
        accumulator.Should().BeApproximately(accumulator % 16.666, 1e-9);

        double strictAccumulator = 100.0;
        StrictDropSpiralMitigation.Mitigate(ref strictAccumulator, 16.666, 2, out uint strictSteps, out _);

        strictSteps.Should().Be(2);
        strictAccumulator.Should().Be(0.0);
    }

    [Fact]
    public void Spiral_Passes_UnderBudget()
    {
        double accumulator = 30.0;
        ModuloSpiralMitigation.Mitigate(ref accumulator, 16.666, 4, out uint steps, out double dropped);

        steps.Should().Be(1);
        dropped.Should().Be(0.0);
        accumulator.Should().BeApproximately(30.0 - 16.666, 1e-9);
    }

    [Fact]
    public void Error_Policies_Map_Dispositions()
    {
        var state = new TestLoopState();
        var systems = SystemId.FromString("test");
        FailFastErrorPolicy<TestLoopState>.OnError(new InvalidOperationException(), systems, GameLoopPhase.Update, 1, ref state)
            .Should().Be(ErrorDisposition.Escalate);
        SuppressAndContinueErrorPolicy<TestLoopState>.OnError(new InvalidOperationException(), systems, GameLoopPhase.Update, 1, ref state)
            .Should().Be(ErrorDisposition.Continue);
        HaltOnErrorPolicy<TestLoopState>.OnError(new InvalidOperationException(), systems, GameLoopPhase.Update, 1, ref state)
            .Should().Be(ErrorDisposition.Stop);
    }

    [Fact]
    public void Clock_Now_Advances()
    {
        GameTime first = HighResolutionStopwatchClock.Now();
        GameTime second = HighResolutionStopwatchClock.Now();
        (second >= first).Should().BeTrue();
    }
}
