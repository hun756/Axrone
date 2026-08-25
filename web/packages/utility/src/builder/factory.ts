import type { DeepReadonly, FactoryProvider, IFactory } from './types';
import { Builder } from './sync-builder';
import { AsyncBuilder } from './async-builder';
import { Schema } from './schema';
import { createDynamicProxy } from './proxy';
import type { DynamicProxyBuilder } from './proxy';

export function defineFactory<TTarget extends object, TArgs extends readonly unknown[] = []>(
    provider: FactoryProvider<TTarget, TArgs>,
    schema?: Schema<TTarget>
): IFactory<TTarget, TArgs> {
    const resolve = (...args: TArgs): Record<string, unknown> => {
        const raw = typeof provider === 'function' ? provider(...args) : provider;
        return Object.assign({}, raw);
    };

    const createInstance = (...args: TArgs): Builder<TTarget, never> => {
        const b = new Builder<TTarget, never>(resolve(...args));
        if (schema !== undefined) {
            b.validateWith((v) => schema.validate(v));
        }
        return b;
    };

    const factory = ((...args: TArgs) => createInstance(...args)) as IFactory<TTarget, TArgs>;

    factory.create = (...args: TArgs) => createInstance(...args);

    factory.createProxy = (...args: TArgs) => {
        const core = createInstance(...args);
        return createDynamicProxy(core);
    };

    factory.createAsync = (...args: TArgs) => {
        const asyncB = new AsyncBuilder<TTarget, never>(resolve(...args));
        if (schema !== undefined) {
            asyncB.validateWith((v) => schema.validate(v));
        }
        return asyncB;
    };

    factory.build = (...args: TArgs) => createInstance(...args).buildUnsafe();

    factory.batch = (count: number, ...args: TArgs) => {
        const out: DeepReadonly<TTarget>[] = new Array(count);
        for (let i = 0; i < count; i++) {
            out[i] = createInstance(...args).buildUnsafe();
        }
        return Object.freeze(out);
    };

    return factory;
}

export function createBuilder<TTarget extends object>(
    seed?: Partial<TTarget>
): Builder<TTarget, never> {
    return new Builder<TTarget, never>(seed ? Object.assign({}, seed) : {});
}

export function createAsyncBuilder<TTarget extends object>(
    seed?: Partial<TTarget>
): AsyncBuilder<TTarget, never> {
    return new AsyncBuilder<TTarget, never>(seed ? Object.assign({}, seed) : {});
}

export function createProxyBuilder<TTarget extends object>(
    seed?: Partial<TTarget>
): DynamicProxyBuilder<TTarget, never> {
    const core = new Builder<TTarget, never>(seed ? Object.assign({}, seed) : {});
    return createDynamicProxy(core);
}

export function createSchema<TTarget extends object>(): Schema<TTarget> {
    return new Schema<TTarget>();
}

export function batchBuild<TTarget extends object, TSupplied extends keyof TTarget>(
    builders: readonly Builder<TTarget, TSupplied>[]
): readonly DeepReadonly<TTarget>[] {
    const results: DeepReadonly<TTarget>[] = new Array(builders.length);
    for (let i = 0; i < builders.length; i++) {
        results[i] = builders[i]!.buildUnsafe();
    }
    return Object.freeze(results);
}
