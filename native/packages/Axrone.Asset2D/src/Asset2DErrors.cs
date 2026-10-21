namespace Axrone.Asset2D;

/// <summary>Coded Asset2D failure.</summary>
public class Asset2DException : Exception
{
    /// <summary>Machine-readable code.</summary>
    public string Code { get; }

    /// <summary>Creates an error.</summary>
    public Asset2DException(string code, string message)
        : base(message) => Code = code;

    /// <summary>Creates an error with an inner cause.</summary>
    public Asset2DException(string code, string message, Exception? innerException)
        : base(message, innerException) => Code = code;
}

/// <summary>Asset2D validation failure.</summary>
public sealed class Asset2DValidationException : Asset2DException
{
    /// <summary>Validation error code.</summary>
    public const string ValidationErrorCode = "asset-2d.validation";

    /// <summary>Creates a validation error.</summary>
    public Asset2DValidationException(string message)
        : base(ValidationErrorCode, message)
    {
    }

    /// <summary>Creates a validation error with an inner cause.</summary>
    public Asset2DValidationException(string message, Exception? innerException)
        : base(ValidationErrorCode, message, innerException)
    {
    }
}

/// <summary>Centralized cold-path throw helpers.</summary>
internal static class ThrowHelper
{
    /// <summary>Throws a validation error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowValidation(string message) => throw new Asset2DValidationException(message);

    /// <summary>Throws a coded import failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowImport(string code, string message) => throw new Asset2DException(code, message);

    /// <summary>Throws for a missing atlas key.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowKeyNotFound(string key) => throw new KeyNotFoundException($"Key '{key}' was not found in atlas.");

    /// <summary>Throws for a blank nominal identifier.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidIdentifier(string name) => throw new Asset2DValidationException($"Identifier '{name}' cannot be null or whitespace.");
}
