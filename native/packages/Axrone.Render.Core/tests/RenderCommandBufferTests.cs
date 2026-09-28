namespace Axrone.Render.Core.Tests;

public sealed class RenderCommandBufferTests
{
    private sealed class MockRenderContext : IRenderContext
    {
        public readonly List<string> Log = new();

        public void BeginPass(in RenderPassDescriptor descriptor) =>
            Log.Add($"BeginPass({descriptor.FramebufferId})");

        public void EndPass() =>
            Log.Add("EndPass");

        public void SetViewport(in ViewportRect viewport) =>
            Log.Add($"SetViewport({viewport.Width}x{viewport.Height})");

        public void SetScissor(in ScissorRect scissor, bool enabled = true) =>
            Log.Add($"SetScissor({scissor.Width}x{scissor.Height}, {enabled})");

        public void SetBlendState(bool enabled, uint srcRGB = 0, uint dstRGB = 0, uint srcAlpha = 0, uint dstAlpha = 0) =>
            Log.Add($"SetBlendState({enabled})");

        public void SetBlendEquation(uint equationRGB, uint equationAlpha) =>
            Log.Add($"SetBlendEquation({equationRGB}, {equationAlpha})");

        public void SetDepthState(bool testEnabled, bool writeEnabled, uint depthFunc = 0) =>
            Log.Add($"SetDepthState({testEnabled}, {writeEnabled})");

        public void SetCullState(bool enabled, uint cullMode = 0, uint frontFace = 0) =>
            Log.Add($"SetCullState({enabled})");

        public void SetColorMask(bool red, bool green, bool blue, bool alpha) =>
            Log.Add($"SetColorMask({red}, {green}, {blue}, {alpha})");

        public void BindProgram(uint programId) =>
            Log.Add($"BindProgram({programId})");

        public void BindTexture(uint slot, uint textureId, uint target = 0) =>
            Log.Add($"BindTexture({slot}, {textureId})");

        public void BindSampler(uint slot, uint samplerId) =>
            Log.Add($"BindSampler({slot}, {samplerId})");

        public void BindVertexArray(uint vaoId) =>
            Log.Add($"BindVertexArray({vaoId})");

        public void SetUniform(int location, float value) =>
            Log.Add($"SetUniform1f({location}, {value})");

        public void SetUniform(int location, float x, float y) =>
            Log.Add($"SetUniform2f({location}, {x}, {y})");

        public void SetUniform(int location, float x, float y, float z) =>
            Log.Add($"SetUniform3f({location}, {x}, {y}, {z})");

        public void SetUniform(int location, float x, float y, float z, float w) =>
            Log.Add($"SetUniform4f({location}, {x}, {y}, {z}, {w})");

        public void SetUniform(int location, int value) =>
            Log.Add($"SetUniform1i({location}, {value})");

        public void SetUniformMatrix4(int location, ReadOnlySpan<float> matrix, bool transpose = false) =>
            Log.Add($"SetUniformMatrix4({location})");

        public void Draw(uint primitiveMode, int first, uint count) =>
            Log.Add($"Draw({primitiveMode}, {first}, {count})");

        public void DrawIndexed(uint primitiveMode, uint count, uint indexType, nint indicesOffset = 0) =>
            Log.Add($"DrawIndexed({primitiveMode}, {count})");

        public void DrawInstanced(uint primitiveMode, int first, uint count, uint instanceCount) =>
            Log.Add($"DrawInstanced({primitiveMode}, {count}, {instanceCount})");

        public void DrawIndexedInstanced(uint primitiveMode, uint count, uint indexType, nint indicesOffset, uint instanceCount) =>
            Log.Add($"DrawIndexedInstanced({primitiveMode}, {count}, {instanceCount})");

        public void DrawFullscreenQuad() =>
            Log.Add("DrawFullscreenQuad");

        public void Blit(uint sourceFbo, uint destinationFbo, in ViewportRect srcRect, in ViewportRect dstRect, uint mask, uint filter) =>
            Log.Add($"Blit({sourceFbo} -> {destinationFbo})");

        public void Clear(uint mask, in ClearColorValue color, float depth = 1f, int stencil = 0) =>
            Log.Add($"Clear({mask})");

        public void DispatchCompute(uint numGroupsX, uint numGroupsY, uint numGroupsZ) =>
            Log.Add($"DispatchCompute({numGroupsX}, {numGroupsY}, {numGroupsZ})");

        public void MemoryBarrier(uint barriers) =>
            Log.Add($"MemoryBarrier({barriers})");

        public void ExecuteCustom(Action<IRenderContext> callback) =>
            callback(this);
    }

    [Fact]
    public void RecordAndPlayback_DispatchesInExactOrder()
    {
        var cmdBuffer = new RenderCommandBuffer();
        var mock = new MockRenderContext();

        var desc = RenderPassDescriptor.CreateDefault(new ViewportRect(0, 0, 1920, 1080));
        cmdBuffer.BeginPass(desc);
        cmdBuffer.SetViewport(new ViewportRect(0, 0, 1920, 1080));
        cmdBuffer.BindProgram(101);
        cmdBuffer.SetUniform(0, 42.0f);
        cmdBuffer.DrawFullscreenQuad();
        cmdBuffer.EndPass();

        cmdBuffer.CommandCount.Should().Be(6);

        cmdBuffer.Playback(mock);

        mock.Log.Should().ContainInOrder(
            "BeginPass(0)",
            "SetViewport(1920x1080)",
            "BindProgram(101)",
            "SetUniform1f(0, 42)",
            "DrawFullscreenQuad",
            "EndPass"
        );
    }

    [Fact]
    public void RecordAndPlayback_SetColorMaskRoundTripsChannelBits()
    {
        var cmdBuffer = new RenderCommandBuffer();
        var mock = new MockRenderContext();

        cmdBuffer.SetColorMask(true, false, true, false);
        cmdBuffer.Playback(mock);

        mock.Log.Should().ContainSingle().Which.Should().Be("SetColorMask(True, False, True, False)");
    }

    [Fact]
    public void Reset_ClearsCommandsWithoutAllocating()
    {
        var cmdBuffer = new RenderCommandBuffer();
        cmdBuffer.DrawFullscreenQuad();
        cmdBuffer.CommandCount.Should().Be(1);

        cmdBuffer.Reset();
        cmdBuffer.CommandCount.Should().Be(0);

        var mock = new MockRenderContext();
        cmdBuffer.Playback(mock);
        mock.Log.Should().BeEmpty();
    }
}
