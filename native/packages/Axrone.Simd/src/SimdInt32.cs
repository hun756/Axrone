namespace Axrone.Simd;

public static unsafe class SimdInt32
{
    // ── Arithmetic ──────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Add(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count * 2)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector512.LoadUnsafe(in lRef, i) + Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector512.LoadUnsafe(in lRef, i + step) + Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) + Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count * 2)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector256.LoadUnsafe(in lRef, i) + Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector256.LoadUnsafe(in lRef, i + step) + Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) + Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count * 2)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector128.LoadUnsafe(in lRef, i) + Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector128.LoadUnsafe(in lRef, i + step) + Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128.LoadUnsafe(in lRef, i) + Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) + Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) + Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) + Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) + Unsafe.Add(ref rRef, (nint)(i + 3));
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) + Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Subtract(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count * 2)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector512.LoadUnsafe(in lRef, i + step) - Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count * 2)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector256.LoadUnsafe(in lRef, i + step) - Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count * 2)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector128.LoadUnsafe(in lRef, i) - Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector128.LoadUnsafe(in lRef, i + step) - Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128.LoadUnsafe(in lRef, i) - Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) - Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) - Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) - Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) - Unsafe.Add(ref rRef, (nint)(i + 3));
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) - Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Multiply(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count * 2)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector512.LoadUnsafe(in lRef, i + step) * Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count * 2)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector256.LoadUnsafe(in lRef, i + step) * Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count * 2)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector128.LoadUnsafe(in lRef, i + step) * Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) * Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) * Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) * Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) * Unsafe.Add(ref rRef, (nint)(i + 3));
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) * Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Negate(ReadOnlySpan<int> source, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512<int>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256<int>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128<int>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = -Unsafe.Add(ref src, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Abs(ReadOnlySpan<int> source, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector512.Abs(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector256.Abs(Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector128.Abs(Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
        {
            int v = Unsafe.Add(ref src, (nint)i);
            Unsafe.Add(ref dst, (nint)i) = v < 0 ? -v : v;
        }
    }

    // ── Clamping ────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Clamp(ReadOnlySpan<int> source, int min, int max, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            Vector512<int> vMin = Vector512.Create(min), vMax = Vector512.Create(max);
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.Min(Vector512.Max(Vector512.LoadUnsafe(in src, i), vMin), vMax).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            Vector256<int> vMin = Vector256.Create(min), vMax = Vector256.Create(max);
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.Min(Vector256.Max(Vector256.LoadUnsafe(in src, i), vMin), vMax).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            Vector128<int> vMin = Vector128.Create(min), vMax = Vector128.Create(max);
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.Min(Vector128.Max(Vector128.LoadUnsafe(in src, i), vMin), vMax).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
        {
            int v = Unsafe.Add(ref src, (nint)i);
            Unsafe.Add(ref dst, (nint)i) = v < min ? min : (v > max ? max : v);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMax(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
        {
            int l = Unsafe.Add(ref lRef, (nint)i), r = Unsafe.Add(ref rRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = l > r ? l : r;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMin(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
        {
            int l = Unsafe.Add(ref lRef, (nint)i), r = Unsafe.Add(ref rRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = l < r ? l : r;
        }
    }

    // ── Reductions ──────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int ComputeSum(ReadOnlySpan<int> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;
        ref int src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            Vector512<int> acc0 = Vector512.LoadUnsafe(in src, 0), acc1 = Vector512<int>.Zero;
            nuint step = (nuint)Vector512<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                acc0 += Vector512.LoadUnsafe(in src, i);
                acc1 += Vector512.LoadUnsafe(in src, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) acc0 += Vector512.LoadUnsafe(in src, i);
            int sum = Vector512.Sum(acc0) + Vector512.Sum(acc1);
            for (nuint j = i; j < length; ++j) sum += Unsafe.Add(ref src, (nint)j);
            return sum;
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            Vector256<int> acc0 = Vector256.LoadUnsafe(in src, 0), acc1 = Vector256<int>.Zero;
            nuint step = (nuint)Vector256<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2) { acc0 += Vector256.LoadUnsafe(in src, i); acc1 += Vector256.LoadUnsafe(in src, i + step); }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) acc0 += Vector256.LoadUnsafe(in src, i);
            int sum = Vector256.Sum(acc0) + Vector256.Sum(acc1);
            for (nuint j = i; j < length; ++j) sum += Unsafe.Add(ref src, (nint)j);
            return sum;
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            Vector128<int> acc0 = Vector128.LoadUnsafe(in src, 0), acc1 = Vector128<int>.Zero;
            nuint step = (nuint)Vector128<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2) { acc0 += Vector128.LoadUnsafe(in src, i); acc1 += Vector128.LoadUnsafe(in src, i + step); }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) acc0 += Vector128.LoadUnsafe(in src, i);
            int sum = Vector128.Sum(acc0) + Vector128.Sum(acc1);
            for (nuint j = i; j < length; ++j) sum += Unsafe.Add(ref src, (nint)j);
            return sum;
        }
        int s = 0;
        for (nuint j = 0; j < length; ++j) s += Unsafe.Add(ref src, (nint)j);
        return s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int ComputeDotProduct(ReadOnlySpan<int> left, ReadOnlySpan<int> right)
    {
        if (left.Length != right.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return 0;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            Vector512<int> acc0 = Vector512<int>.Zero, acc1 = Vector512<int>.Zero;
            nuint step = (nuint)Vector512<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                acc0 += Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
                acc1 += Vector512.LoadUnsafe(in lRef, i + step) * Vector512.LoadUnsafe(in rRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) acc0 += Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
            int dot = Vector512.Sum(acc0) + Vector512.Sum(acc1);
            for (nuint j = i; j < length; ++j) dot += Unsafe.Add(ref lRef, (nint)j) * Unsafe.Add(ref rRef, (nint)j);
            return dot;
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            Vector256<int> acc0 = Vector256<int>.Zero, acc1 = Vector256<int>.Zero;
            nuint step = (nuint)Vector256<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                acc0 += Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
                acc1 += Vector256.LoadUnsafe(in lRef, i + step) * Vector256.LoadUnsafe(in rRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) acc0 += Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
            int dot = Vector256.Sum(acc0) + Vector256.Sum(acc1);
            for (nuint j = i; j < length; ++j) dot += Unsafe.Add(ref lRef, (nint)j) * Unsafe.Add(ref rRef, (nint)j);
            return dot;
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            Vector128<int> acc0 = Vector128<int>.Zero, acc1 = Vector128<int>.Zero;
            nuint step = (nuint)Vector128<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                acc0 += Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
                acc1 += Vector128.LoadUnsafe(in lRef, i + step) * Vector128.LoadUnsafe(in rRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) acc0 += Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
            int dot = Vector128.Sum(acc0) + Vector128.Sum(acc1);
            for (nuint j = i; j < length; ++j) dot += Unsafe.Add(ref lRef, (nint)j) * Unsafe.Add(ref rRef, (nint)j);
            return dot;
        }
        int d = 0;
        for (nuint j = 0; j < length; ++j) d += Unsafe.Add(ref lRef, (nint)j) * Unsafe.Add(ref rRef, (nint)j);
        return d;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ExtremaPair<int> ComputeExtrema(ReadOnlySpan<int> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();
        ref int src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            Vector512<int> vmin = Vector512.LoadUnsafe(in src, 0), vmax = vmin;
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<int> v = Vector512.LoadUnsafe(in src, i);
                vmin = Vector512.Min(vmin, v);
                vmax = Vector512.Max(vmax, v);
            }
            int* minBuf = stackalloc int[Vector512<int>.Count];
            int* maxBuf = stackalloc int[Vector512<int>.Count];
            vmin.Store(minBuf);
            vmax.Store(maxBuf);
            int min = int.MaxValue, max = int.MinValue;
            for (int k = 0; k < Vector512<int>.Count; k++)
            {
                if (minBuf[k] < min) min = minBuf[k];
                if (maxBuf[k] > max) max = maxBuf[k];
            }
            for (nuint j = i; j < length; ++j)
            {
                int v = Unsafe.Add(ref src, (nint)j);
                if (v < min) min = v;
                if (v > max) max = v;
            }
            return new ExtremaPair<int>(min, max);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            Vector256<int> vmin = Vector256.LoadUnsafe(in src, 0), vmax = vmin;
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<int> v = Vector256.LoadUnsafe(in src, i);
                vmin = Vector256.Min(vmin, v);
                vmax = Vector256.Max(vmax, v);
            }
            int* minBuf = stackalloc int[Vector256<int>.Count];
            int* maxBuf = stackalloc int[Vector256<int>.Count];
            vmin.Store(minBuf);
            vmax.Store(maxBuf);
            int min = int.MaxValue, max = int.MinValue;
            for (int k = 0; k < Vector256<int>.Count; k++)
            {
                if (minBuf[k] < min) min = minBuf[k];
                if (maxBuf[k] > max) max = maxBuf[k];
            }
            for (nuint j = i; j < length; ++j)
            {
                int v = Unsafe.Add(ref src, (nint)j);
                if (v < min) min = v;
                if (v > max) max = v;
            }
            return new ExtremaPair<int>(min, max);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            Vector128<int> vmin = Vector128.LoadUnsafe(in src, 0), vmax = vmin;
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<int> v = Vector128.LoadUnsafe(in src, i);
                vmin = Vector128.Min(vmin, v);
                vmax = Vector128.Max(vmax, v);
            }
            int* minBuf = stackalloc int[Vector128<int>.Count];
            int* maxBuf = stackalloc int[Vector128<int>.Count];
            vmin.Store(minBuf);
            vmax.Store(maxBuf);
            int min = int.MaxValue, max = int.MinValue;
            for (int k = 0; k < Vector128<int>.Count; k++)
            {
                if (minBuf[k] < min) min = minBuf[k];
                if (maxBuf[k] > max) max = maxBuf[k];
            }
            for (nuint j = i; j < length; ++j)
            {
                int v = Unsafe.Add(ref src, (nint)j);
                if (v < min) min = v;
                if (v > max) max = v;
            }
            return new ExtremaPair<int>(min, max);
        }
        int sMin = Unsafe.Add(ref src, 0), sMax = sMin;
        for (nuint j = 1; j < length; ++j)
        {
            int v = Unsafe.Add(ref src, (nint)j);
            if (v < sMin) sMin = v;
            if (v > sMax) sMax = v;
        }
        return new ExtremaPair<int>(sMin, sMax);
    }

    // ── Scan ────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstGreaterThan(ReadOnlySpan<int> source, int threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;
        ref int src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            Vector512<int> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<int> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            Vector256<int> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<int> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            Vector128<int> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<int> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) > threshold)
                return (int)i;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountGreaterThan(ReadOnlySpan<int> source, int threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;
        ref int src = ref MemoryMarshal.GetReference(source);
        nuint count = 0;
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            Vector512<int> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<int> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector512.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            Vector256<int> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<int> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector256.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            Vector128<int> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<int> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector128.ExtractMostSignificantBits(mask));
            }
        }
        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) > threshold)
                ++count;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstEqual(ReadOnlySpan<int> source, int value)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;
        ref int src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            Vector512<int> vVal = Vector512.Create(value);
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<int> mask = Vector512.Equals(Vector512.LoadUnsafe(in src, i), vVal);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            Vector256<int> vVal = Vector256.Create(value);
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<int> mask = Vector256.Equals(Vector256.LoadUnsafe(in src, i), vVal);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            Vector128<int> vVal = Vector128.Create(value);
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<int> mask = Vector128.Equals(Vector128.LoadUnsafe(in src, i), vVal);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) == value)
                return (int)i;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountEqual(ReadOnlySpan<int> source, int value)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;
        ref int src = ref MemoryMarshal.GetReference(source);
        nuint count = 0;
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            Vector512<int> vVal = Vector512.Create(value);
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<int> mask = Vector512.Equals(Vector512.LoadUnsafe(in src, i), vVal);
                count += (nuint)BitOperations.PopCount(Vector512.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            Vector256<int> vVal = Vector256.Create(value);
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<int> mask = Vector256.Equals(Vector256.LoadUnsafe(in src, i), vVal);
                count += (nuint)BitOperations.PopCount(Vector256.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            Vector128<int> vVal = Vector128.Create(value);
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<int> mask = Vector128.Equals(Vector128.LoadUnsafe(in src, i), vVal);
                count += (nuint)BitOperations.PopCount(Vector128.ExtractMostSignificantBits(mask));
            }
        }
        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) == value)
                ++count;
        }
        return count;
    }

    // ── Compare ─────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareEqual(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.Equals(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.Equals(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.Equals(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) == Unsafe.Add(ref rRef, (nint)i) ? -1 : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThan(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.GreaterThan(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.GreaterThan(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.GreaterThan(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) > Unsafe.Add(ref rRef, (nint)i) ? -1 : 0;
    }

    // ── Bitwise ─────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAnd(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.LoadUnsafe(in lRef, i) & Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.LoadUnsafe(in lRef, i) & Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.LoadUnsafe(in lRef, i) & Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) & Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorOr(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.LoadUnsafe(in lRef, i) | Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.LoadUnsafe(in lRef, i) | Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.LoadUnsafe(in lRef, i) | Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) | Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorXor(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.LoadUnsafe(in lRef, i) ^ Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.LoadUnsafe(in lRef, i) ^ Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.LoadUnsafe(in lRef, i) ^ Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) ^ Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ShiftLeft(ReadOnlySpan<int> source, int count, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.LoadUnsafe(in src, i) << count).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.LoadUnsafe(in src, i) << count).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.LoadUnsafe(in src, i) << count).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, (nint)i) << count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ShiftRight(ReadOnlySpan<int> source, int count, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.LoadUnsafe(in src, i) >> count).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.LoadUnsafe(in src, i) >> count).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.LoadUnsafe(in src, i) >> count).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, (nint)i) >> count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAbs(ReadOnlySpan<int> source, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector512.Abs(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector256.Abs(Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector128.Abs(Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = Math.Abs(Unsafe.Add(ref src, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorNegate(ReadOnlySpan<int> source, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count * 2)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector512<int>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector512<int>.Zero - Vector512.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) (Vector512<int>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count * 2)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector256<int>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector256<int>.Zero - Vector256.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) (Vector256<int>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count * 2)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector128<int>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector128<int>.Zero - Vector128.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) (Vector128<int>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = -Unsafe.Add(ref src, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorMin(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count * 2)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i + step), Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count * 2)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i + step), Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count * 2)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i + step), Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = Math.Min(Unsafe.Add(ref lRef, (nint)i), Unsafe.Add(ref rRef, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorMax(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count * 2)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i + step), Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count * 2)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i + step), Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count * 2)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i + step), Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = Math.Max(Unsafe.Add(ref lRef, (nint)i), Unsafe.Add(ref rRef, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorClamp(ReadOnlySpan<int> source, int min, int max, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int sRef = ref MemoryMarshal.GetReference(source);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            Vector512<int> vMin = Vector512.Create(min), vMax = Vector512.Create(max);
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector512.Min(Vector512.Max(Vector512.LoadUnsafe(in sRef, i), vMin), vMax).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            Vector256<int> vMin = Vector256.Create(min), vMax = Vector256.Create(max);
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector256.Min(Vector256.Max(Vector256.LoadUnsafe(in sRef, i), vMin), vMax).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            Vector128<int> vMin = Vector128.Create(min), vMax = Vector128.Create(max);
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector128.Min(Vector128.Max(Vector128.LoadUnsafe(in sRef, i), vMin), vMax).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = Math.Clamp(Unsafe.Add(ref sRef, (nint)i), min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThan(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref int lRef = ref MemoryMarshal.GetReference(left);
        ref int rRef = ref MemoryMarshal.GetReference(right);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector512.LessThan(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsInt32().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector256.LessThan(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsInt32().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector128.LessThan(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsInt32().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) < Unsafe.Add(ref rRef, (nint)i) ? -1 : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void TransformLinear(ReadOnlySpan<int> source, int multiplier, int offset, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count * 2)
        {
            Vector512<int> vMul = Vector512.Create(multiplier), vOff = Vector512.Create(offset);
            nuint step = (nuint)Vector512<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector512.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
                ((Vector512.LoadUnsafe(in src, i + step) * vMul) + vOff).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) ((Vector512.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count * 2)
        {
            Vector256<int> vMul = Vector256.Create(multiplier), vOff = Vector256.Create(offset);
            nuint step = (nuint)Vector256<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector256.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
                ((Vector256.LoadUnsafe(in src, i + step) * vMul) + vOff).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) ((Vector256.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count * 2)
        {
            Vector128<int> vMul = Vector128.Create(multiplier), vOff = Vector128.Create(offset);
            nuint step = (nuint)Vector128<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector128.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
                ((Vector128.LoadUnsafe(in src, i + step) * vMul) + vOff).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) ((Vector128.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
        }
        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dst, (nint)(i + 0)) = (Unsafe.Add(ref src, (nint)(i + 0)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 1)) = (Unsafe.Add(ref src, (nint)(i + 1)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 2)) = (Unsafe.Add(ref src, (nint)(i + 2)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 3)) = (Unsafe.Add(ref src, (nint)(i + 3)) * multiplier) + offset;
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = (Unsafe.Add(ref src, (nint)i) * multiplier) + offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFma(ReadOnlySpan<int> a, ReadOnlySpan<int> b, ReadOnlySpan<int> c, Span<int> destination)
    {
        if (a.Length != b.Length || a.Length != c.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;
        ref int aRef = ref MemoryMarshal.GetReference(a);
        ref int bRef = ref MemoryMarshal.GetReference(b);
        ref int cRef = ref MemoryMarshal.GetReference(c);
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count * 2)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) + Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector512.LoadUnsafe(in aRef, i + step) * Vector512.LoadUnsafe(in bRef, i + step)) + Vector512.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) + Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count * 2)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) + Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector256.LoadUnsafe(in aRef, i + step) * Vector256.LoadUnsafe(in bRef, i + step)) + Vector256.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) + Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count * 2)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) + Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector128.LoadUnsafe(in aRef, i + step) * Vector128.LoadUnsafe(in bRef, i + step)) + Vector128.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) + Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = (Unsafe.Add(ref aRef, (nint)(i + 0)) * Unsafe.Add(ref bRef, (nint)(i + 0))) + Unsafe.Add(ref cRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = (Unsafe.Add(ref aRef, (nint)(i + 1)) * Unsafe.Add(ref bRef, (nint)(i + 1))) + Unsafe.Add(ref cRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = (Unsafe.Add(ref aRef, (nint)(i + 2)) * Unsafe.Add(ref bRef, (nint)(i + 2))) + Unsafe.Add(ref cRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = (Unsafe.Add(ref aRef, (nint)(i + 3)) * Unsafe.Add(ref bRef, (nint)(i + 3))) + Unsafe.Add(ref cRef, (nint)(i + 3));
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = (Unsafe.Add(ref aRef, (nint)i) * Unsafe.Add(ref bRef, (nint)i)) + Unsafe.Add(ref cRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void BitwiseNot(ReadOnlySpan<int> source, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (~Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (~Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (~Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = ~Unsafe.Add(ref src, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ConvertToFloat(ReadOnlySpan<int> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector512.ConvertToSingle(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector256.ConvertToSingle(Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector128.ConvertToSingle(Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = (float)Unsafe.Add(ref src, (nint)i);
    }
}
