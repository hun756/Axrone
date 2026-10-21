namespace Axrone.Numeric;

/// <summary>Mathematical color vector contract.</summary>
/// <typeparam name="TSelf">The concrete value type.</typeparam>
public interface IColorVector<TSelf> :
    IEquatable<TSelf>,
    IEqualityOperators<TSelf, TSelf, bool>
    where TSelf : struct, IColorVector<TSelf>
{
    /// <summary>First component.</summary>
    float Component0 { get; }

    /// <summary>Second component.</summary>
    float Component1 { get; }

    /// <summary>Third component.</summary>
    float Component2 { get; }

    /// <summary>Clamped copy.</summary>
    TSelf Clamp();
}

/// <summary>Static monomorphized zero-vtable color transformation.</summary>
/// <typeparam name="TSource">Input color struct.</typeparam>
/// <typeparam name="TDestination">Output color struct.</typeparam>
public interface IColorTransformer<TSource, TDestination>
    where TSource : struct
    where TDestination : struct
{
    /// <summary>Transforms one value.</summary>
    static abstract TDestination Transform(scoped in TSource source);

    /// <summary>Transforms a batch.</summary>
    static abstract void TransformBatch(ReadOnlySpan<TSource> source, Span<TDestination> destination);
}

/// <summary>Static compositor over hardware vector registers.</summary>
public interface IVectorCompositor
{
    /// <summary>Composites 512-bit lanes.</summary>
    static abstract Vector512<float> Composite(Vector512<float> source, Vector512<float> destination);

    /// <summary>Composites 256-bit lanes.</summary>
    static abstract Vector256<float> Composite(Vector256<float> source, Vector256<float> destination);

    /// <summary>Composites 128-bit lanes.</summary>
    static abstract Vector128<float> Composite(Vector128<float> source, Vector128<float> destination);

    /// <summary>Composites one scalar color.</summary>
    static abstract Rgba128Float CompositeScalar(scoped in Rgba128Float source, scoped in Rgba128Float destination);
}
