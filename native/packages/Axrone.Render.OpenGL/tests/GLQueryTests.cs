namespace Axrone.Render.OpenGL.Tests;

using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;

/// <summary>
/// Tests for <see cref="GLQuery"/> operations including creation, begin/end,
/// result retrieval, disposal, and context-loss recovery.
/// </summary>
public sealed class GLQueryTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public GLQueryTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void Constructor_CreatesQuery_WithCorrectTarget()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed, "test_query");

        query.Id.Should().BeGreaterThan(0u);
        query.Target.Should().Be(QueryTarget.SamplesPassed);
        query.Label.Should().Be("test_query");
        query.IsActive.Should().BeFalse();
        query.IsDisposed.Should().BeFalse();
        _context.Registry.Count.Should().Be(1);
    }

    [Fact]
    public void Begin_StartsQuery_AndSetsIsActive()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed);
        _mock.ClearCallLog();

        query.Begin();

        query.IsActive.Should().BeTrue();
        _mock.CallLog.Should().Contain(c => c.Contains("BeginQuery"));
    }

    [Fact]
    public void End_EndsQuery_AndClearsIsActive()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed);
        query.Begin();
        _mock.ClearCallLog();

        query.End();

        query.IsActive.Should().BeFalse();
        _mock.CallLog.Should().Contain(c => c.Contains("EndQuery"));
    }

    [Fact]
    public void DoubleBegin_Throws()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed);
        query.Begin();

        var action = () => query.Begin();

        action.Should().Throw<RenderException>();
    }

    [Fact]
    public void End_WithoutBegin_Throws()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed);

        var action = () => query.End();

        action.Should().Throw<RenderException>();
    }

    [Fact]
    public void GetResult_ReturnsValue()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed);
        query.Begin();
        query.End();

        long result = query.GetResult();

        result.Should().Be(1); // Mock returns 1
    }

    [Fact]
    public void GetResultAvailable_ReturnsBool()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed);
        query.Begin();
        query.End();

        bool available = query.GetResultAvailable();

        available.Should().BeTrue(); // Mock returns non-zero
    }

    [Fact]
    public void ContextLost_SetsIdToZero()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed);
        query.Id.Should().BeGreaterThan(0u);

        _context.NotifyContextLost();

        query.Id.Should().Be(0u);
        query.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ContextRestore_GetsNewId()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed, "test");
        uint originalId = query.Id;

        _context.NotifyContextLost();
        query.Id.Should().Be(0u);

        _context.NotifyContextRestored();

        query.Id.Should().BeGreaterThan(0u);
        query.Id.Should().NotBe(originalId);
    }

    [Fact]
    public void Begin_AfterDispose_Throws()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed);
        query.Dispose();

        var action = () => query.Begin();

        action.Should().Throw<RenderException>();
    }

    [Fact]
    public void End_AfterDispose_Throws()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed);
        query.Dispose();

        var action = () => query.End();

        action.Should().Throw<RenderException>();
    }

    [Fact]
    public void GetResult_AfterDispose_Throws()
    {
        var query = new GLQuery(_context, QueryTarget.SamplesPassed);
        query.Dispose();

        var action = () => query.GetResult();

        action.Should().Throw<RenderException>();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
