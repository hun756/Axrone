namespace Axrone.Scene.Runtime.Tests;

using System.Text.Json;
using Axrone.Geometry;
using Axrone.Numeric;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Mock;
using Axrone.Scene;
using Axrone.Scene.Assets;
using Axrone.Scene.Prefab;
using Axrone.Scene.Rendering;
using Axrone.Scene.Runtime;
using Axrone.Scene.Snapshot;
using FluentAssertions;
using Xunit;

public sealed class SceneRuntimeTests
{
    [Fact]
    public void Runtime_ComposesWorldAssetsAndCodecs()
    {
        using var runtime = new SceneRuntime(new SceneRuntimeOptions { Profile = CreateProfile() });

        runtime.World.Should().NotBeNull();
        runtime.Codecs.TryGet("Transform3D", out _).Should().BeTrue();
        runtime.Codecs.TryGet("Camera", out _).Should().BeTrue();
        runtime.Codecs.TryGet("MeshRenderer", out _).Should().BeTrue();
        runtime.Codecs.TryGet("DirectionalLight", out _).Should().BeTrue();
        runtime.Frame.ViewportWidth.Should().Be(1280);
        runtime.Frame.ViewportHeight.Should().Be(720);
    }

    [Fact]
    public void Runtime_VerifiesViewport()
    {
        Action act = () => _ = new SceneRuntime(new SceneRuntimeOptions
        {
            Profile = CreateProfile(),
            ViewportWidth = 0
        });

        act.Should().Throw<SceneConfigurationException>();
    }

    [Fact]
    public void Runtime_LoadSnapshotInstantiatesPrefabAndAssets()
    {
        using var runtime = new SceneRuntime(new SceneRuntimeOptions { Profile = CreateProfile() });
        SceneSnapshot snapshot = CreateSnapshot(out _);

        ScenePrefabInstantiationResult result = runtime.LoadSnapshot(snapshot);

        result.Actors.Should().HaveCount(2);
        runtime.World.ActorCount.Should().Be(2);
        runtime.Assets.Shaders.Count.Should().Be(1);
        runtime.Assets.Materials.Count.Should().Be(1);
        runtime.Assets.RenderPasses.Count.Should().Be(1);
        runtime.Frame.Passes.Count.Should().Be(1);
    }

    [Fact]
    public void Runtime_ResolvesVariantAndInstantiatesIt()
    {
        using var runtime = new SceneRuntime(new SceneRuntimeOptions { Profile = CreateProfile() });
        ScenePrefabDefinition basePrefab = CreatePrefab("prefab/base");
        runtime.RegisterPrefab(basePrefab);
        runtime.RegisterPrefab(ScenePrefabVariants.CreateVariant(
            "prefab/base",
            "prefab/variant",
            [
                new ScenePrefabSetActorFieldOperation(
                    "root",
                    ScenePrefabActorField.Name,
                    ScenePrefabActorFieldValue.FromString("VariantRoot"))
            ]));

        ScenePrefabDefinition resolved = runtime.ResolvePrefab("prefab/variant");
        ScenePrefabInstantiationResult result = runtime.Instantiate(resolved);

        resolved.Kind.Should().Be(ScenePrefabKind.Resolved);
        result.Actors.Should().HaveCount(2);
        runtime.World.GetName(result.Actors[0]).Should().Be("VariantRoot");
    }

    [Fact]
    public void Pipeline_PlansCameraLightingAndItems()
    {
        using var runtime = CreateRenderableRuntime(out SceneActor camera, out _);
        _ = camera;

        bool planned = runtime.Frame.Plan(3);

        planned.Should().BeTrue();
        runtime.Frame.Camera.Should().NotBeNull();
        runtime.Frame.Lighting.Should().NotBeNull();
        runtime.Frame.CollectItems(SceneRenderPassIds.Main).Should().Be(1);

        SceneRenderStats stats = runtime.Frame.Complete(1);
        stats.Frame.Should().Be(3);
        stats.PassCount.Should().Be(1);
        stats.OpaqueCount.Should().Be(1);
        stats.TransparentCount.Should().Be(0);
    }

    [Fact]
    public void Pipeline_ReturnsFalseWithoutCamera()
    {
        using var runtime = new SceneRuntime(new SceneRuntimeOptions { Profile = CreateProfile() });
        _ = runtime.World.CreateActor("NoCamera");

        runtime.Frame.Plan(0).Should().BeFalse();
        runtime.Frame.Camera.Should().BeNull();
        runtime.Frame.Complete(0).DrawCalls.Should().Be(0);
    }

    [Fact]
    public void Pipeline_RejectsItemsBeforePlanning()
    {
        using var runtime = new SceneRuntime(new SceneRuntimeOptions { Profile = CreateProfile() });

        Action act = () => runtime.Frame.CollectItems(SceneRenderPassIds.Main);

        act.Should().Throw<SceneLifecycleException>();
    }

    [Fact]
    public void GlRenderer_RendersPlannedPasses()
    {
        var api = new MockGLApi { EnableCallLogging = true };
        using var context = new GLContext(api);
        using var runtime = CreateRenderableRuntime(out _, out _);
        using var renderer = new SceneGlFrameRenderer(context, runtime);

        renderer.UploadAssets();
        SceneRenderStats stats = renderer.Render(0);

        stats.DrawCalls.Should().Be(1);
        stats.OpaqueCount.Should().Be(1);
        stats.PassCount.Should().Be(1);
        api.CallLog.Should().Contain(call => call.StartsWith("DrawElements", StringComparison.Ordinal));
    }

