namespace Axrone.AssetGltf.Tests;

public sealed class GltfIdsTests
{
    [Fact]
    public void NominalIds_InvalidSentinel_IsNotValid()
    {
        AccessorId.Invalid.IsValid.Should().BeFalse();
        BufferId.Invalid.IsValid.Should().BeFalse();
        BufferViewId.Invalid.IsValid.Should().BeFalse();
        MeshId.Invalid.IsValid.Should().BeFalse();
        MaterialId.Invalid.IsValid.Should().BeFalse();
        TextureId.Invalid.IsValid.Should().BeFalse();
        SamplerId.Invalid.IsValid.Should().BeFalse();
        ImageId.Invalid.IsValid.Should().BeFalse();
        SkinId.Invalid.IsValid.Should().BeFalse();
        AnimationId.Invalid.IsValid.Should().BeFalse();
        NodeId.Invalid.IsValid.Should().BeFalse();
        SceneId.Invalid.IsValid.Should().BeFalse();
    }

    [Fact]
    public void NominalIds_CompareAndToString()
    {
        var a = new AccessorId(2);
        var b = new AccessorId(5);
        a.IsValid.Should().BeTrue();
        (a < b).Should().BeTrue();
        (b > a).Should().BeTrue();
        (a <= b).Should().BeTrue();
        (b >= a).Should().BeTrue();
        a.CompareTo(b).Should().BeLessThan(0);
        a.ToString().Should().Be("2");

        var n0 = new NodeId(0);
        var n1 = new NodeId(1);
        (n0 < n1).Should().BeTrue();
        n1.ToString().Should().Be("1");
    }

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
