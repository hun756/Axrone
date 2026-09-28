namespace Axrone.Execution;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>
/// Best-effort current-thread processor pinning. The only hard guarantees here
/// are the ones the OS actually provides — Windows gives per-thread affinity
/// masks, Linux gives a per-thread CPU set — so every path degrades to "not
/// pinned" instead of throwing, and the worker treats pinning as advisory.
/// </summary>
/// <remarks>
/// <para>
/// Bindings are source-generated <see cref="LibraryImportAttribute"/> rather than
/// <c>DllImport</c>, which keeps the package trim- and AOT-clean: the marshalling
/// stub is generated at compile time, there is no runtime marshaller to preserve,
/// and the entry-point probe is compile-time known per platform.
/// </para>
/// <para>
/// Platform coverage: Windows and Linux only. Every other target (macOS, the
/// browser-hosted runtime, a platform this build does not know) is a documented
/// no-op that reports <c>false</c> — a caller that treats the result as a
/// requirement is asking for something the BCL cannot promise cross-platform
/// either.
/// </para>
/// </remarks>
internal static partial class ThreadAffinityScope
{
    /// <summary>Windows encodes affinity as a 64-bit mask.</summary>
    private const int MaxWindowsCoreId = 63;

    /// <summary>Linux cpu_set_t is a fixed 1024-bit set.</summary>
    private const int MaxLinuxCoreId = 1023;

    /// <summary>Words in a Linux cpu_set_t (1024 bits / 64).</summary>
    private const int LinuxCpuSetWords = 16;

    /// <summary>Size of a Linux cpu_set_t in bytes.</summary>
    private const nuint LinuxCpuSetBytes = 128;

    /// <summary>
    /// Restricts the calling thread to one logical processor.
    /// </summary>
    /// <param name="coreId">Zero-based logical processor id.</param>
    /// <returns>
    /// <c>true</c> when the OS accepted the mask; <c>false</c> for an
    /// out-of-range core, an unsupported platform, or a rejected call. Never
    /// throws: a failed pin must not take the worker down with it.
    /// </returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Probing a platform ABI must never propagate: a rejected mask, a missing entry point and a platform fault all degrade to 'not pinned'.")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool TryPinCurrentThread(int coreId)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return TryPinWindows(coreId);

            if (OperatingSystem.IsLinux())
                return TryPinLinux(coreId);

            return false;
        }
        catch
        {
            // Deliberate: DllNotFoundException, EntryPointNotFoundException and
            // anything the platform raises on a rejected mask all mean the same
            // thing here — the request is advisory, so report it as declined.
            return false;
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentThread")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nuint GetCurrentThread();

    [LibraryImport("kernel32.dll", EntryPoint = "SetThreadAffinityMask")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nuint SetThreadAffinityMask(nuint thread, nuint threadAffinityMask);

    [LibraryImport("libc", EntryPoint = "sched_setaffinity")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static unsafe partial int SchedSetAffinity(int pid, nuint cpuSetSize, ulong* mask);

    [SupportedOSPlatform("windows")]
    private static bool TryPinWindows(int coreId)
    {
        if ((uint)coreId > MaxWindowsCoreId)
            return false;

        nuint thread = GetCurrentThread();
        return SetThreadAffinityMask(thread, (nuint)(1UL << coreId)) != 0;
    }

    [SupportedOSPlatform("linux")]
    private static unsafe bool TryPinLinux(int coreId)
    {
        if ((uint)coreId > MaxLinuxCoreId)
            return false;

        ulong* mask = stackalloc ulong[LinuxCpuSetWords];
        new Span<ulong>(mask, LinuxCpuSetWords).Clear();
        mask[coreId >> 6] = 1UL << (coreId & 63);

        // pid 0 addresses the calling thread, which is the thread being pinned.
        return SchedSetAffinity(0, LinuxCpuSetBytes, mask) == 0;
    }
}
