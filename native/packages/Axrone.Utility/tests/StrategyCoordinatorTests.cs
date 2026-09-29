namespace Axrone.Utility.Tests.Strategies;

using Xunit;
using FluentAssertions;
using Axrone.Utility.Strategies;

public class StrategyCoordinatorTests
{
    private readonly struct AddOne : IStaticStrategy<int, int, int>
    {
        public static StrategyId Id => new(21);

        public static StrategyResult Execute(ref int context, scoped in int input, out int output)
        {
            output = input + context + 1;
            return StrategyResult.Success((ulong)output);
        }
    }

    private readonly struct TimesTwo : IStaticStrategy<int, int, int>
    {
        public static StrategyId Id => new(22);

        public static StrategyResult Execute(ref int context, scoped in int input, out int output)
        {
            output = (input + context) * 2;
            return StrategyResult.Success();
        }
    }

    private sealed class Faulting : IStrategy<int, int, int>
    {
        public StrategyId Id => new(23);

        public StrategyResult Execute(ref int context, in int input, out int output)
        {
            output = 0;
            throw new InvalidOperationException("boom");
        }
    }

    [Fact]
    public void Construct_DoesNotThrowAndStartsEmpty()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        coordinator.ActiveStrategyId.Should().Be(StrategyId.Empty);
        coordinator.InFlightCount.Should().Be(0);
    }

    [Fact]
    public void RegisterFirst_BecomesActive()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        coordinator.Register<AddOne>();
        coordinator.ActiveStrategyId.Should().Be(new StrategyId(21));

        int ctx = 10;
        StrategyResult result = coordinator.Execute(ref ctx, 5, out int output);
        result.IsSuccess.Should().BeTrue();
        output.Should().Be(16);
    }

    [Fact]
    public void Swap_RoutesToNewStrategy()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        coordinator.Register<AddOne>();
        coordinator.Register<TimesTwo>();

        coordinator.TrySwap(new StrategyId(22)).Should().BeTrue();
        coordinator.ActiveStrategyId.Should().Be(new StrategyId(22));

        int ctx = 10;
        coordinator.Execute(ref ctx, 5, out int output).IsSuccess.Should().BeTrue();
        output.Should().Be(30);

        coordinator.TrySwap(new StrategyId(999)).Should().BeFalse();
        coordinator.TrySwap(StrategyId.Empty).Should().BeFalse();
    }

    [Fact]
    public void FaultingStrategy_FaultsCoordinatorAndPropagates()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        coordinator.Register(new Faulting());

        int ctx = 0;
        Action execute = () => coordinator.Execute(ref ctx, 1, out _);
        execute.Should().Throw<InvalidOperationException>();

        Action again = () => coordinator.Execute(ref ctx, 1, out _);
        again.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task DrainAsync_EmptyCompletesImmediately()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        coordinator.Register<AddOne>();
        await coordinator.DrainAsync();
    }

    [Fact]
    public async Task DisposeAsync_DrainsAndDisposes()
    {
        var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        coordinator.Register<AddOne>();
        await coordinator.DisposeAsync();

        int ctx = 0;
        Action execute = () => coordinator.Execute(ref ctx, 1, out _);
        execute.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CompleteWithError_Faults()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        coordinator.Register<AddOne>();
        coordinator.Complete(new InvalidOperationException("terminal"));

        int ctx = 0;
        Action execute = () => coordinator.Execute(ref ctx, 1, out _);
        execute.Should().Throw<InvalidOperationException>();
    }
}
