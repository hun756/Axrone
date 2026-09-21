namespace Axrone.Utility.Concurrency;

using System.Threading.Tasks.Sources;

/// <summary>
/// Address-bucketed async waiters for wait/notify rendezvous. Exactly-once completion is driven
/// by chain membership under one gate: unlink wins cancel, detach wins wake, never both.
/// </summary>
/// <remarks>
/// Cancellation disposes its registration in the consumer's GetResult, never inside the
/// callback itself — disposing a registration from within its own callback deadlocks.
/// </remarks>
internal sealed class AsyncWaitTable
{
    public static readonly AsyncWaitTable Shared = new();

    private readonly Lock _gate = new();
    private readonly AsyncWaitNode?[] _heads = new AsyncWaitNode?[512];
    private readonly AsyncWaitNode?[] _tails = new AsyncWaitNode?[512];
    private AsyncWaitNode? _pool;

    /// <summary>Links a waiter and hooks cancellation.</summary>
    public AsyncWaitNode Enqueue(int bucket, CancellationToken cancellationToken)
    {
        AsyncWaitNode node;
        lock (_gate)
        {
            node = _pool ?? new AsyncWaitNode();
            if (node == _pool)
            {
                _pool = node.Next;
            }

            node.Reset(this, bucket);
            node.Next = null;
            if (_tails[bucket] == null)
            {
                _heads[bucket] = _tails[bucket] = node;
            }
            else
            {
                _tails[bucket]!.Next = node;
                _tails[bucket] = node;
            }
        }

        node.HookCancellation(cancellationToken);
        return node;
    }

    /// <summary>Silently unlinks a waiter (recheck raced ahead); the caller releases it.</summary>
    public bool TryUnlink(AsyncWaitNode node, int bucket)
    {
        lock (_gate)
        {
            return Unlink(node, bucket);
        }
    }

    /// <summary>Cancels a waiter if still linked; a detached waiter belongs to the waker.</summary>
    public bool TryCancel(AsyncWaitNode node, int bucket, CancellationToken token)
    {
        lock (_gate)
        {
            if (Unlink(node, bucket))
            {
                node.Fail(new OperationCanceledException(token));
                return true;
            }

            return false;
        }
    }

    /// <summary>Wakes one or all waiters in the bucket.</summary>
    public void Wake(int bucket, bool all)
    {
        AsyncWaitNode? detached = null;
        AsyncWaitNode? tail = null;
        lock (_gate)
        {
            while (_heads[bucket] != null)
            {
                AsyncWaitNode current = _heads[bucket]!;
                _heads[bucket] = current.Next;
                if (_heads[bucket] == null)
                {
                    _tails[bucket] = null;
                }

                current.Next = null;
                if (tail == null)
                {
                    detached = tail = current;
                }
                else
                {
                    tail.Next = current;
                    tail = current;
                }

                if (!all)
                {
                    break;
                }
            }
        }

        while (detached != null)
        {
            AsyncWaitNode current = detached;
            detached = detached.Next;
            current.Next = null;
            current.Complete();
        }
    }

    /// <summary>Returns a settled node to the pool.</summary>
    public void Release(AsyncWaitNode node)
    {
        lock (_gate)
        {
            node.Next = _pool;
            _pool = node;
        }
    }

    private bool Unlink(AsyncWaitNode node, int bucket)
    {
        AsyncWaitNode? current = _heads[bucket];
        AsyncWaitNode? previous = null;
        while (current != null)
        {
            if (ReferenceEquals(current, node))
            {
                if (previous == null)
                {
                    _heads[bucket] = node.Next;
                }
                else
                {
                    previous.Next = node.Next;
                }

                if (ReferenceEquals(_tails[bucket], node))
                {
                    _tails[bucket] = previous;
                }

                node.Next = null;
                return true;
            }

            previous = current;
            current = current.Next;
        }

        return false;
    }
}
