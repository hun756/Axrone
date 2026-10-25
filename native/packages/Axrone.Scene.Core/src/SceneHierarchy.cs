namespace Axrone.Scene;

/// <summary>
/// Callback invoked during hierarchy traversal. The visitor is a generic type
/// parameter at the call site, so traversal closes over the callback type
/// rather than a delegate instance. <typeparamref name="TContext"/> may be a
/// <see langword="ref"/> struct, which lets callers accumulate into stack-only
/// state without boxing.
/// </summary>
/// <typeparam name="TContext">Traversal state.</typeparam>
public interface ISceneHierarchyVisitor<TContext>
    where TContext : allows ref struct
{
    /// <summary>Visits one actor.</summary>
    /// <param name="actor">Visited actor.</param>
    /// <param name="depth">Hierarchy depth of the actor.</param>
    /// <param name="context">Traversal state.</param>
    /// <returns><see langword="true"/> to continue; <see langword="false"/> to stop early.</returns>
    bool Visit(SceneActor actor, int depth, ref TContext context);
}

/// <summary>Receives structural hierarchy notifications from <see cref="SceneHierarchy"/>.</summary>
internal interface ISceneHierarchyObserver
{
    void BeforeStructuralChange(string operation);

    void OnParentChanged(in SceneActor child, in SceneActor oldParent, in SceneActor newParent);

    void OnActiveInHierarchyChanged(in SceneActor actor, bool active);
}

/// <summary>
/// Allocation-free view over the actor hierarchy: parent/child links, depth
/// and active-in-hierarchy propagation. Structural methods are expected to run
/// outside frame iteration.
/// </summary>
public sealed class SceneHierarchy
{
    private readonly SceneActorTable _actors;
    private readonly ISceneHierarchyObserver? _observer;

    internal SceneHierarchy(SceneActorTable actors, ISceneHierarchyObserver? observer)
    {
        ArgumentNullException.ThrowIfNull(actors);
        _actors = actors;
        _observer = observer;
    }

    /// <summary>Parent of an actor, or <see cref="SceneActor.Null"/> for a root.</summary>
    public SceneActor ParentOf(SceneActor actor)
    {
        int slot = _actors.Resolve(actor);
        int parent = _actors.RecordAt(slot).Parent;
        return parent < 0 ? SceneActor.Null : SceneActor.FromEntity(_actors.RecordAt(parent).Entity);
    }

    /// <summary>Whether the actor has a parent.</summary>
    public bool HasParent(SceneActor actor) => _actors.RecordAt(_actors.Resolve(actor)).Parent >= 0;

    /// <summary>Root ancestor of an actor, including the actor itself when it has no parent.</summary>
    public SceneActor RootOf(SceneActor actor)
    {
        int slot = _actors.Resolve(actor);
        while (true)
        {
            int parent = _actors.RecordAt(slot).Parent;
            if (parent < 0)
            {
                return SceneActor.FromEntity(_actors.RecordAt(slot).Entity);
            }

            slot = parent;
        }
    }

    /// <summary>Zero-based depth of an actor; roots are depth zero.</summary>
    public int DepthOf(SceneActor actor) => _actors.RecordAt(_actors.Resolve(actor)).Meta.Depth;

    /// <summary>Number of direct children.</summary>
    public int ChildCount(SceneActor actor)
    {
        int slot = _actors.Resolve(actor);
        int count = 0;
        for (int child = _actors.RecordAt(slot).FirstChild; child >= 0; child = _actors.RecordAt(child).NextSibling)
        {
            count++;
        }

        return count;
    }

    /// <summary>Whether the actor has at least one child.</summary>
    public bool HasChildren(SceneActor actor) => _actors.RecordAt(_actors.Resolve(actor)).FirstChild >= 0;

    /// <summary>Whether <paramref name="ancestor"/> is on the parent chain of <paramref name="descendant"/>.</summary>
    public bool IsAncestorOf(SceneActor ancestor, SceneActor descendant)
    {
        int ancestorSlot = _actors.Resolve(ancestor);
        int current = _actors.Resolve(descendant);

        while (current >= 0)
        {
            if (current == ancestorSlot)
            {
                return true;
            }

            current = _actors.RecordAt(current).Parent;
        }

        return false;
    }

    /// <summary>Whether <paramref name="descendant"/> is below <paramref name="ancestor"/>.</summary>
    public bool IsDescendantOf(SceneActor descendant, SceneActor ancestor) =>
        IsAncestorOf(ancestor, descendant);

