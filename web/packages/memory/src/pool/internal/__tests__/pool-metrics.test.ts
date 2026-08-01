import { describe, expect, it } from 'vitest';
import { PoolMetricsCollector } from '../pool-metrics';
import { MemoryPoolError } from '../../pool-support';

describe('PoolMetricsCollector', () => {
    describe('enabled mode', () => {
        it('records allocations', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordAllocation(true, true);
            collector.recordAllocation(true, false);
            collector.recordAllocation(false, false);
            expect(collector.internal.allocations).toBe(3);
            expect(collector.internal.fastPath).toBe(2);
            expect(collector.internal.hits).toBe(1);
            expect(collector.internal.slowPath).toBe(1);
            expect(collector.internal.misses).toBe(1);
        });

        it('records releases', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordRelease();
            collector.recordRelease();
            expect(collector.internal.releases).toBe(2);
        });

        it('records creation time', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordCreation(5);
            collector.recordCreation(10);
            expect(collector.internal.creations).toBe(2);
            expect(collector.internal.creationTimer.count).toBe(2);
            expect(collector.internal.creationTimer.total).toBe(15);
            expect(collector.internal.creationTimer.min).toBe(5);
            expect(collector.internal.creationTimer.max).toBe(10);
            expect(collector.internal.creationTimer.last).toBe(10);
        });

        it('records allocation time', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordAllocationTime(3);
            collector.recordAllocationTime(7);
            expect(collector.internal.allocationTimer.count).toBe(2);
            expect(collector.internal.allocationTimer.min).toBe(3);
            expect(collector.internal.allocationTimer.max).toBe(7);
        });

        it('records release time', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordReleaseTime(2);
            collector.recordReleaseTime(4);
            expect(collector.internal.releaseTimer.count).toBe(2);
            expect(collector.internal.releaseTimer.min).toBe(2);
            expect(collector.internal.releaseTimer.max).toBe(4);
        });

        it('records compaction time', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordCompactionTime(10);
            expect(collector.internal.compactions).toBe(1);
            expect(collector.internal.compactionTimer.last).toBe(10);
        });

        it('records resize time', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordResizeTime(15);
            expect(collector.internal.resizeTimer.last).toBe(15);
        });

        it('records lifetime', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordLifetime(100);
            collector.recordLifetime(200);
            expect(collector.internal.objectLifetime.count).toBe(2);
            expect(collector.internal.objectLifetime.total).toBe(300);
        });

        it('records validation failures', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordValidationFailure();
            expect(collector.internal.validationFailures).toBe(1);
        });

        it('records expansion and contraction', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordExpansion();
            collector.recordContraction();
            expect(collector.internal.expansions).toBe(1);
            expect(collector.internal.contractions).toBe(1);
        });

        it('records eviction', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordEviction();
            expect(collector.internal.evictions).toBe(1);
        });

        it('records high water mark', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordHighWaterMark(10);
            collector.recordHighWaterMark(20);
            collector.recordHighWaterMark(15);
            expect(collector.internal.highWaterMark).toBe(20);
        });

        it('resetAggregate resets counters', () => {
            const collector = new PoolMetricsCollector('test', true);
            collector.recordExpansion();
            collector.recordContraction();
            collector.recordHighWaterMark(10);
            collector.resetAggregate();
            expect(collector.internal.expansions).toBe(0);
            expect(collector.internal.contractions).toBe(0);
            expect(collector.internal.highWaterMark).toBe(0);
        });

        it('snapshot returns full metrics', () => {
            const collector = new PoolMetricsCollector('test-pool', true);
            collector.recordAllocation(true, true);
            collector.recordRelease();
            const snap = collector.snapshot(100, 80, 4096, 0.1);
            expect(snap.name).toBe('test-pool');
            expect(snap.capacity).toBe(100);
            expect(snap.available).toBe(80);
            expect(snap.allocated).toBe(20);
            expect(snap.allocations).toBe(1);
            expect(snap.releases).toBe(1);
            expect(snap.fragmentationRatio).toBe(0.1);
            expect(snap.utilizationRatio).toBe(0.2);
            expect(snap.turnoverRate).toBe(1);
        });

        it('newTimer returns timer when enabled', () => {
            const collector = new PoolMetricsCollector('test', true);
            const timer = collector.newTimer();
            expect(timer).not.toBeNull();
            expect(timer!.stop()).toBeGreaterThanOrEqual(0);
        });

        it('isEnabled returns true', () => {
            const collector = new PoolMetricsCollector('test', true);
            expect(collector.isEnabled).toBe(true);
        });
    });

    describe('disabled mode', () => {
        it('all record methods are no-ops', () => {
            const collector = new PoolMetricsCollector('test', false);
            collector.recordAllocation(true, true);
            collector.recordRelease();
            collector.recordCreation(5);
            collector.recordAllocationTime(3);
            collector.recordReleaseTime(2);
            collector.recordCompactionTime(10);
            collector.recordResizeTime(15);
            collector.recordLifetime(100);
            collector.recordValidationFailure();
            collector.recordExpansion();
            collector.recordContraction();
            collector.recordEviction();
            collector.recordHighWaterMark(10);
            expect(collector.internal.allocations).toBe(0);
            expect(collector.internal.releases).toBe(0);
        });

        it('snapshot throws when disabled', () => {
            const collector = new PoolMetricsCollector('test', false);
            expect(() => collector.snapshot(100, 80, 0, 0)).toThrow(MemoryPoolError);
        });

        it('newTimer returns null', () => {
            const collector = new PoolMetricsCollector('test', false);
            expect(collector.newTimer()).toBeNull();
        });

        it('isEnabled returns false', () => {
            const collector = new PoolMetricsCollector('test', false);
            expect(collector.isEnabled).toBe(false);
        });
    });
});
