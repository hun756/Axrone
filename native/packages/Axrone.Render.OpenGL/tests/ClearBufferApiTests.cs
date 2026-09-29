namespace Axrone.Render.OpenGL.Tests;

using Axrone.Render.OpenGL.Context;

/// <summary>
/// Tests for the per-attachment clear and framebuffer invalidate surface of IGLApi.
/// Each entry point must reach the backend exactly once under its own call name, so the
/// load/store model can tell glClearBufferfv from glClearBufferiv and friends from the log alone.
/// </summary>
public sealed class ClearBufferApiTests
{
    private static GLContext CreateContext(out MockGLApi mock)
    {
        mock = new MockGLApi();
        return new GLContext(mock);
    }

    [Fact]
    public void ClearBufferfv_RecordsSingleCall()
    {
        using GLContext context = CreateContext(out MockGLApi mock);
        mock.ClearCallLog();

        context.GL.ClearBufferfv(0x1900, 0, [0.1f, 0.2f, 0.3f, 1.0f]);

        mock.CallLog.Should().ContainSingle(c => c.Contains("ClearBufferfv", StringComparison.Ordinal));
    }

    [Fact]
    public void ClearBufferiv_RecordsSingleCall()
    {
        using GLContext context = CreateContext(out MockGLApi mock);
        mock.ClearCallLog();

        context.GL.ClearBufferiv(0x1900, 1, [7]);

        mock.CallLog.Should().ContainSingle(c => c.Contains("ClearBufferiv", StringComparison.Ordinal));
    }

    [Fact]
    public void ClearBufferuiv_RecordsSingleCall()
    {
        using GLContext context = CreateContext(out MockGLApi mock);
        mock.ClearCallLog();

        context.GL.ClearBufferuiv(0x1900, 0, [7u, 9u]);

        mock.CallLog.Should().ContainSingle(c => c.Contains("ClearBufferuiv", StringComparison.Ordinal));
    }

    [Fact]
    public void ClearBufferfi_RecordsSingleCall()
    {
        using GLContext context = CreateContext(out MockGLApi mock);
        mock.ClearCallLog();

        context.GL.ClearBufferfi(0x1801, 0, 1.0f, 3);

        mock.CallLog.Should().ContainSingle(c => c.Contains("ClearBufferfi", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidateFramebuffer_RecordsSingleCall()
    {
        using GLContext context = CreateContext(out MockGLApi mock);
        mock.ClearCallLog();

        context.GL.InvalidateFramebuffer(0x8CA9, [0x8CE0, 0x8D00, 0x8D20]);

        mock.CallLog.Should().ContainSingle(c => c.Contains("InvalidateFramebuffer", StringComparison.Ordinal));
    }

    [Fact]
    public void ClearBufferSurface_StaysDistinctFromLegacyClear()
    {
        using GLContext context = CreateContext(out MockGLApi mock);
        mock.ClearCallLog();

        context.GL.Clear(0x4000);
        context.GL.ClearBufferfv(0x1900, 0, [0f, 0f, 0f, 1f]);

        mock.CallLog.Should().ContainSingle(c => c.StartsWith("Clear(", StringComparison.Ordinal));
        mock.CallLog.Should().ContainSingle(c => c.StartsWith("ClearBufferfv(", StringComparison.Ordinal));
    }
}
