namespace Axrone.AssetShader;

/// <summary>Macro definition: optional parameters plus replacement body.</summary>
internal sealed record ShaderMacroDefinition(string[]? Parameters, string Body);

/// <summary>Mutable macro table backing #define/#undef and expansion.</summary>
internal sealed class ShaderMacroTable
{
    private readonly Dictionary<string, ShaderMacroDefinition> _macros = new(StringComparer.Ordinal);

    /// <summary>Seeds object-like macros from predefined defines.</summary>
    public void Seed(ImmutableDictionary<ShaderSymbol, string> defines)
    {
        foreach (KeyValuePair<ShaderSymbol, string> kvp in defines)
        {
            _macros[kvp.Key.Name] = new ShaderMacroDefinition(null, CoerceDefineValue(kvp.Value));
        }
    }

    /// <summary>Coerces a predefined value to macro body text.</summary>
    public static string CoerceDefineValue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "1"
        : string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ? "0"
        : value;

    /// <summary>Defines or redefines a macro.</summary>
    public void Define(string name, ShaderMacroDefinition macro) => _macros[name] = macro;

    /// <summary>Removes a macro.</summary>
    public void Undefine(string name) => _macros.Remove(name);

    /// <summary>Whether the macro is defined.</summary>
    public bool IsDefined(string name) => _macros.ContainsKey(name);

    /// <summary>Tries to resolve a macro body.</summary>
    public bool TryGetBody(string name, out string? body)
    {
        if (_macros.TryGetValue(name, out ShaderMacroDefinition? macro) && macro.Parameters is null)
        {
            body = macro.Body;
            return true;
        }

        body = null;
        return false;
    }

    /// <summary>Expands object-like and function-like macros with a recursion guard.</summary>
    public string Expand(string text) => ExpandInternal(text, null);

    private string ExpandInternal(string text, HashSet<string>? active)
    {
        var output = new StringBuilder(text.Length);
        int index = 0;

        while (index < text.Length)
        {
            char c = text[index];
            if (!IsIdentStart(c))
            {
                output.Append(c);
                index++;
                continue;
            }

            int end = index + 1;
            while (end < text.Length && IsIdentPart(text[end]))
            {
                end++;
            }

            string name = text[index..end];
            if (!_macros.TryGetValue(name, out ShaderMacroDefinition? macro) || (active is not null && active.Contains(name)))
            {
                output.Append(name);
                index = end;
                continue;
            }

            int consumed = end;
            if (macro.Parameters is not null)
            {
                int scan = end;
                while (scan < text.Length && (text[scan] == ' ' || text[scan] == '\t'))
                {
                    scan++;
                }

                if (!TryReadCallArguments(text, scan, out string[] args, out int nextIndex))
                {
                    output.Append(name);
                    index = end;
                    continue;
                }

                string[] expandedArgs = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    expandedArgs[i] = ExpandInternal(args[i], active);
                }

                string substituted = SubstituteParameters(macro, expandedArgs);
                active ??= new HashSet<string>(StringComparer.Ordinal);
                active.Add(name);
                output.Append(ExpandInternal(substituted, active));
                active.Remove(name);
                consumed = nextIndex;
            }
            else
            {
                active ??= new HashSet<string>(StringComparer.Ordinal);
                active.Add(name);
                output.Append(ExpandInternal(macro.Body, active));
                active.Remove(name);
            }

            index = consumed;
        }

