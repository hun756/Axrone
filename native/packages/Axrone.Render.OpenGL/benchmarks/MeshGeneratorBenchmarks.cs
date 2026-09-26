using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.Mesh;

namespace Axrone.Render.OpenGL.Benchmarks;

/// <summary>
/// Benchmarks mesh generation throughput for common primitive types.
/// Measures the CPU cost of generating vertex and index data for planes,
/// cubes, spheres at various tessellation levels, and fullscreen quads.
/// </summary>
[MemoryDiagnoser]
public class MeshGeneratorBenchmarks
{
    private GLContext _context = null!;

    [GlobalSetup]
    public void Setup()
    {
        var mock = new MockGLApi();
        mock.EnableCallLogging = false;
        _context = new GLContext(mock);
    }

    [GlobalCleanup]
    public void Cleanup() => _context.Dispose();

    /// <summary>
    /// Generates a 10x10 plane mesh (4 vertices, 6 indices).
    /// </summary>
    [Benchmark(Baseline = true)]
    public GLMesh CreatePlane()
    {
        return MeshGenerators.CreatePlane(_context, 10.0f, 10.0f);
    }

    /// <summary>
    /// Generates a unit cube mesh (24 vertices, 36 indices).
    /// </summary>
    [Benchmark]
    public GLMesh CreateCube()
    {
        return MeshGenerators.CreateCube(_context, 1.0f);
    }

    /// <summary>
    /// Generates a low-poly sphere (8 segments, 4 rings = ~45 vertices).
    /// </summary>
    [Benchmark]
    public GLMesh CreateSphere_LowPoly()
    {
        return MeshGenerators.CreateSphere(_context, 1.0f, segments: 8, rings: 4);
    }

    /// <summary>
    /// Generates a high-poly sphere (64 segments, 32 rings = ~2145 vertices).
    /// </summary>
    [Benchmark]
    public GLMesh CreateSphere_HighPoly()
    {
        return MeshGenerators.CreateSphere(_context, 1.0f, segments: 64, rings: 32);
    }

    /// <summary>
    /// Generates a fullscreen quad (3 vertices, no index buffer).
    /// </summary>
    [Benchmark]
    public GLMesh CreateFullscreenQuad()
    {
        return MeshGenerators.CreateFullscreenQuad(_context);
    }
}
