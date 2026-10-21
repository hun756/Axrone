namespace Axrone.Numeric;

/// <summary>HSL color: hue degrees, saturation, lightness, alpha.</summary>
public readonly record struct HslColor(float H, float S, float L, float A)
{
    /// <summary>Whether the value is finite.</summary>
    public bool IsFinite => float.IsFinite(H) && float.IsFinite(S) && float.IsFinite(L) && float.IsFinite(A);
}

/// <summary>HSV color: hue degrees, saturation, value, alpha.</summary>
public readonly record struct HsvColor(float H, float S, float V, float A)
{
    /// <summary>Whether the value is finite.</summary>
    public bool IsFinite => float.IsFinite(H) && float.IsFinite(S) && float.IsFinite(V) && float.IsFinite(A);
}

/// <summary>CMYK color with alpha.</summary>
public readonly record struct CmykColor(float C, float M, float Y, float K, float A)
{
    /// <summary>Whether the value is finite.</summary>
    public bool IsFinite => float.IsFinite(C) && float.IsFinite(M) && float.IsFinite(Y) && float.IsFinite(K) && float.IsFinite(A);
}

/// <summary>CIELAB color with alpha.</summary>
public readonly record struct LabColor(float L, float A, float B, float Alpha)
{
    /// <summary>Whether the value is finite.</summary>
    public bool IsFinite => float.IsFinite(L) && float.IsFinite(A) && float.IsFinite(B) && float.IsFinite(Alpha);
}

/// <summary>CIEXYZ color with alpha.</summary>
public readonly record struct XyzColor(float X, float Y, float Z, float Alpha)
{
    /// <summary>Whether the value is finite.</summary>
    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) && float.IsFinite(Alpha);
}
