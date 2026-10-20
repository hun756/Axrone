namespace Axrone.AssetShader;

/// <summary>Zero-allocation line splitter over a character span.</summary>
public ref struct SpanLineEnumerator
{
    private ReadOnlySpan<char> _remaining;
    private ReadOnlySpan<char> _current;
    private uint _lineNumber;

    /// <summary>Creates an enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanLineEnumerator(ReadOnlySpan<char> source)
    {
        _remaining = source;
        _current = default;
        _lineNumber = 0;
    }

    /// <summary>Advances to the next line.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_remaining.IsEmpty)
        {
            return false;
        }

        int idx = _remaining.IndexOfAny('\r', '\n');
        if (idx < 0)
        {
            _current = _remaining;
            _remaining = default;
        }
        else
        {
            _current = _remaining[..idx];
            if (_remaining[idx] == '\r' && idx + 1 < _remaining.Length && _remaining[idx + 1] == '\n')
            {
                _remaining = _remaining[(idx + 2)..];
            }
            else
            {
                _remaining = _remaining[(idx + 1)..];
            }
        }

        _lineNumber++;
        return true;
    }

    /// <summary>Current line without the terminator.</summary>
    public readonly ReadOnlySpan<char> Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    /// <summary>1-based line number.</summary>
    public readonly uint LineNumber
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _lineNumber;
    }

    /// <summary>Returns this enumerator for foreach.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly SpanLineEnumerator GetEnumerator() => this;
}

/// <summary>GLSL text utilities.</summary>
public static class GLSL
{
    /// <summary>Removes the common leading indentation.</summary>
    public static string Dedent(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        ReadOnlySpan<char> span = code.AsSpan();
        int minIndent = int.MaxValue;
        var enumerator = new SpanLineEnumerator(span);

        while (enumerator.MoveNext())
        {
            ReadOnlySpan<char> line = enumerator.Current;
            if (line.IsWhiteSpace())
            {
                continue;
            }

            int indent = 0;
            while (indent < line.Length && char.IsWhiteSpace(line[indent]))
            {
                indent++;
            }

            if (indent < minIndent)
            {
                minIndent = indent;
            }
        }

        if (minIndent == int.MaxValue || minIndent == 0)
        {
            return code;
        }

        var sb = new StringBuilder(code.Length);
        enumerator = new SpanLineEnumerator(span);
        bool first = true;

        while (enumerator.MoveNext())
        {
            ReadOnlySpan<char> line = enumerator.Current;
            if (!first)
            {
                sb.Append('\n');
            }

            first = false;

            if (line.IsWhiteSpace())
            {
                continue;
            }

            int trim = Math.Min(minIndent, line.Length);
            sb.Append(line[trim..]);
        }

        return sb.ToString();
    }
}
