using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Shading;

namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Tests for real program reflection via GetActiveUniform and GetActiveAttrib.
/// </summary>
public sealed class ReflectionTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public ReflectionTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void ReflectUniforms_ReturnsCannedUniforms_WithDeterministicLocations()
    {
        var program = new GLProgram(_context, "vertex", "fragment");
        _mock.SetActiveUniforms(program.Id, [
            ("u_color", 1, GLConst.Float),
            ("u_model", 1, GLConst.Float),
        ]);
        _mock.SetActiveAttribs(program.Id, [
            ("a_position", 1, GLConst.Float),
        ]);

        try
        {
            program.UniformLocations.Should().ContainKeys("u_color", "u_model");
            program.UniformLocations["u_color"].Should().Be(_mock.GetUniformLocation(program.Id, "u_color"));
            program.UniformLocations["u_model"].Should().Be(_mock.GetUniformLocation(program.Id, "u_model"));
            program.GetUniformLocation("u_color").Should().Be(program.UniformLocations["u_color"]);
        }
        finally
        {
            program.Dispose();
        }
    }

    [Fact]
    public void ReflectAttributes_ReturnsCannedAttrib_WithDeterministicLocation()
    {
        var program = new GLProgram(_context, "vertex", "fragment");
        _mock.SetActiveUniforms(program.Id, [
            ("u_color", 1, GLConst.Float),
            ("u_model", 1, GLConst.Float),
        ]);
        _mock.SetActiveAttribs(program.Id, [
            ("a_position", 1, GLConst.Float),
        ]);

        try
        {
            program.AttributeLocations.Should().ContainKey("a_position");
            program.AttributeLocations["a_position"].Should().Be(_mock.GetAttribLocation(program.Id, "a_position"));
            program.GetAttribLocation("a_position").Should().Be(program.AttributeLocations["a_position"]);
        }
        finally
        {
            program.Dispose();
        }
    }

    [Fact]
    public void UnknownNames_ReturnMinusOne()
    {
        var program = new GLProgram(_context, "vertex", "fragment");
        _mock.SetActiveUniforms(program.Id, [
            ("u_color", 1, GLConst.Float),
            ("u_model", 1, GLConst.Float),
        ]);
        _mock.SetActiveAttribs(program.Id, [
            ("a_position", 1, GLConst.Float),
        ]);

        try
        {
            program.GetUniformLocation("u_unknown").Should().Be(-1);
            program.GetAttribLocation("a_unknown").Should().Be(-1);
        }
        finally
        {
            program.Dispose();
        }
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
