using Axrone.Render.OpenGL.Context;
using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.Native;

namespace Axrone.Render.OpenGL.Benchmarks;

/// <summary>
/// Benchmarks framebuffer (blit) command dispatch: a recorded blit replayed through
/// <see cref="BlitDispatcher"/> against the direct state-cache-plus-GL call sequence
/// it replaces. Both legs issue the identical three-primitive sequence, so the point
/// is not to beat the baseline but to show the abstraction is free — the command and
/// the dispatcher must fold away entirely, leaving the same state-cache work and the
/// same <c>glBlitFramebuffer</c> call. The counting-invoker leg measures the floor:
/// pure replay with no GL and no state cache at all.
/// </summary>
/// <remarks>
/// The mock still formats one log string per call even with logging disabled, so both
/// GL legs allocate identically and the ratio stays valid; the absolute allocation
/// figure is an artifact of the mock, not of the dispatch path.
/// </remarks>
[MemoryDiagnoser]
public class BlitDispatchBenchmarks
{
    private const uint SourceFramebuffer = 7u;
    private const uint DestinationFramebuffer = 9u;
    private const int SourceX0 = 0;
    private const int SourceY0 = 0;
    private const int SourceX1 = 16;
    private const int SourceY1 = 8;
    private const int DestinationX0 = 0;
    private const int DestinationY0 = 0;
    private const int DestinationX1 = 16;
    private const int DestinationY1 = 8;
    private const uint Mask = GLConst.ColorBufferBit;
    private const uint Filter = GLConst.Nearest;

    private MockGLApi _mock = null!;
    private GLContext _context = null!;
    private GLFramebufferInvoker _invoker;
    private CountingBlitInvoker _counting;
    private BlitCommand<GLFramebufferInvoker> _command;
    private BlitCommand<CountingBlitInvoker> _countingCommand;

    [GlobalSetup]
    public void Setup()
    {
        _mock = new MockGLApi();
        _mock.EnableCallLogging = false; // string logging would dominate a three-call sequence
        _context = new GLContext(_mock);
        _invoker = new GLFramebufferInvoker(_context);
        _command = new BlitCommand<GLFramebufferInvoker>(
            SourceFramebuffer, DestinationFramebuffer,
            SourceX0, SourceY0, SourceX1, SourceY1,
            DestinationX0, DestinationY0, DestinationX1, DestinationY1,
            Mask, Filter);
        _countingCommand = new BlitCommand<CountingBlitInvoker>(
            SourceFramebuffer, DestinationFramebuffer,
            SourceX0, SourceY0, SourceX1, SourceY1,
            DestinationX0, DestinationY0, DestinationX1, DestinationY1,
            Mask, Filter);
    }

    [GlobalCleanup]
    public void Teardown() => _context.Dispose();

    /// <summary>
    /// The leg the command vocabulary replaces: bind the read and draw framebuffers
    /// through the state cache, then issue the blit directly.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void Dispatch_Baseline_DirectStateCacheAndGl()
    {
        _context.State.Invalidate();
        _context.State.BindFramebuffer(GLConst.ReadFramebuffer, SourceFramebuffer);
        _context.State.BindFramebuffer(GLConst.DrawFramebuffer, DestinationFramebuffer);
        _context.GL.BlitFramebuffer(
            SourceX0, SourceY0, SourceX1, SourceY1,
            DestinationX0, DestinationY0, DestinationX1, DestinationY1,
            Mask, Filter);
    }

    /// <summary>
    /// The same sequence replayed from a recorded command: one direct, monomorphized
    /// call into the command, whose payload is inlined into the caller. Expected to
    /// land at parity with the baseline — the abstraction must cost nothing.
    /// </summary>
    [Benchmark]
    public void Dispatch_Monomorphic_ProductionInvoker()
    {
        _context.State.Invalidate();
        BlitDispatcher.Dispatch(ref _invoker, ref _command);
    }

