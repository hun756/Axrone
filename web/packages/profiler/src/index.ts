export const PROFILER_VERSION = '0.1.0' as const;

// Types
export type {
  ProfilerOptions,
  ProfilerResult,
  SampleEvent,
  MemorySample,
  HistogramBucket,
  MetricsSummary,
  TimelineEvent,
  FlameGraphOptions,
  FlameGraphLayoutAlgorithm,
} from './types';

// Errors
export {
  ProfilerErrorBase,
  ProfilerStartupError,
  ProfilerShutdownError,
  ProfilerTickError,
  ProfilerStackCaptureError,
  isProfilerError,
  tryCatch,
  tryCatchAsync,
  fromThrowable,
  fromThrowableAsync,
  fromPromise,
  assertTimerId,
  PROFILER_ERROR_CODES,
} from './errors';

// Clock
export type {
  Nanoseconds,
  Microseconds,
  Milliseconds,
  Seconds,
  ClockId,
  TimeUnit,
  Duration,
  TimePoint,
  ClockSnapshot,
  ClockState,
  ClockSourceType,
  ClockMetadata,
  ClockMetrics,
  ClockPrecision,
  MonotonicClockOptions,
  WallClockOptions,
  VirtualClockOptions,
  StopwatchOptions,
  LapResult,
} from './core/clock';

export {
  TIME_CONVERSION,
  ClockError,
  ClockNotRunningError,
  ClockAlreadyRunningError,
  ClockOverflowError,
  ClockSkewError,
  ClockResolutionError,
  isClockError,
  MonotonicClock,
  WallClock,
  VirtualClock,
  Stopwatch,
} from './core/clock';

// Core modules
export { ContinuousSampler } from './core/sampler';
export type { StackFrameCapture } from './core/stack-capture';
export { StackCaptureEngine } from './core/stack-capture';
export { LogBoundedHistogram } from './core/metrics-collector';
export type { MemorySampleEvent } from './core/memory-tracker';
export { MemoryTracker } from './core/memory-tracker';
export { FlameGraphBuilder, FlameGraphNode } from './core/flame-graph';
export type { TimelineRecordEvent } from './core/timeline-recorder';
export { TimelineRecorder } from './core/timeline-recorder';

// Export/perfetto
export { PerfettoSerializer } from './export/perfetto-serializer';

// Main profiler orchestration
export type { ProfileMode } from './profile';
export { ProfileTimerTree, Profiler, profile, CORE_PROFILE_ID } from './profile';
