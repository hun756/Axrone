using Axrone.Asset;

namespace Axrone.AssetShader.Tests;

internal readonly struct AssetShaderSchema;

internal sealed class AssetShaderTestDatabase : IDisposable
{
    public AssetDatabase<AssetShaderSchema, DefaultDataCodecPolicy<object>, DefaultLifecyclePolicy<object>> Inner { get; } = new();

    public void Dispose() => Inner.Dispose();
}

public sealed class ShaderBridgeTests : IDisposable
{
    private readonly AssetShaderTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private const string MinimalEffectJson = """
        {
          "id": "test",
          "vertex": { "main": "gl_Position = vec4(0.0);" },
          "fragment": { "main": "gl_FragColor = vec4(1.0);" }
        }
        """;

    [Fact]
    public async Task Import_Effect_Json_Through_Pipeline()
    {
        _db.Inner.RegisterImporter(new ShaderEffectAssetImporter());

        AssetImportReceipt<object> receipt = await _db.Inner.ImportAsync<object>(
            new AssetTextSource(MinimalEffectJson),
            new AssetKey("shaders/test"),
            sourceUri: new AssetUri("shaders/test.effect.json"));

        receipt.ImporterId.Value.Should().Be("axrone.importer.shader-effect");
        receipt.Primary.Kind.Should().Be("shader-effect");
        receipt.Primary.Data.Should().BeOfType<ShaderEffectDefinition>();

        var definition = (ShaderEffectDefinition)receipt.Primary.Data!;
        definition.Id.Name.Should().Be("test");
    }

    [Fact]
    public async Task Import_Glsl_Module_Through_Pipeline()
    {
        _db.Inner.RegisterImporter(new ShaderEffectAssetImporter());

        AssetImportReceipt<object> receipt = await _db.Inner.ImportAsync<object>(
            new AssetBytesSource(Encoding.UTF8.GetBytes("void main() {}")),
            new AssetKey("shaders/basic"),
            sourceUri: new AssetUri("shaders/basic.glsl"));

        var definition = (ShaderEffectDefinition)receipt.Primary.Data!;
        definition.Id.Name.Should().Be("basic");
        definition.Vertex.Main.Should().Be(definition.Fragment.Main);
    }

    [Fact]
    public void Non_Shader_Source_Is_Rejected()
    {
        var importer = new ShaderEffectAssetImporter();
        var context = new AssetImportContext(
            new AssetTextSource("hello"),
            new AssetKey("docs/readme"),
            sourceUri: new AssetUri("docs/readme.txt"));
        importer.CanImport(context).Should().BeFalse();
    }

    [Fact]
    public async Task Broken_Effect_Surfaces_Preprocess_Error()
    {
        _db.Inner.RegisterImporter(new ShaderEffectAssetImporter());

        Func<Task> act = () => _db.Inner.ImportAsync<object>(
            new AssetTextSource("{ not json"),
            new AssetKey("shaders/bad"),
            sourceUri: new AssetUri("shaders/bad.effect.json")).AsTask();
        await act.Should().ThrowAsync<ShaderPreprocessException>();
    }
}
