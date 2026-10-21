namespace Axrone.AssetShader.Tests;

public sealed class ShaderEffectTests
{
    private static ShaderEffectDefinition BaseEffect() =>
        ShaderPipelineBuilder<StageUninitialized>.Begin("lit", 2)
            .WithVertex(new ShaderStageDefinition("gl_Position = vec4(0.0);"))
            .WithFragment(new ShaderStageDefinition("gl_FragColor = vec4(1.0);"))
            .WithProperties(ImmutableArray.Create(new ShaderPropertyDefinition(new ShaderSymbol("albedo"), "vec3")))
            .WithKeywords(ImmutableArray.Create(new ShaderKeywordDefinition(new ShaderSymbol("FOG"))))
            .Seal()
            .BuildEffect()
            .Definition;

    [Fact]
    public void Pipeline_Builds_Full_Effect()
    {
        ShaderEffectDefinition effect = BaseEffect();
        effect.Id.Name.Should().Be("lit");
        effect.Version.Should().Be(2);
        effect.Vertex.Main.Should().Contain("gl_Position");
        effect.Properties.Should().ContainSingle(p => p.Name.Name == "albedo");
        effect.Keywords.Should().ContainSingle(k => k.Name.Name == "FOG");
        effect.Keywords[0].IsToggle.Should().BeTrue();
    }

    [Fact]
    public void Composer_Merges_And_Overrides()
    {
        ShaderEffectDefinition effect = BaseEffect();
        ShaderEffectDefinition extended = ShaderComposer.ExtendShaderEffect(
            in effect,
            new PartialShaderEffectOverride(
                Version: 3,
                Vertex: new PartialShaderStageOverride(Main: "  gl_Position = vec4(1.0);\n"),
                Properties: ImmutableArray.Create(new ShaderPropertyDefinition(new ShaderSymbol("roughness"), "float"))));

        extended.Version.Should().Be(3);
        extended.Vertex.Main.Should().Contain("gl_Position = vec4(1.0);");
        extended.Properties.Should().HaveCount(2);
        extended.Id.Should().Be(effect.Id);
    }

    [Fact]
    public void Composer_Replaces_Same_Named_Entries()
    {
        ShaderEffectDefinition effect = BaseEffect();
        ShaderEffectDefinition extended = ShaderComposer.ExtendShaderEffect(
            in effect,
            new PartialShaderEffectOverride(
                Properties: ImmutableArray.Create(new ShaderPropertyDefinition(new ShaderSymbol("albedo"), "vec4"))));

        extended.Properties.Should().ContainSingle();
        extended.Properties[0].Type.Should().Be("vec4");
    }

    [Fact]
    public void Dedent_Removes_Common_Indent()
    {
        string code = "    line1\n      line2\n    line3\n";
        GLSL.Dedent(code).Should().Be("line1\n  line2\nline3");

        GLSL.Dedent(string.Empty).Should().BeEmpty();
        GLSL.Dedent("flat").Should().Be("flat");
    }

    [Fact]
    public void Line_Enumerator_Splits_CrLf_And_Lf()
    {
        var enumerator = new SpanLineEnumerator("a\r\nb\nc".AsSpan());
        var lines = new List<string>();
        while (enumerator.MoveNext())
        {
            lines.Add(new string(enumerator.Current));
        }

        lines.Should().Equal("a", "b", "c");
        enumerator.LineNumber.Should().Be(3);
    }
}
