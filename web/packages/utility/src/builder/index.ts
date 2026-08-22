// Types
export {
    $brand,
    $state,
    $node,
} from './types';
export type {
    IsNever,
    IsAny,
    IsUnknown,
    DepthCounter,
    NestedKey,
    Path,
    PathValue,
    RequiredKeys,
    OptionalKeys,
    MissingKeys,
    IsBuildable,
    ValueUpdater,
    AsyncValueResolver,
    ValidationIssue,
    ValidationResult,
    SyncValidator,
    AsyncValidator,
    AnyValidator,
    SyncHook,
    AsyncHook,
    IBuilder,
    IAsyncBuilder,
    ILens,
    FactoryProvider,
    IFactory,
} from './types';

// Errors
export {
    BuilderError,
    MissingPropertyError,
    SchemaValidationError,
    SecurityViolationError,
    assertSafeKey,
} from './errors';

// Core internals
export { StateNode, DeltaKind, parsePath, setPathInTarget, getPathInTarget, fastDeepFreeze } from './core';
export type { DeltaRecord } from './core';

// Schema
export { Schema } from './schema';

// Lens
export { Lens } from './lens';

// Builders
export { Builder } from './sync-builder';
export { AsyncBuilder } from './async-builder';

// Proxy builder
export { createDynamicProxy } from './proxy';
export type { DynamicProxyBuilder } from './proxy';

// Factory & convenience functions
export {
    defineFactory,
    createBuilder,
    createAsyncBuilder,
    createProxyBuilder,
    createSchema,
    batchBuild,
} from './factory';
