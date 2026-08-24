using System.Runtime.CompilerServices;

namespace Enterprise.Patterns.Result;

/// <summary>Provides LINQ-style and monadic extension methods for <see cref="Result{T}"/>.</summary>
public static class ResultExtensions
{
    /// <summary>Projects the value of a successful result into a new form (enables LINQ query syntax).</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="selector">The projection function.</param>
    /// <returns>A new result containing the projected value, or the original failure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TOut> Select<TIn, TOut>(
        this in Result<TIn> result,
        Func<TIn, TOut> selector) =>
        result.Map(selector);

    /// <summary>Chains two dependent result-producing operations (enables LINQ query syntax).</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TIntermediate">The intermediate result value type.</typeparam>
    /// <typeparam name="TOut">The final output value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="binder">A function that produces an intermediate result from the input value.</param>
    /// <param name="resultSelector">A function that combines the input and intermediate values into the final result.</param>
    /// <returns>A result containing the combined value, or the first encountered failure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TOut> SelectMany<TIn, TIntermediate, TOut>(
        this in Result<TIn> result,
        Func<TIn, Result<TIntermediate>> binder,
        Func<TIn, TIntermediate, TOut> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(resultSelector);

        if (result.IsFailure)
        {
            return Result<TOut>.Failure(result.Errors);
        }

        TIn inVal = result.Value;
        Result<TIntermediate> intermediateResult = binder(inVal);
        if (intermediateResult.IsFailure)
        {
            return Result<TOut>.Failure(intermediateResult.Errors);
        }

        return Result<TOut>.Success(resultSelector(inVal, intermediateResult.Value));
    }

    /// <summary>Filters a successful result by a predicate, returning a failure if the condition is not met.</summary>
    /// <typeparam name="TIn">The value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="predicate">The condition to evaluate against the value.</param>
    /// <param name="error">An optional error to use when the predicate fails; defaults to a validation error.</param>
    /// <returns>The original result if the predicate passes or the result was already a failure; otherwise a failure result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TIn> Where<TIn>(
        this in Result<TIn> result,
        Func<TIn, bool> predicate,
        Error? error = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (result.IsFailure) return result;
        return predicate(result.Value)
            ? result
            : Result<TIn>.Failure(error ?? Error.Validation("Result.PredicateFailed", "The predicate was not satisfied."));
    }

    /// <summary>Combines two results into a single result containing a tuple of both values.</summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns>A result containing both values as a tuple, or merged errors from any failures.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<(T1, T2)> Zip<T1, T2>(
        this in Result<T1> left,
        in Result<T2> right)
    {
        if (left.IsSuccess && right.IsSuccess)
        {
            return Result<(T1, T2)>.Success((left.Value, right.Value));
        }

        return Result<(T1, T2)>.Failure(MergeFailures([left.Errors, right.Errors]));
    }

    /// <summary>Combines two results into a single result by applying a merger function to both values.</summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    /// <typeparam name="TMerged">The merged output type.</typeparam>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <param name="merger">A function that combines both values into a single result.</param>
    /// <returns>A result containing the merged value, or merged errors from any failures.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TMerged> Zip<T1, T2, TMerged>(
        this in Result<T1> left,
        in Result<T2> right,
        Func<T1, T2, TMerged> merger)
    {
        ArgumentNullException.ThrowIfNull(merger);
        if (left.IsSuccess && right.IsSuccess)
        {
            return Result<TMerged>.Success(merger(left.Value, right.Value));
        }

        return Result<TMerged>.Failure(MergeFailures([left.Errors, right.Errors]));
    }

    /// <summary>Combines three results into a single result containing a tuple of all values.</summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    /// <typeparam name="T3">The third value type.</typeparam>
    /// <param name="r1">The first result.</param>
    /// <param name="r2">The second result.</param>
    /// <param name="r3">The third result.</param>
    /// <returns>A result containing all three values as a tuple, or merged errors from any failures.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<(T1, T2, T3)> Combine<T1, T2, T3>(
        in Result<T1> r1,
        in Result<T2> r2,
        in Result<T3> r3)
    {
        if (r1.IsSuccess && r2.IsSuccess && r3.IsSuccess)
        {
            return Result<(T1, T2, T3)>.Success((r1.Value, r2.Value, r3.Value));
        }

        return Result<(T1, T2, T3)>.Failure(MergeFailures([r1.Errors, r2.Errors, r3.Errors]));
    }

    /// <summary>Combines four results into a single result containing a tuple of all values.</summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    /// <typeparam name="T3">The third value type.</typeparam>
    /// <typeparam name="T4">The fourth value type.</typeparam>
    /// <param name="r1">The first result.</param>
    /// <param name="r2">The second result.</param>
    /// <param name="r3">The third result.</param>
    /// <param name="r4">The fourth result.</param>
    /// <returns>A result containing all four values as a tuple, or merged errors from any failures.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<(T1, T2, T3, T4)> Combine<T1, T2, T3, T4>(
        in Result<T1> r1,
        in Result<T2> r2,
        in Result<T3> r3,
        in Result<T4> r4)
    {
        if (r1.IsSuccess && r2.IsSuccess && r3.IsSuccess && r4.IsSuccess)
        {
            return Result<(T1, T2, T3, T4)>.Success((r1.Value, r2.Value, r3.Value, r4.Value));
        }

        return Result<(T1, T2, T3, T4)>.Failure(MergeFailures([r1.Errors, r2.Errors, r3.Errors, r4.Errors]));
    }

    /// <summary>Combines five results into a single result containing a tuple of all values.</summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    /// <typeparam name="T3">The third value type.</typeparam>
    /// <typeparam name="T4">The fourth value type.</typeparam>
    /// <typeparam name="T5">The fifth value type.</typeparam>
    /// <param name="r1">The first result.</param>
    /// <param name="r2">The second result.</param>
    /// <param name="r3">The third result.</param>
    /// <param name="r4">The fourth result.</param>
    /// <param name="r5">The fifth result.</param>
    /// <returns>A result containing all five values as a tuple, or merged errors from any failures.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<(T1, T2, T3, T4, T5)> Combine<T1, T2, T3, T4, T5>(
        in Result<T1> r1,
        in Result<T2> r2,
        in Result<T3> r3,
        in Result<T4> r4,
        in Result<T5> r5)
    {
        if (r1.IsSuccess && r2.IsSuccess && r3.IsSuccess && r4.IsSuccess && r5.IsSuccess)
        {
            return Result<(T1, T2, T3, T4, T5)>.Success((r1.Value, r2.Value, r3.Value, r4.Value, r5.Value));
        }

        return Result<(T1, T2, T3, T4, T5)>.Failure(MergeFailures([r1.Errors, r2.Errors, r3.Errors, r4.Errors, r5.Errors]));
    }

    /// <summary>Collects an enumerable of results into a single result containing all values, or all errors.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The enumerable of results.</param>
    /// <returns>A success result with all values if all succeeded, or a failure with all collected errors.</returns>
    public static Result<IReadOnlyList<T>> Collect<T>(this IEnumerable<Result<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<T>? values = null;
        List<Error>? errors = null;

        if (source is IReadOnlyCollection<Result<T>> collection)
        {
            values = new List<T>(collection.Count);
        }

        foreach (var item in source)
        {
            if (item.IsSuccess)
            {
                if (errors is null)
                {
                    values ??= new List<T>();
                    values.Add(item.Value);
                }
            }
            else
            {
                errors ??= new List<Error>();
                var errs = item.Errors;
                for (int i = 0; i < errs.Count; i++)
                {
                    errors.Add(errs[i]);
                }
            }
        }

        if (errors is not null)
        {
            return Result<IReadOnlyList<T>>.Failure(errors);
        }

        return Result<IReadOnlyList<T>>.Success(values ?? (IReadOnlyList<T>)Array.Empty<T>());
    }

    /// <summary>Partitions an enumerable of results into separate collections of successes and failures.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="source">The enumerable of results.</param>
    /// <returns>A tuple containing the list of successful values and the collection of all errors.</returns>
    public static (IReadOnlyList<T> Successes, ErrorCollection Failures) Partition<T>(this IEnumerable<Result<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<T> successes = [];
        List<Error> failures = [];

        foreach (var item in source)
        {
            if (item.IsSuccess)
            {
                successes.Add(item.Value);
            }
            else
            {
                var errs = item.Errors;
                for (int i = 0; i < errs.Count; i++)
                {
                    failures.Add(errs[i]);
                }
            }
        }

        return (successes, new ErrorCollection(failures));
    }

    /// <summary>Maps the value of a successful result to a new value asynchronously using a Task-returning function.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="mapper">An async function that maps the value.</param>
    /// <returns>A task containing the mapped result.</returns>
    public static Task<Result<TResult>> MapAsync<TValue, TResult>(
        this Result<TValue> result,
        Func<TValue, Task<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        if (result.IsFailure) return Task.FromResult(Result<TResult>.Failure(result.Errors));
        return MapAsyncCore(result, mapper);
    }

    private static async Task<Result<TResult>> MapAsyncCore<TValue, TResult>(
        Result<TValue> result,
        Func<TValue, Task<TResult>> mapper)
    {
        TResult res = await mapper(result.Value).ConfigureAwait(false);
        return Result<TResult>.Success(res);
    }

    /// <summary>Maps the value of a successful result to a new value asynchronously using a ValueTask-returning function.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="mapper">An async function that maps the value.</param>
    /// <returns>A value task containing the mapped result.</returns>
    public static ValueTask<Result<TResult>> MapAsync<TValue, TResult>(
        this Result<TValue> result,
        Func<TValue, ValueTask<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        if (result.IsFailure) return new ValueTask<Result<TResult>>(Result<TResult>.Failure(result.Errors));
        return MapAsyncCore(result, mapper);
    }

    private static async ValueTask<Result<TResult>> MapAsyncCore<TValue, TResult>(
        Result<TValue> result,
        Func<TValue, ValueTask<TResult>> mapper)
    {
        TResult res = await mapper(result.Value).ConfigureAwait(false);
        return Result<TResult>.Success(res);
    }

    /// <summary>Chains an async result-producing operation using a Task-returning binder.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="binder">An async function that returns a result.</param>
    /// <returns>A task containing the bound result.</returns>
    public static Task<Result<TResult>> BindAsync<TValue, TResult>(
        this Result<TValue> result,
        Func<TValue, Task<Result<TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        if (result.IsFailure) return Task.FromResult(Result<TResult>.Failure(result.Errors));
        return BindAsyncCore(result, binder);
    }

    private static async Task<Result<TResult>> BindAsyncCore<TValue, TResult>(
        Result<TValue> result,
        Func<TValue, Task<Result<TResult>>> binder)
    {
        return await binder(result.Value).ConfigureAwait(false);
    }

    /// <summary>Chains an async result-producing operation using a ValueTask-returning binder.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="binder">An async function that returns a result.</param>
    /// <returns>A value task containing the bound result.</returns>
    public static ValueTask<Result<TResult>> BindAsync<TValue, TResult>(
        this Result<TValue> result,
        Func<TValue, ValueTask<Result<TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        if (result.IsFailure) return new ValueTask<Result<TResult>>(Result<TResult>.Failure(result.Errors));
        return BindAsyncCore(result, binder);
    }

    private static async ValueTask<Result<TResult>> BindAsyncCore<TValue, TResult>(
        Result<TValue> result,
        Func<TValue, ValueTask<Result<TResult>>> binder)
    {
        return await binder(result.Value).ConfigureAwait(false);
    }

    /// <summary>Executes an async side-effect on a successful result using a Task-returning action.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="action">An async action to execute on the value.</param>
    /// <returns>A task containing the original result.</returns>
    public static Task<Result<TValue>> TapAsync<TValue>(
        this Result<TValue> result,
        Func<TValue, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsFailure) return Task.FromResult(result);
        return TapAsyncCore(result, action);
    }

    private static async Task<Result<TValue>> TapAsyncCore<TValue>(
        Result<TValue> result,
        Func<TValue, Task> action)
    {
        await action(result.Value).ConfigureAwait(false);
        return result;
    }

    /// <summary>Executes an async side-effect on a successful result using a ValueTask-returning action.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="action">An async action to execute on the value.</param>
    /// <returns>A value task containing the original result.</returns>
    public static ValueTask<Result<TValue>> TapAsync<TValue>(
        this Result<TValue> result,
        Func<TValue, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsFailure) return new ValueTask<Result<TValue>>(result);
        return TapAsyncCore(result, action);
    }

    private static async ValueTask<Result<TValue>> TapAsyncCore<TValue>(
        Result<TValue> result,
        Func<TValue, ValueTask> action)
    {
        await action(result.Value).ConfigureAwait(false);
        return result;
    }

    /// <summary>Executes an async side-effect on a failed result using a Task-returning action.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="action">An async action to execute on the errors.</param>
    /// <returns>A task containing the original result.</returns>
    public static Task<Result<TValue>> TapErrorAsync<TValue>(
        this Result<TValue> result,
        Func<ErrorCollection, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsSuccess) return Task.FromResult(result);
        return TapErrorAsyncCore(result, action);
    }

    private static async Task<Result<TValue>> TapErrorAsyncCore<TValue>(
        Result<TValue> result,
        Func<ErrorCollection, Task> action)
    {
        await action(result.Errors).ConfigureAwait(false);
        return result;
    }

    /// <summary>Executes an async side-effect on a failed result using a ValueTask-returning action.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="action">An async action to execute on the errors.</param>
    /// <returns>A value task containing the original result.</returns>
    public static ValueTask<Result<TValue>> TapErrorAsync<TValue>(
        this Result<TValue> result,
        Func<ErrorCollection, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsSuccess) return new ValueTask<Result<TValue>>(result);
        return TapErrorAsyncCore(result, action);
    }

    private static async ValueTask<Result<TValue>> TapErrorAsyncCore<TValue>(
        Result<TValue> result,
        Func<ErrorCollection, ValueTask> action)
    {
        await action(result.Errors).ConfigureAwait(false);
        return result;
    }

    /// <summary>Validates a successful result asynchronously using a Task-returning predicate.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="predicate">An async function that validates the value.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>A task containing the original result if validation passes, or a failure result.</returns>
    public static Task<Result<TValue>> EnsureAsync<TValue>(
        this Result<TValue> result,
        Func<TValue, Task<bool>> predicate,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (result.IsFailure) return Task.FromResult(result);
        return EnsureAsyncCore(result, predicate, error);
    }

    private static async Task<Result<TValue>> EnsureAsyncCore<TValue>(
        Result<TValue> result,
        Func<TValue, Task<bool>> predicate,
        Error error)
    {
        bool passes = await predicate(result.Value).ConfigureAwait(false);
        return passes ? result : Result<TValue>.Failure(error);
    }

    /// <summary>Validates a successful result asynchronously using a ValueTask-returning predicate.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="result">The source result.</param>
    /// <param name="predicate">An async function that validates the value.</param>
    /// <param name="error">The error to return if the predicate fails.</param>
    /// <returns>A value task containing the original result if validation passes, or a failure result.</returns>
    public static ValueTask<Result<TValue>> EnsureAsync<TValue>(
        this Result<TValue> result,
        Func<TValue, ValueTask<bool>> predicate,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (result.IsFailure) return new ValueTask<Result<TValue>>(result);
        return EnsureAsyncCore(result, predicate, error);
    }

    private static async ValueTask<Result<TValue>> EnsureAsyncCore<TValue>(
        Result<TValue> result,
        Func<TValue, ValueTask<bool>> predicate,
        Error error)
    {
        bool passes = await predicate(result.Value).ConfigureAwait(false);
        return passes ? result : Result<TValue>.Failure(error);
    }

    /// <summary>Maps the value of a successful result from a Task using a synchronous mapper.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="resultTask">The task containing the source result.</param>
    /// <param name="mapper">The mapping function.</param>
    /// <returns>A task containing the mapped result.</returns>
    public static async Task<Result<TResult>> MapAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(mapper);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Map(mapper);
    }

    /// <summary>Maps the value of a successful result from a Task using an async Task-returning mapper.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="resultTask">The task containing the source result.</param>
    /// <param name="mapper">The async mapping function.</param>
    /// <returns>A task containing the mapped result.</returns>
    public static async Task<Result<TResult>> MapAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Task<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(mapper);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.MapAsync(mapper).ConfigureAwait(false);
    }

    /// <summary>Maps the value of a successful result from a ValueTask using a synchronous mapper.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="resultTask">The value task containing the source result.</param>
    /// <param name="mapper">The mapping function.</param>
    /// <returns>A value task containing the mapped result.</returns>
    public static async ValueTask<Result<TResult>> MapAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Map(mapper);
    }

    /// <summary>Maps the value of a successful result from a ValueTask using an async ValueTask-returning mapper.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="resultTask">The value task containing the source result.</param>
    /// <param name="mapper">The async mapping function.</param>
    /// <returns>A value task containing the mapped result.</returns>
    public static async ValueTask<Result<TResult>> MapAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, ValueTask<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.MapAsync(mapper).ConfigureAwait(false);
    }

    /// <summary>Chains a result-producing operation from a Task using a synchronous binder.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="resultTask">The task containing the source result.</param>
    /// <param name="binder">The binding function.</param>
    /// <returns>A task containing the bound result.</returns>
    public static async Task<Result<TResult>> BindAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Result<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(binder);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Bind(binder);
    }

    /// <summary>Chains a result-producing operation from a Task using an async Task-returning binder.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="resultTask">The task containing the source result.</param>
    /// <param name="binder">The async binding function.</param>
    /// <returns>A task containing the bound result.</returns>
    public static async Task<Result<TResult>> BindAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Task<Result<TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(binder);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.BindAsync(binder).ConfigureAwait(false);
    }

    /// <summary>Chains a result-producing operation from a ValueTask using a synchronous binder.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="resultTask">The value task containing the source result.</param>
    /// <param name="binder">The binding function.</param>
    /// <returns>A value task containing the bound result.</returns>
    public static async ValueTask<Result<TResult>> BindAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, Result<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Bind(binder);
    }

    /// <summary>Chains a result-producing operation from a ValueTask using an async ValueTask-returning binder.</summary>
    /// <typeparam name="TValue">The input value type.</typeparam>
    /// <typeparam name="TResult">The output value type.</typeparam>
    /// <param name="resultTask">The value task containing the source result.</param>
    /// <param name="binder">The async binding function.</param>
    /// <returns>A value task containing the bound result.</returns>
    public static async ValueTask<Result<TResult>> BindAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, ValueTask<Result<TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.BindAsync(binder).ConfigureAwait(false);
    }

    /// <summary>Executes a synchronous side-effect on a successful result from a Task.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="resultTask">The task containing the source result.</param>
    /// <param name="action">The action to execute on the value.</param>
    /// <returns>A task containing the original result.</returns>
    public static async Task<Result<TValue>> TapAsync<TValue>(
        this Task<Result<TValue>> resultTask,
        Action<TValue> action)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(action);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Tap(action);
    }

    /// <summary>Executes an async side-effect on a successful result from a Task using a Task-returning action.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="resultTask">The task containing the source result.</param>
    /// <param name="action">The async action to execute on the value.</param>
    /// <returns>A task containing the original result.</returns>
    public static async Task<Result<TValue>> TapAsync<TValue>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Task> action)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(action);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.TapAsync(action).ConfigureAwait(false);
    }

    /// <summary>Executes a synchronous side-effect on a successful result from a ValueTask.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="resultTask">The value task containing the source result.</param>
    /// <param name="action">The action to execute on the value.</param>
    /// <returns>A value task containing the original result.</returns>
    public static async ValueTask<Result<TValue>> TapAsync<TValue>(
        this ValueTask<Result<TValue>> resultTask,
        Action<TValue> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Tap(action);
    }

    /// <summary>Executes an async side-effect on a successful result from a ValueTask using a ValueTask-returning action.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="resultTask">The value task containing the source result.</param>
    /// <param name="action">The async action to execute on the value.</param>
    /// <returns>A value task containing the original result.</returns>
    public static async ValueTask<Result<TValue>> TapAsync<TValue>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.TapAsync(action).ConfigureAwait(false);
    }

    /// <summary>Pattern-matches a result from a Task using synchronous handlers.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="resultTask">The task containing the source result.</param>
    /// <param name="onSuccess">The handler for a successful result.</param>
    /// <param name="onFailure">The handler for a failed result.</param>
    /// <returns>A task containing the result of the matched handler.</returns>
    public static async Task<TResult> MatchAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, TResult> onSuccess,
        Func<ErrorCollection, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Match(onSuccess, onFailure);
    }

    /// <summary>Pattern-matches a result from a ValueTask using synchronous handlers.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="resultTask">The value task containing the source result.</param>
    /// <param name="onSuccess">The handler for a successful result.</param>
    /// <param name="onFailure">The handler for a failed result.</param>
    /// <returns>A value task containing the result of the matched handler.</returns>
    public static async ValueTask<TResult> MatchAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, TResult> onSuccess,
        Func<ErrorCollection, TResult> onFailure)
    {
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Match(onSuccess, onFailure);
    }

    /// <summary>Pattern-matches a result from a Task using async Task-returning handlers.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="resultTask">The task containing the source result.</param>
    /// <param name="onSuccess">The async handler for a successful result.</param>
    /// <param name="onFailure">The async handler for a failed result.</param>
    /// <returns>A task containing the result of the matched handler.</returns>
    public static async Task<TResult> MatchAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Task<TResult>> onSuccess,
        Func<ErrorCollection, Task<TResult>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.IsSuccess
            ? await onSuccess(result.Value).ConfigureAwait(false)
            : await onFailure(result.Errors).ConfigureAwait(false);
    }

    /// <summary>Pattern-matches a result from a ValueTask using async ValueTask-returning handlers.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <typeparam name="TResult">The output type.</typeparam>
    /// <param name="resultTask">The value task containing the source result.</param>
    /// <param name="onSuccess">The async handler for a successful result.</param>
    /// <param name="onFailure">The async handler for a failed result.</param>
    /// <returns>A value task containing the result of the matched handler.</returns>
    public static async ValueTask<TResult> MatchAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, ValueTask<TResult>> onSuccess,
        Func<ErrorCollection, ValueTask<TResult>> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.IsSuccess
            ? await onSuccess(result.Value).ConfigureAwait(false)
            : await onFailure(result.Errors).ConfigureAwait(false);
    }

    /// <summary>Projects the value of a successful result from a Task into a new form.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="resultTask">The task containing the source result.</param>
    /// <param name="selector">The projection function.</param>
    /// <returns>A task containing the projected result.</returns>
    public static async Task<Result<TOut>> Select<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(selector);
        Result<TIn> result = await resultTask.ConfigureAwait(false);
        return result.Map(selector);
    }

    /// <summary>Chains two dependent result-producing operations from a Task.</summary>
    /// <typeparam name="TIn">The input value type.</typeparam>
    /// <typeparam name="TIntermediate">The intermediate result value type.</typeparam>
    /// <typeparam name="TOut">The final output value type.</typeparam>
    /// <param name="resultTask">The task containing the source result.</param>
    /// <param name="binder">An async function that produces an intermediate result from the input value.</param>
    /// <param name="resultSelector">A function that combines the input and intermediate values into the final result.</param>
    /// <returns>A task containing the combined result, or the first encountered failure.</returns>
    public static async Task<Result<TOut>> SelectMany<TIn, TIntermediate, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<Result<TIntermediate>>> binder,
        Func<TIn, TIntermediate, TOut> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(resultSelector);

        Result<TIn> result = await resultTask.ConfigureAwait(false);
        if (result.IsFailure) return Result<TOut>.Failure(result.Errors);

        TIn inVal = result.Value;
        Result<TIntermediate> intermediate = await binder(inVal).ConfigureAwait(false);
        if (intermediate.IsFailure) return Result<TOut>.Failure(intermediate.Errors);

        return Result<TOut>.Success(resultSelector(inVal, intermediate.Value));
    }

    private static ErrorCollection MergeFailures(scoped ReadOnlySpan<ErrorCollection> collections)
    {
        int total = 0;
        for (int i = 0; i < collections.Length; i++)
            total += collections[i].Count;

        var errors = new List<Error>(total);
        for (int i = 0; i < collections.Length; i++)
        {
            var col = collections[i];
            for (int j = 0; j < col.Count; j++)
                errors.Add(col[j]);
        }
        return new ErrorCollection(errors);
    }
}