        return output.ToString();
    }

    /// <summary>Resolves defined(X)/defined X to 1/0 against table plus lookup.</summary>
    public string ResolveDefined<TLookup>(string text, in TLookup lookup)
        where TLookup : struct, IMacroLookupPolicy
    {
        var output = new StringBuilder(text.Length);
        int index = 0;

        while (index < text.Length)
        {
            char c = text[index];
            if (!IsIdentStart(c) || !text.AsSpan(index).StartsWith("defined".AsSpan(), StringComparison.Ordinal))
            {
                output.Append(c);
                index++;
                continue;
            }

            int after = index + 7;
            if (after < text.Length && IsIdentPart(text[after]))
            {
                output.Append(c);
                index++;
                continue;
            }

            int scan = after;
            while (scan < text.Length && (text[scan] == ' ' || text[scan] == '\t'))
            {
                scan++;
            }

            bool parenthesised = scan < text.Length && text[scan] == '(';
            if (parenthesised)
            {
                scan++;
            }

            while (scan < text.Length && (text[scan] == ' ' || text[scan] == '\t'))
            {
                scan++;
            }

            int start = scan;
            while (scan < text.Length && IsIdentPart(text[scan]))
            {
                scan++;
            }

            string name = text[start..scan];
            if (parenthesised)
            {
                while (scan < text.Length && (text[scan] == ' ' || text[scan] == '\t'))
                {
                    scan++;
                }

                if (scan < text.Length && text[scan] == ')')
                {
                    scan++;
                }
            }

            output.Append(IsDefined(name) || lookup.IsDefined(new ShaderSymbol(name)) ? '1' : '0');
            index = scan;
        }

        return output.ToString();
    }

    internal static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_' || c == '$';

    internal static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '$';

    internal static ShaderMacroDefinition ParseDefine(string rest, string name)
    {
        if (rest.Length > 0 && rest[0] == '(')
        {
            int depth = 0;
            bool closed = false;
            var parameters = new List<string>();
            var current = new StringBuilder();

            for (int i = 0; i < rest.Length; i++)
            {
                char c = rest[i];
                if (c == '(')
                {
                    depth++;
                    if (depth > 1)
                    {
                        current.Append(c);
                    }

                    continue;
                }

                if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closed = true;
                        if (current.ToString().Trim().Length != 0)
                        {
                            parameters.Add(current.ToString().Trim());
                        }

                        continue;
                    }

                    current.Append(c);
                    continue;
                }

                if (c == ',' && depth == 1)
                {
                    parameters.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            if (!closed)
            {
                return new ShaderMacroDefinition(null, $"({rest}");
            }

            string body = rest[(rest.IndexOf(')', StringComparison.Ordinal) + 1)..].Trim();
            return new ShaderMacroDefinition([.. parameters.Where(static p => p.Length != 0)], body);
        }

        return new ShaderMacroDefinition(null, rest.Trim());
    }

    internal static bool TryReadCallArguments(string text, int openParenIndex, out string[] args, out int nextIndex)
    {
        args = [];
        nextIndex = openParenIndex;
        if (openParenIndex < 0 || openParenIndex >= text.Length || text[openParenIndex] != '(')
        {
            return false;
        }

        var collected = new List<string>();
        var current = new StringBuilder();
        int depth = 0;

        for (int i = openParenIndex; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '(')
            {
                depth++;
                if (depth > 1)
                {
                    current.Append(c);
                }

                continue;
            }

            if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    if (current.Length != 0 || collected.Count > 0)
                    {
                        collected.Add(current.ToString().Trim());
                    }

                    args = [.. collected];
                    nextIndex = i + 1;
                    return true;
                }

                current.Append(c);
                continue;
            }

            if (c == ',' && depth == 1)
            {
                collected.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        return false;
    }

    internal static string SubstituteParameters(ShaderMacroDefinition macro, string[] args)
    {
        if (macro.Parameters is null)
        {
            return macro.Body;
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < macro.Parameters.Length; i++)
        {
            map[macro.Parameters[i]] = i < args.Length ? args[i] : string.Empty;
        }

        var output = new StringBuilder(macro.Body.Length);
        int index = 0;
        while (index < macro.Body.Length)
        {
            char c = macro.Body[index];
            if (!IsIdentStart(c))
            {
                output.Append(c);
                index++;
                continue;
            }

            int end = index + 1;
            while (end < macro.Body.Length && IsIdentPart(macro.Body[end]))
            {
                end++;
            }

            string token = macro.Body[index..end];
            output.Append(map.TryGetValue(token, out string? replacement) ? replacement : token);
            index = end;
        }

        return output.ToString();
    }
}

/// <summary>Constant-expression evaluator: floats, hex, suffixes and ternary.</summary>
internal static class ConstantExpression
{
    private enum TokenKind : byte
    {
        Number,
        Operator,
        LeftParen,
        RightParen,
        Question,
        Colon,
    }

    private readonly record struct Token(TokenKind Kind, long Number, string Text);

