export const SCENE_RUNTIME_PROFILER_PHASES = [
    'preUpdate',
    'fixedUpdate',
    'update',
    'postUpdate',
    'render',
] as const;

export type SceneRuntimeProfilerPhaseId = (typeof SCENE_RUNTIME_PROFILER_PHASES)[number];

export interface SceneRuntimeRenderStats {
    readonly drawCalls: number;
    readonly trianglesSubmitted: number;
}

export interface SceneRuntimePhysicsStats {
    readonly stepMs: number;
    readonly collisionMs: number;
    readonly solveMs: number;
}

export interface SceneRuntimeFrameRecord {
    readonly frame: number;
    readonly timestampMs: number;
    readonly frameTimeMs: number;
    readonly fps: number;
    readonly deltaMs: number;
    readonly fixedSteps: number;
    readonly phaseMs: Readonly<Record<SceneRuntimeProfilerPhaseId, number>>;
    readonly render: SceneRuntimeRenderStats | null;
    readonly physics: SceneRuntimePhysicsStats | null;
    readonly memoryUsedBytes: number | null;
}

export interface SceneRuntimeProfilerSummary {
    readonly frameCount: number;
    readonly avgFrameTimeMs: number;
    readonly minFrameTimeMs: number;
    readonly maxFrameTimeMs: number;
    readonly p95FrameTimeMs: number;
    readonly avgFps: number;
    readonly avgPhaseMs: Readonly<Record<SceneRuntimeProfilerPhaseId, number>>;
}

export interface SceneRuntimeProfilerOptions {
    readonly capacity?: number;
    readonly memorySampleIntervalMs?: number;
    readonly enabled?: boolean;
}

export type SceneRuntimeProfilerListener = (record: SceneRuntimeFrameRecord) => void;

const DEFAULT_CAPACITY = 300;

const createEmptyPhaseAccumulator = (): Record<SceneRuntimeProfilerPhaseId, number> => ({
    preUpdate: 0,
    fixedUpdate: 0,
    update: 0,
    postUpdate: 0,
    render: 0,
});

export class SceneRuntimeProfiler {
    private readonly _capacity: number;
    private readonly _records: SceneRuntimeFrameRecord[] = [];
    private readonly _listeners = new Set<SceneRuntimeProfilerListener>();
    private readonly _phaseAccumulator: Record<SceneRuntimeProfilerPhaseId, number> =
        createEmptyPhaseAccumulator();
    private _enabled: boolean;
    private _frameOpen = false;
    private _frame = 0;
    private _frameStartMs = 0;
    private _deltaMs = 0;
    private _pendingRender: SceneRuntimeRenderStats | null = null;
    private _pendingPhysics: SceneRuntimePhysicsStats | null = null;
    private _disposed = false;

    constructor(options: SceneRuntimeProfilerOptions = {}) {
        this._capacity =
            options.capacity !== undefined && options.capacity > 0
                ? Math.floor(options.capacity)
                : DEFAULT_CAPACITY;
        this._enabled = options.enabled ?? false;
    }

    get isEnabled(): boolean {
        return this._enabled;
    }

    get isDisposed(): boolean {
        return this._disposed;
    }

    enable(): void {
        this._enabled = true;
    }

    disable(): void {
        this._enabled = false;
        this._frameOpen = false;
    }

    subscribe(listener: SceneRuntimeProfilerListener): () => void {
        this._listeners.add(listener);
        return () => {
            this._listeners.delete(listener);
        };
    }

    beginFrame(frame: number, nowMs: number, deltaMs: number): void {
        if (this._disposed || !this._enabled) {
            return;
        }
        this._frameOpen = true;
        this._frame = frame;
        this._frameStartMs = nowMs;
        this._deltaMs = deltaMs;
    }

