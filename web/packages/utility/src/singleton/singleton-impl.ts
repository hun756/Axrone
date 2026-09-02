import type {
    ISingleton,
    ISingletonMetadata,
    SingletonKey,
    SingletonState,
    SingletonOptions,
    SingletonDisposer,
    SingletonCore,
} from './singleton-core';
import { __singleton_brand, __singleton_state_brand } from './singleton-core';
import { SingletonError } from './singleton-errors';
import { computeDeferredSync } from '../internal/deferred-core';

const UNINITIALIZED: SingletonState = 'uninitialized';
const COMPUTING: SingletonState = 'computing';
const RESOLVED: SingletonState = 'resolved';
const DISPOSING: SingletonState = 'disposing';
const DISPOSED: SingletonState = 'disposed';
const FAULTED: SingletonState = 'faulted';

let keyCounter = 0;

function generateKey(): SingletonKey {
    return Symbol(`singleton_${++keyCounter}`);
}

export class SingletonImpl<T> implements ISingleton<T> {
    readonly [__singleton_brand] = true as const;
    readonly [__singleton_state_brand]!: 'SingletonCore';

    readonly key: SingletonKey;

    state: SingletonState = UNINITIALIZED;
    hasValue = false;
    exception: Error | null = null;
    createdAt: number | null = null;
    disposedAt: number | null = null;
    accessCount = 0;
    value!: T;
    factory: (() => T) | null;

    private readonly originalFactory: () => T;
    private readonly disposer: SingletonDisposer<T> | null;
    private disposePromise: Promise<void> | null = null;

    constructor(factory: () => T, options: SingletonOptions<T> = {}) {
        this.key = options.key ?? generateKey();
        this.factory = factory;
        this.originalFactory = factory;
        this.disposer = options.disposer ?? null;

        if (options.lazy !== true) {
            this.initialize();
        }
    }

    get instance(): T {
        return this.getInstance();
    }

    get isCreated(): boolean {
        return this.hasValue;
    }

    get isFaulted(): boolean {
        return this.state === FAULTED;
    }

    get isDisposed(): boolean {
        return this.state === DISPOSED;
    }

    get metadata(): Readonly<ISingletonMetadata<T>> {
        return {
            state: this.state,
            hasValue: this.hasValue,
            exception: this.exception,
            createdAt: this.createdAt,
            disposedAt: this.disposedAt,
            accessCount: this.accessCount,
            value: this.value,
            factory: this.factory,
        };
    }

    getInstance(): T {
        if (this.state === DISPOSED) {
            throw SingletonError.disposed(this.key);
        }

        if (!this.hasValue) {
            this.initialize();
        }

        this.accessCount++;
        return this.value;
    }

    tryGetInstance(): T | null {
        if (this.state === DISPOSED || this.state === FAULTED || this.state === COMPUTING) {
            return null;
        }

        if (!this.hasValue) {
            try {
                this.initialize();
            } catch {
                return null;
            }
        }

        this.accessCount++;
        return this.value;
    }

    reset(): void {
        if (this.state === DISPOSED) {
            throw SingletonError.disposed(this.key);
        }

        if (this.state === DISPOSING) {
            throw SingletonError.invalidOperation('Cannot reset while disposing', this.key);
        }

        this.value = undefined as T;
        this.hasValue = false;
        this.exception = null;
        this.createdAt = null;
        this.accessCount = 0;
        this.state = UNINITIALIZED;
        this.factory = this.originalFactory;
    }

    dispose(): void {
        if (this.state === DISPOSED) {
            return;
        }

        if (this.state === DISPOSING) {
            return;
        }

        this.state = DISPOSING;

        try {
            if (this.hasValue && this.disposer) {
                const result = this.disposer(this.value);
                if (result instanceof Promise) {
                    throw SingletonError.invalidOperation(
                        'Disposer returned a Promise. Use disposeAsync() instead.',
                        this.key
                    );
                }
            }
        } catch (error) {
            this.state = RESOLVED;
            throw SingletonError.disposeFailed(
                this.key,
                error instanceof Error ? error : new Error(String(error))
            );
        }

        this.finalizeDispose();
    }

    async disposeAsync(): Promise<void> {
        if (this.state === DISPOSED) {
            return;
        }

        if (this.disposePromise) {
            return this.disposePromise;
        }

        if (this.state === DISPOSING) {
            return;
        }

        this.state = DISPOSING;

        this.disposePromise = this.performAsyncDispose();
        return this.disposePromise;
    }

    private async performAsyncDispose(): Promise<void> {
        try {
            if (this.hasValue && this.disposer) {
                await this.disposer(this.value);
            }
        } catch (error) {
            this.state = RESOLVED;
            this.disposePromise = null;
            throw SingletonError.disposeFailed(
                this.key,
                error instanceof Error ? error : new Error(String(error))
            );
        }

        this.finalizeDispose();
        this.disposePromise = null;
    }

    private finalizeDispose(): void {
        this.value = undefined as T;
        this.hasValue = false;
        this.factory = null;
        this.state = DISPOSED;
        this.disposedAt = Date.now();
    }

    private initialize(): void {
        if (this.hasValue) {
            return;
        }

        if (this.state === DISPOSED) {
            throw SingletonError.disposed(this.key);
        }

        computeDeferredSync(this, {
            circularError: () =>
                SingletonError.invalidOperation(
                    'Circular dependency detected during initialization',
                    this.key
                ),
            wrapError: (err) => SingletonError.initializationFailed(this.key, err),
            afterCompute: () => {
                this.createdAt = Date.now();
            },
        });
    }
}
