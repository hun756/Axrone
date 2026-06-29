import type { ClockId, TimePoint, Duration, ClockSnapshot, ClockMetadata, ClockState, ClockMetrics, ClockPrecision } from './types';
import { TIME_CONVERSION } from './types';
import { ClockError, ClockNotRunningError, ClockAlreadyRunningError, ClockOverflowError } from './clock-error';

export interface VirtualClockOptions {
  readonly clockId?: string;
  readonly initialTimeNs?: bigint;
  readonly initialWallTimeMs?: number;
  readonly timeScale?: number;
  readonly precision?: ClockPrecision;
  readonly maxTimeNs?: bigint;
}

const MAX_SAFE_TIME_NS = 1n << 62n;

export class VirtualClock implements AsyncDisposable {
  private static nextId = 0;

  readonly clockId: ClockId;
  readonly startTimeNs: bigint;
  readonly startWallTimeMs: number;
  readonly maxTimeNs: bigint;
  timeScale: number;

  private _currentTimeNs: bigint;
  private _state: ClockState = 'stopped';
  private _totalTicks = 0n;
  private _totalElapsedNs = 0n;
  private _maxDriftNs = 0n;
  private _lastTickValue: bigint | null = null;
  private _realStartNs: bigint;
  private _precision: ClockPrecision;
  private _disposed = false;

  constructor(options?: VirtualClockOptions) {
    const id = options?.clockId ?? `virtual-${++VirtualClock.nextId}`;
    this.clockId = id as ClockId;
    this.startTimeNs = options?.initialTimeNs ?? 0n;
    this.startWallTimeMs = options?.initialWallTimeMs ?? Date.now();
    this.timeScale = options?.timeScale ?? 1;
    this.maxTimeNs = options?.maxTimeNs ?? MAX_SAFE_TIME_NS;
    this._currentTimeNs = this.startTimeNs;
    this._precision = options?.precision ?? 'ultra';
    this._realStartNs = BigInt(Math.floor(performance.now() * 1_000_000));
  }

  now(): bigint {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (this._state === 'stopped') return this._currentTimeNs;
    const realElapsed = BigInt(Math.floor(performance.now() * 1_000_000)) - this._realStartNs;
    const scaledElapsed = this.applyTimeScale(realElapsed);
    const virtualTime = this.startTimeNs + scaledElapsed;
    this._currentTimeNs = virtualTime;
    this._lastTickValue = virtualTime;
    return virtualTime;
  }

  private applyTimeScale(elapsed: bigint): bigint {
    if (this.timeScale === 1) return elapsed;
    const scaled = Number(elapsed) * this.timeScale;
    return BigInt(Math.floor(scaled));
  }

  start(): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (this._state === 'running') throw new ClockAlreadyRunningError();
    this._realStartNs = BigInt(Math.floor(performance.now() * 1_000_000));
    this._state = 'running';
  }

  stop(): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (this._state !== 'running') throw new ClockNotRunningError();
    this.sample();
    this._state = 'stopped';
  }

  pause(): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (this._state !== 'running') throw new ClockNotRunningError();
    this.now();
    this._state = 'paused';
  }

  resume(): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (this._state !== 'paused') throw new ClockError('Clock is not paused');
    this._realStartNs = BigInt(Math.floor(performance.now() * 1_000_000));
    this._state = 'running';
  }

  setTime(ns: bigint): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (ns > this.maxTimeNs) throw new ClockOverflowError();
    this._currentTimeNs = ns;
  }

  advanceBy(ns: bigint): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    const newTime = this._currentTimeNs + ns;
    if (newTime > this.maxTimeNs) throw new ClockOverflowError();
    this._currentTimeNs = newTime;
  }

  setTimeScale(scale: number): void {
    if (this._disposed) throw new ClockError('Clock is disposed');
    if (scale <= 0) throw new ClockError('Time scale must be positive');
    this.now();
    this.timeScale = scale;
  }

  sample(): ClockSnapshot {
    if (this._disposed) throw new ClockError('Clock is disposed');
    const now = this.now();
    const elapsed = now - this.startTimeNs;
    return {
      timePoint: {
        clockId: this.clockId,
        timestampNs: now,
        wallTimeMs: this.startWallTimeMs + Number(elapsed / BigInt(TIME_CONVERSION.milliseconds)),
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
    const elapsed = this.now() - this.startTimeNs;
    return this.toDuration(elapsed);
  }

  elapsedSince(point: TimePoint): Duration {
    const diff = this.now() - point.timestampNs;
    return this.toDuration(diff);
  }

  ticks(count: number): void {
    this._totalTicks += BigInt(count);
  }

  getMetadata(): ClockMetadata {
    return Object.freeze({
      clockId: this.clockId,
      sourceType: 'virtual',
      startTimeNs: this.startTimeNs,
      startWallTimeMs: this.startWallTimeMs,
      resolutionHz: Number.MAX_SAFE_INTEGER,
      isMonotonic: this.timeScale >= 0,
    });
  }

  getMetrics(): ClockMetrics {
    const elapsed = this.now() - this.startTimeNs;
    const uptimeMs = Number(elapsed / BigInt(TIME_CONVERSION.milliseconds));
    return Object.freeze({
      totalTicks: this._totalTicks,
      totalElapsedNs: elapsed,
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
    this._currentTimeNs = 0n;
    this._totalTicks = 0n;
    this._totalElapsedNs = 0n;
    return Promise.resolve();
  }
}
