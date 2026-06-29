import { ProfilerErrorBase, ProfilerStartupError } from './errors';
import { Ok, Err, type Result } from '@axrone/utility';
import type { SampleEvent, MemorySample, MetricsSummary, TimelineEvent } from './types';
import { FlameGraphBuilder, FlameGraphNode } from './core/flame-graph';
import { LogBoundedHistogram } from './core/metrics-collector';
import { TimelineRecorder } from './core/timeline-recorder';
import { MemoryTracker } from './core/memory-tracker';

export type ProfileMode = 'Sampled' | 'Continuous' | 'OnDemand';

const MAX_PROFILE_ID = 2 ** 31 - 1;

interface ProfilerInternalState {
  sessionId: string;
  startedAtMs: number;
  endedAtMs?: number;
  totalSamples: bigint;
  sessions: SessionData[];
}

class SessionData {
  readonly startTimestampNs = BigInt(Math.floor(performance.now())) * 1_000_000n;
  readonly samples: SampleEvent[] = [];
}

export class ProfileTimerTree implements Iterable<FlameGraphNode> {
  readonly #builder: FlameGraphBuilder;
  readonly #metrics: LogBoundedHistogram;
  readonly #timelineRecorder: TimelineRecorder;
  readonly #memoryTracker?: MemoryTracker;
  readonly #state = new Map<string, ProfilerInternalState>();

  protected static markerCounter = 0;

  constructor(options?: { mode?: ProfileMode; bufferCapacity?: number }) {
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
    const session = new SessionData();
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
    for (const sessionData of this.#state.values()) {
      sessionData.endedAtMs = Date.now();
    }
    if (this.#memoryTracker) this.#memoryTracker.stopTracking();
    if (this.#timelineRecorder) this.#timelineRecorder.stop();
  }

  addTimer(name: string): number {
    void name;
    return ++ProfileTimerTree.markerCounter & MAX_PROFILE_ID;
  }

  endTimer(timerId?: number): void {
    void timerId;
  }

  pauseTimer(timerId?: number): void {
    void timerId;
  }

  resumeTimer(timerId?: number): void {
    void timerId;
  }

  cancelTimer(timerId?: number): void {
    void timerId;
  }

  getHierarchy(): readonly FlameGraphNode[] {
    return this.#builder.getRoots();
  }

  addMarker(name: string, parentId?: number): void {
    void name;
    void parentId;
  }

  addMarkerWithCallback(callback: () => void): number {
    void callback;
    return ++ProfileTimerTree.markerCounter & MAX_PROFILE_ID;
  }

  getSampledMetrics(timerId?: number) {
    void timerId;
    return undefined;
  }

  clear(): void {
    this.#state.clear();
  }

  dispose(): void {
    for (const sessionData of this.#state.values()) {
      for (const s of sessionData.sessions) {
        s.samples.length = 0;
      }
    }
    void this.#builder[Symbol.asyncDispose]();
    void this.#timelineRecorder[Symbol.asyncDispose]();
    if (this.#memoryTracker) void this.#memoryTracker[Symbol.asyncDispose]();
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
      const session = new SessionData();
      this.#state.set('main', session);
      this.#memoryTracker?.startTracking({ intervalMs: 50 });
      this.#timelineRecorder.start();
    } else if (options?.autoStart) {
      this.start();
    }
  }

  start(): void {
    const session = new SessionData();
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

  addTimer(name: string): number {
    return super.addTimer(name);
  }

  getHierarchy(): readonly FlameGraphNode[] {
    return super.getHierarchy();
  }

  addMarker(name: string, parentId?: number): void {
    void name;
    void parentId;
  }

  addMarkerWithCallback(callback: () => void): number {
    void callback;
    return ++ProfileTimerTree.markerCounter & MAX_PROFILE_ID;
  }

  getSampledMetrics(timerId?: number) {
    void timerId;
    return undefined;
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

// ---------------------------------------------------------------------------
// Standalone profiling utility
// ---------------------------------------------------------------------------

export function profile<T>(fn: () => T): Result<T, ProfilerErrorBase> {
  try {
    const value = fn();
    return Ok.of(value);
  } catch (e) {
    return Err.of(new ProfilerStartupError(String(e)));
  }
}

export function getProfileLastProfileTime(): number {
  return 0;
}

export const CORE_PROFILE_ID = 0 as const;
