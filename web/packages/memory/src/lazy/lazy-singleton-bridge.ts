/**
 * Bridge conversions between lazy (this module) and singleton (@axrone/utility).
 *
 * These functions enable interoperability between the two deferred-computation
 * systems. They create lightweight adapters that delegate to the underlying
 * implementation while exposing the target interface.
 *
 * Note: Not all features map perfectly between the two systems:
 * - Lazy has functional combinators (map/flatMap/filter) that singleton lacks
 * - Singleton has lifecycle features (dispose/key/accessCount) that lazy lacks
 * - The bridges handle the common cases; advanced features may not translate
 *
 * @module lazy-singleton-bridge
 */

import type {
    ISingleton,
    IAsyncSingleton,
    SingletonOptions,
    AsyncSingletonOptions,
} from '@axrone/utility';
import { SingletonImpl, AsyncSingletonImpl } from '@axrone/utility';
import type { ILazy, ILazyAsync } from './lazy-core';
import { LazyImpl, LazyAsyncImpl } from './lazy-impl';

// ---------------------------------------------------------------------------
// Lazy → Singleton
// ---------------------------------------------------------------------------

/**
 * Wrap an `ILazy<T>` as an `ISingleton<T>`.
 *
 * The singleton delegates to the lazy for value computation. Disposal is a
 * no-op since lazy doesn't have lifecycle management. A key is auto-generated.
 *
 * @beta
 */
export function lazyToSingleton<T>(
    lazy: ILazy<T>,
    options?: Omit<SingletonOptions<T>, 'key' | 'lazy' | 'disposer'>
): ISingleton<T> {
    return new SingletonImpl(
        () => lazy.force(),
        { ...options, lazy: true }
    );
}

/**
 * Wrap an `ILazyAsync<T>` as an `IAsyncSingleton<T>`.
 *
 * The singleton delegates to the lazy async for value computation. Disposal
 * is a no-op since lazy doesn't have lifecycle management.
 *
 * @beta
 */
export function lazyAsyncToAsyncSingleton<T>(
    lazy: ILazyAsync<T>,
    options?: Omit<AsyncSingletonOptions<T>, 'key' | 'lazy' | 'disposer'>
): IAsyncSingleton<T> {
    return new AsyncSingletonImpl(
        () => lazy.force(),
        { ...options, lazy: true }
    );
}

// ---------------------------------------------------------------------------
// Singleton → Lazy
// ---------------------------------------------------------------------------

/**
 * Wrap an `ISingleton<T>` as an `ILazy<T>`.
 *
 * The lazy delegates to the singleton for value computation. The singleton's
 * lifecycle features (dispose, key, accessCount) are not exposed through the
 * lazy interface.
 *
 * @beta
 */
export function singletonToLazy<T>(singleton: ISingleton<T>): ILazy<T> {
    return new LazyImpl(() => singleton.getInstance());
}

/**
 * Wrap an `IAsyncSingleton<T>` as an `ILazyAsync<T>`.
 *
 * The lazy delegates to the singleton for value computation.
 *
 * @beta
 */
export function asyncSingletonToLazyAsync<T>(singleton: IAsyncSingleton<T>): ILazyAsync<T> {
    return new LazyAsyncImpl(() => singleton.getInstance());
}
