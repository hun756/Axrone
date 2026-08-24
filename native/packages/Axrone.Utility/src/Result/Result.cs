using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Enterprise.Patterns.Result;

/// <summary>Represents the outcome of an operation that either succeeded or failed, carrying error information on failure.</summary>
/// <remarks>This is a non-generic result that does not carry a value on success. For results that carry a value, use <see cref="Result{TValue}"/>.</remarks>
[DebuggerDisplay("IsSuccess = {_isSuccess}")]
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

    /// <summary>Gets the top-level error from this failed result, or <see cref="Error.None"/> if successful.</summary>
    /// <remarks>Returns <see cref="Error.Uninitialized"/> when the result is a failure but no errors were recorded (e.g., default-initialized struct).</remarks>
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

    /// <summary>Creates a successful result.</summary>
    /// <returns>A <see cref="Result"/> representing success.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Success() => CachedSuccess;

    /// <summary>Creates a successful result carrying the specified value.</summary>
    /// <typeparam name="TValue">The type of the success value.</typeparam>
    /// <param name="value">The success value.</param>
    /// <returns>A <see cref="Result{TValue}"/> representing success with the given value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);

    /// <summary>Creates a failed result from a single error.</summary>
    /// <param name="error">The error that caused the failure.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Failure(Error error) => new(false, new ErrorCollection(error));

    /// <summary>Creates a failed result from an error collection.</summary>
    /// <param name="errors">The collection of errors that caused the failure.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Failure(in ErrorCollection errors) =>
        new(false, NormalizeErrors(in errors));

    /// <summary>Creates a failed result from one or more errors.</summary>
    /// <param name="errors">The errors that caused the failure.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Failure(params Error[] errors)
    {
        var ec = errors is null || errors.Length == 0 ? default(ErrorCollection) : new ErrorCollection(errors);
        return new(false, NormalizeErrors(in ec));
    }

    /// <summary>Creates a failed result from a list of errors.</summary>
    /// <param name="errors">The list of errors that caused the failure.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Failure(List<Error> errors)
    {
        var ec = errors is null || errors.Count == 0 ? default(ErrorCollection) : new ErrorCollection(errors);
        return new(false, NormalizeErrors(in ec));
    }

    /// <summary>Creates a failed <see cref="Result{TValue}"/> from a single error.</summary>
    /// <typeparam name="TValue">The type of the success value.</typeparam>
    /// <param name="error">The error that caused the failure.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failure(error);

    /// <summary>Creates a failed <see cref="Result{TValue}"/> from an error collection.</summary>
    /// <typeparam name="TValue">The type of the success value.</typeparam>
    /// <param name="errors">The collection of errors that caused the failure.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure<TValue>(in ErrorCollection errors) => Result<TValue>.Failure(in errors);

    /// <summary>Creates a failed <see cref="Result{TValue}"/> from one or more errors.</summary>
    /// <typeparam name="TValue">The type of the success value.</typeparam>
    /// <param name="errors">The errors that caused the failure.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure<TValue>(params Error[] errors) => Result<TValue>.Failure(errors);

    /// <summary>Creates a failed <see cref="Result{TValue}"/> from a list of errors.</summary>
    /// <typeparam name="TValue">The type of the success value.</typeparam>
    /// <param name="errors">The list of errors that caused the failure.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Failure<TValue>(List<Error> errors) => Result<TValue>.Failure(errors);

    /// <summary>Creates a success result if the condition is true; otherwise, creates a failure result.</summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="error">The error to use when the condition is false.</param>
    /// <returns>A success or failure <see cref="Result"/> based on the condition.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Create(bool condition, Error error) => condition ? Success() : Failure(error);

    /// <summary>Creates a success result if the condition is true; otherwise, invokes the factory to produce a failure error.</summary>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="errorFactory">A factory function that produces the error when the condition is false.</param>
    /// <returns>A success or failure <see cref="Result"/> based on the condition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errorFactory"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result Create(bool condition, Func<Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return condition ? Success() : Failure(errorFactory());
    }

    /// <summary>Creates a success result with a value if the condition is true; otherwise, creates a failure result.</summary>
    /// <typeparam name="TValue">The type of the success value.</typeparam>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="value">The value to use when the condition is true.</param>
    /// <param name="error">The error to use when the condition is false.</param>
    /// <returns>A success or failure <see cref="Result{TValue}"/> based on the condition.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Create<TValue>(bool condition, TValue value, Error error) =>
        condition ? Success(value) : Failure<TValue>(error);

    /// <summary>Creates a success result via a value factory if the condition is true; otherwise, creates a failure result via an error factory.</summary>
    /// <typeparam name="TValue">The type of the success value.</typeparam>
    /// <param name="condition">The boolean condition to evaluate.</param>
    /// <param name="valueFactory">A factory function that produces the success value when the condition is true.</param>
    /// <param name="errorFactory">A factory function that produces the error when the condition is false.</param>
    /// <returns>A success or failure <see cref="Result{TValue}"/> based on the condition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="valueFactory"/> or <paramref name="errorFactory"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TValue> Create<TValue>(bool condition, Func<TValue> valueFactory, Func<Error> errorFactory)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        ArgumentNullException.ThrowIfNull(errorFactory);
        return condition ? Success(valueFactory()) : Failure<TValue>(errorFactory());
    }

    /// <summary>Executes an action and wraps it in a result, catching any exception as a failure.</summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>A success result if the action completed without exception; otherwise, a failure result containing the exception error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
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

    /// <summary>Executes a function and wraps its return value in a result, catching any exception as a failure.</summary>
    /// <typeparam name="TValue">The type of the value returned by the function.</typeparam>
    /// <param name="func">The function to execute.</param>
    /// <returns>A success result with the function's return value, or a failure result containing the exception error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="func"/> is null.</exception>
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

    /// <summary>Executes an async action and wraps it in a result, catching any exception as a failure.</summary>
    /// <param name="action">The async action to execute.</param>
    /// <returns>A task that completes with a success result, or a failure result containing the exception error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
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

    /// <summary>Executes an async action returning a <see cref="ValueTask"/> and wraps it in a result, catching any exception as a failure.</summary>
    /// <param name="action">The async action to execute.</param>
    /// <returns>A value task that completes with a success result, or a failure result containing the exception error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
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

    /// <summary>Executes an async function and wraps its return value in a result, catching any exception as a failure.</summary>
    /// <typeparam name="TValue">The type of the value returned by the async function.</typeparam>
    /// <param name="func">The async function to execute.</param>
    /// <returns>A task that completes with a success result containing the value, or a failure result containing the exception error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="func"/> is null.</exception>
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

    /// <summary>Executes an async function returning a <see cref="ValueTask{TValue}"/> and wraps its return value in a result, catching any exception as a failure.</summary>
    /// <typeparam name="TValue">The type of the value returned by the async function.</typeparam>
    /// <param name="func">The async function to execute.</param>
    /// <returns>A value task that completes with a success result containing the value, or a failure result containing the exception error.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="func"/> is null.</exception>
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

    /// <summary>Combines multiple results into a single result. Returns success if all results succeeded; otherwise, returns a failure with all collected errors.</summary>
    /// <param name="results">A read-only span of results to combine.</param>
    /// <returns>A combined <see cref="Result"/> that is successful only if all input results succeeded.</returns>
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

    /// <summary>Combines multiple results into a single result. Returns success if all results succeeded; otherwise, returns a failure with all collected errors.</summary>
    /// <param name="results">An enumerable of results to combine.</param>
    /// <returns>A combined <see cref="Result"/> that is successful only if all input results succeeded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="results"/> is null.</exception>
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

    /// <summary>Matches this result to one of two functions, returning the result of the matching branch.</summary>
    /// <typeparam name="TResult">The return type of both branch functions.</typeparam>
    /// <param name="onSuccess">The function to invoke when this result is successful.</param>
    /// <param name="onFailure">The function to invoke when this result is a failure, receiving the error collection.</param>
    /// <returns>The result of the invoked function.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onSuccess"/> or <paramref name="onFailure"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public TResult Match<TResult>(Func<TResult> onSuccess, Func<ErrorCollection, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return _isSuccess ? onSuccess() : onFailure(Errors);
    }

    /// <summary>Matches this result to one of two actions, executing the matching branch.</summary>
    /// <param name="onSuccess">The action to invoke when this result is successful.</param>
    /// <param name="onFailure">The action to invoke when this result is a failure, receiving the error collection.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="onSuccess"/> or <paramref name="onFailure"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public void Match(Action onSuccess, Action<ErrorCollection> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        if (_isSuccess) onSuccess();
        else onFailure(Errors);
    }

    /// <summary>Maps a successful result to another result using the specified mapper function. Returns this result unchanged if it is a failure.</summary>
    /// <param name="mapper">The function to apply to a successful result.</param>
    /// <returns>The mapped result if successful; otherwise, this failure result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapper"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result Map(Func<Result> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _isSuccess ? mapper() : this;
    }

    /// <summary>Maps a successful result's outcome to a typed result using the specified mapper function.</summary>
    /// <typeparam name="TResult">The type of the success value in the mapped result.</typeparam>
    /// <param name="mapper">The function to apply to a successful result.</param>
    /// <returns>A new <see cref="Result{TResult}"/> with the mapped value if successful; otherwise, a failure result with the same errors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapper"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TResult> Map<TResult>(Func<TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return _isSuccess ? Result<TResult>.Success(mapper()) : Result<TResult>.Failure(Errors);
    }

    /// <summary>Chains this result with a binder function that returns a typed result. Returns a failure if this result is already a failure.</summary>
    /// <typeparam name="TResult">The type of the success value in the bound result.</typeparam>
    /// <param name="binder">The function to apply to a successful result.</param>
    /// <returns>The bound result if successful; otherwise, a failure result with the same errors.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result<TResult> Bind<TResult>(Func<Result<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _isSuccess ? binder() : Result<TResult>.Failure(Errors);
    }

    /// <summary>Chains this result with a binder function that returns a non-generic result. Returns this result unchanged if it is a failure.</summary>
    /// <param name="binder">The function to apply to a successful result.</param>
    /// <returns>The bound result if successful; otherwise, this failure result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binder"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result Bind(Func<Result> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        return _isSuccess ? binder() : this;
    }

    /// <summary>Executes an action if this result is successful and returns this result unchanged.</summary>
    /// <param name="action">The action to execute on success.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result Tap(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_isSuccess) action();
        return this;
    }

    /// <summary>Executes an action with the error collection if this result is a failure and returns this result unchanged.</summary>
    /// <param name="action">The action to execute on failure, receiving the error collection.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result TapError(Action<ErrorCollection> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!_isSuccess) action(Errors);
        return this;
    }

    /// <summary>Returns this result if successful; otherwise, returns the specified fallback result.</summary>
    /// <param name="fallback">The fallback result to return on failure.</param>
    /// <returns>This result if successful; otherwise, <paramref name="fallback"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result OrElse(Result fallback) => _isSuccess ? this : fallback;

    /// <summary>Returns this result if successful; otherwise, invokes the factory to produce a fallback result.</summary>
    /// <param name="fallbackFactory">A factory function that produces the fallback result.</param>
    /// <returns>This result if successful; otherwise, the result of <paramref name="fallbackFactory"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fallbackFactory"/> is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public Result OrElse(Func<Result> fallbackFactory)
    {
        ArgumentNullException.ThrowIfNull(fallbackFactory);
        return _isSuccess ? this : fallbackFactory();
    }

    /// <summary>Implicitly converts an <see cref="Error"/> to a failed <see cref="Result"/>.</summary>
    /// <param name="error">The error to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>Implicitly converts an array of errors to a failed <see cref="Result"/>.</summary>
    /// <param name="errors">The error array to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result(Error[] errors) => Failure(errors);

    /// <summary>Implicitly converts an <see cref="ErrorCollection"/> to a failed <see cref="Result"/>.</summary>
    /// <param name="errors">The error collection to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static implicit operator Result(in ErrorCollection errors) => Failure(in errors);

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ErrorCollection NormalizeErrors(in ErrorCollection errors) =>
        errors.Count == 0 ? new ErrorCollection(Error.Uninitialized) : errors;

    /// <summary>Indicates whether this result is equal to another result.</summary>
    /// <param name="other">The other result to compare with.</param>
    /// <returns><c>true</c> if both results have the same success state and errors; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public bool Equals(Result other)
    {
        if (_isSuccess != other._isSuccess) return false;
        return _isSuccess || Errors.Equals(other.Errors);
    }

    /// <summary>Indicates whether this result is equal to another object.</summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><c>true</c> if <paramref name="obj"/> is a <see cref="Result"/> with the same state; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <summary>Returns a hash code for this result.</summary>
    /// <returns>A hash code based on the success state and errors.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public override int GetHashCode() => _isSuccess ? 1 : HashCode.Combine(false, Errors);

    /// <summary>Determines whether two results are equal.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><c>true</c> if the results are equal; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>Determines whether two results are not equal.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><c>true</c> if the results are not equal; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

}
