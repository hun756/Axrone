namespace Axrone.Profiler;

/// <summary>Coded profiler error.</summary>
public class ProfilerException : Exception
{
    /// <summary>Machine-readable code.</summary>
    public string Code { get; }

    /// <summary>Creates an error.</summary>
    public ProfilerException(string code, string message)
        : base(message) => Code = code;

    /// <summary>Creates an error with an inner exception.</summary>
    public ProfilerException(string code, string message, Exception? inner)
        : base(message, inner) => Code = code;
}

/// <summary>Coded clock error.</summary>
public class ClockException(string code, string message) : ProfilerException(code, message);

/// <summary>Reading a clock that is not running.</summary>
public sealed class ClockNotRunningException(string message) : ClockException("CLOCK_NOT_RUNNING", message);

/// <summary>Starting a clock that is already running.</summary>
public sealed class ClockAlreadyRunningException(string message) : ClockException("CLOCK_ALREADY_RUNNING", message);

/// <summary>Timestamp arithmetic overflowed.</summary>
public sealed class ClockOverflowException(string message) : ClockException("CLOCK_OVERFLOW", message);

/// <summary>Wall-clock skew exceeded the budget.</summary>
public sealed class ClockSkewException(string message) : ClockException("CLOCK_SKEW", message);

/// <summary>Requested resolution exceeds the clock's capability.</summary>
public sealed class ClockResolutionException(string message) : ClockException("CLOCK_RESOLUTION", message);

/// <summary>Profiler session failed to start.</summary>
public sealed class ProfilerStartupException(string message, Exception? inner = null)
    : ProfilerException("PROFILER_STARTUP", message, inner);

/// <summary>Profiler session failed to shut down.</summary>
public sealed class ProfilerShutdownException(string message, Exception? inner = null)
    : ProfilerException("PROFILER_SHUTDOWN", message, inner);

/// <summary>A sampler tick failed.</summary>
public sealed class ProfilerTickException(string message, Exception? inner = null)
    : ProfilerException("PROFILER_TICK_ERROR", message, inner);

/// <summary>Stack capture failed.</summary>
public sealed class ProfilerStackCaptureException(string message, Exception? inner = null)
    : ProfilerException("PROFILER_STACK_CAPTURE_ERROR", message, inner);

/// <summary>Centralized profiler throw helpers.</summary>
internal static class ProfilerThrowHelper
{
    /// <summary>Throws when a stopped clock is read.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotRunning(string entity) =>
        throw new ClockNotRunningException($"'{entity}' is not running.");

    /// <summary>Throws when a running clock is started again.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAlreadyRunning(string entity) =>
        throw new ClockAlreadyRunningException($"'{entity}' is already running.");

    /// <summary>Throws when timestamp arithmetic overflows.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static double ThrowOverflow()
    {
        throw new ClockOverflowException("Timestamp arithmetic overflowed.");
    }

    /// <summary>Throws when wall-clock skew exceeds the budget.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSkew(long skewMs, long maxSkewMs) =>
        throw new ClockSkewException($"Clock skew {skewMs} ms exceeded budget {maxSkewMs} ms.");

    /// <summary>Throws for an invalid profiler argument.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgument(string paramName, string message) =>
        throw new ArgumentException(message, paramName);
}
