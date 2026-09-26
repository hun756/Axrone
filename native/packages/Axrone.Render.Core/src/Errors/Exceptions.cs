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
