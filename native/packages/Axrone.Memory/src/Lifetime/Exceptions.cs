namespace Enterprise.Patterns.Singleton;

public class SingletonException : Exception
{
    public SingletonException() { }
    public SingletonException(string message) : base(message) { }
    public SingletonException(string message, Exception? innerException) : base(message, innerException) { }
}

public sealed class SingletonInitializationException : SingletonException
{
    public SingletonInitializationException() { }
    public SingletonInitializationException(string message) : base(message) { }
    public SingletonInitializationException(string message, Exception? innerException) : base(message, innerException) { }
}

public sealed class SingletonDisposedException : SingletonException
{
    public SingletonDisposedException() { }
    public SingletonDisposedException(string message) : base(message) { }
    public SingletonDisposedException(string message, Exception? innerException) : base(message, innerException) { }
}

public sealed class SingletonAlreadyInitializedException : SingletonException
{
    public SingletonAlreadyInitializedException() { }
    public SingletonAlreadyInitializedException(string message) : base(message) { }
    public SingletonAlreadyInitializedException(string message, Exception? innerException) : base(message, innerException) { }
}
