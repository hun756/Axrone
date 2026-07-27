import { describe, expect, it } from 'vitest';
import { AnimationClip } from '../clip';
import { optimizeAnimationClipDefinition } from '../optimization';
import { AnimationCurveLayout } from '../pose';
import { AnimationRig } from '../rig';
import { AnimationClipStreamingScheduler } from '../streaming';
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
