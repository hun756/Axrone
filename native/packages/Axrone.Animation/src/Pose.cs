namespace Axrone.Animation;

/// <summary>
/// Per-bone layer mask over a bitset. A default (null-backed) mask includes everything,
/// so callers that do not mask pay nothing and cannot trip on uninitialized state.
/// </summary>
public readonly record struct AnimationMask
{
    private readonly uint[]? _bits;

    /// <summary>Bone count covered.</summary>
    public int BoneCount { get; }

    /// <summary>Creates a mask; optionally enables all bones.</summary>
    public AnimationMask(int boneCount, bool enableAll = true)
    {
        BoneCount = boneCount;
        int wordLength = (boneCount + 31) / 32;
        _bits = new uint[wordLength];
        if (enableAll)
        {
            Array.Fill(_bits, 0xFFFFFFFFu);
            int remainder = boneCount % 32;
            if (remainder > 0)
            {
                _bits[wordLength - 1] = (1u << remainder) - 1u;
            }
        }
    }

    /// <summary>Tests a bone; out-of-range reads as disabled, missing backing as enabled.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsEnabled(int boneIndex)
    {
        if (_bits is null)
        {
            return true;
        }

        if ((uint)boneIndex >= (uint)BoneCount)
        {
            return false;
        }

        return (_bits[boneIndex >> 5] & (1u << (boneIndex & 31))) != 0;
    }

    /// <summary>Flips a bone; out-of-range writes are ignored.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int boneIndex, bool enabled)
    {
        if (_bits is null)
        {
            return;
        }

        if ((uint)boneIndex >= (uint)BoneCount)
        {
            return;
        }

        int word = boneIndex >> 5;
        uint mask = 1u << (boneIndex & 31);
        if (enabled)
        {
            _bits[word] |= mask;
        }
        else
        {
            _bits[word] &= ~mask;
        }
    }
}
