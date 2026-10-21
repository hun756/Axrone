namespace Axrone.Shapes2D;

/// <summary>Machine-readable shape failure code.</summary>
public enum Shapes2DErrorCode : byte
{
    /// <summary>Invalid number.</summary>
    InvalidNumber = 0,

    /// <summary>Invalid point.</summary>
    InvalidPoint = 1,

    /// <summary>Invalid color.</summary>
    InvalidColor = 2,

    /// <summary>Invalid paint.</summary>
    InvalidPaint = 3,

    /// <summary>Invalid gradient.</summary>
    InvalidGradient = 4,

    /// <summary>Invalid stroke.</summary>
    InvalidStroke = 5,

    /// <summary>Invalid shape.</summary>
    InvalidShape = 6,

    /// <summary>Invalid serialized payload.</summary>
    InvalidSerializedPayload = 7,

    /// <summary>Registry disposed.</summary>
    RegistryDisposed = 8,

    /// <summary>Shape not found.</summary>
    ShapeNotFound = 9,

    /// <summary>Capacity exceeded.</summary>
    CapacityExceeded = 10,
}

/// <summary>Coded shapes failure.</summary>
public class Shapes2DException : Exception
{
    /// <summary>Machine-readable code.</summary>
    public Shapes2DErrorCode ErrorCode { get; }

    /// <summary>Creates an error.</summary>
    public Shapes2DException(Shapes2DErrorCode errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>Shape validation failure.</summary>
public sealed class ShapeValidationException : Shapes2DException
{
    /// <summary>Creates a validation error.</summary>
    public ShapeValidationException(string message, Exception? innerException = null)
        : base(Shapes2DErrorCode.InvalidShape, message, innerException)
    {
    }
}

/// <summary>Paint validation failure.</summary>
public sealed class PaintValidationException : Shapes2DException
{
    /// <summary>Creates a validation error.</summary>
    public PaintValidationException(string message, Exception? innerException = null)
        : base(Shapes2DErrorCode.InvalidPaint, message, innerException)
    {
    }
}

/// <summary>Serialization failure.</summary>
public sealed class SerializationException : Shapes2DException
{
    /// <summary>Creates a serialization error.</summary>
    public SerializationException(string message, Exception? innerException = null)
        : base(Shapes2DErrorCode.InvalidSerializedPayload, message, innerException)
    {
    }
}

/// <summary>Registry failure.</summary>
public sealed class ShapeRegistryException : Shapes2DException
{
    /// <summary>Creates a registry error.</summary>
    public ShapeRegistryException(Shapes2DErrorCode errorCode, string message, Exception? innerException = null)
        : base(errorCode, message, innerException)
    {
    }
}

/// <summary>Centralized cold-path throw helpers.</summary>
internal static class ThrowHelper
{
    /// <summary>Throws a shape validation error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowShapeValidation(string message) => throw new ShapeValidationException(message);

    /// <summary>Throws a paint validation error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowPaintValidation(string message) => throw new PaintValidationException(message);

    /// <summary>Throws a serialization error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSerialization(string message) => throw new SerializationException(message);

    /// <summary>Throws a registry error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowRegistry(Shapes2DErrorCode code, string message) => throw new ShapeRegistryException(code, message);

    /// <summary>Guards against non-finite values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AssertFinite(float value, string paramName)
    {
        if (!float.IsFinite(value))
        {
            ThrowShapeValidation($"{paramName} must be a finite number.");
        }

        return value;
    }

    /// <summary>Guards against non-positive values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AssertPositive(float value, string paramName)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            ThrowShapeValidation($"{paramName} must be greater than 0.");
        }

        return value;
    }

    /// <summary>Guards against negative values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AssertNonNegative(float value, string paramName)
    {
        if (!float.IsFinite(value) || value < 0f)
        {
            ThrowShapeValidation($"{paramName} must be greater than or equal to 0.");
        }

        return value;
    }
}
