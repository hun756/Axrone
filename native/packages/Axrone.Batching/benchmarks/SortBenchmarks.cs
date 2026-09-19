using Axrone.Batching;
using BenchmarkDotNet.Attributes;

namespace Axrone.Batching.Benchmarks;

/// <summary>
/// Sort baselines against <see cref="Array.Sort(Array)"/>, which is the BCL's own introsort.
/// </summary>
/// <remarks>
/// Every case sorts in place, so <see cref="Restore"/> re-seeds the working buffer between
/// iterations. BenchmarkDotNet excludes setup from the measured region; the numbers below are the
/// sort alone.
/// </remarks>
[MemoryDiagnoser]
public class SortBenchmarks
{
    private readonly struct Ascending : IBatchComparer<int>
    {
        public readonly int Compare(in int left, in int right) => left.CompareTo(right);
    }

    [Params(64, 1024, 16384, 262144)]
    public int Length { get; set; }

    private int[] _pristineInts = [];
    private int[] _workingInts = [];
    private int[] _scratchInts = [];
    private uint[] _pristineUInts = [];
    private uint[] _workingUInts = [];
    private uint[] _scratchUInts = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(20260919);

        _pristineInts = new int[Length];
        _pristineUInts = new uint[Length];
        for (var i = 0; i < Length; i++)
        {
            _pristineInts[i] = random.Next(int.MinValue / 4, int.MaxValue / 4);
            _pristineUInts[i] = ((uint)random.Next() << 1) | (uint)random.Next(0, 2);
        }

        _workingInts = new int[Length];
        _scratchInts = new int[Length];
        _workingUInts = new uint[Length];
        _scratchUInts = new uint[Length];
    }

    [IterationSetup]
    public void Restore()
    {
        Array.Copy(_pristineInts, _workingInts, Length);
        Array.Copy(_pristineUInts, _workingUInts, Length);
    }

    [Benchmark(Baseline = true)]
    public void BclArraySort() => Array.Sort(_workingInts);

    [Benchmark]
    public void Introsort()
    {
        unsafe
        {
            fixed (int* data = _workingInts)
            {
                new NativeBatch<int>(data, Length).Sort(new Ascending());
            }
        }
    }

    [Benchmark]
    public void StableMergeSort()
    {
        unsafe
        {
            fixed (int* data = _workingInts)
            fixed (int* scratch = _scratchInts)
            {
                new NativeBatch<int>(data, Length).StableSort(new Span<int>(scratch, Length), new Ascending());
            }
        }
    }

    [Benchmark]
    public void BclArraySortUInt32() => Array.Sort(_workingUInts);

    [Benchmark]
    public void RadixSortUInt32()
    {
        unsafe
        {
            fixed (uint* data = _workingUInts)
            fixed (uint* scratch = _scratchUInts)
            {
                new NativeBatch<uint>(data, Length).RadixSort(new Span<uint>(scratch, Length));
            }
        }
    }
}
