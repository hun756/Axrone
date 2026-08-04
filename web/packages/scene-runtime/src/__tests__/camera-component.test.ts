import { describe, expect, it } from 'vitest';
import { Vec3, Vec4, Mat4 } from '@axrone/numeric';
import { Camera, resolveCameraVerticalFieldOfViewRadians } from '../components/camera';

describe('Camera', () => {
    describe('constructor defaults', () => {
        it('creates a camera with sensible defaults', () => {
            const camera = new Camera();
            expect(camera.primary).toBe(false);
            expect(camera.near).toBe(0.1);
            expect(camera.far).toBe(1000);
            expect(camera.fieldOfView).toBe(60);
            expect(camera.fieldOfViewAxis).toBe('vertical');
            expect(camera.orthographic).toBe(false);
            expect(camera.orthographicSize).toBe(5);
            expect(camera.clearDepth).toBe(1);
            expect(camera.clearFlags).toEqual(['color', 'depth']);
        });

        it('accepts custom configuration', () => {
            const camera = new Camera({
                primary: true,
                near: 0.5,
                far: 500,
                fieldOfView: 90,
                fieldOfViewAxis: 'horizontal',
                orthographic: true,
                orthographicSize: 10,
                clearFlags: ['color'],
                clearDepth: 0.5,
                clearColor: new Vec4(1, 0, 0, 1),
            });

            expect(camera.primary).toBe(true);
            expect(camera.near).toBe(0.5);
            expect(camera.far).toBe(500);
            expect(camera.fieldOfView).toBe(90);
            expect(camera.fieldOfViewAxis).toBe('horizontal');
            expect(camera.orthographic).toBe(true);
            expect(camera.orthographicSize).toBe(10);
            expect(camera.clearFlags).toEqual(['color']);
            expect(camera.clearDepth).toBe(0.5);
            expect(camera.clearColor.x).toBe(1);
            expect(camera.clearColor.y).toBe(0);
        });
    });

    describe('property setters', () => {
        it('normalizes fieldOfViewAxis to vertical for unknown values', () => {
            const camera = new Camera();
            camera.fieldOfViewAxis = 'invalid' as any;
            expect(camera.fieldOfViewAxis).toBe('vertical');
        });

        it('accepts clearColor as array tuple', () => {
            const camera = new Camera();
            camera.clearColor = [0.5, 0.6, 0.7, 0.8];
            expect(camera.clearColor.x).toBeCloseTo(0.5);
            expect(camera.clearColor.y).toBeCloseTo(0.6);
            expect(camera.clearColor.z).toBeCloseTo(0.7);
            expect(camera.clearColor.w).toBeCloseTo(0.8);
        });

        it('filters invalid clear flags', () => {
            const camera = new Camera();
            camera.clearFlags = ['color', 'invalid' as any, 'depth', 'color'];
            expect(camera.clearFlags).toEqual(['color', 'depth']);
        });

        it('uses default clear flags for empty array', () => {
            const camera = new Camera();
            camera.clearFlags = [];
            expect(camera.clearFlags).toEqual(['color', 'depth']);
        });
    });

    describe('serialize', () => {
        it('serializes all camera properties', () => {
            const camera = new Camera({
                primary: true,
                near: 0.5,
                far: 200,
                fieldOfView: 75,
                fieldOfViewAxis: 'horizontal',
                orthographic: true,
                orthographicSize: 8,
                clearFlags: ['depth'],
                clearDepth: 0.9,
                clearColor: new Vec4(0.1, 0.2, 0.3, 1),
            });

            const data = camera.serialize();
            expect(data).toEqual({
                primary: true,
                near: 0.5,
                far: 200,
                fieldOfView: 75,
                fieldOfViewAxis: 'horizontal',
                orthographic: true,
                orthographicSize: 8,
                clearFlags: ['depth'],
                clearDepth: 0.9,
                clearColor: [0.1, 0.2, 0.3, 1],
            });
        });
    });

    describe('deserialize', () => {
        it('restores all properties from serialized data', () => {
            const camera = new Camera();
            camera.deserialize({
                primary: true,
                near: 0.25,
                far: 800,
                fieldOfView: 45,
                fieldOfViewAxis: 'horizontal',
                orthographic: true,
                orthographicSize: 12,
                clearFlags: ['color'],
                clearDepth: 0.5,
                clearColor: [1, 0, 0, 1],
            });

            expect(camera.primary).toBe(true);
            expect(camera.near).toBe(0.25);
            expect(camera.far).toBe(800);
            expect(camera.fieldOfView).toBe(45);
            expect(camera.fieldOfViewAxis).toBe('horizontal');
            expect(camera.orthographic).toBe(true);
            expect(camera.orthographicSize).toBe(12);
            expect(camera.clearFlags).toEqual(['color']);
            expect(camera.clearDepth).toBe(0.5);
            expect(camera.clearColor.x).toBe(1);
        });

        it('ignores missing properties', () => {
            const camera = new Camera({ near: 0.5, far: 200 });
            camera.deserialize({ primary: true });
            expect(camera.primary).toBe(true);
            expect(camera.near).toBe(0.5);
            expect(camera.far).toBe(200);
        });

        it('ignores wrong-type properties', () => {
            const camera = new Camera({ near: 0.5 });
            camera.deserialize({ near: 'not-a-number' as any });
            expect(camera.near).toBe(0.5);
        });

        it('ignores clearColor with wrong length', () => {
            const camera = new Camera({ clearColor: new Vec4(1, 0, 0, 1) });
            camera.deserialize({ clearColor: [0.5, 0.5] });
            expect(camera.clearColor.x).toBe(1);
        });
    });

    describe('serialize ↔ deserialize roundtrip', () => {
        it('preserves all properties through roundtrip', () => {
            const original = new Camera({
                primary: true,
                near: 0.3,
                far: 600,
                fieldOfView: 80,
                fieldOfViewAxis: 'horizontal',
                orthographic: false,
                orthographicSize: 7,
                clearFlags: ['color', 'depth'],
                clearDepth: 0.8,
                clearColor: new Vec4(0.2, 0.4, 0.6, 1),
            });

            const restored = new Camera();
            restored.deserialize(original.serialize());

            expect(restored.primary).toBe(original.primary);
            expect(restored.near).toBe(original.near);
            expect(restored.far).toBe(original.far);
            expect(restored.fieldOfView).toBe(original.fieldOfView);
            expect(restored.fieldOfViewAxis).toBe(original.fieldOfViewAxis);
            expect(restored.orthographic).toBe(original.orthographic);
            expect(restored.orthographicSize).toBe(original.orthographicSize);
            expect(restored.clearFlags).toEqual([...original.clearFlags]);
            expect(restored.clearDepth).toBe(original.clearDepth);
            expect(restored.clearColor.x).toBeCloseTo(original.clearColor.x);
            expect(restored.clearColor.y).toBeCloseTo(original.clearColor.y);
            expect(restored.clearColor.z).toBeCloseTo(original.clearColor.z);
            expect(restored.clearColor.w).toBeCloseTo(original.clearColor.w);
        });
    });

    describe('getRuntimeCamera', () => {
        it('returns a camera with perspective projection by default', () => {
            const camera = new Camera({ fieldOfView: 60 });
            const runtime = camera.getRuntimeCamera(16 / 9);
            expect(runtime.projection.kind).toBe('perspective');
        });

        it('returns orthographic projection when configured', () => {
            const camera = new Camera({ orthographic: true, orthographicSize: 5 });
            const runtime = camera.getRuntimeCamera(2);
            expect(runtime.projection.kind).toBe('orthographic');
            const proj = runtime.projection as any;
            expect(proj.left).toBeCloseTo(-10);
            expect(proj.right).toBeCloseTo(10);
            expect(proj.bottom).toBeCloseTo(-5);
            expect(proj.top).toBeCloseTo(5);
        });

        it('clamps aspect ratio to minimum 0.001', () => {
            const camera = new Camera();
            expect(() => camera.getRuntimeCamera(0)).not.toThrow();
            expect(() => camera.getRuntimeCamera(-1)).not.toThrow();
        });

        it('reuses the same Camera3D instance across calls', () => {
            const camera = new Camera();
            const first = camera.getRuntimeCamera(1);
            const second = camera.getRuntimeCamera(2);
            expect(first).toBe(second);
        });
    });

    describe('matrix accessors without transform', () => {
        it('getViewMatrix returns a Mat4', () => {
            const camera = new Camera();
            const view = camera.getViewMatrix();
            expect(view).toBeInstanceOf(Mat4);
        });

        it('getProjectionMatrix returns a Mat4', () => {
            const camera = new Camera();
            const proj = camera.getProjectionMatrix(16 / 9);
            expect(proj).toBeInstanceOf(Mat4);
        });

        it('getViewProjectionMatrix returns a Mat4', () => {
            const camera = new Camera();
            const vp = camera.getViewProjectionMatrix(16 / 9);
            expect(vp).toBeInstanceOf(Mat4);
        });

        it('getWorldPosition returns Vec3.ZERO when no transform', () => {
            const camera = new Camera();
            const pos = camera.getWorldPosition();
            expect(pos.x).toBe(0);
            expect(pos.y).toBe(0);
            expect(pos.z).toBe(0);
        });
    });
});

describe('resolveCameraVerticalFieldOfViewRadians', () => {
    it('returns direct radian conversion for vertical axis', () => {
        const result = resolveCameraVerticalFieldOfViewRadians(90, 'vertical', 1);
        expect(result).toBeCloseTo(Math.PI / 2);
    });

    it('converts horizontal FOV to vertical using aspect ratio', () => {
        const result = resolveCameraVerticalFieldOfViewRadians(90, 'horizontal', 1);
        expect(result).toBeCloseTo(Math.PI / 2);
    });

    it('produces smaller vertical FOV for wide aspect with horizontal axis', () => {
        const vertical = resolveCameraVerticalFieldOfViewRadians(60, 'vertical', 2);
        const horizontal = resolveCameraVerticalFieldOfViewRadians(60, 'horizontal', 2);
        expect(horizontal).toBeLessThan(vertical);
    });

    it('clamps aspect ratio to 0.001 minimum', () => {
        expect(() => resolveCameraVerticalFieldOfViewRadians(60, 'horizontal', 0)).not.toThrow();
    });
});
