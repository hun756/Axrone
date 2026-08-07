import { describe, expect, it } from 'vitest';
import { AnimationClip } from '../clip';
import { optimizeAnimationClipDefinition, optimizeAnimationClipDefinitions } from '../optimization';
import { AnimationCurveLayout } from '../pose';
import { AnimationRig } from '../rig';
import { AnimationClipStreamingScheduler } from '../streaming';
import { AnimationValidationError } from '../errors';
import type { AnimationControllerClipActivity } from '../types';

const rig = new AnimationRig({ bones: [{ name: 'root' }] });
const curveLayout = new AnimationCurveLayout();

const makeStreamedClip = (id: string, priority = 0): AnimationClip =>
    new AnimationClip(
        {
            id,
            tracks: [
                {
                    target: 'root',
                    path: 'translation',
                    times: [0, 1],
                    values: [0, 0, 0, 1, 0, 0],
                },
            ],
            streaming: {
                mode: 'streamed',
                sourceUri: `clips/${id}.anim`,
                chunkDuration: 0.5,
                preloadWindow: 0.25,
                priority,
            },
        },
        rig,
        curveLayout
    );

const activity = (
    clipId: string,
    time: number,
    weight = 1
): AnimationControllerClipActivity => ({
    clipId,
    layerId: 'base',
    stateId: clipId,
    layerWeight: weight,
    motionWeight: 1,
    loop: false,
    time,
    normalizedTime: time,
});

describe('AnimationClipStreamingScheduler lifecycle', () => {
    it('re-requests failed chunks only after an explicit reset', () => {
        const scheduler = new AnimationClipStreamingScheduler([makeStreamedClip('walk')]);

        let snapshot = scheduler.update([activity('walk', 0.1)]);
        expect(snapshot.pendingRequests.map((request) => request.chunkId)).toContain('walk:virtual:0');

        expect(scheduler.markChunkFailed('walk', 'walk:virtual:0', 'network')).toBe(true);
        snapshot = scheduler.update([activity('walk', 0.1)]);
        expect(snapshot.clips[0]?.failedChunkIds).toContain('walk:virtual:0');
        expect(snapshot.pendingRequests).toHaveLength(0);

        scheduler.reset('walk');
        snapshot = scheduler.update([activity('walk', 0.1)]);
        expect(snapshot.pendingRequests.map((request) => request.chunkId)).toContain('walk:virtual:0');
        expect(snapshot.clips[0]?.failedChunkIds).toHaveLength(0);
    });

    it('orders pending requests across clips by reason then priority', () => {
        const scheduler = new AnimationClipStreamingScheduler([
            makeStreamedClip('low', 0),
            makeStreamedClip('high', 5),
        ]);

        const snapshot = scheduler.update([activity('low', 0.4), activity('high', 0.4)]);
        const activeRequests = snapshot.pendingRequests.filter((request) => request.reason === 'active');
        expect(activeRequests[0]?.clipId).toBe('high');
        expect(activeRequests[1]?.clipId).toBe('low');
        const firstPreloadIndex = snapshot.pendingRequests.findIndex(
            (request) => request.reason === 'preload'
        );
        expect(firstPreloadIndex).toBeGreaterThanOrEqual(activeRequests.length);
    });

    it('returns false for unknown chunk ids without mutating state', () => {
        const scheduler = new AnimationClipStreamingScheduler([makeStreamedClip('walk')]);
        expect(scheduler.markChunkLoaded('walk', 'missing-chunk')).toBe(false);
        expect(scheduler.markChunkLoaded('missing-clip', 'walk:virtual:0')).toBe(false);
    });

    it('reuses the previous snapshot when nothing changed and rebuilds when it does', () => {
        const scheduler = new AnimationClipStreamingScheduler([makeStreamedClip('walk')]);

        scheduler.update([activity('walk', 0.1)]);
        // Second update clears the emitted requests from the snapshot.
        const settled = scheduler.update([activity('walk', 0.1)]);
        const cached = scheduler.update([activity('walk', 0.1)]);
        expect(cached).toBe(settled);

        scheduler.markChunkLoaded('walk', 'walk:virtual:0');
        const afterLoad = scheduler.update([activity('walk', 0.1)]);
        expect(afterLoad).not.toBe(cached);
        expect(afterLoad.clips[0]?.loadedChunkIds).toContain('walk:virtual:0');
    });
});

