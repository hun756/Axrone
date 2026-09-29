namespace Axrone.Render.Core;

/// <summary>
/// Represents a GPU texture handle.
/// </summary>
/// <param name="Id">The texture ID.</param>
/// <param name="Width">The texture width.</param>
/// <param name="Height">The texture height.</param>
/// <param name="Format">The texture format.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GpuTextureHandle(uint Id, int Width, int Height, uint Format)
{
    /// <summary>
    /// Gets a null/invalid texture handle.
    /// </summary>
    public static readonly GpuTextureHandle Null = new(0, 0, 0, 0);

    /// <summary>
    /// Gets a value indicating whether this handle is valid (non-zero ID).
    /// </summary>
    public bool IsValid => Id != 0;
}

/// <summary>
/// Represents the default framebuffer (backbuffer).
/// </summary>
/// <param name="Handle">The framebuffer handle (always 0 for default).</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DefaultFramebuffer(uint Handle)
{
    /// <summary>
    /// Gets the singleton instance of the default framebuffer.
    /// </summary>
    public static readonly DefaultFramebuffer Instance = new(0);
}

/// <summary>
/// Represents a native GPU handle that can be either a texture or the default framebuffer.
/// Uses explicit field layout for efficient union-like behavior.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public readonly struct NativeHandle : IEquatable<NativeHandle>
{
    /// <summary>
    /// Gets a value indicating whether this handle represents the default framebuffer.
    /// </summary>
    [FieldOffset(0)]
    public readonly bool IsDefaultFramebuffer;

    /// <summary>
    /// Gets the texture handle (valid when IsDefaultFramebuffer is false).
    /// </summary>
    [FieldOffset(4)]
    public readonly GpuTextureHandle TextureHandle;

    /// <summary>
    /// Gets the default framebuffer handle (valid when IsDefaultFramebuffer is true).
    /// </summary>
    [FieldOffset(4)]
    public readonly DefaultFramebuffer FramebufferHandle;

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeHandle"/> struct from a texture handle.
    /// </summary>
    /// <param name="texture">The texture handle.</param>
    public NativeHandle(GpuTextureHandle texture)
    {
        IsDefaultFramebuffer = false;
        FramebufferHandle = default;
        TextureHandle = texture;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeHandle"/> struct from a default framebuffer.
    /// </summary>
    /// <param name="fb">The default framebuffer.</param>
    public NativeHandle(DefaultFramebuffer fb)
    {
        IsDefaultFramebuffer = true;
        TextureHandle = default;
        FramebufferHandle = fb;
    }

    /// <summary>
    /// Creates a native handle from a texture handle.
    /// </summary>
    /// <param name="texture">The texture handle.</param>
    /// <returns>A native handle representing the texture.</returns>
    public static NativeHandle FromTexture(GpuTextureHandle texture) => new(texture);

    /// <summary>
    /// Creates a native handle from the default framebuffer.
    /// </summary>
    /// <returns>A native handle representing the default framebuffer.</returns>
    public static NativeHandle FromDefaultFramebuffer() => new(DefaultFramebuffer.Instance);

    /// <inheritdoc/>
    public bool Equals(NativeHandle other)
    {
        if (IsDefaultFramebuffer != other.IsDefaultFramebuffer)
            return false;

        return IsDefaultFramebuffer
            ? FramebufferHandle.Equals(other.FramebufferHandle)
            : TextureHandle.Equals(other.TextureHandle);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NativeHandle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        IsDefaultFramebuffer ? FramebufferHandle.GetHashCode() : TextureHandle.GetHashCode();

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(NativeHandle left, NativeHandle right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(NativeHandle left, NativeHandle right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() =>
        IsDefaultFramebuffer ? "DefaultFramebuffer" : $"Texture({TextureHandle.Id})";
}

/// <summary>
/// Represents a GPU buffer handle.
/// </summary>
/// <param name="Id">The buffer ID.</param>
/// <param name="Size">The buffer size in bytes.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GpuBufferHandle(uint Id, int Size)
{
    /// <summary>
    /// Gets a null/invalid buffer handle.
    /// </summary>
    public static readonly GpuBufferHandle Null = new(0, 0);

    /// <summary>
    /// Gets a value indicating whether this handle is valid (non-zero ID).
    /// </summary>
    public bool IsValid => Id != 0;
}

/// <summary>
/// Represents a GPU vertex array handle.
/// </summary>
/// <param name="Id">The vertex array ID.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GpuVertexArrayHandle(uint Id)
{
    /// <summary>
    /// Gets a null/invalid vertex array handle.
    /// </summary>
    public static readonly GpuVertexArrayHandle Null = new(0);

    /// <summary>
    /// Gets a value indicating whether this handle is valid (non-zero ID).
    /// </summary>
    public bool IsValid => Id != 0;
}

/// <summary>
/// Represents a GPU framebuffer handle.
/// </summary>
/// <param name="Id">The framebuffer ID.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GpuFramebufferHandle(uint Id)
{
    /// <summary>
    /// Gets a null/invalid framebuffer handle.
    /// </summary>
    public static readonly GpuFramebufferHandle Null = new(0);

    /// <summary>
    /// Gets a value indicating whether this handle is valid (non-zero ID).
    /// </summary>
    public bool IsValid => Id != 0;
}

/// <summary>
/// Represents a GPU program handle.
/// </summary>
/// <param name="Id">The program ID.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GpuProgramHandle(uint Id)
{
    /// <summary>
    /// Gets a null/invalid program handle.
    /// </summary>
    public static readonly GpuProgramHandle Null = new(0);

    /// <summary>
    /// Gets a value indicating whether this handle is valid (non-zero ID).
    /// </summary>
    public bool IsValid => Id != 0;
}

/// <summary>
/// Represents a GPU sampler handle.
/// </summary>
/// <param name="Id">The sampler ID.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GpuSamplerHandle(uint Id)
{
    /// <summary>
    /// Gets a null/invalid sampler handle.
    /// </summary>
    public static readonly GpuSamplerHandle Null = new(0);

    /// <summary>
    /// Gets a value indicating whether this handle is valid (non-zero ID).
    /// </summary>
    public bool IsValid => Id != 0;
}

/// <summary>
/// Represents a GPU renderbuffer handle.
/// </summary>
/// <param name="Id">The renderbuffer ID.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GpuRenderbufferHandle(uint Id)
{
    /// <summary>
    /// Gets a null/invalid renderbuffer handle.
    /// </summary>
    public static readonly GpuRenderbufferHandle Null = new(0);

    /// <summary>
    /// Gets a value indicating whether this handle is valid (non-zero ID).
    /// </summary>
    public bool IsValid => Id != 0;
}

/// <summary>
/// Represents a GPU query handle.
/// </summary>
/// <param name="Id">The query ID.</param>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct GpuQueryHandle(uint Id)
{
    /// <summary>
    /// Gets a null/invalid query handle.
    /// </summary>
    public static readonly GpuQueryHandle Null = new(0);

    /// <summary>
    /// Gets a value indicating whether this handle is valid (non-zero ID).
    /// </summary>
    public bool IsValid => Id != 0;
}

/// <summary>
/// Represents a GPU sync handle.
/// </summary>
/// <param name="Handle">The sync handle (pointer).</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct GpuSyncHandle(nint Handle)
{
    /// <summary>
    /// Gets a null/invalid sync handle.
    /// </summary>
    public static readonly GpuSyncHandle Null = new(0);

    /// <summary>
    /// Gets a value indicating whether this handle is valid (non-zero pointer).
    /// </summary>
    public bool IsValid => Handle != 0;
}
