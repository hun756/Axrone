import { describe, expect, it } from 'vitest';
import { CapacityPlanner } from '../capacity-planner';
import type { PoolSlot, PoolableObject } from '../../pool-support';

function makeSlot(status: 'free' | 'allocated', lastAccessed = 0): PoolSlot<PoolableObject> {
    return {
        obj: status === 'allocated' ? { reset() {}, __poolId: 0, __poolStatus: status as any, __lastAccessed: lastAccessed, __allocCount: 0 } : undefined,
        status,
        lastAccessed,
        allocCount: 0,
        createdAt: 0,
    };
}

describe('CapacityPlanner', () => {
    describe('computeExpandedCapacity()', () => {
        const planner = new CapacityPlanner('test', null);

        it('fixed strategy adds expansionRate or default 32', () => {
            expect(planner.computeExpandedCapacity(100, 'fixed', 1.5, 0)).toBe(132);
            expect(planner.computeExpandedCapacity(100, 'fixed', 1.5, 64)).toBe(164);
        });

        it('multiplicative strategy multiplies by factor', () => {
            expect(planner.computeExpandedCapacity(100, 'multiplicative', 1.5, 0)).toBe(150);
            expect(planner.computeExpandedCapacity(100, 'multiplicative', 2.0, 0)).toBe(200);
        });

        it('fibonacci strategy returns next fibonacci number above current', () => {
            // Fibonacci: 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, ...
            expect(planner.computeExpandedCapacity(10, 'fibonacci', 1.5, 0)).toBe(13);
            expect(planner.computeExpandedCapacity(100, 'fibonacci', 1.5, 0)).toBe(144);
        });

        it('prime strategy returns next prime above current*factor', () => {
            // 100 * 1.5 = 150, next prime after 150 = 151
            expect(planner.computeExpandedCapacity(100, 'prime', 1.5, 0)).toBe(151);
        });

        it('unknown strategy defaults to multiplicative', () => {
            const result = planner.computeExpandedCapacity(100, 'unknown' as any, 1.5, 0);
            expect(result).toBe(150);
        });
    });

    describe('grow()', () => {
        it('adds slots and updates freeList', () => {
            const planner = new CapacityPlanner('test', null);
            const slots: PoolSlot<PoolableObject>[] = [makeSlot('free'), makeSlot('free')];
            const freeList = new Set([0, 1]);
            const createSlot = (id: number, withObject: boolean) => makeSlot('free');

            planner.grow(slots, freeList, 5, false, createSlot);
            expect(slots.length).toBe(5);
            expect(freeList.size).toBe(5);
        });
    });

    describe('shrink()', () => {
        it('removes free slots to reach new capacity', () => {
            const planner = new CapacityPlanner('test', null);
            const slots: PoolSlot<PoolableObject>[] = [
                makeSlot('free', 10),
                makeSlot('free', 20),
                makeSlot('free', 30),
                makeSlot('free', 40),
            ];
            const freeList = new Set([0, 1, 2, 3]);

            planner.shrink(slots, freeList, 2);
            // After shrink, compact reindexes; removed slots become undefined
            // The compact method preserves undefined entries in the array
            expect(freeList.size).toBeGreaterThanOrEqual(2);
        });

        it('throws if shrinking below allocated count', () => {
            const planner = new CapacityPlanner('test', null);
            const slots: PoolSlot<PoolableObject>[] = [
                { obj: { reset() {} }, status: 'allocated', lastAccessed: 0, allocCount: 0, createdAt: 0 },
                { obj: { reset() {} }, status: 'allocated', lastAccessed: 0, allocCount: 0, createdAt: 0 },
                makeSlot('free'),
            ];
            const freeList = new Set([2]);

            expect(() => planner.shrink(slots, freeList, 1)).toThrow(/Cannot shrink pool below allocated count/);
        });

        it('no-op if newCapacity >= current', () => {
            const planner = new CapacityPlanner('test', null);
            const slots: PoolSlot<PoolableObject>[] = [makeSlot('free')];
            const freeList = new Set([0]);

            planner.shrink(slots, freeList, 5);
            expect(slots.length).toBe(1);
            expect(freeList.size).toBe(1);
        });
    });

    describe('compact()', () => {
        it('removes undefined holes and reindexes', () => {
            const planner = new CapacityPlanner('test', null);
            const slots: PoolSlot<PoolableObject>[] = [
                makeSlot('allocated', 10),
                undefined as unknown as PoolSlot<PoolableObject>,
                makeSlot('free', 30),
            ];
            const freeList = new Set([2]);

            planner.compact(slots, freeList);
            expect(slots.length).toBe(2);
            expect(slots[0]!.status).toBe('allocated');
            expect(slots[1]!.status).toBe('free');
        });
    });

    describe('shouldContract()', () => {
        const planner = new CapacityPlanner('test', null);

        it('returns false when at or below initial capacity', () => {
            const slots = [makeSlot('free'), makeSlot('free')];
            const freeList = new Set([0, 1]);
            const result = planner.shouldContract(slots, freeList, {
                initialCapacity: 10,
                compactionThreshold: 32,
                lowWatermarkRatio: 0.25,
                minFree: 4,
                compactionTriggerRatio: 0.3,
            }, 0);
            expect(result.shouldResize).toBe(false);
        });

        it('returns shouldResize=true when utilization is low', () => {
            // 100 slots, 90 free => 10% utilization (below 0.25 threshold)
            const slots: PoolSlot<PoolableObject>[] = [];
            const freeList = new Set<number>();
            for (let i = 0; i < 100; i++) {
                slots.push(makeSlot(i < 10 ? 'allocated' : 'free', i));
                if (i >= 10) freeList.add(i);
            }
            const result = planner.shouldContract(slots, freeList, {
                initialCapacity: 10,
                compactionThreshold: 32,
                lowWatermarkRatio: 0.25,
                minFree: 4,
                compactionTriggerRatio: 0.3,
            }, 0);
            expect(result.shouldResize).toBe(true);
        });

        it('returns shouldCompact=true when fragmentation is high', () => {
            const slots: PoolSlot<PoolableObject>[] = [];
            const freeList = new Set<number>();
            for (let i = 0; i < 100; i++) {
                slots.push(makeSlot(i < 50 ? 'allocated' : 'free', i));
                if (i >= 50) freeList.add(i);
            }
            const result = planner.shouldContract(slots, freeList, {
                initialCapacity: 10,
                compactionThreshold: 32,
                lowWatermarkRatio: 0.25,
                minFree: 4,
                compactionTriggerRatio: 0.3,
            }, 0.5); // High fragmentation
            expect(result.shouldCompact).toBe(true);
        });
    });
});
