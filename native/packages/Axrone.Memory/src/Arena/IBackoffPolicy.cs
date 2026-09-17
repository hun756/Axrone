namespace Axrone.Memory.Arena;

public interface IBackoffPolicy
{
    void OnSpin(int spinCount);

    void Reset();
}
