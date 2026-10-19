namespace Axrone.Profiler;

/// <summary>Result helpers mapping thrown failures to coded <see cref="Error"/> values.</summary>
public static class ProfilerResults
{
    /// <summary>Runs a function, capturing failures as results.</summary>
    public static Result<T> TryExecute<T>(Func<T> func, string code = "PROFILER_ERROR")
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            return Result<T>.Success(func());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(Error.FromException(ex, code));
        }
    }

    /// <summary>Runs an action, capturing failures as results.</summary>
    public static Result<Unit> TryExecute(Action action, string code = "PROFILER_ERROR")
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
            return Result<Unit>.Success(Unit.Default);
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure(Error.FromException(ex, code));
        }
    }

    /// <summary>Runs an async function, capturing failures as results.</summary>
    public static async ValueTask<Result<T>> TryExecuteAsync<T>(Func<ValueTask<T>> func, string code = "PROFILER_ERROR")
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            return Result<T>.Success(await func().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(Error.FromException(ex, code));
        }
    }
}
