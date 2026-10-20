namespace Axrone.Asset;

/// <summary>Fixed-capacity revision ring; oldest entries fall off.</summary>
internal sealed class RevisionHistoryRingBuffer
{
    private readonly StoredAsset?[] _buffer;
    private int _head;
    private int _count;

    /// <summary>Creates a ring with the given capacity.</summary>
    public RevisionHistoryRingBuffer(int capacity = 32)
    {
        if (capacity < 1)
        {
            ThrowHelper.ThrowConfigurationError("Revision history capacity must be positive.");
        }

        _buffer = new StoredAsset[capacity];
    }

    /// <summary>Capacity.</summary>
    public int Capacity => _buffer.Length;

    /// <summary>Pushes a superseded row.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(StoredAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        _buffer[_head] = asset;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length)
        {
            _count++;
        }
    }

    /// <summary>Finds a revision, oldest first.</summary>
    public bool TryFind(AssetRevision revision, [NotNullWhen(true)] out StoredAsset? asset)
    {
        int start = (_head - _count + (_buffer.Length * 2)) % _buffer.Length;
        for (int i = 0; i < _count; i++)
        {
            StoredAsset? candidate = _buffer[(start + i) % _buffer.Length];
            if (candidate is not null && candidate.Revision == revision)
            {
                asset = candidate;
                return true;
            }
        }

        asset = null;
        return false;
    }

    /// <summary>Copies stored revisions, oldest first.</summary>
    public StoredAsset[] ToArray()
    {
        var result = new StoredAsset[_count];
        int start = (_head - _count + (_buffer.Length * 2)) % _buffer.Length;
        for (int i = 0; i < _count; i++)
        {
            result[i] = _buffer[(start + i) % _buffer.Length]!;
        }

        return result;
    }
}
