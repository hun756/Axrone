using Axrone.Render.OpenGL.FrameGraph;

namespace Axrone.Render.OpenGL.Benchmarks;

/// <summary>
/// Benchmarks fullscreen command dispatch: monomorphic (static-abstract, value
/// command) against virtual (interface-typed class command). Both replays run the
/// identical five-primitive sequence against the same invoker; the invoker is a
/// counter rather than a GL context so the measured delta is pure dispatch overhead
/// — indirect call plus lost inlining versus a direct call the JIT can fold into the
/// caller. GL-backed sequencing is covered by the unit tests, not here.
/// </summary>
[MemoryDiagnoser]
public class FullscreenDispatchBenchmarks
{
    private CounterInvoker _invoker;
    private MonoCommand _mono;
    private VirtualCommand _virtual = null!;

    // Interface-typed on purpose: the field type is the benchmark. A concrete type
    // here would let the JIT devirtualize and measure nothing.
#pragma warning disable CA1859 // Use concrete type for better performance - the indirect call is the subject
    private IFullscreenDrawCommand<CounterInvoker> _virtualDispatch = null!;
#pragma warning restore CA1859

    [GlobalSetup]
    public void Setup()
    {
        _mono = new MonoCommand(1u, 10u, 2, 3f);
        _virtual = new VirtualCommand(1u, 10u, 2, 3f);
        _virtualDispatch = _virtual;
    }

    /// <summary>
    /// Monomorphic replay: one direct call, monomorphized for the closed
    /// (CounterInvoker, MonoCommand) pair, with the payload inlined into the caller.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Dispatch_Monomorphic()
    {
        FullscreenDispatcher.Dispatch(ref _invoker, ref _mono);
    }

    /// <summary>
    /// Virtual replay of the same five primitives through an interface-typed command.
    /// Measures the indirect call and the inlining the JIT cannot perform.
    /// </summary>
    [Benchmark]
    public void Dispatch_Virtual()
    {
        _virtualDispatch.Execute(ref _invoker);
    }

    /// <summary>
    /// The pass-level entry cost: resolving a pass payload into a value command and
    /// replaying it, as <c>FullscreenQuadPassExecutor.ExecuteMonomorphic</c> does.
    /// </summary>
    [Benchmark]
    public void Dispatch_Monomorphic_WithPayloadSetup()
    {
        var command = new MonoCommand(1u, 10u, 2, 3f);
        FullscreenDispatcher.Dispatch(ref _invoker, ref command);
    }

    /// <summary>
    /// Minimal invoker: static abstracts cannot assign to the readonly <c>in</c>
    /// context, so the work lands in a static sink — no allocation, no GL, no
    /// interface call of its own.
    /// </summary>
    private readonly struct CounterInvoker : IGLFullscreenInvoker<CounterInvoker>
    {
        public static ulong Sink;

        public static void UseProgram(in CounterInvoker ctx, uint program) => Sink += program;

        public static void BindTextureUnit(in CounterInvoker ctx, uint unit, uint texture) => Sink += unit + texture;

        public static void SetBlendState(in CounterInvoker ctx, bool enable, uint srcFactor, uint dstFactor) =>
            Sink += (enable ? srcFactor : dstFactor);

        public static void SetUniform1f(in CounterInvoker ctx, int location, float v) => Sink += (ulong)(location + (int)v);

        public static void DrawFullscreenTriangle(in CounterInvoker ctx) => Sink += 3;
    }

    /// <summary>Value command: the production shape — payload plus one replay method.</summary>
    private struct MonoCommand : IFullscreenDrawCommand<CounterInvoker>
    {
        private readonly uint _program;
        private readonly uint _texture;
        private readonly int _location;
        private readonly float _value;

        public MonoCommand(uint program, uint texture, int location, float value)
        {
            _program = program;
            _texture = texture;
            _location = location;
            _value = value;
        }

        public void Execute(ref CounterInvoker invoker)
        {
            CounterInvoker.UseProgram(in invoker, _program);
            CounterInvoker.BindTextureUnit(in invoker, 0u, _texture);
            CounterInvoker.SetUniform1f(in invoker, _location, _value);
            CounterInvoker.SetBlendState(in invoker, false, 1u, 0u);
            CounterInvoker.DrawFullscreenTriangle(in invoker);
        }
    }

    /// <summary>Class command: the vtable shape the monomorphic path replaces.</summary>
    private sealed class VirtualCommand : IFullscreenDrawCommand<CounterInvoker>
    {
        private readonly uint _program;
        private readonly uint _texture;
        private readonly int _location;
        private readonly float _value;

        public VirtualCommand(uint program, uint texture, int location, float value)
        {
            _program = program;
            _texture = texture;
            _location = location;
            _value = value;
        }

        public void Execute(ref CounterInvoker invoker)
        {
            CounterInvoker.UseProgram(in invoker, _program);
            CounterInvoker.BindTextureUnit(in invoker, 0u, _texture);
            CounterInvoker.SetUniform1f(in invoker, _location, _value);
            CounterInvoker.SetBlendState(in invoker, false, 1u, 0u);
            CounterInvoker.DrawFullscreenTriangle(in invoker);
        }
    }
}
