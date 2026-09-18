namespace Axrone.Collections;

public sealed record RingBufferOptions
{
    public int Capacity { get; init; } = 1024;
    public bool AutoClearOnDispose { get; init; } = true;
}
