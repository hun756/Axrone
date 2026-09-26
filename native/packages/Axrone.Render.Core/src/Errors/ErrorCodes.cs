namespace Axrone.Render.Core;

/// <summary>
/// GL context error codes.
/// </summary>
public enum GLContextErrorCode : uint
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>GL context has been lost.</summary>
    ContextLost = 1,

    /// <summary>GL context has already been disposed.</summary>
    ContextAlreadyDisposed = 2,

    /// <summary>Failed to create GL context.</summary>
    ContextCreationFailed = 3,

    /// <summary>Invalid GL operation.</summary>
    InvalidOperation = 4,

    /// <summary>Invalid value parameter.</summary>
    InvalidValue = 5,

    /// <summary>Out of GPU memory.</summary>
    OutOfMemory = 6,

    /// <summary>Operation not supported.</summary>
    UnsupportedOperation = 7,

    /// <summary>Required extension not supported.</summary>
    ExtensionNotSupported = 8,

    /// <summary>Failed to query capability.</summary>
    CapabilityQueryFailed = 9
}

/// <summary>
/// GL buffer error codes.
/// </summary>
public enum GLBufferErrorCode : uint
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>Buffer has already been disposed.</summary>
    BufferAlreadyDisposed = 1,

    /// <summary>Invalid offset parameter.</summary>
    InvalidOffset = 2,

    /// <summary>Operation exceeds buffer bounds.</summary>
    BoundsExceeded = 3,

    /// <summary>Alignment violation.</summary>
    AlignmentViolation = 4,

    /// <summary>Buffer factory has been disposed.</summary>
    FactoryDisposed = 5,

    /// <summary>Invalid buffer target.</summary>
    InvalidTarget = 6,

    /// <summary>Invalid buffer usage.</summary>
    InvalidUsage = 7
}

/// <summary>
/// GL shader error codes.
/// </summary>
public enum GLShaderErrorCode : uint
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>Shader compilation failed.</summary>
    ShaderCompileFailed = 1,

    /// <summary>Program linking failed.</summary>
    ShaderLinkFailed = 2,

    /// <summary>Program has been disposed.</summary>
    ProgramDisposed = 3,

    /// <summary>Program is not linked.</summary>
    ProgramNotLinked = 4,

    /// <summary>Uniform not found.</summary>
    UniformNotFound = 5,

    /// <summary>Attribute not found.</summary>
    AttributeNotFound = 6,

    /// <summary>Invalid argument.</summary>
    InvalidArgument = 7,

    /// <summary>Invalid value type.</summary>
    InvalidValueType = 8,

    /// <summary>Batch overflow.</summary>
    BatchOverflow = 9,

    /// <summary>Batch already started.</summary>
    BatchAlreadyStarted = 10,

    /// <summary>Batch not started.</summary>
    BatchNotStarted = 11,

    /// <summary>Out of memory.</summary>
    OutOfMemory = 12,

    /// <summary>Backend unavailable.</summary>
    BackendUnavailable = 13
}

/// <summary>
/// GL framebuffer error codes.
/// </summary>
public enum GLFramebufferErrorCode : uint
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>Texture has already been disposed.</summary>
    TextureAlreadyDisposed = 1,

    /// <summary>Renderbuffer has already been disposed.</summary>
    RenderbufferAlreadyDisposed = 2,

    /// <summary>Framebuffer has already been disposed.</summary>
    FramebufferAlreadyDisposed = 3,

    /// <summary>Invalid attachment configuration.</summary>
    InvalidAttachment = 4,

    /// <summary>Framebuffer is incomplete.</summary>
    IncompleteFramebuffer = 5,

    /// <summary>Unsupported attachment type.</summary>
    UnsupportedAttachment = 6,

    /// <summary>Failed to create framebuffer.</summary>
    CreationFailed = 7,

    /// <summary>Framebuffer unsupported attachment.</summary>
    FramebufferUnsupportedAttachment = 8,

    /// <summary>Framebuffer create failed.</summary>
    FramebufferCreateFailed = 9,

    /// <summary>Source texture invalid.</summary>
    SourceTextureInvalid = 10,

    /// <summary>Program create failed.</summary>
    ProgramCreateFailed = 11,

    /// <summary>Shader create failed.</summary>
    ShaderCreateFailed = 12
}

/// <summary>
/// Render pipeline error codes.
/// </summary>
public enum RenderErrorCode : uint
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>Backend execution failed.</summary>
    BackendFailed = 1,

    /// <summary>Executor not found for pass kind.</summary>
    ExecutorNotFound = 2,

    /// <summary>Source texture is invalid.</summary>
    SourceTextureInvalid = 3,

    /// <summary>Pass execution failed.</summary>
    PassExecutionFailed = 4,

    /// <summary>Frame graph contains cycles.</summary>
    GraphCycleDetected = 5,

    /// <summary>Resource allocation failed.</summary>
    ResourceAllocationFailed = 6,

    /// <summary>Invalid pass configuration.</summary>
    InvalidPassConfiguration = 7,

    /// <summary>Invalid operation on a disposed or invalid resource.</summary>
    InvalidOperation = 8,

    /// <summary>GL context has been lost.</summary>
    ContextLost = 9,

    /// <summary>Operation is not supported.</summary>
    UnsupportedOperation = 10,

    /// <summary>Invalid value parameter.</summary>
    InvalidValue = 11,

    /// <summary>Invalid attachment configuration.</summary>
    InvalidAttachment = 12,

    /// <summary>Framebuffer is incomplete.</summary>
    IncompleteFramebuffer = 13
}

/// <summary>
/// Mesh error codes.
/// </summary>
public enum MeshErrorCode : uint
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>Mesh has been disposed.</summary>
    MeshDisposed = 1,

    /// <summary>Invalid vertex layout.</summary>
    InvalidVertexLayout = 2,

    /// <summary>Attribute not found.</summary>
    AttributeNotFound = 3,

    /// <summary>Invalid index type.</summary>
    InvalidIndexType = 4,

    /// <summary>Index out of range.</summary>
    IndexOutOfRange = 5,

    /// <summary>Invalid topology.</summary>
    InvalidTopology = 6,

    /// <summary>Invalid data format.</summary>
    InvalidDataFormat = 7
}

/// <summary>
/// Texture error codes.
/// </summary>
public enum TextureErrorCode : uint
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>Texture has been disposed.</summary>
    TextureDisposed = 1,

    /// <summary>Invalid texture format.</summary>
    InvalidFormat = 2,

    /// <summary>Invalid texture dimensions.</summary>
    InvalidDimensions = 3,

    /// <summary>Invalid mip level.</summary>
    InvalidMipLevel = 4,

    /// <summary>Invalid array layer.</summary>
    InvalidArrayLayer = 5,

    /// <summary>Unsupported operation.</summary>
    UnsupportedOperation = 6,

    /// <summary>Format not supported.</summary>
    FormatNotSupported = 7,

    /// <summary>Sampler disposed.</summary>
    SamplerDisposed = 8
}
