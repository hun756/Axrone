using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;

namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Tests for <see cref="GLSync"/> operations including creation, wait, status,
/// disposal, and context-loss recovery.
/// </summary>
public sealed class GLSyncTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public GLSyncTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void Constructor_CreatesSyncObject()
    {
        var sync = new GLSync(_context, "test_sync");

        sync.Handle.Should().NotBe(nint.Zero);
        sync.Label.Should().Be("test_sync");
        sync.IsDisposed.Should().BeFalse();
        _context.Registry.Count.Should().Be(1);
    }

    [Fact]
    public void ClientWait_ReturnsSyncWaitResult()
    {
        var sync = new GLSync(_context);

        SyncWaitResult result = sync.ClientWait();

        result.Should().Be(SyncWaitResult.ConditionSatisfied); // Mock returns 0x9119
    }

    [Fact]
    public void ServerWait_DoesNotThrow()
    {
        var sync = new GLSync(_context);

        var action = () => sync.ServerWait();

        action.Should().NotThrow();
    }

    [Fact]
    public void GetStatus_ReturnsSyncStatus()
    {
        var sync = new GLSync(_context);

        SyncStatus status = sync.GetStatus();

        status.Should().Be(SyncStatus.Signaled); // Mock returns 0x9119
    }

    [Fact]
    public void IsSignaled_ReturnsBool()
    {
        var sync = new GLSync(_context);

        bool signaled = sync.IsSignaled();

        signaled.Should().BeTrue(); // Mock returns Signaled
    }

    [Fact]
    public void ContextLost_SetsHandleToZero()
    {
        var sync = new GLSync(_context);
        sync.Handle.Should().NotBe(nint.Zero);

        _context.NotifyContextLost();

        sync.Handle.Should().Be(nint.Zero);
    }

    [Fact]
    public void ContextRestore_GetsNewHandle()
    {
        var sync = new GLSync(_context, "test");
        nint originalHandle = sync.Handle;

        _context.NotifyContextLost();
        sync.Handle.Should().Be(nint.Zero);

        _context.NotifyContextRestored();

        sync.Handle.Should().NotBe(nint.Zero);
        sync.Handle.Should().NotBe(originalHandle);
    }

    [Fact]
    public void ClientWait_AfterDispose_Throws()
    {
        var sync = new GLSync(_context);
        sync.Dispose();

        var action = () => sync.ClientWait();

        action.Should().Throw<RenderException>();
    }

    [Fact]
    public void ServerWait_AfterDispose_Throws()
    {
        var sync = new GLSync(_context);
        sync.Dispose();

        var action = () => sync.ServerWait();

        action.Should().Throw<RenderException>();
    }

    [Fact]
    public void GetStatus_AfterDispose_Throws()
    {
        var sync = new GLSync(_context);
        sync.Dispose();

        var action = () => sync.GetStatus();

        action.Should().Throw<RenderException>();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

/// <summary>
/// Tests for <see cref="GLTransformFeedback"/> operations including creation, bind/unbind,
/// begin/end, pause/resume, buffer binding, disposal, and context-loss recovery.
/// </summary>
public sealed class GLTransformFeedbackTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public GLTransformFeedbackTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void Constructor_CreatesTransformFeedback()
    {
        var tf = new GLTransformFeedback(_context, "test_tf");

        tf.Id.Should().BeGreaterThan(0u);
        tf.Label.Should().Be("test_tf");
        tf.IsActive.Should().BeFalse();
        tf.IsDisposed.Should().BeFalse();
        _context.Registry.Count.Should().Be(1);
    }

    [Fact]
    public void Bind_BindsTransformFeedback()
    {
        var tf = new GLTransformFeedback(_context);
        _mock.ClearCallLog();

        tf.Bind();

        _mock.CallLog.Should().Contain(c => c.Contains("BindTransformFeedback") && c.Contains(tf.Id.ToString()));
    }

    [Fact]
    public void Unbind_UnbindsTransformFeedback()
    {
        var tf = new GLTransformFeedback(_context);
        _mock.ClearCallLog();

        tf.Unbind();

        _mock.CallLog.Should().Contain(c => c.Contains("BindTransformFeedback") && c.Contains('0'));
    }

    [Fact]
    public void Begin_SetsIsActive()
    {
        var tf = new GLTransformFeedback(_context);
        _mock.ClearCallLog();

        tf.Begin(GLConst.Triangles);

        tf.IsActive.Should().BeTrue();
        _mock.CallLog.Should().Contain(c => c.Contains("BeginTransformFeedback"));
    }

    [Fact]
    public void End_ClearsIsActive()
    {
        var tf = new GLTransformFeedback(_context);
        tf.Begin(GLConst.Triangles);
        _mock.ClearCallLog();

        tf.End();

        tf.IsActive.Should().BeFalse();
        _mock.CallLog.Should().Contain(c => c.Contains("EndTransformFeedback"));
    }

    [Fact]
    public void DoubleBegin_Throws()
    {
        var tf = new GLTransformFeedback(_context);
        tf.Begin(GLConst.Triangles);

        var action = () => tf.Begin(GLConst.Triangles);

        action.Should().Throw<GLException>();
    }

    [Fact]
    public void Pause_DoesNotThrow()
    {
        var tf = new GLTransformFeedback(_context);
        tf.Begin(GLConst.Triangles);
        _mock.ClearCallLog();

        var action = () => tf.Pause();

        action.Should().NotThrow();
        _mock.CallLog.Should().Contain(c => c.Contains("PauseTransformFeedback"));
    }

    [Fact]
    public void Resume_DoesNotThrow()
    {
        var tf = new GLTransformFeedback(_context);
        tf.Begin(GLConst.Triangles);
        tf.Pause();
        _mock.ClearCallLog();

        var action = () => tf.Resume();

        action.Should().NotThrow();
        _mock.CallLog.Should().Contain(c => c.Contains("ResumeTransformFeedback"));
    }

    [Fact]
    public void BindBuffer_BindsBufferToIndex()
    {
        var tf = new GLTransformFeedback(_context);
        var buffer = new GLBuffer(_context, GLConst.ArrayBuffer, GLConst.StaticDraw, 1024);
        _mock.ClearCallLog();

        tf.BindBuffer(0, buffer);

        _mock.CallLog.Should().Contain(c => c.Contains("BindBufferBase") && c.Contains(GLConst.TransformFeedbackBuffer.ToString()));
    }

    [Fact]
    public void ContextLost_SetsIdToZero()
    {
        var tf = new GLTransformFeedback(_context);
        tf.Id.Should().BeGreaterThan(0u);

        _context.NotifyContextLost();

        tf.Id.Should().Be(0u);
        tf.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ContextRestore_GetsNewId()
    {
        var tf = new GLTransformFeedback(_context, "test");
        uint originalId = tf.Id;

        _context.NotifyContextLost();
        tf.Id.Should().Be(0u);

        _context.NotifyContextRestored();

        tf.Id.Should().BeGreaterThan(0u);
        tf.Id.Should().NotBe(originalId);
    }

    [Fact]
    public void Bind_AfterDispose_Throws()
    {
        var tf = new GLTransformFeedback(_context);
        tf.Dispose();

        var action = () => tf.Bind();

        action.Should().Throw<RenderException>();
    }

    [Fact]
    public void Begin_AfterDispose_Throws()
    {
        var tf = new GLTransformFeedback(_context);
        tf.Dispose();

        var action = () => tf.Begin(GLConst.Triangles);

        action.Should().Throw<RenderException>();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
