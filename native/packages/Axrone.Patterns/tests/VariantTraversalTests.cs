using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;
using Axrone.Patterns;

namespace Axrone.Patterns.Tests;

// ---------------------------------------------------------------------------
// Worked-example consumer: a tiny expression AST living on a sealed arena.
// This is intentionally test-local: the shipped surface stays fully generic,
// and the variant family proves the policies, traversals and dispatch tables
// against a real hierarchy.
// ---------------------------------------------------------------------------

enum VariantNodeKind : int
{
    Invalid = -1,
    Int64Literal = 0,
    UnaryOperator = 1,
    BinaryOperator = 2,
    Float64Literal = 3,
    RangeReference = 4
}

[StructLayout(LayoutKind.Explicit, Size = 32)]
internal readonly struct VariantNode : IEquatable<VariantNode>
{
    [FieldOffset(0)] public readonly NodeTypeId Type;
    [FieldOffset(4)] public readonly NodeHandle Handle;

    [FieldOffset(8)] public readonly long Int64Value;
    [FieldOffset(8)] public readonly double Float64Value;
    [FieldOffset(8)] public readonly NodeHandle UnaryChild;
    [FieldOffset(8)] public readonly NodeHandle BinaryLeft;

    [FieldOffset(12)] public readonly NodeHandle BinaryRight;
    [FieldOffset(16)] public readonly int Tag;
    [FieldOffset(20)] public readonly int Reserved;
    [FieldOffset(24)] public readonly long ExtraPayload;

    public static VariantNode CreateInt64(NodeTypeId type, NodeHandle handle, long value, int tag = 0)
    {
        Unsafe.SkipInit(out VariantNode node);
        Unsafe.AsRef(in node.Type) = type;
        Unsafe.AsRef(in node.Handle) = handle;
        Unsafe.AsRef(in node.Int64Value) = value;
        Unsafe.AsRef(in node.Tag) = tag;
        Unsafe.AsRef(in node.Reserved) = 0;
        return node;
    }

    public static VariantNode CreateBinary(NodeTypeId type, NodeHandle handle, NodeHandle left, NodeHandle right, int tag = 0)
    {
        Unsafe.SkipInit(out VariantNode node);
        Unsafe.AsRef(in node.Type) = type;
        Unsafe.AsRef(in node.Handle) = handle;
        Unsafe.AsRef(in node.BinaryLeft) = left;
        Unsafe.AsRef(in node.BinaryRight) = right;
        Unsafe.AsRef(in node.Tag) = tag;
        Unsafe.AsRef(in node.Reserved) = 0;
        return node;
    }

    public bool Equals(VariantNode other)
    {
        ref long selfLong = ref Unsafe.As<VariantNode, long>(ref Unsafe.AsRef(in this));
        ref long otherLong = ref Unsafe.As<VariantNode, long>(ref Unsafe.AsRef(in other));
        return selfLong == otherLong
            && Unsafe.Add(ref selfLong, 1) == Unsafe.Add(ref otherLong, 1)
            && Unsafe.Add(ref selfLong, 2) == Unsafe.Add(ref otherLong, 2)
            && Unsafe.Add(ref selfLong, 3) == Unsafe.Add(ref otherLong, 3);
    }

    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is VariantNode other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Type, Handle, Int64Value, Tag);

    public static bool operator ==(in VariantNode left, in VariantNode right) => left.Equals(right);
    public static bool operator !=(in VariantNode left, in VariantNode right) => !left.Equals(right);
}

internal readonly struct VariantNodeHierarchy : INodeHierarchy<VariantNode>
{
    public static ReadOnlySpan<NodeHandle> GetChildren(in VariantNode node)
    {
        if ((VariantNodeKind)node.Type.Value == VariantNodeKind.UnaryOperator)
        {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in node.UnaryChild), 1);
        }
        if ((VariantNodeKind)node.Type.Value == VariantNodeKind.BinaryOperator)
        {
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in node.BinaryLeft), 2);
        }
        return ReadOnlySpan<NodeHandle>.Empty;
    }
}

