namespace Axrone.Simd;

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static class SimdFloatingPointOps<T> where T : unmanaged, IFloatingPoint<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T AllBitsSet()
    {
        if (typeof(T) == typeof(float))
            return (T)(object)BitConverter.Int32BitsToSingle(-1);
        return (T)(object)BitConverter.Int64BitsToDouble(-1L);
    }

    // ── Arithmetic ──────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAdd(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector.LoadUnsafe(in lRef, i) + Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector.LoadUnsafe(in lRef, i + step) + Vector.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector.LoadUnsafe(in lRef, i) + Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) + Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSubtract(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector.LoadUnsafe(in lRef, i) - Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector.LoadUnsafe(in lRef, i + step) - Vector.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector.LoadUnsafe(in lRef, i) - Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) - Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorMultiply(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector.LoadUnsafe(in lRef, i) * Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector.LoadUnsafe(in lRef, i + step) * Vector.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector.LoadUnsafe(in lRef, i) * Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) * Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorDivide(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector.LoadUnsafe(in lRef, i) / Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                (Vector.LoadUnsafe(in lRef, i + step) / Vector.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector.LoadUnsafe(in lRef, i) / Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) / Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorNegate(ReadOnlySpan<T> source, Span<T> destination)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector<T>.Zero - Vector.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector<T>.Zero - Vector.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector<T>.Zero - Vector.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = -Unsafe.Add(ref src, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAbs(ReadOnlySpan<T> source, Span<T> destination)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector.Abs(Vector.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = T.Abs(Unsafe.Add(ref src, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void TransformLinear(ReadOnlySpan<T> source, T multiplier, T offset, Span<T> destination)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            Vector<T> vMul = Vector.Create(multiplier), vOff = Vector.Create(offset);
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
                ((Vector.LoadUnsafe(in src, i + step) * vMul) + vOff).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) ((Vector.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, (nint)i) * multiplier + offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFma(ReadOnlySpan<T> a, ReadOnlySpan<T> b, ReadOnlySpan<T> c, Span<T> destination)
    {
        ThrowHelper.ValidateTernarySpans(a, b, c, destination);
        nuint length = (nuint)a.Length;
        if (length == 0) return;
        ref T aRef = ref MemoryMarshal.GetReference(a);
        ref T bRef = ref MemoryMarshal.GetReference(b);
        ref T cRef = ref MemoryMarshal.GetReference(c);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector.LoadUnsafe(in aRef, i) * Vector.LoadUnsafe(in bRef, i)) + Vector.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector.LoadUnsafe(in aRef, i + step) * Vector.LoadUnsafe(in bRef, i + step)) + Vector.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector.LoadUnsafe(in aRef, i) * Vector.LoadUnsafe(in bRef, i)) + Vector.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref aRef, (nint)i) * Unsafe.Add(ref bRef, (nint)i) + Unsafe.Add(ref cRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFms(ReadOnlySpan<T> a, ReadOnlySpan<T> b, ReadOnlySpan<T> c, Span<T> destination)
    {
        ThrowHelper.ValidateTernarySpans(a, b, c, destination);
        nuint length = (nuint)a.Length;
        if (length == 0) return;
        ref T aRef = ref MemoryMarshal.GetReference(a);
        ref T bRef = ref MemoryMarshal.GetReference(b);
        ref T cRef = ref MemoryMarshal.GetReference(c);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                ((Vector.LoadUnsafe(in aRef, i) * Vector.LoadUnsafe(in bRef, i)) - Vector.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
                ((Vector.LoadUnsafe(in aRef, i + step) * Vector.LoadUnsafe(in bRef, i + step)) - Vector.LoadUnsafe(in cRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector.LoadUnsafe(in aRef, i) * Vector.LoadUnsafe(in bRef, i)) - Vector.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref aRef, (nint)i) * Unsafe.Add(ref bRef, (nint)i) - Unsafe.Add(ref cRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLerp(ReadOnlySpan<T> a, ReadOnlySpan<T> b, T t, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(a, b, destination);
        nuint length = (nuint)a.Length;
        if (length == 0) return;
        ref T aRef = ref MemoryMarshal.GetReference(a);
        ref T bRef = ref MemoryMarshal.GetReference(b);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            Vector<T> vT = Vector.Create(t), vOne = Vector.Create(T.One);
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector<T> a0 = Vector.LoadUnsafe(in aRef, i), b0 = Vector.LoadUnsafe(in bRef, i);
                (a0 + (b0 - a0) * vT).StoreUnsafe(ref dRef, i);
                Vector<T> a1 = Vector.LoadUnsafe(in aRef, i + step), b1 = Vector.LoadUnsafe(in bRef, i + step);
                (a1 + (b1 - a1) * vT).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector<T> a0 = Vector.LoadUnsafe(in aRef, i), b0 = Vector.LoadUnsafe(in bRef, i);
                (a0 + (b0 - a0) * vT).StoreUnsafe(ref dRef, i);
            }
        }
        T oneMinusT = T.One - t;
        for (; i < length; ++i)
        {
            T aVal = Unsafe.Add(ref aRef, (nint)i), bVal = Unsafe.Add(ref bRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = aVal * oneMinusT + bVal * t;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorClamp(ReadOnlySpan<T> source, T min, T max, Span<T> destination)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> vMin = Vector.Create(min), vMax = Vector.Create(max);
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector.Clamp(Vector.LoadUnsafe(in src, i), vMin, vMax).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
        {
            T v = Unsafe.Add(ref src, (nint)i);
            Unsafe.Add(ref dst, (nint)i) = v < min ? min : (v > max ? max : v);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMax(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector.Max(Vector.LoadUnsafe(in lRef, i), Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector.Max(Vector.LoadUnsafe(in lRef, i + step), Vector.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector.Max(Vector.LoadUnsafe(in lRef, i), Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
        {
            T l = Unsafe.Add(ref lRef, (nint)i), r = Unsafe.Add(ref rRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = l > r ? l : r;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMin(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector.Min(Vector.LoadUnsafe(in lRef, i), Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector.Min(Vector.LoadUnsafe(in lRef, i + step), Vector.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector.Min(Vector.LoadUnsafe(in lRef, i), Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
        {
            T l = Unsafe.Add(ref lRef, (nint)i), r = Unsafe.Add(ref rRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = l < r ? l : r;
        }
    }

    // ── Utility ─────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Fill(Span<T> destination, T value)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> v = Vector.Create(value);
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void FillLinear(Span<T> destination, T start, T step)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            nuint vc = (nuint)Vector<T>.Count;
            Span<T> indices = stackalloc T[(int)vc];
            for (int k = 0; k < (int)vc; k++) indices[k] = T.CreateChecked(k);
            Vector<T> vIndices = Vector.Create(indices);
            Vector<T> vStep = Vector.Create(step);
            Vector<T> vBase = Vector.Create(start);
            Vector<T> vOffset = vStep * Vector.Create(T.CreateChecked((int)vc));
            Vector<T> current = vBase + vStep * vIndices;
            nuint limit = length - vc + 1;
            for (; i < limit; i += vc)
            {
                current.StoreUnsafe(ref dst, i);
                current += vOffset;
            }
        }
        T val = start + T.CreateChecked((long)i) * step;
        for (; i < length; ++i)
        {
            Unsafe.Add(ref dst, (nint)i) = val;
            val += step;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Gather(ReadOnlySpan<T> source, ReadOnlySpan<int> indices, Span<T> destination)
    {
        ThrowHelper.ValidateDestinationSpan(indices, destination);
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        for (nuint i = 0; i < count; ++i)
            Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, Unsafe.Add(ref idx, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Scatter(ReadOnlySpan<T> source, ReadOnlySpan<int> indices, Span<T> destination)
    {
        if (source.Length > destination.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        for (nuint i = 0; i < count; ++i)
            Unsafe.Add(ref dst, Unsafe.Add(ref idx, (nint)i)) = Unsafe.Add(ref src, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Swizzle(ReadOnlySpan<T> source, ReadOnlySpan<int> indices, Span<T> destination)
        => Gather(source, indices, destination);

    // ── Math ────────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorReLU(ReadOnlySpan<T> source, Span<T> destination)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> zero = Vector<T>.Zero;
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector.Max(Vector.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
        {
            T v = Unsafe.Add(ref src, (nint)i);
            Unsafe.Add(ref dst, (nint)i) = v > T.Zero ? v : T.Zero;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorRSqrt(ReadOnlySpan<T> source, Span<T> destination)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        for (nuint i = 0; i < length; ++i)
        {
            T value = Unsafe.Add(ref src, (nint)i);
            T sqrt = typeof(T) == typeof(float)
                ? (T)(object)MathF.Sqrt((float)(object)value!)
                : (T)(object)Math.Sqrt((double)(object)value!);
            Unsafe.Add(ref dst, (nint)i) = T.One / sqrt;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Softmax(ReadOnlySpan<T> source, Span<T> destination)
    {
        ThrowHelper.ValidateDestinationSpan(source, destination);
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);

        T maxVal = ComputeMax(ref src, length);
        T sum = T.Zero;
        for (nuint i = 0; i < length; ++i)
        {
            T val = Unsafe.Add(ref src, (nint)i) - maxVal;
            T e = typeof(T) == typeof(float)
                ? (T)(object)MathF.Exp((float)(object)val!)
                : (T)(object)Math.Exp((double)(object)val!);
            Unsafe.Add(ref dst, (nint)i) = e;
            sum += e;
        }
        T invSum = T.One / sum;
        nuint i2 = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> vInv = Vector.Create(invSum);
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i2 < limit; i2 += step)
                (Vector.LoadUnsafe(in dst, i2) * vInv).StoreUnsafe(ref dst, i2);
        }
        for (; i2 < length; ++i2)
            Unsafe.Add(ref dst, (nint)i2) = Unsafe.Add(ref dst, (nint)i2) * invSum;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ComputeMax(ref T src, nuint length)
    {
        T maxVal = Unsafe.Add(ref src, 0);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> vmax = Vector.LoadUnsafe(in src, 0);
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step) vmax = Vector.Max(vmax, Vector.LoadUnsafe(in src, i));
            Span<T> buf = stackalloc T[(int)step];
            vmax.CopyTo(buf);
            for (int k = 0; k < (int)step; k++) if (buf[k] > maxVal) maxVal = buf[k];
            i = step;
        }
        for (; i < length; ++i)
        {
            T v = Unsafe.Add(ref src, (nint)i);
            if (v > maxVal) maxVal = v;
        }
        return maxVal;
    }

    // ── Reductions ──────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T ComputeSum(ReadOnlySpan<T> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return T.Zero;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            Vector<T> acc0 = Vector<T>.Zero, acc1 = Vector<T>.Zero;
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                acc0 += Vector.LoadUnsafe(in src, i);
                acc1 += Vector.LoadUnsafe(in src, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) acc0 += Vector.LoadUnsafe(in src, i);
            T sum = Vector.Sum(acc0) + Vector.Sum(acc1);
            for (nuint j = i; j < length; ++j) sum += Unsafe.Add(ref src, (nint)j);
            return sum;
        }
        T s = T.Zero;
        for (nuint j = 0; j < length; ++j) s += Unsafe.Add(ref src, (nint)j);
        return s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T ComputeProduct(ReadOnlySpan<T> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return T.One;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> acc = Vector.Create(T.One);
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step) acc *= Vector.LoadUnsafe(in src, i);
            T product = T.One;
            Span<T> buf = stackalloc T[(int)step];
            acc.CopyTo(buf);
            for (int k = 0; k < (int)step; k++) product *= buf[k];
            for (nuint j = i; j < length; ++j) product *= Unsafe.Add(ref src, (nint)j);
            return product;
        }
        T p = T.One;
        for (nuint j = 0; j < length; ++j) p *= Unsafe.Add(ref src, (nint)j);
        return p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T ComputeSumOfSquares(ReadOnlySpan<T> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return T.Zero;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            Vector<T> acc0 = Vector<T>.Zero, acc1 = Vector<T>.Zero;
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector<T> v0 = Vector.LoadUnsafe(in src, i);
                Vector<T> v1 = Vector.LoadUnsafe(in src, i + step);
                acc0 += v0 * v0;
                acc1 += v1 * v1;
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector<T> v = Vector.LoadUnsafe(in src, i);
                acc0 += v * v;
            }
            T sum = Vector.Sum(acc0) + Vector.Sum(acc1);
            for (nuint j = i; j < length; ++j) { T v = Unsafe.Add(ref src, (nint)j); sum += v * v; }
            return sum;
        }
        T s = T.Zero;
        for (nuint j = 0; j < length; ++j) { T v = Unsafe.Add(ref src, (nint)j); s += v * v; }
        return s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T ComputeMean(ReadOnlySpan<T> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return T.Zero;
        return ComputeSum(source) / T.CreateChecked(length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T ComputeVariance(ReadOnlySpan<T> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return T.Zero;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            Vector<T> accS = Vector<T>.Zero, accQ = Vector<T>.Zero;
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector<T> v0 = Vector.LoadUnsafe(in src, i);
                Vector<T> v1 = Vector.LoadUnsafe(in src, i + step);
                accS += v0 + v1;
                accQ += v0 * v0 + v1 * v1;
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector<T> v = Vector.LoadUnsafe(in src, i);
                accS += v; accQ += v * v;
            }
            T sum = Vector.Sum(accS), sumSq = Vector.Sum(accQ);
            for (nuint j = i; j < length; ++j) { T v = Unsafe.Add(ref src, (nint)j); sum += v; sumSq += v * v; }
            T n = T.CreateChecked(length); T mean = sum / n;
            return sumSq / n - mean * mean;
        }
        T s = T.Zero, sq = T.Zero;
        for (nuint j = 0; j < length; ++j) { T v = Unsafe.Add(ref src, (nint)j); s += v; sq += v * v; }
        T mean2 = s / T.CreateChecked(length);
        return sq / T.CreateChecked(length) - mean2 * mean2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T ComputeStdDev(ReadOnlySpan<T> source)
    {
        T variance = ComputeVariance(source);
        return typeof(T) == typeof(float)
            ? (T)(object)MathF.Sqrt((float)(object)variance!)
            : (T)(object)Math.Sqrt((double)(object)variance!);
    }

    // ── Norms ───────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T ComputeDotProduct(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
    {
        ThrowHelper.ValidateBinarySpans(left, right, left);
        nuint length = (nuint)left.Length;
        if (length == 0) return T.Zero;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            Vector<T> acc0 = Vector<T>.Zero, acc1 = Vector<T>.Zero;
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                acc0 += Vector.LoadUnsafe(in lRef, i) * Vector.LoadUnsafe(in rRef, i);
                acc1 += Vector.LoadUnsafe(in lRef, i + step) * Vector.LoadUnsafe(in rRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                acc0 += Vector.LoadUnsafe(in lRef, i) * Vector.LoadUnsafe(in rRef, i);
            T dot = Vector.Sum(acc0) + Vector.Sum(acc1);
            for (nuint j = i; j < length; ++j) dot += Unsafe.Add(ref lRef, (nint)j) * Unsafe.Add(ref rRef, (nint)j);
            return dot;
        }
        T d = T.Zero;
        for (nuint j = 0; j < length; ++j) d += Unsafe.Add(ref lRef, (nint)j) * Unsafe.Add(ref rRef, (nint)j);
        return d;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T ComputeL2Norm(ReadOnlySpan<T> source)
    {
        T sumSq = ComputeSumOfSquares(source);
        return typeof(T) == typeof(float)
            ? (T)(object)MathF.Sqrt((float)(object)sumSq!)
            : (T)(object)Math.Sqrt((double)(object)sumSq!);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T ComputeL1Norm(ReadOnlySpan<T> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return T.Zero;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count * 2)
        {
            Vector<T> acc0 = Vector<T>.Zero, acc1 = Vector<T>.Zero;
            nuint step = (nuint)Vector<T>.Count, limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                acc0 += Vector.Abs(Vector.LoadUnsafe(in src, i));
                acc1 += Vector.Abs(Vector.LoadUnsafe(in src, i + step));
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) acc0 += Vector.Abs(Vector.LoadUnsafe(in src, i));
            T sum = Vector.Sum(acc0) + Vector.Sum(acc1);
            for (nuint j = i; j < length; ++j) sum += T.Abs(Unsafe.Add(ref src, (nint)j));
            return sum;
        }
        T s = T.Zero;
        for (nuint j = 0; j < length; ++j) s += T.Abs(Unsafe.Add(ref src, (nint)j));
        return s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T ComputeLinfNorm(ReadOnlySpan<T> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return T.Zero;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        T maxVal = T.Zero;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> vmax = Vector<T>.Zero;
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step) vmax = Vector.Max(vmax, Vector.Abs(Vector.LoadUnsafe(in src, i)));
            Span<T> buf = stackalloc T[(int)step];
            vmax.CopyTo(buf);
            for (int k = 0; k < (int)step; k++) if (buf[k] > maxVal) maxVal = buf[k];
        }
        for (; i < length; ++i)
        {
            T v = T.Abs(Unsafe.Add(ref src, (nint)i));
            if (v > maxVal) maxVal = v;
        }
        return maxVal;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ExtremaPair<T> ComputeExtrema(ReadOnlySpan<T> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> vmin = Vector.LoadUnsafe(in src, 0), vmax = vmin;
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector<T> v = Vector.LoadUnsafe(in src, i);
                vmin = Vector.Min(vmin, v);
                vmax = Vector.Max(vmax, v);
            }
            Span<T> minBuf = stackalloc T[(int)step];
            Span<T> maxBuf = stackalloc T[(int)step];
            vmin.CopyTo(minBuf);
            vmax.CopyTo(maxBuf);
            T min = minBuf[0], max = maxBuf[0];
            for (int k = 1; k < (int)step; k++)
            {
                if (minBuf[k] < min) min = minBuf[k];
                if (maxBuf[k] > max) max = maxBuf[k];
            }
            for (nuint j = i; j < length; ++j)
            {
                T v = Unsafe.Add(ref src, (nint)j);
                if (v < min) min = v;
                if (v > max) max = v;
            }
            return new ExtremaPair<T>(min, max);
        }
        T sMin = Unsafe.Add(ref src, 0), sMax = sMin;
        for (nuint j = 1; j < length; ++j)
        {
            T v = Unsafe.Add(ref src, (nint)j);
            if (v < sMin) sMin = v;
            if (v > sMax) sMax = v;
        }
        return new ExtremaPair<T>(sMin, sMax);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMin(ReadOnlySpan<T> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();
        ref T src = ref MemoryMarshal.GetReference(source);
        T minVal = Unsafe.Add(ref src, 0);
        nuint minIdx = 0;
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> vmin = Vector.LoadUnsafe(in src, 0);
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                vmin = Vector.Min(vmin, Vector.LoadUnsafe(in src, i));
            Span<T> buf = stackalloc T[(int)step];
            vmin.CopyTo(buf);
            for (int k = 0; k < (int)step; k++)
            {
                if (buf[k] < minVal) { minVal = buf[k]; minIdx = i; }
            }
        }
        for (; i < length; ++i)
        {
            T v = Unsafe.Add(ref src, (nint)i);
            if (v < minVal) { minVal = v; minIdx = i; }
        }
        return minIdx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMax(ReadOnlySpan<T> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();
        ref T src = ref MemoryMarshal.GetReference(source);
        T maxVal = Unsafe.Add(ref src, 0);
        nuint maxIdx = 0;
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> vmax = Vector.LoadUnsafe(in src, 0);
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                vmax = Vector.Max(vmax, Vector.LoadUnsafe(in src, i));
            Span<T> buf = stackalloc T[(int)step];
            vmax.CopyTo(buf);
            for (int k = 0; k < (int)step; k++)
            {
                if (buf[k] > maxVal) { maxVal = buf[k]; maxIdx = i; }
            }
        }
        for (; i < length; ++i)
        {
            T v = Unsafe.Add(ref src, (nint)i);
            if (v > maxVal) { maxVal = v; maxIdx = i; }
        }
        return maxIdx;
    }

    // ── Scan / Compare ──────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstGreaterThan(ReadOnlySpan<T> source, T threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) > threshold) return (int)i;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountGreaterThan(ReadOnlySpan<T> source, T threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint count = 0;
        for (nuint i = 0; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) > threshold) ++count;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterGreaterThan(ReadOnlySpan<T> source, T threshold, Span<T> destination)
    {
        nuint length = (nuint)source.Length;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint written = 0;
        for (nuint i = 0; i < length; ++i)
        {
            T v = Unsafe.Add(ref src, (nint)i);
            if (v > threshold)
            {
                if (written >= (nuint)destination.Length) return ScanResult.Overflow(written);
                Unsafe.Add(ref dst, (nint)written) = v;
                ++written;
            }
        }
        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstLessThan(ReadOnlySpan<T> source, T threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) < threshold) return (int)i;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountLessThan(ReadOnlySpan<T> source, T threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint count = 0;
        for (nuint i = 0; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) < threshold) ++count;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterLessThan(ReadOnlySpan<T> source, T threshold, Span<T> destination)
    {
        nuint length = (nuint)source.Length;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint written = 0;
        for (nuint i = 0; i < length; ++i)
        {
            T v = Unsafe.Add(ref src, (nint)i);
            if (v < threshold)
            {
                if (written >= (nuint)destination.Length) return ScanResult.Overflow(written);
                Unsafe.Add(ref dst, (nint)written) = v;
                ++written;
            }
        }
        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstEqual(ReadOnlySpan<T> source, T value)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) == value) return (int)i;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountEqual(ReadOnlySpan<T> source, T value)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;
        ref T src = ref MemoryMarshal.GetReference(source);
        nuint count = 0;
        for (nuint i = 0; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) == value) ++count;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterEqual(ReadOnlySpan<T> source, T value, Span<T> destination)
    {
        nuint length = (nuint)source.Length;
        ref T src = ref MemoryMarshal.GetReference(source);
        ref T dst = ref MemoryMarshal.GetReference(destination);
        nuint written = 0;
        for (nuint i = 0; i < length; ++i)
        {
            T v = Unsafe.Add(ref src, (nint)i);
            if (v == value)
            {
                if (written >= (nuint)destination.Length) return ScanResult.Overflow(written);
                Unsafe.Add(ref dst, (nint)written) = v;
                ++written;
            }
        }
        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<T> condition, ReadOnlySpan<T> trueValues, ReadOnlySpan<T> falseValues, Span<T> destination)
    {
        ThrowHelper.ValidateTernarySpans(condition, trueValues, falseValues, destination);
        nuint length = (nuint)condition.Length;
        if (length == 0) return;
        ref T cRef = ref MemoryMarshal.GetReference(condition);
        ref T tRef = ref MemoryMarshal.GetReference(trueValues);
        ref T fRef = ref MemoryMarshal.GetReference(falseValues);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector<T> cond = Vector.LoadUnsafe(in cRef, i);
                Vector.ConditionalSelect(cond, Vector.LoadUnsafe(in tRef, i), Vector.LoadUnsafe(in fRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref cRef, (nint)i) != T.Zero ? Unsafe.Add(ref tRef, (nint)i) : Unsafe.Add(ref fRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<T> condition, T trueValue, T falseValue, Span<T> destination)
    {
        ThrowHelper.ValidateDestinationSpan(condition, destination);
        nuint length = (nuint)condition.Length;
        if (length == 0) return;
        ref T cRef = ref MemoryMarshal.GetReference(condition);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            Vector<T> vTrue = Vector.Create(trueValue), vFalse = Vector.Create(falseValue);
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector<T> cond = Vector.LoadUnsafe(in cRef, i);
                Vector.ConditionalSelect(cond, vTrue, vFalse).StoreUnsafe(ref dRef, i);
            }
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref cRef, (nint)i) != T.Zero ? trueValue : falseValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThan(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector.LessThan(Vector.LoadUnsafe(in lRef, i), Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        T mask = AllBitsSet();
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) < Unsafe.Add(ref rRef, (nint)i) ? mask : T.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThan(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector.GreaterThan(Vector.LoadUnsafe(in lRef, i), Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        T mask = AllBitsSet();
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) > Unsafe.Add(ref rRef, (nint)i) ? mask : T.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector.Equals(Vector.LoadUnsafe(in lRef, i), Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        T mask = AllBitsSet();
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) == Unsafe.Add(ref rRef, (nint)i) ? mask : T.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareNotEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector.OnesComplement(Vector.Equals(Vector.LoadUnsafe(in lRef, i), Vector.LoadUnsafe(in rRef, i))).StoreUnsafe(ref dRef, i);
        }
        T mask = AllBitsSet();
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) != Unsafe.Add(ref rRef, (nint)i) ? mask : T.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThanOrEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector.LessThanOrEqual(Vector.LoadUnsafe(in lRef, i), Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        T mask = AllBitsSet();
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) <= Unsafe.Add(ref rRef, (nint)i) ? mask : T.Zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThanOrEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        ThrowHelper.ValidateBinarySpans(left, right, destination);
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector.IsHardwareAccelerated && length >= (nuint)Vector<T>.Count)
        {
            nuint step = (nuint)Vector<T>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector.GreaterThanOrEqual(Vector.LoadUnsafe(in lRef, i), Vector.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        T mask = AllBitsSet();
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) >= Unsafe.Add(ref rRef, (nint)i) ? mask : T.Zero;
    }

    // ── Geometry ────────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Mat4x4Multiply(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        if (left.Length < 16 || right.Length < 16 || destination.Length < 16) ThrowHelper.ThrowMismatchedSpans();
        ref T lRef = ref MemoryMarshal.GetReference(left);
        ref T rRef = ref MemoryMarshal.GetReference(right);
        ref T dRef = ref MemoryMarshal.GetReference(destination);
        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                T sum = T.Zero;
                for (int k = 0; k < 4; k++)
                    sum += Unsafe.Add(ref lRef, row * 4 + k) * Unsafe.Add(ref rRef, k * 4 + col);
                Unsafe.Add(ref dRef, row * 4 + col) = sum;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CrossProduct(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        if (left.Length < 3 || right.Length < 3 || destination.Length < 3) ThrowHelper.ThrowMismatchedSpans();
        ref T l = ref MemoryMarshal.GetReference(left);
        ref T r = ref MemoryMarshal.GetReference(right);
        ref T d = ref MemoryMarshal.GetReference(destination);
        T l0 = Unsafe.Add(ref l, 0), l1 = Unsafe.Add(ref l, 1), l2 = Unsafe.Add(ref l, 2);
        T r0 = Unsafe.Add(ref r, 0), r1 = Unsafe.Add(ref r, 1), r2 = Unsafe.Add(ref r, 2);
        Unsafe.Add(ref d, 0) = l1 * r2 - l2 * r1;
        Unsafe.Add(ref d, 1) = l2 * r0 - l0 * r2;
        Unsafe.Add(ref d, 2) = l0 * r1 - l1 * r0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionMultiply(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    {
        if (left.Length < 4 || right.Length < 4 || destination.Length < 4) ThrowHelper.ThrowMismatchedSpans();
        ref T l = ref MemoryMarshal.GetReference(left);
        ref T r = ref MemoryMarshal.GetReference(right);
        ref T d = ref MemoryMarshal.GetReference(destination);
        T lx = Unsafe.Add(ref l, 0), ly = Unsafe.Add(ref l, 1), lz = Unsafe.Add(ref l, 2), lw = Unsafe.Add(ref l, 3);
        T rx = Unsafe.Add(ref r, 0), ry = Unsafe.Add(ref r, 1), rz = Unsafe.Add(ref r, 2), rw = Unsafe.Add(ref r, 3);
        Unsafe.Add(ref d, 0) = lw * rx + lx * rw + ly * rz - lz * ry;
        Unsafe.Add(ref d, 1) = lw * ry - lx * rz + ly * rw + lz * rx;
        Unsafe.Add(ref d, 2) = lw * rz + lx * ry - ly * rx + lz * rw;
        Unsafe.Add(ref d, 3) = lw * rw - lx * rx - ly * ry - lz * rz;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionSlerp(ReadOnlySpan<T> start, ReadOnlySpan<T> end, T t, Span<T> destination)
    {
        if (start.Length < 4 || end.Length < 4 || destination.Length < 4) ThrowHelper.ThrowMismatchedSpans();
        ref T s = ref MemoryMarshal.GetReference(start);
        ref T e = ref MemoryMarshal.GetReference(end);
        ref T d = ref MemoryMarshal.GetReference(destination);
        T dot = T.Zero;
        for (int i = 0; i < 4; i++) dot += Unsafe.Add(ref s, i) * Unsafe.Add(ref e, i);
        if (dot < T.Zero) { dot = -dot; for (int i = 0; i < 4; i++) Unsafe.Add(ref e, i) = -Unsafe.Add(ref e, i); }
        T scale0, scale1;
        if (dot < T.CreateChecked(0.9995))
        {
            T theta = typeof(T) == typeof(float)
                ? (T)(object)MathF.Acos((float)(object)dot!)
                : (T)(object)Math.Acos((double)(object)dot!);
            T sinTheta = typeof(T) == typeof(float)
                ? (T)(object)MathF.Sin((float)(object)theta!)
                : (T)(object)Math.Sin((double)(object)theta!);
            T oneMinusTTheta = (T.One - t) * theta;
            T tTheta = t * theta;
            T sin0 = typeof(T) == typeof(float)
                ? (T)(object)MathF.Sin((float)(object)oneMinusTTheta!)
                : (T)(object)Math.Sin((double)(object)oneMinusTTheta!);
            T sin1 = typeof(T) == typeof(float)
                ? (T)(object)MathF.Sin((float)(object)tTheta!)
                : (T)(object)Math.Sin((double)(object)tTheta!);
            scale0 = sin0 / sinTheta;
            scale1 = sin1 / sinTheta;
        }
        else
        {
            scale0 = T.One - t;
            scale1 = t;
        }
        for (int i = 0; i < 4; i++)
            Unsafe.Add(ref d, i) = scale0 * Unsafe.Add(ref s, i) + scale1 * Unsafe.Add(ref e, i);
    }
}
