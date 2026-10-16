namespace Axrone.Input;

/// <summary>
/// Per-frame consumed-control set: contexts with capture mark the controls
/// they read so lower-priority contexts skip them. Tiny linear set; frames
/// touch a handful of paths and clear every update.
/// </summary>
public sealed class ConsumedControls
{
    private InputControlPath[] _paths = new InputControlPath[16];
    private int _count;

    /// <summary>Number of consumed controls.</summary>
    public int Count => _count;

    /// <summary>Marks a control consumed.</summary>
    /// <param name="path">Canonical control address.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Mark(in InputControlPath path)
    {
        if (Contains(path))
        {
            return;
        }

        if (_count == _paths.Length)
        {
            Array.Resize(ref _paths, _paths.Length * 2);
        }

        _paths[_count++] = path;
    }

    /// <summary>Whether a control is consumed.</summary>
    /// <param name="path">Canonical control address.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(in InputControlPath path)
    {
        for (int i = 0; i < _count; i++)
        {
            if (_paths[i].Equals(path))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Clears the set for the next update.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _count = 0;
}
