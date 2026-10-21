namespace Axrone.Numeric;

/// <summary>Color comparison by selectable metric.</summary>
public readonly struct ColorComparer
{
    private readonly ColorComparisonMode _mode;

    /// <summary>Creates a comparer.</summary>
    public ColorComparer(ColorComparisonMode mode = ColorComparisonMode.Luminance)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported color comparison mode.");
        }

        _mode = mode;
    }

    /// <summary>Compares two colors: -1, 0 or 1.</summary>
    public int Compare(Color a, Color b)
    {
        const float Epsilon = 1e-6f;
        switch (_mode)
        {
            case ColorComparisonMode.Luminance:
            {
                float lumA = Color.Luminance(a);
                float lumB = Color.Luminance(b);
                if (MathF.Abs(lumA - lumB) < Epsilon)
                {
                    return 0;
                }

                return lumA < lumB ? -1 : 1;
            }

            case ColorComparisonMode.Hue:
            {
                float hA = a.ToHsl().H;
                float hB = b.ToHsl().H;
                if (MathF.Abs(hA - hB) < Epsilon)
                {
                    return 0;
                }

                return hA < hB ? -1 : 1;
            }

            case ColorComparisonMode.Saturation:
            {
                float sA = a.ToHsl().S;
                float sB = b.ToHsl().S;
                if (MathF.Abs(sA - sB) < Epsilon)
                {
                    return 0;
                }

                return sA < sB ? -1 : 1;
            }

            case ColorComparisonMode.RgbDistance:
            {
                float distA = MathF.Sqrt(a.R * a.R + a.G * a.G + a.B * a.B);
                float distB = MathF.Sqrt(b.R * b.R + b.G * b.G + b.B * b.B);
                if (MathF.Abs(distA - distB) < Epsilon)
                {
                    return 0;
                }

                return distA < distB ? -1 : 1;
            }

            case ColorComparisonMode.LabDistance:
            {
                LabColor labA = a.ToLab();
                LabColor labB = b.ToLab();
                float distA = MathF.Sqrt(labA.L * labA.L + labA.A * labA.A + labA.B * labA.B);
                float distB = MathF.Sqrt(labB.L * labB.L + labB.A * labB.A + labB.B * labB.B);
                if (MathF.Abs(distA - distB) < Epsilon)
                {
                    return 0;
                }

                return distA < distB ? -1 : 1;
            }

            case ColorComparisonMode.Alpha:
                if (MathF.Abs(a.A - b.A) < Epsilon)
                {
                    return 0;
                }

                return a.A < b.A ? -1 : 1;

            default:
                throw new InvalidOperationException($"Unsupported color comparison mode: {_mode}.");
        }
    }
}

/// <summary>Epsilon color equality with hashing.</summary>
public readonly struct ColorEqualityComparer
{
    private readonly float _epsilon;

    /// <summary>Creates a comparer.</summary>
    public ColorEqualityComparer(float epsilon = 1e-6f) => _epsilon = epsilon;

    /// <summary>Epsilon equality.</summary>
    public bool Equals(Color a, Color b) => a.ApproximatelyEquals(b, _epsilon);

    /// <summary>Hash code.</summary>
    public static int GetHashCode(Color color) => color.GetHashCode();
}
