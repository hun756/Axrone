namespace Axrone.Memory.Arena;

public interface IBackoffPolicy
{
    static abstract void Step(ref int spinCount);
}
