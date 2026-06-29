import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { LogBoundedHistogram } from '../metrics-collector';

describe('LogBoundedHistogram', () => {
    let hist: LogBoundedHistogram;

    beforeEach(() => {
        hist = new LogBoundedHistogram({ minMs: 0.1, maxMs: 10_000 });
    });

    afterEach(async () => {
        await hist[Symbol.asyncDispose]();
    });

    describe('construction', () => {
        it('should create with default options', () => {
            const h = new LogBoundedHistogram();
            expect(h.minValue()).toBe(Infinity);
            expect(h.maxValue()).toBe(-Infinity);
            h[Symbol.asyncDispose]();
        });

        it('should generate at most maxBuckets boundaries', () => {
            const h = new LogBoundedHistogram({ minMs: 0.001, maxMs: 1e100 });
            const stats = h.getStatistics();
            const buckets = stats.histogramBuckets as number[];
            expect(buckets.length).toBeLessThanOrEqual(256);
            h[Symbol.asyncDispose]();
        });
    });

    describe('record()', () => {
        it('should ignore zero or negative durations', () => {
            hist.record(0);
            hist.record(-1);
            const stats = hist.getStatistics();
            expect(stats.totalDurationMs).toBe(0n);
        });

        it('should record a single value', () => {
            hist.record(100);
            const stats = hist.getStatistics();
            expect(stats.totalDurationMs).toBeGreaterThan(0n);
            expect(stats.minMs).toBe(100);
            expect(stats.maxMs).toBe(100);
        });

        it('should track min and max values', () => {
            hist.record(50);
            hist.record(200);
            hist.record(100);

            expect(hist.minValue()).toBe(50);
            expect(hist.maxValue()).toBe(200);
        });

        it('should accumulate multiple records in the same bucket', () => {
            hist.record(100);
            hist.record(100);
            hist.record(100);
            const stats = hist.getStatistics();
            const totalMs = stats.totalDurationMs as bigint;
            expect(totalMs).toBeGreaterThanOrEqual(300n);
        });
    });

    describe('getStatistics()', () => {
        it('should return empty stats when no records', () => {
            const stats = hist.getStatistics();
            expect(stats.totalDurationMs).toBe(0n);
        });

        it('should compute mean correctly', () => {
            hist.record(100);
            hist.record(200);
            hist.record(300);
            const stats = hist.getStatistics();
            expect(stats.meanMs).toBe(200);
        });

        it('should compute percentiles correctly', () => {
            for (let i = 0; i < 100; i++) {
                hist.record(100);
            }
            const stats = hist.getStatistics();
            expect(stats.p50Ms).toBeDefined();
            expect(stats.p95Ms).toBeDefined();
            expect(stats.p99Ms).toBeDefined();
        });

        it('should return frozen result object', () => {
            hist.record(100);
            const stats = hist.getStatistics();
            expect(Object.isFrozen(stats)).toBe(true);
        });

        it('should return histogramBuckets and countsPerBucket', () => {
            hist.record(100);
            const stats = hist.getStatistics();
            expect(Array.isArray(stats.histogramBuckets)).toBe(true);
            expect(Array.isArray(stats.countsPerBucket)).toBe(true);
        });
    });

    describe('minValue / maxValue', () => {
        it('should return Infinity / -Infinity when empty', () => {
            const h = new LogBoundedHistogram();
            expect(h.minValue()).toBe(Infinity);
            expect(h.maxValue()).toBe(-Infinity);
            h[Symbol.asyncDispose]();
        });

        it('should reflect recorded extremes', () => {
            hist.record(5);
            hist.record(5000);
            expect(hist.minValue()).toBe(5);
            expect(hist.maxValue()).toBe(5000);
        });
    });

    describe('async dispose', () => {
        it('should release internal memory on dispose', async () => {
            hist.record(100);
            const statsBefore = hist.getStatistics();
            expect((statsBefore.histogramBuckets as number[]).length).toBeGreaterThan(0);
            await hist[Symbol.asyncDispose]();
            hist.record(200);
            expect(hist.minValue()).toBe(200);
        });
    });
});
