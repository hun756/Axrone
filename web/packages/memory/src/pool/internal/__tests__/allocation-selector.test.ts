import { describe, expect, it } from 'vitest';
import { AllocationSelector } from '../allocation-selector';
import type { PoolSlot, PoolableObject } from '../../pool-support';

function makeFreeSlot(): PoolSlot<PoolableObject> {
    return { obj: undefined, status: 'free', lastAccessed: 0, allocCount: 0, createdAt: 0 };
}

describe('AllocationSelector', () => {
    describe('first-available', () => {
        it('returns first free slot id', () => {
            const slots = [makeFreeSlot(), makeFreeSlot(), makeFreeSlot()];
            const freeList = new Set([0, 1, 2]);
            const selector = new AllocationSelector(slots, freeList, null);
            expect(selector.pickNextFreeId('first-available')).toBe(0);
        });

        it('returns -1 when freeList is empty', () => {
            const slots: PoolSlot<PoolableObject>[] = [];
            const freeList = new Set<number>();
            const selector = new AllocationSelector(slots, freeList, null);
            expect(selector.pickNextFreeId('first-available')).toBe(-1);
        });
    });

    describe('round-robin', () => {
        it('cycles through free slots', () => {
            const slots = [makeFreeSlot(), makeFreeSlot(), makeFreeSlot()];
            const freeList = new Set([0, 1, 2]);
            const selector = new AllocationSelector(slots, freeList, null);
            const first = selector.pickNextFreeId('round-robin');
            const second = selector.pickNextFreeId('round-robin');
            const third = selector.pickNextFreeId('round-robin');
            expect(new Set([first, second, third]).size).toBe(3);
        });

        it('wraps around after exhausting slots', () => {
            const slots = [makeFreeSlot(), makeFreeSlot()];
            const freeList = new Set([0, 1]);
            const selector = new AllocationSelector(slots, freeList, null);
            const first = selector.pickNextFreeId('round-robin');
            selector.pickNextFreeId('round-robin');
            const third = selector.pickNextFreeId('round-robin');
            expect(third).toBe(first);
        });
    });

    describe('reset()', () => {
        it('resets round-robin index', () => {
            const slots = [makeFreeSlot(), makeFreeSlot()];
            const freeList = new Set([0, 1]);
            const selector = new AllocationSelector(slots, freeList, null);
            selector.pickNextFreeId('round-robin');
            selector.reset();
            const afterReset = selector.pickNextFreeId('round-robin');
            expect(afterReset).toBe(0);
        });
    });

    describe('empty free list', () => {
        it('returns -1 for all strategies', () => {
            const slots: PoolSlot<PoolableObject>[] = [];
            const freeList = new Set<number>();
            const selector = new AllocationSelector(slots, freeList, null);
            expect(selector.pickNextFreeId('first-available')).toBe(-1);
            expect(selector.pickNextFreeId('round-robin')).toBe(-1);
            expect(selector.pickNextFreeId('least-recently-used')).toBe(-1);
            expect(selector.pickNextFreeId('most-recently-used')).toBe(-1);
        });
    });
});