    /// <summary>Attaches <paramref name="child"/> under <paramref name="parent"/>.</summary>
    /// <param name="child">Actor to reparent.</param>
    /// <param name="parent">New parent, or <see cref="SceneActor.Null"/> to detach.</param>
    /// <exception cref="SceneValidationException">The operation would create a cycle or exceed the depth budget.</exception>
    public void SetParent(SceneActor child, SceneActor parent)
    {
        _observer?.BeforeStructuralChange(nameof(SetParent));

        int childSlot = _actors.Resolve(child);
        int parentSlot = parent.IsValid ? _actors.Resolve(parent) : -1;

        if (parentSlot == childSlot)
        {
            throw new SceneValidationException(
                SceneErrorCodes.ValidationInvalid,
                $"{child} cannot be parented to itself.");
        }

        if (parentSlot >= 0 && IsAncestorOf(child, parent))
        {
            throw new SceneValidationException(
                SceneErrorCodes.ValidationInvalid,
                $"Parenting {child} under {parent} would create a cycle.");
        }

        ref SceneActorRecord childRecord = ref _actors.RecordAt(childSlot);
        int oldParentSlot = childRecord.Parent;
        if (oldParentSlot == parentSlot)
        {
            return;
        }

        int parentDepth = parentSlot < 0 ? -1 : _actors.RecordAt(parentSlot).Meta.Depth;
        if (parentDepth + 1 + SubtreeHeight(childSlot) > byte.MaxValue)
        {
            throw new SceneValidationException(
                SceneErrorCodes.ValidationInvalid,
                $"Parenting {child} under {parent} would exceed the maximum hierarchy depth of {byte.MaxValue}.");
        }

        SceneActor oldParent = oldParentSlot < 0
            ? SceneActor.Null
            : SceneActor.FromEntity(_actors.RecordAt(oldParentSlot).Entity);

        if (oldParentSlot >= 0)
        {
            DetachFromParent(childSlot, oldParentSlot);
        }

        if (parentSlot >= 0)
        {
            AttachToParent(childSlot, parentSlot);
        }

        bool parentActive = parentSlot < 0 ||
            (_actors.RecordAt(parentSlot).Meta.Flags & SceneActorFlags.ActiveInHierarchy) != 0;
        UpdateSubtreeState(childSlot, parentActive, parentDepth + 1);

        BumpVersion(childSlot);
        if (oldParentSlot >= 0)
        {
            BumpVersion(oldParentSlot);
        }

        if (parentSlot >= 0)
        {
            BumpVersion(parentSlot);
        }

        if (oldParent != parent)
        {
            _observer?.OnParentChanged(in child, in oldParent, in parent);
        }
    }

    /// <summary>Sets the local active flag and propagates active-in-hierarchy to descendants.</summary>
    public void SetActiveSelf(SceneActor actor, bool active)
    {
        _observer?.BeforeStructuralChange(nameof(SetActiveSelf));

        int slot = _actors.Resolve(actor);
        ref SceneActorRecord record = ref _actors.RecordAt(slot);
        bool wasActive = (record.Meta.Flags & SceneActorFlags.ActiveSelf) != 0;
        if (wasActive == active)
        {
            return;
        }

        record.Meta.Flags = active
            ? record.Meta.Flags | SceneActorFlags.ActiveSelf
            : record.Meta.Flags & ~SceneActorFlags.ActiveSelf;

        bool parentActive = record.Parent < 0 ||
            (_actors.RecordAt(record.Parent).Meta.Flags & SceneActorFlags.ActiveInHierarchy) != 0;
        UpdateSubtreeState(slot, parentActive, record.Meta.Depth);
        BumpVersion(slot);
    }

    /// <summary>Whether the actor is active locally.</summary>
    public bool IsActiveSelf(SceneActor actor) =>
        (_actors.RecordAt(_actors.Resolve(actor)).Meta.Flags & SceneActorFlags.ActiveSelf) != 0;

    /// <summary>Whether the actor is active after its ancestor chain is applied.</summary>
    public bool IsActiveInHierarchy(SceneActor actor) =>
        (_actors.RecordAt(_actors.Resolve(actor)).Meta.Flags & SceneActorFlags.ActiveInHierarchy) != 0;

    /// <summary>Enumerates direct children in sibling order.</summary>
    public SceneChildEnumerable Children(SceneActor actor)
    {
        int slot = _actors.Resolve(actor);
        return new SceneChildEnumerable(_actors, _actors.RecordAt(slot).FirstChild);
    }

