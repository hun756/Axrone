namespace Axrone.Render.Core;

/// <summary>
/// Base exception for all rendering errors.
/// </summary>
public class RenderException : Exception
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public RenderErrorCode Code { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="code">The error code.</param>
    public RenderException(string message, RenderErrorCode code)
        : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="code">The error code.</param>
    /// <param name="innerException">The inner exception.</param>
    public RenderException(string message, RenderErrorCode code, Exception? innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}

/// <summary>
/// Exception for GL context errors.
/// </summary>
public class GLException : Exception
{
    /// <summary>
    /// Gets the error code.
    /// </summary>
    public GLContextErrorCode Code { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GLException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="code">The error code.</param>
    public GLException(string message, GLContextErrorCode code)
        : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GLException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="code">The error code.</param>
    /// <param name="innerException">The inner exception.</param>
    public GLException(string message, GLContextErrorCode code, Exception? innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}

/// <summary>
/// Exception for GL buffer errors.
/// </summary>
public class GLBufferException : GLException
{
    /// <summary>
    /// Gets the buffer error code.
    /// </summary>
    public GLBufferErrorCode BufferCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GLBufferException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="code">The buffer error code.</param>
    public GLBufferException(string message, GLBufferErrorCode code)
        : base(message, GLContextErrorCode.InvalidOperation)
    {
        BufferCode = code;
    }
}

/// <summary>
/// Exception for GL shader errors.
/// </summary>
public class GLShaderException : GLException
{
    /// <summary>
    /// Gets the shader error code.
    /// </summary>
    public GLShaderErrorCode ShaderCode { get; }

    /// <summary>
    /// Gets the shader info log (if available).
    /// </summary>
    public string? InfoLog { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GLShaderException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="code">The shader error code.</param>
    /// <param name="infoLog">The shader info log.</param>
    public GLShaderException(string message, GLShaderErrorCode code, string? infoLog = null)
        : base(message, GLContextErrorCode.InvalidOperation)
    {
        ShaderCode = code;
        InfoLog = infoLog;
    }
}

/// <summary>
/// Exception for GL framebuffer errors.
/// </summary>
public class GLFramebufferException : GLException
{
    /// <summary>
    /// Gets the framebuffer error code.
    /// </summary>
    public GLFramebufferErrorCode FramebufferCode { get; }

    /// <summary>
    /// Gets the framebuffer status (if incomplete).
    /// </summary>
    public uint? Status { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GLFramebufferException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="code">The framebuffer error code.</param>
    /// <param name="status">The framebuffer status.</param>
    public GLFramebufferException(string message, GLFramebufferErrorCode code, uint? status = null)
        : base(message, GLContextErrorCode.InvalidOperation)
    {
        FramebufferCode = code;
        Status = status;
    }
}

/// <summary>
/// Helper class for throwing exceptions with proper JIT optimization.
/// All methods are marked with [DoesNotReturn] and [MethodImpl(NoInlining)]
/// to allow the JIT to inline the calling code without exception handling overhead.
/// </summary>
public static class ThrowHelper
{
    /// <summary>Throws a context lost exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowContextLost() =>
        throw new GLException("CONTEXT_LOST", GLContextErrorCode.ContextLost);

    /// <summary>Throws a context disposed exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowContextDisposed() =>
        throw new GLException("CONTEXT_ALREADY_DISPOSED", GLContextErrorCode.ContextAlreadyDisposed);

    /// <summary>Throws an invalid operation exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidOperation(string message) =>
        throw new GLException(message, GLContextErrorCode.InvalidOperation);

    /// <summary>Throws an extension not supported exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowExtensionNotSupported(string extension) =>
        throw new GLException($"EXTENSION_NOT_SUPPORTED: {extension}", GLContextErrorCode.ExtensionNotSupported);

    /// <summary>Throws an offset negative exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowOffsetNegative() =>
        throw new GLBufferException("Offset cannot be negative", GLBufferErrorCode.InvalidOffset);

    /// <summary>Throws a buffer bounds exceeded exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowBufferBoundsExceeded() =>
        throw new GLBufferException("Offset and length exceed buffer bounds", GLBufferErrorCode.BoundsExceeded);

    /// <summary>Throws a source range exceeded exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowSourceRangeExceeded() =>
        throw new GLBufferException("Source range exceeds data bounds", GLBufferErrorCode.BoundsExceeded);

    /// <summary>Throws a destination range exceeded exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowDestRangeExceeded() =>
        throw new GLBufferException("Destination range exceeds buffer bounds", GLBufferErrorCode.BoundsExceeded);

    /// <summary>Throws an alignment violation exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowAlignmentViolation(int offset, int elemSize) =>
        throw new GLBufferException($"Byte offset ({offset}) must be a multiple of element size ({elemSize})", GLBufferErrorCode.AlignmentViolation);

    /// <summary>Throws a buffer factory disposed exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowBufferFactoryDisposed() =>
        throw new GLBufferException("BufferFactory has been disposed", GLBufferErrorCode.FactoryDisposed);

    /// <summary>Throws a buffer disposed exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowBufferDisposed() =>
        throw new GLBufferException("BUFFER_ALREADY_DISPOSED", GLBufferErrorCode.BufferAlreadyDisposed);

    /// <summary>Throws a shader compile failed exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowShaderCompileFailed(string infoLog) =>
        throw new GLShaderException($"SHADER_COMPILE_FAILED: {infoLog}", GLShaderErrorCode.ShaderCompileFailed, infoLog);

    /// <summary>Throws a shader link failed exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowShaderLinkFailed(string infoLog) =>
        throw new GLShaderException($"SHADER_LINK_FAILED: {infoLog}", GLShaderErrorCode.ShaderLinkFailed, infoLog);

    /// <summary>Throws a program disposed exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowProgramDisposed() =>
        throw new GLShaderException("PROGRAM_DISPOSED", GLShaderErrorCode.ProgramDisposed);

    /// <summary>Throws a batch overflow exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowBatchOverflow() =>
        throw new GLShaderException("BATCH_OVERFLOW", GLShaderErrorCode.BatchOverflow);

    /// <summary>Throws an incomplete framebuffer exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowIncompleteFramebuffer(uint status) =>
        throw new GLFramebufferException($"INCOMPLETE_FRAMEBUFFER: 0x{status:X4}", GLFramebufferErrorCode.IncompleteFramebuffer, status);

    /// <summary>Throws an executor not found exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowExecutorNotFound(string kind) =>
        throw new RenderException($"No render pass executor is registered for pass kind '{kind}'", RenderErrorCode.ExecutorNotFound);

    /// <summary>Throws an out of memory exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowOutOfMemory(string message) =>
        throw new GLException(message, GLContextErrorCode.OutOfMemory);

    /// <summary>Throws an object disposed exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowObjectDisposed(string objectName) =>
        throw new ObjectDisposedException(objectName);

    /// <summary>Throws an argument null exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentNull(string paramName) =>
        throw new ArgumentNullException(paramName);

    /// <summary>Throws an argument out of range exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentOutOfRange(string paramName, object? actualValue = null, string? message = null) =>
        throw new ArgumentOutOfRangeException(paramName, actualValue, message);

    /// <summary>Throws an invalid argument exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidArgument(string message) =>
        throw new ArgumentException(message);

    /// <summary>Throws a not implemented exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotImplemented(string? message = null) =>
        throw new NotImplementedException(message);

    /// <summary>Throws an unsupported operation exception.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowUnsupportedOperation(string message) =>
        throw new NotSupportedException(message);

    /// <summary>Throws an invalid operation exception with context error code.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidValue(string message) =>
        throw new GLException(message, GLContextErrorCode.InvalidValue);

    /// <summary>Generic throw helper for render errors with resource context.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(RenderErrorCode code, string message, string context) =>
        throw new RenderException($"{context}: {message}", code);
}
