namespace Axrone.Scene.Assets.Tests;

using Axrone.Numeric;
using Axrone.Scene;
using Axrone.Scene.Assets;
using FluentAssertions;
using Xunit;

public sealed class SceneAssetTests
{
    [Fact]
    public void ShaderDefinition_CopiesCollections()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<ShaderAssetTag> id = registry.CreateId<ShaderAssetTag>("shader/basic");
        var uniforms = new List<string> { "u_Model" };
        var attributes = new Dictionary<SceneMeshSemantic, string>
        {
            [SceneMeshSemantic.Position] = "a_Position"
        };

        var definition = new SceneShaderDefinition(
            id,
            "void main(){}",
            "void main(){}",
            uniforms,
            attributes);

        uniforms.Add("u_View");
        attributes[SceneMeshSemantic.Normal] = "a_Normal";

        definition.Uniforms.Should().Equal("u_Model");
        definition.Attributes.Should().ContainSingle();
        definition.Attributes.ContainsKey(SceneMeshSemantic.Normal).Should().BeFalse();
        definition.DepthTest.Should().BeTrue();
        definition.Cull.Should().BeTrue();
        definition.Blend.Should().BeFalse();
    }

    [Fact]
    public void ShaderDefinition_RequiresSources()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<ShaderAssetTag> id = registry.CreateId<ShaderAssetTag>("shader/bad");

        Action emptyVertex = () => { _ = new SceneShaderDefinition(id, "", "void main(){}"); };
        Action emptyFragment = () => { _ = new SceneShaderDefinition(id, "void main(){}", ""); };

        emptyVertex.Should().Throw<SceneAssetException>();
        emptyFragment.Should().Throw<SceneAssetException>();
    }

    [Fact]
    public void MeshDefinition_ComputesCounts()
    {
        var registry = new SceneAssetRegistry();
        SceneMeshDefinition mesh = CreateMesh(registry, "mesh/triangle");

        mesh.VertexCount.Should().Be(3);
        mesh.IndexCount.Should().Be(3);
        mesh.VertexStride.Should().Be(12);
        mesh.Topology.Should().Be(SceneMeshTopology.Triangles);
        mesh.IndexFormat.Should().Be(SceneIndexFormat.UInt16);
        mesh.Indices.Length.Should().Be(6);
    }

    [Fact]
    public void MeshDefinition_RejectsEmptyVertices()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<MeshAssetTag> id = registry.CreateId<MeshAssetTag>("mesh/empty");
        SceneVertexAttribute[] attributes =
        [
            new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12)
        ];

        Action create = () => { _ = new SceneMeshDefinition(id, ReadOnlyMemory<byte>.Empty, attributes); };

        create.Should().Throw<SceneAssetException>();
    }

    [Fact]
    public void MeshDefinition_RejectsMismatchedStrides()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<MeshAssetTag> id = registry.CreateId<MeshAssetTag>("mesh/stride");
        SceneVertexAttribute[] attributes =
        [
            new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12),
            new SceneVertexAttribute(SceneMeshSemantic.Uv0, 2, 12, 20)
        ];

        Action create = () => { _ = new SceneMeshDefinition(id, new byte[36], attributes); };

        create.Should().Throw<SceneAssetException>();
    }

    [Fact]
    public void MeshDefinition_RejectsUnalignedIndices()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<MeshAssetTag> id = registry.CreateId<MeshAssetTag>("mesh/indices");
        SceneVertexAttribute[] attributes =
        [
            new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12)
        ];

        Action create = () =>
        {
            _ = new SceneMeshDefinition(
                id,
                new byte[36],
                attributes,
                indices: new byte[3]);
        };

        create.Should().Throw<SceneAssetException>();
    }

    [Fact]
    public void Registry_RegisterLookupReplaceRemove()
    {
        var registry = new SceneAssetRegistry();
        SceneShaderDefinition first = CreateShader(registry, "shader/first");
        SceneShaderDefinition second = CreateShader(registry, "shader/first");

        SceneRegistrationResult<SceneShaderDefinition> initial = registry.Shaders.Register(first);
        initial.Previous.Should().BeNull();
        initial.Definition.Should().BeSameAs(first);

        SceneRegistrationResult<SceneShaderDefinition> replaced = registry.Shaders.Register(second);
        replaced.Previous.Should().BeSameAs(first);
        replaced.Definition.Should().BeSameAs(second);
        registry.Shaders.Count.Should().Be(1);
        registry.Shaders.Get(first.Id).Should().BeSameAs(second);

        registry.Shaders.Remove(first.Id, out SceneShaderDefinition? removed).Should().BeTrue();
        removed.Should().BeSameAs(second);
        registry.Shaders.Count.Should().Be(0);

        Action getMissing = () => registry.Shaders.Get(first.Id);
        getMissing.Should().Throw<SceneAssetNotFoundException<ShaderAssetTag>>();
    }

    [Fact]
    public void Registry_ClearReturnsDefinitions()
    {
        var registry = new SceneAssetRegistry();
        SceneShaderDefinition shader = CreateShader(registry, "shader/clear");
        SceneMeshDefinition mesh = CreateMesh(registry, "mesh/clear");
        registry.Shaders.Register(shader);
        registry.Meshes.Register(mesh);

        registry.Shaders.Clear().Should().ContainSingle().Which.Should().BeSameAs(shader);
        registry.Meshes.Definitions().Should().ContainSingle().Which.Should().BeSameAs(mesh);
        registry.Clear();
        registry.Count.Should().Be(0);
    }

    [Fact]
    public void AssetRegistry_CreatesAndResolvesTypedIds()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<MeshAssetTag> id = registry.CreateId<MeshAssetTag>("mesh/quad");

        id.IsValid.Should().BeTrue();
        registry.ResolveName(id).Should().Be("mesh/quad");
        registry.Names.TryGetId("mesh/quad", out SceneStringId symbol).Should().BeTrue();
        symbol.Should().Be(id.Value);
    }

    [Fact]
    public void SamplerDefinition_ValidatesAnisotropy()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<SamplerAssetTag> id = registry.CreateId<SamplerAssetTag>("sampler/linear");
        var sampler = new SceneSamplerDefinition(id, maxAnisotropy: 8);

        sampler.MaxAnisotropy.Should().Be(8);
        sampler.MinFilter.Should().Be(SceneFilterMode.Linear);
        sampler.WrapS.Should().Be(SceneWrapMode.Repeat);

        Action invalid = () => { _ = new SceneSamplerDefinition(id, maxAnisotropy: 0); };
        invalid.Should().Throw<SceneAssetException>();
    }

    [Fact]
    public void TextureDefinition_SupportsDataUrlAndColorSources()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<TextureAssetTag> dataId = registry.CreateId<TextureAssetTag>("texture/data");
        SceneAssetId<SamplerAssetTag> samplerId = registry.CreateId<SamplerAssetTag>("sampler/default");

        var dataTexture = new SceneTextureDefinition(
            dataId,
            new SceneTextureData(new byte[16]),
            format: SceneTextureFormat.Srgba8,
            width: 2,
            height: 2,
            samplerId: samplerId);
        var urlTexture = new SceneTextureDefinition(
            registry.CreateId<TextureAssetTag>("texture/url"),
            new SceneTextureUrl("../textures/albedo.png"));
        var colorTexture = new SceneTextureDefinition(
            registry.CreateId<TextureAssetTag>("texture/color"),
            new SceneTextureColor(new Vec4(1.0f, 0.5f, 0.25f, 1.0f), 4, 4));

        dataTexture.Width.Should().Be(2);
        dataTexture.Height.Should().Be(2);
        dataTexture.Format.Should().Be(SceneTextureFormat.Srgba8);
        dataTexture.SamplerId.Should().Be(samplerId);
        urlTexture.Source.Should().BeOfType<SceneTextureUrl>();
        ((SceneTextureUrl)urlTexture.Source).Url.OriginalString.Should().Be("../textures/albedo.png");
        colorTexture.Source.Should().BeOfType<SceneTextureColor>();

        Action emptyData = () => { _ = new SceneTextureData(ReadOnlyMemory<byte>.Empty); };
        emptyData.Should().Throw<SceneAssetException>();
    }

    [Fact]
    public void RenderPassDefinition_StoresClearState()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<RenderPassAssetTag> id = registry.CreateId<RenderPassAssetTag>("pass/main");
        var pass = new SceneRenderPassDefinition(
            id,
            order: 2,
            clearMask: SceneClearMask.Color | SceneClearMask.Depth,
            clearColor: new Vec4(0.1f, 0.2f, 0.3f, 1.0f),
            clearDepth: 1.0f,
            depthTest: true,
            cull: true,
            blend: false);

        pass.Order.Should().Be(2);
        pass.Enabled.Should().BeTrue();
        pass.ClearMask.Should().HaveFlag(SceneClearMask.Color);
        pass.ClearMask.Should().HaveFlag(SceneClearMask.Depth);
        pass.ClearColor.Should().Be(new Vec4(0.1f, 0.2f, 0.3f, 1.0f));
        pass.ClearDepth.Should().Be(1.0f);
        pass.DepthTest.Should().BeTrue();
        pass.Cull.Should().BeTrue();
        pass.Blend.Should().BeFalse();
    }

    [Fact]
    public void MaterialDefinition_CopiesPassesAndUniforms()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<ShaderAssetTag> shaderId = registry.CreateId<ShaderAssetTag>("material/shader");
        SceneAssetId<TextureAssetTag> textureId = registry.CreateId<TextureAssetTag>("material/albedo");
        SceneAssetId<SamplerAssetTag> samplerId = registry.CreateId<SamplerAssetTag>("material/sampler");
        var surface = new SceneMaterialSurface
        {
            ShadingModel = SceneMaterialShadingModel.Pbr,
            AlphaMode = SceneMaterialAlphaMode.Mask,
            AlphaCutoff = 0.4f,
            Metallic = 0.5f,
            Roughness = 0.3f,
            Features = SceneMaterialFeatures.AlbedoMap | SceneMaterialFeatures.NormalMap,
            AlbedoMap = new SceneMaterialTextureBinding(textureId, samplerId, unit: 0)
        };
        var passes = new List<SceneMaterialPass>
        {
            new("main", depthWrite: true),
            new("outline", priority: -1, cull: true)
        };
        var uniforms = new Dictionary<string, SceneUniformValue>
        {
            ["u_Custom"] = SceneUniformValue.FromFloat(0.75f)
        };

        var material = new SceneMaterialDefinition(
            registry.CreateId<MaterialAssetTag>("material/test"),
            shaderId,
            surface,
            passes,
            uniforms);

        passes.Clear();
        uniforms["u_Custom"] = SceneUniformValue.FromFloat(0.0f);
        uniforms["u_Added"] = SceneUniformValue.FromFloat(1.0f);

        material.ShaderId.Should().Be(shaderId);
        material.Passes.Should().HaveCount(2);
        material.Passes[0].Id.Should().Be("main");
        material.Passes[1].Id.Should().Be("outline");
        material.Surface.AlphaCutoff.Should().Be(0.4f);
        material.Surface.AlbedoMap!.Value.Unit.Should().Be(0);
        material.Uniforms.ContainsKey("u_Added").Should().BeFalse();
        material.Uniforms["u_Custom"].X.Should().Be(0.75f);
    }

    [Fact]
    public void MaterialSurface_ValidatesRanges()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<MaterialAssetTag> id = registry.CreateId<MaterialAssetTag>("material/invalid");
        SceneAssetId<ShaderAssetTag> shaderId = registry.CreateId<ShaderAssetTag>("material/shader");

        Action invalidMetallic = () => { _ = new SceneMaterialDefinition(id, shaderId, new SceneMaterialSurface { Metallic = 2.0f }); };
        Action invalidCutoff = () => { _ = new SceneMaterialDefinition(id, shaderId, new SceneMaterialSurface { AlphaCutoff = -0.1f }); };

        invalidMetallic.Should().Throw<SceneAssetException>();
        invalidCutoff.Should().Throw<SceneAssetException>();
    }

    [Fact]
    public void MaterialPass_RequiresId()
    {
        Action create = () => { _ = new SceneMaterialPass(""); };

        create.Should().Throw<SceneAssetException>();
    }

    [Fact]
    public void UniformValue_ValidatesFinitePayloads()
    {
        SceneUniformValue value = SceneUniformValue.FromVec3(new Vec3(1.0f, 2.0f, 3.0f));

        value.Kind.Should().Be(SceneUniformKind.Vec3);
        value.X.Should().Be(1.0f);
        value.Z.Should().Be(3.0f);

        Action invalid = () => SceneUniformValue.FromFloat(float.NaN);
        invalid.Should().Throw<SceneAssetException>();
    }

    [Fact]
    public void TextureBinding_RejectsNegativeUnit()
    {
        var registry = new SceneAssetRegistry();
        SceneAssetId<TextureAssetTag> textureId = registry.CreateId<TextureAssetTag>("binding/texture");

        Action create = () => { _ = new SceneMaterialTextureBinding(textureId, unit: -1); };

        create.Should().Throw<SceneAssetException>();
    }

    [Fact]
    public void AssetRegistry_CountsEveryKind()
    {
        var registry = new SceneAssetRegistry();
        registry.Shaders.Register(CreateShader(registry, "count/shader"));
        registry.Meshes.Register(CreateMesh(registry, "count/mesh"));
        registry.Materials.Register(CreateMaterial(registry, "count/material"));
        registry.Samplers.Register(new SceneSamplerDefinition(
            registry.CreateId<SamplerAssetTag>("count/sampler")));
        registry.Textures.Register(new SceneTextureDefinition(
            registry.CreateId<TextureAssetTag>("count/texture"),
            new SceneTextureColor(new Vec4(1.0f))));
        registry.RenderPasses.Register(new SceneRenderPassDefinition(
            registry.CreateId<RenderPassAssetTag>("count/pass")));

        registry.Count.Should().Be(6);
        registry.Clear();
        registry.Count.Should().Be(0);
    }

    private static SceneMaterialDefinition CreateMaterial(SceneAssetRegistry registry, string name)
    {
        SceneAssetId<ShaderAssetTag> shaderId = registry.CreateId<ShaderAssetTag>($"{name}/shader");
        return new SceneMaterialDefinition(
            registry.CreateId<MaterialAssetTag>(name),
            shaderId,
            new SceneMaterialSurface());
    }

    private static SceneShaderDefinition CreateShader(SceneAssetRegistry registry, string name)
    {
        SceneAssetId<ShaderAssetTag> id = registry.CreateId<ShaderAssetTag>(name);
        return new SceneShaderDefinition(id, "void main(){}", "void main(){}");
    }

    private static SceneMeshDefinition CreateMesh(SceneAssetRegistry registry, string name)
    {
        SceneAssetId<MeshAssetTag> id = registry.CreateId<MeshAssetTag>(name);
        SceneVertexAttribute[] attributes =
        [
            new SceneVertexAttribute(SceneMeshSemantic.Position, 3, 0, 12)
        ];
        byte[] vertices = new byte[36];
        byte[] indices = new byte[6];

        return new SceneMeshDefinition(id, vertices, attributes, indices: indices);
    }
}
