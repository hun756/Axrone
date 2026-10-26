namespace Axrone.Scene.Rendering;

/// <summary>
/// Allocation-free introsort over value-type elements. The BCL's span sort
/// boxes struct comparers (88 bytes per call at this writing), which would
/// break the zero-allocation frame contract.
/// </summary>
internal static class SceneIntroSort<T, TComparer>
    where TComparer : IComparer<T>
{
    private const int InsertionThreshold = 16;

    public static void Sort(Span<T> span, TComparer comparer)
    {
        if (span.Length < 2)
        {
            return;
        }

        IntrospectiveSort(span, 2 * BitOperations.Log2((uint)span.Length), comparer);
    }

    private static void IntrospectiveSort(Span<T> span, int depthLimit, TComparer comparer)
    {
        while (span.Length > InsertionThreshold)
        {
            if (depthLimit == 0)
            {
                HeapSort(span, comparer);
                return;
            }

            depthLimit--;

            int pivot = Partition(span, comparer);
            Span<T> right = span[(pivot + 1)..];
            Span<T> left = span[..pivot];

            // Recurse into the smaller half; loop over the larger one.
            if (left.Length < right.Length)
            {
                IntrospectiveSort(left, depthLimit, comparer);
                span = right;
            }
            else
            {
                IntrospectiveSort(right, depthLimit, comparer);
                span = left;
            }
        }

        InsertionSort(span, comparer);
    }

    private static int Partition(Span<T> span, TComparer comparer)
    {
        int last = span.Length - 1;
        int mid = last >> 1;

        // Order first/middle/last, then park the median at the tail.
        if (comparer.Compare(span[last], span[0]) < 0)
        {
            Swap(span, 0, last);
        }

        if (comparer.Compare(span[mid], span[0]) < 0)
        {
            Swap(span, 0, mid);
        }

        if (comparer.Compare(span[last], span[mid]) < 0)
        {
            Swap(span, mid, last);
        }

        T pivot = span[mid];
        Swap(span, mid, last);

        int store = 0;
        for (int i = 0; i < last; i++)
        {
            if (comparer.Compare(span[i], pivot) < 0)
            {
                Swap(span, i, store);
                store++;
            }
        }

        Swap(span, store, last);
        return store;
    }

    private static void InsertionSort(Span<T> span, TComparer comparer)
    {
        for (int i = 1; i < span.Length; i++)
        {
            T item = span[i];
            int j = i - 1;

            while (j >= 0 && comparer.Compare(item, span[j]) < 0)
            {
                span[j + 1] = span[j];
                j--;
            }

            span[j + 1] = item;
        }
    }

    private static void HeapSort(Span<T> span, TComparer comparer)
    {
        int count = span.Length;

        for (int root = (count >> 1) - 1; root >= 0; root--)
        {
            SiftDown(span, root, count, comparer);
        }

        for (int end = count - 1; end > 0; end--)
        {
            Swap(span, 0, end);
            SiftDown(span, 0, end, comparer);
        }
    }

    private static void SiftDown(Span<T> span, int root, int count, TComparer comparer)
    {
        while (true)
        {
            int child = (root << 1) + 1;
            if (child >= count)
            {
                return;
            }

            if (child + 1 < count && comparer.Compare(span[child], span[child + 1]) < 0)
            {
                child++;
            }

            if (comparer.Compare(span[root], span[child]) >= 0)
            {
                return;
            }

            Swap(span, root, child);
            root = child;
        }
    }

    private static void Swap(Span<T> span, int left, int right)
    {
        T temp = span[left];
        span[left] = span[right];
        span[right] = temp;
    }
}

/// <summary>World-space bounds helpers shared by the 2D and 3D item collectors.</summary>
internal static class SceneBounds
{
    /// <summary>
    /// Promotes a 2D affine matrix to the row-vector 4x4 layout used by the
    /// renderer, moving the translation from row 3 to row 4.
    /// </summary>
    public static Mat4 FromAffine2D(in Mat3 affine) =>
        new(
            affine.M11, affine.M12, 0.0f, 0.0f,
            affine.M21, affine.M22, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f,
            affine.M31, affine.M32, 0.0f, 1.0f);

    /// <summary>Transforms a local sphere by a world matrix, scaling by the largest basis axis.</summary>
    public static BoundingSphere3 TransformSphere(in Mat4 worldMatrix, in BoundingSphere3 local)
    {
        Vec4 center = worldMatrix * new Vec4(local.Center, 1.0f);

        float scaleX = MathF.Sqrt(
            (worldMatrix.M11 * worldMatrix.M11) +
            (worldMatrix.M12 * worldMatrix.M12) +
            (worldMatrix.M13 * worldMatrix.M13));
        float scaleY = MathF.Sqrt(
            (worldMatrix.M21 * worldMatrix.M21) +
            (worldMatrix.M22 * worldMatrix.M22) +
            (worldMatrix.M23 * worldMatrix.M23));
        float scaleZ = MathF.Sqrt(
            (worldMatrix.M31 * worldMatrix.M31) +
            (worldMatrix.M32 * worldMatrix.M32) +
            (worldMatrix.M33 * worldMatrix.M33));

        float maxScale = MathF.Max(scaleX, MathF.Max(scaleY, scaleZ));
        return new BoundingSphere3(
            new Vec3(center.X, center.Y, center.Z),
            local.Radius * maxScale);
    }
}
