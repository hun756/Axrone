namespace Axrone.Utility.Clone;

/// <summary>
/// Interface for types that support deep cloning.
/// </summary>
/// <typeparam name="T">The concrete type being cloned.</typeparam>
public interface IDeepCloneable<out T>
{
    /// <summary>Create a deep copy of this instance.</summary>
    T Clone();
}

/// <summary>
/// Deep clone utilities for common .NET types.
/// </summary>
/// <remarks>
/// <para>For custom types, implement <see cref="IDeepCloneable{T}"/>.</para>
/// <para>Uses <see cref="System.Text.Json"/> source-generated serialization for types
/// that don't implement <see cref="IDeepCloneable{T}"/> — requires a
/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>.</para>
/// </remarks>
public static class DeepClone
{
    /// <summary>Deep clone an array by cloning each element.</summary>
    public static T[] CloneArray<T>(T[] source) where T : IDeepCloneable<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new T[source.Length];
        for (var i = 0; i < source.Length; i++)
            result[i] = source[i].Clone();
        return result;
    }

    /// <summary>Deep clone a <see cref="Span{T}"/> into a new array.</summary>
    public static T[] CloneSpan<T>(ReadOnlySpan<T> source) where T : IDeepCloneable<T>
    {
        var result = new T[source.Length];
        for (var i = 0; i < source.Length; i++)
            result[i] = source[i].Clone();
        return result;
    }

    /// <summary>Deep clone a dictionary by cloning each key-value pair.</summary>
    public static Dictionary<TKey, TValue> CloneDictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> source,
        Func<TValue, TValue> valueCloner) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(valueCloner);
        var result = new Dictionary<TKey, TValue>(source.Count);
        foreach (var kvp in source)
            result[kvp.Key] = valueCloner(kvp.Value);
        return result;
    }

    /// <summary>Deep clone a list by cloning each element.</summary>
    public static List<T> CloneList<T>(IList<T> source, Func<T, T> elementCloner)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(elementCloner);
        var result = new List<T>(source.Count);
        for (var i = 0; i < source.Count; i++)
            result.Add(elementCloner(source[i]));
        return result;
    }

    /// <summary>Shallow clone a blittable struct via direct memory copy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T CloneBlittable<T>(in T source) where T : unmanaged
    {
        T result = default;
        Unsafe.CopyBlock(Unsafe.AsPointer(ref result), Unsafe.AsPointer(ref Unsafe.AsRef(in source)), (uint)sizeof(T));
        return result;
    }
}
