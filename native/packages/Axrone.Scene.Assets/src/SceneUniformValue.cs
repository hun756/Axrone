namespace Axrone.Scene.Assets;

/// <summary>Typed material uniform payload.</summary>
public enum SceneUniformKind : byte
{
    /// <summary>Single-precision scalar.</summary>
    Float = 0,

    /// <summary>Signed integer.</summary>
    Int = 1,

    /// <summary>Unsigned integer.</summary>
    UInt = 2,

    /// <summary>Boolean, encoded as an integer at the backend boundary.</summary>
    Bool = 3,

    /// <summary>Two-component vector.</summary>
    Vec2 = 4,

    /// <summary>Three-component vector.</summary>
    Vec3 = 5,

    /// <summary>Four-component vector.</summary>
    Vec4 = 6
}

/// <summary>
/// Compact tagged material uniform value. Matrices and arrays are deliberately
/// excluded: they are frame or skinning payloads owned by the renderer, not
/// authoring material state.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SceneUniformValue(
    SceneUniformKind Kind,
    float X,
    float Y,
    float Z,
    float W,
    int I,
    uint U,
    bool B)
{
    /// <summary>Creates a float value.</summary>
    public static SceneUniformValue FromFloat(float value)
    {
        ValidateFinite(value, nameof(value));
        return new SceneUniformValue(SceneUniformKind.Float, value, 0.0f, 0.0f, 0.0f, 0, 0U, false);
    }

    /// <summary>Creates an integer value.</summary>
    public static SceneUniformValue FromInt(int value) =>
        new(SceneUniformKind.Int, 0.0f, 0.0f, 0.0f, 0.0f, value, 0U, false);

    /// <summary>Creates an unsigned integer value.</summary>
    public static SceneUniformValue FromUInt(uint value) =>
        new(SceneUniformKind.UInt, 0.0f, 0.0f, 0.0f, 0.0f, 0, value, false);

    /// <summary>Creates a boolean value.</summary>
    public static SceneUniformValue FromBool(bool value) =>
        new(SceneUniformKind.Bool, 0.0f, 0.0f, 0.0f, 0.0f, 0, 0U, value);

    /// <summary>Creates a two-component value.</summary>
    public static SceneUniformValue FromVec2(in Vec2 value)
    {
        ValidateFinite(value.X, nameof(value));
        ValidateFinite(value.Y, nameof(value));
        return new SceneUniformValue(SceneUniformKind.Vec2, value.X, value.Y, 0.0f, 0.0f, 0, 0U, false);
    }

    /// <summary>Creates a three-component value.</summary>
    public static SceneUniformValue FromVec3(in Vec3 value)
    {
        ValidateFinite(value.X, nameof(value));
        ValidateFinite(value.Y, nameof(value));
        ValidateFinite(value.Z, nameof(value));
        return new SceneUniformValue(SceneUniformKind.Vec3, value.X, value.Y, value.Z, 0.0f, 0, 0U, false);
    }

    /// <summary>Creates a four-component value.</summary>
    public static SceneUniformValue FromVec4(in Vec4 value)
    {
        ValidateFinite(value.X, nameof(value));
        ValidateFinite(value.Y, nameof(value));
        ValidateFinite(value.Z, nameof(value));
        ValidateFinite(value.W, nameof(value));
        return new SceneUniformValue(SceneUniformKind.Vec4, value.X, value.Y, value.Z, value.W, 0, 0U, false);
    }

    private static void ValidateFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
        {
            throw new SceneAssetException(
                SceneAssetErrorCodes.Invalid,
                $"Uniform '{parameterName}' must be finite.");
        }
    }
}
