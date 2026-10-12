namespace Axrone.Geometry;

/// <summary>Builder state before any choice is made.</summary>
public readonly struct CameraUnconfigured;

/// <summary>Builder state after the projection is chosen.</summary>
public readonly struct CameraProjectionConfigured;

/// <summary>Builder state after the pose is supplied.</summary>
public readonly struct CameraFullyConfigured;

/// <summary>Position/look-target/up triple consumed by <see cref="Camera3D{TProjection}"/>.</summary>
/// <param name="Position">Eye point.</param>
/// <param name="Target">Look target.</param>
/// <param name="Up">Up hint; Y-up when omitted.</param>
public readonly record struct CameraPose(Vec3 Position, Vec3 Target, Vec3? Up = null)
{
    /// <summary>Up hint with the Y-up default applied.</summary>
    public Vec3 ResolvedUp => Up ?? Vec3.UnitY;
}

/// <summary>
/// Perspective or orthographic camera with a cached view/projection pipeline:
/// every pose or projection change rebuilds the matrices and re-extracts the
/// frustum, so queries always read a consistent snapshot.
/// </summary>
/// <typeparam name="TProjection">Projection policy; a struct for dispatch without virtual calls.</typeparam>
public sealed partial class Camera3D<TProjection>
    where TProjection : struct, ICameraProjection<TProjection>
{
    private TProjection _projection;
    private Vec3 _position;
    private Vec3 _target;
    private Vec3 _up;
    private Mat4 _viewMatrix;
    private Mat4 _projectionMatrix;
    private Mat4 _viewProjectionMatrix;
    private CameraFrustum _frustum;

    /// <summary>Creates a camera and builds its pipeline at once.</summary>
    public Camera3D(CameraId id, in TProjection projection, Vec3 position, Vec3 target, Vec3? up = null)
    {
        Id = id;
        _projection = projection;
        _position = position;
        _target = target;
        _up = up ?? Vec3.UnitY;

        UpdatePipeline();
    }

    /// <summary>Camera identity.</summary>
    public CameraId Id { get; }

    /// <summary>Active projection.</summary>
    public TProjection Projection
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _projection;
    }

    /// <summary>Closest accepted depth, from the projection.</summary>
    public DepthDistance Near
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _projection.NearDistance;
    }

    /// <summary>Furthest accepted depth, from the projection.</summary>
    public DepthDistance Far
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _projection.FarDistance;
    }

    /// <summary>Eye point.</summary>
    public Vec3 Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _position;
    }

    /// <summary>Look target.</summary>
    public Vec3 Target
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _target;
    }

    /// <summary>Up hint.</summary>
    public Vec3 Up
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _up;
    }

    /// <summary>Cached view matrix.</summary>
    public ref readonly Mat4 ViewMatrix
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _viewMatrix;
    }

    /// <summary>Cached projection matrix.</summary>
    public ref readonly Mat4 ProjectionMatrix
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _projectionMatrix;
    }

    /// <summary>Cached view-projection product.</summary>
    public ref readonly Mat4 ViewProjectionMatrix
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _viewProjectionMatrix;
    }

    /// <summary>Cached frustum extracted from the view-projection product.</summary>
    public ref readonly CameraFrustum Frustum
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _frustum;
    }

    /// <summary>Swaps the projection and rebuilds the pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SetProjection(in TProjection projection)
    {
        _projection = projection;
        UpdatePipeline();
    }

    /// <summary>Moves the camera and rebuilds the pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void SetPose(in CameraPose pose)
    {
        _position = pose.Position;
        _target = pose.Target;
        _up = pose.ResolvedUp;
        UpdatePipeline();
    }

    /// <summary>Moves the camera and rebuilds the pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetPose(Vec3 position, Vec3 target, Vec3? up = null) =>
        SetPose(new CameraPose(position, target, up));

    /// <summary>Classifies any bound volume against the cached frustum.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public ContainmentType Classify<TBounds>(in TBounds bounds)
        where TBounds : struct, IBoundingVolume<TBounds> =>
        _frustum.Classify(in bounds);

    /// <summary>Tests any bound volume against the cached frustum.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Intersects<TBounds>(in TBounds bounds)
        where TBounds : struct, IBoundingVolume<TBounds> =>
        _frustum.Intersects(in bounds);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void UpdatePipeline()
    {
        _viewMatrix = Mat4.CreateLookAt(_position, _target, _up);
        _projectionMatrix = _projection.ComputeProjectionMatrix();
        _viewProjectionMatrix = _viewMatrix * _projectionMatrix;
        _frustum.SetFromMatrix(in _viewProjectionMatrix);
    }
}

/// <summary>
/// Compile-time typestate builder for <see cref="Camera3D{TProjection}"/>.
/// The stage parameter only permits <c>Build</c> once a projection and a pose
/// are configured, so a poseless camera cannot be constructed.
/// </summary>
/// <typeparam name="TState">Configuration stage marker.</typeparam>
/// <typeparam name="TProjection">Projection policy to build the camera with.</typeparam>
public readonly struct CameraBuilder<TState, TProjection>
    where TProjection : struct, ICameraProjection<TProjection>
{
    private readonly CameraId _id;
    private readonly TProjection _projection;
    private readonly CameraPose _pose;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CameraBuilder(CameraId id, in TProjection projection, in CameraPose pose)
    {
        _id = id;
        _projection = projection;
        _pose = pose;
    }

    /// <summary>Starts a builder for <paramref name="id"/> with a default perspective projection.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CameraBuilder<CameraUnconfigured, CameraPerspectiveProjection> Create(CameraId id) =>
        new(id, default, default);

    /// <summary>Selects the projection, advancing to the projection-configured stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CameraBuilder<CameraProjectionConfigured, TNewProjection> WithProjection<TNewProjection>(in TNewProjection projection)
        where TNewProjection : struct, ICameraProjection<TNewProjection> =>
        new(_id, projection, _pose);

    /// <summary>Supplies the pose, advancing to the fully-configured stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CameraBuilder<CameraFullyConfigured, TProjection> WithPose(in CameraPose pose) =>
        new(_id, _projection, pose);

    /// <summary>Supplies the pose, advancing to the fully-configured stage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CameraBuilder<CameraFullyConfigured, TProjection> WithPose(Vec3 position, Vec3 target, Vec3? up = null) =>
        WithPose(new CameraPose(position, target, up));
}

/// <summary>Build entry points enabled once the builder stage permits them.</summary>
public static class CameraBuilderExtensions
{
    /// <summary>Builds the camera from a fully-configured builder.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Camera3D<TProjection> Build<TProjection>(
        this CameraBuilder<CameraFullyConfigured, TProjection> builder)
        where TProjection : struct, ICameraProjection<TProjection>
    {
        ref readonly var raw = ref Unsafe.As<CameraBuilder<CameraFullyConfigured, TProjection>,
            CameraBuilderStateInspector<TProjection>>(ref builder);

        return new Camera3D<TProjection>(raw.Id, raw.Projection, raw.Pose.Position, raw.Pose.Target, raw.Pose.Up);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CameraBuilderStateInspector<TProjection>
        where TProjection : struct, ICameraProjection<TProjection>
    {
        public readonly CameraId Id;
        public readonly TProjection Projection;
        public readonly CameraPose Pose;
    }
}
