namespace Axrone.AssetGltf.Tests;

/// <summary>Fallback behavior of the dependency-free default policies.</summary>
public sealed class GltfPolicyTests
{
    [Fact]
    public void NoDracoPolicy_ReportsUnsupported()
    {
        NoDracoCompressionPolicy.IsSupported.Should().BeFalse();

        GltfDracoDecodeStatus status = NoDracoCompressionPolicy.TryDecodeMesh(
            [1, 2, 3],
            GltfDracoDecodeLimits.Unlimited,
            out GltfDracoDecodedMesh? mesh,
            out long decodedBytes);

        status.Should().Be(GltfDracoDecodeStatus.InvalidData);
        mesh.Should().BeNull();
        decodedBytes.Should().Be(0);
    }

    [Fact]
    public void NoMeshoptPolicy_ReportsUnsupported()
    {
        NoMeshoptCompressionPolicy.IsSupported.Should().BeFalse();

        NoMeshoptCompressionPolicy
            .TryDecompressBufferView([1], 1, 1, "ATTRIBUTES", "NONE", new byte[1])
            .Should().BeFalse();
    }

    [Fact]
    public void PassthroughTranscoder_IdentityOnly()
    {
        PassthroughTranscoderPolicy.PolicyId.Should().Be("Passthrough");
        PassthroughTranscoderPolicy
            .CanTranscode([1], GltfTextureFormat.R8G8B8A8_UNorm, GltfTextureFormat.R8G8B8A8_UNorm)
            .Should().BeTrue();
        PassthroughTranscoderPolicy
            .CanTranscode([1], GltfTextureFormat.R8G8B8A8_UNorm, GltfTextureFormat.BC7_RGBA)
            .Should().BeFalse();

        bool ok = PassthroughTranscoderPolicy.TryTranscode(
            new byte[] { 1, 2 },
            GltfTextureFormat.Unknown,
            GltfTextureFormat.Unknown,
            out GltfTranscodeOutput? output,
            out string? failureReason);

        ok.Should().BeTrue();
        output.Should().NotBeNull();
        output!.Value.Format.Should().Be(GltfTextureFormat.Unknown);
        failureReason.Should().BeNull();
    }

    [Fact]
    public void DracoDecodeLimits_RejectNegativeBudget()
    {
        Action act = () => _ = new GltfDracoDecodeLimits(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
