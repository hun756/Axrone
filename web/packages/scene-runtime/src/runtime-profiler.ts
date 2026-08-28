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
