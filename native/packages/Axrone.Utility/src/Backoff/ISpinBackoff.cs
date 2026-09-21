namespace Axrone.Utility.Backoff;

public interface ISpinBackoff
{
    static abstract void Initialize(out int state);
    static abstract void Advance(ref int state);
    static abstract void Reset(ref int state);
}
