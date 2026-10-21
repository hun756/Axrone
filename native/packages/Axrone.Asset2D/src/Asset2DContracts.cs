namespace Axrone.Asset2D;

/// <summary>Frame attribute bits.</summary>
[Flags]
public enum SpriteFrameAttributes : uint
{
    /// <summary>No attributes.</summary>
    None = 0,

    /// <summary>Frame is stored rotated.</summary>
    Rotated = 1 << 0,

    /// <summary>Frame carries a 9-slice border.</summary>
    HasBorder = 1 << 1,

    /// <summary>Frame carries a trim offset.</summary>
    HasTrim = 1 << 2,

    /// <summary>Frame carries an explicit duration.</summary>
    HasDuration = 1 << 3,
}

/// <summary>
/// Role contract for zero-allocation frame query operations supporting span anti-constraints.
/// </summary>
public interface ISpriteFrameLookup<TKey>
    where TKey : allows ref struct
{
    /// <summary>Tries to resolve a frame.</summary>
    bool TryGetFrame(TKey key, out SpriteAtlasFrame frame);
}

/// <summary>
/// Role contract for zero-allocation animation query operations supporting span anti-constraints.
/// </summary>
public interface ISpriteAnimationLookup<TKey>
    where TKey : allows ref struct
{
    /// <summary>Tries to resolve an animation clip.</summary>
    bool TryGetAnimation(TKey key, [NotNullWhen(true)] out SpriteAnimationClip? animation);
}

/// <summary>
/// Unified read-only inspection contract for high-performance sprite atlases.
/// </summary>
public interface IReadOnlySpriteAtlas :
    ISpriteFrameLookup<FrameId>,
    ISpriteFrameLookup<ReadOnlySpan<char>>,
    ISpriteAnimationLookup<ClipId>,
    ISpriteAnimationLookup<ReadOnlySpan<char>>
{
    /// <summary>Atlas identity.</summary>
    AtlasId Id { get; }

    /// <summary>Backing texture identity.</summary>
    TextureId TextureId { get; }

    /// <summary>Backing texture size.</summary>
    Size2D TextureSize { get; }

    /// <summary>Frame count.</summary>
    int FrameCount { get; }

    /// <summary>Animation count.</summary>
    int AnimationCount { get; }
}

/// <summary>
/// Zero-vtable trait policy defining mathematical UV texture space coordinate mapping.
/// </summary>
public interface ICoordinatePolicy
{
    /// <summary>Maps a pixel region to normalized UV space.</summary>
    static abstract UvRect ComputeUv(in Rect2D region, in Size2D textureSize);
}

/// <summary>
/// Standard Top-Left origin coordinate policy (Direct3D, Vulkan, Metal, WebGPU, Modern Canvas).
/// </summary>
public readonly struct TopLeftOriginPolicy : ICoordinatePolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static UvRect ComputeUv(in Rect2D region, in Size2D textureSize)
    {
        float invW = 1.0f / textureSize.Width.Value;
        float invH = 1.0f / textureSize.Height.Value;
        return new UvRect(
            region.X.Value * invW,
            region.Y.Value * invH,
            region.Width.Value * invW,
            region.Height.Value * invH);
    }
}

/// <summary>
/// Bottom-Left origin coordinate policy (Standard OpenGL).
/// </summary>
public readonly struct BottomLeftOriginPolicy : ICoordinatePolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static UvRect ComputeUv(in Rect2D region, in Size2D textureSize)
    {
        float invW = 1.0f / textureSize.Width.Value;
        float invH = 1.0f / textureSize.Height.Value;
        float bottomY = textureSize.Height.Value - (region.Y.Value + region.Height.Value);
        return new UvRect(
            region.X.Value * invW,
            bottomY * invH,
            region.Width.Value * invW,
            region.Height.Value * invH);
    }
}
