import { describe, it, expect, vi, beforeEach } from 'vitest';
import { MonotonicClock } from '../monotonic-clock';

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
    expect(elapsed.milliseconds).toBeGreaterThanOrEqual(100);
    expect(elapsed.nanoseconds).toBeGreaterThan(0n);
  });

  it('should provide a sample() that returns snapshot', () => {
    clock.start();
    const snapshot = clock.sample();
    expect(snapshot.timePoint.clockId).toBe('test');
    expect(snapshot.elapsedSinceStart.milliseconds).toBeGreaterThanOrEqual(0);
  });
});
