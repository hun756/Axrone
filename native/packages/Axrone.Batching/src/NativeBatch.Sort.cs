namespace Axrone.Batching;

/// <summary>
/// In-place introsort over a batch: quicksort with median-of-three pivoting, downgraded to
/// insertion sort on small partitions and to heapsort once recursion runs out of budget.
/// </summary>
/// <remarks>
/// The sort is <em>unstable</em> — equal elements are not kept in their original relative order.
/// Render ordering that depends on tie order needs <c>StableSort</c> instead.
/// </remarks>
public readonly unsafe partial struct NativeBatch<T>
    where T : unmanaged
{
    /// <summary>
    /// Partitions at or below this size are finished with insertion sort, which beats quicksort's
    /// partitioning overhead on short runs.
    /// </summary>
    private const int InsertionSortThreshold = 16;

    /// <summary>
    /// Sorts the batch in place using <paramref name="comparer"/>.
    /// </summary>
    /// <typeparam name="TComparer">Comparer type, passed by value so the call devirtualizes.</typeparam>
    /// <param name="comparer">Total order to sort by.</param>
    /// <remarks>O(n log n) worst case; the depth budget is what prevents quicksort's O(n²) fallback.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Sort<TComparer>(TComparer comparer)
        where TComparer : struct, IBatchComparer<T>
    {
        if (_length > 1)
        {
            IntroSort(0, _length - 1, 2 * BitOperations.Log2((uint)_length), comparer);
        }
    }

    private void IntroSort<TComparer>(int low, int high, int depthLimit, TComparer comparer)
        where TComparer : struct, IBatchComparer<T>
    {
        while (high > low)
        {
            var partitionSize = high - low + 1;

            if (partitionSize <= InsertionSortThreshold)
            {
                InsertionSort(low, high, comparer);
                return;
            }

            if (depthLimit == 0)
            {
                HeapSort(low, high, comparer);
                return;
            }

            depthLimit--;

            var pivot = Partition(low, high, comparer);

            // Recurse into the larger side and loop on the smaller one, so stack depth stays
            // logarithmic rather than tracking the input's skew.
            if (pivot - low < high - pivot)
            {
                IntroSort(low, pivot - 1, depthLimit, comparer);
                low = pivot + 1;
            }
            else
            {
                IntroSort(pivot + 1, high, depthLimit, comparer);
                high = pivot - 1;
            }
        }
    }

    private int Partition<TComparer>(int low, int high, TComparer comparer)
        where TComparer : struct, IBatchComparer<T>
    {
        var mid = low + ((high - low) >> 1);

        MedianOfThree(low, mid, high, comparer);

        // MedianOfThree leaves the median at high and the maximum at mid, so the pivot sits at
        // high and the scan never has to guard against walking off the end.
        var pivot = _data[high];

        var store = low - 1;
        for (var probe = low; probe < high; probe++)
        {
            if (comparer.Compare(in _data[probe], in pivot) <= 0)
            {
                store++;
                Swap(store, probe);
            }
        }

        Swap(store + 1, high);
        return store + 1;
    }

    private void MedianOfThree<TComparer>(int low, int mid, int high, TComparer comparer)
        where TComparer : struct, IBatchComparer<T>
    {
        if (comparer.Compare(in _data[low], in _data[mid]) > 0)
        {
            Swap(low, mid);
        }

        if (comparer.Compare(in _data[low], in _data[high]) > 0)
        {
            Swap(low, high);
        }

        if (comparer.Compare(in _data[mid], in _data[high]) > 0)
        {
            Swap(mid, high);
        }

        Swap(mid, high);
    }

    private void InsertionSort<TComparer>(int low, int high, TComparer comparer)
        where TComparer : struct, IBatchComparer<T>
    {
        for (var probe = low + 1; probe <= high; probe++)
        {
            var key = _data[probe];
            var shift = probe - 1;

            while (shift >= low && comparer.Compare(in _data[shift], in key) > 0)
            {
                _data[shift + 1] = _data[shift];
                shift--;
            }

            _data[shift + 1] = key;
        }
    }

    private void HeapSort<TComparer>(int low, int high, TComparer comparer)
        where TComparer : struct, IBatchComparer<T>
    {
        var count = high - low + 1;

        for (var node = (count >> 1) - 1; node >= 0; node--)
        {
            Heapify(count, node, low, comparer);
        }

        for (var tail = count - 1; tail > 0; tail--)
        {
            Swap(low, low + tail);
            Heapify(tail, 0, low, comparer);
        }
    }

    private void Heapify<TComparer>(int count, int node, int low, TComparer comparer)
        where TComparer : struct, IBatchComparer<T>
    {
        while (true)
        {
            var largest = node;
            var left = (node << 1) + 1;
            var right = (node << 1) + 2;

            if (left < count && comparer.Compare(in _data[low + left], in _data[low + largest]) > 0)
            {
                largest = left;
            }

            if (right < count && comparer.Compare(in _data[low + right], in _data[low + largest]) > 0)
            {
                largest = right;
            }

            if (largest == node)
            {
                return;
            }

            Swap(low + node, low + largest);
            node = largest;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Swap(int left, int right)
    {
        if (left == right)
        {
            return;
        }

        var temp = _data[left];
        _data[left] = _data[right];
        _data[right] = temp;
    }
}
