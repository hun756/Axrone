namespace Axrone.Scene.Rendering.Tests;

using System.Buffers;
using System.Text.Json;
using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Scene;
using Axrone.Scene.Assets;
using Axrone.Scene.Rendering;
using FluentAssertions;
using Xunit;

public sealed class SceneRenderingCodecTests
{
    [Fact]
    public void CameraCodec_RoundTrips()
    {
        var camera = new SceneCamera
        {
            Primary = true,
            Enabled = true,
            Orthographic = true,
            FieldOfViewAxis = SceneCameraFovAxis.Horizontal,
            FieldOfViewDegrees = 75.0f,
            OrthographicSize = 3.5f,
            Near = 0.25f,
            Far = 250.0f,
            ClearMask = SceneClearMask.Color,
            ClearColor = new Vec4(0.1f, 0.2f, 0.3f, 0.4f),
            ClearDepth = 0.5f
        };
        var codec = new SceneCameraCodec();

        SceneCamera restored = codec.Read(Capture(writer => codec.Write(writer, camera)));

        restored.Primary.Should().BeTrue();
        restored.Enabled.Should().BeTrue();
        restored.Orthographic.Should().BeTrue();
        restored.FieldOfViewAxis.Should().Be(SceneCameraFovAxis.Horizontal);
        restored.FieldOfViewDegrees.Should().BeApproximately(75.0f, 1e-6f);
        restored.OrthographicSize.Should().BeApproximately(3.5f, 1e-6f);
        restored.Near.Should().BeApproximately(0.25f, 1e-6f);
        restored.Far.Should().BeApproximately(250.0f, 1e-6f);
        restored.ClearMask.Should().Be(SceneClearMask.Color);
        restored.ClearColor.Should().Be(new Vec4(0.1f, 0.2f, 0.3f, 0.4f));
        restored.ClearDepth.Should().BeApproximately(0.5f, 1e-6f);
    }

    [Fact]
    public void MeshRendererCodec_RoundTripsAssetNames()
    {
        var assets = new SceneAssetRegistry();
        SceneAssetId<MeshAssetTag> mesh = assets.CreateId<MeshAssetTag>("mesh/quad");
        SceneAssetId<MaterialAssetTag> material = assets.CreateId<MaterialAssetTag>("material/basic");
        SceneAssetId<RenderPassAssetTag> pass = assets.CreateId<RenderPassAssetTag>("pass/main");
        var codec = new SceneMeshRendererCodec(assets.Names);

        var renderer = new SceneMeshRenderer
        {
            MeshId = mesh,
            MaterialId = material,
            PassId = pass,
            Visible = false,
            ReceiveLighting = false,
            Transparent = true,
            RenderOrder = 12,
            Bounds = new BoundingSphere3(new Vec3(1.0f, 2.0f, 3.0f), 4.0f)
        };

        JsonElement payload = Capture(writer => codec.Write(writer, renderer));
        SceneMeshRenderer restored = codec.Read(payload);

        payload.GetProperty("mesh").GetString().Should().Be("mesh/quad");
        payload.GetProperty("material").GetString().Should().Be("material/basic");
        payload.GetProperty("pass").GetString().Should().Be("pass/main");
        restored.MeshId.Should().Be(mesh);
        restored.MaterialId.Should().Be(material);
        restored.PassId.Should().Be(pass);
        restored.Visible.Should().BeFalse();
        restored.ReceiveLighting.Should().BeFalse();
        restored.Transparent.Should().BeTrue();
        restored.RenderOrder.Should().Be(12);
        restored.Bounds.Should().Be(new BoundingSphere3(new Vec3(1.0f, 2.0f, 3.0f), 4.0f));
    }

    [Fact]
    public void MeshRendererCodec_DefaultsWhenPayloadIsSparse()
    {
        var assets = new SceneAssetRegistry();
        var codec = new SceneMeshRendererCodec(assets.Names);

        SceneMeshRenderer restored = codec.Read(Capture(static _ => { }));

        restored.Visible.Should().BeTrue();
        restored.ReceiveLighting.Should().BeTrue();
        restored.Transparent.Should().BeFalse();
        restored.PassId.Should().Be(SceneRenderPassIds.Main);
        restored.Bounds.Should().BeNull();
    }

    [Fact]
    public void MeshRendererCodec_RejectsUnresolvedAssetIds()
    {
        var assets = new SceneAssetRegistry();
        var codec = new SceneMeshRendererCodec(assets.Names);
        var renderer = SceneMeshRenderer.Default;
        renderer.MeshId = SceneAssetId<MeshAssetTag>.FromName("mesh/unregistered");

        Action act = () => Capture(writer => codec.Write(writer, renderer));

        act.Should().Throw<SceneValidationException>()
            .Where(exception => exception.Code == SceneErrorCodes.ValidationInvalid)
            .WithMessage("*not present in the scene name table*");
    }

