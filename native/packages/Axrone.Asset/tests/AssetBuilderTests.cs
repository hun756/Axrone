namespace Axrone.Asset.Tests;

public sealed class AssetBuilderTests
{
    [Fact]
    public void Builder_Produces_Valid_Write_Input()
    {
        AssetWriteInput<string> input = AssetEnvelopeBuilder<string, AssetStage.Initial>
            .Start("clip", new AssetKey("sfx/boom"), "data")
            .WithMetadata(new AssetMetadata { MimeType = "text/plain" })
            .Validate()
            .Build();

        input.Kind.Should().Be("clip");
        input.Key.Value.Should().Be("sfx/boom");
        input.Data.Should().Be("data");
    }

    [Fact]
    public void Builder_Rejects_Empty_Kind()
    {
        Action invalid = () => AssetEnvelopeBuilder<string, AssetStage.Initial>
            .Start(string.Empty, new AssetKey("sfx/boom"), "data")
            .Validate();
        invalid.Should().Throw<AssetConfigurationError>();
    }
}
