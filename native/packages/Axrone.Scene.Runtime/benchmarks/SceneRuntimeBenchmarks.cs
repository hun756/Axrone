namespace Axrone.Scene.Runtime.Benchmarks;

using Axrone.Numeric;
using Axrone.Scene;
using Axrone.Scene.Assets;
using Axrone.Scene.Rendering;
using Axrone.Scene.Runtime;
using BenchmarkDotNet.Attributes;

/// <summary>Measures camera, lighting and render-item planning across scene sizes.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class SceneFramePlanningBenchmarks
{
    private SceneRuntime _runtime = null!;
    private SceneAssetId<RenderPassAssetTag> _passId;

    /// <summary>Number of mesh renderers in the scene.</summary>
    [Params(100, 1000)]
    public int RendererCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        SceneProfile profile = new SceneProfileBuilder(
                new SceneProfileId(SceneStringId.FromName("bench/frame")))
            .AddTransform3D()
            .AddRendering3D()
            .AddLighting3D()
            .Build();

        _runtime = new SceneRuntime(new SceneRuntimeOptions
        {
            Profile = profile,
            ViewportWidth = 1280,
            ViewportHeight = 720
        });

        SceneAssetRegistry assets = _runtime.Assets;

        SceneAssetId<MeshAssetTag> meshId = assets.CreateId<MeshAssetTag>("bench/mesh");
        assets.Meshes.Register(SceneMeshFactory.CreateBox(meshId));

        SceneAssetId<ShaderAssetTag> shaderId = assets.CreateId<ShaderAssetTag>("bench/shader");
        assets.Shaders.Register(new SceneShaderDefinition(shaderId, "void main(){}", "void main(){}"));

        SceneAssetId<MaterialAssetTag> materialId = assets.CreateId<MaterialAssetTag>("bench/material");
        assets.Materials.Register(new SceneMaterialDefinition(
            materialId,
            shaderId,
            new SceneMaterialSurface()));

        _passId = assets.CreateId<RenderPassAssetTag>("main");
        assets.RenderPasses.Register(new SceneRenderPassDefinition(
            _passId,
            clearMask: SceneClearMask.Color | SceneClearMask.Depth));

        SceneWorld world = _runtime.World;
        SceneActor camera = world.CreateActor("BenchCamera");
        world.GetTransform3D(camera).SetPosition(new Vec3(0.0f, 0.0f, 40.0f));
        SceneCamera cameraData = SceneCamera.Default;
        cameraData.Primary = true;
        world.AddData(camera, cameraData);

        SceneActor sun = world.CreateActor("BenchSun");
        world.AddData(sun, SceneDirectionalLight.Default);

        int columns = (int)MathF.Ceiling(MathF.Sqrt(RendererCount));
        for (int i = 0; i < RendererCount; i++)
        {
            SceneActor actor = world.CreateActor("Renderer");
            world.GetTransform3D(actor).SetPosition(new Vec3(
                (i % columns) - (columns * 0.5f),
                (i / columns) - (columns * 0.5f),
                0.0f));

            SceneMeshRenderer renderer = SceneMeshRenderer.Default;
            renderer.MeshId = meshId;
            renderer.MaterialId = materialId;
            renderer.PassId = _passId;
            world.AddData(actor, renderer);
        }

        world.UpdateTransforms();
        _runtime.Frame.RebuildPasses();
    }

    [GlobalCleanup]
    public void Cleanup() => _runtime.Dispose();

    /// <summary>Plans a frame and collects the visible render items.</summary>
    [Benchmark]
    public int PlanAndCollect()
    {
        if (!_runtime.Frame.Plan(0))
        {
            return -1;
        }

        return _runtime.Frame.CollectItems(_passId);
    }
}

/// <summary>Measures procedural mesh authoring cost.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class SceneMeshGenerationBenchmarks
{
    private SceneAssetId<MeshAssetTag> _id;

    [GlobalSetup]
    public void Setup() => _id = SceneAssetId<MeshAssetTag>.FromName("bench/procedural");

    /// <summary>Generates a 32-segment sphere.</summary>
    [Benchmark]
    public int CreateSphere() => SceneMeshFactory.CreateSphere(_id, radius: 1.0f, segments: 32).VertexCount;

    /// <summary>Generates a subdivided box.</summary>
    [Benchmark]
    public int CreateBox() => SceneMeshFactory.CreateBox(_id, 1.0f, 1.0f, 1.0f, segments: 4).VertexCount;
}
