using Axrone.Simd;
using FluentAssertions;
using Xunit;

namespace Axrone.Simd.Tests;

public sealed class SoaBufferTests
{
    [Fact]
    public void Vec3Buffer_SupportsElementAndBulkOperations()
    {
        using var a = new SoaVec3Buffer(4);
        using var b = new SoaVec3Buffer(4);
        using var target = new SoaVec3Buffer(4);
        a.SetCount(4);
        b.SetCount(4);
        target.SetCount(4);

        a.Set(0, 1f, 2f, 3f);
        b.Set(0, 4f, 5f, 6f);
        target.Add(0, a, 0, b, 0);
        target.Get(0).Should().Be((5f, 7f, 9f));

        target.Subtract(1, b, 0, a, 0);
        target.Get(1).Should().Be((3f, 3f, 3f));

        target.Multiply(2, a, 0, b, 0);
        target.Get(2).Should().Be((4f, 10f, 18f));

        target.Scale(3, a, 0, 2f);
        target.Get(3).Should().Be((2f, 4f, 6f));

        SoaVec3Buffer.Dot(a, 0, b, 0).Should().Be(32f);
        target.Cross(0, a, 0, b, 0);
        target.Get(0).Should().Be((-3f, 6f, -3f));

        a.Set(1, 0f, 3f, 4f);
        a.Length(1).Should().BeApproximately(5f, 1e-6f);
        a.Normalize(1, a, 1);
        a.Length(1).Should().BeApproximately(1f, 1e-6f);

        target.LerpAll(a, b, 0.5f);
        target.Get(0).X.Should().BeApproximately((a.Get(0).X + b.Get(0).X) * 0.5f, 1e-6f);
    }

    [Fact]
    public void Vec3Buffer_GrowsAndPreservesElements()
    {
        using var buffer = new SoaVec3Buffer(2);
        buffer.SetCount(2);
        buffer.Set(0, 1f, 2f, 3f);
        buffer.Set(1, 4f, 5f, 6f);

        buffer.SetCount(8);

        buffer.Count.Should().Be(8);
        buffer.Capacity.Should().BeGreaterThanOrEqualTo(8);
        buffer.Get(0).Should().Be((1f, 2f, 3f));
        buffer.Get(1).Should().Be((4f, 5f, 6f));
    }

    [Fact]
    public void QuatBuffer_MultipliesRotations()
    {
        using var a = new SoaQuatBuffer(2);
        using var b = new SoaQuatBuffer(2);
        using var target = new SoaQuatBuffer(2);
        a.SetCount(2);
        b.SetCount(2);
        target.SetCount(2);

        float half = MathF.Sin(MathF.PI / 4f);
        float w = MathF.Cos(MathF.PI / 4f);
        a.Set(0, 0f, half, 0f, w);
        b.Set(0, 0f, half, 0f, w);

        target.Multiply(0, a, 0, b, 0);
        (float X, float Y, float Z, float W) result = target.Get(0);
        result.X.Should().BeApproximately(0f, 1e-5f);
        result.Y.Should().BeApproximately(1f, 1e-5f);
        result.Z.Should().BeApproximately(0f, 1e-5f);
        result.W.Should().BeApproximately(0f, 1e-5f);
    }

    [Fact]
    public void QuatBuffer_SlerpAndApplyToVec3()
    {
        using var a = new SoaQuatBuffer(2);
        using var b = new SoaQuatBuffer(2);
        using var target = new SoaQuatBuffer(2);
        using var vector = new SoaVec3Buffer(2);
        using var rotated = new SoaVec3Buffer(2);
        a.SetCount(2);
        b.SetCount(2);
        target.SetCount(2);
        vector.SetCount(2);
        rotated.SetCount(2);

        a.SetIdentity(0);
        b.Set(0, 0f, 1f, 0f, 0f);
        target.Slerp(0, a, 0, b, 0, 0.5f);
        target.Normalize(0, target, 0);
        MathF.Abs(target.Get(0).Y).Should().BeApproximately(MathF.Sqrt(0.5f), 1e-4f);
        MathF.Abs(target.Get(0).W).Should().BeApproximately(MathF.Sqrt(0.5f), 1e-4f);

        float half = MathF.Sqrt(0.5f);
        target.Set(0, 0f, half, 0f, half);
        vector.Set(0, 1f, 0f, 0f);
        target.ApplyToVec3(0, rotated, 0, vector, 0);
        rotated.Get(0).X.Should().BeApproximately(0f, 1e-4f);
        rotated.Get(0).Z.Should().BeApproximately(-1f, 1e-4f);
    }

