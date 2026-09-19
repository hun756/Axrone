namespace Axrone.Batching;

/// <summary>Three-way fate of one element under classification.</summary>
/// <remarks>
/// A boolean predicate only knows keep and drop. Poison is neither: it must be counted and
/// located (see <see cref="NativeBisectionIsolator"/>), not silently compacted away with the
/// merely skipped. Zero is <see cref="Keep"/> so a default-classified element flows through.
/// </remarks>
public enum BatchItemFate : byte
{
    /// <summary>Retained by compaction.</summary>
    Keep = 0,

    /// <summary>Dropped silently.</summary>
    Skip = 1,

    /// <summary>Dropped and reported by index.</summary>
    Poison = 2,
}

/// <summary>
/// Classifies one element into its fate.
/// </summary>
/// <typeparam name="T">Element type. Unmanaged so batches can back onto native memory.</typeparam>
/// <remarks>
/// Implementations must be pure and side-effect free, like <see cref="IBatchPredicate{T}"/>.
/// </remarks>
public interface IBatchFatePredicate<T> where T : unmanaged
{
    /// <summary>Returns the fate of <paramref name="item"/>.</summary>
    /// <param name="item">The element to classify.</param>
    BatchItemFate Classify(in T item);
}

/// <summary>Outcome of a fate-classified compaction.</summary>
public readonly record struct FateCompactionResult(int KeptCount, int PoisonCount);
