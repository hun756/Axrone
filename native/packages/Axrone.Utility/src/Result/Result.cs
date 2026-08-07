namespace Axrone.Utility.Result;

/// <summary>
/// Represents the outcome of an operation that can either succeed with a value of type <typeparamref name="T"/>
/// or fail with an error of type <typeparamref name="TErr"/>.
/// </summary>
/// <remarks>
/// <para>Thread-safety: instances are immutable and safe for concurrent reads.</para>
/// </remarks>
public abstract class Result<T, TErr> : IEquatable<Result<T, TErr>>
{
    internal Result() { }

    public abstract bool IsOk { get; }
    public bool IsErr => !IsOk;

    public abstract TResult Fold<TResult>(Func<T, TResult> onOk, Func<TErr, TResult> onErr);
    public abstract T Unwrap();
    public abstract T UnwrapOr(T defaultValue);
    public abstract T UnwrapOrElse(Func<TErr, T> onError);

    public T Expect(string message)
    {
        if (IsOk) return Unwrap();
        throw new ResultException(message) { InnerError = UnwrapErrAsObject() };
    }

    public abstract Result<TResult, TErr> Map<TResult>(Func<T, TResult> fn);
    public abstract Result<T, TResult> MapErr<TResult>(Func<TErr, TResult> fn);
    public abstract Result<TResult, TErr> AndThen<TResult>(Func<T, Result<TResult, TErr>> fn);
    public abstract Result<T, TResult> OrElse<TResult>(Func<TErr, Result<T, TResult>> fn);
    public abstract Result<T, TErr> Tap(Action<T> action);
    public abstract Result<T, TErr> TapErr(Action<TErr> action);

    internal abstract object UnwrapErrAsObject();

    public abstract T? ToNullable();
    public abstract (bool Success, T? Value) ToTuple();

    public abstract bool Equals(Result<T, TErr>? other);
    public abstract override int GetHashCode();
    public abstract override string ToString();
}

public sealed class Ok<T, TErr> : Result<T, TErr>
{
    private readonly T _value;

    internal Ok(T value) => _value = value;

    public override bool IsOk => true;

    public override TResult Fold<TResult>(Func<T, TResult> onOk, Func<TErr, TResult> onErr) => onOk(_value);
    public override T Unwrap() => _value;
    public override T UnwrapOr(T defaultValue) => _value;
    public override T UnwrapOrElse(Func<TErr, T> onError) => _value;

    public override Result<TResult, TErr> Map<TResult>(Func<T, TResult> fn) => new Ok<TResult, TErr>(fn(_value));
    public override Result<T, TResult> MapErr<TResult>(Func<TErr, TResult> fn) => new Ok<T, TResult>(_value);
    public override Result<TResult, TErr> AndThen<TResult>(Func<T, Result<TResult, TErr>> fn) => fn(_value);
    public override Result<T, TResult> OrElse<TResult>(Func<TErr, Result<T, TResult>> fn) => new Ok<T, TResult>(_value);

    public override Result<T, TErr> Tap(Action<T> action)
    {
        action(_value);
        return this;
    }

    public override Result<T, TErr> TapErr(Action<TErr> action) => this;

    internal override object UnwrapErrAsObject() => _value!;

    public override T? ToNullable() => _value;
    public override (bool Success, T? Value) ToTuple() => (true, _value);

    public override bool Equals(Result<T, TErr>? other) =>
        other is Ok<T, TErr> ok && EqualityComparer<T>.Default.Equals(_value, ok._value);

    public override int GetHashCode() => HashCode.Combine(true, _value);
    public override string ToString() => $"Ok({_value})";
}

public sealed class Err<T, TErr> : Result<T, TErr>
{
    private readonly TErr _error;

    internal Err(TErr error) => _error = error;

    public override bool IsOk => false;

    public override TResult Fold<TResult>(Func<T, TResult> onOk, Func<TErr, TResult> onErr) => onErr(_error);

