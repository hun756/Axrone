using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Enterprise.Patterns.Result;

/// <summary>Categorizes the kind of error represented by an <see cref="Error"/>.</summary>
public enum ErrorType : byte
{
    /// <summary>Generic failure.</summary>
    Failure = 0,
    /// <summary>Unexpected or unhandled error.</summary>
    Unexpected = 1,
    /// <summary>Input or business-rule validation failure.</summary>
    Validation = 2,
    /// <summary>Requested resource was not found.</summary>
    NotFound = 3,
    /// <summary>State or version conflict.</summary>
    Conflict = 4,
    /// <summary>Operation lacks valid authentication.</summary>
    Unauthorized = 5,
    /// <summary>Operation is authenticated but not permitted.</summary>
    Forbidden = 6,
    /// <summary>Operation exceeded its time limit.</summary>
    Timeout = 7,
    /// <summary>Operation was cancelled before completion.</summary>
    Cancelled = 8,
    /// <summary>Unrecoverable system-level failure.</summary>
    Critical = 9
}

/// <summary>Contract for an immutable error descriptor.</summary>
public interface IError : IEquatable<IError>
{
    /// <summary>Machine-readable error code.</summary>
    string Code { get; }
    /// <summary>Human-readable error message.</summary>
    string Message { get; }
    /// <summary>Error category.</summary>
    ErrorType Type { get; }
    /// <summary>Optional key-value metadata attached to the error.</summary>
    IReadOnlyDictionary<string, object?> Metadata { get; }
    /// <summary>Underlying exception, if any.</summary>
    Exception? Exception { get; }
}

/// <summary>Non-generic result contract exposing success/failure state and error list.</summary>
public interface IResult
{
    /// <summary><see langword="true"/> when the operation succeeded.</summary>
    bool IsSuccess { get; }
    /// <summary><see langword="true"/> when the operation failed.</summary>
    bool IsFailure { get; }
    /// <summary>The most recent (top-most) error.</summary>
    Error TopError { get; }
    /// <summary>All accumulated errors.</summary>
    ErrorCollection Errors { get; }
}

/// <summary>Generic result contract that additionally exposes a typed value.</summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
public interface IResult<out TValue> : IResult
{
    /// <summary>The success value.</summary>
    TValue Value { get; }
}

