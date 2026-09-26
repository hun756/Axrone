using System.Runtime.CompilerServices;
using System.Threading;
using Axrone.Render.Core;
using Axrone.Render.Core.Abstractions;
using Axrone.Render.OpenGL.Context;

namespace Axrone.Render.OpenGL.Resources;

/// <summary>
/// GPU buffer resource with context-loss recovery and snapshot-based restoration.
/// </summary>
public sealed class GLBuffer : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private byte[]? _snapshot;
    private int _isDisposed;

    /// <summary>
    /// Gets the buffer ID.
    /// </summary>
    public uint Id { get; private set; }

    /// <summary>
    /// Gets the buffer target.
    /// </summary>
    public uint Target { get; }

    /// <summary>
    /// Gets the buffer usage hint.
    /// </summary>
    public uint Usage { get; }

    /// <summary>
    /// Gets the buffer size in bytes.
    /// </summary>
    public int ByteLength { get; private set; }

    /// <summary>
    /// Gets the buffer label.
    /// </summary>
    public string Label { get; }

    /// <inheritdoc/>
    public int RegistrySequence { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 10;

    /// <summary>
    /// Gets a value indicating whether the buffer has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLBuffer"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="target">The buffer target.</param>
    /// <param name="usage">The buffer usage hint.</param>
    /// <param name="initialCapacity">The initial capacity in bytes.</param>
    /// <param name="label">The debug label.</param>
    public GLBuffer(GLContext context, uint target, uint usage, int initialCapacity, string label = "")
    {
        ArgumentNullException.ThrowIfNull(context);

        if (initialCapacity < 0)
            ThrowHelper.ThrowInvalidValue("Buffer capacity cannot be negative");

        _context = context;
        Target = target;
        Usage = usage;
        ByteLength = initialCapacity;
        Label = label;

        Rebuild();
        _context.Registry.Register(this);
    }

    /// <summary>
    /// Updates the buffer with new data.
    /// </summary>
    /// <param name="data">The data to upload.</param>
    /// <param name="byteOffset">The offset in bytes.</param>
    public unsafe void Update(ReadOnlySpan<byte> data, int byteOffset = 0)
    {
        if (IsDisposed)
            ThrowHelper.ThrowBufferDisposed();

        if (_context.IsLost)
            ThrowHelper.ThrowContextLost();

        if (byteOffset < 0)
            ThrowHelper.ThrowOffsetNegative();

        if (byteOffset + data.Length > ByteLength)
            ThrowHelper.ThrowBufferBoundsExceeded();

        _context.State.BindArrayBuffer(Id);

        fixed (byte* ptr = data)
        {
            _context.GL.BufferSubData(Target, byteOffset, (nuint)data.Length, ptr);
        }

        // Snapshot full-buffer updates for context-loss recovery
        if (byteOffset == 0 && data.Length == ByteLength)
        {
            _snapshot ??= new byte[ByteLength];
            data.CopyTo(_snapshot);
        }
    }

    /// <summary>
    /// Updates a range of the buffer.
    /// </summary>
    /// <param name="data">The data to upload.</param>
    /// <param name="dstByteOffset">The destination offset in bytes.</param>
    /// <param name="srcByteOffset">The source offset in bytes.</param>
    /// <param name="length">The length in bytes.</param>
    public unsafe void UpdateRange(ReadOnlySpan<byte> data, int dstByteOffset, int srcByteOffset = 0, int length = -1)
    {
        if (IsDisposed)
            ThrowHelper.ThrowBufferDisposed();

        if (_context.IsLost)
            ThrowHelper.ThrowContextLost();

        if (dstByteOffset < 0 || srcByteOffset < 0)
            ThrowHelper.ThrowOffsetNegative();

        if (length < 0)
            length = data.Length - srcByteOffset;

        if (srcByteOffset + length > data.Length)
            ThrowHelper.ThrowSourceRangeExceeded();

        if (dstByteOffset + length > ByteLength)
            ThrowHelper.ThrowDestRangeExceeded();

        _context.State.BindArrayBuffer(Id);

        fixed (byte* ptr = &data[srcByteOffset])
        {
            _context.GL.BufferSubData(Target, dstByteOffset, (nuint)length, ptr);
        }
    }

    /// <summary>
    /// Resizes the buffer.
    /// </summary>
    /// <param name="newSize">The new size in bytes.</param>
    public unsafe void Resize(int newSize)
    {
        if (IsDisposed)
            ThrowHelper.ThrowBufferDisposed();

        if (_context.IsLost)
            ThrowHelper.ThrowContextLost();

        if (newSize < 0)
            ThrowHelper.ThrowInvalidValue("Buffer size cannot be negative");

        ByteLength = newSize;
        _snapshot = null;

        _context.State.BindArrayBuffer(Id);
        _context.GL.BufferData(Target, (nuint)newSize, null, Usage);
    }

    /// <summary>
    /// Copies data from this buffer to another.
    /// </summary>
    /// <param name="destination">The destination buffer.</param>
    /// <param name="srcOffset">Source offset in bytes.</param>
    /// <param name="dstOffset">Destination offset in bytes.</param>
    /// <param name="size">Size in bytes to copy.</param>
    public void CopyTo(GLBuffer destination, int srcOffset = 0, int dstOffset = 0, int size = -1)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (IsDisposed)
            ThrowHelper.ThrowBufferDisposed();

        if (destination.IsDisposed)
            ThrowHelper.ThrowInvalidOperation("Cannot copy to a disposed buffer");

        if (_context.IsLost)
            ThrowHelper.ThrowContextLost();

        if (size < 0)
            size = Math.Min(ByteLength - srcOffset, destination.ByteLength - dstOffset);

        if (size <= 0)
            return;

        if (srcOffset + size > ByteLength)
            ThrowHelper.ThrowBufferBoundsExceeded();

        if (dstOffset + size > destination.ByteLength)
            ThrowHelper.ThrowBufferBoundsExceeded();

        _context.State.BindCopyReadBuffer(Id);
        _context.State.BindCopyWriteBuffer(destination.Id);
        _context.GL.CopyBufferSubData(0x8F36, 0x8F37, srcOffset, dstOffset, (nuint)size); // COPY_READ_BUFFER, COPY_WRITE_BUFFER
    }

    /// <summary>
    /// Reads data from the buffer.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="byteOffset">The offset in bytes.</param>
    /// <param name="length">The length in bytes.</param>
    public unsafe void GetData(Span<byte> output, int byteOffset = 0, int length = -1)
    {
        if (IsDisposed)
            ThrowHelper.ThrowBufferDisposed();

        if (_context.IsLost)
            ThrowHelper.ThrowContextLost();

        if (byteOffset < 0)
            ThrowHelper.ThrowOffsetNegative();

        if (length < 0)
            length = Math.Min(ByteLength - byteOffset, output.Length);

        if (byteOffset + length > ByteLength)
            ThrowHelper.ThrowBufferBoundsExceeded();

        if (length > output.Length)
            ThrowHelper.ThrowDestRangeExceeded();

        if (length == 0)
            return;

        // Bind to PIXEL_PACK_BUFFER for readback
        _context.State.BindArrayBuffer(Id); // Note: Should use PIXEL_PACK_BUFFER but simplified here

        fixed (byte* ptr = output)
        {
            _context.GL.GetBufferSubData(Target, byteOffset, (nuint)length, ptr);
        }
    }

    /// <inheritdoc/>
    public void Invalidate() => Id = 0;

    /// <inheritdoc/>
    public void OnContextLost() => Invalidate();

    /// <inheritdoc/>
    public unsafe void OnContextRestored() => Rebuild();

    /// <inheritdoc/>
    public unsafe void Rebuild()
    {
        if (IsDisposed)
            return;

        Id = _context.GL.GenBuffer();
        _context.State.BindArrayBuffer(Id);

        if (_snapshot != null)
        {
            fixed (byte* ptr = _snapshot)
            {
                _context.GL.BufferData(Target, (nuint)ByteLength, ptr, Usage);
            }
        }
        else
        {
            _context.GL.BufferData(Target, (nuint)ByteLength, null, Usage);
        }

        if (!string.IsNullOrEmpty(Label))
        {
            _context.GL.ObjectLabel(0x9151, Id, (uint)Label.Length, Label); // GL_BUFFER
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            if (Id != 0)
            {
                _context.GL.DeleteBuffer(Id);
                Id = 0;
            }

            _context.Registry.Unregister(this);
            _snapshot = null;
        }
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLBuffer: Id={Id}, Target={Target}, Size={ByteLength}, Label=\"{Label}\"";
}
