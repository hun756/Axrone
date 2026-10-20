namespace Axrone.AssetShader;

/// <summary>One shader diagnostic notice.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly record struct ShaderDiagnostic(
    ShaderSymbol Code,
    ShaderDiagnosticSeverity Severity,
    SourceLocation Location,
    ShaderSymbol SourceId,
    string Message)
{
    /// <inheritdoc/>
    public override string ToString() =>
        $"[{Severity.ToString().ToLowerInvariant()}] {Code.Name}: {Message} at {SourceId.Name}{Location}";
}

/// <summary>Span action over a character span with caller state.</summary>
/// <typeparam name="TState">Caller state.</typeparam>
public delegate void SpanAction<TState>(ReadOnlySpan<char> span, ref TState state)
    where TState : allows ref struct;

/// <summary>Zero-allocation item sink.</summary>
/// <typeparam name="TItem">Item type.</typeparam>
public interface ISink<in TItem>
    where TItem : allows ref struct
{
    /// <summary>Accepts an item.</summary>
    void Accept(TItem item);
}

/// <summary>Diagnostic reporting policy trait.</summary>
/// <typeparam name="TSelf">Policy type.</typeparam>
public interface IShaderDiagnosticPolicy<TSelf>
    where TSelf : struct, IShaderDiagnosticPolicy<TSelf>
{
    /// <summary>Handles a reported diagnostic.</summary>
    static abstract void OnReport(in ShaderDiagnostic diagnostic);

    /// <summary>Whether the severity is enabled.</summary>
    static abstract bool IsSeverityEnabled(ShaderDiagnosticSeverity severity);
}

/// <summary>Reports every severity.</summary>
public readonly struct StrictDiagnosticPolicy : IShaderDiagnosticPolicy<StrictDiagnosticPolicy>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnReport(in ShaderDiagnostic diagnostic)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSeverityEnabled(ShaderDiagnosticSeverity severity) => true;
}

/// <summary>Reports errors only.</summary>
public readonly struct SilentDiagnosticPolicy : IShaderDiagnosticPolicy<SilentDiagnosticPolicy>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnReport(in ShaderDiagnostic diagnostic)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSeverityEnabled(ShaderDiagnosticSeverity severity) =>
        severity == ShaderDiagnosticSeverity.Error;
}

/// <summary>Thread-safe diagnostic collector.</summary>
public sealed class ShaderDiagnosticSink
{
    private readonly Lock _lock = new();
    private readonly List<ShaderDiagnostic> _diagnostics = new();
    private int _hasErrors;

    /// <summary>Whether any error was reported.</summary>
    public bool HasErrors => Volatile.Read(ref _hasErrors) != 0;

    /// <summary>Reported diagnostics snapshot.</summary>
    public ImmutableArray<ShaderDiagnostic> Diagnostics
    {
        get
        {
            lock (_lock)
            {
                return _diagnostics.ToImmutableArray();
            }
        }
    }

    /// <summary>Reports a diagnostic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Report(in ShaderDiagnostic diagnostic)
    {
        lock (_lock)
        {
            _diagnostics.Add(diagnostic);
            if (diagnostic.Severity == ShaderDiagnosticSeverity.Error)
            {
                Volatile.Write(ref _hasErrors, 1);
            }
        }
    }

    /// <summary>Reports a diagnostic through a policy gate.</summary>
    public void Report<TPolicy>(in ShaderDiagnostic diagnostic)
        where TPolicy : struct, IShaderDiagnosticPolicy<TPolicy>
    {
        if (!TPolicy.IsSeverityEnabled(diagnostic.Severity))
        {
            return;
        }

        TPolicy.OnReport(diagnostic);
        Report(diagnostic);
    }

    /// <summary>Reports an error.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReportError(ShaderSymbol code, string message, SourceLocation location = default, ShaderSymbol sourceId = default) =>
        Report(new ShaderDiagnostic(code, ShaderDiagnosticSeverity.Error, location, sourceId, message));

    /// <summary>Reports a warning.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReportWarning(ShaderSymbol code, string message, SourceLocation location = default, ShaderSymbol sourceId = default) =>
        Report(new ShaderDiagnostic(code, ShaderDiagnosticSeverity.Warning, location, sourceId, message));

    /// <summary>Reports an informational notice.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReportInfo(ShaderSymbol code, string message, SourceLocation location = default, ShaderSymbol sourceId = default) =>
        Report(new ShaderDiagnostic(code, ShaderDiagnosticSeverity.Info, location, sourceId, message));

    /// <summary>Clears all diagnostics.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _diagnostics.Clear();
            Volatile.Write(ref _hasErrors, 0);
        }
    }
}

/// <summary>Preprocessing failure carrying diagnostics.</summary>
public sealed class ShaderPreprocessException : Exception
{
    /// <summary>Failure diagnostics.</summary>
    public ImmutableArray<ShaderDiagnostic> Diagnostics { get; }

    /// <summary>Creates a failure.</summary>
    public ShaderPreprocessException(ImmutableArray<ShaderDiagnostic> diagnostics)
        : base(diagnostics.Length > 0 ? diagnostics[0].Message : "Shader preprocessing error.")
    {
        Diagnostics = diagnostics;
    }
}

/// <summary>Centralized cold-path throw helpers.</summary>
internal static class ThrowHelper
{
    /// <summary>Throws for an unmatched import path.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNoMatchingImporter(string path)
    {
        throw new InvalidOperationException($"No suitable asset importer found for path: '{path}'");
    }

    /// <summary>Throws for a missing shader library.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowShaderLibraryNotFound(ShaderSymbol id)
    {
        throw new KeyNotFoundException($"Shader library with identifier '{id.Name}' was not found in registry.");
    }

    /// <summary>Throws when the preprocessor exceeds stack depth.</summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowPreprocessorStackOverflow()
    {
        throw new InvalidOperationException("GLSL preprocessor evaluation exceeded maximum recursion/branch stack depth.");
    }
}
