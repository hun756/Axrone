import { $state, $node } from './types';
import type {
    DeepReadonly,
    Path,
    PathValue,
    AsyncValueResolver,
    AnyValidator,
    AsyncHook,
    ValidationIssue,
    RequiredKeys,
} from './types';
import { assertSafeKey, MissingPropertyError, SchemaValidationError } from './errors';
import { StateNode, DeltaKind, fastDeepFreeze, parsePath, setPathInTarget } from './core';
import type { DeltaRecord } from './core';

export class AsyncBuilder<TTarget extends object, in out TSupplied extends keyof TTarget = never> {
    protected readonly [$state]: Record<string, unknown>;
    protected readonly [$node]: StateNode;
    protected readonly asyncResolvers: Map<keyof TTarget, AsyncValueResolver<unknown>>;
    protected readonly asyncPathResolvers: Map<string, AsyncValueResolver<unknown>>;
    protected readonly validators: readonly AnyValidator<TTarget>[];
    protected readonly beforeHooks: readonly AsyncHook<Partial<TTarget>>[];
    protected readonly afterHooks: readonly AsyncHook<TTarget>[];
    protected readonly shouldFreeze: boolean;

    constructor(
        seed: Record<string, unknown> = {},
        node: StateNode = new StateNode(null, null),
        validators: readonly AnyValidator<TTarget>[] = [],
        beforeHooks: readonly AsyncHook<Partial<TTarget>>[] = [],
        afterHooks: readonly AsyncHook<TTarget>[] = [],
        shouldFreeze: boolean = false,
        asyncResolvers: Map<keyof TTarget, AsyncValueResolver<unknown>> = new Map(),
        asyncPathResolvers: Map<string, AsyncValueResolver<unknown>> = new Map()
    ) {
        this[$state] = seed;
        this[$node] = node;
        this.validators = validators;
        this.beforeHooks = beforeHooks;
        this.afterHooks = afterHooks;
        this.shouldFreeze = shouldFreeze;
        this.asyncResolvers = asyncResolvers;
        this.asyncPathResolvers = asyncPathResolvers;
    }

    public set<K extends keyof TTarget>(
        key: K,
        valueOrResolver:
            | TTarget[K]
            | Promise<TTarget[K]>
            | AsyncValueResolver<TTarget[K]>
            | ((prev: TTarget[K] | undefined) => TTarget[K] | Promise<TTarget[K]>)
    ): AsyncBuilder<TTarget, TSupplied | K> {
        assertSafeKey(String(key));

        const nextResolvers = new Map(this.asyncResolvers);

        if (typeof valueOrResolver === 'function') {
            nextResolvers.set(key, () => {
                const snap = this.peek();
                return (valueOrResolver as Function)(snap[key]);
            });
            return new AsyncBuilder<TTarget, TSupplied | K>(
                this[$state],
                this[$node],
                this.validators,
                this.beforeHooks,
                this.afterHooks,
                this.shouldFreeze,
                nextResolvers,
                this.asyncPathResolvers
            );
        }

        if (valueOrResolver instanceof Promise) {
            nextResolvers.set(key, () => valueOrResolver);
            return new AsyncBuilder<TTarget, TSupplied | K>(
                this[$state],
                this[$node],
                this.validators,
                this.beforeHooks,
                this.afterHooks,
                this.shouldFreeze,
                nextResolvers,
                this.asyncPathResolvers
            );
        }

        nextResolvers.delete(key);
        const delta: DeltaRecord = { kind: DeltaKind.DIRECT, key, value: valueOrResolver };

        return new AsyncBuilder<TTarget, TSupplied | K>(
            this[$state],
            new StateNode(this[$node], delta),
            this.validators,
            this.beforeHooks,
            this.afterHooks,
            this.shouldFreeze,
            nextResolvers,
            this.asyncPathResolvers
        );
    }

    public setPath<P extends Path<TTarget>>(
        path: P,
        valueOrResolver:
            | PathValue<TTarget, P>
            | Promise<PathValue<TTarget, P>>
            | AsyncValueResolver<PathValue<TTarget, P>>
    ): AsyncBuilder<
        TTarget,
        TSupplied |
            (P extends `${infer K}.${string}`
                ? K extends keyof TTarget
                    ? K
                    : never
                : P extends keyof TTarget
                  ? P
                  : never)
    > {
        type KeyType = P extends `${infer K}.${string}`
            ? K extends keyof TTarget
                ? K
                : never
            : P extends keyof TTarget
              ? P
              : never;

        const segments = parsePath(path);
        const nextPathResolvers = new Map(this.asyncPathResolvers);

        if (typeof valueOrResolver === 'function' || valueOrResolver instanceof Promise) {
            nextPathResolvers.set(
                path,
                typeof valueOrResolver === 'function'
                    ? (valueOrResolver as AsyncValueResolver<unknown>)
                    : () => valueOrResolver
            );
            return new AsyncBuilder<TTarget, TSupplied | KeyType>(
                this[$state],
                this[$node],
                this.validators,
                this.beforeHooks,
                this.afterHooks,
                this.shouldFreeze,
                this.asyncResolvers,
                nextPathResolvers
            );
        }

        nextPathResolvers.delete(path);
        const delta: DeltaRecord = { kind: DeltaKind.PATH, segments, value: valueOrResolver };

        return new AsyncBuilder<TTarget, TSupplied | KeyType>(
            this[$state],
            new StateNode(this[$node], delta),
            this.validators,
            this.beforeHooks,
            this.afterHooks,
            this.shouldFreeze,
            this.asyncResolvers,
            nextPathResolvers
        );
    }

