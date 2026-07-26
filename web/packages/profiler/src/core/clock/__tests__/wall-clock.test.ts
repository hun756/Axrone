import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { WallClock } from '../wall-clock';
import { ClockError, ClockNotRunningError, ClockAlreadyRunningError } from '../clock-error';

describe('WallClock', () => {
    let clock: WallClock;

    beforeEach(() => {
        vi.useFakeTimers();
        vi.setSystemTime(1_000_000);
        clock = new WallClock({ clockId: 'test-wall' });
    });

    afterEach(async () => {
        await clock[Symbol.asyncDispose]();
        vi.useRealTimers();
    });

    describe('construction', () => {
        it('should create with default options', () => {
            const c = new WallClock();
            expect(c.clockId).toBeTruthy();
            expect(c.resolutionHz).toBe(1000);
            expect(c.maxSkewMs).toBe(100);
            c[Symbol.asyncDispose]();
        });

        it('should accept custom options', () => {
            const c = new WallClock({
                clockId: 'custom-wall',
                resolutionHz: 500,
                maxSkewMs: 50,
                precision: 'low',
            });
            expect(c.clockId).toBe('custom-wall');
            expect(c.resolutionHz).toBe(500);
            expect(c.maxSkewMs).toBe(50);
            c[Symbol.asyncDispose]();
        });

        it('should set startTimeNs based on Date.now()', () => {
            const c = new WallClock();
            const expected = BigInt(1_000_000) * 1_000_000n;
            expect(c.startTimeNs).toBe(expected);
            c[Symbol.asyncDispose]();
        });
    });

    describe('now()', () => {
        it('should return wall time in Milliseconds', () => {
            const t = clock.now() as unknown as number;
            expect(t).toBe(1_000_000);
        });

        it('should advance with system time', () => {
            clock.now();
            vi.advanceTimersByTime(1000);
            const t2 = clock.now() as unknown as number;
            expect(t2).toBe(1_001_000);
        });

        it('should throw when disposed', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.now()).toThrow(ClockError);
        });
    });

    describe('skew detection', () => {
        it('should detect large time jumps as skew', () => {
            clock.now();
            vi.setSystemTime(1_000_500);
            clock.now();
            const metrics = clock.getMetrics();
            expect(metrics.maxDriftNs).toBeGreaterThan(0n);
        });

        it('should not flag small time differences as skew', () => {
            clock.now();
            vi.advanceTimersByTime(1);
            clock.now();
            const metrics = clock.getMetrics();
            expect(metrics.maxDriftNs).toBe(0n);
        });
    });

    describe('lifecycle', () => {
        it('should start and transition to running', () => {
            clock.start();
            expect(clock.getState()).toBe('running');
        });

        it('should throw when starting twice', () => {
            clock.start();
            expect(() => clock.start()).toThrow(ClockAlreadyRunningError);
        });

        it('should stop and transition to stopped', () => {
            clock.start();
            clock.stop();
            expect(clock.getState()).toBe('stopped');
        });

        it('should throw when stopping if not running', () => {
            expect(() => clock.stop()).toThrow(ClockNotRunningError);
        });

        it('should pause when running', () => {
            clock.start();
            clock.pause();
            expect(clock.getState()).toBe('paused');
        });

        it('should throw pause if not running', () => {
            expect(() => clock.pause()).toThrow(ClockNotRunningError);
        });

        it('should resume from paused state', () => {
            clock.start();
            clock.pause();
            clock.resume();
            expect(clock.getState()).toBe('running');
        });

        it('should throw resume if not paused', () => {
            expect(() => clock.resume()).toThrow(ClockError);
        });
    });

    describe('sample() and elapsed()', () => {
        it('sample should return ClockSnapshot', () => {
            clock.start();
            const snapshot = clock.sample();
            expect(snapshot.timePoint.clockId).toBe('test-wall');
            expect(snapshot.timePoint.wallTimeMs).toBe(1_000_000);
            expect(snapshot.elapsedSinceStart).toBeDefined();
        });

        it('elapsed should return Duration', () => {
            const dur = clock.elapsed();
            expect(dur.nanoseconds).toBeDefined();
            expect(dur.milliseconds).toBeGreaterThanOrEqual(0);
        });

        it('elapsedSince should compute difference', () => {
            const point = {
                clockId: 'test-wall' as string,
                timestampNs: BigInt(1_000_000) * 1_000_000n,
            };
            const dur = clock.elapsedSince(point);
            expect(dur.nanoseconds).toBeDefined();
        });
    });

    describe('unix conversion', () => {
        it('toUnixMs should return current wall time', () => {
            expect(clock.toUnixMs()).toBe(1_000_000);
        });

        it('toUnixNs should return current wall time in nanoseconds', () => {
            const ns = clock.toUnixNs();
            expect(ns).toBe(BigInt(1_000_000) * 1_000_000n);
        });
    });

    describe('ticks and metadata', () => {
        it('should track ticks', () => {
            clock.ticks(7);
            const metrics = clock.getMetrics();
            expect(metrics.totalTicks).toBe(7n);
        });

        it('getMetadata should return frozen object', () => {
            const meta = clock.getMetadata();
            expect(meta.clockId).toBe('test-wall');
            expect(meta.sourceType).toBe('wall');
            expect(meta.isMonotonic).toBe(false);
            expect(Object.isFrozen(meta)).toBe(true);
        });

        it('getMetrics should return frozen state', () => {
            const metrics = clock.getMetrics();
            expect(metrics.state).toBe('stopped');
            expect(Object.isFrozen(metrics)).toBe(true);
        });

        it('getState should return current state', () => {
            expect(clock.getState()).toBe('stopped');
            clock.start();
            expect(clock.getState()).toBe('running');
        });

        it('getPrecision should return configured precision', () => {
            expect(clock.getPrecision()).toBe('medium');
        });
    });

    describe('async dispose', () => {
        it('should reset state after disposal', async () => {
            clock.start();
            await clock[Symbol.asyncDispose]();
            expect(() => clock.now()).toThrow(ClockError);
        });
    });
});
