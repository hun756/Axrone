namespace Axrone.Animation;

/// <summary>Strongly-typed rig identity.</summary>
public readonly record struct RigId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(RigId id) => id.Value;
}

/// <summary>
/// Strongly-typed clip identity with allocation-free formatting: char and UTF-8
/// span writers for logging, hashing, and key materialization without transcoding.
/// </summary>
public readonly record struct ClipId(string Value) : ISpanFormattable, IUtf8SpanFormattable
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(ClipId id) => id.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        ReadOnlySpan<char> value = Value.AsSpan();
        if (destination.Length < value.Length)
        {
            charsWritten = 0;
            return false;
        }

        value.CopyTo(destination);
        charsWritten = value.Length;
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString(string? format, IFormatProvider? formatProvider) => Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
    {
        if (!System.Text.Encoding.UTF8.TryGetBytes(Value.AsSpan(), utf8Destination, out bytesWritten))
        {
            bytesWritten = 0;
            return false;
        }

        return true;
    }
}

/// <summary>Strongly-typed layer identity.</summary>
public readonly record struct LayerId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(LayerId id) => id.Value;
}

/// <summary>Strongly-typed state identity.</summary>
public readonly record struct StateId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(StateId id) => id.Value;
}

/// <summary>Strongly-typed parameter identity.</summary>
public readonly record struct ParameterId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(ParameterId id) => id.Value;
}

/// <summary>Strongly-typed curve identity.</summary>
public readonly record struct CurveId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(CurveId id) => id.Value;
}

/// <summary>Strongly-typed IK job identity.</summary>
public readonly record struct IkJobId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(IkJobId id) => id.Value;
}

/// <summary>Strongly-typed retarget profile identity.</summary>
public readonly record struct RetargetProfileId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Unwraps the value.</summary>
    public static implicit operator string(RetargetProfileId id) => id.Value;
}
