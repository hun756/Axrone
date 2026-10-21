namespace Axrone.AssetShader.Tests;

public sealed class ShaderSymbolTests
{
    [Fact]
    public void Same_Name_Same_Hash_Different_Name_Different_Hash()
    {
        var a = new ShaderSymbol("albedo".AsSpan());
        var b = new ShaderSymbol("albedo".AsSpan());
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());

        var c = new ShaderSymbol("normal".AsSpan());
        c.Should().NotBe(a);
    }

    [Fact]
    public void Name_Fallback_And_Operators()
    {
        ShaderSymbol named = "hero";
        named.Name.Should().Be("hero");

        var anonymous = new ShaderSymbol(0x1234UL);
        anonymous.Name.Should().Be("0x0000000000001234");
        anonymous.ToString().Should().Be(anonymous.Name);

        (named > anonymous || named < anonymous || named == anonymous).Should().BeTrue();
        (named != anonymous).Should().BeTrue();
        named.CompareTo(anonymous).Should().NotBe(0);
        named.Equals((object)named).Should().BeTrue();
    }

    [Fact]
    public void Locations_Compare_And_Format()
    {
        var a = new SourceLocation(3, 7);
        var b = new SourceLocation(3, 9);
        (a < b).Should().BeTrue();
        (b > a).Should().BeTrue();
        a.ToString().Should().Be("(3:7)");
        SourceLocation.Zero.Should().Be(default);
    }

    [Fact]
    public void VariantKey_Compares_And_Formats()
    {
        var key = new VariantKey(1UL, 2UL);
        key.ToString().Should().Be("00000000000000010000000000000002");
        (key > VariantKey.Default).Should().BeTrue();

        VariantKeyEqualityComparer.Instance.Equals(key, new VariantKey(1UL, 2UL)).Should().BeTrue();
        VariantKeyEqualityComparer.Instance.GetHashCode(key)
            .Should().Be(VariantKeyEqualityComparer.Instance.GetHashCode(new VariantKey(1UL, 2UL)));
    }

    [Fact]
    public void Sink_Collects_Policies_Gate_And_Clears()
    {
        var sink = new ShaderDiagnosticSink();
        sink.HasErrors.Should().BeFalse();

        sink.ReportWarning("W1", "careful");
        sink.ReportInfo("I1", "note");
        sink.HasErrors.Should().BeFalse();
        sink.Diagnostics.Should().HaveCount(2);

        sink.Report<StrictDiagnosticPolicy>(new ShaderDiagnostic("S", ShaderDiagnosticSeverity.Info, default, default, "strict"));
        sink.Diagnostics.Should().HaveCount(3);

        sink.Report<SilentDiagnosticPolicy>(new ShaderDiagnostic("S", ShaderDiagnosticSeverity.Warning, default, default, "gated"));
        sink.Diagnostics.Should().HaveCount(3);

        sink.ReportError("E1", "boom");
        sink.HasErrors.Should().BeTrue();

        sink.Clear();
        sink.HasErrors.Should().BeFalse();
        sink.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Diagnostic_Formats_With_Severity()
    {
        var diagnostic = new ShaderDiagnostic("CODE", ShaderDiagnosticSeverity.Error, new SourceLocation(1, 2), "src", "bad");
        diagnostic.ToString().Should().Be("[error] CODE: bad at src(1:2)");
    }

    [Fact]
    public void Preprocess_Exception_Carries_First_Message()
    {
        var diagnostics = ImmutableArray.Create(
            new ShaderDiagnostic("E", ShaderDiagnosticSeverity.Error, default, default, "first"),
            new ShaderDiagnostic("E", ShaderDiagnosticSeverity.Error, default, default, "second"));
        var ex = new ShaderPreprocessException(diagnostics);
        ex.Message.Should().Be("first");
        ex.Diagnostics.Should().HaveCount(2);

        var empty = new ShaderPreprocessException(ImmutableArray<ShaderDiagnostic>.Empty);
        empty.Message.Should().Be("Shader preprocessing error.");
    }
}
