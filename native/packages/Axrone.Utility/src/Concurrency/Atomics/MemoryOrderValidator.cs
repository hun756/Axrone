namespace Axrone.Utility.Concurrency;

using Axrone.Utility.Internal;

/// <summary>Rejects orderings a given operation cannot honor.</summary>
internal static class MemoryOrderValidator
{
    /// <summary>Loads accept Relaxed, Acquire, or SequentiallyConsistent.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateLoad(MemoryOrder order)
    {
        if (order is MemoryOrder.Release or MemoryOrder.AcquireRelease)
        {
            ThrowHelper.ThrowArgumentException(nameof(order), $"MemoryOrder.{order} is not valid for loads.");
        }
    }

    /// <summary>Stores accept Relaxed, Release, or SequentiallyConsistent.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateStore(MemoryOrder order)
    {
        if (order == MemoryOrder.Acquire)
        {
            ThrowHelper.ThrowArgumentException(nameof(order), $"MemoryOrder.{order} is not valid for stores.");
        }
    }
}
