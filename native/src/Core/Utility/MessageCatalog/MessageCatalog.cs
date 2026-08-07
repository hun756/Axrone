namespace Axrone.Utility.MessageCatalog;

/// <summary>
/// A type-safe catalog mapping error/info codes to human-readable message templates.
/// Thread-safety: immutable after construction.
/// </summary>
public sealed class MessageCatalog<TCode> where TCode : notnull
{
    private readonly Dictionary<TCode, string> _messages;

    internal MessageCatalog(Dictionary<TCode, string> messages) => _messages = messages;

    public string Resolve(TCode code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _messages.TryGetValue(code, out var msg) ? msg : $"Unknown message code: {code}";
    }

    public bool TryResolve(TCode code, [NotNullWhen(true)] out string? message)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _messages.TryGetValue(code, out message);
    }

    public int Count => _messages.Count;
    public bool Contains(TCode code) => _messages.ContainsKey(code);
}

public static class MessageCatalogFactory
{
    public static MessageCatalog<string> Create(params (string Code, string Message)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var dict = new Dictionary<string, string>(entries.Length, StringComparer.Ordinal);
        foreach (var (code, message) in entries)
            dict[code] = message;
        return new MessageCatalog<string>(dict);
    }

    public static MessageCatalog<TCode> Create<TCode>(params (TCode Code, string Message)[] entries)
        where TCode : notnull, Enum
    {
        ArgumentNullException.ThrowIfNull(entries);
        var dict = new Dictionary<TCode, string>(entries.Length);
        foreach (var (code, message) in entries)
            dict[code] = message;
        return new MessageCatalog<TCode>(dict);
    }

    public static MessageCatalog<TCode> FromDictionary<TCode>(IReadOnlyDictionary<TCode, string> messages)
        where TCode : notnull
    {
        ArgumentNullException.ThrowIfNull(messages);
        var dict = new Dictionary<TCode, string>(messages.Count);
        foreach (var kvp in messages)
            dict[kvp.Key] = kvp.Value;
        return new MessageCatalog<TCode>(dict);
    }
}
