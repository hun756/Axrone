namespace Axrone.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Houses exceptional throwing paths outside hot methods to preserve RyuJIT inline budgets.</summary>
internal static class ThrowHelper
{
    /// <summary>Throws a value outside its validated range.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowArgumentOutOfRange<T>(string paramName) =>
        throw new ArgumentOutOfRangeException(paramName);

    /// <summary>Throws an input configuration error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowConfigError(string code, string message) =>
        throw new InputConfigurationError(code, message);

    /// <summary>Throws an input context error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowContextError(string code, string message) =>
        throw new InputContextError(code, message);

    /// <summary>Throws an input user error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowUserError(string code, string message) =>
        throw new InputUserError(code, message);

    /// <summary>Throws an input rebinding error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRebindError(string code, string message) =>
        throw new InputRebindingError(code, message);

    /// <summary>Throws when the engine is used after disposal.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDisposed() =>
        throw new InputDisposedError();

    /// <summary>Throws a snapshot validation error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSnapshotError(string code, string message) =>
        throw new InputSnapshotError(code, message);
}

/// <summary>
/// Coded input error.
/// </summary>
public class InputException(string code, string message) : Exception(message)
{
    /// <summary>Machine-readable code.</summary>
    public string Code { get; } = code;
}

/// <summary>
/// Invalid input configuration.
/// </summary>
public sealed class InputConfigurationError(string code, string message) : InputException(code, message);

/// <summary>
/// Unknown or conflicting context.
/// </summary>
public sealed class InputContextError(string code, string message) : InputException(code, message);

/// <summary>
/// Unknown or conflicting user.
/// </summary>
public sealed class InputUserError(string code, string message) : InputException(code, message);

/// <summary>
/// Failed rebinding operation.
/// </summary>
public sealed class InputRebindingError(string code, string message) : InputException(code, message);

/// <summary>
/// Use after engine disposal.
/// </summary>
public sealed class InputDisposedError() : InputException("input.disposed", "The input engine has already been disposed.");

/// <summary>
/// Malformed system snapshot.
/// </summary>
public sealed class InputSnapshotError(string code, string message) : InputException(code, message);
