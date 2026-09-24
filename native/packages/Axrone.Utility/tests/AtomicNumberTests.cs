namespace Axrone.Utility.Tests.Concurrency;

using Axrone.Utility.Concurrency;

public class AtomicNumberTests
{
    [Fact]
    public void Add_ReturnsNewValue()
    {
        var atomic = new AtomicNumber<int>(10);

        atomic.Add(5).Should().Be(15);
        atomic.Value.Should().Be(15);
    }

    [Fact]
    public void Subtract_ReturnsNewValue()
    {
        var atomic = new AtomicNumber<long>(10L);

        atomic.Subtract(4L).Should().Be(6L);
        atomic.Decrement().Should().Be(5L);
        atomic.Increment().Should().Be(6L);
    }

    [Fact]
    public void Subtract_MinValue_FallsBackCorrectly()
    {
        var atomic = new AtomicNumber<int>(10);

        atomic.Subtract(int.MinValue).Should().Be(unchecked(10 - int.MinValue));
        atomic.Value.Should().Be(unchecked(10 - int.MinValue));
    }

    [Fact]
    public void Add_Short_UsesCasFallback()
    {
        var atomic = new AtomicNumber<short>(100);

        atomic.Add(23).Should().Be(123);
        atomic.Subtract(23).Should().Be(100);
    }

    [Fact]
    public void Bitwise_ReturnsNewValue()
    {
        var atomic = new AtomicNumber<int>(0b1100);

        atomic.And(0b1010).Should().Be(0b1000);
        atomic.Or(0b0101).Should().Be(0b1101);
        atomic.Xor(0b1111).Should().Be(0b0010);
    }

    [Fact]
    public void Bitwise_Long_Works()
    {
        var atomic = new AtomicNumber<long>(0b1100L);

        atomic.And(0b1010L).Should().Be(0b1000L);
        atomic.Or(0b0101L).Should().Be(0b1101L);
    }

    [Fact]
    public void Operators_DelegateToMethods()
    {
        var atomic = new AtomicNumber<int>(10);

        (atomic + 5).Should().Be(15);
        (atomic - 3).Should().Be(7);

        // Postfix returns the pre-increment snapshot; the variable holds the new value.
        atomic++.Value.Should().Be(10);
        atomic.Value.Should().Be(11);
        (++atomic).Value.Should().Be(12);
        ((int)atomic).Should().Be(12);
    }

    [Fact]
    public async Task ContendedAdds_AreLossless()
    {
        var atomic = new AtomicNumber<int>(0);
        const int Threads = 4;
        const int PerThread = 2500;
        var tasks = new Task[Threads];
        for (int t = 0; t < Threads; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < PerThread; i++)
                {
                    atomic.Increment();
                }
            });
        }

        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15));
        atomic.Value.Should().Be(Threads * PerThread);
    }
}
