import { ClockError, ClockNotRunningError } from './clock-error';
import type { Duration } from './types';
import { TIME_CONVERSION } from './types';

export interface StopwatchOptions {
    readonly autoStart?: boolean;
    readonly clock?: { now(): bigint | number } & AsyncDisposable;
}

export interface LapResult {
    readonly lapNumber: number;
    readonly lapDuration: Duration;
    readonly totalDuration: Duration;
    readonly timestampNs: bigint;
}

class DefaultClockImpl implements AsyncDisposable {
    now(): bigint {
        return BigInt(Math.floor(performance.now() * 1_000_000));
    }
    [Symbol.asyncDispose](): Promise<void> {
        return Promise.resolve();
    }
}

const defaultClock = new DefaultClockImpl();

export class Stopwatch implements AsyncDisposable {
    private readonly clock: { now(): bigint | number } & AsyncDisposable;
    private _startTimeNs: bigint | null = null;
    private _lastLapTimeNs: bigint | null = null;
    private _lapCount = 0;
    private _running = false;
    private _disposed = false;

    constructor(options?: StopwatchOptions) {
        this.clock = options?.clock ?? defaultClock;
        if (options?.autoStart) this.start();
    }

    start(): void {
        if (this._disposed) throw new ClockError('Stopwatch is disposed');
        if (this._running) return;
        this._running = true;
        const now = this.getNow();
        this._startTimeNs = now;
        this._lastLapTimeNs = now;
    }

    stop(): void {
        if (this._disposed) throw new ClockError('Stopwatch is disposed');
        if (!this._running) throw new ClockNotRunningError();
        this._running = false;
    }

    reset(): void {
        if (this._disposed) throw new ClockError('Stopwatch is disposed');
        this._startTimeNs = null;
        this._lastLapTimeNs = null;
        this._lapCount = 0;
        this._running = false;
    }

    restart(): void {
        this.reset();
        this.start();
    }

    lap(): LapResult | null {
        if (this._disposed) throw new ClockError('Stopwatch is disposed');
        if (!this._running || this._startTimeNs === null) return null;
        const now = this.getNow();
        this._lapCount++;
        const lapDuration = this.toDuration(now - (this._lastLapTimeNs ?? this._startTimeNs));
        const totalDuration = this.toDuration(now - this._startTimeNs);
        this._lastLapTimeNs = now;
        return {
            lapNumber: this._lapCount,
            lapDuration,
            totalDuration,
            timestampNs: now,
        };
    }

    elapsed(): Duration | null {
        if (this._disposed || !this._running || this._startTimeNs === null) return null;
        const now = this.getNow();
        return this.toDuration(now - this._startTimeNs);
    }

    isRunning(): boolean {
        return this._running;
    }

    private getNow(): bigint {
        const raw = this.clock.now();
        if (typeof raw === 'bigint') return raw;
        return BigInt(raw) * 1_000_000n;
    }

    private toDuration(ns: bigint): Duration {
        return {
            nanoseconds: ns,
            microseconds: ns / BigInt(TIME_CONVERSION.microseconds),
            milliseconds: Number(ns / BigInt(TIME_CONVERSION.milliseconds)),
            seconds: Number(ns / BigInt(TIME_CONVERSION.seconds)),
        };
    }

    [Symbol.asyncDispose](): Promise<void> {
        this._disposed = true;
        this._running = false;
        this._startTimeNs = null;
        this._lastLapTimeNs = null;
        this._lapCount = 0;
        return Promise.resolve();
    }
}