    /// <summary>Enumerates ancestors from nearest parent to root.</summary>
    public SceneAncestorEnumerable Ancestors(SceneActor actor)
    {
        int slot = _actors.Resolve(actor);
        return new SceneAncestorEnumerable(_actors, _actors.RecordAt(slot).Parent);
    }

    /// <summary>Visits direct children in sibling order.</summary>
    public bool VisitChildren<TVisitor, TContext>(SceneActor actor, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, ISceneHierarchyVisitor<TContext>
        where TContext : allows ref struct
    {
        int slot = _actors.Resolve(actor);
        int child = _actors.RecordAt(slot).FirstChild;
        while (child >= 0)
        {
            ref SceneActorRecord record = ref _actors.RecordAt(child);
            int next = record.NextSibling;
            if (!visitor.Visit(SceneActor.FromEntity(record.Entity), record.Meta.Depth, ref context))
            {
                return false;
            }

            child = next;
        }

        return true;
    }

    /// <summary>Visits ancestors from nearest parent to root.</summary>
    public bool VisitAncestors<TVisitor, TContext>(SceneActor actor, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, ISceneHierarchyVisitor<TContext>
        where TContext : allows ref struct
    {
        int current = _actors.RecordAt(_actors.Resolve(actor)).Parent;
        while (current >= 0)
        {
            ref SceneActorRecord record = ref _actors.RecordAt(current);
            int next = record.Parent;
            if (!visitor.Visit(SceneActor.FromEntity(record.Entity), record.Meta.Depth, ref context))
            {
                return false;
            }

            current = next;
        }

        return true;
    }

    /// <summary>Visits descendants in pre-order using parent links only; no stack allocation.</summary>
    public bool VisitDescendants<TVisitor, TContext>(SceneActor actor, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, ISceneHierarchyVisitor<TContext>
        where TContext : allows ref struct
    {
        int start = _actors.Resolve(actor);
        int current = start;

        while (true)
        {
            int child = _actors.RecordAt(current).FirstChild;
            if (child >= 0)
            {
                current = child;
                ref SceneActorRecord childRecord = ref _actors.RecordAt(current);
                if (!visitor.Visit(SceneActor.FromEntity(childRecord.Entity), childRecord.Meta.Depth, ref context))
                {
                    return false;
                }

                continue;
            }

            while (current != start && _actors.RecordAt(current).NextSibling < 0)
            {
                current = _actors.RecordAt(current).Parent;
            }

            if (current == start)
            {
                return true;
            }

            current = _actors.RecordAt(current).NextSibling;
            ref SceneActorRecord nextRecord = ref _actors.RecordAt(current);
            if (!visitor.Visit(SceneActor.FromEntity(nextRecord.Entity), nextRecord.Meta.Depth, ref context))
            {
                return false;
            }
        }
    }

    /// <summary>Visits the actor and every descendant in pre-order.</summary>
    public bool VisitSubtree<TVisitor, TContext>(SceneActor actor, ref TVisitor visitor, ref TContext context)
        where TVisitor : struct, ISceneHierarchyVisitor<TContext>
        where TContext : allows ref struct
    {
        int slot = _actors.Resolve(actor);
        ref SceneActorRecord record = ref _actors.RecordAt(slot);
        if (!visitor.Visit(SceneActor.FromEntity(record.Entity), record.Meta.Depth, ref context))
        {
            return false;
        }

        return VisitDescendants(actor, ref visitor, ref context);
    }

    private int SubtreeHeight(int slot)
    {
        int height = 0;
        int child = _actors.RecordAt(slot).FirstChild;
        while (child >= 0)
        {
            int childHeight = 1 + SubtreeHeight(child);
            if (childHeight > height)
            {
                height = childHeight;
            }

            child = _actors.RecordAt(child).NextSibling;
        }

        return height;
    }

    private void DetachFromParent(int childSlot, int parentSlot)
    {
        ref SceneActorRecord child = ref _actors.RecordAt(childSlot);
        ref SceneActorRecord parent = ref _actors.RecordAt(parentSlot);
        int prev = child.PrevSibling;
        int next = child.NextSibling;

        if (prev < 0)
        {
            parent.FirstChild = next;
        }
        else
        {
            _actors.RecordAt(prev).NextSibling = next;
        }

        if (next >= 0)
        {
            _actors.RecordAt(next).PrevSibling = prev;
        }

        child.Parent = -1;
        child.PrevSibling = -1;
        child.NextSibling = -1;
    }

    private void AttachToParent(int childSlot, int parentSlot)
    {
        ref SceneActorRecord child = ref _actors.RecordAt(childSlot);
        ref SceneActorRecord parent = ref _actors.RecordAt(parentSlot);
        int first = parent.FirstChild;

        child.Parent = parentSlot;
        child.PrevSibling = -1;
        child.NextSibling = first;

        if (first >= 0)
        {
            _actors.RecordAt(first).PrevSibling = childSlot;
        }

        parent.FirstChild = childSlot;
    }

    private void UpdateSubtreeState(int slot, bool parentActive, int depth)
    {
        if (depth > byte.MaxValue)
        {
            throw new SceneValidationException(
                SceneErrorCodes.ValidationInvalid,
                $"Hierarchy depth {depth} exceeds the maximum of {byte.MaxValue}.");
        }

        ref SceneActorRecord record = ref _actors.RecordAt(slot);
        record.Meta.Depth = (byte)depth;
        bool active = parentActive && (record.Meta.Flags & SceneActorFlags.ActiveSelf) != 0;
        bool wasActive = (record.Meta.Flags & SceneActorFlags.ActiveInHierarchy) != 0;

        record.Meta.Flags = active
            ? record.Meta.Flags | SceneActorFlags.ActiveInHierarchy
            : record.Meta.Flags & ~SceneActorFlags.ActiveInHierarchy;

        if (wasActive != active)
        {
            BumpVersion(slot);
            SceneActor actor = SceneActor.FromEntity(record.Entity);
            _observer?.OnActiveInHierarchyChanged(in actor, active);
        }

        int child = record.FirstChild;
        while (child >= 0)
        {
            int next = _actors.RecordAt(child).NextSibling;
            UpdateSubtreeState(child, active, depth + 1);
            child = next;
        }
    }

    private void BumpVersion(int slot) => _actors.RecordAt(slot).HierarchyVersion++;
}

/// <summary>Allocation-free enumerable over direct children.</summary>
public readonly ref struct SceneChildEnumerable
{
    private readonly SceneActorTable _actors;
    private readonly int _firstChild;

    internal SceneChildEnumerable(SceneActorTable actors, int firstChild)
    {
        _actors = actors;
        _firstChild = firstChild;
    }

    /// <summary>Creates a child enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SceneChildEnumerator GetEnumerator() => new(_actors, _firstChild);
}

/// <summary>Allocation-free enumerator over direct children.</summary>
public ref struct SceneChildEnumerator
{
    private readonly SceneActorTable _actors;
    private int _next;
    private SceneActor _current;

    internal SceneChildEnumerator(SceneActorTable actors, int firstChild)
    {
        _actors = actors;
        _next = firstChild;
        _current = SceneActor.Null;
    }

    /// <summary>Current child.</summary>
    public SceneActor Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    /// <summary>Advances to the next child.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_next < 0)
        {
            return false;
        }

        ref SceneActorRecord record = ref _actors.RecordAt(_next);
        if ((record.Meta.Flags & SceneActorFlags.IsFree) != 0)
        {
            _next = -1;
            return false;
        }

        _current = SceneActor.FromEntity(record.Entity);
        _next = record.NextSibling;
        return true;
    }
}

/// <summary>Allocation-free enumerable over ancestors.</summary>
public readonly ref struct SceneAncestorEnumerable
{
    private readonly SceneActorTable _actors;
    private readonly int _parent;

    internal SceneAncestorEnumerable(SceneActorTable actors, int parent)
    {
        _actors = actors;
        _parent = parent;
    }

    /// <summary>Creates an ancestor enumerator.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SceneAncestorEnumerator GetEnumerator() => new(_actors, _parent);
}

/// <summary>Allocation-free enumerator over ancestors.</summary>
public ref struct SceneAncestorEnumerator
{
    private readonly SceneActorTable _actors;
    private int _next;
    private SceneActor _current;

    internal SceneAncestorEnumerator(SceneActorTable actors, int parent)
    {
        _actors = actors;
        _next = parent;
        _current = SceneActor.Null;
    }

    /// <summary>Current ancestor.</summary>
    public SceneActor Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    /// <summary>Advances to the next ancestor.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (_next < 0)
        {
            return false;
        }

        ref SceneActorRecord record = ref _actors.RecordAt(_next);
        if ((record.Meta.Flags & SceneActorFlags.IsFree) != 0)
        {
            _next = -1;
            return false;
        }

        _current = SceneActor.FromEntity(record.Entity);
        _next = record.Parent;
        return true;
    }
}
