using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Axrone.Patterns;

namespace Axrone.Patterns.Tests;

file sealed class Ctx
{
    public List<int> Seen { get; } = new();
    public bool FallbackRan;
}

file readonly struct RecordPolicy : IVisitorPolicy<int, Ctx, Unit>
{
    public static Unit Visit(in int node, ref Ctx context)
    {
        context.Seen.Add(node);
        return Unit.Value;
    }
}

file readonly struct DoublePolicy : IVisitorPolicy<int, Ctx, Unit>
{
    public static Unit Visit(in int node, ref Ctx context)
    {
        context.Seen.Add(node * 2);
        return Unit.Value;
    }
}

file readonly struct EvenPolicy : IFallbackVisitorPolicy<int, Ctx, int>
{
    public static bool TryVisit(in int node, ref Ctx context, out int result)
    {
        if ((node & 1) == 0)
        {
            result = node * 10;
            return true;
        }

        result = default;
        return false;
    }
}

file readonly struct ConstantPolicy : IVisitorPolicy<int, Ctx, int>
{
    public static int Visit(in int node, ref Ctx context)
    {
        context.FallbackRan = true;
        return -1;
    }
}

file struct RecordingVisitor : IVisitor<int, Ctx, Unit>
{
    public int Scale;

    public Unit Visit(in int node, ref Ctx context)
    {
        context.Seen.Add(node * Scale);
        return Unit.Value;
    }
}

file struct FlagTryVisitor : IFallbackVisitorPolicy<int, Ctx, int>
{
    public static bool TryVisit(in int node, ref Ctx context, out int result)
    {
        if (node > 0)
        {
            result = node + 1;
            return true;
        }

        result = default;
        return false;
    }
}

file struct FallbackValueVisitor : IVisitor<int, Ctx, int>
{
    public int Visit(in int node, ref Ctx context)
    {
        context.FallbackRan = true;
        return -100;
    }
}

public sealed class VisitorPolicyTests
{
    [Fact]
    public void ChainedPolicy_RunsBothInOrder()
    {
        var context = new Ctx();

        ChainedPolicy<RecordPolicy, DoublePolicy, int, Ctx>.Visit(21, ref context);

        context.Seen.Should().Equal(21, 42);
    }

    [Fact]
    public void FallbackPolicy_Hit_SkipsFallback()
    {
        var context = new Ctx();

        int result = FallbackPolicy<EvenPolicy, ConstantPolicy, int, Ctx, int>.Visit(4, ref context);

        result.Should().Be(40);
        context.FallbackRan.Should().BeFalse();
    }

    [Fact]
    public void FallbackPolicy_Miss_RunsFallback()
    {
        var context = new Ctx();

        int result = FallbackPolicy<EvenPolicy, ConstantPolicy, int, Ctx, int>.Visit(5, ref context);

        result.Should().Be(-1);
        context.FallbackRan.Should().BeTrue();
    }

    [Fact]
    public void ChainedVisitor_RunsBoth()
    {
        var context = new Ctx();
        var chained = new ChainedVisitor<RecordingVisitor, RecordingVisitor, int, Ctx>(
            new RecordingVisitor { Scale = 1 }, new RecordingVisitor { Scale = 100 });

        chained.Visit(3, ref context);

        context.Seen.Should().Equal(3, 300);
    }

    [Fact]
    public void FallbackVisitor_Hit_ReturnsPrimary()
    {
        var context = new Ctx();
        var visitor = new FallbackVisitor<FlagTryVisitor, FallbackValueVisitor, int, Ctx, int>(
            new FlagTryVisitor(), new FallbackValueVisitor());

        visitor.Visit(7, ref context).Should().Be(8);
        context.FallbackRan.Should().BeFalse();
    }

    [Fact]
    public void FallbackVisitor_Miss_ReturnsFallback()
    {
        var context = new Ctx();
        var visitor = new FallbackVisitor<FlagTryVisitor, FallbackValueVisitor, int, Ctx, int>(
            new FlagTryVisitor(), new FallbackValueVisitor());

        visitor.Visit(-2, ref context).Should().Be(-100);
        context.FallbackRan.Should().BeTrue();
    }

    [Fact]
    public unsafe void FoldVisitor_Accumulates()
    {
        var context = new Ctx();
        int total = 0;
        var visitor = new FoldVisitor<int, int, Ctx>(ref total, &Sum);

        visitor.Visit(4, ref context);
        visitor.Visit(6, ref context);

        total.Should().Be(10);

        static void Sum(ref int acc, in int node, ref Ctx ctx) => acc += node;
    }

    [Fact]
    public void DefaultFilter_AllowsEverything()
    {
        var context = new Ctx();

        DefaultTraversalFilter<int, Ctx>.ShouldTraverse(1, ref context).Should().BeTrue();
        DefaultTraversalFilter<int, Ctx>.ShouldDescend(1, ref context).Should().BeTrue();
    }
}
