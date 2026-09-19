namespace Axrone.Batching;

/// <summary>
/// Locates poison elements by halving the batch around a whole-range validator.
/// </summary>
/// <remarks>
/// The validator sees many overlapping sub-ranges of the same input, so it must be cheap relative
/// to the range length or the bisection degrades past a linear scan. The search stops when the
/// output fills; a return equal to <c>poisonOutput.Length</c> with a non-empty remainder means
/// "truncated", not "all found".
/// </remarks>
public static class NativeBisectionIsolator
{
    /// <summary>
    /// Finds indices failing <paramref name="validator"/>.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <typeparam name="TValidator">Validator type, passed by value so the call devirtualizes.</typeparam>
    /// <param name="items">Range to search.</param>
    /// <param name="poisonOutput">Receives failing indices in ascending order.</param>
    /// <param name="validator">Whole-range invariant check.</param>
    /// <returns>Indices written; zero when the whole range validates.</returns>
    public static int FindPoisonRanges<T, TValidator>(
        ReadOnlySpan<T> items, Span<int> poisonOutput, TValidator validator)
        where T : unmanaged
        where TValidator : struct, IBatchValidator<T>
    {
        if (items.IsEmpty || validator.Validate(items))
        {
            return 0;
        }

        return Bisect(items, 0, items.Length, poisonOutput, 0, validator);
    }

    private static int Bisect<T, TValidator>(
        ReadOnlySpan<T> items, int offset, int length, Span<int> poisonOutput, int found, TValidator validator)
        where T : unmanaged
        where TValidator : struct, IBatchValidator<T>
    {
        if (found >= poisonOutput.Length)
        {
            return found;
        }

        if (length == 1)
        {
            poisonOutput[found] = offset;
            return found + 1;
        }

        var half = length >> 1;
        if (!validator.Validate(items.Slice(offset, half)))
        {
            found = Bisect(items, offset, half, poisonOutput, found, validator);
        }

        if (!validator.Validate(items.Slice(offset + half, length - half)))
        {
            found = Bisect(items, offset + half, length - half, poisonOutput, found, validator);
        }

        return found;
    }
}
