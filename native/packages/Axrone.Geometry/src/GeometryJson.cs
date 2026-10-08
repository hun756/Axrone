namespace Axrone.Geometry;

using System.Text.Json.Serialization;

/// <summary>Source-generated JSON context (AOT-safe, no reflection).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Aabb3D))]
[JsonSerializable(typeof(Aabb2D))]
[JsonSerializable(typeof(Ray3D))]
[JsonSerializable(typeof(Triangle3))]
[JsonSerializable(typeof(TriangleHit3))]
[JsonSerializable(typeof(SweepHit3))]
[JsonSerializable(typeof(BoundingSphere3))]
[JsonSerializable(typeof(ContainmentType))]
[JsonSerializable(typeof(Axis))]
[JsonSerializable(typeof(SpatialItemId))]
[JsonSerializable(typeof(RayHit))]
[JsonSerializable(typeof(SweepResult))]
[JsonSerializable(typeof(RayIntersection))]
public sealed partial class AabbJsonContext : JsonSerializerContext
{
}
