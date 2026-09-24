namespace Axrone.Memory.Tests.Memory;

using Axrone.Memory;

public class AlignedNodePoolTests
{
    private struct Slot
    {
        public long A;
        public long B;
    }

    [Fact]
    public unsafe void RentReturn_PreservesLifoOrder()
    {
        using var pool = new AlignedNodePool<Slot>(4);

        Slot* first = pool.Rent();
        first->A = 1;
        first->B = 2;
        pool.Return(first);

        Slot* second = pool.Rent();
        ((long)second).Should().Be((long)first);
        second->A.Should().Be(0);
        second->B.Should().Be(0);
    }

    [Fact]
    public unsafe void Exhaustion_TryRentFailsRentNulls()
    {
        using var pool = new AlignedNodePool<Slot>(2);

        pool.Rent()->A.Should().Be(0);
        pool.Rent()->A.Should().Be(0);
        pool.TryRent(out Slot* empty).Should().BeFalse();
        ((long)empty).Should().Be(0);
        ((long)pool.Rent()).Should().Be(0);
    }

    [Fact]
    public unsafe void ReturnNull_Throws()
    {
        using var pool = new AlignedNodePool<Slot>(2);

        var act = () => pool.Return(null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SmallNodeType_IsRejected()
    {
        var act = () => new AlignedNodePool<byte>(4);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        var zeroCapacity = () => new AlignedNodePool<Slot>(0);
        zeroCapacity.Should().Throw<ArgumentOutOfRangeException>();

        var badAlignment = () => new AlignedNodePool<Slot>(4, 24);
        badAlignment.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static unsafe void Hammer(AlignedNodePool<Slot> pool, int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            Slot* node;
            while (!pool.TryRent(out node))
            {
            }

            node->A = i;
            pool.Return(node);
        }
    }

    private static unsafe int Drain(AlignedNodePool<Slot> pool)
    {
        int drained = 0;
        while (pool.TryRent(out _))
        {
            drained++;
        }

        return drained;
    }

    [Fact]
    public async Task ContendedRentReturn_LosesNothing()
    {
        const int Capacity = 64;
        const int Threads = 8;
        const int Iterations = 2000;
        using var pool = new AlignedNodePool<Slot>(Capacity);

        var tasks = new Task[Threads];
        for (int t = 0; t < Threads; t++)
        {
            tasks[t] = Task.Run(() => Hammer(pool, Iterations));
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));

        Drain(pool).Should().Be(Capacity);
    }
}
