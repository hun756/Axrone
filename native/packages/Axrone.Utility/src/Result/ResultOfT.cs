using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Enterprise.Patterns.Result;

[DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
[StructLayout(LayoutKind.Auto)]
public readonly struct Result<TValue> : IResult<TValue>, IEquatable<Result<TValue>>
{
    private readonly TValue? _value;
    private readonly ErrorCollection _errors;
    private readonly bool _isSuccess;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private Result(TValue value)
    {
        _value = value;
        _errors = default;
        _isSuccess = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private Result(in ErrorCollection errors)
    {
        _value = default;
        _errors = errors;
        _isSuccess = false;
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

    public TValue Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (!_isSuccess) ThrowInvalidOperationException();
            return _value!;
        }
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
    public static Result<TValue> Success(TValue value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure(Error error) => new(new ErrorCollection(error));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure(in ErrorCollection errors) =>
        new(errors.Count == 0 ? new ErrorCollection(Error.Uninitialized) : errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure(params Error[] errors) =>
        new(errors is null || errors.Length == 0 ? new ErrorCollection(Error.Uninitialized) : new ErrorCollection(errors));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure(List<Error> errors) =>
        new(errors is null || errors.Count == 0 ? new ErrorCollection(Error.Uninitialized) : new ErrorCollection(errors));

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetValue([NotNullWhen(true)] out TValue? value)
    {
        value = _value;
        return _isSuccess;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryUnwrap([NotNullWhen(true)] out TValue? value, out ErrorCollection errors)
    {
        if (_isSuccess)
        {
            value = _value!;
            errors = ErrorCollection.Empty;
            return true;
        }

        value = default;
        errors = Errors;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue ValueOrDefault(TValue defaultValue = default!) => _isSuccess ? _value! : defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue ValueOr(Func<ErrorCollection, TValue> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return _isSuccess ? _value! : fallbackFactory(Errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<ErrorCollection, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess(_value!) : onFailure(Errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Match(Action<TValue> onSuccess, Action<ErrorCollection> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        if (_isSuccess) onSuccess(_value!);
        else onFailure(Errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TResult> Map<TResult>(Func<TValue, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _isSuccess ? Result<TResult>.Success(mapper(_value!)) : Result<TResult>.Failure(in _errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TResult> Bind<TResult>(Func<TValue, Result<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _isSuccess ? binder(_value!) : Result<TResult>.Failure(in _errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result Bind(Func<TValue, Result> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _isSuccess ? binder(_value!) : Result.Failure(in _errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TResult> BiMap<TResult>(Func<TValue, TResult> onSuccess, Func<ErrorCollection, Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? Result<TResult>.Success(onSuccess(_value!)) : Result<TResult>.Failure(onFailure(Errors));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> Ensure(Func<TValue, bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (!_isSuccess) return this;
        return predicate(_value!) ? this : Failure(error);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> Ensure(Func<TValue, bool> predicate, Func<TValue, Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);
        if (!_isSuccess) return this;
        return predicate(_value!) ? this : Failure(errorFactory(_value!));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> Tap(Action<TValue> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_isSuccess) action(_value!);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> TapError(Action<ErrorCollection> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!_isSuccess) action(Errors);
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> OrElse(in Result<TValue> fallback) => _isSuccess ? this : fallback;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> OrElse(Func<Result<TValue>> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return _isSuccess ? this : fallbackFactory();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> OrElse(Func<ErrorCollection, Result<TValue>> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return _isSuccess ? this : fallbackFactory(Errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result<TValue>(Error error) => Failure(error);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result<TValue>(Error[] errors) => Failure(errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result<TValue>(in ErrorCollection errors) => Failure(in errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result(in Result<TValue> result) =>
        result.IsSuccess ? Result.Success() : Result.Failure(in result._errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(Result<TValue> other)
    {
        if (_isSuccess != other._isSuccess) return false;
        if (_isSuccess)
        {
            return EqualityComparer<TValue>.Default.Equals(_value, other._value);
        }
        return Errors.Equals(other.Errors);
    }

    public override bool Equals(object? obj) => obj is Result<TValue> other && Equals(other);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode()
    {
        if (_isSuccess)
        {
            return _value is null ? 1 : HashCode.Combine(true, _value);
        }
        return HashCode.Combine(false, Errors);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Result<TValue> left, Result<TValue> right) => left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Result<TValue> left, Result<TValue> right) => !left.Equals(right);

    [DoesNotReturn]
    private void ThrowInvalidOperationException() =>
        throw new InvalidOperationException($"Cannot access Value on a failed Result. Error: [{TopError.Code}] {TopError.Message}");

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private string GetDebuggerDisplay() =>
        _isSuccess ? $"Success: {_value}" : $"Failure: [{TopError.Code}] {TopError.Message}";
}
