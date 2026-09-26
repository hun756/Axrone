namespace Axrone.Render.OpenGL.Resources;

/// <summary>Query target types.</summary>
public enum QueryTarget : uint
{
    /// <summary>No query (invalid target; zero sentinel).</summary>
    None = 0,

    /// <summary>Samples-passed query.</summary>
    SamplesPassed = 0x8914,

    /// <summary>Any-samples-passed query.</summary>
    AnySamplesPassed = 0x8C2F,

    /// <summary>Timer elapsed query.</summary>
    TimeElapsed = 0x88BF,

    /// <summary>Timestamp query.</summary>
    Timestamp = 0x8E28,

    /// <summary>Primitives generated query.</summary>
    PrimitivesGenerated = 0x8C87,

    /// <summary>Transform feedback primitives written.</summary>
    TransformFeedbackPrimitivesWritten = 0x8C88,
}

/// <summary>
/// GPU query object for timing, occlusion, and transform feedback queries.
/// </summary>
public sealed class GLQuery : IGLResource, IDisposable
{
    private readonly GLContext _context;
    private readonly IGLApi _gl;
    private int _disposed;
    private bool _isActive;

    /// <summary>Gets the query handle.</summary>
    public uint Id { get; private set; }

    /// <summary>Gets the query target.</summary>
    public QueryTarget Target { get; }

    /// <summary>Gets the debug label.</summary>
    public string Label { get; }

    /// <inheritdoc/>
    public int RegistrySequence { get; set; }

    /// <inheritdoc/>
    public int RebuildPriority => 50;

    /// <summary>Gets a value indicating whether this query has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets a value indicating whether this query is currently active (Begin called without End).</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="GLQuery"/> class.
    /// </summary>
    public GLQuery(GLContext context, QueryTarget target, string label = "query")
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _gl = context.GL;
        Target = target;
        Label = label;

        _context.AssertRenderThread();

        Id = _gl.GenQuery();
        _context.Registry.Register(this);

        if (_context.DebugLabelsEnabled)
        {
            _gl.ObjectLabel(GLConst.Query, Id, label);
        }
    }

    /// <summary>Begins the query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Begin()
    {
        EnsureAlive();
        _context.AssertRenderThread();

        if (_isActive)
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Query already active", nameof(GLQuery));

        _gl.BeginQuery((uint)Target, Id);
        _isActive = true;
    }

    /// <summary>Ends the query.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void End()
    {
        EnsureAlive();
        _context.AssertRenderThread();

        if (!_isActive)
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Query is not active", nameof(GLQuery));

        _gl.EndQuery((uint)Target);
        _isActive = false;
    }

    /// <summary>Gets the query result.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetResult()
    {
        EnsureAlive();
        _gl.GetQueryParameter(Id, GLConst.QueryResult, out int result);
        return result;
    }

    /// <summary>Checks if the query result is available.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetResultAvailable()
    {
        EnsureAlive();
        _gl.GetQueryParameter(Id, GLConst.QueryResultAvailable, out int available);
        return available != 0;
    }

    /// <inheritdoc/>
    public void Invalidate() => Id = 0;

    /// <inheritdoc/>
    public void Rebuild() => OnContextRestored();

    /// <inheritdoc/>
    public void OnContextLost()
    {
        Id = 0;
        _isActive = false;
    }

    /// <inheritdoc/>
    public void OnContextRestored()
    {
        if (IsDisposed) return;

        Id = _gl.GenQuery();

        if (_context.DebugLabelsEnabled && !string.IsNullOrEmpty(Label))
        {
            _gl.ObjectLabel(GLConst.Query, Id, Label);
        }
    }

    private void EnsureAlive()
    {
        if (IsDisposed)
            ThrowHelper.Throw(RenderErrorCode.InvalidOperation, "Query has been disposed", nameof(GLQuery));

        if (_context.Lifecycle.IsLost)
            ThrowHelper.Throw(RenderErrorCode.ContextLost, "GL context is lost", nameof(GLQuery));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        if (Id != 0)
        {
            _gl.DeleteQuery(Id);
            Id = 0;
        }

        _context.Registry.Unregister(this);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"GLQuery: Id={Id}, Target={Target}, Active={_isActive}, Label=\"{Label}\"";
}
