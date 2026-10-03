using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;
using Axrone.Numeric;

namespace Axrone.Numeric.Tests;

public class QuatCoreTests
{
    [Fact]
    public void Layout_AndConstants_AreCorrect()
    {
        Marshal.SizeOf<Quat>().Should().Be(16);
        Quat.Identity.Should().Be(new Quat(0f, 0f, 0f, 1f));
        Quat.Zero.Should().Be(new Quat(0f, 0f, 0f, 0f));
        Quat.AdditiveIdentity.Should().Be(Quat.Zero);
        Quat.MultiplicativeIdentity.Should().Be(Quat.Identity);
    }

    [Fact]
    public void Ctors_Indexer_Classify()
    {
        new Quat(new Vec3(1f, 2f, 3f), 4f).Should().Be(new Quat(1f, 2f, 3f, 4f));
        var q = new Quat(1f, 2f, 3f, 4f);
        q[0].Should().Be(1f);
        q[3].Should().Be(4f);
        Action bad = () => { _ = q[4]; };
        bad.Should().Throw<Exception>();
        q.IsIdentity.Should().BeFalse();
        Quat.Identity.IsIdentity.Should().BeTrue();
        q.VectorPart.Should().Be(new Vec3(1f, 2f, 3f));
        (q.X, q.Y, q.Z, q.W).Should().Be((1f, 2f, 3f, 4f));
    }

    [Fact]
    public void Bridges_RoundTrip()
    {
        var q = new Quat(1f, -2f, 3f, -4f);
        Quat.FromSystemNumerics(q.ToSystemNumerics()).Should().Be(q);
        Quat.FromVector128(q.AsVector128()).Should().Be(q);
        (((float, float, float, float))q).Should().Be((1f, -2f, 3f, -4f));
        Vec4 v = (Vec4)q;
        v.Should().Be(new Vec4(1f, -2f, 3f, -4f));
        ((Quat)v).Should().Be(q);
        float[] arr = new float[4];
        q.CopyTo(arr);
        arr.Should().Equal(1f, -2f, 3f, -4f);
    }

    [Fact]
    public void Visitors_AndEnumerator()
    {
        var q = new Quat(1f, 2f, 3f, 4f);
        var count = new Counter();
        q.Inspect<CountVisitor, Counter>(ref count);
        count.Total.Should().Be(4);

        var sum = new Summer();
        q.Project<SumConsumer, Summer>(ref sum);
        sum.Total.Should().Be(10f);

        var seen = new System.Collections.Generic.List<float>();
        foreach (float c in q)
            seen.Add(c);
        seen.Should().Equal(1f, 2f, 3f, 4f);
    }

    private struct Counter
    {
        public int Total;
    }

    private readonly struct CountVisitor : IQuatVisitor<Counter>
    {
        public static void Visit(ref Counter state, float component, nuint index) => state.Total++;
    }

    private struct Summer
    {
        public float Total;
    }

    private readonly struct SumConsumer : IQuatSpanConsumer<Summer>
    {
        public static void Consume(ReadOnlySpan<float> components, ref Summer context)
        {
            foreach (float c in components)
                context.Total += c;
        }
    }
}
