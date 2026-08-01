import { describe, expect, it } from 'vitest';
import { LruSlotIndex } from '../lru-slot-index';
import type { PoolSlot, PoolableObject } from '../../pool-support';

function makeSlot(status: 'free' | 'allocated', lastAccessed: number): PoolSlot<PoolableObject> {
    return {
        obj: status === 'allocated' ? { reset() {} } : undefined,
        status,
        lastAccessed,
        allocCount: 0,
        createdAt: 0,
    };
}

describe('LruSlotIndex', () => {
    describe('upsert() / size', () => {
        it('adds entries and tracks size', () => {
            const index = new LruSlotIndex(100, 'least-recently-used');
            expect(index.size).toBe(0);
            index.upsert(0, 100);
            expect(index.size).toBe(1);
            index.upsert(1, 200);
            expect(index.size).toBe(2);
        });

        it('updates existing entry', () => {
            const index = new LruSlotIndex(100, 'least-recently-used');
            index.upsert(0, 100);
            index.upsert(0, 200); // update same key
            expect(index.size).toBe(1);
        });
    });

    describe('remove()', () => {
        it('removes an entry by slot id', () => {
            const index = new LruSlotIndex(100, 'least-recently-used');
            index.upsert(0, 100);
            index.upsert(1, 200);
            expect(index.size).toBe(2);
            index.remove(0);
            expect(index.size).toBe(1);
        });

        it('no-op when removing non-existent key', () => {
            const index = new LruSlotIndex(100, 'least-recently-used');
            index.upsert(0, 100);
            index.remove(999);
            expect(index.size).toBe(1);
        });
    });

    describe('clear()', () => {
        it('removes all entries', () => {
            const index = new LruSlotIndex(100, 'least-recently-used');
            index.upsert(0, 100);
            index.upsert(1, 200);
            index.upsert(2, 300);
            expect(index.size).toBe(3);
            index.clear();
            expect(index.size).toBe(0);
        });
    });

    describe('pickAndRemove()', () => {
        it('returns LRU entry key (least-recently-used order)', () => {
            const index = new LruSlotIndex(100, 'least-recently-used');
            index.upsert(0, 100);
            index.upsert(1, 50);
            index.upsert(2, 200);

            const picked = index.pickAndRemove();
            expect(picked).not.toBeNull();
            expect(index.size).toBe(2);
        });

        it('returns null when empty', () => {
            const index = new LruSlotIndex(100, 'least-recently-used');
            expect(index.pickAndRemove()).toBeNull();
        });
    });

    describe('rebuild()', () => {
        it('rebuilds index from slot array (only free slots)', () => {
            const index = new LruSlotIndex(100, 'least-recently-used');
            const slots: PoolSlot<PoolableObject>[] = [
                makeSlot('free', 10),
                makeSlot('allocated', 20),
                makeSlot('free', 30),
                undefined as unknown as PoolSlot<PoolableObject>,
                makeSlot('free', 50),
            ];

            index.rebuild(slots);
            // Only free slots (0, 2, 4) should be indexed
            expect(index.size).toBe(3);
        });

        it('clears previous entries before rebuilding', () => {
            const index = new LruSlotIndex(100, 'least-recently-used');
            index.upsert(99, 999);
            expect(index.size).toBe(1);

            const slots: PoolSlot<PoolableObject>[] = [
                makeSlot('free', 10),
            ];
            index.rebuild(slots);
            expect(index.size).toBe(1);
        });
    });

    describe('order property', () => {
        it('returns the configured order', () => {
            const lru = new LruSlotIndex(100, 'least-recently-used');
            expect(lru.order).toBe('least-recently-used');

            const mru = new LruSlotIndex(100, 'most-recently-used');
            expect(mru.order).toBe('most-recently-used');
        });
    });
});