internal interface IVariantVisitor<TContext, TResult>
    where TContext : allows ref struct
    where TResult : allows ref struct
{
    TResult VisitInt64(in VariantNode node, ref TContext context);
    TResult VisitBinary(in VariantNode node, ref TContext context);
    TResult VisitFallback(in VariantNode node, ref TContext context);
}

internal static class VariantNodeDispatcher
{
    public static TResult DispatchExhaustive<TVisitor, TContext, TResult>(
        in VariantNode node,
        ref TVisitor visitor,
        ref TContext context)
        where TVisitor : IVariantVisitor<TContext, TResult>, allows ref struct
        where TContext : allows ref struct
        where TResult : allows ref struct
    {
        return (VariantNodeKind)node.Type.Value switch
        {
            VariantNodeKind.Int64Literal => visitor.VisitInt64(in node, ref context),
            VariantNodeKind.BinaryOperator => visitor.VisitBinary(in node, ref context),
            _ => visitor.VisitFallback(in node, ref context)
        };
    }
}

internal sealed class EvalContext
{
    public Stack<long> Stack { get; } = new();
    public int Visited;
}

internal static class EvalShim
{
    public sealed class ShimVisitor : IVariantVisitor<EvalContext, Unit>
    {
        public static readonly ShimVisitor Instance = new();
        public Unit VisitInt64(in VariantNode node, ref EvalContext context)
        {
            context.Visited++;
            context.Stack.Push(node.Int64Value);
            return Unit.Value;
        }
        public Unit VisitBinary(in VariantNode node, ref EvalContext context)
        {
            context.Visited++;
            long right = context.Stack.Pop();
            long left = context.Stack.Pop();
            context.Stack.Push(node.Tag == 0 ? left + right : left * right);
            return Unit.Value;
        }
        public Unit VisitFallback(in VariantNode node, ref EvalContext context) =>
            throw new NotSupportedException($"Unsupported node kind {(VariantNodeKind)node.Type.Value}.");
    }
}

internal readonly struct CountPolicy : IVisitorPolicy<VariantNode, EvalContext, Unit>
{
    public static Unit Visit(in VariantNode node, ref EvalContext context)
    {
        context.Visited++;
        return Unit.Value;
    }
}

internal readonly struct SkipLiteralsFilter : ITraversalFilter<VariantNode, EvalContext>
{
    public static bool ShouldTraverse(in VariantNode node, ref EvalContext context) =>
        (VariantNodeKind)node.Type.Value != VariantNodeKind.Int64Literal;

    public static bool ShouldDescend(in VariantNode node, ref EvalContext context) => true;
}

public sealed class VariantTraversalTests : IDisposable
{
    private NativeNodeArena<VariantNode, WritablePhase>? _arena;

    public void Dispose() => _arena?.Dispose();

    // Builds ((3 + 4) * 2); tag 0 = add, tag 1 = mul.
    private (NativeNodeArena<VariantNode, SealedPhase> Sealed, NodeHandle Root) BuildExpression()
    {
        _arena?.Dispose();
        var arena = new NativeNodeArenaBuilder<VariantNode>().WithCapacity(8).Build();
        _arena = arena;

        NodeTypeId lit = new(0);
        NodeTypeId bin = new(2);
        NodeHandle h0 = arena.Allocate(VariantNode.CreateInt64(lit, default, 3));
        NodeHandle h1 = arena.Allocate(VariantNode.CreateInt64(lit, default, 4));
        NodeHandle h2 = arena.Allocate(VariantNode.CreateBinary(bin, default, h0, h1, tag: 0));
        NodeHandle h3 = arena.Allocate(VariantNode.CreateInt64(lit, default, 2));
        NodeHandle root = arena.Allocate(VariantNode.CreateBinary(bin, default, h2, h3, tag: 1));

        return (arena.Seal(), root);
    }

    [Fact]
    public void Enumerator_YieldsPreOrder()
    {
        var (sealedArena, root) = BuildExpression();
        try
        {
            var handles = new List<int>();
            var values = new List<long>();
            var enumerator = sealedArena.EnumeratePreOrder<VariantNode, VariantNodeHierarchy>(root).GetEnumerator();
            while (enumerator.MoveNext())
            {
                handles.Add(enumerator.CurrentHandle.Value);
                VariantNode node = enumerator.Current;
                values.Add(node.Type.Value == 2 ? -1 : node.Int64Value);
            }

            handles.Should().Equal(4, 2, 0, 1, 3);
            values.Should().Equal(-1, -1, 3, 4, 2);
        }
        finally
        {
            sealedArena.Dispose();
            _arena = null;
        }
    }

