import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { MonotonicClock } from '../monotonic-clock';
import { ClockError } from '../clock-error';

describe('MonotonicClock', () => {
    let clock: MonotonicClock;

    beforeEach(() => {
        vi.useFakeTimers();
        clock = new MonotonicClock({ clockId: 'test' });
    });

    afterEach(async () => {
        await clock[Symbol.asyncDispose]();
        vi.useRealTimers();
    });

    it('should return monotonic increasing time values', () => {
        const t1 = clock.now() as unknown as bigint;
        vi.advanceTimersByTime(10);
        const t2 = clock.now() as unknown as bigint;
        expect(t2).toBeGreaterThan(t1);
    });

    it('should throw when starting twice', () => {
        clock.start();
        expect(() => clock.start()).toThrow('Clock is already running');
    });

    it('should throw when stopping a non-running clock', () => {
        expect(() => clock.stop()).toThrow('Clock is not running');
    });

    it('should complete full lifecycle (start, pause, resume, stop)', () => {
        clock.start();
        expect(clock.getState()).toBe('running');

        clock.pause();
        expect(clock.getState()).toBe('paused');

        clock.resume();
        expect(clock.getState()).toBe('running');

        clock.stop();
        expect(clock.getState()).toBe('stopped');
    });

    it('should provide metadata with clock configuration', () => {
        const meta = clock.getMetadata();
        expect(meta.clockId).toBe('test');
        expect(meta.sourceType).toBe('monotonic');
        expect(meta.isMonotonic).toBe(true);
        expect(meta.resolutionHz).toBeGreaterThan(0);
    });

    it('should track ticks', () => {
        clock.ticks(5);
        const metrics = clock.getMetrics();
        expect(metrics.totalTicks).toBe(5n);
    });

    it('should produce elapsed duration snapshots', () => {
        clock.start();
        vi.advanceTimersByTime(100);
        const elapsed = clock.elapsed();
        expect(elapsed.milliseconds).toBeGreaterThanOrEqual(99);
        expect(elapsed.nanoseconds).toBeGreaterThan(0n);
    });

    it('should provide a sample() that returns snapshot', () => {
        clock.start();
        const snapshot = clock.sample();
        expect(snapshot.timePoint.clockId).toBe('test');
        expect(snapshot.elapsedSinceStart.milliseconds).toBeGreaterThanOrEqual(0);
    });

    describe('disposal guards', () => {
        it('should throw on now() after dispose', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.now()).toThrow(ClockError);
        });

        it('should throw on start() after dispose', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.start()).toThrow(ClockError);
        });

        it('should throw on stop() after dispose', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.stop()).toThrow(ClockError);
        });

        it('should throw on pause() after dispose', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.pause()).toThrow(ClockError);
        });

        it('should throw on resume() after dispose', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.resume()).toThrow(ClockError);
        });

        it('should throw on sample() after dispose', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.sample()).toThrow(ClockError);
        });

        it('should throw on waitForNextTick() after dispose', async () => {
            await clock[Symbol.asyncDispose]();
            await expect(clock.waitForNextTick()).rejects.toThrow(ClockError);
        });

        it('should throw on elapsed() after dispose (indirect via now())', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.elapsed()).toThrow(ClockError);
        });

        it('should throw on elapsedSince() after dispose (indirect via now())', async () => {
            await clock[Symbol.asyncDispose]();
            const point = { clockId: 'test' as string, timestampNs: 0n };
            expect(() => clock.elapsedSince(point)).toThrow(ClockError);
        });

        it('should throw on getMetrics() after dispose (indirect via now())', async () => {
            await clock[Symbol.asyncDispose]();
            expect(() => clock.getMetrics()).toThrow(ClockError);
        });

        it('should allow multiple dispose calls', async () => {
            await clock[Symbol.asyncDispose]();
            await expect(clock[Symbol.asyncDispose]()).resolves.toBeUndefined();
        });
    });
});
