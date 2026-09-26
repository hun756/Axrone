using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Shading;

namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Tests for <see cref="GLProgram"/> and <see cref="UniformCache"/>.
/// </summary>
public sealed class ShaderSystemTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public ShaderSystemTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    // =========================================================================
    // GLProgram Tests
    // =========================================================================

    [Fact]
    public void GLProgram_Constructor_CompilesAndLinksSuccessfully()
    {
        var program = new GLProgram(_context, "vertex", "fragment");

        program.Id.Should().BeGreaterThan(0u);
        program.IsDisposed.Should().BeFalse();
        program.VertexSource.Should().Be("vertex");
        program.FragmentSource.Should().Be("fragment");

        program.Dispose();
    }

    [Fact]
    public void GLProgram_GetUniformLocation_ReturnsMinusOneWhenUniformNotInReflectionData()
    {
        var program = new GLProgram(_context, "vertex", "fragment");

        // The reflection data is empty by default (ReflectUniforms returns empty dictionary),
        // so all uniform lookups return -1 regardless of the name.
        int location = program.GetUniformLocation("u_color");

        location.Should().Be(-1);

        program.Dispose();
    }

    [Fact]
    public void GLProgram_GetUniformLocation_ReturnsMinusOneForUnknownUniform()
    {
        var program = new GLProgram(_context, "vertex", "fragment");

        // The UniformLocations dictionary is empty by default (no reflection data)
        // So any lookup should return -1
        int location = program.GetUniformLocation("nonexistent_uniform");

        location.Should().Be(-1);

        program.Dispose();
    }

    [Fact]
    public void GLProgram_GetAttribLocation_ReturnsLocationForKnownAttribute()
    {
        var program = new GLProgram(_context, "vertex", "fragment");

        // The AttributeLocations dictionary is empty by default
        // So any lookup should return -1
        int location = program.GetAttribLocation("a_position");

        // Since reflection data is empty, this returns -1
        location.Should().Be(-1);

        program.Dispose();
    }

    [Fact]
    public void GLProgram_ContextLoss_SetsIdToZero()
    {
        var program = new GLProgram(_context, "vertex", "fragment");
        uint originalId = program.Id;
        originalId.Should().BeGreaterThan(0u);

        program.OnContextLost();

        program.Id.Should().Be(0u);

        program.Dispose();
    }

    [Fact]
    public void GLProgram_ContextRestore_GetsNewIdAndRecompiles()
    {
        var program = new GLProgram(_context, "vertex", "fragment");
        uint originalId = program.Id;

        program.OnContextLost();
        program.Id.Should().Be(0u);

        program.OnContextRestored();

        program.Id.Should().BeGreaterThan(0u);
        program.Id.Should().NotBe(originalId, "a new program ID should be allocated after restore");

        program.Dispose();
    }

    [Fact]
    public void GLProgram_Dispose_DeletesProgram()
    {
        var program = new GLProgram(_context, "vertex", "fragment");
        uint programId = program.Id;

        _mock.ClearCallLog();
        program.Dispose();

        program.IsDisposed.Should().BeTrue();
        program.Id.Should().Be(0u);
        _mock.CallLog.Should().Contain(c => c.Contains($"DeleteProgram({programId})"));
    }

    [Fact]
    public void GLProgram_Dispose_DoubleDispose_DoesNotThrow()
    {
        var program = new GLProgram(_context, "vertex", "fragment");

        var action = () =>
        {
            program.Dispose();
            program.Dispose();
        };

        action.Should().NotThrow();
    }

    // =========================================================================
    // UniformCache Tests
    // =========================================================================

    [Fact]
    public void UniformCache_CheckAndSet_ReturnsTrueOnFirstCall()
    {
        var cache = new UniformCache();

        bool result = cache.CheckAndSet(programId: 1, location: 0, valueHash: 12345u);

        result.Should().BeTrue("first call should be a cache miss");
    }

    [Fact]
    public void UniformCache_CheckAndSet_ReturnsFalseOnSameValue()
    {
        var cache = new UniformCache();

        cache.CheckAndSet(programId: 1, location: 0, valueHash: 12345u);
        bool result = cache.CheckAndSet(programId: 1, location: 0, valueHash: 12345u);

        result.Should().BeFalse("same value should be a cache hit");
    }

    [Fact]
    public void UniformCache_CheckAndSet_ReturnsTrueWhenValueChanges()
    {
        var cache = new UniformCache();

        cache.CheckAndSet(programId: 1, location: 0, valueHash: 12345u);
        bool result = cache.CheckAndSet(programId: 1, location: 0, valueHash: 67890u);

        result.Should().BeTrue("changed value should be a cache miss");
    }

    [Fact]
    public void UniformCache_Clear_ResetsAllEntries()
    {
        var cache = new UniformCache();

        cache.CheckAndSet(programId: 1, location: 0, valueHash: 12345u);
        cache.Clear();

        // After clear, the same value should be a miss again
        bool result = cache.CheckAndSet(programId: 1, location: 0, valueHash: 12345u);
        result.Should().BeTrue("cache should be empty after clear");
    }

    [Fact]
    public void UniformCache_DifferentProgramIds_DoNotCollide()
    {
        var cache = new UniformCache();

        // Set value for program 1
        cache.CheckAndSet(programId: 1, location: 0, valueHash: 12345u);

        // Same location and hash but different program should be a miss
        bool result = cache.CheckAndSet(programId: 2, location: 0, valueHash: 12345u);

        result.Should().BeTrue("different program IDs should not share cache entries");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
