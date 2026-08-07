import { describe, expect, it } from 'vitest';
import { SlotFactory } from '../slot-factory';
import { PoolMetricsCollector } from '../pool-metrics';
import type { PoolSlot, PoolableObject } from '../../pool-support';

function createTestMetrics(enabled = true): PoolMetricsCollector {
    return new PoolMetricsCollector('test', enabled);
}

function createTestObject(): PoolableObject {
    return {
        reset() {},
        __poolId: undefined,
        __poolStatus: undefined,
        __lastAccessed: undefined,
        __allocCount: undefined,
    };
}

describe('SlotFactory', () => {
    describe('create()', () => {
        it('creates slot without pre-allocation (withObject=false)', () => {
            const metrics = createTestMetrics();
            const factory = new SlotFactory(createTestObject, metrics);
            const slot = factory.create(0, false);

            expect(slot.obj).toBeUndefined();
            expect(slot.status).toBe('free');
            expect(slot.allocCount).toBe(0);
            expect(typeof slot.lastAccessed).toBe('number');
            expect(typeof slot.createdAt).toBe('number');
        });

        it('creates slot with pre-allocation (withObject=true)', () => {
            const metrics = createTestMetrics();
            const factory = new SlotFactory(createTestObject, metrics);
            const slot = factory.create(0, true);

            expect(slot.obj).toBeDefined();
            expect(slot.obj!.reset).toBeTypeOf('function');
            expect(slot.status).toBe('free');
            expect(slot.obj!.__poolId).toBe(0);
            expect(slot.obj!.__poolStatus).toBe('free');
        });

        it('records creation time when metrics enabled and withObject=true', () => {
            const metrics = createTestMetrics(true);
            const factory = new SlotFactory(createTestObject, metrics);
            factory.create(0, true);

            expect(metrics.internal.creations).toBe(1);
            expect(metrics.internal.creationTimer.count).toBe(1);
        });

        it('does not record creation time when withObject=false', () => {
            const metrics = createTestMetrics(true);
            const factory = new SlotFactory(createTestObject, metrics);
            factory.create(0, false);

            expect(metrics.internal.creations).toBe(0);
        });
    });

    describe('preallocate()', () => {
        it('fills slots and freeList with pre-allocated objects', () => {
            const metrics = createTestMetrics();
            const factory = new SlotFactory(createTestObject, metrics);
            const slots: PoolSlot<PoolableObject>[] = [];
            const freeList = new Set<number>();

            const result = factory.preallocate(slots, freeList, 5);
            expect(result).toBe(5);
            expect(slots.length).toBe(5);
            expect(freeList.size).toBe(5);

            for (let i = 0; i < 5; i++) {
                expect(slots[i]!.obj).toBeDefined();
                expect(slots[i]!.obj!.__poolId).toBe(i);
                expect(freeList.has(i)).toBe(true);
            }
        });
    });

    describe('reserve()', () => {
        it('creates empty slots without objects', () => {
            const metrics = createTestMetrics();
            const factory = new SlotFactory(createTestObject, metrics);
            const slots: PoolSlot<PoolableObject>[] = [];
            const freeList = new Set<number>();

            const result = factory.reserve(slots, freeList, 3);
            expect(result).toBe(3);
            expect(slots.length).toBe(3);
            expect(freeList.size).toBe(3);

            for (let i = 0; i < 3; i++) {
                expect(slots[i]!.obj).toBeUndefined();
                expect(slots[i]!.status).toBe('free');
            }
        });

        it('appends to existing slots', () => {
            const metrics = createTestMetrics();
            const factory = new SlotFactory(createTestObject, metrics);
            const slots: PoolSlot<PoolableObject>[] = [
                { obj: undefined, status: 'free', lastAccessed: 0, allocCount: 0, createdAt: 0 },
            ];
            const freeList = new Set([0]);

            factory.reserve(slots, freeList, 3);
            expect(slots.length).toBe(3);
            expect(freeList.size).toBe(3);
        });
    });
});
