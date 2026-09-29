namespace Axrone.Utility.Tests.Strategies;

using Xunit;
using FluentAssertions;
using Axrone.Utility.Strategies;

public class StrategyValueTests
{
    [Fact]
    public void Id_OrdersAndPrints()
    {
        var a = new StrategyId(1);
        var b = new StrategyId(2);
        a.CompareTo(b).Should().BeNegative();
        b.CompareTo(a).Should().BePositive();
        a.CompareTo(a).Should().Be(0);
        a.ToString().Should().Be("1");
        StrategyId.Empty.Should().Be(new StrategyId(0));
    }

    [Fact]
    public void Result_FactoriesCarryStatus()
    {
        StrategyResult.Success(7).Should().Be(new StrategyResult(StrategyStatus.Success, 0, 7));
        StrategyResult.Success().IsSuccess.Should().BeTrue();
        StrategyResult.Rejected(3).Status.Should().Be(StrategyStatus.Rejected);
        StrategyResult.Faulted(9).IsSuccess.Should().BeFalse();
        StrategyResult.Fallback(2).Status.Should().Be(StrategyStatus.FallbackRequested);

        var x = StrategyResult.Success(1);
        var y = StrategyResult.Success(1);
        (x == y).Should().BeTrue();
        (x != StrategyResult.Success(2)).Should().BeTrue();
        x.Equals((object)y).Should().BeTrue();
        x.Equals((object)7).Should().BeFalse();
    }

    [Fact]
    public void Descriptor_RejectsEmptyIdAndNullInvoker()
    {
        StrategyInvoker<int, int, int> invoker = static (ref int c, in int i, out int o) => { o = i + c; return StrategyResult.Success(); };

        Action empty = () => { _ = new StrategyDescriptor<int, int, int>(StrategyId.Empty, invoker); };
        empty.Should().Throw<ArgumentException>();

        Action nullInvoker = () => { _ = new StrategyDescriptor<int, int, int>(new StrategyId(1), null!); };
        nullInvoker.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Descriptor_InvokesThroughDelegate()
    {
        var descriptor = new StrategyDescriptor<int, int, int>(
            new StrategyId(5),
            static (ref int c, in int i, out int o) => { o = i * 2 + c; return StrategyResult.Success((ulong)o); });

        int ctx = 3;
        StrategyResult result = descriptor.Invoker(ref ctx, 4, out int output);
        output.Should().Be(11);
        result.Metric.Should().Be(11ul);
        descriptor.State.Should().BeNull();
    }
}

public class StrategyContractTests
{
    private readonly struct DoubleIt : IStaticStrategy<int, int, int>
    {
        public static StrategyId Id => new(11);

        public static StrategyResult Execute(ref int context, scoped in int input, out int output)
        {
            output = input * 2 + context;
            return StrategyResult.Success((ulong)output);
        }
    }

    private sealed class TripleIt : IStrategy<int, int, int>
    {
        public StrategyId Id => new(12);

        public StrategyResult Execute(ref int context, in int input, out int output)
        {
            output = input * 3 + context;
            return StrategyResult.Success();
        }
    }

    [Fact]
    public void StaticStrategy_ExecutesMonomorphically()
    {
        int ctx = 1;
        StrategyResult result = StaticStrategyContext<DoubleIt, int, int, int>.Execute(ref ctx, 5, out int output);
        output.Should().Be(11);
        result.IsSuccess.Should().BeTrue();
        DoubleIt.Id.Should().Be(new StrategyId(11));
    }

    [Fact]
    public void InstanceStrategy_ExecutesDynamically()
    {
        IStrategy<int, int, int> strategy = new TripleIt();
        int ctx = 2;
        StrategyResult result = strategy.Execute(ref ctx, 5, out int output);
        output.Should().Be(17);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void NullSink_DropsEvents()
    {
        var sink = new NullStrategyMetricsSink();
        Action act = () =>
        {
            sink.OnSwapped(new StrategyId(1), new StrategyId(2));
            sink.OnExecuted(new StrategyId(1), StrategyResult.Success());
            sink.OnFaulted(new StrategyId(1), new InvalidOperationException());
        };
        act.Should().NotThrow();
    }
}
