namespace Axrone.Render.Core;

/// <summary>
/// Discriminator for recorded commands in <see cref="RenderCommandBuffer"/>.
/// </summary>
internal enum CommandOpCode : byte
{
    BeginPass = 0,
    EndPass = 1,
    SetViewport = 2,
    SetScissor = 3,
    SetBlendState = 4,
    SetDepthState = 5,
    SetCullState = 6,
    BindProgram = 7,
    BindTexture = 8,
    BindSampler = 9,
    BindVertexArray = 10,
    SetUniform1f = 11,
    SetUniform2f = 12,
    SetUniform3f = 13,
    SetUniform4f = 14,
    SetUniform1i = 15,
    SetUniformMatrix4 = 16,
    Draw = 17,
    DrawIndexed = 18,
    DrawInstanced = 19,
    DrawIndexedInstanced = 20,
    DrawFullscreenQuad = 21,
    Blit = 22,
    Clear = 23,
    DispatchCompute = 24,
    MemoryBarrier = 25,
    CustomAction = 26,
    SetColorMask = 27,
    SetBlendEquation = 28
}

/// <summary>
/// Tagged union representing a recorded command in a <see cref="RenderCommandBuffer"/>.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
internal readonly struct RecordedCommand : IEquatable<RecordedCommand>
{
    [FieldOffset(0)]
    public readonly CommandOpCode OpCode;

    [FieldOffset(4)]
    public readonly uint Arg0;

    [FieldOffset(8)]
    public readonly uint Arg1;

    [FieldOffset(12)]
    public readonly uint Arg2;

    [FieldOffset(16)]
    public readonly uint Arg3;

    [FieldOffset(20)]
    public readonly int IntArg0;

    [FieldOffset(24)]
    public readonly int IntArg1;

    [FieldOffset(28)]
    public readonly float FloatArg0;

    [FieldOffset(32)]
    public readonly float FloatArg1;

    [FieldOffset(36)]
    public readonly float FloatArg2;

    [FieldOffset(40)]
    public readonly float FloatArg3;

    [FieldOffset(44)]
    public readonly nint NativeIntArg0;

    [FieldOffset(52)]
    public readonly bool BoolArg0;

    [FieldOffset(53)]
    public readonly bool BoolArg1;

    [FieldOffset(56)]
    public readonly ViewportRect Viewport;

    [FieldOffset(72)]
    public readonly ScissorRect Scissor;

    [FieldOffset(88)]
    public readonly ClearColorValue ClearColor;

    [FieldOffset(104)]
    public readonly int MatrixDataOffset;

    [FieldOffset(108)]
    public readonly int CustomActionIndex;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, in ViewportRect vp)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        Viewport = vp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, in ScissorRect sc, bool enabled)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        Scissor = sc;
        BoolArg0 = enabled;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, bool enabled, uint srcRGB, uint dstRGB, uint srcAlpha, uint dstAlpha)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        BoolArg0 = enabled;
        Arg0 = srcRGB;
        Arg1 = dstRGB;
        Arg2 = srcAlpha;
        Arg3 = dstAlpha;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, bool testEnabled, bool writeEnabled, uint func)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        BoolArg0 = testEnabled;
        BoolArg1 = writeEnabled;
        Arg0 = func;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, bool enabled, uint cullMode, uint frontFace)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        BoolArg0 = enabled;
        Arg0 = cullMode;
        Arg1 = frontFace;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, uint arg0)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        Arg0 = arg0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, uint arg0, uint arg1, uint arg2 = 0)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        Arg0 = arg0;
        Arg1 = arg1;
        Arg2 = arg2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, int loc, float v)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        IntArg0 = loc;
        FloatArg0 = v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, int loc, float v0, float v1)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        IntArg0 = loc;
        FloatArg0 = v0;
        FloatArg1 = v1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, int loc, float v0, float v1, float v2)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        IntArg0 = loc;
        FloatArg0 = v0;
        FloatArg1 = v1;
        FloatArg2 = v2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, int loc, float v0, float v1, float v2, float v3)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        IntArg0 = loc;
        FloatArg0 = v0;
        FloatArg1 = v1;
        FloatArg2 = v2;
        FloatArg3 = v3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, int loc, int v)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        IntArg0 = loc;
        IntArg1 = v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, int loc, int matrixOffset, bool transpose)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        IntArg0 = loc;
        MatrixDataOffset = matrixOffset;
        BoolArg0 = transpose;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, uint mode, int first, uint count)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        Arg0 = mode;
        IntArg0 = first;
        Arg1 = count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, uint mode, uint count, uint indexType, nint offset)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        Arg0 = mode;
        Arg1 = count;
        Arg2 = indexType;
        NativeIntArg0 = offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, uint mode, int first, uint count, uint instanceCount)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        Arg0 = mode;
        IntArg0 = first;
        Arg1 = count;
        Arg2 = instanceCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, uint mode, uint count, uint indexType, nint offset, uint instanceCount)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        Arg0 = mode;
        Arg1 = count;
        Arg2 = indexType;
        NativeIntArg0 = offset;
        Arg3 = instanceCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, uint srcFbo, uint dstFbo, in ViewportRect srcRect, in ViewportRect dstRect, uint mask, uint filter)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        Arg0 = srcFbo;
        Arg1 = dstFbo;
        Viewport = srcRect;
        Scissor = new ScissorRect(dstRect.X, dstRect.Y, dstRect.Width, dstRect.Height);
        Arg2 = mask;
        Arg3 = filter;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, uint mask, in ClearColorValue color, float depth, int stencil)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        Arg0 = mask;
        ClearColor = color;
        FloatArg0 = depth;
        IntArg0 = stencil;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RecordedCommand(CommandOpCode opCode, int customActionIndex)
    {
        Unsafe.SkipInit(out this);
        OpCode = opCode;
        CustomActionIndex = customActionIndex;
    }

    /// <inheritdoc/>
    public bool Equals(RecordedCommand other) =>
        OpCode == other.OpCode &&
        Arg0 == other.Arg0 &&
        Arg1 == other.Arg1 &&
        Arg2 == other.Arg2 &&
        Arg3 == other.Arg3 &&
        IntArg0 == other.IntArg0 &&
        IntArg1 == other.IntArg1 &&
        FloatArg0 == other.FloatArg0 &&
        FloatArg1 == other.FloatArg1 &&
        FloatArg2 == other.FloatArg2 &&
        FloatArg3 == other.FloatArg3 &&
        NativeIntArg0 == other.NativeIntArg0 &&
        BoolArg0 == other.BoolArg0 &&
        BoolArg1 == other.BoolArg1 &&
        Viewport == other.Viewport &&
        Scissor == other.Scissor &&
        ClearColor == other.ClearColor &&
        MatrixDataOffset == other.MatrixDataOffset &&
        CustomActionIndex == other.CustomActionIndex;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is RecordedCommand other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine((byte)OpCode, Arg0, Arg1, IntArg0, FloatArg0);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(in RecordedCommand left, in RecordedCommand right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(in RecordedCommand left, in RecordedCommand right) => !left.Equals(right);
}

