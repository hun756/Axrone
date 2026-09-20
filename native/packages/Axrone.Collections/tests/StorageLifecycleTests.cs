namespace Axrone.Collections.Tests;

/// <summary>
/// Native storage lifecycle: construction and disposal must round-trip without corrupting the
/// native heap (aligned allocate/clear/free pairing).
/// </summary>
public class StorageLifecycleTests
{
    [Fact]
    public void ConstructDispose_RoundTrips()
    {
        var queue = new VyukovBoundedBatchQueue<int>(new BufferCapacity(8));
        queue.Dispose();
    }

    [Fact]
    public void ConstructDispose_ManagedPayload_RoundTrips()
    {
        var queue = new VyukovBoundedBatchQueue<string>(new BufferCapacity(8));
        queue.TryEnqueue("hello").Should().BeTrue();
        queue.TryDequeue(out string? item).Should().BeTrue();
        item.Should().Be("hello");
        queue.Dispose();
    }
}
