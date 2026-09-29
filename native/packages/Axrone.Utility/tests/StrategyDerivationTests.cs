namespace Axrone.Utility.Tests.Strategies;

using Xunit;
using FluentAssertions;
using Axrone.Utility.Descriptors;
using Axrone.Utility.Strategies;

public class StrategyDerivationTests
{
    private static StrategyDescriptor<int, int, int> Node(uint id, int add) =>
        new(new StrategyId(id),
            static (ref int c, in int i, out int o) => { o = i + c; return StrategyResult.Success(); },
            state: add);

    [Fact]
    public void StandaloneDescriptor_HandleIsInvalidUntilRegistered()
    {
        Node(51, 0).Handle.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RegisteredDescriptor_CarriesValidGenerationalHandle()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        StrategyDescriptor<int, int, int> first = Node(52, 0);
        StrategyDescriptor<int, int, int> second = Node(53, 0);

        coordinator.Register(first);
        coordinator.Register(second);

        first.Handle.IsValid.Should().BeTrue();
        second.Handle.IsValid.Should().BeTrue();
        second.Handle.SlotIndex.Should().NotBe(first.Handle.SlotIndex);
        coordinator.RegisteredCount.Should().Be(2);
        coordinator.ActiveStrategyHandle.Should().Be(first.Handle);
        coordinator.ActiveStrategyId.Should().Be(new StrategyId(52));
    }

    [Fact]
    public void TrySwap_ByHandle_RoutesDirectly()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        StrategyDescriptor<int, int, int> first = Node(54, 0);
        StrategyDescriptor<int, int, int> second = Node(55, 0);
        coordinator.Register(first);
        coordinator.Register(second);

        coordinator.TrySwap(second.Handle).Should().BeTrue();
        coordinator.ActiveStrategyId.Should().Be(new StrategyId(55));
        coordinator.ActiveStrategyHandle.Should().Be(second.Handle);

        coordinator.TrySwap(first.Handle).Should().BeTrue();
        coordinator.ActiveStrategyId.Should().Be(new StrategyId(54));
    }

    [Fact]
    public void TrySwap_StaleHandle_FailsClosed()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        coordinator.Register(Node(56, 0));

        coordinator.TrySwap(default).Should().BeFalse();
        coordinator.TrySwap(new DescriptorHandle<StrategyNode>(0, 999)).Should().BeFalse();
        coordinator.TrySwap(new DescriptorHandle<StrategyNode>(77, 1)).Should().BeFalse();
        coordinator.ActiveStrategyId.Should().Be(new StrategyId(56));
    }

    [Fact]
    public void DoubleRegistration_Throws()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        StrategyDescriptor<int, int, int> descriptor = Node(57, 0);
        coordinator.Register(descriptor);

        Action twice = () => coordinator.Register(descriptor);
        twice.Should().Throw<InvalidOperationException>();
        coordinator.RegisteredCount.Should().Be(1);
    }

    [Fact]
    public void RegistryBound_ThrowsWhenExhausted()
    {
        using var coordinator = new DynamicStrategyCoordinator<int, int, int>(
            new DescriptorTableOptions { Capacity = 2 });
        coordinator.Register(Node(58, 0));
        coordinator.Register(Node(59, 0));

        Action overflow = () => coordinator.Register(Node(60, 0));
        overflow.Should().Throw<InvalidOperationException>();
        coordinator.RegisteredCount.Should().Be(2);
    }

    [Fact]
    public void Dispose_ReclaimsAllTableSlots()
    {
        var coordinator = new DynamicStrategyCoordinator<int, int, int>();
        coordinator.Register(Node(61, 0));
        coordinator.Register(Node(62, 0));
        coordinator.RegisteredCount.Should().Be(2);

        coordinator.Dispose();
        coordinator.RegisteredCount.Should().Be(0);
    }
}
