declare const NanosecondsBrand: unique symbol;
declare const MicrosecondsBrand: unique symbol;
declare const MillisecondsBrand: unique symbol;
declare const SecondsBrand: unique symbol;
declare const ClockIdBrand: unique symbol;

export type Nanoseconds = bigint & { readonly [NanosecondsBrand]: typeof NanosecondsBrand };
export type Microseconds = bigint & { readonly [MicrosecondsBrand]: typeof MicrosecondsBrand };
export type Milliseconds = number & { readonly [MillisecondsBrand]: typeof MillisecondsBrand };
export type Seconds = number & { readonly [SecondsBrand]: typeof SecondsBrand };
export type ClockId = string & { readonly [ClockIdBrand]: typeof ClockIdBrand };

export type TimeUnit =
  | { readonly unit: 'nanoseconds'; readonly value: Nanoseconds }
  | { readonly unit: 'microseconds'; readonly value: Microseconds }
  | { readonly unit: 'milliseconds'; readonly value: Milliseconds }
  | { readonly unit: 'seconds'; readonly value: Seconds };

export interface Duration {
  readonly nanoseconds: bigint;
  readonly microseconds: bigint;
  readonly milliseconds: number;
  readonly seconds: number;
}

export interface TimePoint {
  readonly clockId: ClockId;
  readonly timestampNs: bigint;
  readonly wallTimeMs?: number;
}

export interface ClockSnapshot {
  readonly timePoint: TimePoint;
  readonly elapsedSinceStart: Duration;
}

export type ClockState = 'stopped' | 'running' | 'paused';

export type ClockSourceType = 'monotonic' | 'wall' | 'virtual';

export interface ClockMetadata {
  readonly clockId: ClockId;
  readonly sourceType: ClockSourceType;
  readonly startTimeNs: bigint;
  readonly startWallTimeMs: number;
  readonly resolutionHz: number;
  readonly isMonotonic: boolean;
}

export interface ClockMetrics {
  readonly totalTicks: bigint;
  readonly totalElapsedNs: bigint;
  readonly maxDriftNs: bigint;
  readonly state: ClockState;
  readonly uptimeMs: number;
}

export interface TimeConversionMap {
  readonly nanoseconds: number;
  readonly microseconds: number;
  readonly milliseconds: number;
  readonly seconds: number;
}

export const TIME_CONVERSION: Readonly<TimeConversionMap> = Object.freeze({
  nanoseconds: 1,
  microseconds: 1_000,
  milliseconds: 1_000_000,
  seconds: 1_000_000_000,
});

export interface Epoch {
  readonly unix: bigint;
  readonly monotonic: bigint;
}

export type ClockPrecision = 'low' | 'medium' | 'high' | 'ultra';