/// <summary>Lightweight, immutable error descriptor with a code, message, type, optional metadata, and optional exception.</summary>
[DebuggerDisplay("[{Type}] {Code}: {Message}")]
[StructLayout(LayoutKind.Auto)]
public readonly struct Error : IError, IEquatable<Error>
{
    private static readonly FrozenDictionary<string, object?> EmptyMetadata = FrozenDictionary<string, object?>.Empty;

    /// <summary>Sentinel representing the absence of an error.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure, EmptyMetadata, null);

    /// <summary>Standard error for a <see langword="null"/> value encounter.</summary>
    public static readonly Error NullValue = new("Error.NullValue", "The provided value is null.", ErrorType.Failure, EmptyMetadata, null);

    /// <summary>Standard error for a default-initialized <see cref="IResult"/> instance.</summary>
    public static readonly Error Uninitialized = new("Error.Uninitialized", "Result instance was default-initialized without state.", ErrorType.Failure, EmptyMetadata, null);

    /// <inheritdoc/>
    public string Code { get; }

    /// <inheritdoc/>
    public string Message { get; }

    /// <inheritdoc/>
    public ErrorType Type { get; }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Metadata { get; }

    /// <inheritdoc/>
    public Exception? Exception { get; }

    /// <summary>Initializes a new <see cref="Error"/> instance.</summary>
    /// <param name="code">Machine-readable error code.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="type">Error category. Defaults to <see cref="ErrorType.Failure"/>.</param>
    /// <param name="metadata">Optional key-value metadata.</param>
    /// <param name="exception">Optional underlying exception.</param>
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

    /// <summary>Creates a generic <see cref="ErrorType.Failure"/> error.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new <see cref="Error"/> of type <see cref="ErrorType.Failure"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Failure(
        string code = "Error.Failure",
        string message = "A failure occurred.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Failure, metadata, exception);

    /// <summary>Creates an <see cref="ErrorType.Unexpected"/> error.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new <see cref="Error"/> of type <see cref="ErrorType.Unexpected"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Unexpected(
        string code = "Error.Unexpected",
        string message = "An unexpected error occurred.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Unexpected, metadata, exception);

    /// <summary>Creates an <see cref="ErrorType.Validation"/> error.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new <see cref="Error"/> of type <see cref="ErrorType.Validation"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Validation(
        string code = "Error.Validation",
        string message = "A validation error occurred.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Validation, metadata, exception);

    /// <summary>Creates an <see cref="ErrorType.NotFound"/> error.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new <see cref="Error"/> of type <see cref="ErrorType.NotFound"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error NotFound(
        string code = "Error.NotFound",
        string message = "The requested resource was not found.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.NotFound, metadata, exception);

    /// <summary>Creates an <see cref="ErrorType.Conflict"/> error.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new <see cref="Error"/> of type <see cref="ErrorType.Conflict"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Conflict(
        string code = "Error.Conflict",
        string message = "A state conflict occurred.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Conflict, metadata, exception);

    /// <summary>Creates an <see cref="ErrorType.Unauthorized"/> error.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new <see cref="Error"/> of type <see cref="ErrorType.Unauthorized"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Unauthorized(
        string code = "Error.Unauthorized",
        string message = "The operation was unauthorized.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Unauthorized, metadata, exception);

    /// <summary>Creates an <see cref="ErrorType.Forbidden"/> error.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new <see cref="Error"/> of type <see cref="ErrorType.Forbidden"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Forbidden(
        string code = "Error.Forbidden",
        string message = "Access to the resource is forbidden.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Forbidden, metadata, exception);

    /// <summary>Creates an <see cref="ErrorType.Timeout"/> error.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new <see cref="Error"/> of type <see cref="ErrorType.Timeout"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Timeout(
        string code = "Error.Timeout",
        string message = "The operation timed out.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Timeout, metadata, exception);

    /// <summary>Creates an <see cref="ErrorType.Cancelled"/> error.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new <see cref="Error"/> of type <see cref="ErrorType.Cancelled"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Cancelled(
        string code = "Error.Cancelled",
        string message = "The operation was cancelled.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Cancelled, metadata, exception);

    /// <summary>Creates an <see cref="ErrorType.Critical"/> error.</summary>
    /// <param name="code">Error code.</param>
    /// <param name="message">Error message.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <param name="exception">Optional exception.</param>
    /// <returns>A new <see cref="Error"/> of type <see cref="ErrorType.Critical"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Error Critical(
        string code = "Error.Critical",
        string message = "A critical system error occurred.",
        IReadOnlyDictionary<string, object?>? metadata = null,
        Exception? exception = null) =>
        new(code, message, ErrorType.Critical, metadata, exception);

    /// <summary>Creates an <see cref="ErrorType.Unexpected"/> error from an exception.</summary>
    /// <param name="exception">The source exception.</param>
    /// <param name="code">Optional error code; defaults to the exception type name.</param>
    /// <returns>A new <see cref="Error"/> wrapping the exception.</returns>
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

    /// <summary>Determines whether this error equals another <see cref="IError"/>.</summary>
    /// <param name="other">The other error to compare.</param>
    /// <returns><see langword="true"/> if type, code, and message match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(IError? other)
    {
        if (other is null) return false;
        return Type == other.Type &&
               string.Equals(Code, other.Code, StringComparison.Ordinal) &&
               string.Equals(Message, other.Message, StringComparison.Ordinal);
    }

    /// <summary>Determines whether this error equals another <see cref="Error"/>.</summary>
    /// <param name="other">The other error to compare.</param>
    /// <returns><see langword="true"/> if type, code, and message match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(Error other)
    {
        return Type == other.Type &&
               string.Equals(Code, other.Code, StringComparison.Ordinal) &&
               string.Equals(Message, other.Message, StringComparison.Ordinal);
    }

    /// <summary>Determines whether this error equals the specified object.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="obj"/> is an <see cref="Error"/> with the same value.</returns>
    public override bool Equals(object? obj) => obj is Error other && Equals(other);

    /// <summary>Equality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if both errors are equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Error left, Error right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> if the errors differ.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Error left, Error right) => !left.Equals(right);

    /// <summary>Returns a hash code based on <see cref="Code"/>, <see cref="Message"/>, and <see cref="Type"/>.</summary>
    /// <returns>The hash code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode() => HashCode.Combine(Code, Message, Type);
}
