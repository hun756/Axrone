namespace Axrone.Physics2D;

using System.Runtime.CompilerServices;

/// <summary>Allocation-free duck-typed enumerator over dynamic island roots.</summary>
internal unsafe ref struct IslandRootEnumerator
{
    private readonly DisjointSetNode* _nodes;
    private readonly int _capacity;
    private int _currentSlot;

    /// <summary>Binds the enumerator to forest memory.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IslandRootEnumerator(DisjointSetNode* nodes, int capacity)
    {
        _nodes = nodes;
        _capacity = capacity;
        _currentSlot = -1;
    }

    /// <summary>Advances to the next dynamic root.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        while (++_currentSlot < _capacity)
        {
            if (_nodes[_currentSlot].Parent == _currentSlot && _nodes[_currentSlot].IsDynamic)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The current root slot.</summary>
    public readonly SlotIndex Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (SlotIndex)_currentSlot;
    }
}

/// <summary>Stack-only duck-typed enumerable over dynamic island roots.</summary>
internal readonly unsafe ref struct IslandRootsEnumerable
{
    private readonly DisjointSetNode* _nodes;
    private readonly int _capacity;

    /// <summary>Binds the enumerable to forest memory.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IslandRootsEnumerable(DisjointSetNode* nodes, int capacity)
    {
        _nodes = nodes;
        _capacity = capacity;
    }

    /// <summary>Enumerates the dynamic roots.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IslandRootEnumerator GetEnumerator() => new(_nodes, _capacity);
}

/// <summary>Allocation-free duck-typed enumerator over one island's members.</summary>
internal unsafe ref struct IslandMemberEnumerator
{
    private readonly DisjointSetNode* _nodes;
    private readonly int _capacity;
    private readonly int _root;
    private int _currentSlot;

    /// <summary>Binds the enumerator to forest memory and a root.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IslandMemberEnumerator(DisjointSetNode* nodes, int capacity, int root)
    {
        _nodes = nodes;
        _capacity = capacity;
        _root = root;
        _currentSlot = -1;
    }

    /// <summary>Advances to the next member of the root.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        while (++_currentSlot < _capacity)
        {
            if (_nodes[_currentSlot].Parent >= 0 &&
                NativePartitionBuffer.FindRoot(_nodes, _currentSlot) == _root)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The current member slot.</summary>
    public readonly SlotIndex Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (SlotIndex)_currentSlot;
    }
}

/// <summary>Stack-only duck-typed enumerable over one island's members.</summary>
internal readonly unsafe ref struct IslandMembersEnumerable
{
    private readonly DisjointSetNode* _nodes;
    private readonly int _capacity;
    private readonly int _root;

    /// <summary>Binds the enumerable to forest memory and a root.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IslandMembersEnumerable(DisjointSetNode* nodes, int capacity, int root)
    {
        _nodes = nodes;
        _capacity = capacity;
        _root = root;
    }

    /// <summary>Enumerates the members of the root.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IslandMemberEnumerator GetEnumerator() => new(_nodes, _capacity, _root);
}
