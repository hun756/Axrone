namespace Axrone.Collections;

public class RingBufferException : Exception
{
    public RingBufferException() { }
    public RingBufferException(string? message) : base(message) { }
    public RingBufferException(string? message, Exception? innerException) : base(message, innerException) { }
}

public sealed class RingBufferFullException : RingBufferException
{
    public RingBufferFullException() : base("The ring buffer is full.") { }
    public RingBufferFullException(string? message) : base(message) { }
    public RingBufferFullException(string? message, Exception? innerException) : base(message, innerException) { }
}

public sealed class RingBufferEmptyException : RingBufferException
{
    public RingBufferEmptyException() : base("The ring buffer is empty.") { }
    public RingBufferEmptyException(string? message) : base(message) { }
    public RingBufferEmptyException(string? message, Exception? innerException) : base(message, innerException) { }
}
