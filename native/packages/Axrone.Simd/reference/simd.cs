#nullable enable

namespace CoreEngine.SimdArchitecture;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Threading;

public interface IVectorTransformer<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    void TransformLinear(ReadOnlySpan<T> source, T multiplier, T offset, Span<T> destination);
    void VectorAdd(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void VectorMultiply(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void VectorFma(ReadOnlySpan<T> a, ReadOnlySpan<T> b, ReadOnlySpan<T> c, Span<T> destination);
    void VectorClamp(ReadOnlySpan<T> source, T min, T max, Span<T> destination);
    void VectorSubtract(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void VectorNegate(ReadOnlySpan<T> source, Span<T> destination);
    void VectorDivide(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void VectorAbs(ReadOnlySpan<T> source, Span<T> destination);
    void VectorFms(ReadOnlySpan<T> a, ReadOnlySpan<T> b, ReadOnlySpan<T> c, Span<T> destination);
    void VectorLerp(ReadOnlySpan<T> a, ReadOnlySpan<T> b, T t, Span<T> destination);
    void ElementWiseMax(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void ElementWiseMin(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
}

public interface IVectorReducer<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    T ComputeSum(ReadOnlySpan<T> source);
    T ComputeDotProduct(ReadOnlySpan<T> left, ReadOnlySpan<T> right);
    T ComputeL2Norm(ReadOnlySpan<T> source);
    ExtremaPair<T> ComputeExtrema(ReadOnlySpan<T> source);
    T ComputeMean(ReadOnlySpan<T> source);
    T ComputeVariance(ReadOnlySpan<T> source);
    T ComputeStdDev(ReadOnlySpan<T> source);
    T ComputeSumOfSquares(ReadOnlySpan<T> source);
    T ComputeProduct(ReadOnlySpan<T> source);
    T ComputeL1Norm(ReadOnlySpan<T> source);
    T ComputeLinfNorm(ReadOnlySpan<T> source);
}

public interface IVectorScanner<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    int FindFirstGreaterThan(ReadOnlySpan<T> source, T threshold);
    nuint CountGreaterThan(ReadOnlySpan<T> source, T threshold);
    ScanResult FilterGreaterThan(ReadOnlySpan<T> source, T threshold, Span<T> destination);
    int FindFirstLessThan(ReadOnlySpan<T> source, T threshold);
    int FindFirstEqual(ReadOnlySpan<T> source, T value);
    nuint CountLessThan(ReadOnlySpan<T> source, T threshold);
    nuint CountEqual(ReadOnlySpan<T> source, T value);
    ScanResult FilterLessThan(ReadOnlySpan<T> source, T threshold, Span<T> destination);
    ScanResult FilterEqual(ReadOnlySpan<T> source, T value, Span<T> destination);
    nuint ComputeArgMin(ReadOnlySpan<T> source);
    nuint ComputeArgMax(ReadOnlySpan<T> source);
}

public interface IVectorMaskOps<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    void Select(ReadOnlySpan<T> condition, ReadOnlySpan<T> trueValues, ReadOnlySpan<T> falseValues, Span<T> destination);
    void Select(ReadOnlySpan<T> condition, T trueValue, T falseValue, Span<T> destination);
    void CompareLessThan(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void CompareGreaterThan(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void CompareEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void CompareLessThanOrEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void CompareGreaterThanOrEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
    void CompareNotEqual(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination);
}

public interface IVectorFillOps<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    void Fill(Span<T> destination, T value);
    void FillLinear(Span<T> destination, T start, T step);
}

public interface IVectorGatherOps<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    void Gather(ReadOnlySpan<T> source, ReadOnlySpan<int> indices, Span<T> destination);
    void Scatter(ReadOnlySpan<T> source, ReadOnlySpan<int> indices, Span<T> destination);
}

public interface IVectorMathOps<T> where T : struct, IBinaryFloatingPointIeee754<T>
{
    void VectorExp(ReadOnlySpan<T> source, Span<T> destination);
    void VectorLog(ReadOnlySpan<T> source, Span<T> destination);
    void VectorPow(ReadOnlySpan<T> @base, ReadOnlySpan<T> exponent, Span<T> destination);
    void VectorSigmoid(ReadOnlySpan<T> source, Span<T> destination);
    void VectorTanh(ReadOnlySpan<T> source, Span<T> destination);
    void VectorReLU(ReadOnlySpan<T> source, Span<T> destination);
    void Softmax(ReadOnlySpan<T> source, Span<T> destination);
    void VectorRSqrt(ReadOnlySpan<T> source, Span<T> destination);
}

public interface IAlignedMemoryBlock<T> : IDisposable where T : unmanaged
{
    nuint Alignment { get; }
    BatchCapacity Capacity { get; }
    Span<T> Span { get; }
    ReadOnlySpan<T> ReadOnlySpan { get; }
    unsafe T* RawPointer { get; }
}

public interface IContentionStrategy
{
    static abstract void Backoff(uint iteration);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct BatchCapacity
{
    public const int MaximumCapacity = 0x3FFFFFFF;
    public int Value { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public BatchCapacity(int value)
    {
        if (value <= 0 || value > MaximumCapacity)
        {
            ThrowHelper.ThrowInvalidCapacity(value);
        }
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(BatchCapacity capacity) => capacity.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(BatchCapacity capacity) => (nuint)capacity.Value;
}

[StructLayout(LayoutKind.Sequential, Pack = sizeof(nuint))]
public readonly record struct MemoryAlignment
{
    public nuint Value { get; }

    public static readonly MemoryAlignment CacheLine64 = new(64);
    public static readonly MemoryAlignment CacheLine128 = new(128);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public MemoryAlignment(nuint value)
    {
        if (value < (nuint)sizeof(nuint) || (value & (value - 1)) != 0)
        {
            ThrowHelper.ThrowInvalidAlignment(value);
        }
        Value = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator nuint(MemoryAlignment alignment) => alignment.Value;
}

[StructLayout(LayoutKind.Sequential)]
public readonly record struct ExtremaPair<T>(T Min, T Max) where T : struct;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ScanResult
{
    public OperationStatus Status { get; }
    public nuint WrittenCount { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ScanResult(OperationStatus status, nuint writtenCount)
    {
        Status = status;
        WrittenCount = writtenCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScanResult Success(nuint count) => new(OperationStatus.Done, count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ScanResult Overflow(nuint count) => new(OperationStatus.DestinationTooSmall, count);
}

public readonly struct AdaptiveBackoff : IContentionStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Backoff(uint iteration)
    {
        if (iteration < 4)
        {
            Thread.SpinWait(1 << (int)iteration);
        }
        else if (iteration < 16)
        {
            Thread.SpinWait(1 << 5);
        }
        else if (iteration < 32)
        {
            Thread.Yield();
        }
        else
        {
            Thread.Sleep(0);
        }
    }
}

public readonly struct PinnedCoreSpinBackoff : IContentionStrategy
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Backoff(uint iteration)
    {
        Thread.SpinWait(Math.Min((int)iteration + 1, 32));
    }
}

public sealed unsafe class AlignedCounter : IDisposable
{
    private long* _value;
    private int _disposed;

    public long Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref *_value);
    }

    public AlignedCounter(long initialValue = 0)
    {
        _value = (long*)NativeMemory.AlignedAlloc(128, 128);
        NativeMemory.Clear(_value, 128);
        *_value = initialValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Increment() => Interlocked.Increment(ref *_value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Add(long value) => Interlocked.Add(ref *_value, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CompareExchange(long expected, long next) => Interlocked.CompareExchange(ref *_value, next, expected) == expected;

    ~AlignedCounter()
    {
        Release();
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            long* ptr = Interlocked.Exchange(ref _value, null);
            if (ptr != null)
            {
                NativeMemory.AlignedFree(ptr);
            }
        }
    }
}

public sealed unsafe class AlignedMemoryBlock<T> : IAlignedMemoryBlock<T> where T : unmanaged
{
    private void* _pointer;
    private readonly BatchCapacity _capacity;
    private readonly MemoryAlignment _alignment;
    private int _disposed;

    public nuint Alignment => _alignment.Value;
    public BatchCapacity Capacity => _capacity;

    public unsafe T* RawPointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            void* ptr = Volatile.Read(ref _pointer);
            if (ptr == null) ThrowHelper.ThrowObjectDisposed();
            return (T*)ptr;
        }
    }

    public Span<T> Span
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            void* ptr = Volatile.Read(ref _pointer);
            if (ptr == null) ThrowHelper.ThrowObjectDisposed();
            return new Span<T>(ptr, _capacity.Value);
        }
    }

    public ReadOnlySpan<T> ReadOnlySpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Span;
    }

    public AlignedMemoryBlock(BatchCapacity capacity, MemoryAlignment alignment)
    {
        _capacity = capacity;
        _alignment = alignment;
        nuint byteCount = (nuint)capacity.Value * (nuint)sizeof(T);
        _pointer = NativeMemory.AlignedAlloc(byteCount, alignment.Value);
        NativeMemory.Clear(_pointer, byteCount);
    }

    ~AlignedMemoryBlock()
    {
        Release();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            void* ptr = Interlocked.Exchange(ref _pointer, null);
            if (ptr != null)
            {
                NativeMemory.AlignedFree(ptr);
            }
        }
    }
}

public sealed unsafe class VyukovBoundedBatchQueue<T, TBackoff> : IDisposable
    where T : unmanaged
    where TBackoff : struct, IContentionStrategy
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Cell
    {
        public nuint Sequence;
        public T Element;
    }

    private readonly Cell* _cells;
    private readonly nuint _bufferMask;
    private readonly nuint* _enqueuePosition;
    private readonly nuint* _dequeuePosition;
    private int _disposed;

    public nuint Capacity => _bufferMask + 1;

    public VyukovBoundedBatchQueue(BatchCapacity capacity)
    {
        nuint cap = BitOperations.RoundUpToPowerOf2((uint)capacity.Value);
        _bufferMask = cap - 1;

        nuint totalBytes = cap * (nuint)sizeof(Cell);
        _cells = (Cell*)NativeMemory.AlignedAlloc(totalBytes, 64);

        for (nuint i = 0; i < cap; ++i)
        {
            _cells[i].Sequence = i;
            _cells[i].Element = default;
        }

        _enqueuePosition = (nuint*)NativeMemory.AlignedAlloc(128, 128);
        _dequeuePosition = (nuint*)NativeMemory.AlignedAlloc(128, 128);
        NativeMemory.Clear(_enqueuePosition, 128);
        NativeMemory.Clear(_dequeuePosition, 128);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryEnqueue(in T item)
    {
        Cell* cell;
        nuint pos = Volatile.Read(ref *_enqueuePosition);
        uint spin = 0;

        while (true)
        {
            cell = &_cells[pos & _bufferMask];
            nuint seq = Volatile.Read(ref cell->Sequence);
            nint diff = (nint)seq - (nint)pos;

            if (diff == 0)
            {
                if (Interlocked.CompareExchange(ref *_enqueuePosition, pos + 1, pos) == pos)
                {
                    break;
                }
            }
            else if (diff < 0)
            {
                return false;
            }
            else
            {
                pos = Volatile.Read(ref *_enqueuePosition);
            }

            TBackoff.Backoff(++spin);
        }

        cell->Element = item;
        Volatile.Write(ref cell->Sequence, pos + 1);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryDequeue(out T item)
    {
        Cell* cell;
        nuint pos = Volatile.Read(ref *_dequeuePosition);
        uint spin = 0;

        while (true)
        {
            cell = &_cells[pos & _bufferMask];
            nuint seq = Volatile.Read(ref cell->Sequence);
            nint diff = (nint)seq - (nint)(pos + 1);

            if (diff == 0)
            {
                if (Interlocked.CompareExchange(ref *_dequeuePosition, pos + 1, pos) == pos)
                {
                    break;
                }
            }
            else if (diff < 0)
            {
                item = default;
                return false;
            }
            else
            {
                pos = Volatile.Read(ref *_dequeuePosition);
            }

            TBackoff.Backoff(++spin);
        }

        item = cell->Element;
        Volatile.Write(ref cell->Sequence, pos + _bufferMask + 1);
        return true;
    }

    ~VyukovBoundedBatchQueue()
    {
        Release();
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            if (_cells != null) NativeMemory.AlignedFree(_cells);
            if (_enqueuePosition != null) NativeMemory.AlignedFree(_enqueuePosition);
            if (_dequeuePosition != null) NativeMemory.AlignedFree(_dequeuePosition);
        }
    }
}

public sealed unsafe class SpscVectorStreamRingBuffer<T> : IDisposable where T : unmanaged
{
    private readonly AlignedMemoryBlock<T> _storage;
    private readonly int _capacity;
    private readonly int _mask;
    private readonly long* _head;
    private readonly long* _tail;
    private int _disposed;

    public int Capacity => _capacity;

    public SpscVectorStreamRingBuffer(BatchCapacity capacity)
    {
        int cap = (int)BitOperations.RoundUpToPowerOf2((uint)capacity.Value);
        _capacity = cap;
        _mask = cap - 1;
        _storage = new AlignedMemoryBlock<T>(new BatchCapacity(cap), MemoryAlignment.CacheLine64);

        _head = (long*)NativeMemory.AlignedAlloc(128, 128);
        _tail = (long*)NativeMemory.AlignedAlloc(128, 128);
        NativeMemory.Clear(_head, 128);
        NativeMemory.Clear(_tail, 128);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryWrite(ReadOnlySpan<T> source)
    {
        int count = source.Length;
        if (count > _capacity) return false;

        long currentTail = Volatile.Read(ref *_tail);
        long currentHead = Volatile.Read(ref *_head);

        if ((currentTail - currentHead) + count > _capacity)
        {
            return false;
        }

        int offset = (int)(currentTail & _mask);
        int contiguous = _capacity - offset;
        Span<T> buffer = _storage.Span;

        if (count <= contiguous)
        {
            source.CopyTo(buffer.Slice(offset, count));
        }
        else
        {
            source[..contiguous].CopyTo(buffer[offset..]);
            source[contiguous..].CopyTo(buffer[..(count - contiguous)]);
        }

        Volatile.Write(ref *_tail, currentTail + count);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryRead(Span<T> destination, out int elementsRead)
    {
        long currentHead = Volatile.Read(ref *_head);
        long currentTail = Volatile.Read(ref *_tail);
        long available = currentTail - currentHead;

        if (available <= 0)
        {
            elementsRead = 0;
            return false;
        }

        int toRead = (int)Math.Min((long)destination.Length, available);
        int offset = (int)(currentHead & _mask);
        int contiguous = _capacity - offset;
        ReadOnlySpan<T> buffer = _storage.ReadOnlySpan;

        if (toRead <= contiguous)
        {
            buffer.Slice(offset, toRead).CopyTo(destination);
        }
        else
        {
            buffer.Slice(offset, contiguous).CopyTo(destination[..contiguous]);
            buffer[..(toRead - contiguous)].CopyTo(destination[contiguous..]);
        }

        Volatile.Write(ref *_head, currentHead + toRead);
        elementsRead = toRead;
        return true;
    }

    ~SpscVectorStreamRingBuffer()
    {
        Release();
    }

    public void Dispose()
    {
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _storage.Dispose();
            if (_head != null) NativeMemory.AlignedFree(_head);
            if (_tail != null) NativeMemory.AlignedFree(_tail);
        }
    }
}

public sealed class SpscAsyncBatchSignal : IValueTaskSource<int>
{
    private ManualResetValueTaskSourceCore<int> _source;
    private int _gate;

    public SpscAsyncBatchSignal()
    {
        _source.RunContinuationsAsynchronously = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<int> WaitAsync()
    {
        if (Interlocked.CompareExchange(ref _gate, 1, 0) != 0)
        {
            ThrowHelper.ThrowConcurrentWaitNotSupported();
        }
        return new ValueTask<int>(this, _source.Version);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Signal(int itemsTransferred)
    {
        _source.SetResult(itemsTransferred);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Volatile.Write(ref _gate, 0);
        _source.Reset();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetResult(short token) => _source.GetResult(token);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => _source.OnCompleted(continuation, state, token, flags);
}

[SkipLocalsInit]
public static unsafe class SimdFloat32
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void TransformLinear(ReadOnlySpan<float> source, float multiplier, float offset, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<float> vMul = Vector512.Create(multiplier);
            Vector512<float> vOff = Vector512.Create(offset);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = (Vector512.LoadUnsafe(in src, i) * vMul) + vOff;
                Vector512<float> r1 = (Vector512.LoadUnsafe(in src, i + step) * vMul) + vOff;
                r0.StoreUnsafe(ref dst, i);
                r1.StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector512.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<float> vMul = Vector256.Create(multiplier);
            Vector256<float> vOff = Vector256.Create(offset);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = (Vector256.LoadUnsafe(in src, i) * vMul) + vOff;
                Vector256<float> r1 = (Vector256.LoadUnsafe(in src, i + step) * vMul) + vOff;
                r0.StoreUnsafe(ref dst, i);
                r1.StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector256.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<float> vMul = Vector128.Create(multiplier);
            Vector128<float> vOff = Vector128.Create(offset);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = (Vector128.LoadUnsafe(in src, i) * vMul) + vOff;
                Vector128<float> r1 = (Vector128.LoadUnsafe(in src, i + step) * vMul) + vOff;
                r0.StoreUnsafe(ref dst, i);
                r1.StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector128.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dst, (nint)(i + 0)) = (Unsafe.Add(ref src, (nint)(i + 0)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 1)) = (Unsafe.Add(ref src, (nint)(i + 1)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 2)) = (Unsafe.Add(ref src, (nint)(i + 2)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 3)) = (Unsafe.Add(ref src, (nint)(i + 3)) * multiplier) + offset;
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dst, (nint)i) = (Unsafe.Add(ref src, (nint)i) * multiplier) + offset;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAdd(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = Vector512.LoadUnsafe(in lRef, i) + Vector512.LoadUnsafe(in rRef, i);
                Vector512<float> r1 = Vector512.LoadUnsafe(in lRef, i + step) + Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector512.LoadUnsafe(in lRef, i) + Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = Vector256.LoadUnsafe(in lRef, i) + Vector256.LoadUnsafe(in rRef, i);
                Vector256<float> r1 = Vector256.LoadUnsafe(in lRef, i + step) + Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector256.LoadUnsafe(in lRef, i) + Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = Vector128.LoadUnsafe(in lRef, i) + Vector128.LoadUnsafe(in rRef, i);
                Vector128<float> r1 = Vector128.LoadUnsafe(in lRef, i + step) + Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector128.LoadUnsafe(in lRef, i) + Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) + Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) + Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) + Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) + Unsafe.Add(ref rRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) + Unsafe.Add(ref rRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorMultiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
                Vector512<float> r1 = Vector512.LoadUnsafe(in lRef, i + step) * Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
                Vector256<float> r1 = Vector256.LoadUnsafe(in lRef, i + step) * Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
                Vector128<float> r1 = Vector128.LoadUnsafe(in lRef, i + step) * Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) * Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) * Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) * Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) * Unsafe.Add(ref rRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) * Unsafe.Add(ref rRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFma(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> destination)
    {
        if (a.Length != b.Length || a.Length != c.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;

        ref float aRef = ref MemoryMarshal.GetReference(a);
        ref float bRef = ref MemoryMarshal.GetReference(b);
        ref float cRef = ref MemoryMarshal.GetReference(c);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = (Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) + Vector512.LoadUnsafe(in cRef, i);
                Vector512<float> r1 = (Vector512.LoadUnsafe(in aRef, i + step) * Vector512.LoadUnsafe(in bRef, i + step)) + Vector512.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) + Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = (Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) + Vector256.LoadUnsafe(in cRef, i);
                Vector256<float> r1 = (Vector256.LoadUnsafe(in aRef, i + step) * Vector256.LoadUnsafe(in bRef, i + step)) + Vector256.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) + Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = (Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) + Vector128.LoadUnsafe(in cRef, i);
                Vector128<float> r1 = (Vector128.LoadUnsafe(in aRef, i + step) * Vector128.LoadUnsafe(in bRef, i + step)) + Vector128.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) + Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = (Unsafe.Add(ref aRef, (nint)(i + 0)) * Unsafe.Add(ref bRef, (nint)(i + 0))) + Unsafe.Add(ref cRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = (Unsafe.Add(ref aRef, (nint)(i + 1)) * Unsafe.Add(ref bRef, (nint)(i + 1))) + Unsafe.Add(ref cRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = (Unsafe.Add(ref aRef, (nint)(i + 2)) * Unsafe.Add(ref bRef, (nint)(i + 2))) + Unsafe.Add(ref cRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = (Unsafe.Add(ref aRef, (nint)(i + 3)) * Unsafe.Add(ref bRef, (nint)(i + 3))) + Unsafe.Add(ref cRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = (Unsafe.Add(ref aRef, (nint)i) * Unsafe.Add(ref bRef, (nint)i)) + Unsafe.Add(ref cRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorClamp(ReadOnlySpan<float> source, float min, float max, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref float sRef = ref MemoryMarshal.GetReference(source);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vMin = Vector512.Create(min);
            Vector512<float> vMax = Vector512.Create(max);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<float> val = Vector512.LoadUnsafe(in sRef, i);
                Vector512.Min(Vector512.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vMin = Vector256.Create(min);
            Vector256<float> vMax = Vector256.Create(max);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<float> val = Vector256.LoadUnsafe(in sRef, i);
                Vector256.Min(Vector256.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vMin = Vector128.Create(min);
            Vector128<float> vMax = Vector128.Create(max);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<float> val = Vector128.LoadUnsafe(in sRef, i);
                Vector128.Min(Vector128.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Math.Clamp(Unsafe.Add(ref sRef, (nint)i), min, max);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeSum(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 4)
        {
            Vector512<float> a0 = Vector512<float>.Zero;
            Vector512<float> a1 = Vector512<float>.Zero;
            Vector512<float> a2 = Vector512<float>.Zero;
            Vector512<float> a3 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector512.LoadUnsafe(in src, i);
                a1 += Vector512.LoadUnsafe(in src, i + step);
                a2 += Vector512.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector512.LoadUnsafe(in src, i + (step * 3));
            }

            Vector512<float> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector512.LoadUnsafe(in src, i);
            }

            return Vector512.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 4)
        {
            Vector256<float> a0 = Vector256<float>.Zero;
            Vector256<float> a1 = Vector256<float>.Zero;
            Vector256<float> a2 = Vector256<float>.Zero;
            Vector256<float> a3 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector256.LoadUnsafe(in src, i);
                a1 += Vector256.LoadUnsafe(in src, i + step);
                a2 += Vector256.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector256.LoadUnsafe(in src, i + (step * 3));
            }

            Vector256<float> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector256.LoadUnsafe(in src, i);
            }

            return Vector256.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 4)
        {
            Vector128<float> a0 = Vector128<float>.Zero;
            Vector128<float> a1 = Vector128<float>.Zero;
            Vector128<float> a2 = Vector128<float>.Zero;
            Vector128<float> a3 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector128.LoadUnsafe(in src, i);
                a1 += Vector128.LoadUnsafe(in src, i + step);
                a2 += Vector128.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector128.LoadUnsafe(in src, i + (step * 3));
            }

            Vector128<float> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector128.LoadUnsafe(in src, i);
            }

            return Vector128.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        return ScalarTailSum(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ScalarTailSum(ref float src, nuint start, nuint length)
    {
        float s0 = 0.0f, s1 = 0.0f, s2 = 0.0f, s3 = 0.0f;
        nuint i = start;
        nuint limit = length >= 4 ? length - 3 : 0;

        for (; i < limit; i += 4)
        {
            s0 += Unsafe.Add(ref src, (nint)(i + 0));
            s1 += Unsafe.Add(ref src, (nint)(i + 1));
            s2 += Unsafe.Add(ref src, (nint)(i + 2));
            s3 += Unsafe.Add(ref src, (nint)(i + 3));
        }

        float acc = (s0 + s1) + (s2 + s3);
        for (; i < length; ++i)
        {
            acc += Unsafe.Add(ref src, (nint)i);
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeDotProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return 0.0f;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<float> a0 = Vector512<float>.Zero;
            Vector512<float> a1 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
                a1 += Vector512.LoadUnsafe(in lRef, i + step) * Vector512.LoadUnsafe(in rRef, i + step);
            }

            Vector512<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
            }

            return Vector512.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<float> a0 = Vector256<float>.Zero;
            Vector256<float> a1 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
                a1 += Vector256.LoadUnsafe(in lRef, i + step) * Vector256.LoadUnsafe(in rRef, i + step);
            }

            Vector256<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
            }

            return Vector256.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<float> a0 = Vector128<float>.Zero;
            Vector128<float> a1 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
                a1 += Vector128.LoadUnsafe(in lRef, i + step) * Vector128.LoadUnsafe(in rRef, i + step);
            }

            Vector128<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
            }

            return Vector128.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        return ScalarTailDot(ref lRef, ref rRef, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ScalarTailDot(ref float lRef, ref float rRef, nuint start, nuint length)
    {
        float d0 = 0.0f, d1 = 0.0f;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;

        for (; i < limit; i += 2)
        {
            d0 += Unsafe.Add(ref lRef, (nint)(i + 0)) * Unsafe.Add(ref rRef, (nint)(i + 0));
            d1 += Unsafe.Add(ref lRef, (nint)(i + 1)) * Unsafe.Add(ref rRef, (nint)(i + 1));
        }

        float acc = d0 + d1;
        for (; i < length; ++i)
        {
            acc += Unsafe.Add(ref lRef, (nint)i) * Unsafe.Add(ref rRef, (nint)i);
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeL2Norm(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<float> a0 = Vector512<float>.Zero;
            Vector512<float> a1 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<float> v1 = Vector512.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector512<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<float> v = Vector512.LoadUnsafe(in src, i);
                acc += v * v;
            }

            float sumOfSquares = Vector512.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return MathF.Sqrt(sumOfSquares);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<float> a0 = Vector256<float>.Zero;
            Vector256<float> a1 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<float> v1 = Vector256.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector256<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<float> v = Vector256.LoadUnsafe(in src, i);
                acc += v * v;
            }

            float sumOfSquares = Vector256.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return MathF.Sqrt(sumOfSquares);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<float> a0 = Vector128<float>.Zero;
            Vector128<float> a1 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<float> v1 = Vector128.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector128<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128<float> v = Vector128.LoadUnsafe(in src, i);
                acc += v * v;
            }

            float sumOfSquares = Vector128.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return MathF.Sqrt(sumOfSquares);
        }

        return MathF.Sqrt(ScalarTailSquareSum(ref src, 0, length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ScalarTailSquareSum(ref float src, nuint start, nuint length)
    {
        float s0 = 0.0f, s1 = 0.0f;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;

        for (; i < limit; i += 2)
        {
            float v0 = Unsafe.Add(ref src, (nint)(i + 0));
            float v1 = Unsafe.Add(ref src, (nint)(i + 1));
            s0 += v0 * v0;
            s1 += v1 * v1;
        }

        float acc = s0 + s1;
        for (; i < length; ++i)
        {
            float v = Unsafe.Add(ref src, (nint)i);
            acc += v * v;
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ExtremaPair<float> ComputeExtrema(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count;
            Vector512<float> vMin0 = Vector512.LoadUnsafe(in src, 0);
            Vector512<float> vMax0 = vMin0;
            Vector512<float> vMin1 = Vector512.LoadUnsafe(in src, step);
            Vector512<float> vMax1 = vMin1;
            nuint limit = length - (step * 2) + 1;

            for (i = step * 2; i < limit; i += step * 2)
            {
                Vector512<float> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<float> v1 = Vector512.LoadUnsafe(in src, i + step);
                vMin0 = Vector512.Min(vMin0, v0);
                vMax0 = Vector512.Max(vMax0, v0);
                vMin1 = Vector512.Min(vMin1, v1);
                vMax1 = Vector512.Max(vMax1, v1);
            }

            Vector512<float> vMin = Vector512.Min(vMin0, vMin1);
            Vector512<float> vMax = Vector512.Max(vMax0, vMax1);

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<float> val = Vector512.LoadUnsafe(in src, i);
                vMin = Vector512.Min(vMin, val);
                vMax = Vector512.Max(vMax, val);
            }

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int lane = 0; lane < Vector512<float>.Count; ++lane)
            {
                min = float.Min(min, vMin.GetElement(lane));
                max = float.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                float v = Unsafe.Add(ref src, (nint)i);
                min = float.Min(min, v);
                max = float.Max(max, v);
            }
            return new ExtremaPair<float>(min, max);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count;
            Vector256<float> vMin0 = Vector256.LoadUnsafe(in src, 0);
            Vector256<float> vMax0 = vMin0;
            Vector256<float> vMin1 = Vector256.LoadUnsafe(in src, step);
            Vector256<float> vMax1 = vMin1;
            nuint limit = length - (step * 2) + 1;

            for (i = step * 2; i < limit; i += step * 2)
            {
                Vector256<float> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<float> v1 = Vector256.LoadUnsafe(in src, i + step);
                vMin0 = Vector256.Min(vMin0, v0);
                vMax0 = Vector256.Max(vMax0, v0);
                vMin1 = Vector256.Min(vMin1, v1);
                vMax1 = Vector256.Max(vMax1, v1);
            }

            Vector256<float> vMin = Vector256.Min(vMin0, vMin1);
            Vector256<float> vMax = Vector256.Max(vMax0, vMax1);

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<float> val = Vector256.LoadUnsafe(in src, i);
                vMin = Vector256.Min(vMin, val);
                vMax = Vector256.Max(vMax, val);
            }

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int lane = 0; lane < Vector256<float>.Count; ++lane)
            {
                min = float.Min(min, vMin.GetElement(lane));
                max = float.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                float v = Unsafe.Add(ref src, (nint)i);
                min = float.Min(min, v);
                max = float.Max(max, v);
            }
            return new ExtremaPair<float>(min, max);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vMin = Vector128.LoadUnsafe(in src, 0);
            Vector128<float> vMax = vMin;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (i = step; i < limit; i += step)
            {
                Vector128<float> val = Vector128.LoadUnsafe(in src, i);
                vMin = Vector128.Min(vMin, val);
                vMax = Vector128.Max(vMax, val);
            }

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int lane = 0; lane < Vector128<float>.Count; ++lane)
            {
                min = float.Min(min, vMin.GetElement(lane));
                max = float.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                float v = Unsafe.Add(ref src, (nint)i);
                min = float.Min(min, v);
                max = float.Max(max, v);
            }
            return new ExtremaPair<float>(min, max);
        }

        float sMin = Unsafe.Add(ref src, 0);
        float sMax = sMin;
        for (i = 1; i < length; ++i)
        {
            float v = Unsafe.Add(ref src, (nint)i);
            sMin = float.Min(sMin, v);
            sMax = float.Max(sMax, v);
        }
        return new ExtremaPair<float>(sMin, sMax);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstGreaterThan(ReadOnlySpan<float> source, float threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<float> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                if (bits != 0)
                {
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<float> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                if (bits != 0)
                {
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<float> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                if (bits != 0)
                {
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
                }
            }
        }

        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) > threshold)
            {
                return (int)i;
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountGreaterThan(ReadOnlySpan<float> source, float threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint count = 0;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<float> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector512.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<float> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector256.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<float> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector128.ExtractMostSignificantBits(mask));
            }
        }

        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) > threshold)
            {
                count++;
            }
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterGreaterThan(ReadOnlySpan<float> source, float threshold, Span<float> destination)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return ScanResult.Success(0);

        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint dstCap = (nuint)destination.Length;
        nuint written = 0;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector512<float> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector512<float> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector256<float> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector256<float> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector128<float> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector128<float> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }

        for (; i < length; ++i)
        {
            float val = Unsafe.Add(ref src, (nint)i);
            if (val > threshold)
            {
                if (written >= dstCap) return ScanResult.Overflow(written);
                Unsafe.Add(ref dst, (nint)written++) = val;
            }
        }

        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSubtract(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i);
                Vector512<float> r1 = Vector512.LoadUnsafe(in lRef, i + step) - Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i);
                Vector256<float> r1 = Vector256.LoadUnsafe(in lRef, i + step) - Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = Vector128.LoadUnsafe(in lRef, i) - Vector128.LoadUnsafe(in rRef, i);
                Vector128<float> r1 = Vector128.LoadUnsafe(in lRef, i + step) - Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector128.LoadUnsafe(in lRef, i) - Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) - Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) - Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) - Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) - Unsafe.Add(ref rRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) - Unsafe.Add(ref rRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorNegate(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                (Vector512<float>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector512<float>.Zero - Vector512.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector512<float>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                (Vector256<float>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector256<float>.Zero - Vector256.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector256<float>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                (Vector128<float>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector128<float>.Zero - Vector128.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector128<float>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dst, (nint)(i + 0)) = -Unsafe.Add(ref src, (nint)(i + 0));
            Unsafe.Add(ref dst, (nint)(i + 1)) = -Unsafe.Add(ref src, (nint)(i + 1));
            Unsafe.Add(ref dst, (nint)(i + 2)) = -Unsafe.Add(ref src, (nint)(i + 2));
            Unsafe.Add(ref dst, (nint)(i + 3)) = -Unsafe.Add(ref src, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dst, (nint)i) = -Unsafe.Add(ref src, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorDivide(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = Vector512.LoadUnsafe(in lRef, i) / Vector512.LoadUnsafe(in rRef, i);
                Vector512<float> r1 = Vector512.LoadUnsafe(in lRef, i + step) / Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector512.LoadUnsafe(in lRef, i) / Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = Vector256.LoadUnsafe(in lRef, i) / Vector256.LoadUnsafe(in rRef, i);
                Vector256<float> r1 = Vector256.LoadUnsafe(in lRef, i + step) / Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector256.LoadUnsafe(in lRef, i) / Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = Vector128.LoadUnsafe(in lRef, i) / Vector128.LoadUnsafe(in rRef, i);
                Vector128<float> r1 = Vector128.LoadUnsafe(in lRef, i + step) / Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector128.LoadUnsafe(in lRef, i) / Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) / Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) / Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) / Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) / Unsafe.Add(ref rRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) / Unsafe.Add(ref rRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAbs(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<uint> absMask = Vector512.Create(0x7FFFFFFFu);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                (Vector512.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle().StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<uint> absMask = Vector256.Create(0x7FFFFFFFu);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                (Vector256.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle().StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<uint> absMask = Vector128.Create(0x7FFFFFFFu);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                (Vector128.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle().StoreUnsafe(ref dst, i);
            }
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dst, (nint)i) = MathF.Abs(Unsafe.Add(ref src, (nint)i));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFms(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> destination)
    {
        if (a.Length != b.Length || a.Length != c.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;

        ref float aRef = ref MemoryMarshal.GetReference(a);
        ref float bRef = ref MemoryMarshal.GetReference(b);
        ref float cRef = ref MemoryMarshal.GetReference(c);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> r0 = (Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) - Vector512.LoadUnsafe(in cRef, i);
                Vector512<float> r1 = (Vector512.LoadUnsafe(in aRef, i + step) * Vector512.LoadUnsafe(in bRef, i + step)) - Vector512.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) - Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> r0 = (Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) - Vector256.LoadUnsafe(in cRef, i);
                Vector256<float> r1 = (Vector256.LoadUnsafe(in aRef, i + step) * Vector256.LoadUnsafe(in bRef, i + step)) - Vector256.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) - Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> r0 = (Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) - Vector128.LoadUnsafe(in cRef, i);
                Vector128<float> r1 = (Vector128.LoadUnsafe(in aRef, i + step) * Vector128.LoadUnsafe(in bRef, i + step)) - Vector128.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) - Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = (Unsafe.Add(ref aRef, (nint)(i + 0)) * Unsafe.Add(ref bRef, (nint)(i + 0))) - Unsafe.Add(ref cRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = (Unsafe.Add(ref aRef, (nint)(i + 1)) * Unsafe.Add(ref bRef, (nint)(i + 1))) - Unsafe.Add(ref cRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = (Unsafe.Add(ref aRef, (nint)(i + 2)) * Unsafe.Add(ref bRef, (nint)(i + 2))) - Unsafe.Add(ref cRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = (Unsafe.Add(ref aRef, (nint)(i + 3)) * Unsafe.Add(ref bRef, (nint)(i + 3))) - Unsafe.Add(ref cRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = (Unsafe.Add(ref aRef, (nint)i) * Unsafe.Add(ref bRef, (nint)i)) - Unsafe.Add(ref cRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLerp(ReadOnlySpan<float> a, ReadOnlySpan<float> b, float t, Span<float> destination)
    {
        if (a.Length != b.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;

        ref float aRef = ref MemoryMarshal.GetReference(a);
        ref float bRef = ref MemoryMarshal.GetReference(b);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vT = Vector512.Create(t);
            Vector512<float> oneMinusT = Vector512.Create(1.0f - t);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                ((Vector512.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector512.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vT = Vector256.Create(t);
            Vector256<float> oneMinusT = Vector256.Create(1.0f - t);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                ((Vector256.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector256.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vT = Vector128.Create(t);
            Vector128<float> oneMinusT = Vector128.Create(1.0f - t);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                ((Vector128.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector128.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
            }
        }

        for (; i < length; ++i)
        {
            float aVal = Unsafe.Add(ref aRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = (aVal * (1.0f - t)) + (Unsafe.Add(ref bRef, (nint)i) * t);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMax(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i + step), Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i + step), Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i + step), Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = float.Max(Unsafe.Add(ref lRef, (nint)i), Unsafe.Add(ref rRef, (nint)i));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMin(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i + step), Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i + step), Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i + step), Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = float.Min(Unsafe.Add(ref lRef, (nint)i), Unsafe.Add(ref rRef, (nint)i));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeMean(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        float sum = ComputeSum(source);
        return sum / (float)length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeVariance(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;
        if (length == 1) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<float> s0 = Vector512<float>.Zero, s1 = Vector512<float>.Zero;
            Vector512<float> q0 = Vector512<float>.Zero, q1 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<float> v1 = Vector512.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1;
                q0 += v0 * v0; q1 += v1 * v1;
            }

            Vector512<float> accS = s0 + s1;
            Vector512<float> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<float> v = Vector512.LoadUnsafe(in src, i);
                accS += v;
                accQ += v * v;
            }

            float sum = Vector512.Sum(accS) + ScalarTailSum(ref src, i, length);
            float sumSq = Vector512.Sum(accQ) + ScalarTailSquareSum(ref src, i, length);
            float n = (float)length;
            float mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<float> s0 = Vector256<float>.Zero, s1 = Vector256<float>.Zero;
            Vector256<float> q0 = Vector256<float>.Zero, q1 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<float> v1 = Vector256.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1;
                q0 += v0 * v0; q1 += v1 * v1;
            }

            Vector256<float> accS = s0 + s1;
            Vector256<float> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<float> v = Vector256.LoadUnsafe(in src, i);
                accS += v;
                accQ += v * v;
            }

            float sum = Vector256.Sum(accS) + ScalarTailSum(ref src, i, length);
            float sumSq = Vector256.Sum(accQ) + ScalarTailSquareSum(ref src, i, length);
            float n = (float)length;
            float mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<float> s0 = Vector128<float>.Zero, s1 = Vector128<float>.Zero;
            Vector128<float> q0 = Vector128<float>.Zero, q1 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<float> v1 = Vector128.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1;
                q0 += v0 * v0; q1 += v1 * v1;
            }

            Vector128<float> accS = s0 + s1;
            Vector128<float> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128<float> v = Vector128.LoadUnsafe(in src, i);
                accS += v;
                accQ += v * v;
            }

            float sum = Vector128.Sum(accS) + ScalarTailSum(ref src, i, length);
            float sumSq = Vector128.Sum(accQ) + ScalarTailSquareSum(ref src, i, length);
            float n = (float)length;
            float mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }

        float sSum = 0.0f, sSq = 0.0f;
        for (i = 0; i < length; ++i)
        {
            float v = Unsafe.Add(ref src, (nint)i);
            sSum += v;
            sSq += v * v;
        }
        float sn = (float)length;
        float sm = sSum / sn;
        return (sSq / sn) - (sm * sm);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeStdDev(ReadOnlySpan<float> source)
        => MathF.Sqrt(ComputeVariance(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeSumOfSquares(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<float> a0 = Vector512<float>.Zero, a1 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<float> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<float> v1 = Vector512.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector512<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<float> v = Vector512.LoadUnsafe(in src, i);
                acc += v * v;
            }

            return Vector512.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<float> a0 = Vector256<float>.Zero, a1 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<float> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<float> v1 = Vector256.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector256<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<float> v = Vector256.LoadUnsafe(in src, i);
                acc += v * v;
            }

            return Vector256.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<float> a0 = Vector128<float>.Zero, a1 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<float> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<float> v1 = Vector128.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector128<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128<float> v = Vector128.LoadUnsafe(in src, i);
                acc += v * v;
            }

            return Vector128.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
        }

        return ScalarTailSquareSum(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeProduct(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 1.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> acc = Vector512.Create(1.0f);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                acc *= Vector512.LoadUnsafe(in src, i);
            }

            float result = 1.0f;
            for (int lane = 0; lane < Vector512<float>.Count; ++lane)
                result *= acc.GetElement(lane);

            for (; i < length; ++i)
                result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> acc = Vector256.Create(1.0f);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                acc *= Vector256.LoadUnsafe(in src, i);
            }

            float result = 1.0f;
            for (int lane = 0; lane < Vector256<float>.Count; ++lane)
                result *= acc.GetElement(lane);

            for (; i < length; ++i)
                result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> acc = Vector128.Create(1.0f);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                acc *= Vector128.LoadUnsafe(in src, i);
            }

            float result = 1.0f;
            for (int lane = 0; lane < Vector128<float>.Count; ++lane)
                result *= acc.GetElement(lane);

            for (; i < length; ++i)
                result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }

        float product = 1.0f;
        for (; i < length; ++i)
            product *= Unsafe.Add(ref src, (nint)i);
        return product;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeL1Norm(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count * 2)
        {
            Vector512<uint> absMask = Vector512.Create(0x7FFFFFFFu);
            Vector512<float> a0 = Vector512<float>.Zero, a1 = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += (Vector512.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                a1 += (Vector512.LoadUnsafe(in src, i + step).AsUInt32() & absMask).AsSingle();
            }

            Vector512<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += (Vector512.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
            }

            return Vector512.Sum(acc) + ScalarTailAbsSum(ref src, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count * 2)
        {
            Vector256<uint> absMask = Vector256.Create(0x7FFFFFFFu);
            Vector256<float> a0 = Vector256<float>.Zero, a1 = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += (Vector256.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                a1 += (Vector256.LoadUnsafe(in src, i + step).AsUInt32() & absMask).AsSingle();
            }

            Vector256<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += (Vector256.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
            }

            return Vector256.Sum(acc) + ScalarTailAbsSum(ref src, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count * 2)
        {
            Vector128<uint> absMask = Vector128.Create(0x7FFFFFFFu);
            Vector128<float> a0 = Vector128<float>.Zero, a1 = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += (Vector128.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                a1 += (Vector128.LoadUnsafe(in src, i + step).AsUInt32() & absMask).AsSingle();
            }

            Vector128<float> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += (Vector128.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
            }

            return Vector128.Sum(acc) + ScalarTailAbsSum(ref src, i, length);
        }

        return ScalarTailAbsSum(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ComputeLinfNorm(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0f;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<uint> absMask = Vector512.Create(0x7FFFFFFFu);
            Vector512<float> vMax = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<float> abs = (Vector512.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                vMax = Vector512.Max(vMax, abs);
            }

            float result = 0.0f;
            for (int lane = 0; lane < Vector512<float>.Count; ++lane)
                result = float.Max(result, vMax.GetElement(lane));

            for (; i < length; ++i)
                result = float.Max(result, MathF.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<uint> absMask = Vector256.Create(0x7FFFFFFFu);
            Vector256<float> vMax = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<float> abs = (Vector256.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                vMax = Vector256.Max(vMax, abs);
            }

            float result = 0.0f;
            for (int lane = 0; lane < Vector256<float>.Count; ++lane)
                result = float.Max(result, vMax.GetElement(lane));

            for (; i < length; ++i)
                result = float.Max(result, MathF.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<uint> absMask = Vector128.Create(0x7FFFFFFFu);
            Vector128<float> vMax = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<float> abs = (Vector128.LoadUnsafe(in src, i).AsUInt32() & absMask).AsSingle();
                vMax = Vector128.Max(vMax, abs);
            }

            float result = 0.0f;
            for (int lane = 0; lane < Vector128<float>.Count; ++lane)
                result = float.Max(result, vMax.GetElement(lane));

            for (; i < length; ++i)
                result = float.Max(result, MathF.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }

        float sMax = 0.0f;
        for (; i < length; ++i)
            sMax = float.Max(sMax, MathF.Abs(Unsafe.Add(ref src, (nint)i)));
        return sMax;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static float ScalarTailAbsSum(ref float src, nuint start, nuint length)
    {
        float s0 = 0.0f, s1 = 0.0f;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;

        for (; i < limit; i += 2)
        {
            s0 += MathF.Abs(Unsafe.Add(ref src, (nint)(i + 0)));
            s1 += MathF.Abs(Unsafe.Add(ref src, (nint)(i + 1)));
        }

        float acc = s0 + s1;
        for (; i < length; ++i)
            acc += MathF.Abs(Unsafe.Add(ref src, (nint)i));
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstLessThan(ReadOnlySpan<float> source, float threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<float> mask = Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<float> mask = Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<float> mask = Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }

        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) < threshold)
                return (int)i;
        }
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstEqual(ReadOnlySpan<float> source, float value)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vVal = Vector512.Create(value);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<float> mask = Vector512.Equal(Vector512.LoadUnsafe(in src, i), vVal);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vVal = Vector256.Create(value);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<float> mask = Vector256.Equal(Vector256.LoadUnsafe(in src, i), vVal);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                if (bits != 0)
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vVal = Vector128.Create(value);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<float> mask = Vector128.Equal(Vector128.LoadUnsafe(in src, i), vVal);
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
    public static nuint CountLessThan(ReadOnlySpan<float> source, float threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint count = 0;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<float> mask = Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vThresh);
                count += (nuint)uint.PopCount((uint)Vector512.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<float> mask = Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vThresh);
                count += (nuint)uint.PopCount(Vector256.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<float> mask = Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vThresh);
                count += (nuint)uint.PopCount(Vector128.ExtractMostSignificantBits(mask));
            }
        }

        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) < threshold)
                ++count;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountEqual(ReadOnlySpan<float> source, float value)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;

        ref float src = ref MemoryMarshal.GetReference(source);
        nuint count = 0;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vVal = Vector512.Create(value);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<float> mask = Vector512.Equal(Vector512.LoadUnsafe(in src, i), vVal);
                count += (nuint)uint.PopCount((uint)Vector512.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vVal = Vector256.Create(value);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<float> mask = Vector256.Equal(Vector256.LoadUnsafe(in src, i), vVal);
                count += (nuint)uint.PopCount(Vector256.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vVal = Vector128.Create(value);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<float> mask = Vector128.Equal(Vector128.LoadUnsafe(in src, i), vVal);
                count += (nuint)uint.PopCount(Vector128.ExtractMostSignificantBits(mask));
            }
        }

        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) == value)
                ++count;
        }
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterLessThan(ReadOnlySpan<float> source, float threshold, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return ScanResult.Success(0);

        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint dstCap = (nuint)destination.Length;
        nuint written = 0;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector512<float> mask = Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector512<float> mask = Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector256<float> mask = Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector256<float> mask = Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector128<float> mask = Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector128<float> mask = Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }

        for (; i < length; ++i)
        {
            float val = Unsafe.Add(ref src, (nint)i);
            if (val < threshold)
            {
                if (written >= dstCap) return ScanResult.Overflow(written);
                Unsafe.Add(ref dst, (nint)written++) = val;
            }
        }

        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterEqual(ReadOnlySpan<float> source, float value, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return ScanResult.Success(0);

        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint dstCap = (nuint)destination.Length;
        nuint written = 0;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vVal = Vector512.Create(value);
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector512<float> mask = Vector512.Equal(Vector512.LoadUnsafe(in src, i), vVal);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector512<float> mask = Vector512.Equal(Vector512.LoadUnsafe(in src, i), vVal);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vVal = Vector256.Create(value);
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector256<float> mask = Vector256.Equal(Vector256.LoadUnsafe(in src, i), vVal);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector256<float> mask = Vector256.Equal(Vector256.LoadUnsafe(in src, i), vVal);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vVal = Vector128.Create(value);
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector128<float> mask = Vector128.Equal(Vector128.LoadUnsafe(in src, i), vVal);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector128<float> mask = Vector128.Equal(Vector128.LoadUnsafe(in src, i), vVal);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }

        for (; i < length; ++i)
        {
            float val = Unsafe.Add(ref src, (nint)i);
            if (val == value)
            {
                if (written >= dstCap) return ScanResult.Overflow(written);
                Unsafe.Add(ref dst, (nint)written++) = val;
            }
        }

        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMin(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();

        ref float src = ref MemoryMarshal.GetReference(source);

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;
            Vector512<float> vMin = Vector512.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMin = Vector512.Min(vMin, Vector512.LoadUnsafe(in src, i));

            float minVal = float.PositiveInfinity;
            for (int lane = 0; lane < Vector512<float>.Count; ++lane)
                minVal = float.Min(minVal, vMin.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == minVal) return i;
            }
            return 0;
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;
            Vector256<float> vMin = Vector256.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMin = Vector256.Min(vMin, Vector256.LoadUnsafe(in src, i));

            float minVal = float.PositiveInfinity;
            for (int lane = 0; lane < Vector256<float>.Count; ++lane)
                minVal = float.Min(minVal, vMin.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == minVal) return i;
            }
            return 0;
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;
            Vector128<float> vMin = Vector128.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMin = Vector128.Min(vMin, Vector128.LoadUnsafe(in src, i));

            float minVal = float.PositiveInfinity;
            for (int lane = 0; lane < Vector128<float>.Count; ++lane)
                minVal = float.Min(minVal, vMin.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == minVal) return i;
            }
            return 0;
        }

        float sMin = Unsafe.Add(ref src, 0);
        nuint idx = 0;
        for (nuint j = 1; j < length; ++j)
        {
            float v = Unsafe.Add(ref src, (nint)j);
            if (v < sMin) { sMin = v; idx = j; }
        }
        return idx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMax(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();

        ref float src = ref MemoryMarshal.GetReference(source);

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count;
            nuint limit = length - step + 1;
            Vector512<float> vMax = Vector512.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMax = Vector512.Max(vMax, Vector512.LoadUnsafe(in src, i));

            float maxVal = float.NegativeInfinity;
            for (int lane = 0; lane < Vector512<float>.Count; ++lane)
                maxVal = float.Max(maxVal, vMax.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == maxVal) return i;
            }
            return 0;
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count;
            nuint limit = length - step + 1;
            Vector256<float> vMax = Vector256.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMax = Vector256.Max(vMax, Vector256.LoadUnsafe(in src, i));

            float maxVal = float.NegativeInfinity;
            for (int lane = 0; lane < Vector256<float>.Count; ++lane)
                maxVal = float.Max(maxVal, vMax.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == maxVal) return i;
            }
            return 0;
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count;
            nuint limit = length - step + 1;
            Vector128<float> vMax = Vector128.LoadUnsafe(in src, 0);
            nuint i = step;

            for (; i < limit; i += step)
                vMax = Vector128.Max(vMax, Vector128.LoadUnsafe(in src, i));

            float maxVal = float.NegativeInfinity;
            for (int lane = 0; lane < Vector128<float>.Count; ++lane)
                maxVal = float.Max(maxVal, vMax.GetElement(lane));

            for (i = 0; i < length; ++i)
            {
                if (Unsafe.Add(ref src, (nint)i) == maxVal) return i;
            }
            return 0;
        }

        float sMax = Unsafe.Add(ref src, 0);
        nuint idx = 0;
        for (nuint j = 1; j < length; ++j)
        {
            float v = Unsafe.Add(ref src, (nint)j);
            if (v > sMax) { sMax = v; idx = j; }
        }
        return idx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<float> condition, ReadOnlySpan<float> trueValues, ReadOnlySpan<float> falseValues, Span<float> destination)
    {
        if (condition.Length != trueValues.Length || condition.Length != falseValues.Length || destination.Length < condition.Length)
            ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)condition.Length;
        if (length == 0) return;
        ref float cRef = ref MemoryMarshal.GetReference(condition);
        ref float tRef = ref MemoryMarshal.GetReference(trueValues);
        ref float fRef = ref MemoryMarshal.GetReference(falseValues);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.ConditionalSelect(Vector512.LoadUnsafe(in cRef, i), Vector512.LoadUnsafe(in tRef, i), Vector512.LoadUnsafe(in fRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.ConditionalSelect(Vector256.LoadUnsafe(in cRef, i), Vector256.LoadUnsafe(in tRef, i), Vector256.LoadUnsafe(in fRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.ConditionalSelect(Vector128.LoadUnsafe(in cRef, i), Vector128.LoadUnsafe(in tRef, i), Vector128.LoadUnsafe(in fRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
        {
            float c = Unsafe.Add(ref cRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = c != 0f ? Unsafe.Add(ref tRef, (nint)i) : Unsafe.Add(ref fRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<float> condition, float trueValue, float falseValue, Span<float> destination)
    {
        if (destination.Length < condition.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)condition.Length;
        if (length == 0) return;
        ref float cRef = ref MemoryMarshal.GetReference(condition);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vT = Vector512.Create(trueValue), vF = Vector512.Create(falseValue);
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.ConditionalSelect(Vector512.LoadUnsafe(in cRef, i), vT, vF).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vT = Vector256.Create(trueValue), vF = Vector256.Create(falseValue);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.ConditionalSelect(Vector256.LoadUnsafe(in cRef, i), vT, vF).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vT = Vector128.Create(trueValue), vF = Vector128.Create(falseValue);
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.ConditionalSelect(Vector128.LoadUnsafe(in cRef, i), vT, vF).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref cRef, (nint)i) != 0f ? trueValue : falseValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.LessThan(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.LessThan(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.LessThan(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) < Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int32BitsToSingle(-1) : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.GreaterThan(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.GreaterThan(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.GreaterThan(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) > Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int32BitsToSingle(-1) : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.Equal(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.Equal(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.Equal(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) == Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int32BitsToSingle(-1) : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareNotEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.NotEqual(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.NotEqual(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.NotEqual(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) != Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int32BitsToSingle(-1) : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThanOrEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.LessThanOrEqual(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.LessThanOrEqual(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.LessThanOrEqual(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) <= Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int32BitsToSingle(-1) : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThanOrEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref float lRef = ref MemoryMarshal.GetReference(left);
        ref float rRef = ref MemoryMarshal.GetReference(right);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.GreaterThanOrEqual(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.GreaterThanOrEqual(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.GreaterThanOrEqual(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsSingle().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) >= Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int32BitsToSingle(-1) : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Fill(Span<float> destination, float value)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> v = Vector512.Create(value);
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> v = Vector256.Create(value);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> v = Vector128.Create(value);
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void FillLinear(Span<float> destination, float start, float step)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vStart = Vector512.Create(start);
            Vector512<float> indices = Vector512.Create(0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f);
            Vector512<float> vStep = Vector512.Create(step);
            nuint vc = (nuint)Vector512<float>.Count;
            Vector512<float> vInc = Vector512.Create((float)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc)
            {
                (vStart + indices * vStep).StoreUnsafe(ref dRef, i);
                vStart += vInc;
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vStart = Vector256.Create(start);
            Vector256<float> indices = Vector256.Create(0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f);
            Vector256<float> vStep = Vector256.Create(step);
            nuint vc = (nuint)Vector256<float>.Count;
            Vector256<float> vInc = Vector256.Create((float)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc)
            {
                (vStart + indices * vStep).StoreUnsafe(ref dRef, i);
                vStart += vInc;
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vStart = Vector128.Create(start);
            Vector128<float> indices = Vector128.Create(0f, 1f, 2f, 3f);
            Vector128<float> vStep = Vector128.Create(step);
            nuint vc = (nuint)Vector128<float>.Count;
            Vector128<float> vInc = Vector128.Create((float)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc)
            {
                (vStart + indices * vStep).StoreUnsafe(ref dRef, i);
                vStart += vInc;
            }
        }
        float val = start + (float)i * step;
        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = val;
            val += step;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Gather(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
    {
        if (destination.Length < indices.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && count >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector512.Gather(in src, Vector512.LoadUnsafe(in idx, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && count >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector256.Gather(in src, Vector256.LoadUnsafe(in idx, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && count >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector128.Gather(in src, Vector128.LoadUnsafe(in idx, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < count; ++i)
            Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, Unsafe.Add(ref idx, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Scatter(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
    {
        if (source.Length > destination.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && count >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector512.LoadUnsafe(in src, i).Scatter(ref dst, Vector512.LoadUnsafe(in idx, i));
        }
        else if (Vector256.IsHardwareAccelerated && count >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector256.LoadUnsafe(in src, i).Scatter(ref dst, Vector256.LoadUnsafe(in idx, i));
        }
        else if (Vector128.IsHardwareAccelerated && count >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector128.LoadUnsafe(in src, i).Scatter(ref dst, Vector128.LoadUnsafe(in idx, i));
        }
        for (; i < count; ++i)
            Unsafe.Add(ref dst, Unsafe.Add(ref idx, (nint)i)) = Unsafe.Add(ref src, (nint)i);
    }

    private static Vector512<float> ExpKernel512(Vector512<float> x)
    {
        const float LOG2E = 1.4426950408889634f;
        Vector512<float> clamped = Vector512.Max(Vector512.Min(x, Vector512.Create(87f)), Vector512.Create(-87f));
        Vector512<float> t = clamped * Vector512.Create(LOG2E);
        Vector512<int> k = Vector512.Floor(t).AsInt32() - Vector512.Create(1);
        Vector512<float> f = t - Vector512.ConvertToSingle(k);
        Vector512<int> biased = k + Vector512.Create(127);
        Vector512<float> pow2 = Vector512.ShiftLeft(biased, 23).AsSingle();
        Vector512<float> u = f * Vector512.Create(0.6931471805599453f);
        Vector512<float> c5 = Vector512.Create(0.00833333333333333f);
        Vector512<float> poly = c5;
        poly = poly * u + Vector512.Create(0.0416666666666667f);
        poly = poly * u + Vector512.Create(0.166666666666667f);
        poly = poly * u + Vector512.Create(0.5f);
        poly = poly * u + Vector512.Create(1f);
        return pow2 * (poly * u + Vector512.Create(1f));
    }

    private static float ExpScalar(float x)
    {
        x = Math.Max(-87f, Math.Min(87f, x));
        float t = x * 1.4426950408889634f;
        int k = (int)Math.Floor(t) - 1;
        float f = t - k;
        float u = f * 0.6931471805599453f;
        float poly = ((((0.00833333333333333f * u + 0.0416666666666667f) * u + 0.166666666666667f) * u + 0.5f) * u + 1f) * u + 1f;
        return pow2f(k) * poly;
    }

    private static float pow2f(int k)
    {
        k = Math.Max(-126, Math.Min(127, k));
        return BitConverter.Int32BitsToSingle((k + 127) << 23);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorExp(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) ExpKernel512(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vLog2E = Vector256.Create(1.4426950408889634f);
            Vector256<float> vLn2 = Vector256.Create(0.6931471805599453f);
            Vector256<float> vClamp = Vector256.Create(87f);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> x = Vector256.Max(Vector256.Min(Vector256.LoadUnsafe(in src, i), vClamp), -vClamp);
                Vector256<float> tt = x * vLog2E;
                Vector256<int> k2 = Vector256.Floor(tt).AsInt32() - Vector256.Create(1);
                Vector256<float> f2 = tt - Vector256.ConvertToSingle(k2);
                Vector256<int> b2 = k2 + Vector256.Create(127);
                Vector256<float> p2 = Vector256.ShiftLeft(b2, 23).AsSingle();
                Vector256<float> u2 = f2 * vLn2;
                Vector256<float> q2 = Vector256.Create(0.00833333333333333f);
                q2 = q2 * u2 + Vector256.Create(0.0416666666666667f);
                q2 = q2 * u2 + Vector256.Create(0.166666666666667f);
                q2 = q2 * u2 + Vector256.Create(0.5f);
                q2 = q2 * u2 + Vector256.Create(1f);
                (p2 * (q2 * u2 + Vector256.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vLog2E = Vector128.Create(1.4426950408889634f);
            Vector128<float> vLn2 = Vector128.Create(0.6931471805599453f);
            Vector128<float> vClamp = Vector128.Create(87f);
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<float> x = Vector128.Max(Vector128.Min(Vector128.LoadUnsafe(in src, i), vClamp), -vClamp);
                Vector128<float> tt = x * vLog2E;
                Vector128<int> k2 = Vector128.Floor(tt).AsInt32() - Vector128.Create(1);
                Vector128<float> f2 = tt - Vector128.ConvertToSingle(k2);
                Vector128<int> b2 = k2 + Vector128.Create(127);
                Vector128<float> p2 = Vector128.ShiftLeft(b2, 23).AsSingle();
                Vector128<float> u2 = f2 * vLn2;
                Vector128<float> q2 = Vector128.Create(0.00833333333333333f);
                q2 = q2 * u2 + Vector128.Create(0.0416666666666667f);
                q2 = q2 * u2 + Vector128.Create(0.166666666666667f);
                q2 = q2 * u2 + Vector128.Create(0.5f);
                q2 = q2 * u2 + Vector128.Create(1f);
                (p2 * (q2 * u2 + Vector128.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = ExpScalar(Unsafe.Add(ref src, (nint)i));
    }

    private static Vector512<float> LogKernel512(Vector512<float> x)
    {
        Vector512<float> v = x;
        Vector512<float> n = Vector512<float>.Zero;
        Vector512<float> one = Vector512.Create(1f);
        Vector512<float> two = Vector512.Create(2f);
        Vector512<float> half = Vector512.Create(0.5f);
        for (int iter = 0; iter < 200; ++iter)
        {
            Vector512<float> g = Vector512.GreaterThanOrEqual(v, two);
            if (Vector512.Equals(g, Vector512<int>.Zero)) break;
            v = Vector512.ConditionalSelect(g, v * half, v);
            n += Vector512.ConditionalSelect(g, one, Vector512<float>.Zero);
        }
        for (int iter = 0; iter < 200; ++iter)
        {
            Vector512<float> l = Vector512.LessThan(v, one);
            if (Vector512.Equals(l, Vector512<int>.Zero)) break;
            v = Vector512.ConditionalSelect(l, v * two, v);
            n -= Vector512.ConditionalSelect(l, one, Vector512<float>.Zero);
        }
        Vector512<float> u = v - one;
        Vector512<float> poly = Vector512.Create(-0.020835085f);
        poly = poly * u + Vector512.Create(0.0277272808f);
        poly = poly * u + Vector512.Create(-0.0397820075f);
        poly = poly * u + Vector512.Create(0.0667107478f);
        poly = poly * u + Vector512.Create(-0.117496403f);
        poly = poly * u + Vector512.Create(0.333331568f);
        return n * Vector512.Create(0.6931471805599453f) + (poly * u + Vector512.Create(1f)) * u;
    }

    private static float LogScalar(float x)
    {
        if (x <= 0f) return float.NegativeInfinity;
        float v = x, n = 0f;
        for (int iter = 0; iter < 200 && v >= 2f; ++iter) { v *= 0.5f; n += 1f; }
        for (int iter = 0; iter < 200 && v < 1f; ++iter) { v *= 2f; n -= 1f; }
        float u = v - 1f;
        float poly = (((((-0.020835085f * u + 0.0277272808f) * u - 0.0397820075f) * u + 0.0667107478f) * u - 0.117496403f) * u + 0.333331568f) * u + 1f;
        return n * 0.6931471805599453f + poly * u;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLog(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) LogKernel512(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> one = Vector256.Create(1f), two = Vector256.Create(2f), half = Vector256.Create(0.5f);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> v = Vector256.LoadUnsafe(in src, i), nn = Vector256<float>.Zero;
                for (int it = 0; it < 200; ++it) { var g = Vector256.GreaterThanOrEqual(v, two); if (Vector256.Equals(g, Vector256<int>.Zero)) break; v = Vector256.ConditionalSelect(g, v * half, v); nn += Vector256.ConditionalSelect(g, one, Vector256<float>.Zero); }
                for (int it = 0; it < 200; ++it) { var l = Vector256.LessThan(v, one); if (Vector256.Equals(l, Vector256<int>.Zero)) break; v = Vector256.ConditionalSelect(l, v * two, v); nn -= Vector256.ConditionalSelect(l, one, Vector256<float>.Zero); }
                Vector256<float> u2 = v - one;
                Vector256<float> q2 = Vector256.Create(-0.020835085f);
                q2 = q2 * u2 + Vector256.Create(0.0277272808f); q2 = q2 * u2 + Vector256.Create(-0.0397820075f);
                q2 = q2 * u2 + Vector256.Create(0.0667107478f); q2 = q2 * u2 + Vector256.Create(-0.117496403f);
                q2 = q2 * u2 + Vector256.Create(0.333331568f);
                (nn * Vector256.Create(0.6931471805599453f) + (q2 * u2 + one) * u2).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> one = Vector128.Create(1f), two = Vector128.Create(2f), half = Vector128.Create(0.5f);
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<float> v = Vector128.LoadUnsafe(in src, i), nn = Vector128<float>.Zero;
                for (int it = 0; it < 200; ++it) { var g = Vector128.GreaterThanOrEqual(v, two); if (Vector128.Equals(g, Vector128<int>.Zero)) break; v = Vector128.ConditionalSelect(g, v * half, v); nn += Vector128.ConditionalSelect(g, one, Vector128<float>.Zero); }
                for (int it = 0; it < 200; ++it) { var l = Vector128.LessThan(v, one); if (Vector128.Equals(l, Vector128<int>.Zero)) break; v = Vector128.ConditionalSelect(l, v * two, v); nn -= Vector128.ConditionalSelect(l, one, Vector128<float>.Zero); }
                Vector128<float> u2 = v - one;
                Vector128<float> q2 = Vector128.Create(-0.020835085f);
                q2 = q2 * u2 + Vector128.Create(0.0277272808f); q2 = q2 * u2 + Vector128.Create(-0.0397820075f);
                q2 = q2 * u2 + Vector128.Create(0.0667107478f); q2 = q2 * u2 + Vector128.Create(-0.117496403f);
                q2 = q2 * u2 + Vector128.Create(0.333331568f);
                (nn * Vector128.Create(0.6931471805599453f) + (q2 * u2 + one) * u2).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = LogScalar(Unsafe.Add(ref src, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSigmoid(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<float> neg = Vector512.Create(0f) - Vector512.LoadUnsafe(in src, i);
                Vector512<float> e = ExpKernel512(neg);
                (Vector512.Create(1f) / (Vector512.Create(1f) + e)).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> neg = Vector256.Create(0f) - Vector256.LoadUnsafe(in src, i);
                Vector256<float> clamped = Vector256.Max(Vector256.Min(neg, Vector256.Create(87f)), Vector256.Create(-87f));
                Vector256<float> t = clamped * Vector256.Create(1.4426950408889634f);
                Vector256<int> k = Vector256.Floor(t).AsInt32() - Vector256.Create(1);
                Vector256<float> f = t - Vector256.ConvertToSingle(k);
                Vector256<float> p2 = Vector256.ShiftLeft(k + Vector256.Create(127), 23).AsSingle();
                Vector256<float> u = f * Vector256.Create(0.6931471805599453f);
                Vector256<float> poly = Vector256.Create(0.00833333333333333f);
                poly = poly * u + Vector256.Create(0.0416666666666667f); poly = poly * u + Vector256.Create(0.166666666666667f);
                poly = poly * u + Vector256.Create(0.5f); poly = poly * u + Vector256.Create(1f);
                Vector256<float> e = p2 * (poly * u + Vector256.Create(1f));
                (Vector256.Create(1f) / (Vector256.Create(1f) + e)).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<float> neg = Vector128.Create(0f) - Vector128.LoadUnsafe(in src, i);
                Vector128<float> clamped = Vector128.Max(Vector128.Min(neg, Vector128.Create(87f)), Vector128.Create(-87f));
                Vector128<float> t = clamped * Vector128.Create(1.4426950408889634f);
                Vector128<int> k = Vector128.Floor(t).AsInt32() - Vector128.Create(1);
                Vector128<float> f = t - Vector128.ConvertToSingle(k);
                Vector128<float> p2 = Vector128.ShiftLeft(k + Vector128.Create(127), 23).AsSingle();
                Vector128<float> u = f * Vector128.Create(0.6931471805599453f);
                Vector128<float> poly = Vector128.Create(0.00833333333333333f);
                poly = poly * u + Vector128.Create(0.0416666666666667f); poly = poly * u + Vector128.Create(0.166666666666667f);
                poly = poly * u + Vector128.Create(0.5f); poly = poly * u + Vector128.Create(1f);
                Vector128<float> e = p2 * (poly * u + Vector128.Create(1f));
                (Vector128.Create(1f) / (Vector128.Create(1f) + e)).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            float x = Unsafe.Add(ref src, (nint)i);
            Unsafe.Add(ref dst, (nint)i) = 1f / (1f + MathF.Exp(-x));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorTanh(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vClamp = Vector512.Create(9f);
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<float> x = Vector512.Max(Vector512.Min(Vector512.LoadUnsafe(in src, i), vClamp), -vClamp);
                Vector512<float> p = ExpKernel512(x * Vector512.Create(2f));
                ((p - Vector512.Create(1f)) / (p + Vector512.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> x = Vector256.Max(Vector256.Min(Vector256.LoadUnsafe(in src, i), Vector256.Create(9f)), Vector256.Create(-9f));
                Vector256<float> cx = x * Vector256.Create(1.4426950408889634f * 2f);
                Vector256<int> k = Vector256.Floor(cx).AsInt32() - Vector256.Create(1);
                Vector256<float> f = cx - Vector256.ConvertToSingle(k);
                Vector256<float> p2 = Vector256.ShiftLeft(k + Vector256.Create(127), 23).AsSingle();
                Vector256<float> u = f * Vector256.Create(0.6931471805599453f);
                Vector256<float> poly = Vector256.Create(0.00833333333333333f);
                poly = poly * u + Vector256.Create(0.0416666666666667f); poly = poly * u + Vector256.Create(0.166666666666667f);
                poly = poly * u + Vector256.Create(0.5f); poly = poly * u + Vector256.Create(1f);
                Vector256<float> p = p2 * (poly * u + Vector256.Create(1f));
                ((p - Vector256.Create(1f)) / (p + Vector256.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<float> x = Vector128.Max(Vector128.Min(Vector128.LoadUnsafe(in src, i), Vector128.Create(9f)), Vector128.Create(-9f));
                Vector128<float> cx = x * Vector128.Create(1.4426950408889634f * 2f);
                Vector128<int> k = Vector128.Floor(cx).AsInt32() - Vector128.Create(1);
                Vector128<float> f = cx - Vector128.ConvertToSingle(k);
                Vector128<float> p2 = Vector128.ShiftLeft(k + Vector128.Create(127), 23).AsSingle();
                Vector128<float> u = f * Vector128.Create(0.6931471805599453f);
                Vector128<float> poly = Vector128.Create(0.00833333333333333f);
                poly = poly * u + Vector128.Create(0.0416666666666667f); poly = poly * u + Vector128.Create(0.166666666666667f);
                poly = poly * u + Vector128.Create(0.5f); poly = poly * u + Vector128.Create(1f);
                Vector128<float> p = p2 * (poly * u + Vector128.Create(1f));
                ((p - Vector128.Create(1f)) / (p + Vector128.Create(1f))).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            float x = Unsafe.Add(ref src, (nint)i);
            float e2x = MathF.Exp(2f * Math.Max(-9f, Math.Min(9f, x)));
            Unsafe.Add(ref dst, (nint)i) = (e2x - 1f) / (e2x + 1f);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorReLU(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> zero = Vector512<float>.Zero;
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.Max(Vector512.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> zero = Vector256<float>.Zero;
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.Max(Vector256.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> zero = Vector128<float>.Zero;
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.Max(Vector128.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
        {
            float v = Unsafe.Add(ref src, (nint)i);
            Unsafe.Add(ref dst, (nint)i) = v > 0f ? v : 0f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorRSqrt(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector512.Create(1f) / Vector512.Sqrt(Vector512.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector256.Create(1f) / Vector256.Sqrt(Vector256.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector128.Create(1f) / Vector128.Sqrt(Vector128.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = 1f / MathF.Sqrt(Unsafe.Add(ref src, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorPow(ReadOnlySpan<float> @base, ReadOnlySpan<float> exponent, Span<float> destination)
    {
        if (@base.Length != exponent.Length || destination.Length < @base.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)@base.Length;
        if (length == 0) return;
        ref float bRef = ref MemoryMarshal.GetReference(@base);
        ref float eRef = ref MemoryMarshal.GetReference(exponent);
        ref float dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<float> b = Vector512.LoadUnsafe(in bRef, i);
                Vector512<float> e = Vector512.LoadUnsafe(in eRef, i);
                (ExpKernel512(e * LogKernel512(b))).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<float> b = Vector256.LoadUnsafe(in bRef, i);
                Vector256<float> e = Vector256.LoadUnsafe(in eRef, i);
                Vector256<float> lv = Vector256.Create(1f), nn = Vector256<float>.Zero;
                Vector256<float> two = Vector256.Create(2f), half = Vector256.Create(0.5f);
                for (int it = 0; it < 200; ++it) { var g = Vector256.GreaterThanOrEqual(b, two); if (Vector256.Equals(g, Vector256<int>.Zero)) break; b = Vector256.ConditionalSelect(g, b * half, b); nn += Vector256.ConditionalSelect(g, lv, Vector256<float>.Zero); }
                for (int it = 0; it < 200; ++it) { var l = Vector256.LessThan(b, lv); if (Vector256.Equals(l, Vector256<int>.Zero)) break; b = Vector256.ConditionalSelect(l, b * two, b); nn -= Vector256.ConditionalSelect(l, lv, Vector256<float>.Zero); }
                Vector256<float> u = b - lv;
                Vector256<float> poly = Vector256.Create(-0.020835085f);
                poly = poly * u + Vector256.Create(0.0277272808f); poly = poly * u + Vector256.Create(-0.0397820075f);
                poly = poly * u + Vector256.Create(0.0667107478f); poly = poly * u + Vector256.Create(-0.117496403f);
                poly = poly * u + Vector256.Create(0.333331568f);
                Vector256<float> logB = nn * Vector256.Create(0.6931471805599453f) + (poly * u + lv) * u;
                Vector256<float> x = e * logB;
                Vector256<float> clamped = Vector256.Max(Vector256.Min(x, Vector256.Create(87f)), Vector256.Create(-87f));
                Vector256<float> t = clamped * Vector256.Create(1.4426950408889634f);
                Vector256<int> k = Vector256.Floor(t).AsInt32() - Vector256.Create(1);
                Vector256<float> f = t - Vector256.ConvertToSingle(k);
                Vector256<float> p2 = Vector256.ShiftLeft(k + Vector256.Create(127), 23).AsSingle();
                Vector256<float> uu = f * Vector256.Create(0.6931471805599453f);
                Vector256<float> ep = Vector256.Create(0.00833333333333333f);
                ep = ep * uu + Vector256.Create(0.0416666666666667f); ep = ep * uu + Vector256.Create(0.166666666666667f);
                ep = ep * uu + Vector256.Create(0.5f); ep = ep * uu + Vector256.Create(1f);
                (p2 * (ep * uu + Vector256.Create(1f))).StoreUnsafe(ref dRef, i);
            }
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = MathF.Pow(Unsafe.Add(ref bRef, (nint)i), Unsafe.Add(ref eRef, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Softmax(ReadOnlySpan<float> source, Span<float> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        float maxVal = float.NegativeInfinity;
        for (nuint j = 0; j < length; ++j) { float v = Unsafe.Add(ref src, (nint)j); if (v > maxVal) maxVal = v; }
        float sum = 0f;
        for (nuint j = 0; j < length; ++j) { float e = MathF.Exp(Unsafe.Add(ref src, (nint)j) - maxVal); Unsafe.Add(ref dst, (nint)j) = e; sum += e; }
        float inv = 1f / sum;
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<float>.Count)
        {
            Vector512<float> vInv = Vector512.Create(inv);
            nuint step = (nuint)Vector512<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<float> vInv = Vector256.Create(inv);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<float>.Count)
        {
            Vector128<float> vInv = Vector128.Create(inv);
            nuint step = (nuint)Vector128<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) *= inv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Span<byte> PackFloatToHalf(ReadOnlySpan<float> source)
    {
        nuint length = (nuint)source.Length;
        byte[] result = GC.AllocateUninitializedArray<byte>((int)length * 2);
        ref ushort dst = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetArrayDataReference(result));
        ref float src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<float>.Count)
        {
            Vector256<uint> signMask = Vector256.Create(0x80000000u);
            Vector256<uint> expMask = Vector256.Create(0x7F800000u);
            Vector256<uint> mantMask = Vector256.Create(0x007FE000u);
            nuint step = (nuint)Vector256<float>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<uint> bits = Vector256.LoadUnsafe(in src, i).AsUInt32();
                Vector256<uint> sign = (bits & signMask) >> 16;
                Vector256<uint> exp = (bits & expMask) - Vector256.Create(0x38000000u);
                Vector256<uint> mant = (bits & mantMask) >> 13;
                Vector256.ConditionalSelect(Vector256.LessThan((Vector256<int>)(bits & expMask), Vector256.Create(0x38000000u)), sign, sign | exp | mant).AsUInt16().StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            uint bits = BitConverter.SingleToUInt32Bits(Unsafe.Add(ref src, (nint)i));
            ushort h = (ushort)(((bits >> 16) & 0x8000u) | (((bits & 0x7F800000u) - 0x38000000u) >> 13) | ((bits & 0x007FE000u) >> 13));
            Unsafe.Add(ref dst, (nint)i) = h;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void UnpackHalfToFloat(ReadOnlySpan<byte> source, Span<float> destination)
    {
        nuint count = (nuint)(source.Length / 2);
        if ((nuint)destination.Length < count) ThrowHelper.ThrowDestinationTooSmall();
        if (count == 0) return;
        ref ushort src = ref Unsafe.As<byte, ushort>(ref MemoryMarshal.GetReference(source));
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && count >= (nuint)Vector256<ushort>.Count)
        {
            nuint step = (nuint)Vector256<ushort>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<ushort> h = Vector256.LoadUnsafe(in src, i);
                Vector256<uint> sign = (h.AsUInt32() & Vector256.Create(0x8000u)).ToUInt32() << 16;
                Vector256<uint> exp = ((h.AsUInt32() & Vector256.Create(0x7C00u)).ToUInt32() + Vector256.Create(0x38000000u)) & Vector256.Create(0x7F800000u);
                Vector256<uint> mant = (h.AsUInt32() & Vector256.Create(0x03FFu)).ToUInt32() << 13;
                (sign | exp | mant).AsSingle().StoreUnsafe(ref dst, i);
            }
        }
        for (; i < count; ++i)
        {
            uint h = Unsafe.Add(ref src, (nint)i);
            uint f = ((h & 0x8000u) << 16) | (((h & 0x7C00u) + 0x38000000u) & 0x7F800000u) | ((h & 0x03FFu) << 13);
            Unsafe.Add(ref dst, (nint)i) = BitConverter.UInt32BitsToSingle(f);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Mat4x4Multiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length < 16 || right.Length < 16 || destination.Length < 16)
            ThrowHelper.ThrowMismatchedSpans();
        ref float l = ref MemoryMarshal.GetReference(left);
        ref float r = ref MemoryMarshal.GetReference(right);
        ref float d = ref MemoryMarshal.GetReference(destination);
        if (Vector128.IsHardwareAccelerated)
        {
            for (int col = 0; col < 4; ++col)
            {
                Vector128<float> rCol = Vector128.Create(
                    Unsafe.Add(ref r, (nint)(col * 4 + 0)), Unsafe.Add(ref r, (nint)(col * 4 + 1)),
                    Unsafe.Add(ref r, (nint)(col * 4 + 2)), Unsafe.Add(ref r, (nint)(col * 4 + 3)));
                Vector128<float> result = Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 0))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4));
                result += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 1))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 4));
                result += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 2))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 8));
                result += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 3))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 12));
                result.StoreUnsafe(ref d, (nuint)(col * 4));
            }
        }
        else
        {
            for (int col = 0; col < 4; ++col)
            {
                for (int row = 0; row < 4; ++row)
                {
                    float sum = 0f;
                    for (int k = 0; k < 4; ++k)
                        sum += Unsafe.Add(ref l, (nint)(k * 4 + col)) * Unsafe.Add(ref r, (nint)(row + k * 4));
                    Unsafe.Add(ref d, (nint)(row + col * 4)) = sum;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CrossProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length < 3 || right.Length < 3 || destination.Length < 3) ThrowHelper.ThrowMismatchedSpans();
        ref float l = ref MemoryMarshal.GetReference(left);
        ref float r = ref MemoryMarshal.GetReference(right);
        float lx = Unsafe.Add(ref l, 0), ly = Unsafe.Add(ref l, 1), lz = Unsafe.Add(ref l, 2);
        float rx = Unsafe.Add(ref r, 0), ry = Unsafe.Add(ref r, 1), rz = Unsafe.Add(ref r, 2);
        ref float d = ref MemoryMarshal.GetReference(destination);
        Unsafe.Add(ref d, 0) = ly * rz - lz * ry;
        Unsafe.Add(ref d, 1) = lz * rx - lx * rz;
        Unsafe.Add(ref d, 2) = lx * ry - ly * rx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionMultiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        if (left.Length < 4 || right.Length < 4 || destination.Length < 4) ThrowHelper.ThrowMismatchedSpans();
        ref float l = ref MemoryMarshal.GetReference(left);
        ref float r = ref MemoryMarshal.GetReference(right);
        float lx = Unsafe.Add(ref l, 0), ly = Unsafe.Add(ref l, 1), lz = Unsafe.Add(ref l, 2), lw = Unsafe.Add(ref l, 3);
        float rx = Unsafe.Add(ref r, 0), ry = Unsafe.Add(ref r, 1), rz = Unsafe.Add(ref r, 2), rw = Unsafe.Add(ref r, 3);
        ref float d = ref MemoryMarshal.GetReference(destination);
        Unsafe.Add(ref d, 0) = lw * rx + lx * rw + ly * rz - lz * ry;
        Unsafe.Add(ref d, 1) = lw * ry - lx * rz + ly * rw + lz * rx;
        Unsafe.Add(ref d, 2) = lw * rz + lx * ry - ly * rx + lz * rw;
        Unsafe.Add(ref d, 3) = lw * rw - lx * rx - ly * ry - lz * rz;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionSlerp(ReadOnlySpan<float> from, ReadOnlySpan<float> to, float t, Span<float> destination)
    {
        if (from.Length < 4 || to.Length < 4 || destination.Length < 4) ThrowHelper.ThrowMismatchedSpans();
        ref float a = ref MemoryMarshal.GetReference(from);
        ref float b = ref MemoryMarshal.GetReference(to);
        float dot = Unsafe.Add(ref a, 0) * Unsafe.Add(ref b, 0) + Unsafe.Add(ref a, 1) * Unsafe.Add(ref b, 1)
                  + Unsafe.Add(ref a, 2) * Unsafe.Add(ref b, 2) + Unsafe.Add(ref a, 3) * Unsafe.Add(ref b, 3);
        float bx = Unsafe.Add(ref b, 0), by = Unsafe.Add(ref b, 1), bz = Unsafe.Add(ref b, 2), bw = Unsafe.Add(ref b, 3);
        if (dot < 0f) { dot = -dot; bx = -bx; by = -by; bz = -bz; bw = -bw; }
        ref float d = ref MemoryMarshal.GetReference(destination);
        if (dot > 0.9995f)
        {
            Unsafe.Add(ref d, 0) = Unsafe.Add(ref a, 0) + t * (bx - Unsafe.Add(ref a, 0));
            Unsafe.Add(ref d, 1) = Unsafe.Add(ref a, 1) + t * (by - Unsafe.Add(ref a, 1));
            Unsafe.Add(ref d, 2) = Unsafe.Add(ref a, 2) + t * (bz - Unsafe.Add(ref a, 2));
            Unsafe.Add(ref d, 3) = Unsafe.Add(ref a, 3) + t * (bw - Unsafe.Add(ref a, 3));
            float len = MathF.Sqrt(Unsafe.Add(ref d, 0) * Unsafe.Add(ref d, 0) + Unsafe.Add(ref d, 1) * Unsafe.Add(ref d, 1)
                                 + Unsafe.Add(ref d, 2) * Unsafe.Add(ref d, 2) + Unsafe.Add(ref d, 3) * Unsafe.Add(ref d, 3));
            float inv = 1f / len;
            Unsafe.Add(ref d, 0) *= inv; Unsafe.Add(ref d, 1) *= inv; Unsafe.Add(ref d, 2) *= inv; Unsafe.Add(ref d, 3) *= inv;
        }
        else
        {
            float theta = MathF.Acos(Math.Clamp(dot, -1f, 1f));
            float sinTheta = MathF.Sin(theta);
            float w0 = MathF.Sin((1f - t) * theta) / sinTheta;
            float w1 = MathF.Sin(t * theta) / sinTheta;
            Unsafe.Add(ref d, 0) = w0 * Unsafe.Add(ref a, 0) + w1 * bx;
            Unsafe.Add(ref d, 1) = w0 * Unsafe.Add(ref a, 1) + w1 * by;
            Unsafe.Add(ref d, 2) = w0 * Unsafe.Add(ref a, 2) + w1 * bz;
            Unsafe.Add(ref d, 3) = w0 * Unsafe.Add(ref a, 3) + w1 * bw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Swizzle(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
    {
        if (destination.Length < indices.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref float src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref float dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && count >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<float>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector256.Shuffle(Vector256.LoadUnsafe(in src, i), Vector256.LoadUnsafe(in idx, i).AsSingle()).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && count >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<float>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector128.Shuffle(Vector128.LoadUnsafe(in src, i), Vector128.LoadUnsafe(in idx, i).AsSingle()).StoreUnsafe(ref dst, i);
        }
        for (; i < count; ++i)
            Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, Unsafe.Add(ref idx, (nint)i));
    }
}

[SkipLocalsInit]
public static unsafe class SimdFloat64
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void TransformLinear(ReadOnlySpan<double> source, double multiplier, double offset, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<double> vMul = Vector512.Create(multiplier);
            Vector512<double> vOff = Vector512.Create(offset);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = (Vector512.LoadUnsafe(in src, i) * vMul) + vOff;
                Vector512<double> r1 = (Vector512.LoadUnsafe(in src, i + step) * vMul) + vOff;
                r0.StoreUnsafe(ref dst, i);
                r1.StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector512.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<double> vMul = Vector256.Create(multiplier);
            Vector256<double> vOff = Vector256.Create(offset);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = (Vector256.LoadUnsafe(in src, i) * vMul) + vOff;
                Vector256<double> r1 = (Vector256.LoadUnsafe(in src, i + step) * vMul) + vOff;
                r0.StoreUnsafe(ref dst, i);
                r1.StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector256.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<double> vMul = Vector128.Create(multiplier);
            Vector128<double> vOff = Vector128.Create(offset);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = (Vector128.LoadUnsafe(in src, i) * vMul) + vOff;
                Vector128<double> r1 = (Vector128.LoadUnsafe(in src, i + step) * vMul) + vOff;
                r0.StoreUnsafe(ref dst, i);
                r1.StoreUnsafe(ref dst, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector128.LoadUnsafe(in src, i) * vMul) + vOff).StoreUnsafe(ref dst, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dst, (nint)(i + 0)) = (Unsafe.Add(ref src, (nint)(i + 0)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 1)) = (Unsafe.Add(ref src, (nint)(i + 1)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 2)) = (Unsafe.Add(ref src, (nint)(i + 2)) * multiplier) + offset;
            Unsafe.Add(ref dst, (nint)(i + 3)) = (Unsafe.Add(ref src, (nint)(i + 3)) * multiplier) + offset;
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dst, (nint)i) = (Unsafe.Add(ref src, (nint)i) * multiplier) + offset;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAdd(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = Vector512.LoadUnsafe(in lRef, i) + Vector512.LoadUnsafe(in rRef, i);
                Vector512<double> r1 = Vector512.LoadUnsafe(in lRef, i + step) + Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector512.LoadUnsafe(in lRef, i) + Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = Vector256.LoadUnsafe(in lRef, i) + Vector256.LoadUnsafe(in rRef, i);
                Vector256<double> r1 = Vector256.LoadUnsafe(in lRef, i + step) + Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector256.LoadUnsafe(in lRef, i) + Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = Vector128.LoadUnsafe(in lRef, i) + Vector128.LoadUnsafe(in rRef, i);
                Vector128<double> r1 = Vector128.LoadUnsafe(in lRef, i + step) + Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector128.LoadUnsafe(in lRef, i) + Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) + Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) + Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) + Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) + Unsafe.Add(ref rRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) + Unsafe.Add(ref rRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorMultiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
                Vector512<double> r1 = Vector512.LoadUnsafe(in lRef, i + step) * Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
                Vector256<double> r1 = Vector256.LoadUnsafe(in lRef, i + step) * Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
                Vector128<double> r1 = Vector128.LoadUnsafe(in lRef, i + step) * Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                (Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) * Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) * Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) * Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) * Unsafe.Add(ref rRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) * Unsafe.Add(ref rRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFma(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> destination)
    {
        if (a.Length != b.Length || a.Length != c.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);
        ref double cRef = ref MemoryMarshal.GetReference(c);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = (Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) + Vector512.LoadUnsafe(in cRef, i);
                Vector512<double> r1 = (Vector512.LoadUnsafe(in aRef, i + step) * Vector512.LoadUnsafe(in bRef, i + step)) + Vector512.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) + Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = (Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) + Vector256.LoadUnsafe(in cRef, i);
                Vector256<double> r1 = (Vector256.LoadUnsafe(in aRef, i + step) * Vector256.LoadUnsafe(in bRef, i + step)) + Vector256.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) + Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = (Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) + Vector128.LoadUnsafe(in cRef, i);
                Vector128<double> r1 = (Vector128.LoadUnsafe(in aRef, i + step) * Vector128.LoadUnsafe(in bRef, i + step)) + Vector128.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i);
                r1.StoreUnsafe(ref dRef, i + step);
            }

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) + Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
            }
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = (Unsafe.Add(ref aRef, (nint)(i + 0)) * Unsafe.Add(ref bRef, (nint)(i + 0))) + Unsafe.Add(ref cRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = (Unsafe.Add(ref aRef, (nint)(i + 1)) * Unsafe.Add(ref bRef, (nint)(i + 1))) + Unsafe.Add(ref cRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = (Unsafe.Add(ref aRef, (nint)(i + 2)) * Unsafe.Add(ref bRef, (nint)(i + 2))) + Unsafe.Add(ref cRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = (Unsafe.Add(ref aRef, (nint)(i + 3)) * Unsafe.Add(ref bRef, (nint)(i + 3))) + Unsafe.Add(ref cRef, (nint)(i + 3));
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = (Unsafe.Add(ref aRef, (nint)i) * Unsafe.Add(ref bRef, (nint)i)) + Unsafe.Add(ref cRef, (nint)i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorClamp(ReadOnlySpan<double> source, double min, double max, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref double sRef = ref MemoryMarshal.GetReference(source);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vMin = Vector512.Create(min);
            Vector512<double> vMax = Vector512.Create(max);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<double> val = Vector512.LoadUnsafe(in sRef, i);
                Vector512.Min(Vector512.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vMin = Vector256.Create(min);
            Vector256<double> vMax = Vector256.Create(max);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<double> val = Vector256.LoadUnsafe(in sRef, i);
                Vector256.Min(Vector256.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vMin = Vector128.Create(min);
            Vector128<double> vMax = Vector128.Create(max);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<double> val = Vector128.LoadUnsafe(in sRef, i);
                Vector128.Min(Vector128.Max(val, vMin), vMax).StoreUnsafe(ref dRef, i);
            }
        }

        for (; i < length; ++i)
        {
            Unsafe.Add(ref dRef, (nint)i) = Math.Clamp(Unsafe.Add(ref sRef, (nint)i), min, max);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeSum(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 4)
        {
            Vector512<double> a0 = Vector512<double>.Zero;
            Vector512<double> a1 = Vector512<double>.Zero;
            Vector512<double> a2 = Vector512<double>.Zero;
            Vector512<double> a3 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector512.LoadUnsafe(in src, i);
                a1 += Vector512.LoadUnsafe(in src, i + step);
                a2 += Vector512.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector512.LoadUnsafe(in src, i + (step * 3));
            }

            Vector512<double> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector512.LoadUnsafe(in src, i);
            }

            return Vector512.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 4)
        {
            Vector256<double> a0 = Vector256<double>.Zero;
            Vector256<double> a1 = Vector256<double>.Zero;
            Vector256<double> a2 = Vector256<double>.Zero;
            Vector256<double> a3 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector256.LoadUnsafe(in src, i);
                a1 += Vector256.LoadUnsafe(in src, i + step);
                a2 += Vector256.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector256.LoadUnsafe(in src, i + (step * 3));
            }

            Vector256<double> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector256.LoadUnsafe(in src, i);
            }

            return Vector256.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 4)
        {
            Vector128<double> a0 = Vector128<double>.Zero;
            Vector128<double> a1 = Vector128<double>.Zero;
            Vector128<double> a2 = Vector128<double>.Zero;
            Vector128<double> a3 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 4) + 1;

            for (; i < limit; i += step * 4)
            {
                a0 += Vector128.LoadUnsafe(in src, i);
                a1 += Vector128.LoadUnsafe(in src, i + step);
                a2 += Vector128.LoadUnsafe(in src, i + (step * 2));
                a3 += Vector128.LoadUnsafe(in src, i + (step * 3));
            }

            Vector128<double> acc = (a0 + a1) + (a2 + a3);
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector128.LoadUnsafe(in src, i);
            }

            return Vector128.Sum(acc) + ScalarTailSum(ref src, i, length);
        }

        return ScalarTailSum(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ScalarTailSum(ref double src, nuint start, nuint length)
    {
        double s0 = 0.0, s1 = 0.0, s2 = 0.0, s3 = 0.0;
        nuint i = start;
        nuint limit = length >= 4 ? length - 3 : 0;

        for (; i < limit; i += 4)
        {
            s0 += Unsafe.Add(ref src, (nint)(i + 0));
            s1 += Unsafe.Add(ref src, (nint)(i + 1));
            s2 += Unsafe.Add(ref src, (nint)(i + 2));
            s3 += Unsafe.Add(ref src, (nint)(i + 3));
        }

        double acc = (s0 + s1) + (s2 + s3);
        for (; i < length; ++i)
        {
            acc += Unsafe.Add(ref src, (nint)i);
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeDotProduct(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
    {
        if (left.Length != right.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return 0.0;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<double> a0 = Vector512<double>.Zero;
            Vector512<double> a1 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
                a1 += Vector512.LoadUnsafe(in lRef, i + step) * Vector512.LoadUnsafe(in rRef, i + step);
            }

            Vector512<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i);
            }

            return Vector512.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<double> a0 = Vector256<double>.Zero;
            Vector256<double> a1 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
                a1 += Vector256.LoadUnsafe(in lRef, i + step) * Vector256.LoadUnsafe(in rRef, i + step);
            }

            Vector256<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i);
            }

            return Vector256.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<double> a0 = Vector128<double>.Zero;
            Vector128<double> a1 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                a0 += Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
                a1 += Vector128.LoadUnsafe(in lRef, i + step) * Vector128.LoadUnsafe(in rRef, i + step);
            }

            Vector128<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                acc += Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i);
            }

            return Vector128.Sum(acc) + ScalarTailDot(ref lRef, ref rRef, i, length);
        }

        return ScalarTailDot(ref lRef, ref rRef, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ScalarTailDot(ref double lRef, ref double rRef, nuint start, nuint length)
    {
        double d0 = 0.0, d1 = 0.0;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;

        for (; i < limit; i += 2)
        {
            d0 += Unsafe.Add(ref lRef, (nint)(i + 0)) * Unsafe.Add(ref rRef, (nint)(i + 0));
            d1 += Unsafe.Add(ref lRef, (nint)(i + 1)) * Unsafe.Add(ref rRef, (nint)(i + 1));
        }

        double acc = d0 + d1;
        for (; i < length; ++i)
        {
            acc += Unsafe.Add(ref lRef, (nint)i) * Unsafe.Add(ref rRef, (nint)i);
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeL2Norm(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<double> a0 = Vector512<double>.Zero;
            Vector512<double> a1 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector512<double> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<double> v1 = Vector512.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector512<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<double> v = Vector512.LoadUnsafe(in src, i);
                acc += v * v;
            }

            double sumOfSquares = Vector512.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return Math.Sqrt(sumOfSquares);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<double> a0 = Vector256<double>.Zero;
            Vector256<double> a1 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector256<double> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<double> v1 = Vector256.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector256<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<double> v = Vector256.LoadUnsafe(in src, i);
                acc += v * v;
            }

            double sumOfSquares = Vector256.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return Math.Sqrt(sumOfSquares);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<double> a0 = Vector128<double>.Zero;
            Vector128<double> a1 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;

            for (; i < limit; i += step * 2)
            {
                Vector128<double> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<double> v1 = Vector128.LoadUnsafe(in src, i + step);
                a0 += v0 * v0;
                a1 += v1 * v1;
            }

            Vector128<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128<double> v = Vector128.LoadUnsafe(in src, i);
                acc += v * v;
            }

            double sumOfSquares = Vector128.Sum(acc) + ScalarTailSquareSum(ref src, i, length);
            return Math.Sqrt(sumOfSquares);
        }

        return Math.Sqrt(ScalarTailSquareSum(ref src, 0, length));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ScalarTailSquareSum(ref double src, nuint start, nuint length)
    {
        double s0 = 0.0, s1 = 0.0;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;

        for (; i < limit; i += 2)
        {
            double v0 = Unsafe.Add(ref src, (nint)(i + 0));
            double v1 = Unsafe.Add(ref src, (nint)(i + 1));
            s0 += v0 * v0;
            s1 += v1 * v1;
        }

        double acc = s0 + s1;
        for (; i < length; ++i)
        {
            double v = Unsafe.Add(ref src, (nint)i);
            acc += v * v;
        }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ExtremaPair<double> ComputeExtrema(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count;
            Vector512<double> vMin0 = Vector512.LoadUnsafe(in src, 0);
            Vector512<double> vMax0 = vMin0;
            Vector512<double> vMin1 = Vector512.LoadUnsafe(in src, step);
            Vector512<double> vMax1 = vMin1;
            nuint limit = length - (step * 2) + 1;

            for (i = step * 2; i < limit; i += step * 2)
            {
                Vector512<double> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<double> v1 = Vector512.LoadUnsafe(in src, i + step);
                vMin0 = Vector512.Min(vMin0, v0);
                vMax0 = Vector512.Max(vMax0, v0);
                vMin1 = Vector512.Min(vMin1, v1);
                vMax1 = Vector512.Max(vMax1, v1);
            }

            Vector512<double> vMin = Vector512.Min(vMin0, vMin1);
            Vector512<double> vMax = Vector512.Max(vMax0, vMax1);

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<double> val = Vector512.LoadUnsafe(in src, i);
                vMin = Vector512.Min(vMin, val);
                vMax = Vector512.Max(vMax, val);
            }

            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            for (int lane = 0; lane < Vector512<double>.Count; ++lane)
            {
                min = double.Min(min, vMin.GetElement(lane));
                max = double.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                double v = Unsafe.Add(ref src, (nint)i);
                min = double.Min(min, v);
                max = double.Max(max, v);
            }
            return new ExtremaPair<double>(min, max);
        }

        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count;
            Vector256<double> vMin0 = Vector256.LoadUnsafe(in src, 0);
            Vector256<double> vMax0 = vMin0;
            Vector256<double> vMin1 = Vector256.LoadUnsafe(in src, step);
            Vector256<double> vMax1 = vMin1;
            nuint limit = length - (step * 2) + 1;

            for (i = step * 2; i < limit; i += step * 2)
            {
                Vector256<double> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<double> v1 = Vector256.LoadUnsafe(in src, i + step);
                vMin0 = Vector256.Min(vMin0, v0);
                vMax0 = Vector256.Max(vMax0, v0);
                vMin1 = Vector256.Min(vMin1, v1);
                vMax1 = Vector256.Max(vMax1, v1);
            }

            Vector256<double> vMin = Vector256.Min(vMin0, vMin1);
            Vector256<double> vMax = Vector256.Max(vMax0, vMax1);

            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<double> val = Vector256.LoadUnsafe(in src, i);
                vMin = Vector256.Min(vMin, val);
                vMax = Vector256.Max(vMax, val);
            }

            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            for (int lane = 0; lane < Vector256<double>.Count; ++lane)
            {
                min = double.Min(min, vMin.GetElement(lane));
                max = double.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                double v = Unsafe.Add(ref src, (nint)i);
                min = double.Min(min, v);
                max = double.Max(max, v);
            }
            return new ExtremaPair<double>(min, max);
        }

        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vMin = Vector128.LoadUnsafe(in src, 0);
            Vector128<double> vMax = vMin;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;

            for (i = step; i < limit; i += step)
            {
                Vector128<double> val = Vector128.LoadUnsafe(in src, i);
                vMin = Vector128.Min(vMin, val);
                vMax = Vector128.Max(vMax, val);
            }

            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            for (int lane = 0; lane < Vector128<double>.Count; ++lane)
            {
                min = double.Min(min, vMin.GetElement(lane));
                max = double.Max(max, vMax.GetElement(lane));
            }

            for (; i < length; ++i)
            {
                double v = Unsafe.Add(ref src, (nint)i);
                min = double.Min(min, v);
                max = double.Max(max, v);
            }
            return new ExtremaPair<double>(min, max);
        }

        double sMin = Unsafe.Add(ref src, 0);
        double sMax = sMin;
        for (i = 1; i < length; ++i)
        {
            double v = Unsafe.Add(ref src, (nint)i);
            sMin = double.Min(sMin, v);
            sMax = double.Max(sMax, v);
        }
        return new ExtremaPair<double>(sMin, sMax);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstGreaterThan(ReadOnlySpan<double> source, double threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<double> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                if (bits != 0)
                {
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<double> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                if (bits != 0)
                {
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<double> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                if (bits != 0)
                {
                    return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
                }
            }
        }

        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) > threshold)
            {
                return (int)i;
            }
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountGreaterThan(ReadOnlySpan<double> source, double threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint count = 0;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector512<double> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector512.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector256<double> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector256.ExtractMostSignificantBits(mask));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;

            for (; i < limit; i += step)
            {
                Vector128<double> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                count += (nuint)BitOperations.PopCount(Vector128.ExtractMostSignificantBits(mask));
            }
        }

        for (; i < length; ++i)
        {
            if (Unsafe.Add(ref src, (nint)i) > threshold)
            {
                count++;
            }
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterGreaterThan(ReadOnlySpan<double> source, double threshold, Span<double> destination)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return ScanResult.Success(0);

        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint dstCap = (nuint)destination.Length;
        nuint written = 0;
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vThresh = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector512<double> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector512<double> mask = Vector512.GreaterThan(Vector512.LoadUnsafe(in src, i), vThresh);
                ulong bits = Vector512.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vThresh = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector256<double> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector256<double> mask = Vector256.GreaterThan(Vector256.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector256.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vThresh = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;

            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;

            for (; i < safeLimit; i += step)
            {
                Vector128<double> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }

            for (; i < limit; i += step)
            {
                Vector128<double> mask = Vector128.GreaterThan(Vector128.LoadUnsafe(in src, i), vThresh);
                uint bits = Vector128.ExtractMostSignificantBits(mask);
                while (bits != 0)
                {
                    if (written >= dstCap) return ScanResult.Overflow(written);
                    int lane = BitOperations.TrailingZeroCount(bits);
                    Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane));
                    bits &= bits - 1;
                }
            }
        }

        for (; i < length; ++i)
        {
            double val = Unsafe.Add(ref src, (nint)i);
            if (val > threshold)
            {
                if (written >= dstCap) return ScanResult.Overflow(written);
                Unsafe.Add(ref dst, (nint)written++) = val;
            }
        }

        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSubtract(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i);
                Vector512<double> r1 = Vector512.LoadUnsafe(in lRef, i + step) - Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i);
                Vector256<double> r1 = Vector256.LoadUnsafe(in lRef, i + step) - Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = Vector128.LoadUnsafe(in lRef, i) - Vector128.LoadUnsafe(in rRef, i);
                Vector128<double> r1 = Vector128.LoadUnsafe(in lRef, i + step) - Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
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
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) - Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorNegate(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector512<double>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector512<double>.Zero - Vector512.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512<double>.Zero - Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector256<double>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector256<double>.Zero - Vector256.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256<double>.Zero - Vector256.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                (Vector128<double>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
                (Vector128<double>.Zero - Vector128.LoadUnsafe(in src, i + step)).StoreUnsafe(ref dst, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128<double>.Zero - Vector128.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dst, (nint)(i + 0)) = -Unsafe.Add(ref src, (nint)(i + 0));
            Unsafe.Add(ref dst, (nint)(i + 1)) = -Unsafe.Add(ref src, (nint)(i + 1));
            Unsafe.Add(ref dst, (nint)(i + 2)) = -Unsafe.Add(ref src, (nint)(i + 2));
            Unsafe.Add(ref dst, (nint)(i + 3)) = -Unsafe.Add(ref src, (nint)(i + 3));
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = -Unsafe.Add(ref src, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorDivide(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = Vector512.LoadUnsafe(in lRef, i) / Vector512.LoadUnsafe(in rRef, i);
                Vector512<double> r1 = Vector512.LoadUnsafe(in lRef, i + step) / Vector512.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector512.LoadUnsafe(in lRef, i) / Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = Vector256.LoadUnsafe(in lRef, i) / Vector256.LoadUnsafe(in rRef, i);
                Vector256<double> r1 = Vector256.LoadUnsafe(in lRef, i + step) / Vector256.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector256.LoadUnsafe(in lRef, i) / Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = Vector128.LoadUnsafe(in lRef, i) / Vector128.LoadUnsafe(in rRef, i);
                Vector128<double> r1 = Vector128.LoadUnsafe(in lRef, i + step) / Vector128.LoadUnsafe(in rRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                (Vector128.LoadUnsafe(in lRef, i) / Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = Unsafe.Add(ref lRef, (nint)(i + 0)) / Unsafe.Add(ref rRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = Unsafe.Add(ref lRef, (nint)(i + 1)) / Unsafe.Add(ref rRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = Unsafe.Add(ref lRef, (nint)(i + 2)) / Unsafe.Add(ref rRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = Unsafe.Add(ref lRef, (nint)(i + 3)) / Unsafe.Add(ref rRef, (nint)(i + 3));
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) / Unsafe.Add(ref rRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorAbs(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;

        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<ulong> absMask = Vector512.Create(0x7FFFFFFFFFFFFFFFUL);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector512.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble().StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<ulong> absMask = Vector256.Create(0x7FFFFFFFFFFFFFFFUL);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector256.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble().StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<ulong> absMask = Vector128.Create(0x7FFFFFFFFFFFFFFFUL);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                (Vector128.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble().StoreUnsafe(ref dst, i);
        }

        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = Math.Abs(Unsafe.Add(ref src, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorFms(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> destination)
    {
        if (a.Length != b.Length || a.Length != c.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);
        ref double cRef = ref MemoryMarshal.GetReference(c);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<double> r0 = (Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) - Vector512.LoadUnsafe(in cRef, i);
                Vector512<double> r1 = (Vector512.LoadUnsafe(in aRef, i + step) * Vector512.LoadUnsafe(in bRef, i + step)) - Vector512.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector512.LoadUnsafe(in aRef, i) * Vector512.LoadUnsafe(in bRef, i)) - Vector512.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<double> r0 = (Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) - Vector256.LoadUnsafe(in cRef, i);
                Vector256<double> r1 = (Vector256.LoadUnsafe(in aRef, i + step) * Vector256.LoadUnsafe(in bRef, i + step)) - Vector256.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector256.LoadUnsafe(in aRef, i) * Vector256.LoadUnsafe(in bRef, i)) - Vector256.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<double> r0 = (Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) - Vector128.LoadUnsafe(in cRef, i);
                Vector128<double> r1 = (Vector128.LoadUnsafe(in aRef, i + step) * Vector128.LoadUnsafe(in bRef, i + step)) - Vector128.LoadUnsafe(in cRef, i + step);
                r0.StoreUnsafe(ref dRef, i); r1.StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                ((Vector128.LoadUnsafe(in aRef, i) * Vector128.LoadUnsafe(in bRef, i)) - Vector128.LoadUnsafe(in cRef, i)).StoreUnsafe(ref dRef, i);
        }

        nuint scalarLimit = length >= 4 ? length - 3 : 0;
        for (; i < scalarLimit; i += 4)
        {
            Unsafe.Add(ref dRef, (nint)(i + 0)) = (Unsafe.Add(ref aRef, (nint)(i + 0)) * Unsafe.Add(ref bRef, (nint)(i + 0))) - Unsafe.Add(ref cRef, (nint)(i + 0));
            Unsafe.Add(ref dRef, (nint)(i + 1)) = (Unsafe.Add(ref aRef, (nint)(i + 1)) * Unsafe.Add(ref bRef, (nint)(i + 1))) - Unsafe.Add(ref cRef, (nint)(i + 1));
            Unsafe.Add(ref dRef, (nint)(i + 2)) = (Unsafe.Add(ref aRef, (nint)(i + 2)) * Unsafe.Add(ref bRef, (nint)(i + 2))) - Unsafe.Add(ref cRef, (nint)(i + 2));
            Unsafe.Add(ref dRef, (nint)(i + 3)) = (Unsafe.Add(ref aRef, (nint)(i + 3)) * Unsafe.Add(ref bRef, (nint)(i + 3))) - Unsafe.Add(ref cRef, (nint)(i + 3));
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = (Unsafe.Add(ref aRef, (nint)i) * Unsafe.Add(ref bRef, (nint)i)) - Unsafe.Add(ref cRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLerp(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double t, Span<double> destination)
    {
        if (a.Length != b.Length || destination.Length < a.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)a.Length;
        if (length == 0) return;

        ref double aRef = ref MemoryMarshal.GetReference(a);
        ref double bRef = ref MemoryMarshal.GetReference(b);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vT = Vector512.Create(t);
            Vector512<double> oneMinusT = Vector512.Create(1.0 - t);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                ((Vector512.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector512.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vT = Vector256.Create(t);
            Vector256<double> oneMinusT = Vector256.Create(1.0 - t);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                ((Vector256.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector256.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vT = Vector128.Create(t);
            Vector128<double> oneMinusT = Vector128.Create(1.0 - t);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
                ((Vector128.LoadUnsafe(in aRef, i) * oneMinusT) + (Vector128.LoadUnsafe(in bRef, i) * vT)).StoreUnsafe(ref dRef, i);
        }

        for (; i < length; ++i)
        {
            double aVal = Unsafe.Add(ref aRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = (aVal * (1.0 - t)) + (Unsafe.Add(ref bRef, (nint)i) * t);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMax(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i + step), Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i + step), Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i + step), Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }

        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = double.Max(Unsafe.Add(ref lRef, (nint)i), Unsafe.Add(ref rRef, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void ElementWiseMin(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;

        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i + step), Vector512.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i + step), Vector256.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i + step), Vector128.LoadUnsafe(in rRef, i + step)).StoreUnsafe(ref dRef, i + step);
            }
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }

        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = double.Min(Unsafe.Add(ref lRef, (nint)i), Unsafe.Add(ref rRef, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeMean(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;
        return ComputeSum(source) / (double)length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeVariance(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length <= 1) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<double> s0 = Vector512<double>.Zero, s1 = Vector512<double>.Zero;
            Vector512<double> q0 = Vector512<double>.Zero, q1 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<double> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<double> v1 = Vector512.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1; q0 += v0 * v0; q1 += v1 * v1;
            }
            Vector512<double> accS = s0 + s1; Vector512<double> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector512<double> v = Vector512.LoadUnsafe(in src, i);
                accS += v; accQ += v * v;
            }
            double sum = Vector512.Sum(accS) + ScalarTailSumD(ref src, i, length);
            double sumSq = Vector512.Sum(accQ) + ScalarTailSquareSumD(ref src, i, length);
            double n = (double)length; double mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<double> s0 = Vector256<double>.Zero, s1 = Vector256<double>.Zero;
            Vector256<double> q0 = Vector256<double>.Zero, q1 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<double> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<double> v1 = Vector256.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1; q0 += v0 * v0; q1 += v1 * v1;
            }
            Vector256<double> accS = s0 + s1; Vector256<double> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector256<double> v = Vector256.LoadUnsafe(in src, i);
                accS += v; accQ += v * v;
            }
            double sum = Vector256.Sum(accS) + ScalarTailSumD(ref src, i, length);
            double sumSq = Vector256.Sum(accQ) + ScalarTailSquareSumD(ref src, i, length);
            double n = (double)length; double mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<double> s0 = Vector128<double>.Zero, s1 = Vector128<double>.Zero;
            Vector128<double> q0 = Vector128<double>.Zero, q1 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<double> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<double> v1 = Vector128.LoadUnsafe(in src, i + step);
                s0 += v0; s1 += v1; q0 += v0 * v0; q1 += v1 * v1;
            }
            Vector128<double> accS = s0 + s1; Vector128<double> accQ = q0 + q1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
            {
                Vector128<double> v = Vector128.LoadUnsafe(in src, i);
                accS += v; accQ += v * v;
            }
            double sum = Vector128.Sum(accS) + ScalarTailSumD(ref src, i, length);
            double sumSq = Vector128.Sum(accQ) + ScalarTailSquareSumD(ref src, i, length);
            double n = (double)length; double mean = sum / n;
            return (sumSq / n) - (mean * mean);
        }

        double sSum = 0.0, sSq = 0.0;
        for (i = 0; i < length; ++i)
        {
            double v = Unsafe.Add(ref src, (nint)i);
            sSum += v; sSq += v * v;
        }
        double sn = (double)length; double sm = sSum / sn;
        return (sSq / sn) - (sm * sm);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeStdDev(ReadOnlySpan<double> source) => Math.Sqrt(ComputeVariance(source));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeSumOfSquares(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<double> a0 = Vector512<double>.Zero, a1 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector512<double> v0 = Vector512.LoadUnsafe(in src, i);
                Vector512<double> v1 = Vector512.LoadUnsafe(in src, i + step);
                a0 += v0 * v0; a1 += v1 * v1;
            }
            Vector512<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) { Vector512<double> v = Vector512.LoadUnsafe(in src, i); acc += v * v; }
            return Vector512.Sum(acc) + ScalarTailSquareSumD(ref src, i, length);
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<double> a0 = Vector256<double>.Zero, a1 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector256<double> v0 = Vector256.LoadUnsafe(in src, i);
                Vector256<double> v1 = Vector256.LoadUnsafe(in src, i + step);
                a0 += v0 * v0; a1 += v1 * v1;
            }
            Vector256<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) { Vector256<double> v = Vector256.LoadUnsafe(in src, i); acc += v * v; }
            return Vector256.Sum(acc) + ScalarTailSquareSumD(ref src, i, length);
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<double> a0 = Vector128<double>.Zero, a1 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                Vector128<double> v0 = Vector128.LoadUnsafe(in src, i);
                Vector128<double> v1 = Vector128.LoadUnsafe(in src, i + step);
                a0 += v0 * v0; a1 += v1 * v1;
            }
            Vector128<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step) { Vector128<double> v = Vector128.LoadUnsafe(in src, i); acc += v * v; }
            return Vector128.Sum(acc) + ScalarTailSquareSumD(ref src, i, length);
        }
        return ScalarTailSquareSumD(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeProduct(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 1.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> acc = Vector512.Create(1.0);
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step) acc *= Vector512.LoadUnsafe(in src, i);
            double result = 1.0;
            for (int lane = 0; lane < Vector512<double>.Count; ++lane) result *= acc.GetElement(lane);
            for (; i < length; ++i) result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> acc = Vector256.Create(1.0);
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step) acc *= Vector256.LoadUnsafe(in src, i);
            double result = 1.0;
            for (int lane = 0; lane < Vector256<double>.Count; ++lane) result *= acc.GetElement(lane);
            for (; i < length; ++i) result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> acc = Vector128.Create(1.0);
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step) acc *= Vector128.LoadUnsafe(in src, i);
            double result = 1.0;
            for (int lane = 0; lane < Vector128<double>.Count; ++lane) result *= acc.GetElement(lane);
            for (; i < length; ++i) result *= Unsafe.Add(ref src, (nint)i);
            return result;
        }

        double product = 1.0;
        for (; i < length; ++i) product *= Unsafe.Add(ref src, (nint)i);
        return product;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeL1Norm(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count * 2)
        {
            Vector512<ulong> absMask = Vector512.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector512<double> a0 = Vector512<double>.Zero, a1 = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                a0 += (Vector512.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                a1 += (Vector512.LoadUnsafe(in src, i + step).AsUInt64() & absMask).AsDouble();
            }
            Vector512<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                acc += (Vector512.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
            return Vector512.Sum(acc) + ScalarTailAbsSumD(ref src, i, length);
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count * 2)
        {
            Vector256<ulong> absMask = Vector256.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector256<double> a0 = Vector256<double>.Zero, a1 = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                a0 += (Vector256.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                a1 += (Vector256.LoadUnsafe(in src, i + step).AsUInt64() & absMask).AsDouble();
            }
            Vector256<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                acc += (Vector256.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
            return Vector256.Sum(acc) + ScalarTailAbsSumD(ref src, i, length);
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count * 2)
        {
            Vector128<ulong> absMask = Vector128.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector128<double> a0 = Vector128<double>.Zero, a1 = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - (step * 2) + 1;
            for (; i < limit; i += step * 2)
            {
                a0 += (Vector128.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                a1 += (Vector128.LoadUnsafe(in src, i + step).AsUInt64() & absMask).AsDouble();
            }
            Vector128<double> acc = a0 + a1;
            nuint singleLimit = length - step + 1;
            for (; i < singleLimit; i += step)
                acc += (Vector128.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
            return Vector128.Sum(acc) + ScalarTailAbsSumD(ref src, i, length);
        }
        return ScalarTailAbsSumD(ref src, 0, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ComputeLinfNorm(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0.0;

        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;

        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<ulong> absMask = Vector512.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector512<double> vMax = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<double> abs = (Vector512.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                vMax = Vector512.Max(vMax, abs);
            }
            double result = 0.0;
            for (int lane = 0; lane < Vector512<double>.Count; ++lane) result = double.Max(result, vMax.GetElement(lane));
            for (; i < length; ++i) result = double.Max(result, Math.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<ulong> absMask = Vector256.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector256<double> vMax = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> abs = (Vector256.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                vMax = Vector256.Max(vMax, abs);
            }
            double result = 0.0;
            for (int lane = 0; lane < Vector256<double>.Count; ++lane) result = double.Max(result, vMax.GetElement(lane));
            for (; i < length; ++i) result = double.Max(result, Math.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<ulong> absMask = Vector128.Create(0x7FFFFFFFFFFFFFFFUL);
            Vector128<double> vMax = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count;
            nuint limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<double> abs = (Vector128.LoadUnsafe(in src, i).AsUInt64() & absMask).AsDouble();
                vMax = Vector128.Max(vMax, abs);
            }
            double result = 0.0;
            for (int lane = 0; lane < Vector128<double>.Count; ++lane) result = double.Max(result, vMax.GetElement(lane));
            for (; i < length; ++i) result = double.Max(result, Math.Abs(Unsafe.Add(ref src, (nint)i)));
            return result;
        }

        double sMax = 0.0;
        for (; i < length; ++i) sMax = double.Max(sMax, Math.Abs(Unsafe.Add(ref src, (nint)i)));
        return sMax;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ScalarTailSumD(ref double src, nuint start, nuint length)
    {
        double s0 = 0.0, s1 = 0.0;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;
        for (; i < limit; i += 2) { s0 += Unsafe.Add(ref src, (nint)(i + 0)); s1 += Unsafe.Add(ref src, (nint)(i + 1)); }
        double acc = s0 + s1;
        for (; i < length; ++i) acc += Unsafe.Add(ref src, (nint)i);
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ScalarTailSquareSumD(ref double src, nuint start, nuint length)
    {
        double s0 = 0.0, s1 = 0.0;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;
        for (; i < limit; i += 2)
        {
            double v0 = Unsafe.Add(ref src, (nint)(i + 0)); double v1 = Unsafe.Add(ref src, (nint)(i + 1));
            s0 += v0 * v0; s1 += v1 * v1;
        }
        double acc = s0 + s1;
        for (; i < length; ++i) { double v = Unsafe.Add(ref src, (nint)i); acc += v * v; }
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double ScalarTailAbsSumD(ref double src, nuint start, nuint length)
    {
        double s0 = 0.0, s1 = 0.0;
        nuint i = start;
        nuint limit = length >= 2 ? length - 1 : 0;
        for (; i < limit; i += 2) { s0 += Math.Abs(Unsafe.Add(ref src, (nint)(i + 0))); s1 += Math.Abs(Unsafe.Add(ref src, (nint)(i + 1))); }
        double acc = s0 + s1;
        for (; i < length; ++i) acc += Math.Abs(Unsafe.Add(ref src, (nint)i));
        return acc;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstLessThan(ReadOnlySpan<double> source, double threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;
        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vT = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vT));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vT = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vT));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vT = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vT));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) < threshold) return (int)i;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int FindFirstEqual(ReadOnlySpan<double> source, double value)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return -1;
        ref double src = ref MemoryMarshal.GetReference(source);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vV = Vector512.Create(value);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.Equal(Vector512.LoadUnsafe(in src, i), vV));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vV = Vector256.Create(value);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.Equal(Vector256.LoadUnsafe(in src, i), vV));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vV = Vector128.Create(value);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.Equal(Vector128.LoadUnsafe(in src, i), vV));
                if (bits != 0) return (int)(i + (nuint)BitOperations.TrailingZeroCount(bits));
            }
        }
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) == value) return (int)i;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountLessThan(ReadOnlySpan<double> source, double threshold)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;
        ref double src = ref MemoryMarshal.GetReference(source);
        nuint count = 0, i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vT = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount((uint)Vector512.ExtractMostSignificantBits(Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vT)));
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vT = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount(Vector256.ExtractMostSignificantBits(Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vT)));
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vT = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount(Vector128.ExtractMostSignificantBits(Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vT)));
        }
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) < threshold) ++count;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint CountEqual(ReadOnlySpan<double> source, double value)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) return 0;
        ref double src = ref MemoryMarshal.GetReference(source);
        nuint count = 0, i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vV = Vector512.Create(value);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount((uint)Vector512.ExtractMostSignificantBits(Vector512.Equal(Vector512.LoadUnsafe(in src, i), vV)));
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vV = Vector256.Create(value);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount(Vector256.ExtractMostSignificantBits(Vector256.Equal(Vector256.LoadUnsafe(in src, i), vV)));
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vV = Vector128.Create(value);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                count += (nuint)uint.PopCount(Vector128.ExtractMostSignificantBits(Vector128.Equal(Vector128.LoadUnsafe(in src, i), vV)));
        }
        for (; i < length; ++i) if (Unsafe.Add(ref src, (nint)i) == value) ++count;
        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterLessThan(ReadOnlySpan<double> source, double threshold, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return ScanResult.Success(0);
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint dstCap = (nuint)destination.Length, written = 0, i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vT = Vector512.Create(threshold);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vT));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.LessThan(Vector512.LoadUnsafe(in src, i), vT));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vT = Vector256.Create(threshold);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vT));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.LessThan(Vector256.LoadUnsafe(in src, i), vT));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vT = Vector128.Create(threshold);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vT));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.LessThan(Vector128.LoadUnsafe(in src, i), vT));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        for (; i < length; ++i)
        {
            double val = Unsafe.Add(ref src, (nint)i);
            if (val < threshold) { if (written >= dstCap) return ScanResult.Overflow(written); Unsafe.Add(ref dst, (nint)written++) = val; }
        }
        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static ScanResult FilterEqual(ReadOnlySpan<double> source, double value, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return ScanResult.Success(0);
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint dstCap = (nuint)destination.Length, written = 0, i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vV = Vector512.Create(value);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.Equal(Vector512.LoadUnsafe(in src, i), vV));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                ulong bits = Vector512.ExtractMostSignificantBits(Vector512.Equal(Vector512.LoadUnsafe(in src, i), vV));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vV = Vector256.Create(value);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.Equal(Vector256.LoadUnsafe(in src, i), vV));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                uint bits = Vector256.ExtractMostSignificantBits(Vector256.Equal(Vector256.LoadUnsafe(in src, i), vV));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vV = Vector128.Create(value);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            nuint safeLimit = i + ((dstCap - written) / step) * step;
            if (safeLimit > limit) safeLimit = limit;
            for (; i < safeLimit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.Equal(Vector128.LoadUnsafe(in src, i), vV));
                while (bits != 0) { int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
            for (; i < limit; i += step)
            {
                uint bits = Vector128.ExtractMostSignificantBits(Vector128.Equal(Vector128.LoadUnsafe(in src, i), vV));
                while (bits != 0) { if (written >= dstCap) return ScanResult.Overflow(written); int lane = BitOperations.TrailingZeroCount(bits); Unsafe.Add(ref dst, (nint)written++) = Unsafe.Add(ref src, (nint)(i + (nuint)lane)); bits &= bits - 1; }
            }
        }
        for (; i < length; ++i)
        {
            double val = Unsafe.Add(ref src, (nint)i);
            if (val == value) { if (written >= dstCap) return ScanResult.Overflow(written); Unsafe.Add(ref dst, (nint)written++) = val; }
        }
        return ScanResult.Success(written);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMin(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();
        ref double src = ref MemoryMarshal.GetReference(source);
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            Vector512<double> vMin = Vector512.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMin = Vector512.Min(vMin, Vector512.LoadUnsafe(in src, j));
            double minVal = double.PositiveInfinity;
            for (int lane = 0; lane < Vector512<double>.Count; ++lane) minVal = double.Min(minVal, vMin.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == minVal) return j;
            return 0;
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            Vector256<double> vMin = Vector256.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMin = Vector256.Min(vMin, Vector256.LoadUnsafe(in src, j));
            double minVal = double.PositiveInfinity;
            for (int lane = 0; lane < Vector256<double>.Count; ++lane) minVal = double.Min(minVal, vMin.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == minVal) return j;
            return 0;
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            Vector128<double> vMin = Vector128.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMin = Vector128.Min(vMin, Vector128.LoadUnsafe(in src, j));
            double minVal = double.PositiveInfinity;
            for (int lane = 0; lane < Vector128<double>.Count; ++lane) minVal = double.Min(minVal, vMin.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == minVal) return j;
            return 0;
        }
        double sMin = Unsafe.Add(ref src, 0); nuint idx = 0;
        for (nuint j = 1; j < length; ++j) { double v = Unsafe.Add(ref src, (nint)j); if (v < sMin) { sMin = v; idx = j; } }
        return idx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static nuint ComputeArgMax(ReadOnlySpan<double> source)
    {
        nuint length = (nuint)source.Length;
        if (length == 0) ThrowHelper.ThrowEmptySequence();
        ref double src = ref MemoryMarshal.GetReference(source);
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            Vector512<double> vMax = Vector512.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMax = Vector512.Max(vMax, Vector512.LoadUnsafe(in src, j));
            double maxVal = double.NegativeInfinity;
            for (int lane = 0; lane < Vector512<double>.Count; ++lane) maxVal = double.Max(maxVal, vMax.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == maxVal) return j;
            return 0;
        }
        if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            Vector256<double> vMax = Vector256.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMax = Vector256.Max(vMax, Vector256.LoadUnsafe(in src, j));
            double maxVal = double.NegativeInfinity;
            for (int lane = 0; lane < Vector256<double>.Count; ++lane) maxVal = double.Max(maxVal, vMax.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == maxVal) return j;
            return 0;
        }
        if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            Vector128<double> vMax = Vector128.LoadUnsafe(in src, 0);
            for (nuint j = step; j < limit; j += step) vMax = Vector128.Max(vMax, Vector128.LoadUnsafe(in src, j));
            double maxVal = double.NegativeInfinity;
            for (int lane = 0; lane < Vector128<double>.Count; ++lane) maxVal = double.Max(maxVal, vMax.GetElement(lane));
            for (nuint j = 0; j < length; ++j) if (Unsafe.Add(ref src, (nint)j) == maxVal) return j;
            return 0;
        }
        double sMax = Unsafe.Add(ref src, 0); nuint idx = 0;
        for (nuint j = 1; j < length; ++j) { double v = Unsafe.Add(ref src, (nint)j); if (v > sMax) { sMax = v; idx = j; } }
        return idx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<double> condition, ReadOnlySpan<double> trueValues, ReadOnlySpan<double> falseValues, Span<double> destination)
    {
        if (condition.Length != trueValues.Length || condition.Length != falseValues.Length || destination.Length < condition.Length)
            ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)condition.Length;
        if (length == 0) return;
        ref double cRef = ref MemoryMarshal.GetReference(condition);
        ref double tRef = ref MemoryMarshal.GetReference(trueValues);
        ref double fRef = ref MemoryMarshal.GetReference(falseValues);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.ConditionalSelect(Vector512.LoadUnsafe(in cRef, i), Vector512.LoadUnsafe(in tRef, i), Vector512.LoadUnsafe(in fRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.ConditionalSelect(Vector256.LoadUnsafe(in cRef, i), Vector256.LoadUnsafe(in tRef, i), Vector256.LoadUnsafe(in fRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.ConditionalSelect(Vector128.LoadUnsafe(in cRef, i), Vector128.LoadUnsafe(in tRef, i), Vector128.LoadUnsafe(in fRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref cRef, (nint)i) != 0.0 ? Unsafe.Add(ref tRef, (nint)i) : Unsafe.Add(ref fRef, (nint)i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Select(ReadOnlySpan<double> condition, double trueValue, double falseValue, Span<double> destination)
    {
        if (destination.Length < condition.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)condition.Length;
        if (length == 0) return;
        ref double cRef = ref MemoryMarshal.GetReference(condition);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vT = Vector512.Create(trueValue), vF = Vector512.Create(falseValue);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.ConditionalSelect(Vector512.LoadUnsafe(in cRef, i), vT, vF).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vT = Vector256.Create(trueValue), vF = Vector256.Create(falseValue);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.ConditionalSelect(Vector256.LoadUnsafe(in cRef, i), vT, vF).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vT = Vector128.Create(trueValue), vF = Vector128.Create(falseValue);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.ConditionalSelect(Vector128.LoadUnsafe(in cRef, i), vT, vF).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref cRef, (nint)i) != 0.0 ? trueValue : falseValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThan(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.LessThan(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.LessThan(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.LessThan(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) < Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThan(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.GreaterThan(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.GreaterThan(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.GreaterThan(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) > Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.Equal(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.Equal(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.Equal(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) == Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareNotEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.NotEqual(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.NotEqual(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.NotEqual(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) != Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareLessThanOrEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.LessThanOrEqual(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.LessThanOrEqual(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.LessThanOrEqual(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) <= Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CompareGreaterThanOrEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length != right.Length || destination.Length < left.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)left.Length;
        if (length == 0) return;
        ref double lRef = ref MemoryMarshal.GetReference(left);
        ref double rRef = ref MemoryMarshal.GetReference(right);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector512.GreaterThanOrEqual(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector256.GreaterThanOrEqual(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                Vector128.GreaterThanOrEqual(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).AsDouble().StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Unsafe.Add(ref lRef, (nint)i) >= Unsafe.Add(ref rRef, (nint)i) ? BitConverter.Int64BitsToDouble(-1L) : 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Fill(Span<double> destination, double value)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> v = Vector512.Create(value);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> v = Vector256.Create(value);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> v = Vector128.Create(value);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void FillLinear(Span<double> destination, double start, double step)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vStart = Vector512.Create(start);
            Vector512<double> indices = Vector512.Create(0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0);
            Vector512<double> vStep = Vector512.Create(step);
            nuint vc = (nuint)Vector512<double>.Count;
            Vector512<double> vInc = Vector512.Create((double)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc) { (vStart + indices * vStep).StoreUnsafe(ref dRef, i); vStart += vInc; }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vStart = Vector256.Create(start);
            Vector256<double> indices = Vector256.Create(0.0, 1.0, 2.0, 3.0);
            Vector256<double> vStep = Vector256.Create(step);
            nuint vc = (nuint)Vector256<double>.Count;
            Vector256<double> vInc = Vector256.Create((double)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc) { (vStart + indices * vStep).StoreUnsafe(ref dRef, i); vStart += vInc; }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vStart = Vector128.Create(start);
            Vector128<double> indices = Vector128.Create(0.0, 1.0);
            Vector128<double> vStep = Vector128.Create(step);
            nuint vc = (nuint)Vector128<double>.Count;
            Vector128<double> vInc = Vector128.Create((double)vc * step);
            nuint vLimit = (length / vc) * vc;
            for (; i < vLimit; i += vc) { (vStart + indices * vStep).StoreUnsafe(ref dRef, i); vStart += vInc; }
        }
        double val = start + (double)i * step;
        for (; i < length; ++i) { Unsafe.Add(ref dRef, (nint)i) = val; val += step; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Gather(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
    {
        if (destination.Length < indices.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && count >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector256.Gather(in src, Vector256.LoadUnsafe(in idx, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && count >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector128.Gather(in src, Vector128.LoadUnsafe(in idx, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < count; ++i)
            Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, Unsafe.Add(ref idx, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Scatter(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
    {
        if (source.Length > destination.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector256.IsHardwareAccelerated && count >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector256.LoadUnsafe(in src, i).Scatter(ref dst, Vector256.LoadUnsafe(in idx, i));
        }
        else if (Vector128.IsHardwareAccelerated && count >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector128.LoadUnsafe(in src, i).Scatter(ref dst, Vector128.LoadUnsafe(in idx, i));
        }
        for (; i < count; ++i)
            Unsafe.Add(ref dst, Unsafe.Add(ref idx, (nint)i)) = Unsafe.Add(ref src, (nint)i);
    }

    private static Vector512<double> ExpKernel512(Vector512<double> x)
    {
        const double LOG2E = 1.4426950408889634;
        const double LN2 = 0.6931471805599453;
        Vector512<double> clamped = Vector512.Max(Vector512.Min(x, Vector512.Create(709.0)), Vector512.Create(-709.0));
        Vector512<double> t = clamped * Vector512.Create(LOG2E);
        Vector512<long> k = Vector512.Floor(t).AsInt64() - Vector512.Create(1L);
        Vector512<double> f = t - Vector512.ConvertToDouble(k);
        Vector512<long> biased = k + Vector512.Create(1023L);
        Vector512<double> pow2 = Vector512.ShiftLeft(biased, 52).AsDouble();
        Vector512<double> u = f * Vector512.Create(LN2);
        Vector512<double> poly = Vector512.Create(0.00833333333333333);
        poly = poly * u + Vector512.Create(0.0416666666666667);
        poly = poly * u + Vector512.Create(0.166666666666667);
        poly = poly * u + Vector512.Create(0.5);
        poly = poly * u + Vector512.Create(1.0);
        return pow2 * (poly * u + Vector512.Create(1.0));
    }

    private static double ExpScalar(double x)
    {
        x = Math.Max(-709.0, Math.Min(709.0, x));
        double t = x * 1.4426950408889634;
        long k = (long)Math.Floor(t) - 1;
        double f = t - k;
        double u = f * 0.6931471805599453;
        double poly = ((((0.00833333333333333 * u + 0.0416666666666667) * u + 0.166666666666667) * u + 0.5) * u + 1.0) * u + 1.0;
        return BitConverter.Int64BitsToDouble((Math.Max(-1022L, Math.Min(1023L, k)) + 1023L) << 52) * poly;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorExp(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) ExpKernel512(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vLog2E = Vector256.Create(1.4426950408889634), vLn2 = Vector256.Create(0.6931471805599453), vClamp = Vector256.Create(709.0);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> x = Vector256.Max(Vector256.Min(Vector256.LoadUnsafe(in src, i), vClamp), -vClamp);
                Vector256<double> tt = x * vLog2E;
                Vector256<long> k2 = Vector256.Floor(tt).AsInt64() - Vector256.Create(1L);
                Vector256<double> f2 = tt - Vector256.ConvertToDouble(k2);
                Vector256<double> p2 = Vector256.ShiftLeft(k2 + Vector256.Create(1023L), 52).AsDouble();
                Vector256<double> u2 = f2 * vLn2;
                Vector256<double> q2 = Vector256.Create(0.00833333333333333);
                q2 = q2 * u2 + Vector256.Create(0.0416666666666667); q2 = q2 * u2 + Vector256.Create(0.166666666666667);
                q2 = q2 * u2 + Vector256.Create(0.5); q2 = q2 * u2 + Vector256.Create(1.0);
                (p2 * (q2 * u2 + Vector256.Create(1.0))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vLog2E = Vector128.Create(1.4426950408889634), vLn2 = Vector128.Create(0.6931471805599453), vClamp = Vector128.Create(709.0);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector128<double> x = Vector128.Max(Vector128.Min(Vector128.LoadUnsafe(in src, i), vClamp), -vClamp);
                Vector128<double> tt = x * vLog2E;
                Vector128<long> k2 = Vector128.Floor(tt).AsInt64() - Vector128.Create(1L);
                Vector128<double> f2 = tt - Vector128.ConvertToDouble(k2);
                Vector128<double> p2 = Vector128.ShiftLeft(k2 + Vector128.Create(1023L), 52).AsDouble();
                Vector128<double> u2 = f2 * vLn2;
                Vector128<double> q2 = Vector128.Create(0.00833333333333333);
                q2 = q2 * u2 + Vector128.Create(0.0416666666666667); q2 = q2 * u2 + Vector128.Create(0.166666666666667);
                q2 = q2 * u2 + Vector128.Create(0.5); q2 = q2 * u2 + Vector128.Create(1.0);
                (p2 * (q2 * u2 + Vector128.Create(1.0))).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = ExpScalar(Unsafe.Add(ref src, (nint)i));
    }

    private static Vector512<double> LogKernel512(Vector512<double> x)
    {
        Vector512<double> v = x, n = Vector512<double>.Zero;
        Vector512<double> one = Vector512.Create(1.0), two = Vector512.Create(2.0), half = Vector512.Create(0.5);
        for (int iter = 0; iter < 1200; ++iter)
        {
            Vector512<double> g = Vector512.GreaterThanOrEqual(v, two);
            if (Vector512.Equals(g, Vector512<long>.Zero)) break;
            v = Vector512.ConditionalSelect(g, v * half, v);
            n += Vector512.ConditionalSelect(g, one, Vector512<double>.Zero);
        }
        for (int iter = 0; iter < 1200; ++iter)
        {
            Vector512<double> l = Vector512.LessThan(v, one);
            if (Vector512.Equals(l, Vector512<long>.Zero)) break;
            v = Vector512.ConditionalSelect(l, v * two, v);
            n -= Vector512.ConditionalSelect(l, one, Vector512<double>.Zero);
        }
        Vector512<double> u = v - one;
        Vector512<double> poly = Vector512.Create(-0.020835085);
        poly = poly * u + Vector512.Create(0.0277272808); poly = poly * u + Vector512.Create(-0.0397820075);
        poly = poly * u + Vector512.Create(0.0667107478); poly = poly * u + Vector512.Create(-0.117496403);
        poly = poly * u + Vector512.Create(0.333331568);
        return n * Vector512.Create(0.6931471805599453) + (poly * u + one) * u;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorLog(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) LogKernel512(Vector512.LoadUnsafe(in src, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> v = Vector256.LoadUnsafe(in src, i), nn = Vector256<double>.Zero;
                Vector256<double> one = Vector256.Create(1.0), two = Vector256.Create(2.0), half = Vector256.Create(0.5);
                for (int it = 0; it < 1200; ++it) { var g = Vector256.GreaterThanOrEqual(v, two); if (Vector256.Equals(g, Vector256<long>.Zero)) break; v = Vector256.ConditionalSelect(g, v * half, v); nn += Vector256.ConditionalSelect(g, one, Vector256<double>.Zero); }
                for (int it = 0; it < 1200; ++it) { var l = Vector256.LessThan(v, one); if (Vector256.Equals(l, Vector256<long>.Zero)) break; v = Vector256.ConditionalSelect(l, v * two, v); nn -= Vector256.ConditionalSelect(l, one, Vector256<double>.Zero); }
                Vector256<double> u2 = v - one;
                Vector256<double> q2 = Vector256.Create(-0.020835085);
                q2 = q2 * u2 + Vector256.Create(0.0277272808); q2 = q2 * u2 + Vector256.Create(-0.0397820075);
                q2 = q2 * u2 + Vector256.Create(0.0667107478); q2 = q2 * u2 + Vector256.Create(-0.117496403);
                q2 = q2 * u2 + Vector256.Create(0.333331568);
                (nn * Vector256.Create(0.6931471805599453) + (q2 * u2 + one) * u2).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            double x = Unsafe.Add(ref src, (nint)i);
            if (x <= 0) { Unsafe.Add(ref dst, (nint)i) = double.NegativeInfinity; continue; }
            double v = x, nn = 0;
            for (int it = 0; it < 1200 && v >= 2; ++it) { v *= 0.5; nn += 1; }
            for (int it = 0; it < 1200 && v < 1; ++it) { v *= 2; nn -= 1; }
            double u = v - 1;
            double poly = (((((-0.020835085 * u + 0.0277272808) * u - 0.0397820075) * u + 0.0667107478) * u - 0.117496403) * u + 0.333331568) * u + 1.0;
            Unsafe.Add(ref dst, (nint)i) = nn * 0.6931471805599453 + poly * u;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorSigmoid(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<double> e = ExpKernel512(Vector512.Create(0.0) - Vector512.LoadUnsafe(in src, i));
                (Vector512.Create(1.0) / (Vector512.Create(1.0) + e)).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> neg = Vector256.Create(0.0) - Vector256.LoadUnsafe(in src, i);
                Vector256<double> clamped = Vector256.Max(Vector256.Min(neg, Vector256.Create(709.0)), Vector256.Create(-709.0));
                Vector256<double> t = clamped * Vector256.Create(1.4426950408889634);
                Vector256<long> k = Vector256.Floor(t).AsInt64() - Vector256.Create(1L);
                Vector256<double> p2 = Vector256.ShiftLeft(k + Vector256.Create(1023L), 52).AsDouble();
                Vector256<double> u = (t - Vector256.ConvertToDouble(k)) * Vector256.Create(0.6931471805599453);
                Vector256<double> poly = Vector256.Create(0.00833333333333333);
                poly = poly * u + Vector256.Create(0.0416666666666667); poly = poly * u + Vector256.Create(0.166666666666667);
                poly = poly * u + Vector256.Create(0.5); poly = poly * u + Vector256.Create(1.0);
                Vector256<double> e = p2 * (poly * u + Vector256.Create(1.0));
                (Vector256.Create(1.0) / (Vector256.Create(1.0) + e)).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dst, (nint)i) = 1.0 / (1.0 + Math.Exp(-Unsafe.Add(ref src, (nint)i)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorTanh(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector512<double> x = Vector512.Max(Vector512.Min(Vector512.LoadUnsafe(in src, i), Vector512.Create(19.0)), Vector512.Create(-19.0));
                Vector512<double> p = ExpKernel512(x * Vector512.Create(2.0));
                ((p - Vector512.Create(1.0)) / (p + Vector512.Create(1.0))).StoreUnsafe(ref dst, i);
            }
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
            {
                Vector256<double> x = Vector256.Max(Vector256.Min(Vector256.LoadUnsafe(in src, i), Vector256.Create(19.0)), Vector256.Create(-19.0));
                Vector256<double> cx = x * Vector256.Create(1.4426950408889634 * 2.0);
                Vector256<long> k = Vector256.Floor(cx).AsInt64() - Vector256.Create(1L);
                Vector256<double> p2 = Vector256.ShiftLeft(k + Vector256.Create(1023L), 52).AsDouble();
                Vector256<double> u = (cx - Vector256.ConvertToDouble(k)) * Vector256.Create(0.6931471805599453);
                Vector256<double> poly = Vector256.Create(0.00833333333333333);
                poly = poly * u + Vector256.Create(0.0416666666666667); poly = poly * u + Vector256.Create(0.166666666666667);
                poly = poly * u + Vector256.Create(0.5); poly = poly * u + Vector256.Create(1.0);
                Vector256<double> p = p2 * (poly * u + Vector256.Create(1.0));
                ((p - Vector256.Create(1.0)) / (p + Vector256.Create(1.0))).StoreUnsafe(ref dst, i);
            }
        }
        for (; i < length; ++i)
        {
            double x = Unsafe.Add(ref src, (nint)i);
            double e2x = Math.Exp(2.0 * Math.Max(-19.0, Math.Min(19.0, x)));
            Unsafe.Add(ref dst, (nint)i) = (e2x - 1.0) / (e2x + 1.0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorReLU(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> zero = Vector512<double>.Zero;
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector512.Max(Vector512.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> zero = Vector256<double>.Zero;
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector256.Max(Vector256.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> zero = Vector128<double>.Zero;
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector128.Max(Vector128.LoadUnsafe(in src, i), zero).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) { double v = Unsafe.Add(ref src, (nint)i); Unsafe.Add(ref dst, (nint)i) = v > 0 ? v : 0; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorRSqrt(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.Create(1.0) / Vector512.Sqrt(Vector512.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.Create(1.0) / Vector256.Sqrt(Vector256.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.Create(1.0) / Vector128.Sqrt(Vector128.LoadUnsafe(in src, i))).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) = 1.0 / Math.Sqrt(Unsafe.Add(ref src, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void VectorPow(ReadOnlySpan<double> @base, ReadOnlySpan<double> exponent, Span<double> destination)
    {
        if (@base.Length != exponent.Length || destination.Length < @base.Length) ThrowHelper.ThrowMismatchedSpans();
        nuint length = (nuint)@base.Length;
        if (length == 0) return;
        ref double bRef = ref MemoryMarshal.GetReference(@base);
        ref double eRef = ref MemoryMarshal.GetReference(exponent);
        ref double dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step)
                ExpKernel512(Vector512.LoadUnsafe(in eRef, i) * LogKernel512(Vector512.LoadUnsafe(in bRef, i))).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
            Unsafe.Add(ref dRef, (nint)i) = Math.Pow(Unsafe.Add(ref bRef, (nint)i), Unsafe.Add(ref eRef, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Softmax(ReadOnlySpan<double> source, Span<double> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        double maxVal = double.NegativeInfinity;
        for (nuint j = 0; j < length; ++j) { double v = Unsafe.Add(ref src, (nint)j); if (v > maxVal) maxVal = v; }
        double sum = 0;
        for (nuint j = 0; j < length; ++j) { double e = Math.Exp(Unsafe.Add(ref src, (nint)j) - maxVal); Unsafe.Add(ref dst, (nint)j) = e; sum += e; }
        double inv = 1.0 / sum;
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<double>.Count)
        {
            Vector512<double> vInv = Vector512.Create(inv);
            nuint step = (nuint)Vector512<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector512.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<double>.Count)
        {
            Vector256<double> vInv = Vector256.Create(inv);
            nuint step = (nuint)Vector256<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector256.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<double>.Count)
        {
            Vector128<double> vInv = Vector128.Create(inv);
            nuint step = (nuint)Vector128<double>.Count, limit = length - step + 1;
            for (; i < limit; i += step) (Vector128.LoadUnsafe(in dst, i) * vInv).StoreUnsafe(ref dst, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dst, (nint)i) *= inv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Mat4x4Multiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length < 16 || right.Length < 16 || destination.Length < 16) ThrowHelper.ThrowMismatchedSpans();
        ref double l = ref MemoryMarshal.GetReference(left);
        ref double r = ref MemoryMarshal.GetReference(right);
        ref double d = ref MemoryMarshal.GetReference(destination);
        if (Vector128.IsHardwareAccelerated)
        {
            for (int col = 0; col < 4; ++col)
            {
                Vector128<double> r0 = Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 0))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4));
                r0 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 1))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 2));
                r0 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 2))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 4));
                r0 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 3))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 6));
                r0.StoreUnsafe(ref d, (nuint)(col * 2));
                Vector128<double> r1 = Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 0))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 1));
                r1 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 1))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 3));
                r1 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 2))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 5));
                r1 += Vector128.Create(Unsafe.Add(ref l, (nint)(col * 4 + 3))) * Vector128.LoadUnsafe(in r, (nuint)(col * 4 + 7));
                r1.StoreUnsafe(ref d, (nuint)(col * 2 + 1));
            }
        }
        else
        {
            for (int col = 0; col < 4; ++col)
                for (int row = 0; row < 4; ++row)
                {
                    double sum = 0;
                    for (int k = 0; k < 4; ++k) sum += Unsafe.Add(ref l, (nint)(k * 4 + col)) * Unsafe.Add(ref r, (nint)(row + k * 4));
                    Unsafe.Add(ref d, (nint)(row + col * 4)) = sum;
                }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void CrossProduct(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length < 3 || right.Length < 3 || destination.Length < 3) ThrowHelper.ThrowMismatchedSpans();
        ref double l = ref MemoryMarshal.GetReference(left);
        ref double r = ref MemoryMarshal.GetReference(right);
        double lx = Unsafe.Add(ref l, 0), ly = Unsafe.Add(ref l, 1), lz = Unsafe.Add(ref l, 2);
        double rx = Unsafe.Add(ref r, 0), ry = Unsafe.Add(ref r, 1), rz = Unsafe.Add(ref r, 2);
        ref double d = ref MemoryMarshal.GetReference(destination);
        Unsafe.Add(ref d, 0) = ly * rz - lz * ry;
        Unsafe.Add(ref d, 1) = lz * rx - lx * rz;
        Unsafe.Add(ref d, 2) = lx * ry - ly * rx;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionMultiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        if (left.Length < 4 || right.Length < 4 || destination.Length < 4) ThrowHelper.ThrowMismatchedSpans();
        ref double l = ref MemoryMarshal.GetReference(left);
        ref double r = ref MemoryMarshal.GetReference(right);
        double lx = Unsafe.Add(ref l, 0), ly = Unsafe.Add(ref l, 1), lz = Unsafe.Add(ref l, 2), lw = Unsafe.Add(ref l, 3);
        double rx = Unsafe.Add(ref r, 0), ry = Unsafe.Add(ref r, 1), rz = Unsafe.Add(ref r, 2), rw = Unsafe.Add(ref r, 3);
        ref double d = ref MemoryMarshal.GetReference(destination);
        Unsafe.Add(ref d, 0) = lw * rx + lx * rw + ly * rz - lz * ry;
        Unsafe.Add(ref d, 1) = lw * ry - lx * rz + ly * rw + lz * rx;
        Unsafe.Add(ref d, 2) = lw * rz + lx * ry - ly * rx + lz * rw;
        Unsafe.Add(ref d, 3) = lw * rw - lx * rx - ly * ry - lz * rz;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void QuaternionSlerp(ReadOnlySpan<double> from, ReadOnlySpan<double> to, double t, Span<double> destination)
    {
        if (from.Length < 4 || to.Length < 4 || destination.Length < 4) ThrowHelper.ThrowMismatchedSpans();
        ref double a = ref MemoryMarshal.GetReference(from);
        ref double b = ref MemoryMarshal.GetReference(to);
        double dot = Unsafe.Add(ref a, 0) * Unsafe.Add(ref b, 0) + Unsafe.Add(ref a, 1) * Unsafe.Add(ref b, 1)
                   + Unsafe.Add(ref a, 2) * Unsafe.Add(ref b, 2) + Unsafe.Add(ref a, 3) * Unsafe.Add(ref b, 3);
        double bx = Unsafe.Add(ref b, 0), by = Unsafe.Add(ref b, 1), bz = Unsafe.Add(ref b, 2), bw = Unsafe.Add(ref b, 3);
        if (dot < 0) { dot = -dot; bx = -bx; by = -by; bz = -bz; bw = -bw; }
        ref double d = ref MemoryMarshal.GetReference(destination);
        if (dot > 0.9995)
        {
            Unsafe.Add(ref d, 0) = Unsafe.Add(ref a, 0) + t * (bx - Unsafe.Add(ref a, 0));
            Unsafe.Add(ref d, 1) = Unsafe.Add(ref a, 1) + t * (by - Unsafe.Add(ref a, 1));
            Unsafe.Add(ref d, 2) = Unsafe.Add(ref a, 2) + t * (bz - Unsafe.Add(ref a, 2));
            Unsafe.Add(ref d, 3) = Unsafe.Add(ref a, 3) + t * (bw - Unsafe.Add(ref a, 3));
            double len = Math.Sqrt(Unsafe.Add(ref d, 0) * Unsafe.Add(ref d, 0) + Unsafe.Add(ref d, 1) * Unsafe.Add(ref d, 1)
                                 + Unsafe.Add(ref d, 2) * Unsafe.Add(ref d, 2) + Unsafe.Add(ref d, 3) * Unsafe.Add(ref d, 3));
            double inv = 1.0 / len;
            Unsafe.Add(ref d, 0) *= inv; Unsafe.Add(ref d, 1) *= inv; Unsafe.Add(ref d, 2) *= inv; Unsafe.Add(ref d, 3) *= inv;
        }
        else
        {
            double theta = Math.Acos(Math.Clamp(dot, -1.0, 1.0));
            double sinTheta = Math.Sin(theta);
            double w0 = Math.Sin((1.0 - t) * theta) / sinTheta;
            double w1 = Math.Sin(t * theta) / sinTheta;
            Unsafe.Add(ref d, 0) = w0 * Unsafe.Add(ref a, 0) + w1 * bx;
            Unsafe.Add(ref d, 1) = w0 * Unsafe.Add(ref a, 1) + w1 * by;
            Unsafe.Add(ref d, 2) = w0 * Unsafe.Add(ref a, 2) + w1 * bz;
            Unsafe.Add(ref d, 3) = w0 * Unsafe.Add(ref a, 3) + w1 * bw;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Swizzle(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
    {
        if (destination.Length < indices.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref double src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref double dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector128.IsHardwareAccelerated && count >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<double>.Count, limit = count - step + 1;
            for (; i < limit; i += step)
                Vector128.Shuffle(Vector128.LoadUnsafe(in src, i), Vector128.LoadUnsafe(in idx, i).AsDouble()).StoreUnsafe(ref dst, i);
        }
        for (; i < count; ++i)
            Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, Unsafe.Add(ref idx, (nint)i));
    }
}

public sealed class Float32VectorEngine : IVectorTransformer<float>, IVectorReducer<float>, IVectorScanner<float>, IDisposable
{
    private readonly AlignedCounter _transformsExecuted = new();
    private readonly AlignedCounter _reductionsExecuted = new();

    public long TransformsExecuted => _transformsExecuted.Value;
    public long ReductionsExecuted => _reductionsExecuted.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void TransformLinear(ReadOnlySpan<float> source, float multiplier, float offset, Span<float> destination)
    {
        SimdFloat32.TransformLinear(source, multiplier, offset, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorAdd(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        SimdFloat32.VectorAdd(left, right, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorMultiply(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    {
        SimdFloat32.VectorMultiply(left, right, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorFma(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> destination)
    {
        SimdFloat32.VectorFma(a, b, c, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorClamp(ReadOnlySpan<float> source, float min, float max, Span<float> destination)
    {
        SimdFloat32.VectorClamp(source, min, max, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeSum(ReadOnlySpan<float> source)
    {
        float result = SimdFloat32.ComputeSum(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeDotProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        float result = SimdFloat32.ComputeDotProduct(left, right);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeL2Norm(ReadOnlySpan<float> source)
    {
        float result = SimdFloat32.ComputeL2Norm(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ExtremaPair<float> ComputeExtrema(ReadOnlySpan<float> source)
    {
        ExtremaPair<float> result = SimdFloat32.ComputeExtrema(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstGreaterThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloat32.FindFirstGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountGreaterThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloat32.CountGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterGreaterThan(ReadOnlySpan<float> source, float threshold, Span<float> destination)
        => SimdFloat32.FilterGreaterThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorSubtract(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    { SimdFloat32.VectorSubtract(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorNegate(ReadOnlySpan<float> source, Span<float> destination)
    { SimdFloat32.VectorNegate(source, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorDivide(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    { SimdFloat32.VectorDivide(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorAbs(ReadOnlySpan<float> source, Span<float> destination)
    { SimdFloat32.VectorAbs(source, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorFms(ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c, Span<float> destination)
    { SimdFloat32.VectorFms(a, b, c, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorLerp(ReadOnlySpan<float> a, ReadOnlySpan<float> b, float t, Span<float> destination)
    { SimdFloat32.VectorLerp(a, b, t, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ElementWiseMax(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    { SimdFloat32.ElementWiseMax(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ElementWiseMin(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
    { SimdFloat32.ElementWiseMin(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeMean(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeMean(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeVariance(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeVariance(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeStdDev(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeStdDev(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeSumOfSquares(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeSumOfSquares(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeProduct(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeProduct(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeL1Norm(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeL1Norm(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public float ComputeLinfNorm(ReadOnlySpan<float> source)
    { float r = SimdFloat32.ComputeLinfNorm(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstLessThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloat32.FindFirstLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstEqual(ReadOnlySpan<float> source, float value)
        => SimdFloat32.FindFirstEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountLessThan(ReadOnlySpan<float> source, float threshold)
        => SimdFloat32.CountLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountEqual(ReadOnlySpan<float> source, float value)
        => SimdFloat32.CountEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterLessThan(ReadOnlySpan<float> source, float threshold, Span<float> destination)
        => SimdFloat32.FilterLessThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterEqual(ReadOnlySpan<float> source, float value, Span<float> destination)
        => SimdFloat32.FilterEqual(source, value, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint ComputeArgMin(ReadOnlySpan<float> source)
        => SimdFloat32.ComputeArgMin(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint ComputeArgMax(ReadOnlySpan<float> source)
        => SimdFloat32.ComputeArgMax(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Select(ReadOnlySpan<float> condition, ReadOnlySpan<float> trueValues, ReadOnlySpan<float> falseValues, Span<float> destination)
        => SimdFloat32.Select(condition, trueValues, falseValues, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Select(ReadOnlySpan<float> condition, float trueValue, float falseValue, Span<float> destination)
        => SimdFloat32.Select(condition, trueValue, falseValue, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareLessThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareLessThan(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareGreaterThan(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareGreaterThan(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareNotEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareNotEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareLessThanOrEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareLessThanOrEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareGreaterThanOrEqual(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> destination)
        => SimdFloat32.CompareGreaterThanOrEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Fill(Span<float> destination, float value) => SimdFloat32.Fill(destination, value);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void FillLinear(Span<float> destination, float start, float step) => SimdFloat32.FillLinear(destination, start, step);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Gather(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
        => SimdFloat32.Gather(source, indices, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Scatter(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination)
        => SimdFloat32.Scatter(source, indices, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorExp(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorExp(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorLog(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorLog(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorSigmoid(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorSigmoid(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorTanh(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorTanh(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorReLU(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorReLU(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorRSqrt(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.VectorRSqrt(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorPow(ReadOnlySpan<float> b, ReadOnlySpan<float> e, Span<float> d) => SimdFloat32.VectorPow(b, e, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Softmax(ReadOnlySpan<float> source, Span<float> destination) => SimdFloat32.Softmax(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Mat4x4Multiply(ReadOnlySpan<float> l, ReadOnlySpan<float> r, Span<float> d) => SimdFloat32.Mat4x4Multiply(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CrossProduct(ReadOnlySpan<float> l, ReadOnlySpan<float> r, Span<float> d) => SimdFloat32.CrossProduct(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QuaternionMultiply(ReadOnlySpan<float> l, ReadOnlySpan<float> r, Span<float> d) => SimdFloat32.QuaternionMultiply(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QuaternionSlerp(ReadOnlySpan<float> f, ReadOnlySpan<float> t, float tVal, Span<float> d) => SimdFloat32.QuaternionSlerp(f, t, tVal, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Swizzle(ReadOnlySpan<float> source, ReadOnlySpan<int> indices, Span<float> destination) => SimdFloat32.Swizzle(source, indices, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Span<byte> PackFloatToHalf(ReadOnlySpan<float> source) => SimdFloat32.PackFloatToHalf(source);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void UnpackHalfToFloat(ReadOnlySpan<byte> source, Span<float> destination) => SimdFloat32.UnpackHalfToFloat(source, destination);

    public void Dispose()
    {
        _transformsExecuted.Dispose();
        _reductionsExecuted.Dispose();
    }
}

public sealed class Float64VectorEngine : IVectorTransformer<double>, IVectorReducer<double>, IVectorScanner<double>, IDisposable
{
    private readonly AlignedCounter _transformsExecuted = new();
    private readonly AlignedCounter _reductionsExecuted = new();

    public long TransformsExecuted => _transformsExecuted.Value;
    public long ReductionsExecuted => _reductionsExecuted.Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void TransformLinear(ReadOnlySpan<double> source, double multiplier, double offset, Span<double> destination)
    {
        SimdFloat64.TransformLinear(source, multiplier, offset, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorAdd(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        SimdFloat64.VectorAdd(left, right, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorMultiply(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    {
        SimdFloat64.VectorMultiply(left, right, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorFma(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> destination)
    {
        SimdFloat64.VectorFma(a, b, c, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorClamp(ReadOnlySpan<double> source, double min, double max, Span<double> destination)
    {
        SimdFloat64.VectorClamp(source, min, max, destination);
        _transformsExecuted.Increment();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeSum(ReadOnlySpan<double> source)
    {
        double result = SimdFloat64.ComputeSum(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeDotProduct(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
    {
        double result = SimdFloat64.ComputeDotProduct(left, right);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeL2Norm(ReadOnlySpan<double> source)
    {
        double result = SimdFloat64.ComputeL2Norm(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ExtremaPair<double> ComputeExtrema(ReadOnlySpan<double> source)
    {
        ExtremaPair<double> result = SimdFloat64.ComputeExtrema(source);
        _reductionsExecuted.Increment();
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstGreaterThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloat64.FindFirstGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountGreaterThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloat64.CountGreaterThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterGreaterThan(ReadOnlySpan<double> source, double threshold, Span<double> destination)
        => SimdFloat64.FilterGreaterThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorSubtract(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    { SimdFloat64.VectorSubtract(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorNegate(ReadOnlySpan<double> source, Span<double> destination)
    { SimdFloat64.VectorNegate(source, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorDivide(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    { SimdFloat64.VectorDivide(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorAbs(ReadOnlySpan<double> source, Span<double> destination)
    { SimdFloat64.VectorAbs(source, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorFms(ReadOnlySpan<double> a, ReadOnlySpan<double> b, ReadOnlySpan<double> c, Span<double> destination)
    { SimdFloat64.VectorFms(a, b, c, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorLerp(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double t, Span<double> destination)
    { SimdFloat64.VectorLerp(a, b, t, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ElementWiseMax(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    { SimdFloat64.ElementWiseMax(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void ElementWiseMin(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
    { SimdFloat64.ElementWiseMin(left, right, destination); _transformsExecuted.Increment(); }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeMean(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeMean(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeVariance(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeVariance(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeStdDev(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeStdDev(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeSumOfSquares(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeSumOfSquares(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeProduct(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeProduct(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeL1Norm(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeL1Norm(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public double ComputeLinfNorm(ReadOnlySpan<double> source)
    { double r = SimdFloat64.ComputeLinfNorm(source); _reductionsExecuted.Increment(); return r; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstLessThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloat64.FindFirstLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public int FindFirstEqual(ReadOnlySpan<double> source, double value)
        => SimdFloat64.FindFirstEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountLessThan(ReadOnlySpan<double> source, double threshold)
        => SimdFloat64.CountLessThan(source, threshold);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint CountEqual(ReadOnlySpan<double> source, double value)
        => SimdFloat64.CountEqual(source, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterLessThan(ReadOnlySpan<double> source, double threshold, Span<double> destination)
        => SimdFloat64.FilterLessThan(source, threshold, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ScanResult FilterEqual(ReadOnlySpan<double> source, double value, Span<double> destination)
        => SimdFloat64.FilterEqual(source, value, destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint ComputeArgMin(ReadOnlySpan<double> source)
        => SimdFloat64.ComputeArgMin(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public nuint ComputeArgMax(ReadOnlySpan<double> source)
        => SimdFloat64.ComputeArgMax(source);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Select(ReadOnlySpan<double> condition, ReadOnlySpan<double> trueValues, ReadOnlySpan<double> falseValues, Span<double> destination)
        => SimdFloat64.Select(condition, trueValues, falseValues, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Select(ReadOnlySpan<double> condition, double trueValue, double falseValue, Span<double> destination)
        => SimdFloat64.Select(condition, trueValue, falseValue, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareLessThan(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareLessThan(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareGreaterThan(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareGreaterThan(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareNotEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareNotEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareLessThanOrEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareLessThanOrEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CompareGreaterThanOrEqual(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> destination)
        => SimdFloat64.CompareGreaterThanOrEqual(left, right, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Fill(Span<double> destination, double value) => SimdFloat64.Fill(destination, value);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void FillLinear(Span<double> destination, double start, double step) => SimdFloat64.FillLinear(destination, start, step);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Gather(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
        => SimdFloat64.Gather(source, indices, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Scatter(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination)
        => SimdFloat64.Scatter(source, indices, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorExp(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorExp(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorLog(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorLog(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorSigmoid(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorSigmoid(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorTanh(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorTanh(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorReLU(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorReLU(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorRSqrt(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.VectorRSqrt(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void VectorPow(ReadOnlySpan<double> b, ReadOnlySpan<double> e, Span<double> d) => SimdFloat64.VectorPow(b, e, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Softmax(ReadOnlySpan<double> source, Span<double> destination) => SimdFloat64.Softmax(source, destination);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Mat4x4Multiply(ReadOnlySpan<double> l, ReadOnlySpan<double> r, Span<double> d) => SimdFloat64.Mat4x4Multiply(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void CrossProduct(ReadOnlySpan<double> l, ReadOnlySpan<double> r, Span<double> d) => SimdFloat64.CrossProduct(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QuaternionMultiply(ReadOnlySpan<double> l, ReadOnlySpan<double> r, Span<double> d) => SimdFloat64.QuaternionMultiply(l, r, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void QuaternionSlerp(ReadOnlySpan<double> f, ReadOnlySpan<double> t, double tVal, Span<double> d) => SimdFloat64.QuaternionSlerp(f, t, tVal, d);
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Swizzle(ReadOnlySpan<double> source, ReadOnlySpan<int> indices, Span<double> destination) => SimdFloat64.Swizzle(source, indices, destination);

    public void Dispose()
    {
        _transformsExecuted.Dispose();
        _reductionsExecuted.Dispose();
    }
}

[SkipLocalsInit]
public static unsafe class SimdInt32
{
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
            for (; i < singleLimit; i += step) (Vector512.LoadUnsafe(in lRef, i) - Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
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
            for (; i < singleLimit; i += step) (Vector256.LoadUnsafe(in lRef, i) - Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
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
            for (; i < singleLimit; i += step) (Vector128.LoadUnsafe(in lRef, i) - Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
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
            for (; i < singleLimit; i += step) (Vector512.LoadUnsafe(in lRef, i) * Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
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
            for (; i < singleLimit; i += step) (Vector256.LoadUnsafe(in lRef, i) * Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
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
            for (; i < singleLimit; i += step) (Vector128.LoadUnsafe(in lRef, i) * Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
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
    public static void BitwiseAnd(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
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
    public static void BitwiseOr(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
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
    public static void BitwiseXor(ReadOnlySpan<int> left, ReadOnlySpan<int> right, Span<int> destination)
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
    public static void BitwiseNot(ReadOnlySpan<int> source, Span<int> destination)
    {
        if (destination.Length < source.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint length = (nuint)source.Length;
        if (length == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        Vector512<int> allOnes512 = ~Vector512<int>.Zero;
        Vector256<int> allOnes256 = ~Vector256<int>.Zero;
        Vector128<int> allOnes128 = ~Vector128<int>.Zero;
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
            for (; i < limit; i += step) Vector512.Min(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector256.Min(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector128.Min(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
        {
            int l = Unsafe.Add(ref lRef, (nint)i), r = Unsafe.Add(ref rRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = l < r ? l : r;
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
            for (; i < limit; i += step) Vector512.Max(Vector512.LoadUnsafe(in lRef, i), Vector512.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector256.Max(Vector256.LoadUnsafe(in lRef, i), Vector256.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) Vector128.Max(Vector128.LoadUnsafe(in lRef, i), Vector128.LoadUnsafe(in rRef, i)).StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i)
        {
            int l = Unsafe.Add(ref lRef, (nint)i), r = Unsafe.Add(ref rRef, (nint)i);
            Unsafe.Add(ref dRef, (nint)i) = l > r ? l : r;
        }
    }

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
        int s = 0;
        for (nuint j = 0; j < length; ++j) s += Unsafe.Add(ref src, (nint)j);
        return s;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Fill(Span<int> destination, int value)
    {
        nuint length = (nuint)destination.Length;
        if (length == 0) return;
        ref int dRef = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && length >= (nuint)Vector512<int>.Count)
        {
            Vector512<int> v = Vector512.Create(value);
            nuint step = (nuint)Vector512<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        else if (Vector256.IsHardwareAccelerated && length >= (nuint)Vector256<int>.Count)
        {
            Vector256<int> v = Vector256.Create(value);
            nuint step = (nuint)Vector256<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        else if (Vector128.IsHardwareAccelerated && length >= (nuint)Vector128<int>.Count)
        {
            Vector128<int> v = Vector128.Create(value);
            nuint step = (nuint)Vector128<int>.Count, limit = length - step + 1;
            for (; i < limit; i += step) v.StoreUnsafe(ref dRef, i);
        }
        for (; i < length; ++i) Unsafe.Add(ref dRef, (nint)i) = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Gather(ReadOnlySpan<int> source, ReadOnlySpan<int> indices, Span<int> destination)
    {
        if (destination.Length < indices.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && count >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step) Vector512.Gather(in src, Vector512.LoadUnsafe(in idx, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector256.IsHardwareAccelerated && count >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step) Vector256.Gather(in src, Vector256.LoadUnsafe(in idx, i)).StoreUnsafe(ref dst, i);
        }
        else if (Vector128.IsHardwareAccelerated && count >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step) Vector128.Gather(in src, Vector128.LoadUnsafe(in idx, i)).StoreUnsafe(ref dst, i);
        }
        for (; i < count; ++i) Unsafe.Add(ref dst, (nint)i) = Unsafe.Add(ref src, Unsafe.Add(ref idx, (nint)i));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Scatter(ReadOnlySpan<int> source, ReadOnlySpan<int> indices, Span<int> destination)
    {
        if (source.Length > destination.Length) ThrowHelper.ThrowDestinationTooSmall();
        nuint count = (nuint)indices.Length;
        if (count == 0) return;
        ref int src = ref MemoryMarshal.GetReference(source);
        ref int idx = ref MemoryMarshal.GetReference(indices);
        ref int dst = ref MemoryMarshal.GetReference(destination);
        nuint i = 0;
        if (Vector512.IsHardwareAccelerated && count >= (nuint)Vector512<int>.Count)
        {
            nuint step = (nuint)Vector512<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step) Vector512.LoadUnsafe(in src, i).Scatter(ref dst, Vector512.LoadUnsafe(in idx, i));
        }
        else if (Vector256.IsHardwareAccelerated && count >= (nuint)Vector256<int>.Count)
        {
            nuint step = (nuint)Vector256<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step) Vector256.LoadUnsafe(in src, i).Scatter(ref dst, Vector256.LoadUnsafe(in idx, i));
        }
        else if (Vector128.IsHardwareAccelerated && count >= (nuint)Vector128<int>.Count)
        {
            nuint step = (nuint)Vector128<int>.Count, limit = count - step + 1;
            for (; i < limit; i += step) Vector128.LoadUnsafe(in src, i).Scatter(ref dst, Vector128.LoadUnsafe(in idx, i));
        }
        for (; i < count; ++i) Unsafe.Add(ref dst, Unsafe.Add(ref idx, (nint)i)) = Unsafe.Add(ref src, (nint)i);
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

internal static class ThrowHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidCapacity(int value)
    {
        throw new ArgumentOutOfRangeException(nameof(value), value, "Batch capacity out of valid hardware range.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidAlignment(nuint value)
    {
        throw new ArgumentException("Memory alignment must be a power of two and word-sized.", nameof(value));
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDestinationTooSmall()
    {
        throw new ArgumentException("Destination span is insufficiently sized for SIMD vectorized output.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMismatchedSpans()
    {
        throw new ArgumentException("Span operands must possess equal lengths for lock-step vectorization.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowEmptySequence()
    {
        throw new InvalidOperationException("Cannot compute horizontal SIMD reduction over an empty sequence.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowObjectDisposed()
    {
        throw new ObjectDisposedException("AlignedMemoryBlock", "Memory block is disposed or unmapped.");
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowConcurrentWaitNotSupported()
    {
        throw new InvalidOperationException("Concurrent async awaits on single-producer single-consumer signaler are not supported.");
    }
}
