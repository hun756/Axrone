using System.Globalization;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class Mat4OpsTests
{
    private struct TraceProbe : IMatrixTransformAction<TraceProbe>
    {
        public float Trace;

        public static void Execute(ref readonly Mat4 matrix, ref TraceProbe state) =>
            state.Trace = matrix.Trace();
    }

    [Fact]
    public void Equality_Tolerance_Hash()
    {
        var a = Mat4.Identity;
        (a == Mat4.Identity).Should().BeTrue();
        (a != Mat4.Zero).Should().BeTrue();
        a.Equals(Mat4.Identity).Should().BeTrue();
        a.Equals((object)Mat4.Identity).Should().BeTrue();
        a.Equals((object)Mat4.Zero).Should().BeFalse();
        a.GetHashCode().Should().Be(Mat4.Identity.GetHashCode());

        var near = Mat4.Identity;
        near.M11 += Mat4.DefaultTolerance * 0.5f;
        a.Equals(near, Mat4.DefaultTolerance).Should().BeTrue();
        near.M11 += Mat4.DefaultTolerance * 2f;
        a.Equals(near, Mat4.DefaultTolerance).Should().BeFalse();
        a.Equals(near, 1f).Should().BeTrue();
    }

    [Fact]
    public void Format_RoundTrips_CharAndUtf8()
    {
        var m = new Mat4(
            1f, 2f, 3f, 4f,
            5f, 6f, 7f, 8f,
            9f, 10f, 11f, 12f,
            13f, 14f, 15f, 16f);
        string text = m.ToString(null, CultureInfo.InvariantCulture);
        Mat4.Parse(text, CultureInfo.InvariantCulture).Should().Be(m);
        Mat4.TryParse(text, CultureInfo.InvariantCulture, out Mat4 back).Should().BeTrue();
        back.Should().Be(m);

        Span<char> chars = stackalloc char[512];
        m.TryFormat(chars, out int written, default, CultureInfo.InvariantCulture).Should().BeTrue();
        Mat4.TryParse(chars.Slice(0, written), CultureInfo.InvariantCulture, out Mat4 backChars).Should().BeTrue();
        backChars.Should().Be(m);

        Span<byte> utf8 = stackalloc byte[512];
        m.TryFormat(utf8, out int bytesWritten, default, CultureInfo.InvariantCulture).Should().BeTrue();
        Mat4.TryParse(utf8.Slice(0, bytesWritten), CultureInfo.InvariantCulture, out Mat4 backUtf8).Should().BeTrue();
        backUtf8.Should().Be(m);

        Mat4.TryParse("1 2 3", CultureInfo.InvariantCulture, out _).Should().BeFalse();
        Mat4.TryParse((string?)null, CultureInfo.InvariantCulture, out Mat4 nil).Should().BeFalse();
        nil.Should().Be(Mat4.Zero);
        Action bad = () => Mat4.Parse("nope", CultureInfo.InvariantCulture);
        bad.Should().Throw<FormatException>();
        Action badUtf8 = () => Mat4.Parse("nope"u8.ToArray(), CultureInfo.InvariantCulture);
        badUtf8.Should().Throw<FormatException>();
    }

    [Fact]
    public void Enumerators_WalkElementsRowsColumns()
    {
        var m = Mat4.Identity;
        var elements = new System.Collections.Generic.List<float>();
        foreach (float f in m)
        {
            elements.Add(f);
        }
        elements.Should().HaveCount(16);
        elements[0].Should().Be(1f);
        elements[5].Should().Be(1f);
        elements[1].Should().Be(0f);

        var rows = new System.Collections.Generic.List<Vec4>();
        foreach (Vec4 r in m.Rows)
        {
            rows.Add(r);
        }
        rows.Should().HaveCount(4);
        rows[0].Should().Be(new Vec4(1f, 0f, 0f, 0f));
        rows[3].Should().Be(new Vec4(0f, 0f, 0f, 1f));

        var cols = new System.Collections.Generic.List<Vec4>();
        foreach (Vec4 c in m.Columns)
        {
            cols.Add(c);
        }
        cols.Should().HaveCount(4);
        cols[2].Should().Be(new Vec4(0f, 0f, 1f, 0f));
    }

    [Fact]
    public void Apply_DispatchesVisitor_AndSpanMutates()
    {
        var m = Mat4.CreateScale(2f);
        var probe = new TraceProbe();
        m.Apply<TraceProbe, TraceProbe>(ref probe);
        probe.Trace.Should().Be(2f + 2f + 2f + 1f);

        Span<float> writable = m.AsWritableSpan();
        writable[0] = 9f;
        m.M11.Should().Be(9f);
    }
}
