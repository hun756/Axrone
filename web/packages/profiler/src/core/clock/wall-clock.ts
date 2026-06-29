import type { ClockId, TimePoint, Duration, ClockSnapshot, ClockMetadata, ClockState, ClockMetrics, ClockPrecision } from './types';
import { TIME_CONVERSION } from './types';
import { ClockError, ClockNotRunningError, ClockAlreadyRunningError, ClockSkewError } from './clock-error';

export interface WallClockOptions {
  readonly clockId?: string;
  readonly resolutionHz?: number;
  readonly maxSkewMs?: number;
  readonly precision?: ClockPrecision;
}

export class WallClock implements AsyncDisposable {
  private static nextId = 0;

  readonly clockId: ClockId;
  readonly startTimeNs: bigint;
  readonly startWallTimeMs: number;
  readonly resolutionHz: number;
  readonly maxSkewMs: number;

  private _state: ClockState = 'stopped';
  private _totalTicks = 0n;
  private _totalElapsedNs = 0n;
  private _maxDriftNs = 0n;
  private _lastTickValue: bigint | null = null;
  private _lastWallValue: number | null = null;
  private _precision: ClockPrecision;
  private _disposed = false;

  constructor(options?: WallClockOptions) {
    const id = options?.clockId ?? `wall-${++WallClock.nextId}`;
    this.clockId = id as ClockId;
    this.resolutionHz = options?.resolutionHz ?? 1000;
    this.maxSkewMs = options?.maxSkewMs ?? 100;
    this._precision = options?.precision ?? 'medium';
    this.startTimeNs = BigInt(Date.now()) * 1_000_000n;
    this.startWallTimeMs = Date.now();
  }

  now(): Milliseconds {
    if (this._disposed) throw new ClockError('Clock is disposed');
    const value = Date.now();
    this.detectSkew(value);
    this._lastWallValue = value;
    this._lastTickValue = BigInt(value) * 1_000_000n;
    return value as unknown as Milliseconds;
  }

  private detectSkew(wallTimeMs: number): void {
    if (this._lastWallValue !== null) {
      const skew = Math.abs(wallTimeMs - this._lastWallValue);
      if (skew > this.maxSkewMs) {
        const skewNs = BigInt(skew) * 1_000_000n;
        if (skewNs > this._maxDriftNs) {
          this._maxDriftNs = skewNs;
        }
      }
    }
  }

  start(): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (this._state === 'running') throw new ClockAlreadyRunningError();
    this._state = 'running';
    this._lastWallValue = Date.now();
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
    this._lastWallValue = Date.now();
  }

  sample(): ClockSnapshot {
    if (this._disposed) throw new ClockError('Clock is disposed');
    const now = Date.now();
    const nowNs = BigInt(now) * 1_000_000n;
    const elapsed = nowNs - this.startTimeNs;
    return {
      timePoint: {
        clockId: this.clockId,
        timestampNs: nowNs,
        wallTimeMs: now,
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
    const nowNs = BigInt(Date.now()) * 1_000_000n;
    const elapsed = nowNs - this.startTimeNs;
    return this.toDuration(elapsed);
  }

  elapsedSince(point: TimePoint): Duration {
    const nowNs = BigInt(Date.now()) * 1_000_000n;
    const diff = nowNs - point.timestampNs;
    return this.toDuration(diff);
  }

  ticks(count: number): void {
    this._totalTicks += BigInt(count);
  }

  toUnixMs(): number {
    return Date.now();
  }

  toUnixNs(): bigint {
    return BigInt(Date.now()) * 1_000_000n;
  }

  getMetadata(): ClockMetadata {
    return Object.freeze({
      clockId: this.clockId,
      sourceType: 'wall',
      startTimeNs: this.startTimeNs,
      startWallTimeMs: this.startWallTimeMs,
      resolutionHz: this.resolutionHz,
      isMonotonic: false,
    });
  }

  getMetrics(): ClockMetrics {
    const nowNs = BigInt(Date.now()) * 1_000_000n;
    const uptimeMs = Number((nowNs - this.startTimeNs) / BigInt(TIME_CONVERSION.milliseconds));
    return Object.freeze({
      totalTicks: this._totalTicks,
      totalElapsedNs: nowNs - this.startTimeNs,
      maxDriftNs: this._maxDriftNs,
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

  [Symbol.asyncDispose](): Promise<void> {
    this._disposed = true;
    this._state = 'stopped';
    this._totalTicks = 0n;
    this._totalElapsedNs = 0n;
    return Promise.resolve();
  }
}

type Milliseconds = number;
