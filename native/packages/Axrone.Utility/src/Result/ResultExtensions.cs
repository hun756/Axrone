using System.Runtime.CompilerServices;

namespace Enterprise.Patterns.Result;

public static class ResultExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<TOut> Select<TIn, TOut>(
        this in Result<TIn> result,
        Func<TIn, TOut> selector) =>
        result.Map(selector);

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
            : Result<TIn>.Failure(error ?? Error.Validation("Result.PredicateFailed", "Filter predicate evaluation failed."));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static Result<(T1, T2)> Zip<T1, T2>(
        this in Result<T1> left,
        in Result<T2> right)
    {
        if (left.IsSuccess && right.IsSuccess)
        {
            return Result<(T1, T2)>.Success((left.Value, right.Value));
        }

        List<Error> errors = new(left.Errors.Count + right.Errors.Count);
        if (left.IsFailure) errors.AddRange(left.Errors);
        if (right.IsFailure) errors.AddRange(right.Errors);
        return Result<(T1, T2)>.Failure(errors);
    }

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

        List<Error> errors = new(left.Errors.Count + right.Errors.Count);
        if (left.IsFailure) errors.AddRange(left.Errors);
        if (right.IsFailure) errors.AddRange(right.Errors);
        return Result<TMerged>.Failure(errors);
    }

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

        List<Error> errors = new(r1.Errors.Count + r2.Errors.Count + r3.Errors.Count);
        if (r1.IsFailure) errors.AddRange(r1.Errors);
        if (r2.IsFailure) errors.AddRange(r2.Errors);
        if (r3.IsFailure) errors.AddRange(r3.Errors);
        return Result<(T1, T2, T3)>.Failure(errors);
    }

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

        List<Error> errors = new(r1.Errors.Count + r2.Errors.Count + r3.Errors.Count + r4.Errors.Count);
        if (r1.IsFailure) errors.AddRange(r1.Errors);
        if (r2.IsFailure) errors.AddRange(r2.Errors);
        if (r3.IsFailure) errors.AddRange(r3.Errors);
        if (r4.IsFailure) errors.AddRange(r4.Errors);
        return Result<(T1, T2, T3, T4)>.Failure(errors);
    }

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

        List<Error> errors = new(r1.Errors.Count + r2.Errors.Count + r3.Errors.Count + r4.Errors.Count + r5.Errors.Count);
        if (r1.IsFailure) errors.AddRange(r1.Errors);
        if (r2.IsFailure) errors.AddRange(r2.Errors);
        if (r3.IsFailure) errors.AddRange(r3.Errors);
        if (r4.IsFailure) errors.AddRange(r4.Errors);
        if (r5.IsFailure) errors.AddRange(r5.Errors);
        return Result<(T1, T2, T3, T4, T5)>.Failure(errors);
    }

    public static Result<IReadOnlyList<T>> Collect<T>(this IEnumerable<Result<T>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<T>? values = null;
        List<Error>? errors = null;
        int count = 0;

        foreach (var item in source)
        {
            count++;
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

    public static async Task<Result<TResult>> MapAsync<TValue, TResult>(
        this Result<TValue> result,
        Func<TValue, Task<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        if (result.IsFailure) return Result<TResult>.Failure(result.Errors);
        TResult res = await mapper(result.Value).ConfigureAwait(false);
        return Result<TResult>.Success(res);
    }

    public static async ValueTask<Result<TResult>> MapAsync<TValue, TResult>(
        this Result<TValue> result,
        Func<TValue, ValueTask<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        if (result.IsFailure) return Result<TResult>.Failure(result.Errors);
        TResult res = await mapper(result.Value).ConfigureAwait(false);
        return Result<TResult>.Success(res);
    }

    public static async Task<Result<TResult>> BindAsync<TValue, TResult>(
        this Result<TValue> result,
        Func<TValue, Task<Result<TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        if (result.IsFailure) return Result<TResult>.Failure(result.Errors);
        return await binder(result.Value).ConfigureAwait(false);
    }

    public static async ValueTask<Result<TResult>> BindAsync<TValue, TResult>(
        this Result<TValue> result,
        Func<TValue, ValueTask<Result<TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        if (result.IsFailure) return Result<TResult>.Failure(result.Errors);
        return await binder(result.Value).ConfigureAwait(false);
    }

    public static async Task<Result<TValue>> TapAsync<TValue>(
        this Result<TValue> result,
        Func<TValue, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsSuccess)
        {
            await action(result.Value).ConfigureAwait(false);
        }
        return result;
    }

    public static async ValueTask<Result<TValue>> TapAsync<TValue>(
        this Result<TValue> result,
        Func<TValue, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsSuccess)
        {
            await action(result.Value).ConfigureAwait(false);
        }
        return result;
    }

    public static async Task<Result<TValue>> TapErrorAsync<TValue>(
        this Result<TValue> result,
        Func<ErrorCollection, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsFailure)
        {
            await action(result.Errors).ConfigureAwait(false);
        }
        return result;
    }

    public static async ValueTask<Result<TValue>> TapErrorAsync<TValue>(
        this Result<TValue> result,
        Func<ErrorCollection, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (result.IsFailure)
        {
            await action(result.Errors).ConfigureAwait(false);
        }
        return result;
    }

    public static async Task<Result<TValue>> EnsureAsync<TValue>(
        this Result<TValue> result,
        Func<TValue, Task<bool>> predicate,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (result.IsFailure) return result;
        bool passes = await predicate(result.Value).ConfigureAwait(false);
        return passes ? result : Result<TValue>.Failure(error);
    }

    public static async ValueTask<Result<TValue>> EnsureAsync<TValue>(
        this Result<TValue> result,
        Func<TValue, ValueTask<bool>> predicate,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (result.IsFailure) return result;
        bool passes = await predicate(result.Value).ConfigureAwait(false);
        return passes ? result : Result<TValue>.Failure(error);
    }

    public static async Task<Result<TResult>> MapAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(mapper);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Map(mapper);
    }

    public static async Task<Result<TResult>> MapAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Task<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(mapper);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.MapAsync(mapper).ConfigureAwait(false);
    }

    public static async ValueTask<Result<TResult>> MapAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Map(mapper);
    }

    public static async ValueTask<Result<TResult>> MapAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, ValueTask<TResult>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.MapAsync(mapper).ConfigureAwait(false);
    }

    public static async Task<Result<TResult>> BindAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Result<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(binder);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Bind(binder);
    }

    public static async Task<Result<TResult>> BindAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Task<Result<TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(binder);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.BindAsync(binder).ConfigureAwait(false);
    }

    public static async ValueTask<Result<TResult>> BindAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, Result<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Bind(binder);
    }

    public static async ValueTask<Result<TResult>> BindAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, ValueTask<Result<TResult>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.BindAsync(binder).ConfigureAwait(false);
    }

    public static async Task<Result<TValue>> TapAsync<TValue>(
        this Task<Result<TValue>> resultTask,
        Action<TValue> action)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(action);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Tap(action);
    }

    public static async Task<Result<TValue>> TapAsync<TValue>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, Task> action)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(action);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.TapAsync(action).ConfigureAwait(false);
    }

    public static async ValueTask<Result<TValue>> TapAsync<TValue>(
        this ValueTask<Result<TValue>> resultTask,
        Action<TValue> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Tap(action);
    }

    public static async ValueTask<Result<TValue>> TapAsync<TValue>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return await result.TapAsync(action).ConfigureAwait(false);
    }

    public static async Task<TResult> MatchAsync<TValue, TResult>(
        this Task<Result<TValue>> resultTask,
        Func<TValue, TResult> onSuccess,
        Func<ErrorCollection, TResult> onFailure)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Match(onSuccess, onFailure);
    }

    public static async ValueTask<TResult> MatchAsync<TValue, TResult>(
        this ValueTask<Result<TValue>> resultTask,
        Func<TValue, TResult> onSuccess,
        Func<ErrorCollection, TResult> onFailure)
    {
        Result<TValue> result = await resultTask.ConfigureAwait(false);
        return result.Match(onSuccess, onFailure);
    }

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

    public static async Task<Result<TOut>> Select<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        ArgumentNullException.ThrowIfNull(selector);
        Result<TIn> result = await resultTask.ConfigureAwait(false);
        return result.Map(selector);
    }

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
}
