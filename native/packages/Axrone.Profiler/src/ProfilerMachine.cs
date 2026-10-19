namespace Axrone.Profiler;

/// <summary>Machine-level facts: core count, OS and runtime identity.</summary>
public static class MachineInfo
{
    /// <summary>Logical processor count.</summary>
    public static int ProcessorCount => Environment.ProcessorCount;

    /// <summary>OS description string.</summary>
    public static string OsDescription => RuntimeInformation.OSDescription;

    /// <summary>Whether the process runs 64-bit.</summary>
    public static bool Is64BitProcess => Environment.Is64BitProcess;

    /// <summary>CLR version string.</summary>
    public static string ClrVersion => Environment.Version.ToString();

    /// <summary>Captures per-OS-thread CPU usage of the current process.</summary>
    public static IReadOnlyList<ThreadCpuSample> CaptureThreadCpu()
    {
        using System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
        ProcessThreadCollection threads = process.Threads;
        var samples = new List<ThreadCpuSample>(threads.Count);
        foreach (ProcessThread thread in threads)
        {
            try
            {
                samples.Add(new ThreadCpuSample(
                    thread.Id,
                    new Nanoseconds(thread.TotalProcessorTime.Ticks * 100L),
                    thread.ThreadState,
                    thread.BasePriority));
            }
            catch (InvalidOperationException)
            {
                // Thread exited mid-enumeration; skip it.
            }
        }

        return samples;
    }
}

/// <summary>Point-in-time process counters.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct ProcessSnapshot(
    long WorkingSetBytes,
    long PrivateBytes,
    long PeakWorkingSetBytes,
    int HandleCount,
    int ThreadCount,
    Nanoseconds TotalProcessorTime,
    Nanoseconds UserProcessorTime,
    Nanoseconds PrivilegedProcessorTime)
{
    /// <summary>Captures the current process.</summary>
    public static ProcessSnapshot Capture()
    {
        using System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
        return new ProcessSnapshot(
            process.WorkingSet64,
            process.PrivateMemorySize64,
            process.PeakWorkingSet64,
            process.HandleCount,
            process.Threads.Count,
            new Nanoseconds(process.TotalProcessorTime.Ticks * 100L),
            new Nanoseconds(process.UserProcessorTime.Ticks * 100L),
            new Nanoseconds(process.PrivilegedProcessorTime.Ticks * 100L));
    }
}

/// <summary>Per-OS-thread CPU usage. Managed-to-OS thread mapping is not
/// exposed by the runtime; pair <see cref="ManagedThreadId"/> separately.</summary>
public readonly record struct ThreadCpuSample(
    int OsThreadId,
    Nanoseconds TotalProcessorTime,
    System.Diagnostics.ThreadState State,
    int Priority)
{
    /// <summary>Current managed thread id for correlation.</summary>
    public static int ManagedThreadId => Environment.CurrentManagedThreadId;
}

/// <summary>Point-in-time GC counters.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct GcSnapshot(
    long HeapSizeBytes,
    long FragmentedBytes,
    long CommittedBytes,
    double PauseTimePercentage,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections)
{
    /// <summary>Captures the current GC state.</summary>
    public static GcSnapshot Capture()
    {
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        return new GcSnapshot(
            info.HeapSizeBytes,
            info.FragmentedBytes,
            info.TotalCommittedBytes,
            info.PauseTimePercentage,
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2));
    }
}
