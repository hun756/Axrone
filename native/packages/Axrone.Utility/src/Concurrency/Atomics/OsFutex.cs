namespace Axrone.Utility.Concurrency;

/// <summary>OS wait-on-address primitives with a managed fallback.</summary>
/// <remarks>
/// Windows 10+ serves 4-byte locations via WaitOnAddress/WakeByAddress. Everywhere else —
/// including Linux, whose futex binding stays unverified — callers use the Monitor fallback.
/// Non-4-byte locations always use the Monitor fallback.
/// </remarks>
internal static class OsFutex
{
    internal static readonly bool IsSupported = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [DllImport("api-ms-win-core-synch-l1-2-0.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int WaitOnAddress(ref int address, ref int compareAddress, nuint size, int timeoutMs);

    [DllImport("api-ms-win-core-synch-l1-2-0.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void WakeByAddressSingle(ref int address);

    [DllImport("api-ms-win-core-synch-l1-2-0.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void WakeByAddressAll(ref int address);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryWait(ref int address, ref int comparand, int timeoutMs)
    {
        if (!IsSupported)
        {
            return false;
        }

        return WaitOnAddress(ref address, ref comparand, (nuint)sizeof(int), timeoutMs) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WakeOne(ref int address)
    {
        if (IsSupported)
        {
            WakeByAddressSingle(ref address);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WakeAll(ref int address)
    {
        if (IsSupported)
        {
            WakeByAddressAll(ref address);
        }
    }
}
