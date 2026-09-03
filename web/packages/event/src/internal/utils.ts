import type { UnsubscribeFn } from '../definition';
import type { IEventPublisher } from '../interfaces';

/**
 * Type guard: is `value` a thenable?
 *
 * Accepts both real `Promise`s and any object with a `.then` method
 * (the Promise/A+ duck-type). Used by `EventEmitter.emitSync` to detect
 * async listeners in the synchronous fast path.
 */
export function isPromiseLike<T = unknown>(value: unknown): value is PromiseLike<T> {
    return (
        (typeof value === 'object' || typeof value === 'function') &&
        value !== null &&
        typeof (value as PromiseLike<T>).then === 'function'
    );
}

/**
 * Coerce an arbitrary thrown value into an `Error`.
 *
 * Used everywhere the runtime gives us `unknown` from a `catch` clause
 * (`EventEmitter` dispatch, scheduler task failure, batch dispatch).
 * Coercing once at the boundary keeps error handling sites free of
 * `instanceof` checks.
 */
export function toError(error: unknown): Error {
    return error instanceof Error ? error : new Error(String(error));
}

/**
 * Surface an async failure so it cannot be silently swallowed.
 *
 * `await` chains in JS swallow rejections unless the caller attaches a
 * `.catch`. For an event emitter that means a faulty listener in a
 * `void promise` / `detachPromise` path can otherwise vanish. This
 * helper schedules a `throw` on the microtask queue, which is the only
 * way to get the host runtime to actually report the rejection
 * (`unhandledrejection` in browsers, process-level crash in Node).
 */
export function rethrowAsync(error: unknown): void {
    const failure = toError(error);

    if (typeof queueMicrotask === 'function') {
        queueMicrotask(() => {
            throw failure;
        });
        return;
    }

    void Promise.resolve().then(() => {
        throw failure;
    });
}

/**
 * Subscribe to `event` on `source` and forward every payload to
 * `target.emit(targetEvent, …)`.
 *
 * Shared by `EventEmitter.pipe` and `NamespacedEmitter.pipe` (inside
 * `extras.ts`). Each call site supplies its own `subscribe` closure so
 * the helper stays agnostic to whether the source is the full
 * `IEventEmitter` or a remapped view. `EventGroup.pipe` is inherited
 * from `ForwardingEmitter` and just delegates to the underlying emitter,
 * so it does not need this helper directly.
 *
 * The returned `UnsubscribeFn` cancels the source subscription, which
 * is exactly the same as cancelling the pipe.
 */
export function pipeToEmitter(
    subscribe: (callback: (data: any) => unknown) => UnsubscribeFn,
    target: IEventPublisher<any>,
    targetEvent: string
): UnsubscribeFn {
    return subscribe((data) => target.emit(targetEvent as any, data).then(() => undefined));
}
