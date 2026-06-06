import { ILazy, ILazyAsync, ILazyFactory, __factory_brand, __state_brand } from './lazy-core';
import { LazyImpl, LazyAsyncImpl } from './lazy-impl';
import { LruMap } from '../internal/lru-map';

const TRUE = true as const;

export class LazyFactoryImpl<TArgs extends readonly unknown[], TResult>
    implements ILazyFactory<TArgs, TResult>
{
    readonly [__factory_brand] = true as const;
    readonly [__state_brand]!: 'LazyFactoryCore';

    readonly factory: (...args: TArgs) => TResult;
    readonly cache = new Map<string, TResult>();
    readonly keySelector: (...args: TArgs) => string;
    readonly maxCacheSize: number;
    readonly accessOrder: LruMap<string, true>;

    constructor(
        factory: (...args: TArgs) => TResult,
        keySelector: (...args: TArgs) => string = (...args) => JSON.stringify(args),
        maxCacheSize = Number.POSITIVE_INFINITY
    ) {
        this.factory = factory;
        this.keySelector = keySelector;
        this.maxCacheSize = maxCacheSize;
        this.accessOrder = new LruMap<string, true>({
            capacity: Number.isFinite(maxCacheSize) && maxCacheSize > 0 ? maxCacheSize : 1024,
            order: 'least-recently-used',
        });
    }

    get cacheSize(): number {
        return this.cache.size;
    }

    create(...args: TArgs): ILazy<TResult> {
        return new LazyImpl(() => this.getOrAdd(...args));
    }

    createAsync(...args: TArgs): ILazyAsync<TResult> {
        return new LazyAsyncImpl(() => Promise.resolve(this.getOrAdd(...args)));
    }

    getOrAdd(...args: TArgs): TResult {
        const key = this.keySelector(...args);

        if (this.cache.has(key)) {
            this.accessOrder.touch(key);
            return this.cache.get(key)!;
        }

        const result = this.factory(...args);

        if (this.cache.size >= this.maxCacheSize) {
            this.evictLeastRecentlyUsed();
        }

        this.cache.set(key, result);
        this.accessOrder.set(key, TRUE);

        return result;
    }

    tryGetValue(...args: TArgs): [boolean, TResult | undefined] {
        const key = this.keySelector(...args);

        if (this.cache.has(key)) {
            this.accessOrder.touch(key);
            return [true, this.cache.get(key)!];
        }

        return [false, undefined];
    }

    invalidate(...args: TArgs): boolean {
        const key = this.keySelector(...args);
        const existed = this.cache.delete(key);
        this.accessOrder.delete(key);
        return existed;
    }

    clear(): void {
        this.cache.clear();
        this.accessOrder.clear();
    }

    private evictLeastRecentlyUsed(): void {
        const oldest = this.accessOrder.pop();
        if (oldest !== undefined) {
            this.cache.delete(oldest.key);
        }
    }
}
