import { describe, expect, it } from 'vitest';
import { Mat4, Vec3 } from '@axrone/numeric';
import { SceneCameraFrameStateCollector } from '../camera-frame-state';
import { Camera } from '../components/camera';

describe('SceneCameraFrameStateCollector', () => {
    it('returns null when camera is undefined', () => {
        const collector = new SceneCameraFrameStateCollector();
        expect(collector.collect(undefined, 1920, 1080)).toBeNull();
    });

    it('collects frame state from a camera', () => {
        const collector = new SceneCameraFrameStateCollector();
        const camera = new Camera({ fieldOfView: 60 });
        const state = collector.collect(camera, 1920, 1080);

        expect(state).not.toBeNull();
        expect(state!.camera).toBe(camera);
        expect(state!.viewMatrix).toBeInstanceOf(Mat4);
        expect(state!.projectionMatrix).toBeInstanceOf(Mat4);
        expect(state!.viewProjectionMatrix).toBeInstanceOf(Mat4);
        expect(state!.position).toBeInstanceOf(Vec3);
    });

    it('reuses the same state object across calls (no allocation)', () => {
        const collector = new SceneCameraFrameStateCollector();
        const camera = new Camera();

        const first = collector.collect(camera, 1920, 1080);
        const second = collector.collect(camera, 1920, 1080);

        expect(first).toBe(second);
        expect(first!.viewMatrix).toBe(second!.viewMatrix);
        expect(first!.projectionMatrix).toBe(second!.projectionMatrix);
        expect(first!.viewProjectionMatrix).toBe(second!.viewProjectionMatrix);
        expect(first!.position).toBe(second!.position);
    });

    it('updates matrices when viewport changes', () => {
        const collector = new SceneCameraFrameStateCollector();
        const camera = new Camera();

        const state1 = collector.collect(camera, 1920, 1080);
        const proj1Data = [...state1!.projectionMatrix.data];

        collector.collect(camera, 800, 600);
        const proj2Data = [...state1!.projectionMatrix.data];

        expect(proj1Data).not.toEqual(proj2Data);
    });

    it('handles zero-height viewport without throwing', () => {
        const collector = new SceneCameraFrameStateCollector();
        const camera = new Camera();
        expect(() => collector.collect(camera, 1920, 0)).not.toThrow();
    });

    it('provides camera3D reference', () => {
        const collector = new SceneCameraFrameStateCollector();
        const camera = new Camera();
        const state = collector.collect(camera, 1920, 1080);
        expect(state!.camera3D).toBeDefined();
        expect(state!.camera3D).toBe(camera.getRuntimeCamera(1920 / 1080));
    });
});