/// <summary>
/// High-performance recording command buffer that implements <see cref="IRenderContext"/>.
/// Allows recording rendering commands on any thread and replaying them on the dedicated render thread.
/// Achieves steady-state zero memory allocation through pooled linear buffers.
/// </summary>
public sealed class RenderCommandBuffer : IRenderContext
{
    private readonly List<RecordedCommand> _commands;
    private readonly List<RenderPassDescriptor> _passDescriptors;
    private readonly List<float> _matrixBuffer;
    private readonly List<Action<IRenderContext>> _customActions;

    /// <summary>Gets the number of recorded commands.</summary>
    public int CommandCount => _commands.Count;

    /// <summary>Initializes a new instance of <see cref="RenderCommandBuffer"/>.</summary>
    public RenderCommandBuffer(int initialCapacity = 256)
    {
        _commands = new List<RecordedCommand>(initialCapacity);
        _passDescriptors = new List<RenderPassDescriptor>(8);
        _matrixBuffer = new List<float>(64);
        _customActions = new List<Action<IRenderContext>>(4);
    }

    /// <summary>Resets the buffer for recording the next frame without deallocating backing storage.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _commands.Clear();
        _passDescriptors.Clear();
        _matrixBuffer.Clear();
        _customActions.Clear();
    }

    /// <summary>Replays all recorded commands in sequence onto the target render context.</summary>
    public void Playback(IRenderContext target)
    {
        ArgumentNullException.ThrowIfNull(target);

        int count = _commands.Count;
        int passDescIdx = 0;

        for (int i = 0; i < count; i++)
        {
            ref readonly var cmd = ref CollectionsMarshal.AsSpan(_commands)[i];
            switch (cmd.OpCode)
            {
                case CommandOpCode.BeginPass:
                    target.BeginPass(_passDescriptors[passDescIdx++]);
                    break;
                case CommandOpCode.EndPass:
                    target.EndPass();
                    break;
                case CommandOpCode.SetViewport:
                    target.SetViewport(cmd.Viewport);
                    break;
                case CommandOpCode.SetScissor:
                    target.SetScissor(cmd.Scissor, cmd.BoolArg0);
                    break;
                case CommandOpCode.SetBlendState:
                    target.SetBlendState(cmd.BoolArg0, cmd.Arg0, cmd.Arg1, cmd.Arg2, cmd.Arg3);
                    break;
                case CommandOpCode.SetBlendEquation:
                    target.SetBlendEquation(cmd.Arg0, cmd.Arg1);
                    break;
                case CommandOpCode.SetDepthState:
                    target.SetDepthState(cmd.BoolArg0, cmd.BoolArg1, cmd.Arg0);
                    break;
                case CommandOpCode.SetCullState:
                    target.SetCullState(cmd.BoolArg0, cmd.Arg0, cmd.Arg1);
                    break;
                case CommandOpCode.SetColorMask:
                    target.SetColorMask((cmd.Arg0 & 1u) != 0, (cmd.Arg0 & 2u) != 0, (cmd.Arg0 & 4u) != 0, (cmd.Arg0 & 8u) != 0);
                    break;
                case CommandOpCode.BindProgram:
                    target.BindProgram(cmd.Arg0);
                    break;
                case CommandOpCode.BindTexture:
                    target.BindTexture(cmd.Arg0, cmd.Arg1, cmd.Arg2);
                    break;
                case CommandOpCode.BindSampler:
                    target.BindSampler(cmd.Arg0, cmd.Arg1);
                    break;
                case CommandOpCode.BindVertexArray:
                    target.BindVertexArray(cmd.Arg0);
                    break;
                case CommandOpCode.SetUniform1f:
                    target.SetUniform(cmd.IntArg0, cmd.FloatArg0);
                    break;
                case CommandOpCode.SetUniform2f:
                    target.SetUniform(cmd.IntArg0, cmd.FloatArg0, cmd.FloatArg1);
                    break;
                case CommandOpCode.SetUniform3f:
                    target.SetUniform(cmd.IntArg0, cmd.FloatArg0, cmd.FloatArg1, cmd.FloatArg2);
                    break;
                case CommandOpCode.SetUniform4f:
                    target.SetUniform(cmd.IntArg0, cmd.FloatArg0, cmd.FloatArg1, cmd.FloatArg2, cmd.FloatArg3);
                    break;
                case CommandOpCode.SetUniform1i:
                    target.SetUniform(cmd.IntArg0, cmd.IntArg1);
                    break;
                case CommandOpCode.SetUniformMatrix4:
                    ReadOnlySpan<float> matrixSpan = CollectionsMarshal.AsSpan(_matrixBuffer).Slice(cmd.MatrixDataOffset, 16);
                    target.SetUniformMatrix4(cmd.IntArg0, matrixSpan, cmd.BoolArg0);
                    break;
                case CommandOpCode.Draw:
                    target.Draw(cmd.Arg0, cmd.IntArg0, cmd.Arg1);
                    break;
                case CommandOpCode.DrawIndexed:
                    target.DrawIndexed(cmd.Arg0, cmd.Arg1, cmd.Arg2, cmd.NativeIntArg0);
                    break;
                case CommandOpCode.DrawInstanced:
                    target.DrawInstanced(cmd.Arg0, cmd.IntArg0, cmd.Arg1, cmd.Arg2);
                    break;
                case CommandOpCode.DrawIndexedInstanced:
                    target.DrawIndexedInstanced(cmd.Arg0, cmd.Arg1, cmd.Arg2, cmd.NativeIntArg0, cmd.Arg3);
                    break;
                case CommandOpCode.DrawFullscreenQuad:
                    target.DrawFullscreenQuad();
                    break;
                case CommandOpCode.Blit:
                    target.Blit(cmd.Arg0, cmd.Arg1, cmd.Viewport, new ViewportRect(cmd.Scissor.X, cmd.Scissor.Y, cmd.Scissor.Width, cmd.Scissor.Height), cmd.Arg2, cmd.Arg3);
                    break;
                case CommandOpCode.Clear:
                    target.Clear(cmd.Arg0, cmd.ClearColor, cmd.FloatArg0, cmd.IntArg0);
                    break;
                case CommandOpCode.DispatchCompute:
                    target.DispatchCompute(cmd.Arg0, cmd.Arg1, cmd.Arg2);
                    break;
                case CommandOpCode.MemoryBarrier:
                    target.MemoryBarrier(cmd.Arg0);
                    break;
                case CommandOpCode.CustomAction:
                    _customActions[cmd.CustomActionIndex](target);
                    break;
            }
        }
    }

    /// <inheritdoc/>
    public void BeginPass(in RenderPassDescriptor descriptor)
    {
        _passDescriptors.Add(descriptor);
        _commands.Add(new RecordedCommand(CommandOpCode.BeginPass));
    }

    /// <inheritdoc/>
    public void EndPass() =>
        _commands.Add(new RecordedCommand(CommandOpCode.EndPass));

    /// <inheritdoc/>
    public void SetViewport(in ViewportRect viewport) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetViewport, in viewport));

    /// <inheritdoc/>
    public void SetScissor(in ScissorRect scissor, bool enabled = true) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetScissor, in scissor, enabled));

    /// <inheritdoc/>
    public void SetBlendState(bool enabled, uint srcRGB = 0, uint dstRGB = 0, uint srcAlpha = 0, uint dstAlpha = 0) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetBlendState, enabled, srcRGB, dstRGB, srcAlpha, dstAlpha));

    /// <inheritdoc/>
    public void SetBlendEquation(uint equationRGB, uint equationAlpha) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetBlendEquation, equationRGB, equationAlpha));

    /// <inheritdoc/>
    public void SetDepthState(bool testEnabled, bool writeEnabled, uint depthFunc = 0) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetDepthState, testEnabled, writeEnabled, depthFunc));

    /// <inheritdoc/>
    public void SetCullState(bool enabled, uint cullMode = 0, uint frontFace = 0) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetCullState, enabled, cullMode, frontFace));

    /// <inheritdoc/>
    public void SetColorMask(bool red, bool green, bool blue, bool alpha)
    {
        uint mask = (red ? 1u : 0u) | (green ? 2u : 0u) | (blue ? 4u : 0u) | (alpha ? 8u : 0u);
        _commands.Add(new RecordedCommand(CommandOpCode.SetColorMask, mask));
    }

    /// <inheritdoc/>
    public void BindProgram(uint programId) =>
        _commands.Add(new RecordedCommand(CommandOpCode.BindProgram, programId));

    /// <inheritdoc/>
    public void BindTexture(uint slot, uint textureId, uint target = 0) =>
        _commands.Add(new RecordedCommand(CommandOpCode.BindTexture, slot, textureId, target));

    /// <inheritdoc/>
    public void BindSampler(uint slot, uint samplerId) =>
        _commands.Add(new RecordedCommand(CommandOpCode.BindSampler, slot, samplerId));

    /// <inheritdoc/>
    public void BindVertexArray(uint vaoId) =>
        _commands.Add(new RecordedCommand(CommandOpCode.BindVertexArray, vaoId));

    /// <inheritdoc/>
    public void SetUniform(int location, float value) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetUniform1f, location, value));

    /// <inheritdoc/>
    public void SetUniform(int location, float x, float y) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetUniform2f, location, x, y));

    /// <inheritdoc/>
    public void SetUniform(int location, float x, float y, float z) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetUniform3f, location, x, y, z));

    /// <inheritdoc/>
    public void SetUniform(int location, float x, float y, float z, float w) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetUniform4f, location, x, y, z, w));

    /// <inheritdoc/>
    public void SetUniform(int location, int value) =>
        _commands.Add(new RecordedCommand(CommandOpCode.SetUniform1i, location, value));

    /// <inheritdoc/>
    public void SetUniformMatrix4(int location, ReadOnlySpan<float> matrix, bool transpose = false)
    {
        if (matrix.Length < 16)
        {
            ThrowHelper.ThrowArgumentOutOfRange(nameof(matrix), matrix.Length, "Matrix must have at least 16 float elements.");
        }

        int offset = _matrixBuffer.Count;
        for (int i = 0; i < 16; i++)
        {
            _matrixBuffer.Add(matrix[i]);
        }

        _commands.Add(new RecordedCommand(CommandOpCode.SetUniformMatrix4, location, offset, transpose));
    }

    /// <inheritdoc/>
    public void Draw(uint primitiveMode, int first, uint count) =>
        _commands.Add(new RecordedCommand(CommandOpCode.Draw, primitiveMode, first, count));

    /// <inheritdoc/>
    public void DrawIndexed(uint primitiveMode, uint count, uint indexType, nint indicesOffset = 0) =>
        _commands.Add(new RecordedCommand(CommandOpCode.DrawIndexed, primitiveMode, count, indexType, indicesOffset));

    /// <inheritdoc/>
    public void DrawInstanced(uint primitiveMode, int first, uint count, uint instanceCount) =>
        _commands.Add(new RecordedCommand(CommandOpCode.DrawInstanced, primitiveMode, first, count, instanceCount));

    /// <inheritdoc/>
    public void DrawIndexedInstanced(uint primitiveMode, uint count, uint indexType, nint indicesOffset, uint instanceCount) =>
        _commands.Add(new RecordedCommand(CommandOpCode.DrawIndexedInstanced, primitiveMode, count, indexType, indicesOffset, instanceCount));

    /// <inheritdoc/>
    public void DrawFullscreenQuad() =>
        _commands.Add(new RecordedCommand(CommandOpCode.DrawFullscreenQuad));

    /// <inheritdoc/>
    public void Blit(uint sourceFbo, uint destinationFbo, in ViewportRect srcRect, in ViewportRect dstRect, uint mask, uint filter) =>
        _commands.Add(new RecordedCommand(CommandOpCode.Blit, sourceFbo, destinationFbo, in srcRect, in dstRect, mask, filter));

    /// <inheritdoc/>
    public void Clear(uint mask, in ClearColorValue color, float depth = 1f, int stencil = 0) =>
        _commands.Add(new RecordedCommand(CommandOpCode.Clear, mask, in color, depth, stencil));

    /// <inheritdoc/>
    public void DispatchCompute(uint numGroupsX, uint numGroupsY, uint numGroupsZ) =>
        _commands.Add(new RecordedCommand(CommandOpCode.DispatchCompute, numGroupsX, numGroupsY, numGroupsZ));

    /// <inheritdoc/>
    public void MemoryBarrier(uint barriers) =>
        _commands.Add(new RecordedCommand(CommandOpCode.MemoryBarrier, barriers));

    /// <inheritdoc/>
    public void ExecuteCustom(Action<IRenderContext> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        int idx = _customActions.Count;
        _customActions.Add(callback);
        _commands.Add(new RecordedCommand(CommandOpCode.CustomAction, idx));
    }
}