describe('optimizeAnimationClipDefinition tolerances', () => {
    it('preserves STEP tracks regardless of tolerance', () => {
        const optimized = optimizeAnimationClipDefinition({
            id: 'stepper',
            tracks: [
                {
                    target: 'root',
                    path: 'translation',
                    interpolation: 'STEP',
                    times: [0, 0.5, 1],
                    values: [0, 0, 0, 0.5, 0, 0, 1, 0, 0],
                },
            ],
            compression: { codec: 'keyframe-reduced', positionTolerance: 100 },
        });
        expect(optimized.tracks[0]?.keyframeCount).toBe(3);
    });

    it('drops linear keyframes predicted by their neighbours', () => {
        const optimized = optimizeAnimationClipDefinition({
            id: 'linear',
            tracks: [
                {
                    target: 'root',
                    path: 'translation',
                    times: [0, 0.5, 1],
                    values: [0, 0, 0, 0.5, 0, 0, 1, 0, 0],
                },
            ],
            compression: { codec: 'keyframe-reduced', positionTolerance: 1e-3 },
        });
        expect(optimized.tracks[0]?.keyframeCount).toBe(2);
    });

    it('keeps keyframes when the surrounding span is degenerate at animation scale', () => {
        const optimized = optimizeAnimationClipDefinition({
            id: 'degenerate',
            tracks: [
                {
                    target: 'root',
                    path: 'translation',
                    times: [0, 1e-7, 2e-7],
                    values: [0, 0, 0, 5, 0, 0, 10, 0, 0],
                },
            ],
            compression: { codec: 'keyframe-reduced', positionTolerance: 100 },
        });
        expect(optimized.tracks[0]?.keyframeCount).toBe(3);
    });
});

describe('AnimationClipStreamingScheduler markChunkRequested', () => {
    it('prevents duplicate requests by setting status to requested', () => {
        const scheduler = new AnimationClipStreamingScheduler([makeStreamedClip('walk')]);
        expect(scheduler.markChunkRequested('walk', 'walk:virtual:0')).toBe(true);
        // After marking requested, a subsequent update should not re-request the same chunk
        const snapshot = scheduler.update([activity('walk', 0.1)]);
        // The chunk should be in 'requested' status, not generating new pending requests
        expect(snapshot.clips[0]?.requestedChunkIds).toContain('walk:virtual:0');
    });
});

describe('AnimationClipStreamingScheduler global reset', () => {
    it('resets all clips when no clipId is provided', () => {
        const scheduler = new AnimationClipStreamingScheduler([
            makeStreamedClip('walk'),
            makeStreamedClip('run'),
        ]);
        scheduler.update([activity('walk', 0.1), activity('run', 0.1)]);
        scheduler.markChunkLoaded('walk', 'walk:virtual:0');
        scheduler.markChunkLoaded('run', 'run:virtual:0');
        // Global reset clears all state
        scheduler.reset();
        const snapshot = scheduler.update([activity('walk', 0.1), activity('run', 0.1)]);
        // After reset, chunks should be re-requested
        expect(snapshot.pendingRequests.length).toBeGreaterThan(0);
    });
});

