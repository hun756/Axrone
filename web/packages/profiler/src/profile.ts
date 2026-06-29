import { ProfilerErrorBase, ProfilerStartupError, ProfilerTickError } from './errors';
import { Ok, Err, type Result } from '@axrone/utility';
import type { SampleEvent, MetricsSummary, TimelineEvent, HistogramBucket, ProfilerResult } from './types';
import { FlameGraphBuilder } from './core/flame-graph';
import type { FlameGraphNode } from './core/flame-graph';
import { LogBoundedHistogram } from './core/metrics-collector';
import { TimelineRecorder, type TimelineRecordEvent } from './core/timeline-recorder';
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

interface OverheadMetrics {
  totalOverheadNs: bigint;
  timerCount: number;
  maxTimerDurationNs: bigint;
}

const MAX_PROFILE_RESULT_SIZE = 1_000_000_000n;

export class ProfileTimerTree implements Iterable<FlameGraphNode> {
  readonly #builder: FlameGraphBuilder;
  readonly #metrics: LogBoundedHistogram;
  readonly #clock: MonotonicClock;

  protected static markerCounter = 0;
  private static nextTimerId = 1;
  private readonly timers = new Map<number, TimerEntry>();
  private readonly overhead: OverheadMetrics = {
    totalOverheadNs: 0n,
    timerCount: 0,
    maxTimerDurationNs: 0n,
  };

