namespace Axrone.Batching;

/// <summary>
/// Predicate compaction over spans: filter copies, index gathers, in-place partitions.
/// </summary>
/// <remarks>
/// All three passes are scalar by construction — the predicate is a scalar interface, so an
/// 8-at-a-time mask loop would only rename the bottleneck, not remove it. What these guarantee
/// instead is stability: kept elements retain their relative order.
/// </remarks>
public static class NativeStreamCompactor
{
    /// <summary>
    /// Copies elements passing <paramref name="predicate"/> into <paramref name="destination"/>.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="TPredicate">Predicate type, passed by value so the call devirtualizes.</typeparam>
    /// <param name="source">Elements to filter.</param>
    /// <param name="destination">Receives the kept elements in source order.</param>
    /// <param name="predicate">Retention test; must be pure.</param>
    /// <returns>Elements written. When <paramref name="destination"/> is shorter than the kept
    /// run, the copy truncates and the return equals <paramref name="destination"/>.Length.</returns>
    public static int Compact<T, TPredicate>(ReadOnlySpan<T> source, Span<T> destination, TPredicate predicate)
        where T : unmanaged
        where TPredicate : struct, IBatchPredicate<T>
    {
        var write = 0;
        for (var i = 0; i < source.Length && write < destination.Length; i++)
        {
            if (predicate.Evaluate(in source[i]))
            {
                destination[write++] = source[i];
            }
        }

        return write;
    }

    /// <summary>
    /// Writes indices of elements passing <paramref name="predicate"/> into <paramref name="destination"/>.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="TPredicate">Predicate type, passed by value so the call devirtualizes.</typeparam>
    /// <param name="source">Elements to test.</param>
    /// <param name="destination">Receives source indices in ascending order.</param>
    /// <param name="predicate">Retention test; must be pure.</param>
    /// <returns>Indices written; truncates at <paramref name="destination"/>.Length.</returns>
    public static int CompactIndices<T, TPredicate>(ReadOnlySpan<T> source, Span<int> destination, TPredicate predicate)
        where T : unmanaged
        where TPredicate : struct, IBatchPredicate<T>
    {
        var write = 0;
        for (var i = 0; i < source.Length && write < destination.Length; i++)
        {
            if (predicate.Evaluate(in source[i]))
            {
                destination[write++] = i;
            }
        }

        return write;
    }

    /// <summary>
    /// Classifies elements three ways: kept elements copy to <paramref name="kept"/>, poison
    /// indices land in <paramref name="poisonIndices"/>, skipped elements vanish.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="TPredicate">Predicate type, passed by value so the call devirtualizes.</typeparam>
    /// <param name="source">Elements to classify.</param>
    /// <param name="kept">Receives kept elements in source order.</param>
    /// <param name="poisonIndices">Receives source indices of poison elements, ascending.</param>
    /// <param name="predicate">Classification; must be pure.</param>
    /// <returns>Kept and poison counts; each truncates at its own destination length.</returns>
    public static FateCompactionResult ClassifyCompact<T, TPredicate>(
        ReadOnlySpan<T> source, Span<T> kept, Span<int> poisonIndices, TPredicate predicate)
        where T : unmanaged
        where TPredicate : struct, IBatchFatePredicate<T>
    {
        var keptCount = 0;
        var poisonCount = 0;
        for (var i = 0; i < source.Length; i++)
        {
            switch (predicate.Classify(in source[i]))
            {
                case BatchItemFate.Keep when keptCount < kept.Length:
                    kept[keptCount++] = source[i];
                    break;
                case BatchItemFate.Poison when poisonCount < poisonIndices.Length:
                    poisonIndices[poisonCount++] = i;
                    break;
                default:
                    break;
            }
        }

        return new FateCompactionResult(keptCount, poisonCount);
    }

    /// <summary>
    /// Moves passing elements to the front of <paramref name="batch"/>, preserving order.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="TPredicate">Predicate type, passed by value so the call devirtualizes.</typeparam>
    /// <param name="batch">Elements to partition in place.</param>
    /// <param name="predicate">Retention test; must be pure.</param>
    /// <returns>Count of passing elements; they occupy <c>batch[..return]</c>.</returns>
    public static int CompactInPlace<T, TPredicate>(Span<T> batch, TPredicate predicate)
        where T : unmanaged
        where TPredicate : struct, IBatchPredicate<T>
    {
        var write = 0;
        for (var i = 0; i < batch.Length; i++)
        {
            if (predicate.Evaluate(in batch[i]))
            {
                if (write != i)
                {
                    batch[write] = batch[i];
                }

                write++;
            }
        }

        return write;
    }
}
