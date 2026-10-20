namespace Axrone.Asset.Tests;

public sealed class AssetIdTests
{
    [Fact]
    public void CreateNew_Is_Nonzero_And_Unique()
    {
        AssetId first = AssetId.CreateNew();
        AssetId second = AssetId.CreateNew();
        first.Should().NotBe(AssetId.Empty);
        first.Should().NotBe(second);
        first.Value.Should().NotBe(0UL);
    }

    [Fact]
    public void Hex_RoundTrip_With_Prefix()
    {
        var id = new AssetId(0xABCDEF1234567890UL);
        id.ToString().Should().Be("abcdef1234567890");

        AssetId parsed = AssetId.Parse("ast_abcdef1234567890", CultureInfo.InvariantCulture);
        parsed.Should().Be(id);

        AssetId.TryParse("abcdef1234567890".AsSpan(), CultureInfo.InvariantCulture, out AssetId direct).Should().BeTrue();
        direct.Should().Be(id);
    }

    [Fact]
    public void NonHex_Falls_Back_To_Hash()
    {
        AssetId.TryParse("sfx/boom".AsSpan(), CultureInfo.InvariantCulture, out AssetId hashed).Should().BeTrue();
        hashed.Value.Should().NotBe(0UL);
        hashed.Should().Be(AssetId.Parse("sfx/boom".AsSpan(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Key_Holds_Value_And_Hash()
    {
        var key = new AssetKey("sfx/boom");
        key.Value.Should().Be("sfx/boom");
        key.Hash.Should().NotBe(0UL);
        key.CompareTo(new AssetKey("sfx/boom")).Should().Be(0);
        key.ToString().Should().Be("sfx/boom");
    }

    [Fact]
    public void Uri_Locale_Importer_Source_Hold_Values()
    {
        new AssetUri("file://x/y").Value.Should().Be("file://x/y");
        new AssetLocale("en").Value.Should().Be("en");
        AssetLocale.Invariant.Value.Should().Be("invariant");
        Action blank = () => { _ = new AssetImporterId(string.Empty); };
        blank.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void References_Format_Web_Tokens()
    {
        var id = new AssetId(0x1234UL);
        var reference = new AssetReference<string>(id);
        reference.ToString().Should().Be("asset:string:0000000000001234");

        var versioned = new AssetVersionedReference<string>(id, new AssetRevision(3L));
        versioned.ToString().Should().Be("asset:string:0000000000001234@3");
        versioned.Revision.Should().Be(new AssetRevision(3L));
    }

    [Fact]
    public void Selectors_Parse_Tokens_Keys_And_Ids()
    {
        AssetSelector byToken = AssetSelector.Parse("asset:clip:abcdef1234567890".AsSpan());
        byToken.Kind.Should().Be(SelectorKind.Id);
        byToken.Id.Should().Be(AssetId.Parse("abcdef1234567890".AsSpan(), CultureInfo.InvariantCulture));

        AssetSelector keyed = AssetSelector.FromKey(new AssetKey("sfx/boom"), kind: "clip");
        keyed.Kind.Should().Be(SelectorKind.Key);
        keyed.Key.Value.Should().Be("sfx/boom");

        AssetSelector byId = new AssetId(7UL);
        byId.Kind.Should().Be(SelectorKind.Id);
        byId.Id.Value.Should().Be(7UL);

        AssetSelector implicitKey = new AssetKey("music/theme");
        implicitKey.Kind.Should().Be(SelectorKind.Key);
    }
}
