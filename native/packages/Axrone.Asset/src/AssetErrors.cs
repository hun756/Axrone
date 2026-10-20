namespace Axrone.Asset;

/// <summary>
/// Coded asset error.
/// </summary>
public class AssetException : Exception
{
    /// <summary>Machine-readable code.</summary>
    public string Code { get; }

    /// <summary>Creates an error.</summary>
    public AssetException(string code, string message)
        : base(message) => Code = code;

    /// <summary>Creates an error with an inner exception.</summary>
    protected AssetException(string code, string message, Exception? inner)
        : base(message, inner) => Code = code;
}

/// <summary>
/// Invalid asset configuration.
/// </summary>
public sealed class AssetConfigurationException(string code, string message) : AssetException(code, message);

/// <summary>
/// Key already bound or kind mismatch.
/// </summary>
public sealed class AssetConflictException(string code, string message) : AssetException(code, message);

/// <summary>
/// Use after asset database disposal.
/// </summary>
public sealed class AssetDisposedException(string message) : AssetException("asset.disposed", message);

/// <summary>
/// No importer for the requested source.
/// </summary>
public sealed class AssetImporterNotFoundException(string message) : AssetException("asset.importer.not-found", message);

/// <summary>
/// Import failed inside an importer.
/// </summary>
public sealed class AssetImportException : AssetException
{
    /// <summary>Failing importer.</summary>
    public AssetImporterId ImporterId { get; }

    /// <summary>Attempt number.</summary>
    public int Attempt { get; }

    /// <summary>Creates an error.</summary>
    public AssetImportException(string message, AssetImporterId importerId, int attempt, Exception? inner = null)
        : base("asset.import.failed", message, inner)
    {
        ImporterId = importerId;
        Attempt = attempt;
    }
}

/// <summary>
/// Invalid asset reference token.
/// </summary>
public sealed class AssetReferenceException(string message) : AssetException("asset.reference.invalid", message);

/// <summary>
/// Missing asset dependency.
/// </summary>
public sealed class AssetDependencyException : AssetException
{
    /// <summary>Missing dependency.</summary>
    public string Dependency { get; }

    /// <summary>Creates an error.</summary>
    public AssetDependencyException(string message, string dependency)
        : base("asset.dependency.missing", message) => Dependency = dependency;
}

/// <summary>
/// Invalid asset snapshot.
/// </summary>
public sealed class AssetSnapshotException(string message) : AssetException("asset.snapshot.invalid", message);

/// <summary>
/// Asset disposal failure.
/// </summary>
public sealed class AssetLifecycleException : AssetException
{
    /// <summary>Failing asset.</summary>
    public AssetId Id { get; }

    /// <summary>Asset kind.</summary>
    public AssetKind Kind { get; }

    /// <summary>Creates an error.</summary>
    public AssetLifecycleException(string message, AssetId id, AssetKind kind, Exception? inner = null)
        : base("asset.lifecycle.dispose-failed", message, inner)
    {
        Id = id;
        Kind = kind;
    }
}

/// <summary>
/// Centralized asset throw helpers.
/// </summary>
internal static class AssetThrowHelper
{
    /// <summary>Throws for an invalid brand.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalid(string code, string? value) =>
        throw new AssetConfigurationException(code, $"Invalid asset value for '{code}': '{value}'.");

    /// <summary>Throws when the database is used after disposal.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDisposed() => throw new AssetDisposedException("Asset database has been disposed.");

    /// <summary>Throws for a key already bound to another id.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowKeyBound(AssetKey key, AssetId current, AssetId requested) =>
        throw new AssetConflictException("asset.conflict.key-bound", $"Asset key '{key}' is already bound to {current}; requested {requested}.");

    /// <summary>Throws for a kind mismatch.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowKindMismatch(AssetId id, AssetKind expected, AssetKind received) =>
        throw new AssetConflictException("asset.conflict.kind-mismatch", $"Asset {id} expected kind '{expected}' but received '{received}'.");

    /// <summary>Throws for an invalid snapshot.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSnapshot(string reason) =>
        throw new AssetSnapshotException($"Invalid asset snapshot: {reason}.");
}
