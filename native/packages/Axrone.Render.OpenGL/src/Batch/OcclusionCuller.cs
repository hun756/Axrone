namespace Axrone.Render.OpenGL.Batch;

/// <summary>
/// CPU/GPU occlusion culling helper that drives hardware occlusion queries
/// (any-samples-passed targets) through pooled <see cref="GLQuery"/> objects.
/// </summary>
/// <remarks>
/// <para>
/// Meshes that were occluded in the previous frame skip their draw call and issue no query;
/// visible meshes issue a query whose result is consumed on a later frame. Mesh identity is
/// by reference.
/// </para>
/// <para>
/// Query objects are pooled internally at a fixed, validated capacity. When the pool is
/// exhausted the culler conservatively treats meshes as visible and issues no query. Query
/// results that are not yet available at <see cref="EndFrame"/> stay in flight and are
/// consumed on the following frame.
/// </para>
/// <para>
/// All GL calls go through <see cref="GLQuery"/> (BeginQuery/EndQuery/GetQueryParameter on
/// <see cref="IGLApi"/>), so the culler is fully mock-compatible. The culler is thread-affine:
/// all methods except <see cref="Dispose"/> must be called from the render thread.
/// </para>
/// </remarks>
public sealed class OcclusionCuller : IDisposable
{
    private const int DefaultQueryCapacity = 16;
    private const int MaxTrackedMeshes = 4096;
    private const ulong StaleFrameThreshold = 300;

    private readonly GLContext _context;
    private readonly GLQuery[] _queryPool;
    private readonly Queue<GLQuery> _freeQueries;
    private readonly Dictionary<GLMesh, MeshOcclusionState> _meshStates;
    private GLQuery? _activeQuery;
    private MeshOcclusionState? _activeMesh;
    private ulong _frameIndex;
    private int _isDisposed;

    /// <summary>
    /// Gets the fixed number of occlusion queries available to the culler.
    /// </summary>
    public int QueryCapacity => _queryPool.Length;

    /// <summary>
    /// Gets the number of meshes currently tracked by the culler.
    /// </summary>
    public int TrackedMeshCount => _meshStates.Count;

    /// <summary>
    /// Gets a value indicating whether the culler has been disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="OcclusionCuller"/> class.
    /// </summary>
    /// <param name="context">The GL context.</param>
    /// <param name="queryCapacity">The fixed number of pooled occlusion queries. Must be positive.</param>
    public OcclusionCuller(GLContext context, int queryCapacity = DefaultQueryCapacity)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (queryCapacity <= 0)
            ThrowHelper.ThrowInvalidArgument("Query capacity must be positive");

        _context = context;
        _queryPool = new GLQuery[queryCapacity];
        _freeQueries = new Queue<GLQuery>(queryCapacity);
        _meshStates = new Dictionary<GLMesh, MeshOcclusionState>();

