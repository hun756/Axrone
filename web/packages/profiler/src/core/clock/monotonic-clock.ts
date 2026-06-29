import type { ClockId, TimePoint, Duration, ClockSnapshot, ClockMetadata, ClockState, ClockMetrics, ClockPrecision } from './types';
import { TIME_CONVERSION } from './types';
import { ClockError, ClockNotRunningError, ClockAlreadyRunningError } from './clock-error';

export interface MonotonicClockOptions {
  readonly clockId?: string;
  readonly resolutionHz?: number;
  readonly precision?: ClockPrecision;
}

export class MonotonicClock implements AsyncDisposable {
  private static nextId = 0;

  readonly clockId: ClockId;
  readonly startTimeNs: bigint;
  readonly startWallTimeMs: number;
  readonly resolutionHz: number;

  private _state: ClockState = 'stopped';
  private _totalTicks = 0n;
  private _totalElapsedNs = 0n;
  private _lastTickValue: bigint | null = null;
  private _precision: ClockPrecision;
  private _disposed = false;

  constructor(options?: MonotonicClockOptions) {
    const id = options?.clockId ?? `monotonic-${++MonotonicClock.nextId}`;
    this.clockId = id as ClockId;
    this.resolutionHz = options?.resolutionHz ?? 1_000_000;
    this._precision = options?.precision ?? 'high';
    this.startTimeNs = BigInt(Math.floor(performance.now() * 1_000_000));
    this.startWallTimeMs = Date.now();
  }

  now(): Nanoseconds {
    if (this._disposed) throw new ClockError('Clock is disposed');
    const value = BigInt(Math.floor(performance.now() * 1_000_000));
    this._lastTickValue = value;
    return value as unknown as Nanoseconds;
  }

  start(): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (this._state === 'running') throw new ClockAlreadyRunningError();
    this._state = 'running';
    this._lastTickValue = BigInt(Math.floor(performance.now() * 1_000_000));
  }

  stop(): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (this._state !== 'running') throw new ClockNotRunningError();
    this._state = 'stopped';
  }

  pause(): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (this._state !== 'running') throw new ClockNotRunningError();
    this.sample();
    this._state = 'paused';
  }

  resume(): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (this._state !== 'paused') throw new ClockError('Clock is not paused');
    this._state = 'running';
    this._lastTickValue = BigInt(Math.floor(performance.now() * 1_000_000));
  }

  sample(): ClockSnapshot {
    if (this._disposed) throw new ClockError('Clock is disposed');
    const now = this.now();
    const nowNs = now as unknown as bigint;
    const elapsed = nowNs - this.startTimeNs;
    return {
      timePoint: {
        clockId: this.clockId,
        timestampNs: nowNs,
        wallTimeMs: Date.now(),
      },
      elapsedSinceStart: this.toDuration(elapsed),
    };
  }

  private toDuration(ns: bigint): Duration {
    return {
      nanoseconds: ns,
      microseconds: ns / BigInt(TIME_CONVERSION.microseconds),
      milliseconds: Number(ns / BigInt(TIME_CONVERSION.milliseconds)),
      seconds: Number(ns / BigInt(TIME_CONVERSION.seconds)),
    };
  }

  elapsed(): Duration {
    const now = this.now() as unknown as bigint;
    const elapsed = now - this.startTimeNs;
    return this.toDuration(elapsed);
  }

  elapsedSince(point: TimePoint): Duration {
    const diff = (this.now() as unknown as bigint) - point.timestampNs;
    return this.toDuration(diff);
  }

  ticks(count: number): void {
    this._totalTicks += BigInt(count);
  }

  getMetadata(): ClockMetadata {
    return Object.freeze({
      clockId: this.clockId,
      sourceType: 'monotonic',
      startTimeNs: this.startTimeNs,
      startWallTimeMs: this.startWallTimeMs,
      resolutionHz: this.resolutionHz,
      isMonotonic: true,
    });
  }

  getMetrics(): ClockMetrics {
    const elapsed = this.now() as unknown as bigint;
    const uptimeMs = Number((elapsed - this.startTimeNs) / BigInt(TIME_CONVERSION.milliseconds));
    return Object.freeze({
      totalTicks: this._totalTicks,
      totalElapsedNs: elapsed - this.startTimeNs,
      maxDriftNs: 0n,
      state: this._state,
      uptimeMs,
    });
  }

  getState(): ClockState {
    return this._state;
  }

  getPrecision(): ClockPrecision {
    return this._precision;
  }

  async waitForNextTick(): Promise<Nanoseconds> {
    if (this._disposed) throw new ClockError('Clock is disposed');
    return new Promise((resolve) => {
      setTimeout(() => resolve(this.now()), 0);
    });
  }

  [Symbol.asyncDispose](): Promise<void> {
    this._disposed = true;
    this._state = 'stopped';
    this._totalTicks = 0n;
    this._totalElapsedNs = 0n;
    return Promise.resolve();
  }
}

type Nanoseconds = bigint;
