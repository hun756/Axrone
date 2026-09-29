using Axrone.Render.OpenGL.FrameGraph;
using Axrone.Render.OpenGL.Native;

namespace Axrone.Render.OpenGL.Tests;

/// <summary>
/// Covers the monomorphic (static-abstract, zero-vtable) framebuffer command path:
/// the exact replay order a blit must follow, the value equality that makes replay
/// deterministic, and the null guard on the production invoker.
/// </summary>
public sealed class BlitCommandTests
{
    private const uint SourceFramebuffer = 7u;
    private const uint DestinationFramebuffer = 9u;

    // A deliberately non-trivial rectangle: negative source and destination offsets
    // are legal for a blit and must survive the round trip through the payload.
    private const int SourceX0 = -4;
    private const int SourceY0 = 2;
    private const int SourceX1 = 12;
    private const int SourceY1 = 10;
    private const int DestinationX0 = 0;
    private const int DestinationY0 = 0;
    private const int DestinationX1 = 16;
    private const int DestinationY1 = 8;
    private const uint Mask = GLConst.ColorBufferBit;
    private const uint Filter = GLConst.Linear;

    [Fact]
    public void BlitCommand_Execute_RecordsExactSequence()
    {
        List<string> calls = [];
        var invoker = new RecordingBlitInvoker(calls);
        var command = new BlitFramebufferCommand<RecordingBlitInvoker>(
            SourceFramebuffer, DestinationFramebuffer,
            SourceX0, SourceY0, SourceX1, SourceY1,
            DestinationX0, DestinationY0, DestinationX1, DestinationY1,
            Mask, Filter);

        BlitDispatcher.Dispatch(ref invoker, ref command);

        calls.Should().Equal(
        [
            $"BindFramebuffer({GLConst.ReadFramebuffer}, {SourceFramebuffer})",
            $"BindFramebuffer({GLConst.DrawFramebuffer}, {DestinationFramebuffer})",
            $"BlitFramebuffer({SourceX0}, {SourceY0}, {SourceX1}, {SourceY1}, " +
            $"{DestinationX0}, {DestinationY0}, {DestinationX1}, {DestinationY1}, {Mask}, {Filter})",
        ], "a blit reads the read framebuffer and writes the draw framebuffer, so both " +
           "bindings must be current, in that order, before the copy is issued");
    }

    [Fact]
    public void BlitCommand_ValueEquality()
    {
        var first = new BlitFramebufferCommand<RecordingBlitInvoker>(
            SourceFramebuffer, DestinationFramebuffer,
            SourceX0, SourceY0, SourceX1, SourceY1,
            DestinationX0, DestinationY0, DestinationX1, DestinationY1,
            Mask, Filter);
        var second = new BlitFramebufferCommand<RecordingBlitInvoker>(
            SourceFramebuffer, DestinationFramebuffer,
            SourceX0, SourceY0, SourceX1, SourceY1,
            DestinationX0, DestinationY0, DestinationX1, DestinationY1,
            Mask, Filter);

        // The parity-test hook: identical payload means identical replay, so a
        // recorded frame can be diffed against a live one by value alone.
        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());

        var mutated = first with { SourceX0 = SourceX0 + 1 };

        mutated.Should().NotBe(first, "a single changed field must change the command identity");
    }

    [Fact]
    public void GLFramebufferInvoker_RequiresContext()
    {
        Action construct = () => _ = new GLFramebufferInvoker(null!);

        construct.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Hand-written invoker that records every call as a single formatted entry. The
    /// call list lives in a pre-allocated list owned by the test, never by a dispatch,
    /// because an <c>in</c> context is a readonly reference: a static abstract member
    /// may mutate what the invoker points at, but may not assign to the invoker's own
    /// fields.
    /// </summary>
    private readonly struct RecordingBlitInvoker : IGLFramebufferInvoker<RecordingBlitInvoker>
    {
        private readonly List<string> _calls;

        public RecordingBlitInvoker(List<string> calls)
        {
            _calls = calls;
        }

        public static void BindFramebuffer(in RecordingBlitInvoker ctx, uint target, uint framebuffer) =>
            ctx._calls.Add($"BindFramebuffer({target}, {framebuffer})");

        public static void BlitFramebuffer(
            in RecordingBlitInvoker ctx,
            int srcX0, int srcY0, int srcX1, int srcY1,
            int dstX0, int dstY0, int dstX1, int dstY1,
            uint mask, uint filter) =>
            ctx._calls.Add($"BlitFramebuffer({srcX0}, {srcY0}, {srcX1}, {srcY1}, " +
                           $"{dstX0}, {dstY0}, {dstX1}, {dstY1}, {mask}, {filter})");
    }
}
