using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Enterprise.Patterns.Result;

[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
[StructLayout(LayoutKind.Auto)]
public readonly struct Result : IResult, IEquatable<Result>
{
    private static readonly Result CachedSuccess = new(true, default);

    private readonly ErrorCollection _errors;
    private readonly bool _isSuccess;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private Result(bool isSuccess, in ErrorCollection errors)
    {
        _isSuccess = isSuccess;
        _errors = errors;
    }

    public bool IsSuccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _isSuccess;
    }

    public bool IsFailure
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => !_isSuccess;
    }

    public Error TopError
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (_isSuccess) return Error.None;
            return _errors.Count == 0 ? Error.Uninitialized : _errors.TopError;
        }
    }

    public ErrorCollection Errors
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (_isSuccess) return ErrorCollection.Empty;
            return _errors.Count == 0 ? new ErrorCollection(Error.Uninitialized) : _errors;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Success() => CachedSuccess;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Failure(Error error) => new(false, new ErrorCollection(error));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Failure(in ErrorCollection errors) =>
        new(false, errors.Count == 0 ? new ErrorCollection(Error.Uninitialized) : errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Failure(params Error[] errors) =>
        new(false, errors is null || errors.Length == 0 ? new ErrorCollection(Error.Uninitialized) : new ErrorCollection(errors));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Failure(List<Error> errors) =>
        new(false, errors is null || errors.Count == 0 ? new ErrorCollection(Error.Uninitialized) : new ErrorCollection(errors));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failure(error);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure<TValue>(in ErrorCollection errors) => Result<TValue>.Failure(in errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure<TValue>(params Error[] errors) => Result<TValue>.Failure(errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure<TValue>(List<Error> errors) => Result<TValue>.Failure(errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Create(bool condition, Error error) => condition ? Success() : Failure(error);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Create(bool condition, Func<Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return condition ? Success() : Failure(errorFactory());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Create<TValue>(bool condition, TValue value, Error error) =>
        condition ? Success(value) : Failure<TValue>(error);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Create<TValue>(bool condition, Func<TValue> valueFactory, Func<Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        ArgumentNullException.ThrowIfNull(errorFactory);
        return condition ? Success(valueFactory()) : Failure<TValue>(errorFactory());
    }

    public static Result Try(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
            return Success();
        }
        catch (Exception ex)
        {
            return Failure(Error.FromException(ex));
        }
    }

    public static Result<TValue> Try<TValue>(Func<TValue> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            return Success(func());
        }
        catch (Exception ex)
        {
            return Failure<TValue>(Error.FromException(ex));
        }
    }

    public static async Task<Result> TryAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            await action().ConfigureAwait(false);
            return Success();
        }
        catch (Exception ex)
        {
            return Failure(Error.FromException(ex));
        }
    }

    public static async ValueTask<Result> TryAsync(Func<ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            await action().ConfigureAwait(false);
            return Success();
        }
        catch (Exception ex)
        {
            return Failure(Error.FromException(ex));
        }
    }

    public static async Task<Result<TValue>> TryAsync<TValue>(Func<Task<TValue>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            TValue val = await func().ConfigureAwait(false);
            return Success(val);
        }
        catch (Exception ex)
        {
            return Failure<TValue>(Error.FromException(ex));
        }
    }

    public static async ValueTask<Result<TValue>> TryAsync<TValue>(Func<ValueTask<TValue>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            TValue val = await func().ConfigureAwait(false);
            return Success(val);
        }
        catch (Exception ex)
        {
            return Failure<TValue>(Error.FromException(ex));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Combine(scoped ReadOnlySpan<Result> results)
    {
        List<Error>? collected = null;
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i].IsFailure)
            {
                collected ??= new List<Error>(results.Length);
                var errs = results[i].Errors;
                for (int j = 0; j < errs.Count; j++)
                {
                    collected.Add(errs[j]);
                }
            }
        }

        return collected is null ? Success() : Failure(collected);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Combine(IEnumerable<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        List<Error>? collected = null;
        foreach (Result r in results)
        {
            if (r.IsFailure)
            {
                collected ??= new List<Error>();
                var errs = r.Errors;
                for (int j = 0; j < errs.Count; j++)
                {
                    collected.Add(errs[j]);
                }
            }
        }

        return collected is null ? Success() : Failure(collected);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<ErrorCollection, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess() : onFailure(Errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Match(Action onSuccess, Action<ErrorCollection> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        if (_isSuccess) onSuccess();
        else onFailure(Errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result Map(Func<Result> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _isSuccess ? mapper() : this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TResult> Map<TResult>(Func<TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _isSuccess ? Result<TResult>.Success(mapper()) : Result<TResult>.Failure(Errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TResult> Bind<TResult>(Func<Result<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _isSuccess ? binder() : Result<TResult>.Failure(Errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result Bind(Func<Result> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _isSuccess ? binder() : this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result Tap(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_isSuccess) action();
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result TapError(Action<ErrorCollection> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!_isSuccess) action(Errors);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result OrElse(Result fallback) => _isSuccess ? this : fallback;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result OrElse(Func<Result> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return _isSuccess ? this : fallbackFactory();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result(Error error) => Failure(error);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result(Error[] errors) => Failure(errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result(in ErrorCollection errors) => Failure(in errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(Result other)
    {
        if (_isSuccess != other._isSuccess) return false;
        return _isSuccess || Errors.Equals(other.Errors);
    }

    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode() => _isSuccess ? 1 : HashCode.Combine(false, Errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private string GetDebuggerDisplay() => _isSuccess ? "Success" : $"Failure: [{TopError.Code}] {TopError.Message}";
}
