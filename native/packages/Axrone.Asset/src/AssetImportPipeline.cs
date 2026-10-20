namespace Axrone.Asset;

/// <summary>Import stage phase.</summary>
public enum AssetImportStagePhase : byte
{
    /// <summary>Runs on the raw source before importer selection.</summary>
    Source = 0,

    /// <summary>Runs after selection, before the importer.</summary>
    BeforeImport = 1,

    /// <summary>Runs after the importer, before writes commit.</summary>
    AfterImport = 2,
}

/// <summary>Stage output: optional source/result replacement plus diagnostics.</summary>
public sealed record AssetStageOutput(
    IAssetSource? Source = null,
    AssetImportResult? Result = null,
    IReadOnlyList<AssetImportDiagnostic>? Diagnostics = null);

/// <summary>Phased import stage contract.</summary>
public interface IAssetImportStage
{
    /// <summary>Stage identity.</summary>
    AssetImporterId Id { get; }

    /// <summary>Priority; higher runs first within a phase.</summary>
    int Priority => 0;

    /// <summary>Phases this stage runs in; empty means all.</summary>
    IReadOnlyList<AssetImportStagePhase> Phases => [];

    /// <summary>Accepted source kinds; empty matches all.</summary>
    IReadOnlyList<string> SourceKinds => [];

    /// <summary>Whether this stage processes the context.</summary>
    bool CanProcess(AssetImportContext context) => true;

    /// <summary>Runs the stage.</summary>
    ValueTask<AssetStageOutput?> RunAsync(AssetImportContext context, CancellationToken cancellationToken = default);
}

/// <summary>Importer registry with kind/mime/extension prefiltering.</summary>
public sealed class AssetImporterRegistry
{
    private readonly List<IAssetImporter> _importers = new();
    private readonly Lock _gate = new();
    private int _disposed;

    private void EnsureNotDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            ThrowHelper.ThrowDisposed();
        }
    }

    /// <summary>Registers an importer; higher priority sorts first.</summary>
    public void Register(IAssetImporter importer)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(importer);
        lock (_gate)
        {
            _importers.RemoveAll(existing => existing.Id.Equals(importer.Id));
            _importers.Add(importer);
            _importers.Sort(static (a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    /// <summary>Unregisters an importer by identity.</summary>
    public bool Unregister(AssetImporterId id)
    {
        lock (_gate)
        {
            EnsureNotDisposed();
            return _importers.RemoveAll(existing => existing.Id.Equals(id)) > 0;
        }
    }

    /// <summary>Clears all importers.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            EnsureNotDisposed();
            _importers.Clear();
        }
    }

    /// <summary>Lists registered importers in priority order.</summary>
    public IReadOnlyList<IAssetImporter> List()
    {
        lock (_gate)
        {
            EnsureNotDisposed();
            return new List<IAssetImporter>(_importers);
        }
    }

    /// <summary>Releases the registry.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
    }

    /// <summary>Finds the first matching importer.</summary>
    public IAssetImporter? Find(AssetImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            EnsureNotDisposed();
            foreach (IAssetImporter importer in _importers)
            {
                if (Matches(importer, context) && importer.CanImport(context))
                {
                    return importer;
                }
            }

            return null;
        }
    }

    internal static bool Matches(IAssetImporter importer, AssetImportContext context)
    {
        IReadOnlyList<string> kinds = importer.SourceKinds;
        if (kinds.Count > 0)
        {
            bool kindMatch = false;
            foreach (string kind in kinds)
            {
                if (string.Equals(kind, context.Source.Kind, StringComparison.OrdinalIgnoreCase))
                {
                    kindMatch = true;
                    break;
                }
            }

            if (!kindMatch)
            {
                return false;
            }
        }

        IReadOnlyList<string> mimes = importer.MimeTypes;
        if (mimes.Count > 0)
        {
            string? mime = context.Metadata?.MimeType?.Trim();
            if (string.IsNullOrEmpty(mime))
            {
                return false;
            }

            bool mimeMatch = false;
            foreach (string accepted in mimes)
            {
                if (string.Equals(accepted.Trim(), mime, StringComparison.OrdinalIgnoreCase))
                {
                    mimeMatch = true;
                    break;
                }
            }

            if (!mimeMatch)
            {
                return false;
            }
        }

        IReadOnlyList<string> extensions = importer.Extensions;
        if (extensions.Count > 0)
        {
            string? uri = context.SourceUri?.Value;
            if (string.IsNullOrEmpty(uri))
            {
                return false;
            }

            bool extensionMatch = false;
            foreach (string suffix in GetSourceExtensions(uri))
            {
                foreach (string accepted in extensions)
                {
                    if (string.Equals(NormalizeExtension(accepted), suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        extensionMatch = true;
                        break;
                    }
                }

                if (extensionMatch)
                {
                    break;
                }
            }

            if (!extensionMatch)
            {
                return false;
            }
        }

        return true;
    }

    internal static string NormalizeExtension(string value) =>
        value.StartsWith('.') ? value[1..].ToLowerInvariant() : value.ToLowerInvariant();

    internal static List<string> GetSourceExtensions(string uri)
    {
        int query = uri.IndexOfAny(['?', '#']);
        ReadOnlySpan<char> path = (query >= 0 ? uri[..query] : uri).AsSpan();
        int slash = path.LastIndexOfAny('/', '\\');
        ReadOnlySpan<char> file = slash >= 0 ? path[(slash + 1)..] : path;

        var extensions = new List<string>();
        int start = 0;
        while (true)
        {
            int dot = file[start..].IndexOf('.');
            if (dot < 0)
            {
                break;
            }

            start += dot + 1;
            if (start < file.Length)
            {
                extensions.Add(NormalizeExtension(file[start..].ToString()));
            }
        }

        return extensions;
    }
}

