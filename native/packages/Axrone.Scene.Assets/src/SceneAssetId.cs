namespace Axrone.Scene.Assets;

/// <summary>
/// Typed asset identity: a stable name symbol tagged by asset kind. The tag is
/// a phantom type, so shader and mesh ids cannot be interchanged even though
/// both are 64-bit symbols.
/// </summary>
/// <typeparam name="TTag">Phantom asset-kind tag.</typeparam>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct SceneAssetId<TTag>(SceneStringId Value) :
    IComparable<SceneAssetId<TTag>>,
    IComparisonOperators<SceneAssetId<TTag>, SceneAssetId<TTag>, bool>
    where TTag : struct
{
    /// <summary>Whether the id names a non-empty asset.</summary>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Value.IsValid;
    }

    /// <summary>Creates an id by hashing a UTF-16 name.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SceneAssetId<TTag> FromName(ReadOnlySpan<char> name) =>
        new(SceneStringId.FromName(name));

    /// <summary>Wraps an already interned symbol.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SceneAssetId<TTag> FromSymbol(SceneStringId value) => new(value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(SceneAssetId<TTag> other) => Value.CompareTo(other.Value);

    /// <summary>Determines whether the left id sorts before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(SceneAssetId<TTag> left, SceneAssetId<TTag> right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether the left id sorts at or before the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(SceneAssetId<TTag> left, SceneAssetId<TTag> right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether the left id sorts after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(SceneAssetId<TTag> left, SceneAssetId<TTag> right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether the left id sorts at or after the right one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(SceneAssetId<TTag> left, SceneAssetId<TTag> right) => left.CompareTo(right) >= 0;

    /// <summary>Unwraps the name symbol.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SceneStringId(SceneAssetId<TTag> id) => id.Value;

    /// <summary>Wraps a name symbol.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator SceneAssetId<TTag>(SceneStringId value) => new(value);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>Phantom tag for shader ids.</summary>
public readonly struct ShaderAssetTag;

/// <summary>Phantom tag for mesh ids.</summary>
public readonly struct MeshAssetTag;

/// <summary>Phantom tag for material ids.</summary>
public readonly struct MaterialAssetTag;

/// <summary>Phantom tag for texture ids.</summary>
public readonly struct TextureAssetTag;

/// <summary>Phantom tag for sampler ids.</summary>
public readonly struct SamplerAssetTag;

/// <summary>Phantom tag for render-pass ids.</summary>
public readonly struct RenderPassAssetTag;

/// <summary>Definition contract shared by registered scene assets.</summary>
/// <typeparam name="TTag">Phantom asset-kind tag.</typeparam>
public interface ISceneAssetDefinition<TTag>
    where TTag : struct
{
    /// <summary>Definition identity.</summary>
    SceneAssetId<TTag> Id { get; }
}