        for (int i = 0; i < queryCapacity; i++)
        {
            GLQuery query = new(context, Resources.QueryTarget.AnySamplesPassed, "occlusion");
            _queryPool[i] = query;
            _freeQueries.Enqueue(query);
        }
    }

    /// <summary>
    /// Starts a new frame: clears any query left active by an unbalanced
    /// <see cref="BeginOcclusion"/> call and drops mesh states that have been
    /// unseen for a long while once tracking grows past its bound.
    /// </summary>
    public void BeginFrame()
    {
        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("OcclusionCuller disposed");

        _context.AssertRenderThread();

        _activeQuery = null;
        _activeMesh = null;
        _frameIndex++;

        if (_meshStates.Count > MaxTrackedMeshes)
            PruneStaleMeshes(StaleFrameThreshold);
    }

    /// <summary>
    /// Drops tracked meshes last seen more than <paramref name="staleThresholdFrames"/>
    /// frames ago, returning their in-flight queries to the pool. Destroyed meshes
    /// that the host never untracked stop pinning memory; re-registered meshes start
    /// visible, which is the safe default.
    /// </summary>
    /// <param name="staleThresholdFrames">The unseen-frame age at which a mesh is dropped.</param>
    public void PruneStaleMeshes(ulong staleThresholdFrames)
    {
        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("OcclusionCuller disposed");

        _context.AssertRenderThread();

        List<GLMesh>? stale = null;
        foreach (var pair in _meshStates)
        {
            if (_frameIndex - pair.Value.LastSeenFrame > staleThresholdFrames)
            {
                stale ??= new List<GLMesh>();
                stale.Add(pair.Key);
            }
        }

        if (stale is null)
            return;

        foreach (GLMesh mesh in stale)
        {
            if (_meshStates.Remove(mesh, out MeshOcclusionState? state) && state.InFlightQuery is { } inFlight)
            {
                ReleaseQuery(inFlight);
                state.InFlightQuery = null;
            }
        }
    }

    /// <summary>
    /// Begins occlusion testing for a mesh. Returns <c>false</c> (skip the draw call, issue
    /// no query) when the mesh was occluded in the previous frame; otherwise issues an
    /// any-samples-passed query and returns <c>true</c>.
    /// </summary>
    /// <param name="mesh">The mesh to test.</param>
    /// <returns><c>true</c> if the mesh should be drawn; <c>false</c> if it was occluded.</returns>
    public bool BeginOcclusion(GLMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("OcclusionCuller disposed");

        _context.AssertRenderThread();

        MeshOcclusionState state = GetOrCreateState(mesh);
        state.LastSeenFrame = _frameIndex;

        if (state.InFlightQuery is { } inFlight)
        {
            if (inFlight.GetResultAvailable())
            {
                state.LastVisible = inFlight.GetResult() > 0;
                ReleaseQuery(inFlight);
                state.InFlightQuery = null;
            }
            else
            {
                // Previous result still pending; fall back to last known visibility.
                return state.LastVisible;
            }
        }

        if (!state.LastVisible)
            return false;

        if (!_freeQueries.TryDequeue(out GLQuery? query))
            return true; // Pool exhausted: assume visible, issue no query.

        _activeQuery = query;
        _activeMesh = state;
        query.Begin();
        return true;
    }

    /// <summary>
    /// Ends the query issued by <see cref="BeginOcclusion"/> and parks it on the mesh until
    /// its result is consumed.
    /// </summary>
    public void EndOcclusion()
    {
        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("OcclusionCuller disposed");

        _context.AssertRenderThread();

        if (_activeQuery is null || _activeMesh is null)
            ThrowHelper.ThrowInvalidOperation("No active occlusion query");

        _activeQuery.End();
        _activeMesh.InFlightQuery = _activeQuery;
        _activeQuery = null;
        _activeMesh = null;
    }

    /// <summary>
    /// Gets the last known visibility of a mesh, consuming its in-flight query result when
    /// available. Meshes with no recorded state are considered visible.
    /// </summary>
    /// <param name="mesh">The mesh to query.</param>
    /// <returns><c>true</c> if the mesh is considered visible; otherwise <c>false</c>.</returns>
    public bool IsVisible(GLMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("OcclusionCuller disposed");

        _context.AssertRenderThread();

        if (!_meshStates.TryGetValue(mesh, out MeshOcclusionState? state))
            return true;

        state.LastSeenFrame = _frameIndex;

        if (state.InFlightQuery is { } inFlight && inFlight.GetResultAvailable())
        {
            state.LastVisible = inFlight.GetResult() > 0;
            ReleaseQuery(inFlight);
            state.InFlightQuery = null;
        }

        return state.LastVisible;
    }

    /// <summary>
    /// Ends the frame: parks any still-active query and releases every in-flight query
    /// whose result has become available. Queries with pending results stay in flight.
    /// </summary>
    public void EndFrame()
    {
        if (IsDisposed)
            ThrowHelper.ThrowInvalidOperation("OcclusionCuller disposed");

        _context.AssertRenderThread();

        if (_activeQuery is { } active && _activeMesh is { } activeMesh)
        {
            active.End();
            activeMesh.InFlightQuery = active;
            _activeQuery = null;
            _activeMesh = null;
        }

        foreach (MeshOcclusionState state in _meshStates.Values)
        {
            if (state.InFlightQuery is { } inFlight && inFlight.GetResultAvailable())
            {
                state.LastVisible = inFlight.GetResult() > 0;
                ReleaseQuery(inFlight);
                state.InFlightQuery = null;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        foreach (GLQuery query in _queryPool)
        {
            query.Dispose();
        }

        _freeQueries.Clear();
        _meshStates.Clear();
        _activeQuery = null;
        _activeMesh = null;
    }

    private MeshOcclusionState GetOrCreateState(GLMesh mesh)
    {
        if (!_meshStates.TryGetValue(mesh, out MeshOcclusionState? state))
        {
            state = new MeshOcclusionState();
            _meshStates[mesh] = state;
        }

        return state;
    }

    private void ReleaseQuery(GLQuery query) => _freeQueries.Enqueue(query);

    private sealed class MeshOcclusionState
    {
        public GLQuery? InFlightQuery;
        public bool LastVisible = true;
        public ulong LastSeenFrame;
    }
}
