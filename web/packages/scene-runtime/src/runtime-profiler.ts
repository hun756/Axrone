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
    private _enabled: boolean;
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
    }

    subscribe(listener: SceneRuntimeProfilerListener): () => void {
        this._listeners.add(listener);
        return () => {
            this._listeners.delete(listener);
        };
    }

    getRecords(): readonly SceneRuntimeFrameRecord[] {
        return this._records;
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
        this._disposed = true;
    }
}