describe('AnimationClipStreamingScheduler snapshot content', () => {
    it('contains chunk snapshots with correct fields', () => {
        const scheduler = new AnimationClipStreamingScheduler([makeStreamedClip('walk', 3)]);
        const snapshot = scheduler.update([activity('walk', 0.1)]);
        expect(snapshot.clips).toHaveLength(1);
        const clipSnapshot = snapshot.clips[0]!;
        expect(clipSnapshot.clipId).toBe('walk');
        expect(clipSnapshot.enabled).toBe(true);
        expect(clipSnapshot.mode).toBe('streamed');
        expect(clipSnapshot.priority).toBe(3);
        expect(clipSnapshot.chunks.length).toBeGreaterThan(0);
        expect(clipSnapshot.chunks[0]!.status).toBe('requested');
    });

    it('reports ready=true when all active chunks are loaded', () => {
        const scheduler = new AnimationClipStreamingScheduler([makeStreamedClip('walk')]);
        scheduler.update([activity('walk', 0.1)]);
        scheduler.markChunkLoaded('walk', 'walk:virtual:0');
        const snapshot = scheduler.update([activity('walk', 0.1)]);
        expect(snapshot.clips[0]?.ready).toBe(true);
    });
});

describe('optimizeAnimationClipDefinition codec none', () => {
    it('skips optimization when codec is none', () => {
        const optimized = optimizeAnimationClipDefinition({
            id: 'no-opt',
            tracks: [
                {
                    target: 'root',
                    path: 'translation',
                    times: [0, 0.5, 1],
                    values: [0, 0, 0, 0.5, 0, 0, 1, 0, 0],
                },
            ],
            compression: { codec: 'none' },
        });
        // codec 'none' returns the original track unchanged (no keyframeCount added)
        expect(optimized.tracks[0]?.times.length).toBe(3);
    });
});

describe('optimizeAnimationClipDefinition custom tolerances', () => {
    it('uses rotationToleranceDegrees for rotation tracks', () => {
        const optimized = optimizeAnimationClipDefinition({
            id: 'rotation',
            tracks: [
                {
                    target: 'root',
                    path: 'rotation',
                    times: [0, 0.5, 1],
                    values: [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1],
                },
            ],
            compression: { codec: 'keyframe-reduced', rotationToleranceDegrees: 180 },
        });
        // With very high tolerance, middle keyframe should be removed
        expect(optimized.tracks[0]?.keyframeCount).toBe(2);
    });

    it('uses scaleTolerance for scale tracks', () => {
        const optimized = optimizeAnimationClipDefinition({
            id: 'scale',
            tracks: [
                {
                    target: 'root',
                    path: 'scale',
                    times: [0, 0.5, 1],
                    values: [1, 1, 1, 1.0001, 1, 1, 1, 1, 1],
                },
            ],
            compression: { codec: 'keyframe-reduced', scaleTolerance: 0.01 },
        });
        expect(optimized.tracks[0]?.keyframeCount).toBe(2);
    });
});

describe('optimizeAnimationClipDefinition invalid component count', () => {
    it('throws for zero component count', () => {
        expect(() =>
            optimizeAnimationClipDefinition({
                id: 'bad',
                tracks: [
                    {
                        target: 'root',
                        path: 'translation',
                        times: [0, 0.5, 1],
                        values: [0, 0, 0, 0.5, 0, 0, 1, 0, 0],
                        valueComponentCount: 0,
                    },
                ],
                compression: { codec: 'keyframe-reduced' },
            })
        ).toThrow(AnimationValidationError);
    });
});

describe('optimizeAnimationClipDefinitions batch', () => {
    it('optimizes multiple clips with shared compression', () => {
        const results = optimizeAnimationClipDefinitions(
            [
                {
                    id: 'a',
                    tracks: [
                        {
                            target: 'root',
                            path: 'translation',
                            times: [0, 0.5, 1],
                            values: [0, 0, 0, 0.5, 0, 0, 1, 0, 0],
                        },
                    ],
                },
                {
                    id: 'b',
                    tracks: [
                        {
                            target: 'root',
                            path: 'translation',
                            times: [0, 0.5, 1],
                            values: [0, 0, 0, 0.5, 0, 0, 1, 0, 0],
                        },
                    ],
                },
            ],
            { codec: 'keyframe-reduced', positionTolerance: 1e-3 }
        );
        expect(results).toHaveLength(2);
        expect(results[0]!.tracks[0]?.keyframeCount).toBe(2);
        expect(results[1]!.tracks[0]?.keyframeCount).toBe(2);
    });
});
