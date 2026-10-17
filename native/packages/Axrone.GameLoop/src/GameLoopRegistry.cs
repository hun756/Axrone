namespace Axrone.GameLoop;

/// <summary>
/// Priority-ordered system registry with duck-typed enumeration and
/// per-phase dispatch with policy-driven error handling.
/// </summary>
/// <typeparam name="TState">Frame state type.</typeparam>
public sealed class SystemRegistry<TState> where TState : struct
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct RegisteredNode
    {
        /// <summary>System.</summary>
        public IGameLoopSystem<TState> System;

        /// <summary>Identity.</summary>
        public SystemId Id;

        /// <summary>Priority.</summary>
        public SystemPriority Priority;

        /// <summary>Registration order.</summary>
        public uint InsertionOrder;
    }

    private RegisteredNode[] _nodes;
    private int _count;
    private uint _insertionCounter;
    private readonly Lock _gate = new();

    /// <summary>Registered system count.</summary>
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref _count);
    }

    /// <summary>Creates a registry.</summary>
    public SystemRegistry(int capacity = 16)
    {
        _nodes = new RegisteredNode[Math.Max(4, capacity)];
    }

    /// <summary>Enumerates systems in run order.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_nodes.AsSpan(0, Volatile.Read(ref _count)));

    /// <summary>Duck-typed enumerator.</summary>
    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<RegisteredNode> _span;
        private int _index;

        /// <summary>Creates an enumerator.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(ReadOnlySpan<RegisteredNode> span)
        {
            _span = span;
            _index = -1;
        }

        /// <summary>Advances.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            if ((uint)next < (uint)_span.Length)
            {
                _index = next;
                return true;
            }

            return false;
        }

        /// <summary>Current node.</summary>
        public readonly ref readonly RegisteredNode Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _span[_index];
        }
    }

    /// <summary>Registers a system in priority order.</summary>
    public void Register(IGameLoopSystem<TState> system)
    {
        ArgumentNullException.ThrowIfNull(system);
        lock (_gate)
        {
            ReadOnlySpan<RegisteredNode> span = _nodes.AsSpan(0, _count);
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i].Id == system.Id)
                {
                    GameLoopThrowHelper.ThrowDuplicateSystem(system.Id);
                }
            }

            if (_count == _nodes.Length)
            {
                Array.Resize(ref _nodes, _nodes.Length * 2);
            }

            _nodes[_count++] = new RegisteredNode
            {
                System = system,
                Id = system.Id,
                Priority = system.Priority,
                InsertionOrder = ++_insertionCounter,
            };

            SortSystems();
        }
    }

    /// <summary>Removes a system, disposing it outside the gate.</summary>
    public bool Unregister(SystemId id)
    {
        IGameLoopSystem<TState>? toDispose = null;
        lock (_gate)
        {
            int index = -1;
            ReadOnlySpan<RegisteredNode> span = _nodes.AsSpan(0, _count);
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i].Id == id)
                {
                    index = i;
                    toDispose = span[i].System;
                    break;
                }
            }

            if (index < 0)
            {
                return false;
            }

            int remaining = _count - index - 1;
            if (remaining > 0)
            {
                Array.Copy(_nodes, index + 1, _nodes, index, remaining);
            }

            _nodes[--_count] = default;
        }

        toDispose?.Dispose();
        return true;
    }

    /// <summary>Clears all systems, disposing outside the gate.</summary>
    public void Clear()
    {
        RegisteredNode[] snapshot;
        int count;

        lock (_gate)
        {
            count = _count;
            if (count == 0)
            {
                return;
            }

            snapshot = new RegisteredNode[count];
            Array.Copy(_nodes, snapshot, count);
            Array.Clear(_nodes, 0, _count);
            _count = 0;
        }

        for (int i = 0; i < count; i++)
        {
            snapshot[i].System.Dispose();
        }
    }

    /// <summary>Whether a system is registered.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(SystemId id)
    {
        ReadOnlySpan<RegisteredNode> span = _nodes.AsSpan(0, Volatile.Read(ref _count));
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i].Id == id)
            {
                return true;
            }
        }

        return false;
    }

    private void SortSystems()
    {
        for (int i = 1; i < _count; i++)
        {
            RegisteredNode key = _nodes[i];
            int j = i - 1;

            while (j >= 0 && CompareNodes(in _nodes[j], in key) > 0)
            {
                _nodes[j + 1] = _nodes[j];
                j--;
            }

            _nodes[j + 1] = key;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareNodes(in RegisteredNode a, in RegisteredNode b)
    {
        int p = b.Priority.CompareTo(a.Priority);
        if (p != 0)
        {
            return p;
        }

        return a.InsertionOrder.CompareTo(b.InsertionOrder);
    }

    /// <summary>Dispatches BeforeUpdate with retry policy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void DispatchBeforeUpdate<TError>(ref readonly BeforeUpdateContext<TState> ctx, ref TState state)
        where TError : struct, IErrorRecoveryPolicy<TState>
    {
        ReadOnlySpan<RegisteredNode> span = _nodes.AsSpan(0, Volatile.Read(ref _count));
        nuint length = (nuint)span.Length;
        for (nuint i = 0; i < length; i++)
        {
            ref readonly RegisteredNode node = ref span[(int)i];
            if (!node.System.IsEnabled)
            {
                continue;
            }

            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    node.System.BeforeUpdate(in ctx);
                    break;
                }
                catch (Exception ex)
                {
                    ErrorDisposition disposition = TError.OnError(ex, node.Id, GameLoopPhase.BeforeUpdate, attempt, ref state);
                    if (disposition == ErrorDisposition.Retry)
                    {
                        continue;
                    }

                    if (disposition == ErrorDisposition.Continue)
                    {
                        break;
                    }

                    GameLoopThrowHelper.ThrowSystemFault(node.Id, GameLoopPhase.BeforeUpdate, attempt, ex);
                    break;
                }
            }
        }
    }

    /// <summary>Dispatches FixedUpdate with retry policy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void DispatchFixedUpdate<TError>(ref readonly FixedUpdateContext<TState> ctx, ref TState state)
        where TError : struct, IErrorRecoveryPolicy<TState>
    {
        ReadOnlySpan<RegisteredNode> span = _nodes.AsSpan(0, Volatile.Read(ref _count));
        nuint length = (nuint)span.Length;
        for (nuint i = 0; i < length; i++)
        {
            ref readonly RegisteredNode node = ref span[(int)i];
            if (!node.System.IsEnabled)
            {
                continue;
            }

            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    node.System.FixedUpdate(in ctx);
                    break;
                }
                catch (Exception ex)
                {
                    ErrorDisposition disposition = TError.OnError(ex, node.Id, GameLoopPhase.FixedUpdate, attempt, ref state);
                    if (disposition == ErrorDisposition.Retry)
                    {
                        continue;
                    }

                    if (disposition == ErrorDisposition.Continue)
                    {
                        break;
                    }

                    GameLoopThrowHelper.ThrowSystemFault(node.Id, GameLoopPhase.FixedUpdate, attempt, ex);
                    break;
                }
            }
        }
    }

    /// <summary>Dispatches Update with retry policy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void DispatchUpdate<TError>(ref readonly UpdateContext<TState> ctx, ref TState state)
        where TError : struct, IErrorRecoveryPolicy<TState>
    {
        ReadOnlySpan<RegisteredNode> span = _nodes.AsSpan(0, Volatile.Read(ref _count));
        nuint length = (nuint)span.Length;
        for (nuint i = 0; i < length; i++)
        {
            ref readonly RegisteredNode node = ref span[(int)i];
            if (!node.System.IsEnabled)
            {
                continue;
            }

            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    node.System.Update(in ctx);
                    break;
                }
                catch (Exception ex)
                {
                    ErrorDisposition disposition = TError.OnError(ex, node.Id, GameLoopPhase.Update, attempt, ref state);
                    if (disposition == ErrorDisposition.Retry)
                    {
                        continue;
                    }

                    if (disposition == ErrorDisposition.Continue)
                    {
                        break;
                    }

                    GameLoopThrowHelper.ThrowSystemFault(node.Id, GameLoopPhase.Update, attempt, ex);
                    break;
                }
            }
        }
    }

    /// <summary>Dispatches Render with retry policy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void DispatchRender<TError>(ref readonly RenderContext<TState> ctx, ref TState state)
        where TError : struct, IErrorRecoveryPolicy<TState>
    {
        ReadOnlySpan<RegisteredNode> span = _nodes.AsSpan(0, Volatile.Read(ref _count));
        nuint length = (nuint)span.Length;
        for (nuint i = 0; i < length; i++)
        {
            ref readonly RegisteredNode node = ref span[(int)i];
            if (!node.System.IsEnabled)
            {
                continue;
            }

            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    node.System.Render(in ctx);
                    break;
                }
                catch (Exception ex)
                {
                    ErrorDisposition disposition = TError.OnError(ex, node.Id, GameLoopPhase.Render, attempt, ref state);
                    if (disposition == ErrorDisposition.Retry)
                    {
                        continue;
                    }

                    if (disposition == ErrorDisposition.Continue)
                    {
                        break;
                    }

                    GameLoopThrowHelper.ThrowSystemFault(node.Id, GameLoopPhase.Render, attempt, ex);
                    break;
                }
            }
        }
    }

    /// <summary>Dispatches AfterFrame with retry policy.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void DispatchAfterFrame<TError>(ref readonly AfterFrameContext<TState> ctx, ref TState state)
        where TError : struct, IErrorRecoveryPolicy<TState>
    {
        ReadOnlySpan<RegisteredNode> span = _nodes.AsSpan(0, Volatile.Read(ref _count));
        nuint length = (nuint)span.Length;
        for (nuint i = 0; i < length; i++)
        {
            ref readonly RegisteredNode node = ref span[(int)i];
            if (!node.System.IsEnabled)
            {
                continue;
            }

            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    node.System.AfterFrame(in ctx);
                    break;
                }
                catch (Exception ex)
                {
                    ErrorDisposition disposition = TError.OnError(ex, node.Id, GameLoopPhase.AfterFrame, attempt, ref state);
                    if (disposition == ErrorDisposition.Retry)
                    {
                        continue;
                    }

                    if (disposition == ErrorDisposition.Continue)
                    {
                        break;
                    }

                    GameLoopThrowHelper.ThrowSystemFault(node.Id, GameLoopPhase.AfterFrame, attempt, ex);
                    break;
                }
            }
        }
    }
}