    [Fact]
    public void CaptureSnapshot_RoundTripsIntoAnotherRuntime()
    {
        using var source = CreateRenderableRuntime(out _, out _);
        SceneSnapshot snapshot = source.CaptureSnapshot("prefab/captured");

        using var target = new SceneRuntime(new SceneRuntimeOptions { Profile = CreateProfile() });
        ScenePrefabInstantiationResult result = target.LoadSnapshot(snapshot);

        result.Actors.Should().HaveCount(3);
        target.Assets.Shaders.Count.Should().Be(1);
        target.Assets.Meshes.Count.Should().Be(1);
        target.Assets.Materials.Count.Should().Be(1);
        target.Frame.Passes.Count.Should().Be(1);
        target.Frame.Plan(0).Should().BeTrue();
        target.Frame.CollectItems(SceneRenderPassIds.Main).Should().Be(1);
    }

    private static SceneProfile CreateProfile() =>
        new SceneProfileBuilder(new SceneProfileId(SceneStringId.FromName("test/runtime")))
            .AddTransform3D()
            .AddRendering3D()
            .AddLighting3D()
            .Build();

    private static SceneRuntime CreateRenderableRuntime(
        out SceneActor cameraActor,
        out SceneActor rendererActor)
    {
        var runtime = new SceneRuntime(new SceneRuntimeOptions
        {
            Profile = CreateProfile(),
            ViewportWidth = 640,
            ViewportHeight = 360
        });

        SceneAssetRegistry assets = runtime.Assets;
        SceneAssetId<ShaderAssetTag> shaderId = assets.CreateId<ShaderAssetTag>("test/shader");
        assets.Shaders.Register(new SceneShaderDefinition(shaderId, "void main(){}", "void main(){}"));

        SceneAssetId<MeshAssetTag> meshId = assets.CreateId<MeshAssetTag>("test/mesh");
        assets.Meshes.Register(new SceneMeshDefinition(
            meshId,
            new byte[36],
            [new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12)],
            indices: new byte[6],
            bounds: new BoundingSphere3(Vec3.Zero, 1.0f)));

        SceneAssetId<MaterialAssetTag> materialId = assets.CreateId<MaterialAssetTag>("test/material");
        assets.Materials.Register(new SceneMaterialDefinition(
            materialId,
            shaderId,
            new SceneMaterialSurface()));

        SceneAssetId<RenderPassAssetTag> passId = assets.CreateId<RenderPassAssetTag>("main");
        assets.RenderPasses.Register(new SceneRenderPassDefinition(
            passId,
            clearMask: SceneClearMask.Color | SceneClearMask.Depth,
            clearColor: new Vec4(0.1f, 0.1f, 0.1f, 1.0f)));
        runtime.Frame.RebuildPasses();

        SceneWorld world = runtime.World;
        cameraActor = world.CreateActor("Camera");
        world.GetTransform3D(cameraActor).SetPosition(new Vec3(0.0f, 0.0f, 10.0f));
        SceneCamera camera = SceneCamera.Default;
        camera.Primary = true;
        world.AddData(cameraActor, camera);

        rendererActor = world.CreateActor("Renderer");
        SceneMeshRenderer renderer = SceneMeshRenderer.Default;
        renderer.MeshId = meshId;
        renderer.MaterialId = materialId;
        renderer.PassId = passId;
        world.AddData(rendererActor, renderer);

        SceneActor sun = world.CreateActor("Sun");
        world.AddData(sun, SceneDirectionalLight.Default);

        world.UpdateTransforms();
        return runtime;
    }

    private static SceneSnapshot CreateSnapshot(out SceneAssetRegistry assets)
    {
        assets = new SceneAssetRegistry();
        SceneAssetId<ShaderAssetTag> shaderId = assets.CreateId<ShaderAssetTag>("snapshot/shader");
        SceneAssetId<MeshAssetTag> meshId = assets.CreateId<MeshAssetTag>("snapshot/mesh");
        SceneAssetId<MaterialAssetTag> materialId = assets.CreateId<MaterialAssetTag>("snapshot/material");
        SceneAssetId<RenderPassAssetTag> passId = assets.CreateId<RenderPassAssetTag>("snapshot/pass");

        return new SceneSnapshot(
            CreatePrefab("prefab/snapshot"),
            [new SceneShaderDefinition(shaderId, "void main(){}", "void main(){}")],
            [
                new SceneMeshDefinition(
                    meshId,
                    new byte[36],
                    [new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12)])
            ],
            [
                new SceneMaterialDefinition(
                    materialId,
                    shaderId,
                    new SceneMaterialSurface())
            ],
            [],
            [],
            [new SceneRenderPassDefinition(passId)]);
    }

    private static ScenePrefabDefinition CreatePrefab(string id) => new(
        id,
        [
            Actor("root", "Root"),
            Actor("child", "Child", parentNodeId: "root")
        ]);

    private static SceneActorSnapshot Actor(string nodeId, string name, string? parentNodeId = null)
    {
        using JsonDocument document = JsonDocument.Parse("""{"position":[0,0,0]}""");
        return new SceneActorSnapshot(
            nodeId,
            name,
            0,
            "Untagged",
            active: true,
            persistent: false,
            pooled: false,
            [new SceneComponentSnapshot("Transform3D", document.RootElement.Clone())],
            parentNodeId);
    }
}
