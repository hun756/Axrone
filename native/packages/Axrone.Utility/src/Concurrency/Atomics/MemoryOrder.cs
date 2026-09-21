namespace Axrone.Utility.Concurrency;

/// <summary>C++ std::atomic-compatible memory ordering vocabulary.</summary>
/// <remarks>
/// Orders are validated per operation (<see cref="MemoryOrderValidator"/>) and lowered to the
/// strongest primitive the BCL offers where it lacks a relaxed form; the order documents intent
/// even when the platform executes stricter.
/// </remarks>
public enum MemoryOrder : byte
{
    Relaxed = 0,
    Acquire = 1,
    Release = 2,
    AcquireRelease = 3,
    SequentiallyConsistent = 4,
}
