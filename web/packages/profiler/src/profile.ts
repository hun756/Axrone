import { ProfilerErrorBase, ProfilerStartupError, ProfilerTickError } from './errors';
import { Ok, Err, type Result } from '@axrone/utility';
import type { SampleEvent, MemorySample, MetricsSummary, TimelineEvent, HistogramBucket } from './types';
import { FlameGraphBuilder, FlameGraphNode } from './core/flame-graph';
import { LogBoundedHistogram } from './core/metrics-collector';
import { TimelineRecorder } from './core/timeline-recorder';
import { MemoryTracker } from './core/memory-tracker';
import { MonotonicClock } from './core/clock';

export type ProfileMode = 'Sampled' | 'Continuous' | 'OnDemand';

const MAX_PROFILE_ID = 2 ** 31 - 1;

interface TimerEntry {
  name: string;
  startNs: bigint;
  accumulatedNs: bigint;
  startedAtMs: number;
  state: 'active' | 'paused';
}

interface ProfilerInternalState {
  sessionId: string;
  startedAtMs: number;
  endedAtMs?: number;
  totalSamples: bigint;
  sessions: SessionData[];
}

interface OverheadMetrics {
  totalOverheadNs: bigint;
  timerCount: number;
  sampleCount: number;
  maxTimerDurationNs: bigint;
}

class SessionData {
  readonly startTimestampNs: bigint;
  readonly samples: SampleEvent[] = [];

  constructor(clock: MonotonicClock) {
    this.startTimestampNs = clock.now() as unknown as bigint;
  }
}

export class ProfileTimerTree implements Iterable<FlameGraphNode> {
  readonly #builder: FlameGraphBuilder;
  readonly #metrics: LogBoundedHistogram;
  readonly #timelineRecorder: TimelineRecorder;
  readonly #memoryTracker?: MemoryTracker;
  readonly #state = new Map<string, ProfilerInternalState>();
  readonly #clock: MonotonicClock;

  protected static markerCounter = 0;
  private static nextTimerId = 1;
  private readonly timers = new Map<number, TimerEntry>();
  private readonly overhead: OverheadMetrics = {
    totalOverheadNs: 0n,
    timerCount: 0,
    sampleCount: 0,
    maxTimerDurationNs: 0n,
  };

  constructor(options?: { mode?: ProfileMode; bufferCapacity?: number }) {
    this.#clock = new MonotonicClock({ clockId: 'profiler-main' });
    this.#builder = new FlameGraphBuilder();
    this.#metrics = new LogBoundedHistogram({ minMs: 0.1, maxMs: 10_000 });
    this.#timelineRecorder = new TimelineRecorder();
    if (options?.mode !== 'OnDemand') {
      try {
        this.#memoryTracker = new MemoryTracker({ onMemoryChange: () => {} });
      } catch {
        this.#memoryTracker = undefined;
      }
    } else {
      this.#memoryTracker = undefined;
    }

    if (options?.mode === 'Continuous') {
      this.start();
    }
  }

