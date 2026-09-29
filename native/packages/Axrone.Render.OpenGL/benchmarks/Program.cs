namespace Axrone.Render.OpenGL.Benchmarks;

/// <summary>
/// Benchmark entry point.
/// </summary>
public static class Program
{
    /// <summary>
    /// Main entry point for benchmarks.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
