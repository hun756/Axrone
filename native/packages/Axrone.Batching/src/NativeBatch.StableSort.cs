namespace Axrone.Batching;

/// <summary>
/// Bottom-up stable merge sort over a batch, using scratch the caller already owns.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <c>Span&lt;T&gt;.Sort</c> and <see cref="RadixSort"/>, this preserves the relative order of elements that compare
/// equal, which render ordering depends on: transparents sorted by depth must keep submission
/// order within a tie.
/// </para>
/// <para>
/// The O(n) scratch is a parameter rather than an allocation so a batch sorted every frame draws
/// its temp storage from a pool that is already warm.
/// </para>
/// </remarks>
public readonly unsafe partial struct NativeBatch<T>
    where T : unmanaged
{
    /// <summary>
    /// Sorts the batch in place, keeping equal elements in their original relative order.
    /// </summary>
    /// <typeparam name="TComparer">Comparer type, passed by value so the call devirtualizes.</typeparam>
    /// <param name="scratch">Temporary storage, at least <see cref="Length"/> elements. Contents are undefined on return.</param>
    /// <param name="comparer">Total order to sort by.</param>
    /// <exception cref="ArgumentException"><paramref name="scratch"/> is shorter than the batch or overlaps it.</exception>
    /// <remarks>
    /// Runs in O(n log n) with O(n) auxiliary space, and is not in-place despite the batch being
    /// mutated directly — the scratch is where the unsorted half of each merge pass lives.
    /// </remarks>
    public void StableSort<TComparer>(Span<T> scratch, TComparer comparer)
        where TComparer : struct, IBatchComparer<T>
    {
        var count = _length;
        if (count < 2)
        {
            return;
        }

        if (scratch.Length < count)
        {
            ThrowHelper.ThrowDestinationTooSmall();
        }

        fixed (T* scratchPointer = scratch)
        {
            if (Overlaps(scratchPointer, scratch.Length))
            {
                ThrowHelper.ThrowArgumentException(
                    "StableSort scratch must not overlap the batch being sorted.");
            }

            SortByMerging(count, scratchPointer, comparer);
        }
    }

    private void SortByMerging<TComparer>(int count, T* scratch, TComparer comparer)
        where TComparer : struct, IBatchComparer<T>
    {
        // Ping-pong between the batch and the scratch, doubling the merged run width each pass.
        var source = _data;
        var destination = scratch;

        for (var width = 1; width < count; width <<= 1)
        {
            for (var left = 0; left < count; left += width << 1)
            {
                var middle = left + width;
                if (middle > count)
                {
                    middle = count;
                }

                var right = left + (width << 1);
                if (right > count)
                {
                    right = count;
                }

                MergeRuns(source, destination, left, middle, right, comparer);
            }

            var previous = source;
            source = destination;
            destination = previous;
        }

        // An odd number of passes leaves the sorted result in the scratch rather than the batch.
        if (source != _data)
        {
            ReadOnlySpan<T> sorted = new(source, count);
            Span<T> target = new(_data, count);
            sorted.CopyTo(target);
        }
    }

    /// <summary>
    /// Merges <c>[low, middle)</c> with <c>[middle, high)</c> into <c>destination[low..high]</c>.
    /// </summary>
    /// <remarks>Taking from the left run on a tie is what makes the merge stable.</remarks>
    private static void MergeRuns<TComparer>(
        T* source, T* destination, int low, int middle, int high, TComparer comparer)
        where TComparer : struct, IBatchComparer<T>
    {
        var left = low;
        var right = middle;
        var write = low;

        while (left < middle && right < high)
        {
            if (comparer.Compare(source[left], source[right]) <= 0)
            {
                destination[write++] = source[left++];
            }
            else
            {
                destination[write++] = source[right++];
            }
        }

        while (left < middle)
        {
            destination[write++] = source[left++];
        }

        // The right-run tail lands at the same indices it came from, but source and destination
        // are different buffers here, so it still has to be written across.
        while (right < high)
        {
            destination[write++] = source[right++];
        }
    }
}
