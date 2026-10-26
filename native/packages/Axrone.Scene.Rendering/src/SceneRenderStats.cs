namespace Axrone.Scene.Rendering;

/// <summary>Per-frame render planning and submission counters.</summary>
public readonly record struct SceneRenderStats(
    int Frame,
    int DrawCalls,
    int TrianglesSubmitted,
    int PassCount,
    int OpaqueCount,
    int TransparentCount,
    int CulledCount,
    int SpriteCount);

/// <summary>
/// Mutable frame accumulator used by the planner and backend executor. The
/// instance is reused every frame; reading a snapshot copies a value struct.
/// </summary>
public sealed class SceneRenderFrameState
{
    private int _frame;
    private int _drawCalls;
    private int _trianglesSubmitted;
    private int _opaqueCount;
    private int _transparentCount;
    private int _culledCount;
    private int _spriteCount;

    /// <summary>Current frame index.</summary>
    public int Frame => _frame;

    /// <summary>Submitted draw calls.</summary>
    public int DrawCalls => _drawCalls;

    /// <summary>Submitted triangles.</summary>
    public int TrianglesSubmitted => _trianglesSubmitted;

    /// <summary>Planned opaque items.</summary>
    public int OpaqueCount => _opaqueCount;

    /// <summary>Planned transparent items.</summary>
    public int TransparentCount => _transparentCount;

    /// <summary>Items rejected by culling.</summary>
    public int CulledCount => _culledCount;

    /// <summary>Planned sprite items.</summary>
    public int SpriteCount => _spriteCount;

    /// <summary>Starts a frame and clears every counter.</summary>
    public void Begin(int frame)
    {
        _frame = frame;
        _drawCalls = 0;
        _trianglesSubmitted = 0;
        _opaqueCount = 0;
        _transparentCount = 0;
        _culledCount = 0;
        _spriteCount = 0;
    }

    /// <summary>Records one submitted draw.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordDraw(int triangleCount, int drawCalls = 1)
    {
        _drawCalls += Math.Max(0, drawCalls);
        _trianglesSubmitted += Math.Max(0, triangleCount);
    }

    /// <summary>Records culled items.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordCulled(int count = 1) => _culledCount += Math.Max(0, count);

    /// <summary>Records the split of planned items.</summary>
    public void RecordQueues(int opaqueCount, int transparentCount)
    {
        _opaqueCount += Math.Max(0, opaqueCount);
        _transparentCount += Math.Max(0, transparentCount);
    }

    /// <summary>Records planned sprite items.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordSprites(int count) => _spriteCount += Math.Max(0, count);

    /// <summary>Takes an immutable counter snapshot.</summary>
    public SceneRenderStats Snapshot(int passCount) => new(
        _frame,
        _drawCalls,
        _trianglesSubmitted,
        passCount,
        _opaqueCount,
        _transparentCount,
        _culledCount,
        _spriteCount);
}
