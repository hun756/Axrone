namespace Axrone.AssetGltf.Tests;

public sealed class GltfValueTests
{
    [Fact]
    public void MeshBounds_Encapsulate_Expands()
    {
        var bounds = GltfMeshBounds.Empty;
        bounds = bounds.Encapsulate(new Vec3(1.0f, 2.0f, 3.0f));
        bounds = bounds.Encapsulate(new Vec3(-1.0f, 0.0f, 4.0f));
        bounds.Min.X.Should().Be(-1.0f);
        bounds.Min.Y.Should().Be(0.0f);
        bounds.Max.Z.Should().Be(4.0f);
    }

    [Fact]
    public void TextureTransform_Default_IsIdentity()
    {
        var t = GltfTextureTransform.Default;
        t.Offset.Should().Be(Vec2.Zero);
        t.Scale.Should().Be(Vec2.One);
        t.Rotation.Should().Be(0.0f);
        t.TexCoord.Should().Be(0);
    }
}