    public merge(partial: Partial<TTarget> | Promise<Partial<TTarget>>): AsyncBuilder<TTarget, TSupplied> {
        if (partial instanceof Promise) {
            const nextResolvers = new Map(this.asyncResolvers);
            const syntheticKey = Symbol('merge') as unknown as keyof TTarget;
            nextResolvers.set(syntheticKey, async () => partial);
            return new AsyncBuilder<TTarget, TSupplied>(
                this[$state],
                this[$node],
                this.validators,
                this.beforeHooks,
                this.afterHooks,
                this.shouldFreeze,
                nextResolvers,
                this.asyncPathResolvers
            );
        }

        const delta: DeltaRecord = {
            kind: DeltaKind.MERGE,
            partial: Object.assign({}, partial) as Record<PropertyKey, unknown>,
        };

        return new AsyncBuilder<TTarget, TSupplied>(
            this[$state],
            new StateNode(this[$node], delta),
            this.validators,
            this.beforeHooks,
            this.afterHooks,
            this.shouldFreeze,
            this.asyncResolvers,
            this.asyncPathResolvers
        );
    }

    public validateWith(validator: AnyValidator<TTarget>): this {
        (this.validators as AnyValidator<TTarget>[]).push(validator);
        return this;
    }

    public beforeBuild(hook: AsyncHook<Partial<TTarget>>): this {
        (this.beforeHooks as AsyncHook<Partial<TTarget>>[]).push(hook);
        return this;
    }

    public afterBuild(hook: AsyncHook<TTarget>): this {
        (this.afterHooks as AsyncHook<TTarget>[]).push(hook);
        return this;
    }

    public freeze(): this {
        (this as { shouldFreeze: boolean }).shouldFreeze = true;
        return this;
    }

    public peek(): Readonly<Partial<TTarget>> {
        return Object.freeze(this[$node].materialize(this[$state])) as Readonly<Partial<TTarget>>;
    }

    public async build(this: AsyncBuilder<TTarget, RequiredKeys<TTarget>>): Promise<DeepReadonly<TTarget>> {
        return this.executeMaterialization(true);
    }

    public async buildUnsafe(): Promise<DeepReadonly<TTarget>> {
        return this.executeMaterialization(false);
    }

    protected async executeMaterialization(enforceRequired: boolean): Promise<DeepReadonly<TTarget>> {
        let intermediate = this[$node].materialize(this[$state]) as Partial<TTarget>;

        if (this.asyncResolvers.size > 0) {
            const entries = Array.from(this.asyncResolvers.entries());
            const tasks = entries.map(async ([k, resolver]) => {
                const res = await resolver();
                return [k, res] as const;
            });

            const resolved = await Promise.all(tasks);
            for (let i = 0; i < resolved.length; i++) {
                const [k, val] = resolved[i]!;
                if (typeof k === 'symbol') {
                    Object.assign(intermediate, val);
                } else {
                    intermediate[k] = val as TTarget[keyof TTarget];
                }
            }
        }

        if (this.asyncPathResolvers.size > 0) {
            const pathEntries = Array.from(this.asyncPathResolvers.entries());
            const tasks = pathEntries.map(async ([path, resolver]) => {
                const res = await resolver();
                return [path, res] as const;
            });

            const resolvedPaths = await Promise.all(tasks);
            for (let i = 0; i < resolvedPaths.length; i++) {
                const [path, val] = resolvedPaths[i]!;
                setPathInTarget(intermediate as Record<string, unknown>, parsePath(path), val);
            }
        }

        for (let i = 0; i < this.beforeHooks.length; i++) {
            const transformed = await this.beforeHooks[i]!(intermediate);
            if (transformed !== undefined && transformed !== null) {
                intermediate = transformed;
            }
        }

        if (enforceRequired) {
            const missing: string[] = [];
            const keys = Object.keys(this[$state]);
            for (let i = 0; i < keys.length; i++) {
                const k = keys[i]!;
                if (intermediate[k as keyof TTarget] === undefined) {
                    missing.push(k);
                }
            }
            if (missing.length > 0) {
                throw new MissingPropertyError(missing);
            }
        }

        const candidate = intermediate as TTarget;
        const issues: ValidationIssue[] = [];

        for (let i = 0; i < this.validators.length; i++) {
            const result = await this.validators[i]!(candidate);
            if (!result.ok) {
                for (let j = 0; j < result.issues.length; j++) {
                    issues.push(result.issues[j]!);
                }
            }
        }

        if (issues.length > 0) {
            throw new SchemaValidationError(issues);
        }

        let finalInstance = candidate;
        for (let i = 0; i < this.afterHooks.length; i++) {
            const hooked = await this.afterHooks[i]!(finalInstance);
            if (hooked !== undefined && hooked !== null) {
                finalInstance = hooked;
            }
        }

        return this.shouldFreeze ? fastDeepFreeze(finalInstance) : (finalInstance as DeepReadonly<TTarget>);
    }
}
