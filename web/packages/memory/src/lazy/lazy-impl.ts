import {
    ILazy,
    ILazyAsync,
    __lazy_brand,
    __async_brand,
    __state_brand,
    LazyState,
} from './lazy-core';
import {
    computeDeferredSync,
    computeDeferredAsync,
} from '@axrone/utility';

export class LazyImpl<T> implements ILazy<T> {
    readonly [__lazy_brand] = true as const;
    readonly [__state_brand]!: 'LazyCore';

    state: LazyState = 'uninitialized';
    hasValue = false;
    exception: Error | null = null;
    value!: T;
    factory: (() => T) | null;
    private readonly originalFactory: () => T;

    constructor(valueFactory: () => T, originalFactory?: () => T) {
        this.factory = valueFactory;
        this.originalFactory = originalFactory ?? valueFactory;
    }

    get isValueCreated(): boolean {
        return this.hasValue;
    }

    get isValueFaulted(): boolean {
        return this.exception !== null;
    }

    get Value(): T {
        return this.getValue();
    }

    get IsValueCreated(): boolean {
        return this.isValueCreated;
    }

    get IsValueFaulted(): boolean {
        return this.isValueFaulted;
    }

    private getValue(): T {
        return computeDeferredSync(this, {
            circularError: () => new Error('Circular dependency detected in lazy evaluation'),
        });
    }

    map<U>(selector: (value: T) => U): ILazy<U> {
        const factory = () => selector(this.getValue());
        return new LazyImpl(factory, factory);
    }

    flatMap<U>(selector: (value: T) => ILazy<U>): ILazy<U> {
        const factory = () => selector(this.getValue()).force();
        return new LazyImpl(factory, factory);
    }

    filter<U extends T>(predicate: (value: T) => value is U): ILazy<U>;
    filter(predicate: (value: T) => boolean): ILazy<T>;
    filter(predicate: (value: T) => boolean): ILazy<T> {
        const factory = () => {
            const val = this.getValue();
            if (!predicate(val)) {
                throw new Error('Predicate failed for lazy value');
            }
            return val;
        };
        return new LazyImpl(factory, factory);
    }

    orElse(fallback: () => T): ILazy<T> {
        const factory = () => {
            try {
                return this.getValue();
            } catch {
                return fallback();
            }
        };
        return new LazyImpl(factory, factory);
    }

    catch<U = T>(handler: (error: Error) => U): ILazy<T | U> {
        const factory = () => {
            try {
                return this.getValue();
            } catch (error) {
                return handler(error instanceof Error ? error : new Error(String(error)));
            }
        };
        return new LazyImpl(factory, factory);
    }

    tap(effect: (value: T) => void): ILazy<T> {
        const factory = () => {
            const val = this.getValue();
            effect(val);
            return val;
        };
        return new LazyImpl(factory, factory);
    }

    force(): T {
        return this.getValue();
    }

    reset(): ILazy<T> {
        return new LazyImpl(this.originalFactory);
    }

    toAsync(): ILazyAsync<T> {
        return new LazyAsyncImpl(() => Promise.resolve(this.getValue()));
    }
}

export class LazyAsyncImpl<T> implements ILazyAsync<T> {
    readonly [__async_brand] = true as const;
    readonly [__state_brand]!: 'LazyAsyncCore';

    state: LazyState = 'uninitialized';
    hasValue = false;
    exception: Error | null = null;
    value!: T;
    factory: (() => Promise<T>) | null;
    promise: Promise<T> | null = null;
    private readonly originalFactory: () => Promise<T>;

    constructor(promiseFactory: () => Promise<T>, originalFactory?: () => Promise<T>) {
        this.factory = promiseFactory;
        this.originalFactory = originalFactory ?? promiseFactory;
    }

    get isValueCreated(): boolean {
        return this.hasValue;
    }

    get isValueFaulted(): boolean {
        return this.exception !== null;
    }

    get Value(): Promise<T> {
        return this.getValue();
    }

    get IsValueCreated(): boolean {
        return this.isValueCreated;
    }

    get IsValueFaulted(): boolean {
        return this.isValueFaulted;
    }

    private getValue(): Promise<T> {
        return computeDeferredAsync(this, {
            cacheRejectedPromise: true, // Lazy caches rejections (unlike singleton which allows retry)
        });
    }

    map<U>(selector: (value: T) => U): ILazyAsync<U> {
        return new LazyAsyncImpl(() => this.getValue().then(selector));
    }

    mapAsync<U>(selector: (value: T) => Promise<U>): ILazyAsync<U> {
        return new LazyAsyncImpl(() => this.getValue().then(selector));
    }

    flatMap<U>(selector: (value: T) => ILazyAsync<U>): ILazyAsync<U> {
        return new LazyAsyncImpl(() => this.getValue().then((val) => selector(val).force()));
    }

    filter<U extends T>(predicate: (value: T) => value is U): ILazyAsync<U>;
    filter(predicate: (value: T) => boolean): ILazyAsync<T>;
    filter(predicate: (value: T) => boolean): ILazyAsync<T> {
        return new LazyAsyncImpl(() =>
            this.getValue().then((val) => {
                if (!predicate(val)) {
                    throw new Error('Predicate failed for async lazy value');
                }
                return val;
            })
        );
    }

    orElse(fallback: () => Promise<T>): ILazyAsync<T> {
        return new LazyAsyncImpl(() => this.getValue().catch(() => fallback()));
    }

    catch<U = T>(handler: (error: Error) => U): ILazyAsync<T | U> {
        return new LazyAsyncImpl(() =>
            this.getValue().catch((error) =>
                Promise.resolve(handler(error instanceof Error ? error : new Error(String(error))))
            )
        );
    }

    catchAsync<U = T>(handler: (error: Error) => Promise<U>): ILazyAsync<T | U> {
        return new LazyAsyncImpl(() =>
            this.getValue().catch((error) =>
                handler(error instanceof Error ? error : new Error(String(error)))
            )
        );
    }

    tap(effect: (value: T) => void): ILazyAsync<T> {
        return new LazyAsyncImpl(() =>
            this.getValue().then((val) => {
                effect(val);
                return val;
            })
        );
    }

    tapAsync(effect: (value: T) => Promise<void>): ILazyAsync<T> {
        return new LazyAsyncImpl(() =>
            this.getValue().then(async (val) => {
                await effect(val);
                return val;
            })
        );
    }

    timeout(milliseconds: number): ILazyAsync<T> {
        return new LazyAsyncImpl(() =>
            Promise.race([
                this.getValue(),
                new Promise<never>((_, reject) =>
                    setTimeout(
                        () => reject(new Error(`Operation timed out after ${milliseconds}ms`)),
                        milliseconds
                    )
                ),
            ])
        );
    }

    retry(maxAttempts: number, delay = 0): ILazyAsync<T> {
        return new LazyAsyncImpl(async () => {
            let lastError: Error;
            for (let attempt = 0; attempt <= maxAttempts; attempt++) {
                try {
                    return await this.originalFactory();
                } catch (error) {
                    lastError = error instanceof Error ? error : new Error(String(error));
                    if (attempt < maxAttempts && delay > 0) {
                        await new Promise((resolve) => setTimeout(resolve, delay));
                    }
                }
            }
            throw lastError!;
        });
    }

    force(): Promise<T> {
        return this.getValue();
    }

    reset(): ILazyAsync<T> {
        return new LazyAsyncImpl(this.originalFactory);
    }

    toLazy(): ILazy<Promise<T>> {
        return new LazyImpl(() => this.getValue());
    }
}

