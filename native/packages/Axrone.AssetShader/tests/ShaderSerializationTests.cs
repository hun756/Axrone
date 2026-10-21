namespace Axrone.AssetShader.Tests;

public sealed class ShaderSerializationTests
{
    private static ShaderEffectDefinition FullEffect() =>
        ShaderPipelineBuilder<StageUninitialized>.Begin("lit", 2)
            .WithVertex(new ShaderStageDefinition(
                "gl_Position = vec4(0.0);",
                Declarations: ImmutableArray.Create(new ShaderGlslBlock("uniform mat4 uModel;")),
                Precision: ShaderPrecision.Highp,
                Directives: ImmutableArray.Create("#version 300 es"),
                Inputs: ImmutableArray.Create(new ShaderInterfaceDefinition("position", "vec3", ShaderInterpolation.Flat)),
                Includes: ImmutableArray.Create(new ShaderSymbol("common"))))
            .WithFragment(new ShaderStageDefinition("gl_FragColor = vec4(1.0);"))
            .WithAttributes(ImmutableArray.Create(new ShaderAttributeDefinition("aPos", "vec3", 0)))
            .WithProperties(ImmutableArray.Create(
                new ShaderPropertyDefinition(
                    new ShaderSymbol("albedo"),
                    "vec3",
                    Scope: ShaderPropertyScope.Renderer,
                    DefaultValue: "1,1,1",
                    Kind: ShaderPropertyKind.Range,
                    Min: 0.0,
                    Max: 2.0,
                    Stages: ImmutableArray.Create(ShaderStageTag.Fragment),
                    Binding: "b0")))
            .WithKeywords(ImmutableArray.Create(
                new ShaderKeywordDefinition("MODE", Options: ImmutableArray.Create("LIT", "UNLIT"))))
            .WithTechniques(
                ImmutableArray.Create(new ShaderTechniqueDefinition(
                    new ShaderSymbol("main"),
                    ImmutableArray.Create(new ShaderPassDefinition(
                        new ShaderSymbol("p0"),
                        RenderState: new ShaderRenderState(BlendMode: "alpha"))),
                    Label: "Main")),
                new ShaderSymbol("main"))
            .Seal()
            .BuildEffect()
            .Definition;

    [Fact]
    public void RoundTrip_Preserves_Effect()
    {
        ShaderEffectDefinition effect = FullEffect();
        string json = ShaderSerialization.SerializeToJson(effect);
        json.Should().Contain("\"lit\"").And.Contain("\"highp\"").And.Contain("\"renderer\"");

        ShaderEffectDefinition restored = ShaderSerialization.DeserializeFromJson(json);
        restored.Id.Should().Be(effect.Id);
        restored.Version.Should().Be(2);
        restored.Vertex.Precision.Should().Be(ShaderPrecision.Highp);
        restored.Vertex.Inputs.Should().ContainSingle(i => i.Name.Name == "position" && i.Interpolation == ShaderInterpolation.Flat);
        restored.Properties.Should().ContainSingle(p => p.Name.Name == "albedo" && p.Scope == ShaderPropertyScope.Renderer);
        restored.Keywords.Should().ContainSingle(k => k.Name.Name == "MODE" && !k.IsToggle);
        restored.DefaultTechnique.Should().Be(new ShaderSymbol("main"));
        restored.Techniques.Should().ContainSingle(t => t.Label == "Main");
        restored.Techniques[0].Passes.Should().ContainSingle();
    }

    [Fact]
    public void Empty_Effect_RoundTrips()
    {
        ShaderEffectDefinition effect = ShaderPipelineBuilder<StageUninitialized>.Begin("flat")
            .WithVertex(new ShaderStageDefinition("a"))
            .WithFragment(new ShaderStageDefinition("b"))
            .Seal()
            .BuildEffect()
            .Definition;

        string json = ShaderSerialization.SerializeToJson(effect);
        ShaderEffectDefinition restored = ShaderSerialization.DeserializeFromJson(json);
        ShaderSerialization.SerializeToJson(restored).Should().Be(json);
        restored.Id.Should().Be(effect.Id);
        restored.Version.Should().Be(effect.Version);
        restored.Vertex.Main.Should().Be("a");
    }

    [Fact]
    public void Json_Importer_Parses_Effect()
    {
        string json = ShaderSerialization.SerializeToJson(FullEffect());
        var importer = new ShaderEffectJsonImporter();
        importer.Id.Name.Should().Be("asset-shader.effect.json");
        ShaderEffectDefinition effect = importer.Import(json, "lit.effect.json");
        effect.Id.Name.Should().Be("lit");
    }

    [Fact]
    public void Module_Importer_Wraps_Glsl()
    {
        var importer = new ShaderEffectModuleImporter();
        ShaderEffectDefinition effect = importer.Import("void main() {}", "basic.glsl");
        effect.Id.Name.Should().Be("basic");
        effect.Vertex.Main.Should().Be(effect.Fragment.Main);
    }

    [Fact]
    public void Pipeline_Routes_And_Rejects_Unknown()
    {
        var pipeline = new AssetImportPipeline(new ShaderEffectModuleImporter(), new ShaderEffectJsonImporter());
        ShaderEffectDefinition module = pipeline.Import("void main() {}", "basic.glsl");
        module.Id.Name.Should().Be("basic");

        string json = ShaderSerialization.SerializeToJson(FullEffect());
        pipeline.Import(json, "lit.effect.json").Id.Name.Should().Be("lit");

        Action unknown = () => pipeline.Import("x", "readme.txt");
        unknown.Should().Throw<InvalidOperationException>();
    }
}
