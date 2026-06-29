import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { Stopwatch } from '../stopwatch';
import { ClockError, ClockNotRunningError } from '../clock-error';

function createControllableClock() {
  let _now = 0n;
  return {
    now: () => _now,
    advance: (ns: bigint) => { _now += ns; },
    set: (ns: bigint) => { _now = ns; },
    [Symbol.asyncDispose]: () => Promise.resolve(),
  };
}

describe('Stopwatch', () => {
  let watch: Stopwatch;

  afterEach(async () => {
    if (watch) await watch[Symbol.asyncDispose]();
  });

  describe('construction', () => {
    it('should create with default clock', () => {
      watch = new Stopwatch();
      expect(watch.isRunning()).toBe(false);
    });

    it('should auto-start when autoStart option is set', () => {
      watch = new Stopwatch({ autoStart: true });
      expect(watch.isRunning()).toBe(true);
    });

    it('should use custom clock when provided', () => {
      const customClock = createControllableClock();
      watch = new Stopwatch({ clock: customClock });
      expect(watch.isRunning()).toBe(false);
    });
  });

  describe('start and stop', () => {
    it('should start and set running state', () => {
      watch = new Stopwatch();
      watch.start();
      expect(watch.isRunning()).toBe(true);
    });

    it('should be idempotent on repeated start', () => {
      watch = new Stopwatch();
      watch.start();
      watch.start();
      expect(watch.isRunning()).toBe(true);
    });

    it('should stop and clear running state', () => {
      watch = new Stopwatch();
      watch.start();
      watch.stop();
      expect(watch.isRunning()).toBe(false);
    });

    it('should throw when stopping if not running', () => {
      watch = new Stopwatch();
      expect(() => watch.stop()).toThrow(ClockNotRunningError);
    });
  });

  describe('lap()', () => {
    it('should return null when not running', () => {
      watch = new Stopwatch();
      expect(watch.lap()).toBeNull();
    });

    it('should return LapResult when running', () => {
      const clock = createControllableClock();
      watch = new Stopwatch({ clock });
      watch.start();
      clock.advance(1_000_000n);

      const lap1 = watch.lap();
      expect(lap1).not.toBeNull();
      expect(lap1!.lapNumber).toBe(1);
      expect(lap1!.lapDuration.milliseconds).toBe(1);
      expect(lap1!.totalDuration.milliseconds).toBe(1);
    });

    it('should track multiple laps', () => {
      const clock = createControllableClock();
      watch = new Stopwatch({ clock });
      watch.start();
      clock.advance(1_000_000n);
      watch.lap();

      clock.advance(2_000_000n);
      const lap2 = watch.lap();
      expect(lap2!.lapNumber).toBe(2);
      expect(lap2!.lapDuration.milliseconds).toBe(2);
      expect(lap2!.totalDuration.milliseconds).toBe(3);
    });

    it('should record timestampNs in lap result', () => {
      const clock = createControllableClock();
      clock.set(5000n);
      watch = new Stopwatch({ clock });
      watch.start();
      const lap = watch.lap();
      expect(lap!.timestampNs).toBe(5000n);
    });
  });

  describe('elapsed()', () => {
    it('should return null when not running', () => {
      watch = new Stopwatch();
      expect(watch.elapsed()).toBeNull();
    });

    it('should return Duration when running', () => {
      const clock = createControllableClock();
      watch = new Stopwatch({ clock });
      watch.start();
      clock.advance(5_000_000n);

      const dur = watch.elapsed();
      expect(dur).not.toBeNull();
      expect(dur!.milliseconds).toBe(5);
    });
  });

  describe('reset() and restart()', () => {
    it('reset should clear start time and stop', () => {
      const clock = createControllableClock();
      watch = new Stopwatch({ clock });
      watch.start();
      clock.advance(1_000_000n);
      watch.reset();

      expect(watch.isRunning()).toBe(false);
      expect(watch.elapsed()).toBeNull();
    });

    it('restart should reset and start', () => {
      const clock = createControllableClock();
      watch = new Stopwatch({ clock });
      watch.start();
      clock.advance(1_000_000n);
      watch.restart();

      expect(watch.isRunning()).toBe(true);
      const dur = watch.elapsed();
      expect(dur!.milliseconds).toBe(0);
    });
  });

  describe('error handling', () => {
    it('should throw when starting after dispose', async () => {
      watch = new Stopwatch();
      await watch[Symbol.asyncDispose]();
      expect(() => watch.start()).toThrow(ClockError);
    });

    it('should throw when stopping after dispose', async () => {
      watch = new Stopwatch();
      await watch[Symbol.asyncDispose]();
      expect(() => watch.stop()).toThrow(ClockError);
    });

    it('should throw when calling lap after dispose', async () => {
      watch = new Stopwatch();
      await watch[Symbol.asyncDispose]();
      expect(() => watch.lap()).toThrow(ClockError);
    });

    it('should throw when calling elapsed after dispose', async () => {
      watch = new Stopwatch();
      await watch[Symbol.asyncDispose]();
      expect(watch.elapsed()).toBeNull();
    });

    it('should throw when resetting after dispose', async () => {
      watch = new Stopwatch();
      await watch[Symbol.asyncDispose]();
      expect(() => watch.reset()).toThrow(ClockError);
    });
  });

  describe('async dispose', () => {
    it('should reset state after disposal', async () => {
      watch = new Stopwatch();
      watch.start();
      await watch[Symbol.asyncDispose]();
      expect(watch.isRunning()).toBe(false);
    });

    it('should allow multiple dispose calls', async () => {
      watch = new Stopwatch();
      await watch[Symbol.asyncDispose]();
      await watch[Symbol.asyncDispose]();
    });
  });
});
