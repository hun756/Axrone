using Axrone.Memory.Lifetime;

namespace Axrone.Memory.Tests;

public sealed class SimpleService
{
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    private static int _nextId;
}

public sealed class DisposableService : IDisposable
{
    public bool WasDisposed { get; private set; }
    public void Dispose() => WasDisposed = true;
}

public sealed class AsyncDisposableService : IAsyncDisposable
{
    public bool WasDisposed { get; private set; }
    public ValueTask DisposeAsync()
    {
        WasDisposed = true;
        return default;
    }
}

public sealed class ThrowingService
{
    public ThrowingService() => throw new InvalidOperationException("construction failed");
}

public sealed class UntouchedService;

public sealed class NullReturningService
{
    public static SimpleService? Create() => null;
}
