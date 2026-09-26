namespace Axrone.Render.OpenGL.Tests;

using Axrone.Render.OpenGL.Context;

/// <summary>
/// Tests for <see cref="GLStateCache"/> state deduplication.
/// Verifies that redundant GL calls are suppressed and that cache
/// invalidation/reset forces fresh GL calls.
/// </summary>
public sealed class GLStateCacheTests
{
    private readonly MockGLApi _mock;
    private readonly GLStateCache _cache;

    public GLStateCacheTests()
    {
        _mock = new MockGLApi();
        _cache = new GLStateCache(_mock);
        // The constructor calls Invalidate() which sets _isInvalidated = true.
        // Reset() clears the flag so deduplication works for subsequent calls.
        _cache.Reset();
    }

    [Fact]
    public void BindArrayBuffer_FirstCall_CallsGL()
    {
        _mock.ClearCallLog();

        _cache.BindArrayBuffer(5);

        _mock.CallLog.Should().Contain(c => c.Contains("BindBuffer") && c.Contains('5'));
    }

    [Fact]
    public void BindArrayBuffer_SameValue_DoesNotCallGL()
    {
        _cache.BindArrayBuffer(5);
        _mock.ClearCallLog();

        _cache.BindArrayBuffer(5);

        _mock.CallLog.Should().NotContain(c => c.Contains("BindBuffer"));
    }

    [Fact]
    public void BindArrayBuffer_DifferentValue_CallsGL()
    {
        _cache.BindArrayBuffer(5);
        _mock.ClearCallLog();

        _cache.BindArrayBuffer(10);

        _mock.CallLog.Should().Contain(c => c.Contains("BindBuffer") && c.Contains("10"));
    }

    [Fact]
    public void BindVertexArray_InvalidatesElementBufferCache()
    {
        // Bind an element buffer to cache it
        _cache.BindElementBuffer(5);
        _mock.ClearCallLog();

        // Bind VAO — should invalidate element buffer cache
        _cache.BindVertexArray(1);

        // Now binding the same element buffer should still call GL
        _mock.ClearCallLog();
        _cache.BindElementBuffer(5);
        _mock.CallLog.Should().Contain(c => c.Contains("BindBuffer"));
    }

    [Fact]
    public void SetDepthTest_OnlyCallsGL_WhenStateChanges()
    {
        _cache.SetDepthTest(true);
        _mock.ClearCallLog();

        // Same value — no GL call
        _cache.SetDepthTest(true);
        _mock.CallLog.Should().NotContain(c => c.Contains("Enable") || c.Contains("Disable"));

        // Different value — GL call
        _cache.SetDepthTest(false);
        _mock.CallLog.Should().Contain(c => c.Contains("Disable"));
    }

    [Fact]
    public void SetBlend_OnlyCallsGL_WhenStateChanges()
    {
        _cache.SetBlend(true);
        _mock.ClearCallLog();

        // Same value — no GL call
        _cache.SetBlend(true);
        _mock.CallLog.Should().NotContain(c => c.Contains("Enable") || c.Contains("Disable"));

        // Different value — GL call
        _cache.SetBlend(false);
        _mock.CallLog.Should().Contain(c => c.Contains("Disable"));
    }

    [Fact]
    public void Invalidate_ForcesAllSubsequentCallsToGoThrough()
    {
        _cache.BindArrayBuffer(5);
        _mock.ClearCallLog();

        // Dedup works normally
        _cache.BindArrayBuffer(5);
        _mock.CallLog.Should().NotContain(c => c.Contains("BindBuffer"));

        // After invalidation, same value triggers GL call
        _cache.Invalidate();
        _cache.BindArrayBuffer(5);
        _mock.CallLog.Should().Contain(c => c.Contains("BindBuffer"));
    }

    [Fact]
    public void Reset_ReturnsToDefaults()
    {
        _cache.SetDepthTest(true);
        _cache.Reset();
        _mock.ClearCallLog();

        // After reset, depth test is at default (false), so enabling it should call GL
        _cache.SetDepthTest(true);
        _mock.CallLog.Should().Contain(c => c.Contains("Enable"));
    }

    [Fact]
    public void ActiveTexture_Deduplicates()
    {
        _cache.ActiveTexture(0);
        _mock.ClearCallLog();

        // Same unit — no GL call
        _cache.ActiveTexture(0);
        _mock.CallLog.Should().NotContain(c => c.Contains("ActiveTexture"));

        // Different unit — GL call
        _cache.ActiveTexture(1);
        _mock.CallLog.Should().Contain(c => c.Contains("ActiveTexture"));
    }

    [Fact]
    public void BindFramebuffer_HandlesFramebufferTarget()
    {
        _mock.ClearCallLog();

        _cache.BindFramebuffer(0x8D40, 5); // GL_FRAMEBUFFER

        _mock.CallLog.Should().Contain(c => c.Contains("BindFramebuffer"));
    }

    [Fact]
    public void BindFramebuffer_HandlesReadFramebufferTarget()
    {
        _mock.ClearCallLog();

        _cache.BindFramebuffer(0x8CA8, 5); // GL_READ_FRAMEBUFFER

        _mock.CallLog.Should().Contain(c => c.Contains("BindFramebuffer"));
    }

    [Fact]
    public void BindFramebuffer_HandlesDrawFramebufferTarget()
    {
        _mock.ClearCallLog();

        _cache.BindFramebuffer(0x8CA9, 5); // GL_DRAW_FRAMEBUFFER

        _mock.CallLog.Should().Contain(c => c.Contains("BindFramebuffer"));
    }

    [Fact]
    public void BindFramebuffer_SameFramebufferTarget_Deduplicates()
    {
        _cache.BindFramebuffer(0x8D40, 5); // GL_FRAMEBUFFER
        _mock.ClearCallLog();

        _cache.BindFramebuffer(0x8D40, 5);

        _mock.CallLog.Should().NotContain(c => c.Contains("BindFramebuffer"));
    }

    [Fact]
    public void SetViewport_CallsGL_WhenChanged()
    {
        _mock.ClearCallLog();

        _cache.SetViewport(0, 0, 800, 600);

        _mock.CallLog.Should().Contain(c => c.Contains("Viewport"));
    }

    [Fact]
    public void SetViewport_Deduplicates_WhenSame()
    {
        _cache.SetViewport(0, 0, 800, 600);
        _mock.ClearCallLog();

        _cache.SetViewport(0, 0, 800, 600);

        _mock.CallLog.Should().NotContain(c => c.Contains("Viewport"));
    }

    [Fact]
    public void UseProgram_Deduplicates()
    {
        _cache.UseProgram(10);
        _mock.ClearCallLog();

        _cache.UseProgram(10);
        _mock.CallLog.Should().NotContain(c => c.Contains("UseProgram"));

        _cache.UseProgram(20);
        _mock.CallLog.Should().Contain(c => c.Contains("UseProgram"));
    }
}
