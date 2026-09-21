namespace Axrone.Utility.Tests.Concurrency;

using Axrone.Utility.Concurrency;

public class MemoryOrderValidatorTests
{
    [Theory]
    [InlineData(MemoryOrder.Relaxed)]
    [InlineData(MemoryOrder.Acquire)]
    [InlineData(MemoryOrder.SequentiallyConsistent)]
    public void ValidateLoad_AcceptsReadableOrders(MemoryOrder order)
    {
        var act = () => MemoryOrderValidator.ValidateLoad(order);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(MemoryOrder.Release)]
    [InlineData(MemoryOrder.AcquireRelease)]
    public void ValidateLoad_RejectsWriteOnlyOrders(MemoryOrder order)
    {
        var act = () => MemoryOrderValidator.ValidateLoad(order);

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(order));
    }

    [Theory]
    [InlineData(MemoryOrder.Relaxed)]
    [InlineData(MemoryOrder.Release)]
    [InlineData(MemoryOrder.AcquireRelease)]
    [InlineData(MemoryOrder.SequentiallyConsistent)]
    public void ValidateStore_AcceptsWritableOrders(MemoryOrder order)
    {
        var act = () => MemoryOrderValidator.ValidateStore(order);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(MemoryOrder.Acquire)]
    public void ValidateStore_RejectsAcquire(MemoryOrder order)
    {
        var act = () => MemoryOrderValidator.ValidateStore(order);

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(order));
    }
}
