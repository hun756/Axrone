import type {
    DeepReadonly,
    Path,
    PathValue,
    ValueUpdater,
    SyncValidator,
    SyncHook,
    RequiredKeys,
} from './types';
import { assertSafeKey } from './errors';
import { Builder } from './sync-builder';
import type { AsyncBuilder } from './async-builder';

export type DynamicProxyBuilder<TTarget extends object, TSupplied extends keyof TTarget = never> = {
    readonly [K in keyof TTarget]-?: (
        value: TTarget[K] | ValueUpdater<TTarget[K]>
    ) => DynamicProxyBuilder<TTarget, TSupplied | K>;
} & {
    readonly state: Readonly<Partial<TTarget>>;
    set<K extends keyof TTarget>(
        key: K,
        value: TTarget[K] | ValueUpdater<TTarget[K]>
    ): DynamicProxyBuilder<TTarget, TSupplied | K>;
    setPath<P extends Path<TTarget>>(
        path: P,
        value: PathValue<TTarget, P>
    ): DynamicProxyBuilder<
        TTarget,
        TSupplied |
            (P extends `${infer K}.${string}`
                ? K extends keyof TTarget
                    ? K
                    : never
                : P extends keyof TTarget
                  ? P
                  : never)
    >;
    merge<P extends Partial<TTarget>>(
        partial: P
    ): DynamicProxyBuilder<TTarget, TSupplied | (keyof P & keyof TTarget)>;
    mutate(mutator: (draft: Partial<TTarget>) => void): DynamicProxyBuilder<TTarget, TSupplied>;
    validateWith(validator: SyncValidator<TTarget>): DynamicProxyBuilder<TTarget, TSupplied>;
    beforeBuild(hook: SyncHook<Partial<TTarget>>): DynamicProxyBuilder<TTarget, TSupplied>;
    afterBuild(hook: SyncHook<TTarget>): DynamicProxyBuilder<TTarget, TSupplied>;
    when(
        predicate: boolean | ((state: Readonly<Partial<TTarget>>) => boolean),
        thenBranch: (
            builder: DynamicProxyBuilder<TTarget, TSupplied>
        ) => DynamicProxyBuilder<TTarget, TSupplied>,
        elseBranch?: (
            builder: DynamicProxyBuilder<TTarget, TSupplied>
        ) => DynamicProxyBuilder<TTarget, TSupplied>
    ): DynamicProxyBuilder<TTarget, TSupplied>;
    freeze(): DynamicProxyBuilder<TTarget, TSupplied>;
    clone(): DynamicProxyBuilder<TTarget, TSupplied>;
    peek(): Readonly<Partial<TTarget>>;
    build(this: DynamicProxyBuilder<TTarget, RequiredKeys<TTarget>>): DeepReadonly<TTarget>;
    buildUnsafe(): DeepReadonly<TTarget>;
    toAsync(): AsyncBuilder<TTarget, TSupplied>;
};

const PROXY_HANDLER: ProxyHandler<Builder<any, any>> = {
    get(target: Builder<any, any>, prop: string | symbol, receiver: any) {
        if (typeof prop === 'symbol') {
            return Reflect.get(target, prop, receiver);
        }

        if (prop === 'state') {
            return target.peek();
        }

        const value = (target as any)[prop];
        if (typeof value === 'function') {
            return (...args: any[]) => {
                const result = value.apply(target, args);
                if (result instanceof Builder) {
                    return createDynamicProxy(result);
                }
                return result;
            };
        }

        if (prop in target) {
            return value;
        }

        assertSafeKey(prop);

        return (val: any) => {
            const nextBuilder = target.set(prop, val);
            return createDynamicProxy(nextBuilder);
        };
    },
};

export function createDynamicProxy<TTarget extends object, TSupplied extends keyof TTarget>(
    builder: Builder<TTarget, TSupplied>
): DynamicProxyBuilder<TTarget, TSupplied> {
    return new Proxy(builder, PROXY_HANDLER) as unknown as DynamicProxyBuilder<TTarget, TSupplied>;
}
