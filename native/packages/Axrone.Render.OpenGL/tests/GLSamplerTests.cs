namespace Axrone.Render.OpenGL.Tests;

using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Native;
using Axrone.Render.OpenGL.Resources;

/// <summary>
/// Tests for <see cref="GLSampler"/> operations including creation, filter/wrap/LOD/compare
/// configuration, bind/unbind, disposal, and context-loss recovery.
/// </summary>
public sealed class GLSamplerTests : IDisposable
{
    private readonly MockGLApi _mock;
    private readonly GLContext _context;

    public GLSamplerTests()
    {
        _mock = new MockGLApi();
        _context = new GLContext(_mock);
    }

    [Fact]
    public void Constructor_CreatesSampler_AndRegistersWithContext()
    {
        var sampler = new GLSampler(_context, "test_sampler");

        sampler.Id.Should().BeGreaterThan(0u);
        sampler.Label.Should().Be("test_sampler");
        sampler.IsDisposed.Should().BeFalse();
        _context.Registry.Count.Should().Be(1);
    }

    [Fact]
    public void SetFilter_StoresValues()
    {
        var sampler = new GLSampler(_context);
        _mock.ClearCallLog();

        sampler.SetFilter(GLConst.Nearest, GLConst.Nearest);

        // No immediate GL call; Apply sends to GL
        sampler.SetFilter(GLConst.Linear, GLConst.Linear);
        var action = () => sampler.Apply();
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_SendsFilterParametersToGL()
    {
        var sampler = new GLSampler(_context);
        _mock.ClearCallLog();

        sampler.SetFilter(GLConst.Nearest, GLConst.Nearest);
        sampler.Apply();

        _mock.CallLog.Should().Contain(c => c.Contains("SamplerParameter") && c.Contains(GLConst.TextureMinFilter.ToString()));
        _mock.CallLog.Should().Contain(c => c.Contains("SamplerParameter") && c.Contains(GLConst.TextureMagFilter.ToString()));
    }

    [Fact]
    public void SetWrap_StoresWrapModes()
    {
        var sampler = new GLSampler(_context);
        _mock.ClearCallLog();

        sampler.SetWrap(GLConst.ClampToEdge, GLConst.MirroredRepeat, GLConst.Repeat);
        sampler.Apply();

        _mock.CallLog.Should().Contain(c => c.Contains("SamplerParameter") && c.Contains(GLConst.TextureWrapS.ToString()));
        _mock.CallLog.Should().Contain(c => c.Contains("SamplerParameter") && c.Contains(GLConst.TextureWrapT.ToString()));
        _mock.CallLog.Should().Contain(c => c.Contains("SamplerParameter") && c.Contains(GLConst.TextureWrapR.ToString()));
    }

    [Fact]
    public void SetLod_StoresLodRange()
    {
        var sampler = new GLSampler(_context);
        _mock.ClearCallLog();

        sampler.SetLod(0.0f, 10.0f, 0.5f);
        sampler.Apply();

        _mock.CallLog.Should().Contain(c => c.Contains("SamplerParameter") && c.Contains(GLConst.TextureMinLod.ToString()));
        _mock.CallLog.Should().Contain(c => c.Contains("SamplerParameter") && c.Contains(GLConst.TextureMaxLod.ToString()));
        _mock.CallLog.Should().Contain(c => c.Contains("SamplerParameter") && c.Contains(GLConst.TextureLodBias.ToString()));
    }

    [Fact]
    public void SetCompare_EnablesComparisonMode()
    {
        var sampler = new GLSampler(_context);
        _mock.ClearCallLog();

        sampler.SetCompare(GLConst.CompareRefToTexture, GLConst.Lequal);
        sampler.Apply();

        _mock.CallLog.Should().Contain(c => c.Contains("SamplerParameter") && c.Contains(GLConst.TextureCompareMode.ToString()));
        _mock.CallLog.Should().Contain(c => c.Contains("SamplerParameter") && c.Contains(GLConst.TextureCompareFunc.ToString()));
    }

    [Fact]
    public void Bind_CallsBindSampler()
    {
        var sampler = new GLSampler(_context);
        _mock.ClearCallLog();

        sampler.Bind(0);

        _mock.CallLog.Should().Contain(c => c.Contains("BindSampler") && c.Contains('0') && c.Contains(sampler.Id.ToString()));
    }

    [Fact]
    public void Unbind_CallsBindSamplerWithZero()
    {
        var sampler = new GLSampler(_context);
        _mock.ClearCallLog();

        sampler.Unbind(0);

        _mock.CallLog.Should().Contain(c => c.Contains("BindSampler") && c.Contains('0'));
    }

    [Fact]
    public void ContextLost_SetsIdToZero()
    {
        var sampler = new GLSampler(_context);
        sampler.Id.Should().BeGreaterThan(0u);

        _context.NotifyContextLost();

        sampler.Id.Should().Be(0u);
    }

    [Fact]
    public void ContextRestore_GetsNewId_AndReappliesParameters()
    {
        var sampler = new GLSampler(_context, "test");
        uint originalId = sampler.Id;

        sampler.SetFilter(GLConst.Nearest, GLConst.Nearest);

        _context.NotifyContextLost();
        sampler.Id.Should().Be(0u);

        _context.NotifyContextRestored();

        sampler.Id.Should().BeGreaterThan(0u);
        sampler.Id.Should().NotBe(originalId);
    }

    [Fact]
    public void Bind_AfterDispose_Throws()
    {
        var sampler = new GLSampler(_context);
        sampler.Dispose();

        var action = () => sampler.Bind(0);

        action.Should().Throw<RenderException>();
    }

    [Fact]
    public void DoubleDispose_DoesNotThrow()
    {
        var sampler = new GLSampler(_context);

        var action = () =>
        {
            sampler.Dispose();
            sampler.Dispose();
        };

        action.Should().NotThrow();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
