namespace Axrone.Collections;

public sealed class RingBufferOptions
{
    public int Capacity { get; init; } = 1024;
    public IWaitStrategy? WaitStrategy { get; init; }
    public bool AutoClearOnDispose { get; init; } = true;
}
