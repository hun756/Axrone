namespace Axrone.Geometry;

using Axrone.Utility.Descriptors;

/// <summary>UV-sphere parameters.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SphereConfig
{
    /// <summary>Radius.</summary>
    public Metric Radius { get; init; } = new(0.5f);

    /// <summary>Longitude divisions.</summary>
    public SegmentResolution WidthSegments { get; init; } = new(32);

    /// <summary>Latitude divisions.</summary>
    public SegmentResolution HeightSegments { get; init; } = new(16);

    /// <summary>Horizontal start angle.</summary>
    public AngleRadians PhiStart { get; init; } = AngleRadians.Zero;

    /// <summary>Horizontal sweep.</summary>
    public AngleRadians PhiLength { get; init; } = AngleRadians.TwoPi;

    /// <summary>Vertical start angle.</summary>
    public AngleRadians ThetaStart { get; init; } = AngleRadians.Zero;

    /// <summary>Vertical sweep.</summary>
    public AngleRadians ThetaLength { get; init; } = AngleRadians.Pi;

    /// <summary>Creates default configuration.</summary>
    public SphereConfig() { }
}

/// <summary>Box parameters.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct BoxConfig
{
    /// <summary>X size.</summary>
    public Metric Width { get; init; } = Metric.One;

    /// <summary>Y size.</summary>
    public Metric Height { get; init; } = Metric.One;

    /// <summary>Z size.</summary>
    public Metric Depth { get; init; } = Metric.One;

    /// <summary>X divisions.</summary>
    public SegmentResolution WidthSegments { get; init; } = new(1);

    /// <summary>Y divisions.</summary>
    public SegmentResolution HeightSegments { get; init; } = new(1);

    /// <summary>Z divisions.</summary>
    public SegmentResolution DepthSegments { get; init; } = new(1);

    /// <summary>Creates default configuration.</summary>
    public BoxConfig() { }
}

/// <summary>Cylinder parameters.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CylinderConfig
{
    /// <summary>Top radius.</summary>
    public Metric RadiusTop { get; init; } = new(0.5f);

    /// <summary>Bottom radius.</summary>
    public Metric RadiusBottom { get; init; } = new(0.5f);

    /// <summary>Height.</summary>
    public Metric Height { get; init; } = Metric.One;

    /// <summary>Radial divisions.</summary>
    public SegmentResolution RadialSegments { get; init; } = new(32);

    /// <summary>Height divisions.</summary>
    public SegmentResolution HeightSegments { get; init; } = new(1);

    /// <summary>Whether caps are omitted.</summary>
    public bool OpenEnded { get; init; } = false;

    /// <summary>Angular start.</summary>
    public AngleRadians ThetaStart { get; init; } = AngleRadians.Zero;

    /// <summary>Angular sweep.</summary>
    public AngleRadians ThetaLength { get; init; } = AngleRadians.TwoPi;

    /// <summary>Creates default configuration.</summary>
    public CylinderConfig() { }
}

/// <summary>Capsule parameters.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CapsuleConfig
{
    /// <summary>Radius.</summary>
    public Metric Radius { get; init; } = new(0.5f);

    /// <summary>Cylinder-section length.</summary>
    public Metric Length { get; init; } = Metric.One;

    /// <summary>Divisions per cap.</summary>
    public SegmentResolution CapSegments { get; init; } = new(8);

    /// <summary>Radial divisions.</summary>
    public SegmentResolution RadialSegments { get; init; } = new(16);

    /// <summary>Creates default configuration.</summary>
    public CapsuleConfig() { }
}

/// <summary>Plane parameters.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PlaneConfig
{
    /// <summary>X size.</summary>
    public Metric Width { get; init; } = Metric.One;

    /// <summary>Y size.</summary>
    public Metric Height { get; init; } = Metric.One;

    /// <summary>X divisions.</summary>
    public SegmentResolution WidthSegments { get; init; } = new(1);

    /// <summary>Y divisions.</summary>
    public SegmentResolution HeightSegments { get; init; } = new(1);

    /// <summary>Creates default configuration.</summary>
    public PlaneConfig() { }
}

