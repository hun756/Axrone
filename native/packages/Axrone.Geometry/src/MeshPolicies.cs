namespace Axrone.Geometry;

/// <summary>Triangle index emission order for one winding.</summary>
public interface IWindingPolicy
{
    /// <summary>Writes one triangle at <paramref name="offset"/>.</summary>
    static abstract void EmitTriangle<TIndex>(Span<TIndex> target, nuint offset, TIndex i0, TIndex i1, TIndex i2)
        where TIndex : unmanaged;
}

/// <summary>Counter-clockwise winding: indices pass through unchanged.</summary>
public readonly struct CounterClockwiseWinding : IWindingPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EmitTriangle<TIndex>(Span<TIndex> target, nuint offset, TIndex i0, TIndex i1, TIndex i2)
        where TIndex : unmanaged
    {
        target[(int)offset] = i0;
        target[(int)(offset + 1)] = i1;
        target[(int)(offset + 2)] = i2;
    }
}

/// <summary>Clockwise winding: the last two indices swap.</summary>
public readonly struct ClockwiseWinding : IWindingPolicy
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EmitTriangle<TIndex>(Span<TIndex> target, nuint offset, TIndex i0, TIndex i1, TIndex i2)
        where TIndex : unmanaged
    {
        target[(int)offset] = i0;
        target[(int)(offset + 1)] = i2;
        target[(int)(offset + 2)] = i1;
    }
}

/// <summary>Index width contract: GL type plus checked conversions.</summary>
/// <typeparam name="TIndex">The index element type.</typeparam>
public interface IIndexPolicy<TIndex> where TIndex : unmanaged, System.Numerics.IBinaryInteger<TIndex>
{
    /// <summary>The GL attribute type for this width.</summary>
    static abstract GLAttributeType AttributeType { get; }

    /// <summary>Narrows with overflow checking.</summary>
    static abstract TIndex FromUInt32(uint value);

    /// <summary>Widens.</summary>
    static abstract uint ToUInt32(TIndex value);
}

/// <summary>16-bit index policy.</summary>
public readonly struct UInt16IndexPolicy : IIndexPolicy<ushort>
{
    /// <inheritdoc/>
    public static GLAttributeType AttributeType => GLAttributeType.UnsignedShort;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort FromUInt32(uint value) => checked((ushort)value);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ToUInt32(ushort value) => value;
}

/// <summary>32-bit index policy.</summary>
public readonly struct UInt32IndexPolicy : IIndexPolicy<uint>
{
    /// <inheritdoc/>
    public static GLAttributeType AttributeType => GLAttributeType.UnsignedInt;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FromUInt32(uint value) => value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ToUInt32(uint value) => value;
}

/// <summary>Receives one vertex.</summary>
/// <typeparam name="TVertex">The vertex type.</typeparam>
public interface IVertexConsumer<TVertex> where TVertex : unmanaged
{
    /// <summary>Accepts <paramref name="vertex"/>.</summary>
    void Accept(in TVertex vertex);
}

/// <summary>Receives one triangle.</summary>
/// <typeparam name="TIndex">The index element type.</typeparam>
public interface ITriangleConsumer<TIndex> where TIndex : unmanaged
{
    /// <summary>Accepts one triangle.</summary>
    void Accept(TIndex i0, TIndex i1, TIndex i2);
}

/// <summary>Append-only mesh construction target.</summary>
public interface IMeshSink<TVertex, TIndex>
    where TVertex : unmanaged
    where TIndex : unmanaged
{
    /// <summary>Vertices appended so far.</summary>
    uint CurrentVertexCount { get; }

    /// <summary>Appends one vertex.</summary>
    void AppendVertex(in TVertex vertex);

    /// <summary>Appends one triangle.</summary>
    void AppendTriangle(TIndex i0, TIndex i1, TIndex i2);
}
