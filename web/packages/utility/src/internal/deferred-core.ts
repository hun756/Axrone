/**
 * Shared deferred-computation engine.
 *
 * Provides the core state-machine for lazy/deferred initialization that is
 * shared between `@axrone/memory` lazy implementations and `@axrone/utility`
 * singleton implementations. Both systems use the same fundamental pattern:
 *
 *   uninitialized -> computing/initializing -> resolved | faulted
 *
 * This module extracts that common pattern to prevent drift between the two
 * packages and ensure consistent behavior for circular-dependency detection,
 * error handling, and factory lifecycle.
 *
 * @internal This module is an internal implementation detail. Public APIs
 *           live in `memory/src/lazy/` and `utility/src/singleton/`.
 */

// ---------------------------------------------------------------------------
// State types
// ---------------------------------------------------------------------------

/** Core deferred-computation states shared by sync lazy and singleton. */
export type DeferredSyncState = 'uninitialized' | 'computing' | 'resolved' | 'faulted';

/** Core deferred-computation states shared by async lazy and singleton. */
export type DeferredAsyncState = 'uninitialized' | 'computing' | 'resolved' | 'faulted';

/** Mutable state container for synchronous deferred computation. */
export interface DeferredSyncStateContainer<T> {
    state: DeferredSyncState;
    hasValue: boolean;
    exception: Error | null;
    value: T;
    factory: (() => T) | null;
}

/** Mutable state container for asynchronous deferred computation. */
export interface DeferredAsyncStateContainer<T> {
    state: DeferredAsyncState;
    hasValue: boolean;
    exception: Error | null;
    value: T;
    factory: (() => Promise<T>) | null;
    promise: Promise<T> | null;
}

// ---------------------------------------------------------------------------
// Options
// ---------------------------------------------------------------------------

/** Options for customizing synchronous deferred computation. */
export interface ComputeSyncOptions {
    /** Override the circular-dependency error (default: generic Error). */
    circularError?: () => Error;
    /** Wrap a caught error before storing it (e.g. SingletonError wrapping). */
    wrapError?: (error: Error) => Error;
    /** Hook called just before the factory is invoked. */
    beforeCompute?: () => void;
    /** Hook called after successful computation with the result. */
    afterCompute?: (value: unknown) => void;
}

/** Options for customizing asynchronous deferred computation. */
export interface ComputeAsyncOptions {
    /** Override the circular-dependency error (default: generic Error). */
    circularError?: () => Error;
    /** Wrap a caught error before storing it (e.g. SingletonError wrapping). */
    wrapError?: (error: Error) => Error;
    /** Hook called just before the factory is invoked. */
    beforeCompute?: () => void;
    /** Hook called after successful computation with the result. */
    afterCompute?: (value: unknown) => void;
    /**
     * When true, keeps the rejected promise cached after failure so subsequent
     * calls return the same rejection without re-invoking the factory (lazy
     * semantics). When false (default), nulls the promise on failure so the
     * next call re-executes the factory (singleton / retry semantics).
     */
    cacheRejectedPromise?: boolean;
}

// ---------------------------------------------------------------------------
// Sync deferred computation
// ---------------------------------------------------------------------------

/**
 * Create a fresh synchronous deferred-computation state container.
 */
export function createDeferredSyncState<T>(factory: () => T): DeferredSyncStateContainer<T> {
    return {
        state: 'uninitialized',
        hasValue: false,
        exception: null,
        value: undefined as T,
        factory,
    };
}

/**
 * Execute the core synchronous deferred-computation state machine.
 *
 * Returns the computed value. Throws on circular dependency or factory error.
 * The state container is mutated in place.
 */
export function computeDeferredSync<T>(
    ctx: DeferredSyncStateContainer<T>,
    options?: ComputeSyncOptions
): T {
    // Fast path: already resolved
    if (ctx.hasValue) return ctx.value;

    // Fast path: already faulted
    if (ctx.exception) throw ctx.exception;

    // Circular dependency guard
    if (ctx.state === 'computing') {
        throw options?.circularError?.() ?? new Error('Circular dependency detected in deferred evaluation');
    }

    // Transition to computing
    ctx.state = 'computing';
    options?.beforeCompute?.();

    try {
        const result = ctx.factory!();
        ctx.value = result;
        ctx.hasValue = true;
        ctx.state = 'resolved';
        ctx.factory = null;
        options?.afterCompute?.(result);
        return result;
    } catch (error) {
        const raw = error instanceof Error ? error : new Error(String(error));
        const wrapped = options?.wrapError?.(raw) ?? raw;
        ctx.exception = wrapped;
        ctx.state = 'faulted';
        throw wrapped;
    }
}

/**
 * Reset a synchronous deferred-computation state container so it can be
 * re-computed from the original factory.
 */
export function resetDeferredSync<T>(
    ctx: DeferredSyncStateContainer<T>,
    originalFactory: () => T
): void {
    ctx.value = undefined as T;
    ctx.hasValue = false;
    ctx.exception = null;
    ctx.state = 'uninitialized';
    ctx.factory = originalFactory;
}

// ---------------------------------------------------------------------------
// Async deferred computation
// ---------------------------------------------------------------------------

/**
 * Create a fresh asynchronous deferred-computation state container.
 */
export function createDeferredAsyncState<T>(factory: () => Promise<T>): DeferredAsyncStateContainer<T> {
    return {
        state: 'uninitialized',
        hasValue: false,
        exception: null,
        value: undefined as T,
        factory,
        promise: null,
    };
}

/**
 * Execute the core asynchronous deferred-computation state machine.
 *
 * Returns a promise for the computed value. The promise is cached so that
 * concurrent callers share the same computation. The state container is
 * mutated in place.
 */
export function computeDeferredAsync<T>(
    ctx: DeferredAsyncStateContainer<T>,
    options?: ComputeAsyncOptions
): Promise<T> {
    // If we already have a pending promise, reuse it
    if (ctx.promise) return ctx.promise;

    // Fast path: already resolved
    if (ctx.hasValue) return Promise.resolve(ctx.value);

    // Fast path: already faulted
    if (ctx.exception) return Promise.reject(ctx.exception);

    // Transition to computing
    ctx.state = 'computing';
    options?.beforeCompute?.();

    ctx.promise = ctx.factory!()
        .then((result) => {
            ctx.value = result;
            ctx.hasValue = true;
            ctx.state = 'resolved';
            ctx.factory = null;
            options?.afterCompute?.(result);
            return result;
        })
        .catch((error) => {
            const raw = error instanceof Error ? error : new Error(String(error));
            const wrapped = options?.wrapError?.(raw) ?? raw;
            ctx.exception = wrapped;
            ctx.state = 'faulted';
            if (!options?.cacheRejectedPromise) {
                ctx.promise = null; // Allow retry on next access (singleton semantics)
            }
            // When cacheRejectedPromise is true, promise stays set (lazy semantics)
            throw wrapped;
        });

    return ctx.promise;
}

/**
 * Reset an asynchronous deferred-computation state container so it can be
 * re-computed from the original factory.
 */
export function resetDeferredAsync<T>(
    ctx: DeferredAsyncStateContainer<T>,
    originalFactory: () => Promise<T>
): void {
    ctx.value = undefined as T;
    ctx.hasValue = false;
    ctx.exception = null;
    ctx.state = 'uninitialized';
    ctx.factory = originalFactory;
    ctx.promise = null;
}