    [Fact]
    public void LightCodecs_RoundTrip()
    {
        var directional = new SceneDirectionalLight
        {
            Color = new Vec3(1.0f, 0.5f, 0.25f),
            Ambient = new Vec3(0.1f, 0.2f, 0.3f),
            Intensity = 2.5f,
            Primary = true,
            Enabled = false
        };
        var point = new ScenePointLight
        {
            Color = new Vec3(0.3f, 0.4f, 0.5f),
            Intensity = 1.5f,
            Range = 12.0f,
            Attenuation = 3.0f,
            Enabled = false
        };
        var spot = new SceneSpotLight
        {
            Color = new Vec3(0.5f, 0.6f, 0.7f),
            Intensity = 4.0f,
            Range = 20.0f,
            Attenuation = 1.5f,
            InnerConeAngleRadians = 0.25f,
            OuterConeAngleRadians = 0.75f,
            Enabled = false
        };

        var directionalCodec = new SceneDirectionalLightCodec();
        SceneDirectionalLight restoredDirectional =
            directionalCodec.Read(Capture(writer => directionalCodec.Write(writer, directional)));
        restoredDirectional.Color.Should().Be(directional.Color);
        restoredDirectional.Ambient.Should().Be(directional.Ambient);
        restoredDirectional.Intensity.Should().BeApproximately(2.5f, 1e-6f);
        restoredDirectional.Primary.Should().BeTrue();
        restoredDirectional.Enabled.Should().BeFalse();

        var pointCodec = new ScenePointLightCodec();
        ScenePointLight restoredPoint =
            pointCodec.Read(Capture(writer => pointCodec.Write(writer, point)));
        restoredPoint.Color.Should().Be(point.Color);
        restoredPoint.Intensity.Should().BeApproximately(1.5f, 1e-6f);
        restoredPoint.Range.Should().BeApproximately(12.0f, 1e-6f);
        restoredPoint.Attenuation.Should().BeApproximately(3.0f, 1e-6f);
        restoredPoint.Enabled.Should().BeFalse();

        var spotCodec = new SceneSpotLightCodec();
        SceneSpotLight restoredSpot =
            spotCodec.Read(Capture(writer => spotCodec.Write(writer, spot)));
        restoredSpot.Color.Should().Be(spot.Color);
        restoredSpot.Intensity.Should().BeApproximately(4.0f, 1e-6f);
        restoredSpot.Range.Should().BeApproximately(20.0f, 1e-6f);
        restoredSpot.Attenuation.Should().BeApproximately(1.5f, 1e-6f);
        restoredSpot.InnerConeAngleRadians.Should().BeApproximately(0.25f, 1e-6f);
        restoredSpot.OuterConeAngleRadians.Should().BeApproximately(0.75f, 1e-6f);
        restoredSpot.Enabled.Should().BeFalse();
    }

    [Fact]
    public void FogCodec_RoundTrips()
    {
        var fog = new SceneFog
        {
            Enabled = true,
            Color = new Vec3(0.25f, 0.5f, 0.75f),
            Mode = SceneFogMode.ExponentialSquared,
            Density = 0.125f,
            RangeStart = 5.0f,
            RangeEnd = 120.0f
        };
        var codec = new SceneFogCodec();

        JsonElement payload = Capture(writer => codec.Write(writer, fog));
        SceneFog restored = codec.Read(payload);

        payload.GetProperty("mode").GetString().Should().Be("exponential-squared");
        restored.Enabled.Should().BeTrue();
        restored.Color.Should().Be(fog.Color);
        restored.Mode.Should().Be(SceneFogMode.ExponentialSquared);
        restored.Density.Should().BeApproximately(0.125f, 1e-6f);
        restored.RangeStart.Should().BeApproximately(5.0f, 1e-6f);
        restored.RangeEnd.Should().BeApproximately(120.0f, 1e-6f);
    }

    [Fact]
    public void Register_RegistersComponentsPresentInTheRegistry()
    {
        using SceneWorld world = RenderingFixture.CreateProfile().CreateWorld();
        var assets = new SceneAssetRegistry();
        SceneCodecRegistry codecs = new SceneCodecRegistry(world.Registry);

        SceneRenderingCodecs.Register(codecs, world.Registry, assets);

        codecs.Count.Should().Be(6);
        codecs.TryGet("Camera", out _).Should().BeTrue();
        codecs.TryGet("MeshRenderer", out _).Should().BeTrue();
        codecs.TryGet("Fog", out _).Should().BeTrue();
        codecs.TryGet("DirectionalLight", out _).Should().BeTrue();
        codecs.TryGet("PointLight", out _).Should().BeTrue();
        codecs.TryGet("SpotLight", out _).Should().BeTrue();
    }

    [Fact]
    public void Register_SkipsComponentsOutsideTheActiveProfile()
    {
        var profile = new SceneProfileBuilder(
                new SceneProfileId(SceneStringId.FromName("test/rendering-minimal")))
            .AddTransform3D()
            .AddRendering3D()
            .Build();
        using SceneWorld world = profile.CreateWorld();
        var assets = new SceneAssetRegistry();
        SceneCodecRegistry codecs = new SceneCodecRegistry(world.Registry);

        SceneRenderingCodecs.Register(codecs, world.Registry, assets);

        codecs.Count.Should().Be(3);
        codecs.TryGet("Camera", out _).Should().BeTrue();
        codecs.TryGet("MeshRenderer", out _).Should().BeTrue();
        codecs.TryGet("Fog", out _).Should().BeTrue();
    }

    private static JsonElement Capture(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
