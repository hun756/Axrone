using Axrone.Render.Core.Abstractions;
using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.FrameGraph.Passes;
using Graph = Axrone.Render.OpenGL.FrameGraph.FrameGraph<
    Axrone.Render.OpenGL.FrameGraph.BuildingPhase,
    Axrone.Render.OpenGL.FrameGraph.DefaultGraphPolicy>;

namespace Axrone.Render.OpenGL.Benchmarks;

/// <summary>
/// Benchmarks FrameGraph compile and execute overhead.
/// Measures the cost of building, validating, topologically sorting, and executing
/// render pass graphs of varying sizes.
/// </summary>
[MemoryDiagnoser]
public class FrameGraphBenchmarks
{
    private MockGLApi _mock = null!;
    private GLContext _context = null!;

    // Typestate handle in the building phase. The benchmark owns the graph, so it
    // disposes the building handle; a compiled handle obtained from it is a view over
    // the same storage and does not own the lifetime.
    private Graph _graph;

    [GlobalSetup]
    public void Setup()
    {
        _mock = new MockGLApi();
        _mock.EnableCallLogging = false;
        _context = new GLContext(_mock);
    }

    [IterationCleanup]
    public void Cleanup()
    {
        _graph.Dispose();
        _graph = default;
    }

    [GlobalCleanup]
    public void Teardown() => _context.Dispose();

    /// <summary>
    /// Creates and compiles an empty frame graph (no passes).
    /// Measures baseline compile overhead with zero work.
    /// </summary>
    [Benchmark]
    public void Compile_EmptyGraph()
    {
        _graph = new Graph(_context);
        _graph.Compile();
    }

    /// <summary>
    /// Creates and compiles a frame graph with 10 independent passes.
    /// Measures dependency resolution and topological sort overhead.
    /// </summary>
    [Benchmark]
    public void Compile_10Passes()
    {
        _graph = new Graph(_context);

        for (int i = 0; i < 10; i++)
        {
            _graph.AddPass(NoOpPass($"Pass_{i}", FramePassKind.Opaque));
        }

        _graph.Compile();
    }

    /// <summary>
    /// Compiles and executes a frame graph with 10 passes.
    /// Measures the full compile + execute pipeline overhead.
    /// </summary>
    [Benchmark]
    public void Execute_10Passes()
    {
        _graph = new Graph(_context);

        for (int i = 0; i < 10; i++)
        {
            _graph.AddPass(NoOpPass($"Pass_{i}", FramePassKind.Opaque));
        }

        _graph.Compile().Execute();
    }

    /// <summary>
    /// Compiles and executes a frame graph with 50 passes.
    /// Measures scaling behavior with a larger pass count.
    /// </summary>
    [Benchmark]
    public void Execute_50Passes()
    {
        _graph = new Graph(_context);

        for (int i = 0; i < 50; i++)
        {
            _graph.AddPass(NoOpPass($"Pass_{i}", FramePassKind.Opaque));
        }

        _graph.Compile().Execute();
    }

    /// <summary>
    /// Minimal no-op render pass for benchmarking frame graph overhead.
    /// </summary>
    private static RenderPass<CustomPassData> NoOpPass(string name, FramePassKind kind) =>
        CustomPass.Create(name, kind, static (_, _) => { });
}
