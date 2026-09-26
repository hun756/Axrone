namespace Axrone.Render.OpenGL.Tests;

using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;

/// <summary>
/// Tests for <see cref="GLBuffer"/> operations including creation, data upload,
/// resize, copy, readback, disposal, and context-loss recovery via snapshots.
/// </summary>
public sealed class GLBufferTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public GLBufferTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void Constructor_CreatesBuffer_AndRegistersWithContext()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 1024, "test_buffer");

        buffer.Id.Should().BeGreaterThan(0u);
        buffer.ByteLength.Should().Be(1024);
        buffer.Target.Should().Be(GLConst.ArrayBuffer);
        buffer.Usage.Should().Be(GLConst.StaticDraw);
        buffer.Label.Should().Be("test_buffer");
        buffer.IsDisposed.Should().BeFalse();
        _context.Registry.Count.Should().Be(1);
    }

    [Fact]
    public void Update_UploadsData_AndCreatesSnapshot()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 4);
        _mock.ClearCallLog();

        byte[] data = [1, 2, 3, 4];
        buffer.Update(data);

        _mock.CallLog.Should().Contain(c => c.Contains("BufferSubData"));
    }

    [Fact]
    public void Update_WithOffset_WithinBounds_Succeeds()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 8);

        byte[] data = [1, 2, 3, 4];
        var action = () => buffer.Update(data, byteOffset: 4);

        action.Should().NotThrow();
    }

    [Fact]
    public void Update_WithOffset_ExceedingBounds_Throws()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 4);

        byte[] data = [1, 2, 3, 4];
        var action = () => buffer.Update(data, byteOffset: 4);

        action.Should().Throw<GLBufferException>();
    }

    [Fact]
    public void Update_WithNegativeOffset_Throws()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 8);

        byte[] data = [1, 2];
        var action = () => buffer.Update(data, byteOffset: -1);

        action.Should().Throw<GLBufferException>();
    }

    [Fact]
    public void Resize_ChangesBufferSize()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 1024);
        _mock.ClearCallLog();

        buffer.Resize(2048);

        buffer.ByteLength.Should().Be(2048);
        _mock.CallLog.Should().Contain(c => c.Contains("BufferData"));
    }

    [Fact]
    public void Resize_NegativeSize_Throws()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 1024);

        var action = () => buffer.Resize(-1);

        action.Should().Throw<GLException>();
    }

    [Fact]
    public void CopyTo_CopiesBetweenBuffers()
    {
        var src = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 1024, "src");
        var dst = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 1024, "dst");
        _mock.ClearCallLog();

        src.CopyTo(dst, size: 512);

        _mock.CallLog.Should().Contain(c => c.Contains("CopyBufferSubData"));
    }

    [Fact]
    public void GetData_ReadsFromBuffer()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 4);
        _mock.ClearCallLog();

        byte[] output = new byte[4];
        buffer.GetData(output);

        _mock.CallLog.Should().Contain(c => c.Contains("GetBufferSubData"));
    }

    [Fact]
    public void Dispose_DeletesBuffer_AndUnregisters()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 1024);
        uint id = buffer.Id;
        _mock.ClearCallLog();

        buffer.Dispose();

        buffer.IsDisposed.Should().BeTrue();
        buffer.Id.Should().Be(0u);
        _mock.CallLog.Should().Contain(c => c.Contains($"DeleteBuffer({id})"));
        _context.Registry.Count.Should().Be(0);
    }

    [Fact]
    public void ContextLost_SetsIdToZero()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 1024);
        buffer.Id.Should().BeGreaterThan(0u);

        _context.NotifyContextLost();

        buffer.Id.Should().Be(0u);
    }

    [Fact]
    public void ContextRestore_GetsNewId_AndRestoresData()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 4, "test");
        uint originalId = buffer.Id;

        // Upload data (creates snapshot)
        byte[] data = [1, 2, 3, 4];
        buffer.Update(data);

        // Context loss
        _context.NotifyContextLost();
        buffer.Id.Should().Be(0u);

        // Context restore
        _context.NotifyContextRestored();

        buffer.Id.Should().BeGreaterThan(0u);
        buffer.Id.Should().NotBe(originalId);
    }

    [Fact]
    public void Update_AfterDispose_Throws()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 4);
        buffer.Dispose();

        byte[] data = [1, 2, 3, 4];
        var action = () => buffer.Update(data);

        action.Should().Throw<GLBufferException>();
    }

    [Fact]
    public void GetData_AfterDispose_Throws()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 4);
        buffer.Dispose();

        byte[] output = new byte[4];
        var action = () => buffer.GetData(output);

        action.Should().Throw<GLBufferException>();
    }

    [Fact]
    public void Resize_AfterDispose_Throws()
    {
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 1024);
        buffer.Dispose();

        var action = () => buffer.Resize(2048);

        action.Should().Throw<GLBufferException>();
    }

    [Fact]
    public void Constructor_NegativeCapacity_Throws()
    {
        var action = () => new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, -1);

        action.Should().Throw<GLException>();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
