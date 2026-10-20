namespace Axrone.Asset;

/// <summary>Base asset error carrying a machine-readable code.</summary>
public class AssetError : Exception
{
    /// <summary>Machine-readable code.</summary>
    public string Code { get; }

    /// <summary>Creates an error.</summary>
    public AssetError(string message, string code = "asset.error", Exception? inner = null)
        : base(message, inner) => Code = code;
}

/// <summary>Invalid asset configuration.</summary>
public sealed class AssetConfigurationError(string message) : AssetError(message, "asset.configuration");

/// <summary>Key or kind conflict.</summary>
public sealed class AssetConflictError(string message, string conflictCode) : AssetError(message, conflictCode);

/// <summary>Use after database disposal.</summary>
public sealed class AssetDisposedError() : AssetError("The AssetDatabase instance is disposed.", "asset.disposed");

/// <summary>No importer accepts the source.</summary>
public sealed class AssetImporterNotFoundError(string message) : AssetError(message, "importer.not-found");

/// <summary>Invalid asset reference.</summary>
public sealed class AssetReferenceError(string message) : AssetError(message, "reference.invalid");

/// <summary>Missing asset dependency.</summary>
public sealed class AssetDependencyError(string message, string dependency) : AssetError(message, "dependency.missing")
{
    /// <summary>Missing dependency.</summary>
    public string Dependency { get; } = dependency;
}

/// <summary>Invalid asset snapshot.</summary>
public sealed class AssetSnapshotError(string message, Exception? inner = null) : AssetError(message, "snapshot.invalid", inner);

/// <summary>Lifecycle teardown failure.</summary>
public sealed class AssetLifecycleError(string message, string assetId, string kind, Exception? inner = null)
    : AssetError(message, "lifecycle.failed", inner)
{
    /// <summary>Failing asset.</summary>
    public string AssetId { get; } = assetId;

    /// <summary>Failing kind.</summary>
    public string Kind { get; } = kind;
}

/// <summary>Centralized cold-path throw helpers.</summary>
internal static class ThrowHelper
{
    /// <summary>Optional message resolver hook.</summary>
    internal static Func<string, string>? MessageResolver;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Resolve(string raw) => MessageResolver?.Invoke(raw) ?? raw;

    /// <summary>Throws after disposal.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDisposed() => throw new AssetDisposedError();

    /// <summary>Throws a configuration error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowConfigurationError(string message) => throw new AssetConfigurationError(Resolve(message));

    /// <summary>Throws a conflict error.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowConflictError(string message, string code) => throw new AssetConflictError(Resolve(message), code);

    /// <summary>Throws when no importer accepts a source.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowImporterNotFound(string message) => throw new AssetImporterNotFoundError(Resolve(message));

    /// <summary>Throws for an invalid reference.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowReferenceError(string message) => throw new AssetReferenceError(Resolve(message));

    /// <summary>Throws for a missing dependency.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDependencyError(string message, string dependency) => throw new AssetDependencyError(Resolve(message), dependency);

    /// <summary>Throws for an invalid snapshot.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSnapshotError(string message, Exception? inner = null) => throw new AssetSnapshotError(Resolve(message), inner);

    /// <summary>Throws for a lifecycle failure.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowLifecycleError(string message, string id, string kind, Exception? inner = null) =>
        throw new AssetLifecycleError(Resolve(message), id, kind, inner);

    /// <summary>Throws for an unreachable selector arm.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T ThrowInvalidSelector<T>() => throw new AssetReferenceError("Unreachable selector discriminant encountered.");
}
