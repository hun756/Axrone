namespace Axrone.Raycast;

/// <summary>
/// Lazy hit walk over a built hierarchy: leaves report primitive contacts in
/// traversal order while internal nodes descend nearest-first. Owns raw pool
/// pointers without holding the pipeline, so enumeration must complete before
/// the owning pipeline is disposed.
/// </summary>
/// <typeparam name="TPrimitive">Primitive type stored in the hierarchy.</typeparam>
public unsafe ref struct RayHitEnumerator<TPrimitive>
    where TPrimitive : unmanaged, IPrimitiveGeometry<TPrimitive>
{
    private readonly LinearBvhNode* _nodes;
    private readonly int* _indices;
    private readonly TPrimitive* _primitives;
    private Ray _ray;
    private TraversalStack64 _stack;
    private int _stackTop;
    private int _currentLeafStart;
    private int _currentLeafCount;
    private int _currentLeafOffset;
    private PrimitiveHit _current;
    private Distance _maxDistance;

    /// <summary>Creates an enumerator; prefer <c>EnumerateHits</c> on the pipeline.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal RayHitEnumerator(
        LinearBvhNode* nodes,
        int* indices,
        TPrimitive* primitives,
        in Ray ray)
    {
        _nodes = nodes;
        _indices = indices;
        _primitives = primitives;
        _ray = ray;
        _maxDistance = ray.MaxDistance;
        _current = PrimitiveHit.None;
        _currentLeafStart = 0;
        _currentLeafCount = 0;
        _currentLeafOffset = 0;
        _stack = default;
        _stack[0] = 0;
        _stackTop = 1;
    }

    /// <summary>The contact at the current position.</summary>
    public PrimitiveHit Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    /// <summary>Returns this enumerator for <see langword="foreach"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly RayHitEnumerator<TPrimitive> GetEnumerator() => this;

    /// <summary>Advances to the next contact within the ray window, if any.</summary>
    /// <returns><see langword="true"/> while a further contact exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool MoveNext()
    {
        while (_currentLeafOffset < _currentLeafCount)
        {
            int primitiveId = _indices[_currentLeafStart + _currentLeafOffset];
            _currentLeafOffset++;

            if (TPrimitive.TryIntersect(in _ray, in _primitives[primitiveId], out PrimitiveHit hit))
            {
                if (hit.Distance <= _maxDistance)
                {
                    _current = hit;
                    return true;
                }
            }
        }

        while (_stackTop > 0)
        {
            int current = _stack[--_stackTop];
            LinearBvhNode* nodePtr = _nodes + current;

            if (!nodePtr->Intersects(in _ray, out Distance enterDistance) || enterDistance > _maxDistance)
            {
                continue;
            }

            if (nodePtr->IsLeaf)
            {
                _currentLeafStart = nodePtr->ChildOrIndex.Value;
                _currentLeafCount = nodePtr->PrimitiveCount;
                _currentLeafOffset = 0;

                while (_currentLeafOffset < _currentLeafCount)
                {
                    int primitiveId = _indices[_currentLeafStart + _currentLeafOffset];
                    _currentLeafOffset++;

                    if (TPrimitive.TryIntersect(in _ray, in _primitives[primitiveId], out PrimitiveHit hit))
                    {
                        if (hit.Distance <= _maxDistance)
                        {
                            _current = hit;
                            return true;
                        }
                    }
                }
            }
            else
            {
                int leftChild = nodePtr->ChildOrIndex.Value;
                int rightChild = leftChild + 1;

                LinearBvhNode* leftPtr = _nodes + leftChild;
                LinearBvhNode* rightPtr = _nodes + rightChild;

                bool hitLeft = leftPtr->Intersects(in _ray, out Distance leftDistance) && (leftDistance <= _maxDistance);
                bool hitRight = rightPtr->Intersects(in _ray, out Distance rightDistance) && (rightDistance <= _maxDistance);

                if (hitLeft & hitRight)
                {
                    if (leftDistance < rightDistance)
                    {
                        _stack[_stackTop++] = rightChild;
                        _stack[_stackTop++] = leftChild;
                    }
                    else
                    {
                        _stack[_stackTop++] = leftChild;
                        _stack[_stackTop++] = rightChild;
                    }
                }
                else if (hitLeft)
                {
                    _stack[_stackTop++] = leftChild;
                }
                else if (hitRight)
                {
                    _stack[_stackTop++] = rightChild;
                }
            }
        }

        return false;
    }
}
