namespace Axrone.Geometry;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Transfer shape of a camera: projection parameters plus pose.</summary>
public sealed record CameraSerialized
{
    /// <summary>Camera identity.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Projection tag ("perspective", "orthographic", ...).</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>Vertical field of view in radians; perspective only.</summary>
    [JsonPropertyName("verticalFieldOfView")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? VerticalFieldOfView { get; init; }

    /// <summary>Width over height; perspective only.</summary>
    [JsonPropertyName("aspectRatio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? AspectRatio { get; init; }

    /// <summary>Left edge; orthographic only.</summary>
    [JsonPropertyName("left")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Left { get; init; }

    /// <summary>Right edge; orthographic only.</summary>
    [JsonPropertyName("right")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Right { get; init; }

    /// <summary>Bottom edge; orthographic only.</summary>
    [JsonPropertyName("bottom")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Bottom { get; init; }

    /// <summary>Top edge; orthographic only.</summary>
    [JsonPropertyName("top")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Top { get; init; }

    /// <summary>Near plane distance.</summary>
    [JsonPropertyName("near")]
    public float Near { get; init; }

    /// <summary>Far plane distance.</summary>
    [JsonPropertyName("far")]
    public float Far { get; init; }

    /// <summary>Eye point.</summary>
    [JsonPropertyName("position")]
    public Vec3 Position { get; init; }

    /// <summary>Look target.</summary>
    [JsonPropertyName("target")]
    public Vec3 Target { get; init; }

    /// <summary>Up hint.</summary>
    [JsonPropertyName("up")]
    public Vec3 Up { get; init; }
}

/// <summary>Source-generated JSON context (AOT-safe, no reflection).</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CameraSerialized))]
internal sealed partial class CameraJsonContext : JsonSerializerContext
{
}

/// <summary>Serialization half of <see cref="Camera3D{TProjection}"/>.</summary>
public sealed partial class Camera3D<TProjection>
    where TProjection : struct, ICameraProjection<TProjection>
{
    /// <summary>Captures the transfer shape of this camera.</summary>
    public CameraSerialized ToSerialized()
    {
        if (typeof(TProjection) == typeof(CameraPerspectiveProjection))
        {
            ref readonly var perspective = ref Unsafe.As<TProjection, CameraPerspectiveProjection>(ref Unsafe.AsRef(in _projection));
            return new CameraSerialized
            {
                Id = Id.Value,
                Kind = CameraPerspectiveProjection.KindIdentifier,
                VerticalFieldOfView = perspective.VerticalFieldOfView.Value,
                AspectRatio = perspective.Ratio.Value,
                Near = perspective.Near.Value,
                Far = perspective.Far.Value,
                Position = _position,
                Target = _target,
                Up = _up,
            };
        }

        if (typeof(TProjection) == typeof(CameraOrthographicProjection))
        {
            ref readonly var orthographic = ref Unsafe.As<TProjection, CameraOrthographicProjection>(ref Unsafe.AsRef(in _projection));
            return new CameraSerialized
            {
                Id = Id.Value,
                Kind = CameraOrthographicProjection.KindIdentifier,
                Left = orthographic.Left,
                Right = orthographic.Right,
                Bottom = orthographic.Bottom,
                Top = orthographic.Top,
                Near = orthographic.Near.Value,
                Far = orthographic.Far.Value,
                Position = _position,
                Target = _target,
                Up = _up,
            };
        }

        return new CameraSerialized
        {
            Id = Id.Value,
            Kind = TProjection.KindIdentifier,
            Near = _projection.NearDistance.Value,
            Far = _projection.FarDistance.Value,
            Position = _position,
            Target = _target,
            Up = _up,
        };
    }
}

/// <summary>Rebuilds cameras from transfer shapes and JSON payloads.</summary>
public static class CameraSerializationFactory
{
    /// <summary>Deserializes a payload and rebuilds the camera it describes.</summary>
    public static object FromJson(string json)
    {
        if (json is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(json));
        }

        CameraSerialized? dto = JsonSerializer.Deserialize(json, CameraJsonContext.Default.CameraSerialized);
        if (dto is null)
        {
            ThrowHelper.ThrowInvalidOperation<object>("Deserialization payload yielded null.");
        }

        return FromDto(dto);
    }

    /// <summary>Rebuilds the camera a transfer shape describes.</summary>
    public static object FromDto(CameraSerialized dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var id = new CameraId(dto.Id);
        var near = new DepthDistance(dto.Near);
        var far = new DepthDistance(dto.Far);

        if (string.Equals(dto.Kind, CameraPerspectiveProjection.KindIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            if (!dto.VerticalFieldOfView.HasValue || !dto.AspectRatio.HasValue)
            {
                ThrowHelper.ThrowInvalidOperation<object>("Perspective camera requires FOV and aspect ratio.");
            }

            var projection = new CameraPerspectiveProjection(
                new RadianFoV(dto.VerticalFieldOfView.Value),
                new AspectRatio(dto.AspectRatio.Value),
                near,
                far);
            return new Camera3D<CameraPerspectiveProjection>(id, projection, dto.Position, dto.Target, dto.Up);
        }

        if (string.Equals(dto.Kind, CameraOrthographicProjection.KindIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            if (!dto.Left.HasValue || !dto.Right.HasValue || !dto.Bottom.HasValue || !dto.Top.HasValue)
            {
                ThrowHelper.ThrowInvalidOperation<object>("Orthographic camera requires orthogonal box extents.");
            }

            var projection = new CameraOrthographicProjection(
                dto.Left.Value,
                dto.Right.Value,
                dto.Bottom.Value,
                dto.Top.Value,
                near,
                far);
            return new Camera3D<CameraOrthographicProjection>(id, projection, dto.Position, dto.Target, dto.Up);
        }

        return ThrowHelper.ThrowInvalidOperation<object>($"Unknown projection kind: {dto.Kind}");
    }
}
