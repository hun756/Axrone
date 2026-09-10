namespace Axrone.Memory.Lifetime;

public enum SingletonLifecycleState
{
    Uninitialized = 0,
    Initializing = 1,
    Initialized = 2,
    Faulted = 3,
    Disposing = 4,
    Disposed = 5
}
