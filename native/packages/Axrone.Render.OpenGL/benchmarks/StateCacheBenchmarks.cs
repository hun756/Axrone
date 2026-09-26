using Axrone.Render.OpenGL.Context;

namespace Axrone.Render.OpenGL.Benchmarks;

/// <summary>
/// Benchmarks GLStateCache deduplication efficiency.
/// Measures how effectively the state cache eliminates redundant GL calls
/// under various access patterns: first bind, repeated same value, and alternating values.
/// </summary>
[MemoryDiagnoser]
public class StateCacheBenchmarks
{
    private MockGLApi _mock = null!;
    private GLStateCache _cache = null!;

    [GlobalSetup]
    public void Setup()
    {
        _mock = new MockGLApi();
        _mock.EnableCallLogging = false; // Reduce overhead from string logging
        _cache = new GLStateCache(_mock);
    }

    /// <summary>
    /// Baseline: invalidate cache then bind a buffer — forces a real GL call.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void BindArrayBuffer_FirstBind()
    {
        _cache.Invalidate();
        _cache.BindArrayBuffer(42);
    }

    /// <summary>
    /// Bind the same buffer 100 times — all but the first should be zero-cost cache hits.
    /// </summary>
    [Benchmark]
    public void BindArrayBuffer_SameValue()
    {
        _cache.Invalidate();
        _cache.BindArrayBuffer(42); // First call goes through

        for (int i = 0; i < 100; i++)
        {
            _cache.BindArrayBuffer(42); // Should all be cache hits
        }
    }

    /// <summary>
    /// Alternate between two buffer IDs 100 times — every call is a cache miss.
    /// </summary>
    [Benchmark]
    public void BindArrayBuffer_Alternating()
    {
        _cache.Invalidate();

        for (int i = 0; i < 100; i++)
        {
            _cache.BindArrayBuffer((uint)(i % 2 == 0 ? 1 : 2));
        }
    }

    /// <summary>
    /// Toggle depth test on/off 100 times — every call changes state.
    /// </summary>
    [Benchmark]
    public void SetDepthTest_Toggle()
    {
        _cache.Invalidate();

        for (int i = 0; i < 100; i++)
        {
            _cache.SetDepthTest(i % 2 == 0);
        }
    }

    /// <summary>
    /// Set blend enabled then keep the same value 100 times — only first should issue GL call.
    /// </summary>
    [Benchmark]
    public void SetBlend_Constant()
    {
        _cache.Invalidate();
        _cache.SetBlend(true); // First call goes through

        for (int i = 0; i < 100; i++)
        {
            _cache.SetBlend(true); // Should all be cache hits
        }
    }

    /// <summary>
    /// Simulates a typical frame state setup: viewport, depth, blend, cull, program, VAO, textures.
    /// Tests the overhead of a realistic per-frame state configuration.
    /// </summary>
    [Benchmark]
    public void FullStateSetup()
    {
        _cache.Invalidate();

        // Viewport
        _cache.SetViewport(0, 0, 1920, 1080);

        // Depth state
        _cache.SetDepthTest(true);
        _cache.SetDepthFunc(0x0203); // GL_LESS
        _cache.SetDepthMask(true);

        // Blend state
        _cache.SetBlend(true);
        _cache.SetBlendFuncSeparate(0x0302, 0x0303, 0x0302, 0x0303); // SRC_ALPHA, ONE_MINUS_SRC_ALPHA

        // Cull state
        _cache.SetCullFace(true);
        _cache.SetCullMode(0x0405); // GL_BACK
        _cache.SetFrontFace(0x0901); // GL_CCW

        // Program
        _cache.UseProgram(1);

        // VAO
        _cache.BindVertexArray(1);

        // Textures on 4 units
        _cache.BindTexture2D(0, 10);
        _cache.BindTexture2D(1, 11);
        _cache.BindTexture2D(2, 12);
        _cache.BindTexture2D(3, 13);
    }
}
