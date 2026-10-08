using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Mat4CoreTests
{
    [Fact]
    public void Layout_AndConstants_AreCorrect()
    {
        Marshal.SizeOf<Mat4>().Should().Be(64);
        Marshal.SizeOf<RowIndex>().Should().Be(1);
        Marshal.SizeOf<ColumnIndex>().Should().Be(1);
        Marshal.SizeOf<MatrixIndex>().Should().Be(1);
        Mat4.Identity.Should().Be(new Mat4(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f));
        Mat4.Zero.IsAllZero.Should().BeTrue();
        Mat4.AdditiveIdentity.Should().Be(Mat4.Zero);
        Mat4.MultiplicativeIdentity.Should().Be(Mat4.Identity);
    }

    [Fact]
    public void Ctors_CopyElements()
    {
        var m = new Mat4(
            1f, 2f, 3f, 4f,
            5f, 6f, 7f, 8f,
            9f, 10f, 11f, 12f,
            13f, 14f, 15f, 16f);
        float[] span = new float[16];
        m.CopyTo(span);
        span.Should().Equal(1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f, 13f, 14f, 15f, 16f);

        var fromRows = new Mat4(
            new Vec4(1f, 2f, 3f, 4f),
            new Vec4(5f, 6f, 7f, 8f),
            new Vec4(9f, 10f, 11f, 12f),
            new Vec4(13f, 14f, 15f, 16f));
        fromRows.Should().Be(m);

        var fromSpan = new Mat4(span);
        fromSpan.Should().Be(m);
        m.AsSpan().ToArray().Should().Equal(span);
        m.TryCopyTo(new float[4]).Should().BeFalse();
    }

    [Fact]
    public void Indexers_ReadAndWrite()
    {
        var m = Mat4.Identity;
        m[RowIndex.R1, ColumnIndex.C2].Should().Be(0f);
        m[MatrixIndex.From(5)].Should().Be(1f);
        m[1, 1].Should().Be(1f);
        m[5].Should().Be(1f);

        m[RowIndex.R0, ColumnIndex.C3] = 9f;
        m.M14.Should().Be(9f);
        m[MatrixIndex.From(RowIndex.R3, ColumnIndex.C0)] = 7f;
        m.M41.Should().Be(7f);
        m[2, 2] = 5f;
        m.M33.Should().Be(5f);
        m[15] = 3f;
        m.M44.Should().Be(3f);

        Action badRow = () => { _ = m[4, 0]; };
        badRow.Should().Throw<ArgumentOutOfRangeException>();
        Action badFlat = () => { _ = m[16]; };
        badFlat.Should().Throw<ArgumentOutOfRangeException>();
        Action badNominal = () => RowIndex.From(4);
        badNominal.Should().Throw<ArgumentOutOfRangeException>();
        Action badFlatNominal = () => MatrixIndex.From(16);
        badFlatNominal.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rows_Columns_Translation_RoundTrip()
    {
        var m = new Mat4(
            1f, 2f, 3f, 4f,
            5f, 6f, 7f, 8f,
            9f, 10f, 11f, 12f,
            13f, 14f, 15f, 16f);
        m.Row0.Should().Be(new Vec4(1f, 2f, 3f, 4f));
        m.Row3.Should().Be(new Vec4(13f, 14f, 15f, 16f));
        m.Column0.Should().Be(new Vec4(1f, 5f, 9f, 13f));
        m.Column3.Should().Be(new Vec4(4f, 8f, 12f, 16f));
        m.Translation.Should().Be(new Vec3(13f, 14f, 15f));

        m.Row1 = new Vec4(51f, 52f, 53f, 54f);
        m.M21.Should().Be(51f);
        m.M24.Should().Be(54f);
        m.Column2 = new Vec4(61f, 62f, 63f, 64f);
        m.M13.Should().Be(61f);
        m.M43.Should().Be(64f);
        m.Translation = new Vec3(71f, 72f, 73f);
        m.M41.Should().Be(71f);
        m.M43.Should().Be(73f);
    }

    [Fact]
    public void Predicates_Classify()
    {
        Mat4.Identity.IsIdentity.Should().BeTrue();
        Mat4.Zero.IsIdentity.Should().BeFalse();
        Mat4.Zero.IsAllZero.Should().BeTrue();
        Mat4.Identity.IsAllFinite.Should().BeTrue();
        new Mat4(float.NaN, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f).IsAnyNaN.Should().BeTrue();
        new Mat4(float.PositiveInfinity, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f).IsAnyInfinity.Should().BeTrue();
        Mat4.FromSystemNumerics(Mat4.Identity.ToSystemNumerics()).Should().Be(Mat4.Identity);
        Mat4.Identity.Trace().Should().Be(4f);
    }

    [Fact]
    public void NominalIndices_CompareAndDeconstruct()
    {
        (RowIndex.R0 < RowIndex.R3).Should().BeTrue();
        (ColumnIndex.C3 > ColumnIndex.C0).Should().BeTrue();
        MatrixIndex.From(RowIndex.R1, ColumnIndex.C2).Should().Be(MatrixIndex.From(6));
        var (row, col) = MatrixIndex.From(11);
        row.Should().Be(RowIndex.R2);
        col.Should().Be(ColumnIndex.C3);
    }
}
