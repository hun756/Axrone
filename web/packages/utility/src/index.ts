// Result monad
export * from './result';

// Other utilities
export * from './disposable';
export * from './freeze';
export * from './object';
export * from './types';

// Message catalog helper
export * from './message-catalog';

// Submodules
export * from './clone/index';
export * from './comparer/index';
export * from './base64/index';
export * from './builder/index';

// Singleton (selective exports to avoid conflicts with result.ts and types.ts)
export {
    __singleton_brand,
    __async_singleton_brand,
    __scoped_singleton_brand,
    __singleton_state_brand,
} from './singleton/index';
export type {
    SingletonState,
    SingletonLifecycle,
    SingletonKey,
    ISingletonMetadata,
    IAsyncSingletonMetadata,
    IScopedSingletonMetadata,
    SingletonCore,
    AsyncSingletonCore,
    ScopedSingletonCore,
    ISingleton,
    IAsyncSingleton,
    IScopedSingleton,
    ScopeDisposer,
    ISingletonScope,
    ISingletonRegistry,
    SingletonDisposer,
    SingletonOptions,
    AsyncSingletonOptions,
    ExtractSingletonType,
    IsSingletonType,
    IsAsyncSingletonType,
    IsScopedSingletonType,
} from './singleton/index';
export {
    SingletonImpl,
    AsyncSingletonImpl,
    ScopedSingletonImpl,
    SingletonScopeImpl,
    createRootScope,
    SingletonRegistryImpl,
    getGlobalRegistry,
    resetGlobalRegistry,
    resetGlobalRegistryAsync,
    SingletonError,
    SingletonErrorCode,
    create,
    createAsync,
    createScoped,
    createLazy,
    createLazyAsync,
    fromValue,
    createRegistered,
    createRegisteredAsync,
    isSingleton,
    isAsyncSingleton,
    isScopedSingleton,
    isAnySingleton,
    resolve,
    tryResolve,
    map,
    mapAsync,
    combineAsync,
} from './singleton/index';
