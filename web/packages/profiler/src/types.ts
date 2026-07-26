import type { ProfilerErrorBase } from './errors';
import type { FlameGraphNode } from './core/flame-graph';

export interface ProfilerOptions {
    readonly samplingIntervalMs?: number;
    readonly maxStackTraceDepth?: number;
    readonly enableAsyncStackCapture?: boolean;
    readonly onSampleTick?: (sample: SampleEvent) => void;
    readonly onError?: (error: ProfilerErrorBase) => void;
    readonly context?: unknown;
}

export interface ProfilerResult {
    readonly sessionId: string;
    readonly startedAtMs: number;
    readonly endedAtMs: number;
    readonly totalSamples: bigint;
    readonly metrics: MetricsSummary;
    readonly flameGraphs: FlameGraphNode[];
    readonly timelineEvents: TimelineEvent[];
}

export interface SampleEvent {
    readonly timestampNs: bigint;
    readonly stackFrameCount: number;
    readonly stackSignature?: string;
    readonly memoryUsage?: MemorySample;
    readonly phase: 'enter' | 'exit';
}

export interface MemorySample {
    readonly usedHeapSizeBytes: number;
    readonly totalJSHeapSizeBytes?: number;
    readonly availableJSHeapSizeBytes?: number;
    readonly heapSizeChangeSinceLastSample?: bigint;
}

export interface HistogramBucket {
    readonly boundaries: number[];
    readonly counts: bigint[];
    readonly count: bigint;
}

export interface MetricsSummary {
    readonly meanMs?: number;
    readonly p50Ms?: number;
    readonly p75Ms?: number;
    readonly p90Ms?: number;
    readonly p95Ms?: number;
    readonly p99Ms?: number;
    readonly maxMs?: number;
    readonly minMs?: number;
    readonly totalDurationMs: bigint;
    readonly histogramBuckets: HistogramBucket[];
}

export interface TimelineEvent {
    readonly timestampMs: number;
    readonly category: 'cpu' | 'memory' | 'gc' | 'allocation';
    readonly payload: Record<string, unknown>;
}

export type FlameGraphLayoutAlgorithm = 'squarified' | 'slicing';

export interface FlameGraphOptions {
    readonly maxDepth?: number;
    readonly sortByDurationMs?: boolean;
    readonly layoutAlgorithm?: FlameGraphLayoutAlgorithm;
    readonly minNodeWidthPx?: number;
}

export type ProfilerPhase = 'enter' | 'exit';
