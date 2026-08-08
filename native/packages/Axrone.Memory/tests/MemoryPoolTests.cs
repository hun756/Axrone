namespace Axrone.Memory.Tests;

public class MemoryPoolTests
{
    [Fact]
    public void Placeholder()
    {
        var pool = new MemoryPool<byte>();
        pool.Should().NotBeNull();
    }
}
