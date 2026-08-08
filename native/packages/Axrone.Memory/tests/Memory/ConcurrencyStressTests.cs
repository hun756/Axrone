using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Axrone.Memory.Tests.Memory;

[Collection("MemoryPool")]
public sealed class ConcurrencyStressTests
{
    [Fact]
    public void ConcurrentRentReturn_DoesNotCorruptData()
    {
        using var pool = new MemoryPool<byte>();
        const int threadCount = 8;
        const int iterationsPerThread = 1000;
        var errors = new ConcurrentBag<Exception>();

        Parallel.For(0, threadCount, threadId =>
        {
            try
            {
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    using var owner = pool.Rent(256);
                    var span = owner.Span;

                    // Fill with a pattern unique to this thread and iteration
                    byte marker = (byte)((threadId * 31 + i) & 0xFF);
                    span.Fill(marker);

                    // Verify the pattern is intact
                    for (int j = 0; j < span.Length; j++)
                    {
                        if (span[j] != marker)
                        {
                            errors.Add(new InvalidOperationException(
                                $"Data corruption detected: thread {threadId}, iteration {i}, index {j}. " +
                                $"Expected {marker}, got {span[j]}"));
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });

        errors.Should().BeEmpty("concurrent rent/return must not corrupt buffer data");
    }

    [Fact]
    public void ConcurrentRent_MultipleSizes()
    {
        using var pool = new MemoryPool<byte>();
        int[] sizes = [16, 64, 256, 1024, 4096, 16384];
        const int iterations = 200;
        var owners = new ConcurrentBag<(int size, int length, byte[] data)>();
        var errors = new ConcurrentBag<Exception>();

        // Rent buffers of various sizes in parallel
        Parallel.ForEach(sizes, size =>
        {
            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    var owner = pool.Rent(size);
                    var span = owner.Span;

                    // Length must be at least the requested size
                    if (owner.Length < size)
                    {
                        errors.Add(new InvalidOperationException(
                            $"Rented buffer length {owner.Length} is less than requested {size}"));
                        owner.Dispose();
                        return;
                    }

                    // Fill with a size-dependent pattern
                    byte pattern = (byte)(size & 0xFF);
                    span.Fill(pattern);

                    // Snapshot the data before returning
                    var snapshot = span.ToArray();
                    owners.Add((size, owner.Length, snapshot));
                    owner.Dispose();
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });

        errors.Should().BeEmpty("all parallel rents of different sizes should succeed");
        owners.Should().HaveCount(sizes.Length * iterations,
            "every rent should produce a valid independent buffer");

        // Verify buffers were independent — each snapshot should have uniform data
        foreach (var (size, _, data) in owners)
        {
            byte expected = (byte)(size & 0xFF);
            data.Should().AllSatisfy(b => b.Should().Be(expected),
                $"buffer rented with size {size} should contain its fill pattern");
        }
    }

    [Fact]
    public async Task ConcurrentDispose_DoesNotThrow()
    {
        var exceptions = new ConcurrentBag<Exception>();
        const int threadCount = 8;

        // Create multiple pools and dispose them concurrently from different threads
        for (int round = 0; round < 10; round++)
        {
            var pool = new MemoryPool<byte>();

            // Pre-warm the pool with some buffers
            var owners = new IMemoryOwner<byte>[16];
            for (int i = 0; i < owners.Length; i++)
            {
                owners[i] = pool.Rent(128);
            }

            // Return some buffers
            for (int i = 0; i < owners.Length; i += 2)
            {
                owners[i].Dispose();
            }

            // Now dispose the pool from multiple threads simultaneously
            var barrier = new Barrier(threadCount);
            var tasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    pool.Dispose();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            // Dispose remaining owners (should not throw even after pool dispose)
            for (int i = 1; i < owners.Length; i += 2)
            {
                try
                {
                    owners[i].Dispose();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }

        exceptions.Should().BeEmpty("concurrent Dispose calls must not throw");
    }

    [Fact]
    public void ConcurrentRentReturn_NoBufferLost()
    {
        using var pool = new MemoryPool<byte>();
        const int bufferCount = 64;
        const int rounds = 5;
        var errors = new ConcurrentBag<Exception>();

        for (int round = 0; round < rounds; round++)
        {
            var rentedOwners = new ConcurrentBag<IMemoryOwner<byte>>();

            // Rent N buffers in parallel
            Parallel.For(0, bufferCount, i =>
            {
                try
                {
                    var owner = pool.Rent(512);
                    owner.Span.Fill((byte)(i & 0xFF));
                    rentedOwners.Add(owner);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });

            rentedOwners.Count.Should().Be(bufferCount,
                $"all {bufferCount} buffers should be rented in round {round}");

            // Return all buffers in parallel
            var ownersArray = rentedOwners.ToArray();
            Parallel.For(0, ownersArray.Length, i =>
            {
                try
                {
                    ownersArray[i].Dispose();
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            });

            // Verify pool can rent again — all buffers should be available
            var verifyOwners = new IMemoryOwner<byte>[bufferCount];
            try
            {
                for (int i = 0; i < verifyOwners.Length; i++)
                {
                    verifyOwners[i] = pool.Rent(512);
                    verifyOwners[i].Should().NotBeNull(
                        $"pool should be able to rent buffer {i} after all were returned in round {round}");
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
            finally
            {
                for (int i = 0; i < verifyOwners.Length; i++)
                {
                    verifyOwners[i]?.Dispose();
                }
            }
        }

        errors.Should().BeEmpty("no buffers should be lost during concurrent rent/return cycles");
    }

    [Fact]
    public void StressTest_RapidRentReturn()
    {
        using var pool = new MemoryPool<byte>();
        const int cycles = 10000;

        var act = () =>
        {
            for (int i = 0; i < cycles; i++)
            {
                using var owner = pool.Rent(1024);
                var span = owner.Span;

                // Write and verify in each cycle
                span.Fill((byte)(i & 0xFF));
                for (int j = 0; j < span.Length; j++)
                {
                    if (span[j] != (byte)(i & 0xFF))
                    {
                        throw new InvalidOperationException(
                            $"Data mismatch at cycle {i}, index {j}");
                    }
                }
            }
        };

        act.Should().NotThrow(
            $"rapid rent/return of {cycles} cycles should complete without exceptions");
    }
}
