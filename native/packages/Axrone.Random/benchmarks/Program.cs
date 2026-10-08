using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Axrone.Random.Benchmarks;
#if !NATIVE_AOT
using System.Reflection;
using BenchmarkDotNet.Running;
#endif

// Runtime detection. runtimeconfig.json is deliberately not consulted: it records the host the
// build intended, not the process that is actually running. The two independent signals are a
// coreclr.dll host probe (the JIT runtime ships coreclr.dll; a Native AOT image does not) and the
// RuntimeFeature guard (dynamic code is unsupported under Native AOT).
string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
bool coreclrHost = File.Exists(Path.Combine(runtimeDirectory, "coreclr.dll"));
bool dynamicCodeSupported = RuntimeFeature.IsDynamicCodeSupported;
bool nativeAot = !dynamicCodeSupported && !coreclrHost;

Console.WriteLine(
    $"Runtime probe: coreclr.dll={coreclrHost}, IsDynamicCodeSupported={dynamicCodeSupported}, NativeAOT={nativeAot}");

if (nativeAot)
{
    // BenchmarkDotNet generates and loads IL at run time, so it cannot execute under Native AOT.
    // The manual harness produces the AOT numbers instead.
    AotHarness.Run();
    return;
}

#if !NATIVE_AOT
BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
#endif

/// <summary>
/// Stopwatch harness used only when the process is a Native AOT image, where BenchmarkDotNet
/// cannot run. It is deliberately simple: warm up, calibrate an iteration count to the benchmark's
/// speed, then time a tight loop and report ns/op and B/op. It is not a substitute for
/// BenchmarkDotNet's statistics; it exists so the AOT path still produces a labelled, comparable
/// table.
/// </summary>
internal static class AotHarness
{
    private const int WarmupMilliseconds = 100;
    private const int CalibrationMilliseconds = 10;
    private const int MeasureMilliseconds = 250;

    /// <summary>Runs every benchmark in the fixture's AOT plan and prints the result table.</summary>
    public static void Run()
    {
        var fixture = new RandomVsBclBenchmarks();
        fixture.Setup();

        Console.WriteLine(
            $"AOT manual harness: {WarmupMilliseconds} ms warmup + {MeasureMilliseconds} ms measured per benchmark.");
        Console.WriteLine($"{"Benchmark",-42} {"ns/op",12} {"B/op",10}");

        foreach ((string name, Action run) in fixture.CreateAotPlan())
        {
            WarmUp(run);
            long iterations = Calibrate(run);

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (long i = 0; i < iterations; i++)
            {
                run();
            }

            long elapsed = Stopwatch.GetTimestamp() - started;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            double nanoseconds = elapsed * (1_000_000_000.0 / Stopwatch.Frequency) / iterations;
            double bytes = (double)allocated / iterations;
            Console.WriteLine($"{name,-42} {nanoseconds,12:F2} {bytes,10:F2}");
        }
    }

    private static void WarmUp(Action run)
    {
        long end = Stopwatch.GetTimestamp() + (WarmupMilliseconds * Stopwatch.Frequency / 1000);
        while (Stopwatch.GetTimestamp() < end)
        {
            run();
        }
    }

    private static long Calibrate(Action run)
    {
        long started = Stopwatch.GetTimestamp();
        long end = started + (CalibrationMilliseconds * Stopwatch.Frequency / 1000);
        long iterations = 0;
        while (Stopwatch.GetTimestamp() < end)
        {
            run();
            iterations++;
        }

        long elapsed = Stopwatch.GetTimestamp() - started;
        double nanosecondsPerIteration = elapsed * (1_000_000_000.0 / Stopwatch.Frequency) / iterations;
        return Math.Max(1, (long)(MeasureMilliseconds * 1_000_000.0 / nanosecondsPerIteration));
    }
}
