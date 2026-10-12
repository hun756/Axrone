namespace Axrone.Geometry;

/// <summary>
/// Camera projection: near/far window plus the matrix builder. Static-abstract
/// so the camera closes over the projection type with no virtual dispatch.
/// </summary>
/// <typeparam name="TSelf">The projection type itself.</typeparam>
public interface ICameraProjection<TSelf>
    where TSelf : struct, ICameraProjection<TSelf>
{
    /// <summary>Short tag naming the projection ("perspective", "orthographic", ...).</summary>
    static abstract string KindIdentifier { get; }

    /// <summary>Closest accepted depth.</summary>
    DepthDistance NearDistance { get; }

    /// <summary>Furthest accepted depth.</summary>
    DepthDistance FarDistance { get; }

    /// <summary>Builds the projection matrix in the engine's row-vector convention.</summary>
    Mat4 ComputeProjectionMatrix();
}

/// <summary>
/// Perspective projection: vertical field of view, aspect ratio and a
/// near/far window with near strictly closer than far.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CameraPerspectiveProjection : ICameraProjection<CameraPerspectiveProjection>
{
    /// <summary>Vertical field of view.</summary>
    public readonly RadianFoV VerticalFieldOfView;

    /// <summary>Width over height.</summary>
    public readonly AspectRatio Ratio;

    /// <summary>Closest accepted depth.</summary>
    public readonly DepthDistance Near;

    /// <summary>Furthest accepted depth.</summary>
    public readonly DepthDistance Far;

    /// <inheritdoc/>
    public static string KindIdentifier => "perspective";

    /// <summary>Creates a projection; near must sit strictly closer than far.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CameraPerspectiveProjection(RadianFoV verticalFieldOfView, AspectRatio aspectRatio, DepthDistance near, DepthDistance far)
    {
        if (near >= far)
        {
            ThrowHelper.ThrowArgumentException("Near plane must be strictly closer than far plane.");
        }

        VerticalFieldOfView = verticalFieldOfView;
        Ratio = aspectRatio;
        Near = near;
        Far = far;
    }

    /// <inheritdoc/>
    public DepthDistance NearDistance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Near;
    }

    /// <inheritdoc/>
    public DepthDistance FarDistance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Far;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat4 ComputeProjectionMatrix() =>
        Mat4.CreatePerspectiveFieldOfView(VerticalFieldOfView.Value, Ratio.Value, Near.Value, Far.Value);
}

/// <summary>
/// Off-center orthographic projection: an explicit left/right/bottom/top box
/// plus a near/far window. The matrix mirrors the engine's right-handed
/// zero-to-one centered orthographic, generalized with the box offsets.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CameraOrthographicProjection : ICameraProjection<CameraOrthographicProjection>
{
    /// <summary>Left edge of the view box.</summary>
    public readonly float Left;

    /// <summary>Right edge of the view box.</summary>
    public readonly float Right;

    /// <summary>Bottom edge of the view box.</summary>
    public readonly float Bottom;

    /// <summary>Top edge of the view box.</summary>
    public readonly float Top;

    /// <summary>Closest accepted depth.</summary>
    public readonly DepthDistance Near;

    /// <summary>Furthest accepted depth.</summary>
    public readonly DepthDistance Far;

    /// <inheritdoc/>
    public static string KindIdentifier => "orthographic";

    /// <summary>Creates a projection; the box must be non-degenerate and near strictly closer than far.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CameraOrthographicProjection(float left, float right, float bottom, float top, DepthDistance near, DepthDistance far)
    {
        if (left >= right || bottom >= top || near >= far)
        {
            ThrowHelper.ThrowArgumentException("Invalid orthographic projection extents.");
        }

        Left = left;
        Right = right;
        Bottom = bottom;
        Top = top;
        Near = near;
        Far = far;
    }

    /// <inheritdoc/>
    public DepthDistance NearDistance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Near;
    }

    /// <inheritdoc/>
    public DepthDistance FarDistance
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Far;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Mat4 ComputeProjectionMatrix()
    {
        float widthScale = 2.0f / (Right - Left);
        float heightScale = 2.0f / (Top - Bottom);
        float range = Near.Value - Far.Value;

        return new Mat4(
            widthScale, 0.0f, 0.0f, 0.0f,
            0.0f, heightScale, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f / range, 0.0f,
            -(Right + Left) / (Right - Left), -(Top + Bottom) / (Top - Bottom), Near.Value / range, 1.0f);
    }
}
