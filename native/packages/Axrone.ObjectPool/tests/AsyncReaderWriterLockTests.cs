using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Axrone.ObjectPool.Tests;

public class AsyncReaderWriterLockTests
{
    [Fact]
    public void MultipleReaders_CanHoldReadLockSimultaneously()
    {
        var lck = new AsyncReaderWriterLock();
        using var r1 = lck.ReadLock();
        using var r2 = lck.ReadLock();
        using var r3 = lck.ReadLock();
    }

    [Fact]
    public async Task WriteLock_IsExclusive_BlocksOtherWriters()
    {
        var lck = new AsyncReaderWriterLock();
        using var w = lck.WriteLock();
        var acquired = false;
        _ = Task.Run(() =>
        {
            using var w2 = lck.WriteLock();
            acquired = true;
        });
        await Task.Delay(200).ConfigureAwait(false);
        acquired.Should().BeFalse("second writer should be blocked");
    }

    [Fact]
    public async Task WriteLock_BlocksReaders()
    {
        var lck = new AsyncReaderWriterLock();
        using var w = lck.WriteLock();
        var acquired = false;
        _ = Task.Run(() =>
        {
            using var r = lck.ReadLock();
            acquired = true;
        });
        await Task.Delay(200).ConfigureAwait(false);
        acquired.Should().BeFalse("reader should be blocked by writer");
    }

    [Fact]
    public async Task ReadLock_Dispose_ReleasesLock()
    {
        var lck = new AsyncReaderWriterLock();
        var handle = lck.ReadLock();
        handle.Dispose();
        var writerAcquired = false;
        var t = Task.Run(() =>
        {
            using var w = lck.WriteLock();
            writerAcquired = true;
        });
        await t.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        writerAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task WriteLock_Dispose_ReleasesLock()
    {
        var lck = new AsyncReaderWriterLock();
        var handle = lck.WriteLock();
        handle.Dispose();
        var readerAcquired = false;
        var t = Task.Run(() =>
        {
            using var r = lck.ReadLock();
            readerAcquired = true;
        });
        await t.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        readerAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task AsyncReadLock_WorksCorrectly()
    {
        var lck = new AsyncReaderWriterLock();
        using var r1 = await lck.ReadLockAsync().ConfigureAwait(false);
        using var r2 = await lck.ReadLockAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task AsyncWriteLock_WorksCorrectly()
    {
        var lck = new AsyncReaderWriterLock();
        using var w = await lck.WriteLockAsync().ConfigureAwait(false);
        var acquired = false;
        _ = Task.Run(async () =>
        {
            using var w2 = await lck.WriteLockAsync().ConfigureAwait(false);
            acquired = true;
        });
        await Task.Delay(200).ConfigureAwait(false);
        acquired.Should().BeFalse("second async writer should be blocked");
    }

    [Fact]
    public void TryEnterWriteLock_ReturnsFalse_WhenWriteLockHeld()
    {
        var lck = new AsyncReaderWriterLock();
        using var w = lck.WriteLock();
        var result = lck.TryEnterWriteLock(0);
        result.Should().BeFalse();
    }

    [Fact]
    public void TryEnterWriteLock_ReturnsTrue_WhenNoLockHeld()
    {
        var lck = new AsyncReaderWriterLock();
        var result = lck.TryEnterWriteLock(0);
        result.Should().BeTrue();
        lck.ExitWriteLock();
    }

    [Fact]
    public void Dispose_PreventsFurtherLockAcquisition()
    {
        var lck = new AsyncReaderWriterLock();
        lck.Dispose();
        Action act = () => lck.ReadLock();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var lck = new AsyncReaderWriterLock();
        lck.Dispose();
        var act = () => lck.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void RecursiveReadLock_WhenSupportRecursion()
    {
        var lck = new AsyncReaderWriterLock(supportRecursion: true);
        using var r1 = lck.ReadLock();
        using var r2 = lck.ReadLock();
    }

    [Fact]
    public void RecursiveWriteLock_WhenSupportRecursion()
    {
        var lck = new AsyncReaderWriterLock(supportRecursion: true);
        using var w1 = lck.WriteLock();
        using var w2 = lck.WriteLock();
    }

    [Fact]
    public void UpgradeableReadLock_CanUpgradeToWriteLock()
    {
        var lck = new AsyncReaderWriterLock(supportRecursion: true);
        using var ur = lck.UpgradeableReadLock();
        using var w = lck.WriteLock();
    }

    [Fact]
    public async Task ConcurrentStressTest_MultipleParallelReadersSucceed()
    {
        var lck = new AsyncReaderWriterLock();
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var r = await lck.ReadLockAsync().ConfigureAwait(false);
                await Task.Delay(50).ConfigureAwait(false);
            }));
        }
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
    }
}