    timePhase<T>(phase: SceneRuntimeProfilerPhaseId, action: () => T): T {
        if (this._disposed || !this._enabled || !this._frameOpen) {
            return action();
        }
        const startedAt = performance.now();
        try {
            return action();
        } finally {
            this._phaseAccumulator[phase] += performance.now() - startedAt;
        }
    }

    attachRenderStats(stats: SceneRuntimeRenderStats): void {
        if (this._enabled && this._frameOpen) {
            this._pendingRender = stats;
        }
    }

    attachPhysicsStats(stats: SceneRuntimePhysicsStats): void {
        if (this._enabled && this._frameOpen) {
            this._pendingPhysics = stats;
        }
    }

    endFrame(nowMs: number, fixedSteps: number): void {
        if (this._disposed || !this._enabled || !this._frameOpen) {
            return;
        }
        this._frameOpen = false;

        const frameTimeMs = Math.max(0, nowMs - this._frameStartMs);
        const record: SceneRuntimeFrameRecord = {
            frame: this._frame,
            timestampMs: nowMs,
            frameTimeMs,
            fps: frameTimeMs > 0 ? 1000 / frameTimeMs : 0,
            deltaMs: this._deltaMs,
            fixedSteps,
            phaseMs: { ...this._phaseAccumulator },
            render: this._pendingRender,
            physics: this._pendingPhysics,
            memoryUsedBytes: null,
        };

        const accumulator = this._phaseAccumulator;
        for (const phase of SCENE_RUNTIME_PROFILER_PHASES) {
            accumulator[phase] = 0;
        }
        this._pendingRender = null;
        this._pendingPhysics = null;

        this._records.push(record);
        if (this._records.length > this._capacity) {
            this._records.shift();
        }

        for (const listener of this._listeners) {
            listener(record);
        }
    }

    getRecords(): readonly SceneRuntimeFrameRecord[] {
        return this._records;
    }

    getSummary(cap?: number): SceneRuntimeProfilerSummary | null {
        const total = this._records.length;
        if (total === 0) {
            return null;
        }

        const windowSize =
            cap !== undefined && cap > 0 ? Math.min(Math.floor(cap), total) : total;
        const records = this._records.slice(total - windowSize);

        let frameTimeSum = 0;
        let minFrameTimeMs = Number.POSITIVE_INFINITY;
        let maxFrameTimeMs = 0;
        let fpsSum = 0;
        const phaseSums = createEmptyPhaseAccumulator();

        for (const record of records) {
            frameTimeSum += record.frameTimeMs;
            minFrameTimeMs = Math.min(minFrameTimeMs, record.frameTimeMs);
            maxFrameTimeMs = Math.max(maxFrameTimeMs, record.frameTimeMs);
            fpsSum += record.fps;
            for (const phase of SCENE_RUNTIME_PROFILER_PHASES) {
                phaseSums[phase] += record.phaseMs[phase];
            }
        }

        const sortedFrameTimes = records
            .map((record) => record.frameTimeMs)
            .sort((a, b) => a - b);
        const p95Index = Math.min(
            sortedFrameTimes.length - 1,
            Math.ceil(sortedFrameTimes.length * 0.95) - 1
        );
        const avgPhaseMs = createEmptyPhaseAccumulator();
        for (const phase of SCENE_RUNTIME_PROFILER_PHASES) {
            avgPhaseMs[phase] = phaseSums[phase] / windowSize;
        }

        return {
            frameCount: windowSize,
            avgFrameTimeMs: frameTimeSum / windowSize,
            minFrameTimeMs,
            maxFrameTimeMs,
            p95FrameTimeMs: sortedFrameTimes[Math.max(0, p95Index)]!,
            avgFps: fpsSum / windowSize,
            avgPhaseMs,
        };
    }

    clear(): void {
        this._records.length = 0;
    }

    dispose(): void {
        if (this._disposed) {
            return;
        }
        this._listeners.clear();
        this._records.length = 0;
        this._frameOpen = false;
        this._disposed = true;
    }
}
