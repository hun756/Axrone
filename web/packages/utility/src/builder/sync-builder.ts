import { $state, $node } from './types';
import type {
    DeepReadonly,
    Path,
    PathValue,
    ValueUpdater,
    SyncValidator,
    SyncHook,
    ValidationIssue,
    RequiredKeys,
} from './types';
import { assertSafeKey, MissingPropertyError, SchemaValidationError } from './errors';
import { StateNode, DeltaKind, fastDeepFreeze, parsePath } from './core';
import type { DeltaRecord } from './core';
import { AsyncBuilder } from './async-builder';

export class Builder<TTarget extends object, in out TSupplied extends keyof TTarget = never> {
    protected readonly [$state]: Record<string, unknown>;
    protected readonly [$node]: StateNode;
    protected readonly validators: readonly SyncValidator<TTarget>[];
    protected readonly beforeHooks: readonly SyncHook<Partial<TTarget>>[];
    protected readonly afterHooks: readonly SyncHook<TTarget>[];
    protected readonly shouldFreeze: boolean;

    constructor(
        seed: Record<string, unknown> = {},
        node: StateNode = new StateNode(null, null),
        validators: readonly SyncValidator<TTarget>[] = [],
        beforeHooks: readonly SyncHook<Partial<TTarget>>[] = [],
        afterHooks: readonly SyncHook<TTarget>[] = [],
        shouldFreeze: boolean = false
    ) {
        this[$state] = seed;
        this[$node] = node;
        this.validators = validators;
        this.beforeHooks = beforeHooks;
        this.afterHooks = afterHooks;
        this.shouldFreeze = shouldFreeze;
    }

    public set<K extends keyof TTarget>(
        key: K,
        valueOrUpdater: TTarget[K] | ValueUpdater<TTarget[K]>
    ): Builder<TTarget, TSupplied | K> {
        assertSafeKey(String(key));

        let delta: DeltaRecord;
        if (typeof valueOrUpdater === 'function') {
            const snap = this.peek();
            const resolved = (valueOrUpdater as ValueUpdater<TTarget[K]>)(snap[key]);
            delta = { kind: DeltaKind.DIRECT, key, value: resolved };
        } else {
            delta = { kind: DeltaKind.DIRECT, key, value: valueOrUpdater };
        }

        return new Builder<TTarget, TSupplied | K>(
            this[$state],
            new StateNode(this[$node], delta),
            this.validators,
            this.beforeHooks,
            this.afterHooks,
            this.shouldFreeze
        );
    }

    public setPath<P extends Path<TTarget>>(
        path: P,
        value: PathValue<TTarget, P>
    ): Builder<
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
        const delta: DeltaRecord = { kind: DeltaKind.PATH, segments, value };

        return new Builder<TTarget, TSupplied | KeyType>(
            this[$state],
            new StateNode(this[$node], delta),
            this.validators,
            this.beforeHooks,
            this.afterHooks,
            this.shouldFreeze
        );
    }

    public merge<P extends Partial<TTarget>>(
        partial: P
    ): Builder<TTarget, TSupplied | (keyof P & keyof TTarget)> {
        const keys = Object.keys(partial);
        for (let i = 0; i < keys.length; i++) {
            assertSafeKey(keys[i]!);
        }

        const delta: DeltaRecord = {
            kind: DeltaKind.MERGE,
            partial: Object.assign({}, partial) as Record<PropertyKey, unknown>,
        };

        return new Builder<TTarget, TSupplied | (keyof P & keyof TTarget)>(
            this[$state],
            new StateNode(this[$node], delta),
            this.validators,
            this.beforeHooks,
            this.afterHooks,
            this.shouldFreeze
        );
    }

    public mutate(mutator: (draft: Partial<TTarget>) => void): Builder<TTarget, TSupplied> {
        const delta: DeltaRecord = {
            kind: DeltaKind.MUTATION,
            fn: mutator as (draft: Record<string, unknown>) => void,
        };

        return new Builder<TTarget, TSupplied>(
            this[$state],
            new StateNode(this[$node], delta),
            this.validators,
            this.beforeHooks,
            this.afterHooks,
            this.shouldFreeze
        );
    }

    public validateWith(validator: SyncValidator<TTarget>): this {
        (this.validators as SyncValidator<TTarget>[]).push(validator);
        return this;
    }

    public beforeBuild(hook: SyncHook<Partial<TTarget>>): this {
        (this.beforeHooks as SyncHook<Partial<TTarget>>[]).push(hook);
        return this;
    }

    public afterBuild(hook: SyncHook<TTarget>): this {
        (this.afterHooks as SyncHook<TTarget>[]).push(hook);
        return this;
    }

    public freeze(): this {
        (this as { shouldFreeze: boolean }).shouldFreeze = true;
        return this;
    }

    public peek(): Readonly<Partial<TTarget>> {
        return Object.freeze(this[$node].materialize(this[$state])) as Readonly<Partial<TTarget>>;
    }

    public clone(): Builder<TTarget, TSupplied> {
        return new Builder<TTarget, TSupplied>(
            this[$state],
            this[$node],
            this.validators.slice(),
            this.beforeHooks.slice(),
            this.afterHooks.slice(),
            this.shouldFreeze
        );
    }

    public when(
        predicate: boolean | ((current: Readonly<Partial<TTarget>>) => boolean),
        thenBranch: (builder: this) => this,
        elseBranch?: (builder: this) => this
    ): this {
        const condition = typeof predicate === 'function' ? predicate(this.peek()) : predicate;
        if (condition) {
            return thenBranch(this);
        } else if (elseBranch !== undefined) {
            return elseBranch(this);
        }
        return this;
    }

    public build(this: Builder<TTarget, RequiredKeys<TTarget>>): DeepReadonly<TTarget> {
        return this.executeMaterialization(true);
    }

    public buildUnsafe(): DeepReadonly<TTarget> {
        return this.executeMaterialization(false);
    }

    public toAsync(): AsyncBuilder<TTarget, TSupplied> {
        return new AsyncBuilder<TTarget, TSupplied>(
            this[$state],
            this[$node],
            this.validators,
            this.beforeHooks,
            this.afterHooks,
            this.shouldFreeze
        );
    }

    protected executeMaterialization(enforceRequired: boolean): DeepReadonly<TTarget> {
        let intermediate = this[$node].materialize(this[$state]) as Partial<TTarget>;

        for (let i = 0; i < this.beforeHooks.length; i++) {
            const transformed = this.beforeHooks[i]!(intermediate);
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
            const result = this.validators[i]!(candidate);
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
            const hooked = this.afterHooks[i]!(finalInstance);
            if (hooked !== undefined && hooked !== null) {
                finalInstance = hooked;
            }
        }

        return this.shouldFreeze ? fastDeepFreeze(finalInstance) : (finalInstance as DeepReadonly<TTarget>);
    }
}