    [Fact]
    public void PoseBuffer_ResetsAndLerps()
    {
        using var pose = new SoaPoseBuffer(3);
        using var other = new SoaPoseBuffer(3);
        using var blended = new SoaPoseBuffer(3);

        pose.Bone(1).SetTranslation(1f, 2f, 3f);
        pose.Bone(1).SetScale(2f, 2f, 2f);
        other.Bone(1).SetTranslation(5f, 6f, 7f);
        other.Bone(1).SetScale(4f, 4f, 4f);

        blended.LerpAll(pose, other, 0.5f);

        blended.Translations.Get(1).Should().Be((3f, 4f, 5f));
        blended.Scales.Get(1).Should().Be((3f, 3f, 3f));
        blended.Bone(1).Pose.Should().BeSameAs(blended);

        blended.ResetToRest();
        blended.Translations.Get(1).Should().Be((0f, 0f, 0f));
        blended.Rotations.Get(1).Should().Be((0f, 0f, 0f, 1f));
        blended.Scales.Get(1).Should().Be((1f, 1f, 1f));
    }

    [Fact]
    public void MatrixPalette_ComposesAndMultiplies()
    {
        using var pose = new SoaPoseBuffer(1);
        using var identityPose = new SoaPoseBuffer(1);
        using var palette = new SoaMatrixPalette(1);
        using var parent = new SoaMatrixPalette(1);

        pose.Bone(0).SetTranslation(1f, 2f, 3f);
        pose.Bone(0).SetScale(2f, 2f, 2f);
        palette.ComposeFrom(pose);

        Span<float> matrix = palette.Slice(0);
        matrix[0].Should().BeApproximately(2f, 1e-5f);
        matrix[5].Should().BeApproximately(2f, 1e-5f);
        matrix[10].Should().BeApproximately(2f, 1e-5f);
        matrix[3].Should().BeApproximately(1f, 1e-5f);
        matrix[7].Should().BeApproximately(2f, 1e-5f);
        matrix[11].Should().BeApproximately(3f, 1e-5f);

        parent.ComposeFrom(identityPose);
        palette.MultiplyParent(parent);
        palette.Slice(0)[3].Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void MatrixInverse_RoundTripsIdentity()
    {
        Span<float> source = stackalloc float[16];
        Span<float> inverse = stackalloc float[16];
        Span<float> product = stackalloc float[16];

        SoaMat4Ops.Compose(source, 0, 4f, -2f, 7f, 0f, 0f, 0f, 1f, 1f, 1f, 1f);
        SoaMat4Ops.TryInvert(inverse, 0, source, 0).Should().BeTrue();
        SoaMat4Ops.Multiply(product, 0, source, 0, inverse, 0);

        for (var index = 0; index < 16; index++)
        {
            float expected = index % 5 == 0 ? 1f : 0f;
            product[index].Should().BeApproximately(expected, 1e-4f);
        }
    }

    [Fact]
    public void ScratchPool_SlotsAreDistinctAndClearable()
    {
        SoaScratchPool pool = SoaScratchPool.Shared;
        pool.Vec3(0)[0] = 3f;
        pool.Quat(0)[3] = 1f;
        pool.Mat4(0)[0] = 1f;

        pool.Vec3(0)[0].Should().Be(3f);
        pool.Quat(0)[3].Should().Be(1f);
        pool.Mat4(0)[0].Should().Be(1f);

        pool.Clear();
        pool.Vec3(0)[0].Should().Be(0f);
    }

    [Fact]
    public void SteadyStateOperationsDoNotAllocate()
    {
        using var a = new SoaVec3Buffer(16);
        using var b = new SoaVec3Buffer(16);
        using var target = new SoaVec3Buffer(16);
        a.SetCount(16);
        b.SetCount(16);
        target.SetCount(16);

        target.LerpAll(a, b, 0.5f);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
        {
            target.LerpAll(a, b, 0.25f);
            SoaVec3Buffer.Dot(a, 0, b, 0);
            target.Normalize(0, a, 0);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();

        (after - before).Should().Be(0);
    }
}