/// <summary>Stage registry with per-phase priority ordering.</summary>
public sealed class AssetImportStageRegistry
{
    private readonly List<IAssetImportStage> _stages = new();
    private readonly Lock _gate = new();
    private int _disposed;

    private void EnsureNotDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            ThrowHelper.ThrowDisposed();
        }
    }

    /// <summary>Registers a stage; higher priority runs first.</summary>
    public void Register(IAssetImportStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        lock (_gate)
        {
            EnsureNotDisposed();
            _stages.RemoveAll(existing => existing.Id.Equals(stage.Id));
            _stages.Add(stage);
            _stages.Sort(static (a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    /// <summary>Unregisters a stage by identity.</summary>
    public bool Unregister(AssetImporterId id)
    {
        lock (_gate)
        {
            EnsureNotDisposed();
            return _stages.RemoveAll(existing => existing.Id.Equals(id)) > 0;
        }
    }

    /// <summary>Clears all stages.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            EnsureNotDisposed();
            _stages.Clear();
        }
    }

    /// <summary>Lists registered stages in priority order.</summary>
    public IReadOnlyList<IAssetImportStage> List()
    {
        lock (_gate)
        {
            EnsureNotDisposed();
            return new List<IAssetImportStage>(_stages);
        }
    }

    /// <summary>Releases the registry.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
    }

    /// <summary>Lists stages running in a phase, in priority order.</summary>
    public IReadOnlyList<IAssetImportStage> ListByPhase(AssetImportStagePhase phase)
    {
        lock (_gate)
        {
            EnsureNotDisposed();
            var matching = new List<IAssetImportStage>();
            foreach (IAssetImportStage stage in _stages)
            {
                IReadOnlyList<AssetImportStagePhase> phases = stage.Phases;
                if (phases.Count == 0 || phases.Contains(phase))
                {
                    matching.Add(stage);
                }
            }

            return matching;
        }
    }
}

/// <summary>Retry policy for importer execution.</summary>
public sealed record AssetRetryPolicy(
    int Attempts = 1,
    int BaseDelayMs = 0,
    int MaxDelayMs = 0,
    Func<Exception, int, bool>? ShouldRetry = null);

/// <summary>Per-call import options.</summary>
public sealed record AssetImportOptions(
    AssetRetryPolicy? Retry = null,
    int? Concurrency = null);

/// <summary>One batched import request.</summary>
public readonly record struct AssetImportRequest(
    IAssetSource Source,
    AssetKey BaseKey,
    AssetUri? SourceUri = null,
    AssetSourceIdentity? SourceIdentity = null,
    AssetMetadata? Metadata = null);