    [Fact]
    public void PostOrderTraversal_EvaluatesExpression()
    {
        var (sealedArena, root) = BuildExpression();
        try
        {
            var context = new EvalContext();
            var shim = EvalShim.ShimVisitor.Instance;
            var evaluator = new PostOrderShimEvaluator(shim);

            IterativeTraversal<VariantNode, VariantNodeHierarchy>.TraverseDepthFirstPostOrder(
                sealedArena, root, ref evaluator, ref context);

            context.Stack.Should().ContainSingle().Which.Should().Be(14);
            context.Visited.Should().Be(5);
        }
        finally
        {
            sealedArena.Dispose();
            _arena = null;
        }
    }

    [Fact]
    public void PolicyTraversal_CountsAllNodes()
    {
        var (sealedArena, root) = BuildExpression();
        try
        {
            var context = new EvalContext();

            IterativeTraversal<VariantNode, VariantNodeHierarchy>.TraverseDepthFirstPreOrder<
                CountPolicy, DefaultTraversalFilter<VariantNode, EvalContext>, EvalContext, SealedPhase>(
                sealedArena, root, ref context);

            context.Visited.Should().Be(5);
        }
        finally
        {
            sealedArena.Dispose();
            _arena = null;
        }
    }

    [Fact]
    public void PolicyTraversal_FilterSkipsLiterals()
    {
        var (sealedArena, root) = BuildExpression();
        try
        {
            var context = new EvalContext();

            IterativeTraversal<VariantNode, VariantNodeHierarchy>.TraverseDepthFirstPreOrder<
                CountPolicy, SkipLiteralsFilter, EvalContext, SealedPhase>(
                sealedArena, root, ref context);

            context.Visited.Should().Be(2);
        }
        finally
        {
            sealedArena.Dispose();
            _arena = null;
        }
    }

    [Fact]
    public void BreadthFirstTraversal_VisitsLevelOrder()
    {
        var (sealedArena, root) = BuildExpression();
        try
        {
            var context = new EvalContext();
            var collector = new HandleCollector();

            IterativeTraversal<VariantNode, VariantNodeHierarchy>.TraverseBreadthFirst(
                sealedArena, root, ref collector, ref context);

            collector.Order.Should().Equal("T2V1", "T2V0", "T0V2", "T0V3", "T0V4");
        }
        finally
        {
            sealedArena.Dispose();
            _arena = null;
        }
    }

    [Fact]
    public void Dispatcher_DirectCall_EvaluatesLiteral()
    {
        var node = VariantNode.CreateInt64(new NodeTypeId(0), default, 9);
        var context = new EvalContext();
        var shim = EvalShim.ShimVisitor.Instance;

        VariantNodeDispatcher.DispatchExhaustive<EvalShim.ShimVisitor, EvalContext, Unit>(in node, ref shim, ref context);

        context.Stack.Should().ContainSingle().Which.Should().Be(9);
    }

