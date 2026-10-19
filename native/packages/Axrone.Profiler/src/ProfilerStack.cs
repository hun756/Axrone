namespace Axrone.Profiler;

using System.Diagnostics;

/// <summary>One captured managed frame.</summary>
public readonly record struct StackFrameCapture(string Function, string? File, int LineNumber, int ColumnNumber);

/// <summary>
/// Managed stack capture. The web engine parses JS engine traces (V8,
/// SpiderMonkey, JSC); natively there is no JS, so frames come from
/// <see cref="StackTrace"/> and signatures use FNV-1a like the web.
/// </summary>
public static class StackCaptureEngine
{
    private const uint FnvOffset = 2166136261U;
    private const uint FnvPrime = 16777619U;

    /// <summary>Captures the current managed stack, skipping harness frames.</summary>
    public static StackFrameCapture[] CaptureStack(int depth = 50)
    {
        if (depth < 1)
        {
            ProfilerThrowHelper.ThrowArgument(nameof(depth), "Depth must be positive.");
        }

        var trace = new StackTrace(fNeedFileInfo: true);
        StackFrame[] frames = trace.GetFrames() ?? [];
        int capacity = Math.Min(depth, frames.Length);
        var captured = new StackFrameCapture[capacity];
        int written = 0;

        for (int i = 0; i < frames.Length && written < capacity; i++)
        {
            System.Reflection.MethodBase? method = frames[i].GetMethod();
            if (method is null)
            {
                continue;
            }

            string function = method.DeclaringType is null
                ? method.Name
                : string.Create(CultureInfo.InvariantCulture, $"{method.DeclaringType.FullName}.{method.Name}");

            if (function.StartsWith("Axrone.Profiler.StackCaptureEngine.", StringComparison.Ordinal))
            {
                continue;
            }

            captured[written++] = new StackFrameCapture(
                function,
                frames[i].GetFileName(),
                frames[i].GetFileLineNumber(),
                frames[i].GetFileColumnNumber());
        }

        if (written == capacity)
        {
            return captured;
        }

        Array.Resize(ref captured, written);
        return captured;
    }

    /// <summary>FNV-1a signature of the current stack.</summary>
    public static string CaptureStackSignature(int depth = 50)
    {
        StackFrameCapture[] frames = CaptureStack(depth);
        uint hash = FnvOffset;
        foreach (StackFrameCapture frame in frames)
        {
            foreach (char c in frame.Function)
            {
                hash ^= c;
                hash *= FnvPrime;
            }

            hash ^= (byte)'\n';
            hash *= FnvPrime;
        }

        return string.Create(CultureInfo.InvariantCulture, $"stack-{hash:X8}");
    }
}