  start(): void {
    const session = new SessionData(this.#clock);
    this.#state.set('main', {
      sessionId: String(Date.now()),
      startedAtMs: Date.now(),
      endedAtMs: undefined,
      totalSamples: 0n,
      sessions: [session],
    });
    if (this.#memoryTracker) this.#memoryTracker.startTracking({ intervalMs: 50 });
    if (this.#timelineRecorder) this.#timelineRecorder.start();
  }

  stop(): void {
    this.completeAllTimers();
    for (const sessionData of this.#state.values()) {
      sessionData.endedAtMs = Date.now();
    }
    if (this.#memoryTracker) this.#memoryTracker.stopTracking();
    if (this.#timelineRecorder) this.#timelineRecorder.stop();
  }

  addTimer(name: string): number {
    const overheadStart = BigInt(Math.floor(performance.now() * 1_000_000));
    const timerId = ProfileTimerTree.nextTimerId++;
    if (timerId > MAX_PROFILE_ID) ProfileTimerTree.nextTimerId = 1;
    const nowNs = this.#clock.now() as unknown as bigint;
    this.timers.set(timerId, {
      name,
      startNs: nowNs,
      accumulatedNs: 0n,
      startedAtMs: Date.now(),
      state: 'active',
    });
    this.#timelineRecorder.recordEvent({
      timestampMs: Date.now(),
      category: 'cpu',
      payload: { timerId, name, action: 'start' },
    });
    const overheadEnd = BigInt(Math.floor(performance.now() * 1_000_000));
    this.overhead.totalOverheadNs += overheadEnd - overheadStart;
    this.overhead.timerCount++;
    return timerId;
  }

  endTimer(timerId?: number): void {
    if (timerId === undefined) return;
    const overheadStart = BigInt(Math.floor(performance.now() * 1_000_000));
    const entry = this.timers.get(timerId);
    if (!entry) return;
    const nowNs = this.#clock.now() as unknown as bigint;
    const elapsedNs = entry.accumulatedNs + (nowNs - entry.startNs);
    const elapsedMs = Number(elapsedNs) / 1_000_000;

    this.#metrics.record(elapsedMs);
    this.#builder.recordFrame(entry.name, elapsedNs);

    if (elapsedNs > this.overhead.maxTimerDurationNs) {
      this.overhead.maxTimerDurationNs = elapsedNs;
    }

    this.timers.delete(timerId);
    const overheadEnd = BigInt(Math.floor(performance.now() * 1_000_000));
    this.overhead.totalOverheadNs += overheadEnd - overheadStart;
  }

  pauseTimer(timerId?: number): void {
    if (timerId === undefined) return;
    const entry = this.timers.get(timerId);
    if (!entry || entry.state !== 'active') return;
    const nowNs = this.#clock.now() as unknown as bigint;
    entry.accumulatedNs += nowNs - entry.startNs;
    entry.state = 'paused';
  }

  resumeTimer(timerId?: number): void {
    if (timerId === undefined) return;
    const entry = this.timers.get(timerId);
    if (!entry || entry.state !== 'paused') return;
    entry.startNs = this.#clock.now() as unknown as bigint;
    entry.state = 'active';
  }

  cancelTimer(timerId?: number): void {
    if (timerId === undefined) return;
    const entry = this.timers.get(timerId);
    if (!entry) return;
    this.timers.delete(timerId);
  }

  private completeAllTimers(): void {
    for (const [timerId] of this.timers) {
      this.endTimer(timerId);
    }
  }

  getHierarchy(): readonly FlameGraphNode[] {
    return this.#builder.getRoots();
  }

  addMarker(name: string, parentId?: number): void {
    this.#timelineRecorder.recordEvent({
      timestampMs: Date.now(),
      category: 'cpu',
      payload: { marker: name, parentId },
    });
  }

  addMarkerWithCallback(callback: () => void): number {
    const markerId = ++ProfileTimerTree.markerCounter & MAX_PROFILE_ID;
    const startNs = this.#clock.now() as unknown as bigint;
    callback();
    const endNs = this.#clock.now() as unknown as bigint;
    const elapsedNs = endNs - startNs;
    this.#metrics.record(Number(elapsedNs) / 1_000_000);
    this.#builder.recordFrame(`marker-${markerId}`, elapsedNs);
    return markerId;
  }

  getSampledMetrics(timerId?: number): Record<string, unknown> | undefined {
    if (timerId !== undefined) {
      const entry = this.timers.get(timerId);
      if (!entry) return undefined;
      const nowNs = this.#clock.now() as unknown as bigint;
      const elapsedNs = entry.accumulatedNs + (nowNs - entry.startNs);
      return {
        timerId,
        name: entry.name,
        durationMs: Number(elapsedNs) / 1_000_000,
        elapsedNs,
        state: entry.state,
      };
    }
    return this.#metrics.getStatistics();
  }

  getResult(): import('./types').ProfilerResult {
    const sessionData = this.#state.get('main');
    const metrics = this.buildMetricsSummary();
    return Object.freeze({
      sessionId: sessionData?.sessionId ?? 'unknown',
      startedAtMs: sessionData?.startedAtMs ?? Date.now(),
      endedAtMs: Date.now(),
      totalSamples: sessionData?.totalSamples ?? 0n,
      metrics,
      flameGraphs: [...this.#builder.getRoots()],
      timelineEvents: [...this.#timelineRecorder.getEvents()],
    });
  }

  getOverheadMetrics(): Readonly<OverheadMetrics> {
    return Object.freeze({ ...this.overhead });
  }

  getClock(): MonotonicClock {
    return this.#clock;
  }

  private buildMetricsSummary(): MetricsSummary {
    const stats = this.#metrics.getStatistics();
    const rawBoundaries = (stats.histogramBuckets as number[]) ?? [];
    const rawCounts = (stats.countsPerBucket as bigint[]) ?? [];
    const histogramBuckets: HistogramBucket[] = [];

    for (let i = 0; i < rawBoundaries.length; i++) {
      histogramBuckets.push({
        boundaries: [rawBoundaries[i]],
        counts: [rawCounts[i] ?? 0n],
        count: rawCounts[i] ?? 0n,
      });
    }

    return {
      meanMs: stats.meanMs as number | undefined,
      p50Ms: stats.p50Ms as number | undefined,
      p75Ms: stats.p75Ms as number | undefined,
      p90Ms: stats.p90Ms as number | undefined,
      p95Ms: stats.p95Ms as number | undefined,
      p99Ms: stats.p99Ms as number | undefined,
      minMs: stats.minMs as number | undefined,
      maxMs: stats.maxMs as number | undefined,
      totalDurationMs: stats.totalDurationMs as bigint,
      histogramBuckets,
    };
  }

  clear(): void {
    this.#state.clear();
    this.timers.clear();
  }

  dispose(): void {
    this.completeAllTimers();
    for (const sessionData of this.#state.values()) {
      for (const s of sessionData.sessions) {
        s.samples.length = 0;
      }
    }
    void this.#builder[Symbol.asyncDispose]();
    void this.#timelineRecorder[Symbol.asyncDispose]();
    if (this.#memoryTracker) void this.#memoryTracker[Symbol.asyncDispose]();
    void this.#clock[Symbol.asyncDispose]();
  }

  [Symbol.iterator](): Iterator<FlameGraphNode> {
    const hierarchy = this.getHierarchy();
    let idx = 0;
    return {
      next() {
        if (idx < hierarchy.length) {
          return { value: hierarchy[idx++], done: false };
        }
        return { value: undefined as never, done: true };
      },
    };
  }
}

export class Profiler extends ProfileTimerTree {
  readonly #config: Record<string, unknown>;
  readonly #onFrameEnd?: () => void;
  readonly #memoryTracker: MemoryTracker | undefined;
  readonly #timelineRecorder: TimelineRecorder;
  readonly #state = new Map<string, SessionData>();
  private sessionIndex = 0;

  constructor(options?: { mode?: ProfileMode; bufferCapacity?: number; onFrameEnd?: () => void; autoStart?: boolean }) {
    super({ ...options, mode: options?.mode ?? 'Sampled' });
    this.#config = options ?? {};
    this.#onFrameEnd = options?.onFrameEnd;
    if (options?.mode !== 'OnDemand') {
      try {
        this.#memoryTracker = new MemoryTracker({ onMemoryChange: () => {} });
      } catch {
        this.#memoryTracker = undefined;
      }
    } else {
      this.#memoryTracker = undefined;
    }
    this.#timelineRecorder = new TimelineRecorder();

    if (options?.mode === 'Continuous') {
      const session = new SessionData(this.getClock());
      this.#state.set('main', session);
      this.#memoryTracker?.startTracking({ intervalMs: 50 });
      this.#timelineRecorder.start();
    } else if (options?.autoStart) {
      this.start();
    }
  }

  start(): void {
    const session = new SessionData(this.getClock());
    this.#state.set('main', session);
    if (this.#memoryTracker) this.#memoryTracker.startTracking({ intervalMs: 50 });
    if (this.#timelineRecorder) this.#timelineRecorder.start();
  }

  stop(): void {
    for (const sessionData of this.#state.values()) {
      (sessionData as unknown as Record<string, unknown>).endedAtMs = Date.now();
    }
    if (this.#memoryTracker) this.#memoryTracker.stopTracking();
    if (this.#timelineRecorder) this.#timelineRecorder.stop();
  }

  endFrame(): void {
    if (this.#onFrameEnd) this.#onFrameEnd();
  }

  getSampledMetrics(timerId?: number): Record<string, unknown> | undefined {
    return super.getSampledMetrics(timerId);
  }

  clear(): void {
    super.clear();
    this.#state.clear();
    this.sessionIndex = 0;
  }

  dispose(): void {
    super.dispose();
    if (this.#memoryTracker) void this.#memoryTracker[Symbol.asyncDispose]();
    if (this.#timelineRecorder) void this.#timelineRecorder[Symbol.asyncDispose]();
  }
}

export function profile<T>(fn: () => T): Result<T, ProfilerErrorBase> {
  try {
    const value = fn();
    return Ok.of(value);
  } catch (e) {
    return Err.of(new ProfilerStartupError(String(e)));
  }
}

let lastProfileDurationMs = 0;

export function getProfileLastProfileTime(): number {
  return lastProfileDurationMs;
}

export const CORE_PROFILE_ID = 0 as const;
