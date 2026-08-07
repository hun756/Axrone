import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { VirtualClock } from '../virtual-clock';
import {
    ClockError,
    ClockNotRunningError,
    ClockAlreadyRunningError,
    ClockOverflowError,
} from '../clock-error';

describe('VirtualClock', () => {
    let clock: VirtualClock;

    beforeEach(() => {
        vi.useFakeTimers();
        clock = new VirtualClock({ clockId: 'test-virtual' });
    });

    afterEach(async () => {
        await clock[Symbol.asyncDispose]();
        vi.useRealTimers();
    });

    describe('construction', () => {
        it('should create with default options', () => {
            const c = new VirtualClock();
            expect(c.clockId).toBeTruthy();
            expect(c.startTimeNs).toBe(0n);
            expect(c.startWallTimeMs).toBeGreaterThan(0);
            expect(c.timeScale).toBe(1);
            expect(c.maxTimeNs).toBeGreaterThan(0n);
        });

        it('should accept custom options', () => {
            const c = new VirtualClock({
                clockId: 'custom',
                initialTimeNs: 1000n,
                initialWallTimeMs: 50000,
                timeScale: 2,
                precision: 'high',
                maxTimeNs: 1_000_000n,
            });
            expect(c.clockId).toBe('custom');
            expect(c.startTimeNs).toBe(1000n);
            expect(c.startWallTimeMs).toBe(50000);
            expect(c.timeScale).toBe(2);
            expect(c.maxTimeNs).toBe(1_000_000n);
            c[Symbol.asyncDispose]();
        });
    });

    describe('now()', () => {
        it('should return startTimeNs when stopped', () => {
            const now = clock.now() as unknown as bigint;
            expect(now).toBe(clock.startTimeNs);
        });

        it('should advance time when running', () => {
            clock.start();
            vi.advanceTimersByTime(100);
            const now = clock.now() as unknown as bigint;
            expect(now).toBeGreaterThan(clock.startTimeNs);
        });

        it('should apply timeScale factor', () => {
            const c1 = new VirtualClock({ clockId: 'scaled', timeScale: 2 });
            c1.start();
            vi.advanceTimersByTime(100);
            const scaled = c1.now() as unknown as bigint;

            const c2 = new VirtualClock({ clockId: 'normal' });
            c2.start();
            vi.advanceTimersByTime(100);
            const normal = c2.now() as unknown as bigint;

            expect(scaled).toBeGreaterThan(normal);
            c1[Symbol.asyncDispose]();
            c2[Symbol.asyncDispose]();
        });

        it('should throw when disposed', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.now()).toThrow(ClockError);
        });
    });

    describe('lifecycle', () => {
        it('should start and transition to running state', () => {
            clock.start();
            expect(clock.getState()).toBe('running');
        });

        it('should throw when starting twice', () => {
            clock.start();
            expect(() => clock.start()).toThrow(ClockAlreadyRunningError);
        });

        it('should stop and transition to stopped state', () => {
            clock.start();
            clock.stop();
            expect(clock.getState()).toBe('stopped');
        });

        it('should throw when stopping if not running', () => {
            expect(() => clock.stop()).toThrow(ClockNotRunningError);
        });

        it('should pause and resume', () => {
            clock.start();
            clock.pause();
            expect(clock.getState()).toBe('paused');

            clock.resume();
            expect(clock.getState()).toBe('running');
        });

        it('should throw pause if not running', () => {
            expect(() => clock.pause()).toThrow(ClockNotRunningError);
        });

        it('should throw resume if not paused', () => {
            expect(() => clock.resume()).toThrow(ClockError);
        });

        it('should complete full lifecycle', () => {
            clock.start();
            clock.pause();
            clock.resume();
            clock.stop();
            expect(clock.getState()).toBe('stopped');
        });
    });

    describe('time manipulation', () => {
        it('setTime should update current time', () => {
            clock.setTime(5000n);
            const now = clock.now() as unknown as bigint;
            expect(now).toBe(5000n);
        });

        it('setTime should throw on overflow', () => {
            expect(() => clock.setTime(clock.maxTimeNs + 1n)).toThrow(ClockOverflowError);
        });

        it('advanceBy should increase time', () => {
            clock.advanceBy(100n);
            const now = clock.now() as unknown as bigint;
            expect(now).toBe(100n);
        });

        it('advanceBy should throw on overflow', () => {
            expect(() => clock.advanceBy(clock.maxTimeNs + 1n)).toThrow(ClockOverflowError);
        });

        it('setTimeScale should reject non-positive values', () => {
            expect(() => clock.setTimeScale(0)).toThrow(ClockError);
            expect(() => clock.setTimeScale(-1)).toThrow(ClockError);
        });

        it('setTimeScale should update scale', () => {
            clock.setTimeScale(3);
            expect(clock.timeScale).toBe(3);
        });
    });

    describe('sample() and elapsed()', () => {
        it('sample should return ClockSnapshot', () => {
            const snapshot = clock.sample();
            expect(snapshot.timePoint.clockId).toBe('test-virtual');
            expect(snapshot.timePoint.timestampNs).toBeDefined();
            expect(snapshot.timePoint.wallTimeMs).toBeGreaterThan(0);
            expect(snapshot.elapsedSinceStart.nanoseconds).toBeDefined();
        });

        it('elapsed should return Duration', () => {
            const dur = clock.elapsed();
            expect(dur.nanoseconds).toBe(0n);
            expect(dur.milliseconds).toBe(0);
        });

        it('elapsedSince should compute difference from a TimePoint', () => {
            const point = { clockId: 'test-virtual' as string, timestampNs: 100n };
            const dur = clock.elapsedSince(point);
            expect(dur.nanoseconds).toBe(-100n);
        });
    });

    describe('ticks and metadata', () => {
        it('should track ticks', () => {
            clock.ticks(3);
            const metrics = clock.getMetrics();
            expect(metrics.totalTicks).toBe(3n);
        });

        it('getMetadata should return frozen object with clock config', () => {
            const meta = clock.getMetadata();
            expect(meta.clockId).toBe('test-virtual');
            expect(meta.sourceType).toBe('virtual');
            expect(meta.startTimeNs).toBe(0n);
            expect(meta.isMonotonic).toBe(true);
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
            expect(clock.getPrecision()).toBe('ultra');
        });
    });

    describe('async dispose', () => {
        it('should reset state after disposal', async () => {
            clock.start();
            await clock[Symbol.asyncDispose]();
            expect(() => clock.now()).toThrow(ClockError);
        });

        it('should allow multiple dispose calls', async () => {
            await clock[Symbol.asyncDispose]();
            await clock[Symbol.asyncDispose]();
        });
    });

    describe('error handling edge cases', () => {
        it('should throw on disposed setTime', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.setTime(100n)).toThrow(ClockError);
        });

        it('should throw on disposed advanceBy', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.advanceBy(100n)).toThrow(ClockError);
        });

        it('should throw on disposed setTimeScale', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.setTimeScale(2)).toThrow(ClockError);
        });

        it('should throw on disposed start', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.start()).toThrow(ClockError);
        });

        it('should throw on disposed sample', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.sample()).toThrow(ClockError);
        });

        it('should throw on disposed stop', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.stop()).toThrow(ClockError);
        });

        it('should throw on disposed pause', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.pause()).toThrow(ClockError);
        });

        it('should throw on disposed resume', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.resume()).toThrow(ClockError);
        });

        it('should throw on disposed elapsed (indirect via now())', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.elapsed()).toThrow(ClockError);
        });

        it('should throw on disposed elapsedSince (indirect via now())', async () => {
            await clock[Symbol.asyncDispose]();
            const point = { clockId: 'test-virtual' as string, timestampNs: 0n };
            expect(() => clock.elapsedSince(point)).toThrow(ClockError);
        });

        it('should throw on disposed getMetrics (indirect via now())', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.getMetrics()).toThrow(ClockError);
        });
    });
});
