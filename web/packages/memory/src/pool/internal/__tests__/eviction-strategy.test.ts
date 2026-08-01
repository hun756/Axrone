import { describe, expect, it } from 'vitest';
import {
    NoneEvictionStrategy,
    LruEvictionStrategy,
    TtlEvictionStrategy,
    FifoEvictionStrategy,
    createEvictionStrategy,
    type EvictionContext,
} from '../eviction-strategy';
import type { PoolSlot, PoolableObject } from '../../pool-support';

function makeSlot(status: 'free' | 'allocated', lastAccessed: number, createdAt: number): PoolSlot<PoolableObject> {
    return {
        obj: status === 'allocated' ? { reset() {} } : undefined,
        status,
        lastAccessed,
        allocCount: 0,
        createdAt,
    };
}

describe('EvictionStrategy', () => {
    describe('NoneEvictionStrategy', () => {
        it('always returns null', () => {
            const strategy = new NoneEvictionStrategy();
            expect(strategy.policy).toBe('none');
            const ctx: EvictionContext<PoolableObject> = {
                slots: [makeSlot('allocated', 100, 50)],
                now: 200,
            };
            expect(strategy.findEvictableSlot(ctx)).toBeNull();
        });
    });

    describe('LruEvictionStrategy', () => {
        it('returns slot with least recent access', () => {
            const strategy = new LruEvictionStrategy();
            expect(strategy.policy).toBe('lru');
            const ctx: EvictionContext<PoolableObject> = {
                slots: [
                    makeSlot('allocated', 100, 50),
                    makeSlot('allocated', 50, 30),
                    makeSlot('allocated', 200, 80),
                ],
                now: 300,
            };
            expect(strategy.findEvictableSlot(ctx)).toBe(1);
        });

        it('returns null when no allocated slots', () => {
            const strategy = new LruEvictionStrategy();
            const ctx: EvictionContext<PoolableObject> = {
                slots: [makeSlot('free', 100, 50)],
                now: 300,
            };
            expect(strategy.findEvictableSlot(ctx)).toBeNull();
        });

        it('returns null for empty slots', () => {
            const strategy = new LruEvictionStrategy();
            const ctx: EvictionContext<PoolableObject> = { slots: [], now: 0 };
            expect(strategy.findEvictableSlot(ctx)).toBeNull();
        });
    });

    describe('TtlEvictionStrategy', () => {
        it('returns first expired slot', () => {
            const strategy = new TtlEvictionStrategy(100);
            expect(strategy.policy).toBe('ttl');
            const ctx: EvictionContext<PoolableObject> = {
                slots: [
                    makeSlot('allocated', 250, 50),
                    makeSlot('allocated', 50, 30),
                ],
                now: 300,
            };
            expect(strategy.findEvictableSlot(ctx)).toBe(1);
        });

        it('returns null when no slots expired', () => {
            const strategy = new TtlEvictionStrategy(1000);
            const ctx: EvictionContext<PoolableObject> = {
                slots: [makeSlot('allocated', 250, 50)],
                now: 300,
            };
            expect(strategy.findEvictableSlot(ctx)).toBeNull();
        });

        it('returns null when ttl <= 0', () => {
            const strategy = new TtlEvictionStrategy(0);
            const ctx: EvictionContext<PoolableObject> = {
                slots: [makeSlot('allocated', 0, 0)],
                now: 1000,
            };
            expect(strategy.findEvictableSlot(ctx)).toBeNull();
        });
    });

    describe('FifoEvictionStrategy', () => {
        it('returns slot with oldest createdAt', () => {
            const strategy = new FifoEvictionStrategy();
            expect(strategy.policy).toBe('fifo');
            const ctx: EvictionContext<PoolableObject> = {
                slots: [
                    makeSlot('allocated', 200, 100),
                    makeSlot('allocated', 100, 30),
                    makeSlot('allocated', 300, 200),
                ],
                now: 400,
            };
            expect(strategy.findEvictableSlot(ctx)).toBe(1);
        });

        it('returns null when no allocated slots', () => {
            const strategy = new FifoEvictionStrategy();
            const ctx: EvictionContext<PoolableObject> = {
                slots: [makeSlot('free', 100, 50)],
                now: 300,
            };
            expect(strategy.findEvictableSlot(ctx)).toBeNull();
        });
    });

    describe('createEvictionStrategy()', () => {
        it('creates LRU strategy', () => {
            expect(createEvictionStrategy('lru', 0)).toBeInstanceOf(LruEvictionStrategy);
        });

        it('creates TTL strategy', () => {
            expect(createEvictionStrategy('ttl', 100)).toBeInstanceOf(TtlEvictionStrategy);
        });

        it('creates FIFO strategy', () => {
            expect(createEvictionStrategy('fifo', 0)).toBeInstanceOf(FifoEvictionStrategy);
        });

        it('creates None strategy for unknown policy', () => {
            expect(createEvictionStrategy('none', 0)).toBeInstanceOf(NoneEvictionStrategy);
        });
    });
});
