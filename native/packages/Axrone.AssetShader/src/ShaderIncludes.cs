namespace Axrone.AssetShader;

/// <summary>Include source contract.</summary>
public interface IIncludeResolver
{
    /// <summary>Tries to resolve a library chunk.</summary>
    bool TryResolve(ShaderSymbol symbol, [NotNullWhen(true)] out string? code);
}

/// <summary>Process-wide GLSL library registry.</summary>
public sealed class ShaderLibraryRegistry : IIncludeResolver
{
    /// <summary>Shared instance.</summary>
    public static ShaderLibraryRegistry Instance { get; } = new();

    private readonly ConcurrentDictionary<ShaderSymbol, string> _libraries = new();

    /// <summary>Registers a dedented library chunk.</summary>
    public ShaderLibraryDefinition Register(ShaderSymbol id, string code)
    {
        string dedented = GLSL.Dedent(code);
        _libraries[id] = dedented;
        return new ShaderLibraryDefinition(id, dedented);
    }

    /// <inheritdoc/>
    public bool TryResolve(ShaderSymbol symbol, [NotNullWhen(true)] out string? code) =>
        _libraries.TryGetValue(symbol, out code);

    /// <summary>Whether the library exists.</summary>
    public bool Has(ShaderSymbol id) => _libraries.ContainsKey(id);

    /// <summary>Clears the registry.</summary>
    public void Clear() => _libraries.Clear();

    /// <summary>Removes a library.</summary>
    public bool Remove(ShaderSymbol id) => _libraries.TryRemove(id, out _);

    /// <summary>Snapshots the registry.</summary>
    public ShaderLibraryDefinition[] ToArray()
    {
        var array = new ShaderLibraryDefinition[_libraries.Count];
        int index = 0;
        foreach (KeyValuePair<ShaderSymbol, string> kvp in _libraries)
        {
            array[index++] = new ShaderLibraryDefinition(kvp.Key, kvp.Value);
        }

        return array;
    }
}

/// <summary>Recursive #include expander with cycle detection.</summary>
public static class IncludeExpander
{
    /// <summary>Expands includes in a source string.</summary>
    public static string ExpandIncludes<TResolver>(
        string source,
        TResolver resolver,
        ShaderDiagnosticSink sink,
        ShaderSymbol sourceId,
        bool preserveLineMarkers = false)
        where TResolver : IIncludeResolver
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sink);

        var output = new StringBuilder(source.Length);
        Span<ShaderSymbol> callStack = new ShaderSymbol[64];
        callStack[0] = sourceId;
        int stackDepth = 1;

        ExpandInternal(source.AsSpan(), sourceId, resolver, sink, output, callStack, ref stackDepth, preserveLineMarkers);
        return output.ToString();
    }

    private static void ExpandInternal<TResolver>(
        ReadOnlySpan<char> source,
        ShaderSymbol currentSourceId,
        TResolver resolver,
        ShaderDiagnosticSink sink,
        StringBuilder output,
        Span<ShaderSymbol> callStack,
        ref int stackDepth,
        bool preserveLineMarkers)
        where TResolver : IIncludeResolver
    {
        var enumerator = new SpanLineEnumerator(source);

        while (enumerator.MoveNext())
        {
            ReadOnlySpan<char> line = enumerator.Current;
            if (!TryParseIncludeDirective(line, out ReadOnlySpan<char> targetSpan))
            {
                output.Append(line).Append('\n');
                continue;
            }

            var targetSymbol = new ShaderSymbol(targetSpan);

            bool cycleDetected = false;
            for (int i = 0; i < stackDepth; i++)
            {
                if (callStack[i] == targetSymbol)
                {
                    sink.ReportError(
                        "ERR_CIRCULAR_INCLUDE",
                        $"Circular #include detected: '{targetSymbol.Name}' from '{currentSourceId.Name}'.",
                        new SourceLocation(enumerator.LineNumber, 1),
                        currentSourceId);
                    cycleDetected = true;
                    break;
                }
            }

            if (cycleDetected)
            {
                continue;
            }

            if (!resolver.TryResolve(targetSymbol, out string? libCode))
            {
                sink.ReportError(
                    "ERR_INCLUDE_NOT_FOUND",
                    $"Shader library not found: '{targetSymbol.Name}'.",
                    new SourceLocation(enumerator.LineNumber, 1),
                    currentSourceId);
                continue;
            }

            if (stackDepth >= callStack.Length)
            {
                ThrowHelper.ThrowPreprocessorStackOverflow();
            }

            if (preserveLineMarkers)
            {
                output.Append("#line 1 \"").Append(targetSymbol.Name).Append("\"\n");
            }

            callStack[stackDepth++] = targetSymbol;
            ExpandInternal(libCode.AsSpan(), targetSymbol, resolver, sink, output, callStack, ref stackDepth, preserveLineMarkers);
            stackDepth--;

            if (preserveLineMarkers)
            {
                output.Append("#line ").Append(enumerator.LineNumber + 1).Append(" \"")
                      .Append(currentSourceId.Name).Append("\"\n");
            }
        }
    }

    /// <summary>Parses an #include directive line.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryParseIncludeDirective(ReadOnlySpan<char> line, out ReadOnlySpan<char> target)
    {
        target = default;
        ReadOnlySpan<char> trimmed = line.TrimStart();
        if (trimmed.IsEmpty || trimmed[0] != '#')
        {
            return false;
        }

        trimmed = trimmed[1..].TrimStart();
        if (!trimmed.StartsWith("include", StringComparison.Ordinal))
        {
            return false;
        }

        trimmed = trimmed[7..].TrimStart();
        if (trimmed.IsEmpty)
        {
            return false;
        }

        char open = trimmed[0];
        char close = open switch
        {
            '<' => '>',
            '"' => '"',
            _ => '\0',
        };

        if (close == '\0')
        {
            return false;
        }

        int closeIdx = trimmed[1..].IndexOf(close);
        if (closeIdx < 0)
        {
            return false;
        }

        target = trimmed.Slice(1, closeIdx);
        return true;
    }
}
