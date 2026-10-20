namespace Axrone.Asset;

/// <summary>
/// Dependency input union: identity, key, token, typed reference, record or key lookup.
/// Resolved to identities at write time; missing targets raise
/// <see cref="AssetDependencyError"/>.
/// </summary>
public abstract record AssetDependencyInput
{
    /// <summary>Creates an identity dependency.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetDependencyInput FromId(AssetId id) => new IdDependency(id);

    /// <summary>Creates a key dependency.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetDependencyInput FromKey(AssetKey key) => new KeyDependency(key, 0UL);

    /// <summary>Creates a kind-filtered key dependency.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetDependencyInput FromKey(AssetKey key, string? kind) =>
        new KeyDependency(key, kind is null ? 0UL : Fnv1a64.ComputeUtf8Chars(kind.AsSpan()));

    /// <summary>Creates a token dependency (<c>asset:{kind}:{id}[@{rev}]</c>, hex id or key).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetDependencyInput FromToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return new TokenDependency(token);
    }

    /// <summary>Creates a dependency from a typed reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetDependencyInput FromReference<TKind>(in AssetReference<TKind> reference) =>
        new IdDependency(reference.Id);

    /// <summary>Creates a dependency from a typed versioned reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetDependencyInput FromVersionedReference<TKind>(in AssetVersionedReference<TKind> reference) =>
        new IdDependency(reference.Id);

    /// <summary>Creates a dependency from a record.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetDependencyInput FromRecord<TData>(in AssetRecord<TData> record) =>
        new IdDependency(record.Id);

    /// <summary>Creates a dependency from a key lookup.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssetDependencyInput FromLookup(in AssetLookupByKey lookup) =>
        new LookupDependency(lookup);

    /// <summary>Creates an identity dependency.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetDependencyInput(AssetId id) => FromId(id);

    /// <summary>Creates a key dependency.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetDependencyInput(AssetKey key) => FromKey(key);

    /// <summary>Creates a token dependency.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetDependencyInput(string token) => FromToken(token);

    /// <summary>Creates a lookup dependency.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator AssetDependencyInput(AssetLookupByKey lookup) => FromLookup(lookup);

    /// <summary>Identity dependency.</summary>
    public sealed record IdDependency(AssetId Id) : AssetDependencyInput;

    /// <summary>Key dependency with an optional kind filter hash.</summary>
    public sealed record KeyDependency(AssetKey Key, ulong KindHash) : AssetDependencyInput;

    /// <summary>Token dependency.</summary>
    public sealed record TokenDependency(string Token) : AssetDependencyInput;

    /// <summary>Key lookup dependency.</summary>
    public sealed record LookupDependency(AssetLookupByKey Lookup) : AssetDependencyInput;
}