    /// <summary>
    /// The floor: replay against a counting invoker, so the measurement is pure
    /// dispatch with no state cache and no GL. Anything above the direct primitive
    /// calls would show up here.
    /// </summary>
    [Benchmark]
    public void Dispatch_Monomorphic_CountingInvoker()
    {
        BlitDispatcher.Dispatch(ref _counting, ref _countingCommand);
    }

    /// <summary>
    /// The pass-level entry cost: recording a blit into a value command and replaying
    /// it, as a blit leg does when it is built from pass state.
    /// </summary>
    [Benchmark]
    public void Dispatch_Monomorphic_CountingInvoker_WithPayloadSetup()
    {
        var command = new BlitCommand<CountingBlitInvoker>(
            SourceFramebuffer, DestinationFramebuffer,
            SourceX0, SourceY0, SourceX1, SourceY1,
            DestinationX0, DestinationY0, DestinationX1, DestinationY1,
            Mask, Filter);
        BlitDispatcher.Dispatch(ref _counting, ref command);
    }

    /// <summary>
    /// Minimal invoker: static abstracts cannot assign to the readonly <c>in</c>
    /// context, so the work lands in a static sink — no allocation, no GL, no
    /// interface call of its own.
    /// </summary>
    private readonly struct CountingBlitInvoker : IGLFramebufferInvoker<CountingBlitInvoker>
    {
        public static ulong Sink;

        public static void BindFramebuffer(in CountingBlitInvoker ctx, uint target, uint framebuffer) =>
            Sink += target + framebuffer;

        public static void BlitFramebuffer(
            in CountingBlitInvoker ctx,
            int srcX0, int srcY0, int srcX1, int srcY1,
            int dstX0, int dstY0, int dstX1, int dstY1,
            uint mask, uint filter) =>
            Sink += (uint)(srcX0 + srcY0 + srcX1 + srcY1 + dstX0 + dstY0 + dstX1 + dstY1) + mask + filter;
    }

    /// <summary>
    /// Value command mirroring the production <c>BlitFramebufferCommand</c> shape:
    /// payload plus one replay method, no heap. The benchmarks project cannot see the
    /// production command's internals, so the shape is reproduced rather than reused.
    /// </summary>
    private readonly struct BlitCommand<TInvoker> : IFramebufferCommand<TInvoker>
        where TInvoker : struct, IGLFramebufferInvoker<TInvoker>
    {
        private readonly uint _sourceFramebuffer;
        private readonly uint _destinationFramebuffer;
        private readonly int _sourceX0;
        private readonly int _sourceY0;
        private readonly int _sourceX1;
        private readonly int _sourceY1;
        private readonly int _destinationX0;
        private readonly int _destinationY0;
        private readonly int _destinationX1;
        private readonly int _destinationY1;
        private readonly uint _mask;
        private readonly uint _filter;

        public BlitCommand(
            uint sourceFramebuffer,
            uint destinationFramebuffer,
            int sourceX0, int sourceY0, int sourceX1, int sourceY1,
            int destinationX0, int destinationY0, int destinationX1, int destinationY1,
            uint mask, uint filter)
        {
            _sourceFramebuffer = sourceFramebuffer;
            _destinationFramebuffer = destinationFramebuffer;
            _sourceX0 = sourceX0;
            _sourceY0 = sourceY0;
            _sourceX1 = sourceX1;
            _sourceY1 = sourceY1;
            _destinationX0 = destinationX0;
            _destinationY0 = destinationY0;
            _destinationX1 = destinationX1;
            _destinationY1 = destinationY1;
            _mask = mask;
            _filter = filter;
        }

        public void Execute(ref TInvoker invoker)
        {
            // Read binding first, then the draw binding, then the copy: both targets
            // must be current before the blit reads one and writes the other.
            TInvoker.BindFramebuffer(in invoker, GLConst.ReadFramebuffer, _sourceFramebuffer);
            TInvoker.BindFramebuffer(in invoker, GLConst.DrawFramebuffer, _destinationFramebuffer);
            TInvoker.BlitFramebuffer(
                in invoker,
                _sourceX0, _sourceY0, _sourceX1, _sourceY1,
                _destinationX0, _destinationY0, _destinationX1, _destinationY1,
                _mask, _filter);
        }
    }
}