/// <summary>Torus parameters.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TorusConfig
{
    /// <summary>Ring radius.</summary>
    public Metric Radius { get; init; } = new(0.5f);

    /// <summary>Tube radius.</summary>
    public Metric Tube { get; init; } = new(0.2f);

    /// <summary>Radial divisions.</summary>
    public SegmentResolution RadialSegments { get; init; } = new(16);

    /// <summary>Tubular divisions.</summary>
    public SegmentResolution TubularSegments { get; init; } = new(32);

    /// <summary>Arc sweep.</summary>
    public AngleRadians Arc { get; init; } = AngleRadians.TwoPi;

    /// <summary>Creates default configuration.</summary>
    public TorusConfig() { }
}

/// <summary>Shape discriminant for exhaustive dispatch.</summary>
public enum ShapeKind : byte
{
    /// <summary>UV sphere.</summary>
    Sphere = 0,

    /// <summary>Box.</summary>
    Box = 1,

    /// <summary>Cylinder.</summary>
    Cylinder = 2,

    /// <summary>Capsule.</summary>
    Capsule = 3,

    /// <summary>Plane.</summary>
    Plane = 4,

    /// <summary>Torus.</summary>
    Torus = 5
}

/// <summary>
/// Flat discriminated union over the six supported shape configurations.
/// Only the six dispatchable shapes are members; the remaining generators
/// take bespoke parameters by design.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public readonly record struct ShapeDescriptor : IDescriptor<ShapeDescriptor>
{
    /// <summary>Active arm.</summary>
    [FieldOffset(0)] public readonly ShapeKind Kind;

    /// <summary>Sphere arm.</summary>
    [FieldOffset(4)] public readonly SphereConfig Sphere;

    /// <summary>Box arm.</summary>
    [FieldOffset(4)] public readonly BoxConfig Box;

    /// <summary>Cylinder arm.</summary>
    [FieldOffset(4)] public readonly CylinderConfig Cylinder;

    /// <summary>Capsule arm.</summary>
    [FieldOffset(4)] public readonly CapsuleConfig Capsule;

    /// <summary>Plane arm.</summary>
    [FieldOffset(4)] public readonly PlaneConfig Plane;

    /// <summary>Torus arm.</summary>
    [FieldOffset(4)] public readonly TorusConfig Torus;

    private ShapeDescriptor(ShapeKind kind, SphereConfig sphere) { Kind = kind; Sphere = sphere; }
    private ShapeDescriptor(ShapeKind kind, BoxConfig box) { Kind = kind; Box = box; }
    private ShapeDescriptor(ShapeKind kind, CylinderConfig cylinder) { Kind = kind; Cylinder = cylinder; }
    private ShapeDescriptor(ShapeKind kind, CapsuleConfig capsule) { Kind = kind; Capsule = capsule; }
    private ShapeDescriptor(ShapeKind kind, PlaneConfig plane) { Kind = kind; Plane = plane; }
    private ShapeDescriptor(ShapeKind kind, TorusConfig torus) { Kind = kind; Torus = torus; }

    /// <summary>Wraps a sphere configuration.</summary>
    public static ShapeDescriptor FromSphere(SphereConfig cfg) => new(ShapeKind.Sphere, cfg);

    /// <summary>Wraps a box configuration.</summary>
    public static ShapeDescriptor FromBox(BoxConfig cfg) => new(ShapeKind.Box, cfg);

    /// <summary>Wraps a cylinder configuration.</summary>
    public static ShapeDescriptor FromCylinder(CylinderConfig cfg) => new(ShapeKind.Cylinder, cfg);

    /// <summary>Wraps a capsule configuration.</summary>
    public static ShapeDescriptor FromCapsule(CapsuleConfig cfg) => new(ShapeKind.Capsule, cfg);

    /// <summary>Wraps a plane configuration.</summary>
    public static ShapeDescriptor FromPlane(PlaneConfig cfg) => new(ShapeKind.Plane, cfg);

    /// <summary>Wraps a torus configuration.</summary>
    public static ShapeDescriptor FromTorus(TorusConfig cfg) => new(ShapeKind.Torus, cfg);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Validate(in ShapeDescriptor descriptor)
    {
        switch (descriptor.Kind)
        {
            case ShapeKind.Sphere:
                ValidateMetric(descriptor.Sphere.Radius.Value, "Sphere radius");
                ValidateSegments(descriptor.Sphere.WidthSegments.Value, "Sphere width segments");
                ValidateSegments(descriptor.Sphere.HeightSegments.Value, "Sphere height segments");
                ValidateSweep(descriptor.Sphere.PhiLength.Value, "Sphere phi length");
                ValidateSweep(descriptor.Sphere.ThetaLength.Value, "Sphere theta length");
                break;
            case ShapeKind.Box:
                ValidateMetric(descriptor.Box.Width.Value, "Box width");
                ValidateMetric(descriptor.Box.Height.Value, "Box height");
                ValidateMetric(descriptor.Box.Depth.Value, "Box depth");
                ValidateSegments(descriptor.Box.WidthSegments.Value, "Box width segments");
                ValidateSegments(descriptor.Box.HeightSegments.Value, "Box height segments");
                ValidateSegments(descriptor.Box.DepthSegments.Value, "Box depth segments");
                break;
            case ShapeKind.Cylinder:
                ValidateNonNegativeFinite(descriptor.Cylinder.RadiusTop.Value, "Cylinder top radius");
                ValidateNonNegativeFinite(descriptor.Cylinder.RadiusBottom.Value, "Cylinder bottom radius");
                ValidateMetric(descriptor.Cylinder.Height.Value, "Cylinder height");
                ValidateSegments(descriptor.Cylinder.RadialSegments.Value, "Cylinder radial segments");
                ValidateSegments(descriptor.Cylinder.HeightSegments.Value, "Cylinder height segments");
                ValidateSweep(descriptor.Cylinder.ThetaLength.Value, "Cylinder theta length");
                break;
            case ShapeKind.Capsule:
                ValidateMetric(descriptor.Capsule.Radius.Value, "Capsule radius");
                ValidateNonNegativeFinite(descriptor.Capsule.Length.Value, "Capsule length");
                ValidateSegments(descriptor.Capsule.CapSegments.Value, "Capsule cap segments");
                ValidateSegments(descriptor.Capsule.RadialSegments.Value, "Capsule radial segments");
                break;
            case ShapeKind.Plane:
                ValidateMetric(descriptor.Plane.Width.Value, "Plane width");
                ValidateMetric(descriptor.Plane.Height.Value, "Plane height");
                ValidateSegments(descriptor.Plane.WidthSegments.Value, "Plane width segments");
                ValidateSegments(descriptor.Plane.HeightSegments.Value, "Plane height segments");
                break;
            case ShapeKind.Torus:
                ValidateMetric(descriptor.Torus.Radius.Value, "Torus radius");
                ValidateMetric(descriptor.Torus.Tube.Value, "Torus tube");
                ValidateSegments(descriptor.Torus.RadialSegments.Value, "Torus radial segments");
                ValidateSegments(descriptor.Torus.TubularSegments.Value, "Torus tubular segments");
                ValidateSweep(descriptor.Torus.Arc.Value, "Torus arc");
                break;
            default:
                ThrowHelper.ThrowArgumentOutOfRange(nameof(descriptor.Kind));
                break;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Describe(in ShapeDescriptor descriptor) => descriptor.ToString();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateMetric(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0.0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(name);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateNonNegativeFinite(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0.0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(name);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateSegments(uint value, string name)
    {
        if (value == 0U)
        {
            ThrowHelper.ThrowArgumentOutOfRange(name);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateSweep(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0.0f)
        {
            ThrowHelper.ThrowArgumentOutOfRange(name);
        }
    }
}
