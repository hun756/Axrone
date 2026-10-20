namespace Axrone.AssetShader.Tests;

public sealed class ShaderParityTests
{
    [Fact]
    public void Migrations_RoundTrip()
    {
        ShaderEffectDefinition effect = ShaderPipelineBuilder<StageUninitialized>.Begin("a")
            .WithVertex(new ShaderStageDefinition("v"))
            .WithFragment(new ShaderStageDefinition("f"))
            .Seal()
            .BuildEffect()
            .Definition with
        {
            Migrations = new ShaderEffectMigrations(
                ImmutableDictionary.Create<string, ShaderPropertyMigration>()
                    .Add("albedo", new ShaderPropertyMigration(FormerlySerializedAs: "diffuse", Removed: false)),
                ImmutableDictionary.Create<string, ShaderPropertyMigration>()
                    .Add("OLD_KW", new ShaderPropertyMigration(Removed: true, Reason: "gone"))),
        };

        ShaderEffectDefinition restored = ShaderSerialization.DeserializeFromJson(ShaderSerialization.SerializeToJson(effect));
        restored.Migrations.Should().NotBeNull();
        restored.Migrations!.Value.Properties["albedo"].FormerlySerializedAs.Should().Be("diffuse");
        restored.Migrations!.Value.Keywords["OLD_KW"].Removed.Should().BeTrue();
    }

    [Fact]
    public void Editor_Views_Summarize()
    {
        ShaderEffect effect = ShaderPipelineBuilder<StageUninitialized>.Begin("lit")
            .WithVertex(new ShaderStageDefinition("v"))
            .WithFragment(new ShaderStageDefinition("f"))
            .WithKeywords(ImmutableArray.Create(
                new ShaderKeywordDefinition("FOG"),
                new ShaderKeywordDefinition("MODE", Options: ImmutableArray.Create("LIT", "UNLIT"))))
            .WithTechniques(ImmutableArray.Create(new ShaderTechniqueDefinition(
                new ShaderSymbol("main"),
                ImmutableArray.Create(new ShaderPassDefinition(new ShaderSymbol("p"))),
                Label: "Main")))
            .Seal()
            .BuildEffect();

        ShaderEffectDefinition definition = effect.Definition;
        ShaderEditorSummary summary = ShaderEditorViews.Summarize(
            in definition,
            UniformExtractor.ScanUniforms("uniform mat4 uModel;\n".AsSpan(), "uniform sampler2D uAlbedo;\n".AsSpan()));
        summary.Uniforms.Should().HaveCount(2);
        summary.Keywords.Should().Contain(k => k.Name == "FOG" && k.Kind == "toggle");
        summary.Keywords.Should().Contain(k => k.Name == "MODE" && k.Kind == "enum" && k.Options.Length == 2);
        summary.Techniques.Should().ContainSingle();
        summary.Techniques[0].Id.Should().Be("main");
        summary.Techniques[0].PassCount.Should().Be(1);
    }

    [Fact]
    public void Capabilities_Are_Stable()
    {
        ShaderCapabilities.Id.Should().Be("asset/shader");
        ShaderCapabilities.Package.Should().Be("Axrone.AssetShader");
        ShaderCapabilities.OwnerPackage.Should().Be("Axrone.Asset");
    }

    [Fact]
    public void Importer_Unwraps_Effect_And_Validates()
    {
        var importer = new ShaderEffectJsonImporter();
        const string wrapped = """{"effect":{"id":"w","vertex":{"main":"v"},"fragment":{"main":"f"}}}""";
        importer.Import(wrapped, "w.effect.json").Id.Name.Should().Be("w");

        const string badFormat = """{"format":"other","id":"w","vertex":{"main":"v"},"fragment":{"main":"f"}}""";
        Action format = () => importer.Import(badFormat, "w.effect.json");
        format.Should().Throw<InvalidOperationException>();

        const string v1WithKeywords = """{"version":1,"id":"w","vertex":{"main":"v"},"fragment":{"main":"f"},"keywords":[{"name":"K"}]}""";
        Action version = () => importer.Import(v1WithKeywords, "w.effect.json");
        version.Should().Throw<InvalidOperationException>();

        const string noId = """{"vertex":{"main":"v"},"fragment":{"main":"f"}}""";
        importer.Import(noId, "dir/custom.effect.json").Id.Name.Should().Be("custom");
    }
}
