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
/// Lock-free Treiber-stack pool for waiter nodes. Eliminates per-operation allocations:
/// Rent pops an idle node (or allocates when empty), Return pushes it back via CAS.
/// </summary>
internal sealed class LockFreeStackPool<TNode>
    where TNode : class, IPooledWaiterNode<TNode>, new()
{
    private TNode? _head;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TNode Rent(bool isEnqueue)
    {
        TNode? node;
        while (true)
        {
            node = Volatile.Read(ref _head);
            if (node == null)
            {
                node = new TNode();
                break;
            }

            if (Interlocked.CompareExchange(ref _head, node.Next, node) == node)
            {
                break;
            }
        }

        node.OnRented(this, isEnqueue);
        return node;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(TNode node)
    {
        while (true)
        {
            node.Next = Volatile.Read(ref _head);
            if (Interlocked.CompareExchange(ref _head, node, node.Next) == node.Next)
            {
                break;
            }
        }
    }
}
