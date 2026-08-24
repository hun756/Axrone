using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Axrone.ObjectPool;

namespace Axrone.ObjectPool.Benchmarks;

public class Program
{
    public static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}

[MemoryDiagnoser]
[DisassemblyDiagnoser(printSource: true)]
public class ObjectPoolBenchmarks
{
    private ObjectPool<BenchItem> _pool = null!;
    private ObjectPool<BenchItem> _lockFreePool = null!;
    private ObjectPool<BenchItem> _shardedPool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pool = new ObjectPool<BenchItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 256,
                InitialCapacity = 64,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
                DiagnosticsLevel = DiagnosticsLevel.None,
            },
            new PoolableHandler<BenchItem>
            {
                Factory = static () => new BenchItem(),
                Reset = static item => item.Reset(),
            });

        _lockFreePool = new ObjectPool<BenchItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 256,
                InitialCapacity = 64,
                Strategy = PoolingStrategy.LockFree,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
                DiagnosticsLevel = DiagnosticsLevel.None,
            },
            new PoolableHandler<BenchItem>
            {
                Factory = static () => new BenchItem(),
                Reset = static item => item.Reset(),
            });

        _shardedPool = new ObjectPool<BenchItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 256,
                InitialCapacity = 64,
                Strategy = PoolingStrategy.ShardedByThread,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Default,
                DiagnosticsLevel = DiagnosticsLevel.None,
            },
            new PoolableHandler<BenchItem>
            {
                Factory = static () => new BenchItem(),
                Reset = static item => item.Reset(),
            });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _pool.Dispose();
        _lockFreePool.Dispose();
        _shardedPool.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Rent+Return (CentralizedQueue)")]
    public void RentReturnCentralized()
    {
        var item = _pool.Rent();
        _pool.Return(item);
    }

    [Benchmark(Description = "Rent+Return (LockFree)")]
    public void RentReturnLockFree()
    {
        var item = _lockFreePool.Rent();
        _lockFreePool.Return(item);
    }

    [Benchmark(Description = "Rent+Return (ShardedByThread)")]
    public void RentReturnSharded()
    {
        var item = _shardedPool.Rent();
        _shardedPool.Return(item);
    }

    [Benchmark(Description = "TryRent+Return")]
    public void TryRentReturn()
    {
        if (_pool.TryRent(out var item))
            _pool.Return(item);
    }

    [Benchmark(Description = "GetPoolable+Dispose (RAII)")]
    public void GetPoolableDispose()
    {
        using var poolable = _pool.GetPoolable();
    }
}

[MemoryDiagnoser]
public class ConcurrentBenchmarks
{
    private ObjectPool<BenchItem> _concurrentPool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _concurrentPool = new ObjectPool<BenchItem>(
            new PoolConfiguration
            {
                MaximumCapacity = 1024,
                InitialCapacity = 256,
                Strategy = PoolingStrategy.CentralizedQueue,
                UseSlimSynchronization = true,
                ConcurrencyLevel = ConcurrencyLevel.Maximum,
                DiagnosticsLevel = DiagnosticsLevel.None,
            },
            new PoolableHandler<BenchItem>
            {
                Factory = static () => new BenchItem(),
                Reset = static item => item.Reset(),
            });
    }

    [GlobalCleanup]
    public void Cleanup() => _concurrentPool.Dispose();

    [Benchmark(Description = "Concurrent Rent+Return (8 threads)")]
    public void ConcurrentRentReturn()
    {
        Parallel.For(0, 8, _ =>
        {
            for (int i = 0; i < 100; i++)
            {
                var item = _concurrentPool.Rent();
                _concurrentPool.Return(item);
            }
        });
    }
}

public sealed class BenchItem
{
    public int Value;
    public bool IsReset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        Value = 0;
        IsReset = true;
    }
}
