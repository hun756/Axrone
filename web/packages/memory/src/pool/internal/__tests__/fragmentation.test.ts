import { describe, expect, it } from 'vitest';
import { FragmentationAnalyzer } from '../fragmentation';
import type { PoolSlot, PoolableObject } from '../../pool-support';

describe('FragmentationAnalyzer', () => {
    describe('calculate()', () => {
        it('returns 0 for empty slots', () => {
            expect(FragmentationAnalyzer.calculate([])).toBe(0);
        });

        it('returns 0 for all allocated', () => {
            const slots: PoolSlot<PoolableObject>[] = [
                { obj: { reset() {} }, status: 'allocated', lastAccessed: 0, allocCount: 0, createdAt: 0 },
                { obj: { reset() {} }, status: 'allocated', lastAccessed: 0, allocCount: 0, createdAt: 0 },
            ];
            expect(FragmentationAnalyzer.calculate(slots)).toBe(0);
        });

        it('returns 0 for all free', () => {
            const slots: PoolSlot<PoolableObject>[] = [
                { obj: undefined, status: 'free', lastAccessed: 0, allocCount: 0, createdAt: 0 },
                { obj: undefined, status: 'free', lastAccessed: 0, allocCount: 0, createdAt: 0 },
            ];
            expect(FragmentationAnalyzer.calculate(slots)).toBe(0);
        });

        it('calculates fragmentation with holes', () => {
            const slots: (PoolSlot<PoolableObject> | undefined)[] = [
                { obj: { reset() {} }, status: 'allocated', lastAccessed: 0, allocCount: 0, createdAt: 0 },
                undefined,
                { obj: { reset() {} }, status: 'allocated', lastAccessed: 0, allocCount: 0, createdAt: 0 },
                { obj: undefined, status: 'free', lastAccessed: 0, allocCount: 0, createdAt: 0 },
            ];
            const frag = FragmentationAnalyzer.calculate(slots);
            expect(frag).toBeGreaterThan(0);
            expect(frag).toBeLessThan(1);
        });

        it('handles mixed free and allocated', () => {
            const slots: PoolSlot<PoolableObject>[] = [
                { obj: { reset() {} }, status: 'allocated', lastAccessed: 0, allocCount: 0, createdAt: 0 },
                { obj: undefined, status: 'free', lastAccessed: 0, allocCount: 0, createdAt: 0 },
                { obj: { reset() {} }, status: 'allocated', lastAccessed: 0, allocCount: 0, createdAt: 0 },
            ];
            const frag = FragmentationAnalyzer.calculate(slots);
            expect(frag).toBeGreaterThan(0);
        });
    });

    describe('estimateMemoryUsageBytes()', () => {
        it('estimates with minimal params', () => {
            const usage = FragmentationAnalyzer.estimateMemoryUsageBytes({
                slotCount: 10,
                freeListSize: 5,
                lruIndexSize: 0,
                waitQueueLength: 0,
                estimatedObjectSize: undefined,
                slotHasObjectCount: 5,
            });
            expect(usage).toBeGreaterThan(0);
        });

        it('includes object size when provided', () => {
            const usage1 = FragmentationAnalyzer.estimateMemoryUsageBytes({
                slotCount: 10,
                freeListSize: 5,
                lruIndexSize: 0,
                waitQueueLength: 0,
                estimatedObjectSize: 100,
                slotHasObjectCount: 5,
            });
            const usage2 = FragmentationAnalyzer.estimateMemoryUsageBytes({
                slotCount: 10,
                freeListSize: 5,
                lruIndexSize: 0,
                waitQueueLength: 0,
                estimatedObjectSize: 200,
                slotHasObjectCount: 5,
            });
            expect(usage2).toBeGreaterThan(usage1);
        });

        it('includes LRU index size', () => {
            const usage1 = FragmentationAnalyzer.estimateMemoryUsageBytes({
                slotCount: 10,
                freeListSize: 5,
                lruIndexSize: 0,
                waitQueueLength: 0,
                estimatedObjectSize: undefined,
                slotHasObjectCount: 5,
            });
            const usage2 = FragmentationAnalyzer.estimateMemoryUsageBytes({
                slotCount: 10,
                freeListSize: 5,
                lruIndexSize: 10,
                waitQueueLength: 0,
                estimatedObjectSize: undefined,
                slotHasObjectCount: 5,
            });
            expect(usage2).toBeGreaterThan(usage1);
        });

        it('includes wait queue length', () => {
            const usage1 = FragmentationAnalyzer.estimateMemoryUsageBytes({
                slotCount: 10,
                freeListSize: 5,
                lruIndexSize: 0,
                waitQueueLength: 0,
                estimatedObjectSize: undefined,
                slotHasObjectCount: 5,
            });
            const usage2 = FragmentationAnalyzer.estimateMemoryUsageBytes({
                slotCount: 10,
                freeListSize: 5,
                lruIndexSize: 0,
                waitQueueLength: 10,
                estimatedObjectSize: undefined,
                slotHasObjectCount: 5,
            });
            expect(usage2).toBeGreaterThan(usage1);
        });
    });
});