  constructor() {
    this.#clock = new MonotonicClock({ clockId: 'timertree' });
    this.#builder = new FlameGraphBuilder();
    this.#metrics = new LogBoundedHistogram({ minMs: 0.1, maxMs: 10_000 });
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

  getMetrics(): LogBoundedHistogram {
    return this.#metrics;
  }

  getFlameGraphBuilder(): FlameGraphBuilder {
    return this.#builder;
  }

  getClock(): MonotonicClock {
    return this.#clock;
  }

  getActiveTimerCount(): number {
    return this.timers.size;
  }

  getOverheadMetrics(): Readonly<OverheadMetrics> {
    return Object.freeze({ ...this.overhead });
  }

  clear(): void {
    this.timers.clear();
  }

  dispose(): void {
    this.completeAllTimers();
    void this.#builder[Symbol.asyncDispose]();
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

interface SessionState {
  sessionId: string;
  startedAtMs: number;
  endedAtMs?: number;
  totalSamples: bigint;
}

export class Profiler implements AsyncDisposable {
  readonly #timerTree: ProfileTimerTree;
  readonly #config: Record<string, unknown>;
  readonly #onFrameEnd?: () => void;
  readonly #timelineRecorder: TimelineRecorder;
  readonly #memoryTracker?: MemoryTracker;
  readonly #state: SessionState;
  #disposed = false;

  constructor(options?: {
    mode?: ProfileMode;
    bufferCapacity?: number;
    onFrameEnd?: () => void;
    autoStart?: boolean;
    timerTree?: ProfileTimerTree;
  }) {
    this.#timerTree = options?.timerTree ?? new ProfileTimerTree();
    this.#config = options ?? {};
    this.#onFrameEnd = options?.onFrameEnd;
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

    this.#state = {
      sessionId: String(Date.now()),
      startedAtMs: Date.now(),
      totalSamples: 0n,
    };

    if (options?.mode === 'Continuous' || options?.autoStart) {
      this.start();
    }
  }

  start(): void {
    this.#state.startedAtMs = Date.now();
    this.#state.endedAtMs = undefined;
    if (this.#memoryTracker) this.#memoryTracker.startTracking({ intervalMs: 50 });
    this.#timelineRecorder.start();
  }

  stop(): void {
    this.#state.endedAtMs = Date.now();
    if (this.#memoryTracker) this.#memoryTracker.stopTracking();
    this.#timelineRecorder.stop();
  }

  endFrame(): void {
    if (this.#onFrameEnd) this.#onFrameEnd();
  }

  addTimer(name: string): number {
    const timerId = this.#timerTree.addTimer(name);
    this.#timelineRecorder.recordEvent({
      timestampMs: Date.now(),
      category: 'cpu',
      payload: { timerId, name, action: 'start' },
    });
    return timerId;
  }

  endTimer(timerId?: number): void {
    this.#timerTree.endTimer(timerId);
    if (timerId !== undefined) {
      this.#timelineRecorder.recordEvent({
        timestampMs: Date.now(),
        category: 'cpu',
        payload: { timerId, action: 'end' },
      });
    }
  }

  pauseTimer(timerId?: number): void {
    this.#timerTree.pauseTimer(timerId);
  }

  resumeTimer(timerId?: number): void {
    this.#timerTree.resumeTimer(timerId);
  }

  cancelTimer(timerId?: number): void {
    this.#timerTree.cancelTimer(timerId);
  }

  getHierarchy(): readonly FlameGraphNode[] {
    return this.#timerTree.getHierarchy();
  }

  addMarker(name: string, parentId?: number): void {
    this.#timelineRecorder.recordEvent({
      timestampMs: Date.now(),
      category: 'cpu',
      payload: { marker: name, parentId },
    });
  }

  addMarkerWithCallback(callback: () => void): number {
    return this.#timerTree.addMarkerWithCallback(callback);
  }

  getSampledMetrics(timerId?: number): Record<string, unknown> | undefined {
    return this.#timerTree.getSampledMetrics(timerId);
  }

  getResult(): ProfilerResult {
    const treeOverhead = this.#timerTree.getOverheadMetrics();
    const metricsStats = this.#timerTree.getMetrics().getStatistics();
    const histogramBuckets = this.buildHistogramBuckets(metricsStats);

    const result: ProfilerResult = {
      sessionId: this.#state.sessionId,
      startedAtMs: this.#state.startedAtMs,
      endedAtMs: this.#state.endedAtMs ?? Date.now(),
      totalSamples: this.#state.totalSamples,
      metrics: {
        meanMs: metricsStats.meanMs as number | undefined,
        p50Ms: metricsStats.p50Ms as number | undefined,
        p75Ms: metricsStats.p75Ms as number | undefined,
        p90Ms: metricsStats.p90Ms as number | undefined,
        p95Ms: metricsStats.p95Ms as number | undefined,
        p99Ms: metricsStats.p99Ms as number | undefined,
        minMs: metricsStats.minMs as number | undefined,
        maxMs: metricsStats.maxMs as number | undefined,
        totalDurationMs: metricsStats.totalDurationMs as bigint,
        histogramBuckets,
      },
      flameGraphs: [...this.#timerTree.getHierarchy()],
      timelineEvents: [...this.#timelineRecorder.getEvents()],
    };

    if (this.#state.totalSamples < MAX_PROFILE_RESULT_SIZE) {
      this.#state.totalSamples += 1n;
    }

    return Object.freeze(result);
  }

  getOverheadMetrics(): Readonly<OverheadMetrics> {
    return this.#timerTree.getOverheadMetrics();
  }

  getTimelineRecorder(): TimelineRecorder {
    return this.#timelineRecorder;
  }

  getMemoryTracker(): MemoryTracker | undefined {
    return this.#memoryTracker;
  }

  private buildHistogramBuckets(stats: Record<string, unknown>): HistogramBucket[] {
    const rawBoundaries = (stats.histogramBuckets as number[]) ?? [];
    const rawCounts = (stats.countsPerBucket as bigint[]) ?? [];
    const buckets: HistogramBucket[] = [];
    for (let i = 0; i < rawBoundaries.length; i++) {
      const count = rawCounts[i] ?? 0n;
      buckets.push({
        boundaries: [rawBoundaries[i]],
        counts: [count],
        count,
      });
    }
    return buckets;
  }

  clear(): void {
    this.#timerTree.clear();
    this.#state.totalSamples = 0n;
  }

  dispose(): void {
    if (this.#disposed) return;
    this.#disposed = true;
    this.#timerTree.dispose();
    if (this.#memoryTracker) void this.#memoryTracker[Symbol.asyncDispose]();
    void this.#timelineRecorder[Symbol.asyncDispose]();
  }

  [Symbol.asyncDispose](): Promise<void> {
    this.dispose();
    return Promise.resolve();
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

export const CORE_PROFILE_ID = 0 as const;
