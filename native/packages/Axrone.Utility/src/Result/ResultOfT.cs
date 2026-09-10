using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Enterprise.Patterns.Result;

/// <summary>Represents the outcome of an operation that either succeeded with a value of type <typeparamref name="TValue"/> or failed with error information.</summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
/// <remarks>Default-initialized instances of this struct are treated as failures with an uninitialized error state.</remarks>
[DebuggerDisplay("IsSuccess = {_isSuccess}")]
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

    /// <summary>Gets a value indicating whether this result represents a successful operation.</summary>
    public bool IsSuccess
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => _isSuccess;
    }

    /// <summary>Gets a value indicating whether this result represents a failed operation.</summary>
    public bool IsFailure
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get => !_isSuccess;
    }

    /// <summary>Gets the success value.</summary>
    /// <exception cref="InvalidOperationException">Thrown when this result is a failure.</exception>
    public TValue Value
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (!_isSuccess) ThrowInvalidOperationException();
            return _value!;
        }
    }

    /// <summary>Gets the top-level error from this failed result, or <see cref="Error.None"/> if successful.</summary>
    /// <remarks>Returns <see cref="Error.Uninitialized"/> when the result is a failure but no errors were recorded.</remarks>
    public Error TopError
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (_isSuccess) return Error.None;
            return _errors.Count == 0 ? Error.Uninitialized : _errors.TopError;
        }
    }

    /// <summary>Gets the collection of errors from this failed result, or <see cref="ErrorCollection.Empty"/> if successful.</summary>
    /// <remarks>Returns a collection containing <see cref="Error.Uninitialized"/> when the result is a failure but no errors were recorded.</remarks>
    public ErrorCollection Errors
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        get
        {
            if (_isSuccess) return ErrorCollection.Empty;
            return _errors.Count == 0 ? new ErrorCollection(Error.Uninitialized) : _errors;
        }
    }

    /// <summary>Creates a successful result with the specified value.</summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful <see cref="Result{TValue}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Success(TValue value) => new(value);

    /// <summary>Creates a failed result from a single error.</summary>
    /// <param name="error">The error that caused the failure.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure(Error error) => new(new ErrorCollection(error));

    /// <summary>Creates a failed result from an error collection.</summary>
    /// <param name="errors">The collection of errors that caused the failure.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure(in ErrorCollection errors) =>
        new(NormalizeErrors(in errors));

    /// <summary>Creates a failed result from one or more errors.</summary>
    /// <param name="errors">The errors that caused the failure.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure(params Error[] errors) =>
        new(errors is null || errors.Length == 0 ? new ErrorCollection(Error.Uninitialized) : new ErrorCollection(errors));

    /// <summary>Creates a failed result from a list of errors.</summary>
    /// <param name="errors">The list of errors that caused the failure.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure(List<Error> errors) =>
        new(errors is null || errors.Count == 0 ? new ErrorCollection(Error.Uninitialized) : new ErrorCollection(errors));

    /// <summary>Attempts to extract the success value from this result.</summary>
    /// <param name="value">When this method returns, contains the success value if successful; otherwise, the default value.</param>
    /// <returns><c>true</c> if this result is successful; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool TryGetValue([NotNullWhen(true)] out TValue? value)
    {
        value = _value;
        return _isSuccess;
    }

    /// <summary>Attempts to extract both the success value and the error collection from this result.</summary>
    /// <param name="value">When this method returns, contains the success value if successful; otherwise, the default value.</param>
    /// <param name="errors">When this method returns, contains the error collection if failed; otherwise, <see cref="ErrorCollection.Empty"/>.</param>
    /// <returns><c>true</c> if this result is successful; otherwise, <c>false</c>.</returns>
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

    /// <summary>Returns the success value if this result is successful; otherwise, returns the specified default value.</summary>
    /// <param name="defaultValue">The default value to return on failure. Defaults to <c>default(TValue)</c>.</param>
    /// <returns>The success value or <paramref name="defaultValue"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue ValueOrDefault(TValue defaultValue = default!) => _isSuccess ? _value! : defaultValue;

    /// <summary>Returns the success value if this result is successful; otherwise, invokes the factory to produce a fallback value.</summary>
    /// <param name="fallbackFactory">A function that receives the error collection and produces a fallback value.</param>
    /// <returns>The success value or the result of <paramref name="fallbackFactory"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fallbackFactory"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TValue ValueOr(Func<ErrorCollection, TValue> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return _isSuccess ? _value! : fallbackFactory(Errors);
    }

    /// <summary>Matches this result to one of two functions, returning the result of the matching branch.</summary>
    /// <typeparam name="TResult">The return type of both branch functions.</typeparam>
    /// <param name="onSuccess">The function to invoke with the success value when this result is successful.</param>
    /// <param name="onFailure">The function to invoke with the error collection when this result is a failure.</param>
    /// <returns>The result of the invoked function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onSuccess"/> or <paramref name="onFailure"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<ErrorCollection, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess(_value!) : onFailure(Errors);
    }

    /// <summary>Matches this result to one of two actions, executing the matching branch.</summary>
    /// <param name="onSuccess">The action to invoke with the success value when this result is successful.</param>
    /// <param name="onFailure">The action to invoke with the error collection when this result is a failure.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onSuccess"/> or <paramref name="onFailure"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Match(Action<TValue> onSuccess, Action<ErrorCollection> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        if (_isSuccess) onSuccess(_value!);
        else onFailure(Errors);
    }

    /// <summary>Maps the success value to a new typed result using the specified mapper function.</summary>
    /// <typeparam name="TResult">The type of the success value in the mapped result.</typeparam>
    /// <param name="mapper">The function to apply to the success value.</param>
    /// <returns>A new <see cref="Result{TResult}"/> with the mapped value if successful; otherwise, a failure result with the same errors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapper"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TResult> Map<TResult>(Func<TValue, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _isSuccess ? Result<TResult>.Success(mapper(_value!)) : Result<TResult>.Failure(in _errors);
    }

    /// <summary>Chains this result with a binder function that receives the success value and returns a new typed result.</summary>
    /// <typeparam name="TResult">The type of the success value in the bound result.</typeparam>
    /// <param name="binder">The function to apply to the success value.</param>
    /// <returns>The bound result if successful; otherwise, a failure result with the same errors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TResult> Bind<TResult>(Func<TValue, Result<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _isSuccess ? binder(_value!) : Result<TResult>.Failure(in _errors);
    }

    /// <summary>Chains this result with a binder function that receives the success value and returns a non-generic result.</summary>
    /// <param name="binder">The function to apply to the success value.</param>
    /// <returns>The bound result if successful; otherwise, a failure <see cref="Result"/> with the same errors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result Bind(Func<TValue, Result> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _isSuccess ? binder(_value!) : Result.Failure(in _errors);
    }

    /// <summary>Maps both the success value and the error collection to produce a new typed result.</summary>
    /// <typeparam name="TResult">The type of the success value in the mapped result.</typeparam>
    /// <param name="onSuccess">The function to apply to the success value on success.</param>
    /// <param name="onFailure">The function to apply to the error collection on failure, producing a new error.</param>
    /// <returns>A new <see cref="Result{TResult}"/> with the mapped value or the transformed error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onSuccess"/> or <paramref name="onFailure"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TResult> BiMap<TResult>(Func<TValue, TResult> onSuccess, Func<ErrorCollection, Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? Result<TResult>.Success(onSuccess(_value!)) : Result<TResult>.Failure(onFailure(Errors));
    }

    /// <summary>Ensures the success value satisfies a predicate; otherwise, returns a failure with the specified error.</summary>
    /// <param name="predicate">The predicate to evaluate against the success value.</param>
    /// <param name="error">The error to use when the predicate returns false.</param>
    /// <returns>This result if the predicate passes or if already a failure; otherwise, a new failure result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> Ensure(Func<TValue, bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (!_isSuccess) return this;
        return predicate(_value!) ? this : Failure(error);
    }

    /// <summary>Ensures the success value satisfies a predicate; otherwise, returns a failure with an error produced by the factory.</summary>
    /// <param name="predicate">The predicate to evaluate against the success value.</param>
    /// <param name="errorFactory">A function that produces the error from the success value when the predicate returns false.</param>
    /// <returns>This result if the predicate passes or if already a failure; otherwise, a new failure result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> or <paramref name="errorFactory"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> Ensure(Func<TValue, bool> predicate, Func<TValue, Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);
        if (!_isSuccess) return this;
        return predicate(_value!) ? this : Failure(errorFactory(_value!));
    }

    /// <summary>Executes an action with the success value if this result is successful and returns this result unchanged.</summary>
    /// <param name="action">The action to execute with the success value.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> Tap(Action<TValue> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_isSuccess) action(_value!);
        return this;
    }

    /// <summary>Executes an action with the error collection if this result is a failure and returns this result unchanged.</summary>
    /// <param name="action">The action to execute with the error collection on failure.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> TapError(Action<ErrorCollection> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!_isSuccess) action(Errors);
        return this;
    }

    /// <summary>Returns this result if successful; otherwise, returns the specified fallback result.</summary>
    /// <param name="fallback">The fallback result to return on failure.</param>
    /// <returns>This result if successful; otherwise, <paramref name="fallback"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> OrElse(in Result<TValue> fallback) => _isSuccess ? this : fallback;

    /// <summary>Returns this result if successful; otherwise, invokes the factory to produce a fallback result.</summary>
    /// <param name="fallbackFactory">A factory function that produces the fallback result.</param>
    /// <returns>This result if successful; otherwise, the result of <paramref name="fallbackFactory"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fallbackFactory"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> OrElse(Func<Result<TValue>> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return _isSuccess ? this : fallbackFactory();
    }

    /// <summary>Returns this result if successful; otherwise, invokes the factory with the error collection to produce a fallback result.</summary>
    /// <param name="fallbackFactory">A factory function that receives the error collection and produces the fallback result.</param>
    /// <returns>This result if successful; otherwise, the result of <paramref name="fallbackFactory"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fallbackFactory"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TValue> OrElse(Func<ErrorCollection, Result<TValue>> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return _isSuccess ? this : fallbackFactory(Errors);
    }

    /// <summary>Implicitly converts a value to a successful <see cref="Result{TValue}"/>.</summary>
    /// <param name="value">The value to wrap.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Implicitly converts an <see cref="Error"/> to a failed <see cref="Result{TValue}"/>.</summary>
    /// <param name="error">The error to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result<TValue>(Error error) => Failure(error);

    /// <summary>Implicitly converts an array of errors to a failed <see cref="Result{TValue}"/>.</summary>
    /// <param name="errors">The error array to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result<TValue>(Error[] errors) => Failure(errors);

    /// <summary>Implicitly converts an <see cref="ErrorCollection"/> to a failed <see cref="Result{TValue}"/>.</summary>
    /// <param name="errors">The error collection to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result<TValue>(in ErrorCollection errors) => Failure(in errors);

    /// <summary>Implicitly converts a <see cref="Result{TValue}"/> to a non-generic <see cref="Result"/>, discarding the success value.</summary>
    /// <param name="result">The typed result to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result(in Result<TValue> result) =>
        result.IsSuccess ? Result.Success() : Result.Failure(in result._errors);

    /// <summary>Indicates whether this result is equal to another typed result.</summary>
    /// <param name="other">The other result to compare with.</param>
    /// <returns><c>true</c> if both results have the same success state, value, and errors; otherwise, <c>false</c>.</returns>
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

    /// <summary>Indicates whether this result is equal to another object.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><c>true</c> if <paramref name="obj"/> is a <see cref="Result{TValue}"/> with the same state; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is Result<TValue> other && Equals(other);

    /// <summary>Returns a hash code for this result.</summary>
    /// <returns>A hash code based on the success state, value, and errors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode()
    {
        if (_isSuccess)
        {
            return _value is null ? 1 : HashCode.Combine(true, _value);
        }
        return HashCode.Combine(false, Errors);
    }

    /// <summary>Determines whether two typed results are equal.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><c>true</c> if the results are equal; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Result<TValue> left, Result<TValue> right) => left.Equals(right);

    /// <summary>Determines whether two typed results are not equal.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><c>true</c> if the results are not equal; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Result<TValue> left, Result<TValue> right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ErrorCollection NormalizeErrors(in ErrorCollection errors) =>
        errors.Count == 0 ? new ErrorCollection(Error.Uninitialized) : errors;

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidOperationException() =>
        throw new InvalidOperationException($"Cannot access Value on a failed Result. Error: [{TopError.Code}] {TopError.Message}");
}