    [Fact]
    public void Dispatcher_Fallback_Throws()
    {
        var node = VariantNode.CreateInt64(new NodeTypeId(4), default, 0);
        var context = new EvalContext();
        var shim = EvalShim.ShimVisitor.Instance;

        Action act = () => VariantNodeDispatcher.DispatchExhaustive<EvalShim.ShimVisitor, EvalContext, Unit>(in node, ref shim, ref context);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public unsafe void DispatchTable_ExecutesRegisteredKinds()
    {
        var context = new EvalContext();
        using var table = new VariantDispatchTable();
        table.Register(new NodeTypeId(0), &PushLiteral);
        table.Register(new NodeTypeId(2), &ApplyBinary);

        var (sealedArena, root) = BuildExpression();
        try
        {
            table.Execute(in sealedArena.Get(new NodeHandle(0)), ref context);
            table.Execute(in sealedArena.Get(new NodeHandle(1)), ref context);
            table.Execute(in sealedArena.Get(new NodeHandle(2)), ref context);
            table.Execute(in sealedArena.Get(new NodeHandle(3)), ref context);
            table.Execute(in sealedArena.Get(root), ref context);

            context.Stack.Should().ContainSingle().Which.Should().Be(14);
            table.RegisteredCount.Should().Be(2);
        }
        finally
        {
            sealedArena.Dispose();
            _arena = null;
        }

        static void PushLiteral(in VariantNode node, ref EvalContext ctx) => ctx.Stack.Push(node.Int64Value);

        static void ApplyBinary(in VariantNode node, ref EvalContext ctx)
        {
            long right = ctx.Stack.Pop();
            long left = ctx.Stack.Pop();
            ctx.Stack.Push(node.Tag == 0 ? left + right : left * right);
        }
    }

    [Fact]
    public void DispatchTable_UnregisteredKind_Throws()
    {
        var context = new EvalContext();
        using var table = new VariantDispatchTable();
        var node = VariantNode.CreateInt64(new NodeTypeId(4), default, 0);

        Action act = () => table.Execute(in node, ref context);

        act.Should().Throw<NotSupportedException>();
    }

    private struct PostOrderShimEvaluator : IVisitor<VariantNode, EvalContext, Unit>
    {
        private readonly EvalShim.ShimVisitor _shim;

        public PostOrderShimEvaluator(EvalShim.ShimVisitor shim) => _shim = shim;

        public Unit Visit(in VariantNode node, ref EvalContext context)
        {
            var shim = _shim;
            VariantNodeDispatcher.DispatchExhaustive<EvalShim.ShimVisitor, EvalContext, Unit>(in node, ref shim, ref context);
            return Unit.Value;
        }
    }

    private struct HandleCollector : IVisitor<VariantNode, EvalContext, Unit>
    {
        public readonly List<string> Order = new();

        public HandleCollector() { }

        public Unit Visit(in VariantNode node, ref EvalContext context)
        {
            long payload = (VariantNodeKind)node.Type.Value == VariantNodeKind.BinaryOperator
                ? node.Tag
                : node.Int64Value;
            Order.Add($"T{node.Type.Value}V{payload}");
            return Unit.Value;
        }
    }
}

internal sealed class VariantDispatchTable : IDisposable
{
    private unsafe delegate*<in VariantNode, ref EvalContext, void>* _slots;
    private readonly int _capacity;
    private int _registeredCount;
    private int _isDisposed;

    public int RegisteredCount => Volatile.Read(ref _registeredCount);

    public VariantDispatchTable(int maxTypes = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTypes);

        _capacity = maxTypes;
        unsafe
        {
            nuint byteCount = checked((nuint)maxTypes * (nuint)sizeof(delegate*<in VariantNode, ref EvalContext, void>));
            _slots = (delegate*<in VariantNode, ref EvalContext, void>*)NativeMemory.AlignedAlloc(byteCount, 64);
            NativeMemory.Clear(_slots, byteCount);
        }
    }

    public unsafe void Register(NodeTypeId typeId, delegate*<in VariantNode, ref EvalContext, void> handler)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);
        uint index = (uint)typeId.Value;
        if (index >= (uint)_capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(typeId));
        }

        if (_slots[index] == null && handler != null)
        {
            _registeredCount++;
        }
        else if (_slots[index] != null && handler == null)
        {
            _registeredCount--;
        }
        _slots[index] = handler;
    }

    public unsafe void Execute(in VariantNode node, ref EvalContext context)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);
        uint index = (uint)node.Type.Value;
        if (index < (uint)_capacity)
        {
            delegate*<in VariantNode, ref EvalContext, void> target = _slots[index];
            if (target != null)
            {
                target(in node, ref context);
                return;
            }
        }

        throw new NotSupportedException($"Encountered unregistered node type: {node.Type.Value}.");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            unsafe
            {
                if (_slots != null)
                {
                    NativeMemory.AlignedFree(_slots);
                    _slots = null;
                }
            }
            _registeredCount = 0;
        }
    }
}
