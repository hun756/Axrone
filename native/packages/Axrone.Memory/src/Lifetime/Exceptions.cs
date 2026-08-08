namespace Axrone.Memory.Lifetime;

/// <summary>
/// Thrown when a singleton instance cannot be created — factory returned null,
/// constructor threw, or initialization failed.
/// </summary>
public sealed class SingletonCreationException : Exception
{
    /// <summary>The type that failed to initialize.</summary>
    public Type? TargetType { get; }

    public SingletonCreationException() { }

    public SingletonCreationException(string message) : base(message) { }

    public SingletonCreationException(string message, Exception innerException)
        : base(message, innerException) { }

    public SingletonCreationException(string message, Type targetType)
        : base(message)
    {
        TargetType = targetType;
    }

    public SingletonCreationException(string message, Type targetType, Exception innerException)
        : base(message, innerException)
    {
        TargetType = targetType;
    }
}
