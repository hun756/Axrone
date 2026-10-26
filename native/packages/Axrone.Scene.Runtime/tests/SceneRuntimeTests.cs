namespace Axrone.Scene.Runtime.Tests;

using System.Text.Json;
using Axrone.GameLoop;
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
    public void Pipeline_SteadyStatePlanningDoesNotAllocate()
    {
        using var runtime = CreateRenderableRuntime(out _, out _);

        for (int i = 0; i < 10; i++)
        {
            _ = runtime.Frame.Plan(i);
            _ = runtime.Frame.CollectItems(SceneRenderPassIds.Main);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            _ = runtime.Frame.Plan(i);
            _ = runtime.Frame.CollectItems(SceneRenderPassIds.Main);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        (after - before).Should().Be(0);
    }

    [Fact]
    public void Pipeline_SteadyStatePlanningDoesNotAllocateWithManyRenderers()
    {
        using var runtime = CreateRenderableRuntime(out _, out SceneActor firstRenderer);
        SceneWorld world = runtime.World;
        SceneMeshRenderer template = world.GetData<SceneMeshRenderer>(firstRenderer);

        for (int i = 1; i < 1000; i++)
        {
            SceneActor actor = world.CreateActor("Renderer");
            world.GetTransform3D(actor).SetPosition(new Vec3(i % 32, i / 32, 0.0f));
            world.AddData(actor, template);
        }

        world.UpdateTransforms();

        for (int i = 0; i < 10; i++)
        {
            _ = runtime.Frame.Plan(i);
            _ = runtime.Frame.CollectItems(SceneRenderPassIds.Main);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            _ = runtime.Frame.Plan(i);
            _ = runtime.Frame.CollectItems(SceneRenderPassIds.Main);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        (after - before).Should().Be(0);
    }

    [Fact]
    public void Pipeline_Plans2DScene()
    {
        SceneProfile profile = new SceneProfileBuilder(
                new SceneProfileId(SceneStringId.FromName("test/runtime-2d")))
            .AddTransform2D()
            .AddRendering2D()
            .Build();

        using var runtime = new SceneRuntime(new SceneRuntimeOptions
        {
            Profile = profile,
            ViewportWidth = 320,
            ViewportHeight = 180
        });

        SceneAssetId<TextureAssetTag> textureId = runtime.Assets.CreateId<TextureAssetTag>("texture/sprite");
        SceneWorld world = runtime.World;

        SceneActor cameraActor = world.CreateActor("Camera2D");
        world.GetTransform2D(cameraActor).SetPosition(new Vec2(0.0f, 0.0f));
        SceneCamera camera = SceneCamera.Default;
        camera.Primary = true;
        camera.OrthographicSize = 5.0f;
        world.AddData(cameraActor, camera);
        world.AddData(cameraActor, SceneCamera2D.Default);

        for (int i = 0; i < 3; i++)
        {
            SceneActor spriteActor = world.CreateActor("Sprite");
            world.GetTransform2D(spriteActor).SetPosition(new Vec2(i - 1.0f, 0.0f));
            SceneSpriteRenderer sprite = SceneSpriteRenderer.Default;
            sprite.TextureId = textureId;
            sprite.SortingLayer = i;
            world.AddData(spriteActor, sprite);
        }

        world.UpdateTransforms();

        runtime.Frame.Plan(0).Should().BeTrue();
        runtime.Frame.Camera!.Position.Should().Be(new Vec3(0.0f, 0.0f, -10.0f));
        runtime.Frame.CollectSprites(SceneRenderPassIds.Main).Should().Be(3);
        runtime.Frame.Complete(1).SpriteCount.Should().Be(3);
    }

    [Fact]
    public void Pipeline_ResolvesFogState()
    {
        using var runtime = CreateRenderableRuntime(out _, out _);
        SceneActor fogActor = runtime.World.CreateActor("Fog");
        SceneFog fog = SceneFog.Default;
        fog.Enabled = true;
        fog.Mode = SceneFogMode.Exponential;
        fog.Density = 0.5f;
        runtime.World.AddData(fogActor, fog);

        _ = runtime.Frame.Plan(0);

        runtime.Frame.Fog.Enabled.Should().BeTrue();
        runtime.Frame.Fog.Mode.Should().Be(SceneFogMode.Exponential);
        runtime.Frame.Fog.Density.Should().BeApproximately(0.5f, 1e-6f);
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

    [Fact]
    public void Loop_TicksRuntimeAndRenders()
    {
        var api = new MockGLApi { EnableCallLogging = true };
        using var context = new GLContext(api);
        using var runtime = CreateRenderableRuntime(out _, out _);
        using var renderer = new SceneGlFrameRenderer(context, runtime);
        renderer.UploadAssets();
        using var loop = new SceneRuntimeLoop(runtime, renderer);

        loop.Start(GameTime.FromMilliseconds(0.0));
        FrameExecutionOutcome outcome = loop.Tick(GameTime.FromMilliseconds(16.67));

        outcome.Kind.Should().Be(FrameOutcomeKind.Executed);
        outcome.Frame.Value.Should().Be(1);
        loop.State.CameraAvailable.Should().BeTrue();
        loop.State.RenderStats.DrawCalls.Should().Be(1);
        loop.State.RenderStats.PassCount.Should().Be(1);
    }

    [Fact]
    public void Loop_PlansHeadlesslyWithoutRenderer()
    {
        using var runtime = CreateRenderableRuntime(out _, out _);
        using var loop = new SceneRuntimeLoop(runtime);

        loop.Start(GameTime.FromMilliseconds(0.0));
        _ = loop.Tick(GameTime.FromMilliseconds(16.67));

        loop.State.CameraAvailable.Should().BeTrue();
        loop.State.RenderStats.DrawCalls.Should().Be(0);
        loop.State.RenderStats.PassCount.Should().Be(0);
    }

    [Fact]
    public void Loop_HaltsBeforeStart()
    {
        using var runtime = new SceneRuntime(new SceneRuntimeOptions { Profile = CreateProfile() });
        using var loop = new SceneRuntimeLoop(runtime);

        FrameExecutionOutcome outcome = loop.Tick(GameTime.FromMilliseconds(16.67));

        outcome.Kind.Should().Be(FrameOutcomeKind.Halted);
        loop.Status.Should().Be(GameLoopStatus.Idle);
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