    public override T Unwrap() =>
        throw new ResultException($"Attempted to unwrap Err result: {_error}") { InnerError = _error };

    public override T UnwrapOr(T defaultValue) => defaultValue;
    public override T UnwrapOrElse(Func<TErr, T> onError) => onError(_error);

    public override Result<TResult, TErr> Map<TResult>(Func<T, TResult> fn) => new Err<TResult, TErr>(_error);
    public override Result<T, TResult> MapErr<TResult>(Func<TErr, TResult> fn) => new Err<T, TResult>(fn(_error));
    public override Result<TResult, TErr> AndThen<TResult>(Func<T, Result<TResult, TErr>> fn) => new Err<TResult, TErr>(_error);
    public override Result<T, TResult> OrElse<TResult>(Func<TErr, Result<T, TResult>> fn) => fn(_error);

    public override Result<T, TErr> Tap(Action<T> action) => this;

    public override Result<T, TErr> TapErr(Action<TErr> action)
    {
        action(_error);
        return this;
    }

    internal override object UnwrapErrAsObject() => _error!;

    public override T? ToNullable() => default;
    public override (bool Success, T? Value) ToTuple() => (false, default);

    public override bool Equals(Result<T, TErr>? other) =>
        other is Err<T, TErr> err && EqualityComparer<TErr>.Default.Equals(_error, err._error);

    public override int GetHashCode() => HashCode.Combine(false, _error);
    public override string ToString() => $"Err({_error})";
}

public sealed class ResultException : Exception
{
    public object? InnerError { get; init; }

    public ResultException() { }
    public ResultException(string message) : base(message) { }
    public ResultException(string message, Exception inner) : base(message, inner) { }
}

public static class ResultModule
{
    public static Result<T, TErr> Ok<T, TErr>(T value) => new Ok<T, TErr>(value);
    public static Result<T, TErr> Err<T, TErr>(TErr error) => new Err<T, TErr>(error);

    public static Result<T, Exception> Try<T>(Func<T> fn)
    {
        ArgumentNullException.ThrowIfNull(fn);
        try
        {
            return new Ok<T, Exception>(fn());
        }
        catch (Exception ex)
        {
            return new Err<T, Exception>(ex);
        }
    }

    public static async ValueTask<Result<T, Exception>> TryAsync<T>(Func<ValueTask<T>> fn)
    {
        ArgumentNullException.ThrowIfNull(fn);
        try
        {
            return new Ok<T, Exception>(await fn().ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            return new Err<T, Exception>(ex);
        }
    }

    public static Result<T, TErr> FromNullable<T, TErr>(T? value, Func<TErr> onNull) where T : class
    {
        ArgumentNullException.ThrowIfNull(onNull);
        if (value is not null) return new Ok<T, TErr>(value);
        return new Err<T, TErr>(onNull());
    }

    public static Result<T[], TErr> All<T, TErr>(ReadOnlySpan<Result<T, TErr>> results)
    {
        var values = new T[results.Length];
        for (var i = 0; i < results.Length; i++)
        {
            if (results[i] is Err<T, TErr> err)
                return ResultModule.Err<T[], TErr>(err.Fold(static _ => default!, static e => e));
            values[i] = results[i].Unwrap();
        }
        return new Ok<T[], TErr>(values);
    }

    public static (T[] OkValues, TErr[] ErrValues) Partition<T, TErr>(ReadOnlySpan<Result<T, TErr>> results)
    {
        var okCount = 0;
        for (var i = 0; i < results.Length; i++)
            if (results[i].IsOk) okCount++;

        var okValues = new T[okCount];
        var errValues = new TErr[results.Length - okCount];
        var oi = 0;
        var ei = 0;

        for (var i = 0; i < results.Length; i++)
        {
            if (results[i] is Ok<T, TErr> ok)
                okValues[oi++] = ok.Unwrap();
            else
                errValues[ei++] = results[i].Fold(static _ => default!, static e => e);
        }

        return (okValues, errValues);
    }
}
