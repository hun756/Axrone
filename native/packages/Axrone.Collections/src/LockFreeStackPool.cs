namespace Axrone.Collections;

using System.Runtime.CompilerServices;
using System.Threading;

/// <summary>
/// CRTP contract for nodes pooled by <see cref="LockFreeStackPool{TNode}"/>.
/// </summary>
internal interface IPooledWaiterNode<TSelf>
    where TSelf : class, IPooledWaiterNode<TSelf>, new()
{
    TSelf? Next { get; set; }

    void OnRented(LockFreeStackPool<TSelf> pool, bool isEnqueue);
}

/// <summary>
/// Thread-safe stack pool for waiter nodes. Eliminates per-operation allocations:
/// Rent pops an idle node (or allocates when empty), Return pushes it back.
/// Uses a spinlock to prevent ABA problems that affect lock-free Treiber stacks.
/// </summary>
internal sealed class LockFreeStackPool<TNode>
    where TNode : class, IPooledWaiterNode<TNode>, new()
{
    private TNode? _head;
    private SpinLock _lock = new(enableThreadOwnerTracking: false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TNode Rent(bool isEnqueue)
    {
        bool taken = false;
        try
        {
            _lock.Enter(ref taken);
            TNode? node = _head;
            if (node == null)
            {
                _head = null;
                node = new TNode();
            }
            else
            {
                _head = node.Next;
                node.Next = null;
            }
            node.OnRented(this, isEnqueue);
            return node;
        }
        finally
        {
            if (taken) _lock.Exit();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(TNode node)
    {
        bool taken = false;
        try
        {
            _lock.Enter(ref taken);
            node.Next = _head;
            _head = node;
        }
        finally
        {
            if (taken) _lock.Exit();
        }
    }
}
