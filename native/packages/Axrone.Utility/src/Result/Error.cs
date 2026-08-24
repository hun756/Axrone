using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Enterprise.Patterns.Result;

public enum ErrorType : byte
{
    Failure = 0,
    Unexpected = 1,
    Validation = 2,
    NotFound = 3,
    Conflict = 4,
    Unauthorized = 5,
    Forbidden = 6,
    Timeout = 7,
    Cancelled = 8,
    Critical = 9
}

public interface IError : IEquatable<IError>
{
    string Code { get; }
    string Message { get; }
    ErrorType Type { get; }
    IReadOnlyDictionary<string, object?> Metadata { get; }
    Exception? Exception { get; }
}

public interface IResult
{
    bool IsSuccess { get; }
    bool IsFailure { get; }
    Error TopError { get; }
    ErrorCollection Errors { get; }
}

public interface IResult<out TValue> : IResult
{
    TValue Value { get; }
}

[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
[StructLayout(LayoutKind.Auto)]
public readonly record struct Error : IError
{
    private static readonly FrozenDictionary<string, object?> EmptyMetadata = FrozenDictionary<string, object?>.Empty;

    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure, EmptyMetadata, null);
    public static readonly Error NullValue = new("Error.NullValue", "The provided value is null.", ErrorType.Failure, EmptyMetadata, null);
    public static readonly Error Uninitialized = new("Error.Uninitialized", "Result instance was default-initialized without state.", ErrorType.Failure, EmptyMetadata, null);

    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }
    public IReadOnlyDictionary<string, object?> Metadata { get; }
    public Exception? Exception { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Error(
        string code,
        string message,
        ErrorType type = ErrorType.Failure,
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null)
    {
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
        Type = type;
        Metadata = metadata ?? EmptyMetadata;
        Exception = exception;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Failure(
        string code = "Error.Failure",
        string message = "A failure occurred.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Failure, metadata, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Unexpected(
        string code = "Error.Unexpected",
        string message = "An unexpected error occurred.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Unexpected, metadata, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Validation(
        string code = "Error.Validation",
        string message = "A validation error occurred.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Validation, metadata, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error NotFound(
        string code = "Error.NotFound",
        string message = "The requested resource was not found.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.NotFound, metadata, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Conflict(
        string code = "Error.Conflict",
        string message = "A state conflict occurred.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Conflict, metadata, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Unauthorized(
        string code = "Error.Unauthorized",
        string message = "The operation was unauthorized.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Unauthorized, metadata, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Forbidden(
        string code = "Error.Forbidden",
        string message = "Access to the resource is forbidden.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Forbidden, metadata, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Timeout(
        string code = "Error.Timeout",
        string message = "The operation timed out.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Timeout, metadata, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Cancelled(
        string code = "Error.Cancelled",
        string message = "The operation was cancelled.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Cancelled, metadata, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Critical(
        string code = "Error.Critical",
        string message = "A critical system error occurred.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Critical, metadata, exception);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error FromException(Exception exception, string? code = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new(
            code ?? exception.GetType().Name,
            exception.Message,
            ErrorType.Unexpected,
            EmptyMetadata,
            exception);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(IError? other)
    {
        if (other is null) return false;
        return Type == other.Type &&
               string.Equals(Code, other.Code, StringComparison.Ordinal) &&
               string.Equals(Message, other.Message, StringComparison.Ordinal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode() => HashCode.Combine(Code, Message, Type);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private string GetDebuggerDisplay() => $"[{Type}] {Code}: {Message}";
}
