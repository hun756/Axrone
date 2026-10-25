namespace Axrone.Scene.Assets.Tests;

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
