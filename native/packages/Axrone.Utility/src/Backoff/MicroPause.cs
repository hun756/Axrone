using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Axrone.Utility.Backoff;

public static class MicroPause
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Execute(uint count)
    {
        if (X86Base.IsSupported)
        {
            for (uint i = 0; i < count; i++)
            {
                X86Base.Pause();
            }
        }
        else if (ArmBase.IsSupported)
        {
            for (uint i = 0; i < count; i++)
            {
                ArmBase.Yield();
            }
        }
        else
        {
            Thread.SpinWait((int)Math.Min(count, 1024U));
        }
    }
}
