namespace Axrone.AssetShader;

/// <summary>Macro lookup contract for the preprocessor.</summary>
public interface IMacroLookupPolicy
{
    /// <summary>Tries to resolve a numeric macro value.</summary>
    bool TryLookup(ShaderSymbol symbol, out long value);

    /// <summary>Whether the macro is defined.</summary>
    bool IsDefined(ShaderSymbol symbol);

    /// <summary>Tries to resolve macro replacement text.</summary>
    bool TryGetText(ShaderSymbol symbol, [NotNullWhen(true)] out string? text);
}

/// <summary>Empty macro environment: nothing defined.</summary>
public readonly struct EmptyMacroPolicy : IMacroLookupPolicy
{
    /// <inheritdoc/>
    public bool TryLookup(ShaderSymbol symbol, out long value)
    {
        value = 0;
        return false;
    }

    /// <inheritdoc/>
    public bool IsDefined(ShaderSymbol symbol) => false;

    /// <inheritdoc/>
    public bool TryGetText(ShaderSymbol symbol, [NotNullWhen(true)] out string? text)
    {
        text = null;
        return false;
    }
}

/// <summary>Immutable dictionary-backed macro environment.</summary>
public readonly struct ImmutableMacroEnvironment : IMacroLookupPolicy
{
    private readonly ImmutableDictionary<ShaderSymbol, string> _macros;

    /// <summary>Creates an environment.</summary>
    public ImmutableMacroEnvironment(ImmutableDictionary<ShaderSymbol, string> macros)
    {
        _macros = macros;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryLookup(ShaderSymbol symbol, out long value)
    {
        if (_macros.TryGetValue(symbol, out string? text))
        {
            if (long.TryParse(text.AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            value = 1;
            return true;
        }

        value = 0;
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsDefined(ShaderSymbol symbol) => _macros.ContainsKey(symbol);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetText(ShaderSymbol symbol, [NotNullWhen(true)] out string? text) =>
        _macros.TryGetValue(symbol, out text);
}

/// <summary>Zero-allocation recursive-descent constant expression parser.</summary>
/// <typeparam name="TLookup">Macro lookup type.</typeparam>
public ref struct SpanExpressionParser<TLookup>
    where TLookup : struct, IMacroLookupPolicy
{
    private ReadOnlySpan<char> _span;
    private readonly TLookup _lookup;
    private int _pos;

    /// <summary>Creates a parser.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanExpressionParser(ReadOnlySpan<char> span, in TLookup lookup)
    {
        _span = span;
        _lookup = lookup;
        _pos = 0;
    }

    /// <summary>Parses the expression.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Parse() => ParseLogicalOr();

    private void SkipWhitespace()
    {
        while (_pos < _span.Length && char.IsWhiteSpace(_span[_pos]))
        {
            _pos++;
        }
    }

    private bool Match(string op)
    {
        SkipWhitespace();
        if (_pos + op.Length <= _span.Length && _span.Slice(_pos, op.Length).SequenceEqual(op.AsSpan()))
        {
            _pos += op.Length;
            return true;
        }

        return false;
    }

    private long ParseLogicalOr()
    {
        long left = ParseLogicalAnd();
        while (Match("||"))
        {
            long right = ParseLogicalAnd();
            left = (left != 0 || right != 0) ? 1 : 0;
        }

        return left;
    }

    private long ParseLogicalAnd()
    {
        long left = ParseBitOr();
        while (Match("&&"))
        {
            long right = ParseBitOr();
            left = (left != 0 && right != 0) ? 1 : 0;
        }

        return left;
    }

    private long ParseBitOr()
    {
        long left = ParseBitXor();
        while (Match("|") && !Match("|"))
        {
            long right = ParseBitXor();
            left |= right;
        }

        return left;
    }

    private long ParseBitXor()
    {
        long left = ParseBitAnd();
        while (Match("^"))
        {
            long right = ParseBitAnd();
            left ^= right;
        }

        return left;
    }

    private long ParseBitAnd()
    {
        long left = ParseEquality();
        while (Match("&") && !Match("&"))
        {
            long right = ParseEquality();
            left &= right;
        }

        return left;
    }

    private long ParseEquality()
    {
        long left = ParseRelational();
        while (true)
        {
            if (Match("=="))
            {
                left = left == ParseRelational() ? 1 : 0;
            }
            else if (Match("!="))
            {
                left = left != ParseRelational() ? 1 : 0;
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private long ParseRelational()
    {
        long left = ParseShift();
        while (true)
        {
            if (Match("<="))
            {
                left = left <= ParseShift() ? 1 : 0;
            }
            else if (Match(">="))
            {
                left = left >= ParseShift() ? 1 : 0;
            }
            else if (Match("<"))
            {
                left = left < ParseShift() ? 1 : 0;
            }
            else if (Match(">"))
            {
                left = left > ParseShift() ? 1 : 0;
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private long ParseShift()
    {
        long left = ParseAdditive();
        while (true)
        {
            if (Match("<<"))
            {
                left <<= (int)ParseAdditive();
            }
            else if (Match(">>"))
            {
                left >>= (int)ParseAdditive();
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private long ParseAdditive()
    {
        long left = ParseMultiplicative();
        while (true)
        {
            if (Match("+"))
            {
                left += ParseMultiplicative();
            }
            else if (Match("-"))
            {
                left -= ParseMultiplicative();
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private long ParseMultiplicative()
    {
        long left = ParseUnary();
        while (true)
        {
            if (Match("*"))
            {
                left *= ParseUnary();
            }
            else if (Match("/"))
            {
                long right = ParseUnary();
                left = right != 0 ? left / right : 0;
            }
            else if (Match("%"))
            {
                long right = ParseUnary();
                left = right != 0 ? left % right : 0;
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private long ParseUnary()
    {
        SkipWhitespace();
        if (Match("!"))
        {
            return ParseUnary() == 0 ? 1 : 0;
        }

        if (Match("~"))
        {
            return ~ParseUnary();
        }

        if (Match("+"))
        {
            return ParseUnary();
        }

        if (Match("-"))
        {
            return -ParseUnary();
        }

        if (Match("defined"))
        {
            SkipWhitespace();
            bool hasParen = Match("(");
            SkipWhitespace();
            int start = _pos;
            while (_pos < _span.Length && (char.IsLetterOrDigit(_span[_pos]) || _span[_pos] == '_'))
            {
                _pos++;
            }

            ReadOnlySpan<char> idSpan = _span[start.._pos];
            if (hasParen)
            {
                Match(")");
            }

            return _lookup.IsDefined(new ShaderSymbol(idSpan)) ? 1 : 0;
        }

        return ParsePrimary();
    }

    private long ParsePrimary()
    {
        SkipWhitespace();
        if (Match("("))
        {
            long val = ParseLogicalOr();
            Match(")");
            return val;
        }

        if (_pos < _span.Length && char.IsDigit(_span[_pos]))
        {
            int start = _pos;
            if (_pos + 1 < _span.Length && _span[_pos] == '0' && (_span[_pos + 1] == 'x' || _span[_pos + 1] == 'X'))
            {
                _pos += 2;
                while (_pos < _span.Length && Uri.IsHexDigit(_span[_pos]))
                {
                    _pos++;
                }

                return Convert.ToInt64(_span[start.._pos].ToString(), 16);
            }

            while (_pos < _span.Length && char.IsDigit(_span[_pos]))
            {
                _pos++;
            }

            return long.Parse(_span[start.._pos], CultureInfo.InvariantCulture);
        }

        if (_pos < _span.Length && (char.IsLetter(_span[_pos]) || _span[_pos] == '_'))
        {
            int start = _pos;
            while (_pos < _span.Length && (char.IsLetterOrDigit(_span[_pos]) || _span[_pos] == '_'))
            {
                _pos++;
            }

            ReadOnlySpan<char> id = _span[start.._pos];
            if (_lookup.TryLookup(new ShaderSymbol(id), out long val))
            {
                return val;
            }

            return 0;
        }

        return 0;
    }
}

/// <summary>Lexical GLSL preprocessor: conditionals, errors and macro expansion.</summary>
public static class GLSLPreprocessor
{
    private struct BranchContext
    {
        public bool ConditionMet;
        public bool Active;
        public bool HadElse;
    }

    /// <summary>Preprocesses a source string.</summary>
    public static string Preprocess<TLookup>(
        string source,
        in TLookup lookup,
        ShaderDiagnosticSink sink,
        ShaderSymbol sourceId,
        bool preserveLineMarkers = false)
        where TLookup : struct, IMacroLookupPolicy
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sink);

        ReadOnlySpan<char> sourceSpan = source.AsSpan();
        var output = new StringBuilder(source.Length);
        Span<BranchContext> branchStack = stackalloc BranchContext[32];
        int stackDepth = 0;

        var enumerator = new SpanLineEnumerator(sourceSpan);

        while (enumerator.MoveNext())
        {
            ReadOnlySpan<char> line = enumerator.Current;
            ReadOnlySpan<char> trimmed = line.TrimStart();

            if (trimmed.StartsWith("#"))
            {
                ReadOnlySpan<char> directiveSpan = trimmed[1..].TrimStart();
                int spaceIndex = directiveSpan.IndexOfAny(' ', '\t');
                ReadOnlySpan<char> directive = spaceIndex >= 0 ? directiveSpan[..spaceIndex] : directiveSpan;
                ReadOnlySpan<char> rest = spaceIndex >= 0 ? directiveSpan[spaceIndex..].Trim() : default;

                bool isCurrentActive = stackDepth == 0 || branchStack[stackDepth - 1].Active;

                if (directive.SequenceEqual("if".AsSpan()))
                {
                    bool parentActive = stackDepth == 0 || branchStack[stackDepth - 1].Active;
                    bool cond = false;
                    if (parentActive)
                    {
                        var parser = new SpanExpressionParser<TLookup>(rest, lookup);
                        cond = parser.Parse() != 0;
                    }

                    if (stackDepth >= branchStack.Length)
                    {
                        ThrowHelper.ThrowPreprocessorStackOverflow();
                    }

                    branchStack[stackDepth++] = new BranchContext
                    {
                        ConditionMet = cond,
                        Active = parentActive && cond,
                        HadElse = false,
                    };
                    continue;
                }

                if (directive.SequenceEqual("ifdef".AsSpan()))
                {
                    bool parentActive = stackDepth == 0 || branchStack[stackDepth - 1].Active;
                    bool exists = lookup.IsDefined(new ShaderSymbol(rest));

                    if (stackDepth >= branchStack.Length)
                    {
                        ThrowHelper.ThrowPreprocessorStackOverflow();
                    }

                    branchStack[stackDepth++] = new BranchContext
                    {
                        ConditionMet = exists,
                        Active = parentActive && exists,
                        HadElse = false,
                    };
                    continue;
                }

                if (directive.SequenceEqual("ifndef".AsSpan()))
                {
                    bool parentActive = stackDepth == 0 || branchStack[stackDepth - 1].Active;
                    bool notExists = !lookup.IsDefined(new ShaderSymbol(rest));

                    if (stackDepth >= branchStack.Length)
                    {
                        ThrowHelper.ThrowPreprocessorStackOverflow();
                    }

                    branchStack[stackDepth++] = new BranchContext
                    {
                        ConditionMet = notExists,
                        Active = parentActive && notExists,
                        HadElse = false,
                    };
                    continue;
                }

                if (directive.SequenceEqual("elif".AsSpan()))
                {
                    if (stackDepth == 0)
                    {
                        sink.ReportError("ERR_UNEXPECTED_ELIF", "#elif without matching #if",
                            new SourceLocation(enumerator.LineNumber, 1), sourceId);
                        continue;
                    }

                    ref BranchContext current = ref branchStack[stackDepth - 1];
                    if (current.HadElse)
                    {
                        sink.ReportError("ERR_ELIF_AFTER_ELSE", "#elif after #else is forbidden",
                            new SourceLocation(enumerator.LineNumber, 1), sourceId);
                        continue;
                    }

                    bool parentActive = stackDepth <= 1 || branchStack[stackDepth - 2].Active;
                    bool cond = false;
                    if (parentActive && !current.ConditionMet)
                    {
                        var parser = new SpanExpressionParser<TLookup>(rest, lookup);
                        cond = parser.Parse() != 0;
                    }

                    current.Active = parentActive && !current.ConditionMet && cond;
                    if (cond)
                    {
                        current.ConditionMet = true;
                    }

                    continue;
                }

                if (directive.SequenceEqual("else".AsSpan()))
                {
                    if (stackDepth == 0)
                    {
                        sink.ReportError("ERR_UNEXPECTED_ELSE", "#else without matching #if",
                            new SourceLocation(enumerator.LineNumber, 1), sourceId);
                        continue;
                    }

                    ref BranchContext current = ref branchStack[stackDepth - 1];
                    if (current.HadElse)
                    {
                        sink.ReportError("ERR_DUPLICATE_ELSE", "Duplicate #else block encountered",
                            new SourceLocation(enumerator.LineNumber, 1), sourceId);
                        continue;
                    }

                    bool parentActive = stackDepth <= 1 || branchStack[stackDepth - 2].Active;
                    current.Active = parentActive && !current.ConditionMet;
                    current.ConditionMet = true;
                    current.HadElse = true;
                    continue;
                }

                if (directive.SequenceEqual("endif".AsSpan()))
                {
                    if (stackDepth == 0)
                    {
                        sink.ReportError("ERR_UNEXPECTED_ENDIF", "#endif without matching #if",
                            new SourceLocation(enumerator.LineNumber, 1), sourceId);
                        continue;
                    }

                    stackDepth--;
                    continue;
                }

                if (!isCurrentActive)
                {
                    continue;
                }

                if (directive.SequenceEqual("error".AsSpan()))
                {
                    sink.ReportError("ERR_USER_DIRECTIVE", rest.ToString(),
                        new SourceLocation(enumerator.LineNumber, 1), sourceId);
                    continue;
                }

                output.Append(line).Append('\n');
            }
            else
            {
                bool isCurrentActive = stackDepth == 0 || branchStack[stackDepth - 1].Active;
                if (isCurrentActive)
                {
                    ExpandIdentifiersInLine(line, lookup, output);
                    output.Append('\n');
                }
                else if (preserveLineMarkers)
                {
                    output.Append('\n');
                }
            }
        }

        if (stackDepth > 0)
        {
            sink.ReportError("ERR_UNTERMINATED_CONDITIONAL", "Unterminated #if directive block",
                new SourceLocation(enumerator.LineNumber, 1), sourceId);
        }

        if (sink.HasErrors)
        {
            throw new ShaderPreprocessException(sink.Diagnostics);
        }

        return output.ToString();
    }

    private static void ExpandIdentifiersInLine<TLookup>(
        ReadOnlySpan<char> line,
        in TLookup lookup,
        StringBuilder output)
        where TLookup : struct, IMacroLookupPolicy
    {
        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];
            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                {
                    i++;
                }

                ReadOnlySpan<char> token = line[start..i];
                var sym = new ShaderSymbol(token);
                if (lookup.TryGetText(sym, out string? replacement))
                {
                    output.Append(replacement);
                }
                else
                {
                    output.Append(token);
                }
            }
            else
            {
                output.Append(c);
                i++;
            }
        }
    }
}

/// <summary>One scanned uniform declaration.</summary>
public readonly record struct ShaderUniformScanResult(
    ShaderSymbol Name,
    ShaderSymbol Type,
    int ArrayLength);

/// <summary>Allocation-free uniform declaration scanner.</summary>
public static class UniformExtractor
{
    /// <summary>Scans both stages without duplicates.</summary>
    public static ImmutableArray<ShaderUniformScanResult> ScanUniforms(ReadOnlySpan<char> vtx, ReadOnlySpan<char> frag)
    {
        ImmutableArray<ShaderUniformScanResult>.Builder builder = ImmutableArray.CreateBuilder<ShaderUniformScanResult>();
        ScanStage(vtx, builder);
        ScanStage(frag, builder);
        return builder.ToImmutable();
    }

    private static void ScanStage(ReadOnlySpan<char> code, ImmutableArray<ShaderUniformScanResult>.Builder builder)
    {
        var enumerator = new SpanLineEnumerator(code);
        while (enumerator.MoveNext())
        {
            ReadOnlySpan<char> line = enumerator.Current;
            if (TryParseUniformDeclaration(line, out ReadOnlySpan<char> type, out ReadOnlySpan<char> name, out int arrLen))
            {
                var uniformName = new ShaderSymbol(name);
                bool exists = false;
                for (int i = 0; i < builder.Count; i++)
                {
                    if (builder[i].Name == uniformName)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    builder.Add(new ShaderUniformScanResult(uniformName, new ShaderSymbol(type), arrLen));
                }
            }
        }
    }

    /// <summary>Parses a uniform declaration line.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryParseUniformDeclaration(
        ReadOnlySpan<char> line,
        out ReadOnlySpan<char> type,
        out ReadOnlySpan<char> name,
        out int arrayLength)
    {
        type = default;
        name = default;
        arrayLength = 0;

        ReadOnlySpan<char> trimmed = line.Trim();
        if (!trimmed.StartsWith("uniform", StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.Length <= 7 || !char.IsWhiteSpace(trimmed[7]))
        {
            return false;
        }

        ReadOnlySpan<char> rest = trimmed[7..].TrimStart();
        int spaceIdx = rest.IndexOfAny(' ', '\t');
        if (spaceIdx <= 0)
        {
            return false;
        }

        type = rest[..spaceIdx];
        rest = rest[spaceIdx..].TrimStart();

        int semiIdx = rest.IndexOf(';');
        if (semiIdx < 0)
        {
            return false;
        }

        ReadOnlySpan<char> namePart = rest[..semiIdx].TrimEnd();
        int bracketOpen = namePart.IndexOf('[');
        if (bracketOpen >= 0)
        {
            int bracketClose = namePart.IndexOf(']');
            if (bracketClose > bracketOpen)
            {
                ReadOnlySpan<char> lenSpan = namePart.Slice(bracketOpen + 1, bracketClose - bracketOpen - 1).Trim();
                int.TryParse(lenSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out arrayLength);
                name = namePart[..bracketOpen].TrimEnd();
                return true;
            }
        }

        name = namePart;
        return true;
    }
}