    public static long Evaluate(string expression)
    {
        List<Token> tokens = Tokenize(expression);
        int cursor = 0;

        Token? Peek() => cursor < tokens.Count ? tokens[cursor] : null;
        Token Consume() => tokens[cursor++];

        long ParseTernary()
        {
            long condition = ParseBinary(0);
            if (Peek() is { Kind: TokenKind.Question })
            {
                Consume();
                long thenValue = ParseTernary();
                if (Peek() is { Kind: TokenKind.Colon })
                {
                    Consume();
                }

                long elseValue = ParseTernary();
                return condition != 0 ? thenValue : elseValue;
            }

            return condition;
        }

        long ParseBinary(int minPrecedence)
        {
            long left = ParseUnary();
            while (true)
            {
                Token? token = Peek();
                if (token is not { Kind: TokenKind.Operator })
                {
                    break;
                }

                int precedence = PrecedenceOf(token.Value.Text);
                if (precedence < 0 || precedence < minPrecedence)
                {
                    break;
                }

                Consume();
                long right = ParseBinary(precedence + 1);
                left = ApplyBinary(token.Value.Text, left, right);
            }

            return left;
        }

        long ParseUnary()
        {
            Token? token = Peek();
            if (token is { Kind: TokenKind.Operator })
            {
                switch (token.Value.Text)
                {
                    case "!":
                        Consume();
                        return ParseUnary() == 0 ? 1 : 0;
                    case "~":
                        Consume();
                        return ~ParseUnary();
                    case "-":
                        Consume();
                        return -ParseUnary();
                    case "+":
                        Consume();
                        return ParseUnary();
                }
            }

            return ParsePrimary();
        }

        long ParsePrimary()
        {
            Token token = Consume();
            if (token.Kind == TokenKind.Number)
            {
                return token.Number;
            }

            if (token.Kind == TokenKind.LeftParen)
            {
                long value = ParseTernary();
                if (Peek() is { Kind: TokenKind.RightParen })
                {
                    Consume();
                }

                return value;
            }

            return 0;
        }

        return tokens.Count == 0 ? 0 : ParseTernary();

        static int PrecedenceOf(string op) => op switch
        {
            "||" => 0,
            "&&" => 1,
            "|" => 2,
            "^" => 3,
            "&" => 4,
            "==" or "!=" => 5,
            "<" or "<=" or ">" or ">=" => 6,
            "<<" or ">>" => 7,
            "+" or "-" => 8,
            "*" or "/" or "%" => 9,
            _ => -1,
        };

        static long ApplyBinary(string op, long left, long right) => op switch
        {
            "*" => left * right,
            "/" => right == 0 ? 0 : left / right,
            "%" => right == 0 ? 0 : left % right,
            "+" => left + right,
            "-" => left - right,
            "<<" => left << (int)(right & 31),
            ">>" => left >> (int)(right & 31),
            "<" => left < right ? 1 : 0,
            "<=" => left <= right ? 1 : 0,
            ">" => left > right ? 1 : 0,
            ">=" => left >= right ? 1 : 0,
            "==" => left == right ? 1 : 0,
            "!=" => left != right ? 1 : 0,
            "&" => left & right,
            "^" => left ^ right,
            "|" => left | right,
            "&&" => left != 0 && right != 0 ? 1 : 0,
            "||" => left != 0 || right != 0 ? 1 : 0,
            _ => 0,
        };
    }

    private static List<Token> Tokenize(string expression)
    {
        var tokens = new List<Token>();
        int index = 0;

        while (index < expression.Length)
        {
            char c = expression[index];
            if (c == ' ' || c == '\t')
            {
                index++;
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && index + 1 < expression.Length && char.IsDigit(expression[index + 1])))
            {
                int end = index;
                while (end < expression.Length && IsNumberChar(expression[end]))
                {
                    end++;
                }

                string raw = expression[index..end];
                index = end;
                tokens.Add(new Token(TokenKind.Number, ParseNumber(raw), string.Empty));
                continue;
            }

            if (index + 1 < expression.Length)
            {
                string two = expression.Substring(index, 2);
                if (two is "<<" or ">>" or "<=" or ">=" or "==" or "!=" or "&&" or "||")
                {
                    tokens.Add(new Token(TokenKind.Operator, 0, two));
                    index += 2;
                    continue;
                }
            }

            if ("+-*/%<>&^|~!".Contains(c, StringComparison.Ordinal))
            {
                tokens.Add(new Token(TokenKind.Operator, 0, c.ToString()));
                index++;
                continue;
            }

            if (c == '(')
            {
                tokens.Add(new Token(TokenKind.LeftParen, 0, string.Empty));
                index++;
                continue;
            }

            if (c == ')')
            {
                tokens.Add(new Token(TokenKind.RightParen, 0, string.Empty));
                index++;
                continue;
            }

            if (c == '?')
            {
                tokens.Add(new Token(TokenKind.Question, 0, string.Empty));
                index++;
                continue;
            }

            if (c == ':')
            {
                tokens.Add(new Token(TokenKind.Colon, 0, string.Empty));
                index++;
                continue;
            }

            if (ShaderMacroTable.IsIdentStart(c))
            {
                int end = index + 1;
                while (end < expression.Length && ShaderMacroTable.IsIdentPart(expression[end]))
                {
                    end++;
                }

                index = end;
                tokens.Add(new Token(TokenKind.Number, 0, string.Empty));
                continue;
            }

            index++;
        }

        return tokens;

        static bool IsNumberChar(char ch) =>
            char.IsDigit(ch) || ch == '.' || ch is 'x' or 'X' or 'a' or 'b' or 'c' or 'd' or 'e' or 'E' or 'f' or 'F' or 'u' or 'U' or 'l' or 'L';

        static long ParseNumber(string raw)
        {
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string digits = new(raw.SkipWhile(static ch => ch is '0' or 'x' or 'X').ToArray());
                int cut = 0;
                while (cut < digits.Length && Uri.IsHexDigit(digits[cut]))
                {
                    cut++;
                }

                if (cut == 0)
                {
                    return 0;
                }

                return Convert.ToInt64(digits[..cut], 16);
            }

            int dot = raw.IndexOf('.', StringComparison.Ordinal);
            if (dot >= 0)
            {
                string head = raw[..dot];
                if (long.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole))
                {
                    return whole;
                }

                return 0;
            }

            int suffix = 0;
            while (suffix < raw.Length && char.IsDigit(raw[suffix]))
            {
                suffix++;
            }

            if (suffix == 0)
            {
                return 0;
            }

            if (long.TryParse(raw[..suffix], NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                return value;
            }

            return 0;
        }
    }
}
