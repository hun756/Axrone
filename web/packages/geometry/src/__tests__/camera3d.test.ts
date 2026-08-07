import { describe, expect, it } from 'vitest';
import {
    Camera3D,
    CameraValidationError,
    CameraSerializationError,
    createBoundingSphere,
    createBoundingAabb,
} from '@axrone/geometry';

const defaultPerspective = {
    kind: 'perspective' as const,
    verticalFieldOfView: Math.PI / 3,
    aspectRatio: 1,
    near: 0.1,
    far: 100,
};

const defaultOrtho = {
    kind: 'orthographic' as const,
    left: -5,
    right: 5,
    bottom: -5,
    top: 5,
    near: 0.1,
    far: 100,
};

const defaultPose = {
    position: [0, 0, 5] as [number, number, number],
    target: [0, 0, 0] as [number, number, number],
    up: [0, 1, 0] as [number, number, number],
};

describe('Camera3D', () => {
    describe('Construction', () => {
        it('Camera3D.perspective() creates a perspective camera', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            expect(cam.isPerspective()).toBe(true);
            expect(cam.isOrthographic()).toBe(false);
        });

        it('Camera3D.orthographic() creates an orthographic camera', () => {
            const cam = Camera3D.orthographic({
                projection: defaultOrtho,
                pose: defaultPose,
            });
            expect(cam.isOrthographic()).toBe(true);
            expect(cam.isPerspective()).toBe(false);
        });

        it('generic constructor works with perspective projection', () => {
            const cam = new Camera3D({ projection: defaultPerspective, pose: defaultPose });
            expect(cam.projection.kind).toBe('perspective');
        });
    });

    describe('Properties', () => {
        it('exposes id, locale, near, far, position, target, up', () => {
            const cam = Camera3D.perspective({
                id: 'test-cam',
                locale: 'en',
                projection: defaultPerspective,
                pose: defaultPose,
            });
            expect(cam.id).toBe('test-cam');
            expect(cam.locale).toBe('en');
            expect(cam.near).toBe(0.1);
            expect(cam.far).toBe(100);
            expect(cam.position.x).toBe(0);
            expect(cam.position.y).toBe(0);
            expect(cam.position.z).toBe(5);
            expect(cam.target.x).toBe(0);
            expect(cam.target.y).toBe(0);
            expect(cam.target.z).toBe(0);
            expect(cam.up.x).toBe(0);
            expect(cam.up.y).toBe(1);
            expect(cam.up.z).toBe(0);
        });

        it('defaults id to "camera" when not provided', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            expect(cam.id).toBe('camera');
        });

        it('projection property returns frozen projection object', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            expect(cam.projection.kind).toBe('perspective');
            expect(cam.projection.verticalFieldOfView).toBe(Math.PI / 3);
        });
    });

    describe('Matrix correctness', () => {
        it('viewMatrix is a 4x4 matrix with finite values', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            const vm = cam.viewMatrix;
            for (let i = 0; i < 16; i++) {
                expect(Number.isFinite(vm.data[i])).toBe(true);
            }
        });

        it('projectionMatrix is a 4x4 matrix with finite values', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            const pm = cam.projectionMatrix;
            for (let i = 0; i < 16; i++) {
                expect(Number.isFinite(pm.data[i])).toBe(true);
            }
        });

        it('viewProjectionMatrix is a 4x4 matrix with finite values', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            const vpm = cam.viewProjectionMatrix;
            for (let i = 0; i < 16; i++) {
                expect(Number.isFinite(vpm.data[i])).toBe(true);
            }
        });
    });

    describe('Mutation', () => {
        it('setProjection updates projection and triggers re-sync', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            const newProjection = {
                kind: 'perspective' as const,
                verticalFieldOfView: Math.PI / 4,
                aspectRatio: 16 / 9,
                near: 1,
                far: 500,
            };
            cam.setProjection(newProjection);
            expect(cam.projection.verticalFieldOfView).toBe(Math.PI / 4);
            expect(cam.projection.aspectRatio).toBe(16 / 9);
        });

        it('setLocale changes locale', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            cam.setLocale('tr');
            expect(cam.locale).toBe('tr');
        });

        it('setPose updates position, target, and up', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            cam.setPose({
                position: [10, 20, 30],
                target: [0, 0, 0],
                up: [0, 0, 1],
            });
            expect(cam.position.x).toBe(10);
            expect(cam.position.y).toBe(20);
            expect(cam.position.z).toBe(30);
            expect(cam.up.z).toBe(1);
        });

        it('lookAt is shorthand for setPose', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            cam.lookAt([5, 5, 5], [0, 0, 0]);
            expect(cam.position.x).toBe(5);
            expect(cam.position.y).toBe(5);
            expect(cam.position.z).toBe(5);
        });
    });

    describe('Cloning', () => {
        it('clone() preserves state', () => {
            const cam = Camera3D.perspective({
                id: 'original',
                projection: defaultPerspective,
                pose: defaultPose,
            });
            const cloned = cam.clone();
            expect(cloned.id).toBe('original');
            expect(cloned.position.x).toBe(cam.position.x);
            expect(cloned.position.z).toBe(cam.position.z);
            expect(cloned.projection.kind).toBe('perspective');
        });

        it('cloneWithProjection() changes projection kind', () => {
            const cam = Camera3D.perspective({
                id: 'persp-cam',
                projection: defaultPerspective,
                pose: defaultPose,
            });
            const orthoClone = cam.cloneWithProjection(defaultOrtho);
            expect(orthoClone.isOrthographic()).toBe(true);
            expect(orthoClone.id).toBe('persp-cam');
        });
    });

    describe('Serialization', () => {
        it('toJSON() returns frozen object with expected structure', () => {
            const cam = Camera3D.perspective({
                id: 'json-cam',
                projection: defaultPerspective,
                pose: defaultPose,
            });
            const json = cam.toJSON();
            expect(json.id).toBe('json-cam');
            expect(json.projection.kind).toBe('perspective');
            expect(json.pose).toBeDefined();
            expect(json.viewMatrix).toBeDefined();
            expect(json.projectionMatrix).toBeDefined();
            expect(json.viewProjectionMatrix).toBeDefined();
            expect(json.viewMatrix.length).toBe(16);
        });

        it('fromJSON() roundtrips correctly', () => {
            const cam = Camera3D.perspective({
                id: 'roundtrip-cam',
                projection: defaultPerspective,
                pose: defaultPose,
            });
            const json = cam.toJSON();
            const restored = Camera3D.fromJSON(json);
            expect(restored.toJSON()).toEqual(json);
        });

        it('fromJSON() throws CameraSerializationError for invalid payload', () => {
            expect(() => Camera3D.fromJSON(null)).toThrow(CameraSerializationError);
            expect(() => Camera3D.fromJSON('string')).toThrow(CameraSerializationError);
            expect(() => Camera3D.fromJSON({})).toThrow(CameraSerializationError);
        });

        it('fromJSON() throws CameraSerializationError for missing projection', () => {
            expect(() => Camera3D.fromJSON({ id: 'x', pose: defaultPose })).toThrow(
                CameraSerializationError
            );
        });
    });

    describe('Dispose', () => {
        it('isDisposed is false initially, true after dispose', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            expect(cam.isDisposed).toBe(false);
            cam.dispose();
            expect(cam.isDisposed).toBe(true);
        });

        it('zeroes position, target, and up vectors', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            cam.dispose();
            expect(cam.position.x).toBe(0);
            expect(cam.position.y).toBe(0);
            expect(cam.position.z).toBe(0);
            expect(cam.target.x).toBe(0);
            expect(cam.target.y).toBe(0);
            expect(cam.target.z).toBe(0);
            expect(cam.up.x).toBe(0);
            expect(cam.up.y).toBe(0);
            expect(cam.up.z).toBe(0);
        });

        it('viewMatrix throws CameraValidationError after dispose', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            cam.dispose();
            expect(() => cam.viewMatrix).toThrow(CameraValidationError);
        });

        it('projectionMatrix throws CameraValidationError after dispose', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            cam.dispose();
            expect(() => cam.projectionMatrix).toThrow(CameraValidationError);
        });

        it('frustum throws CameraValidationError after dispose', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            cam.dispose();
            expect(() => cam.frustum).toThrow(CameraValidationError);
        });

        it('double-dispose is safe', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            cam.dispose();
            expect(() => cam.dispose()).not.toThrow();
        });

        it('dispose error has CAMERA_DISPOSED code', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: defaultPose,
            });
            cam.dispose();
            try {
                cam.viewMatrix;
                expect.unreachable('should have thrown');
            } catch (e) {
                expect(e).toBeInstanceOf(CameraValidationError);
                expect((e as CameraValidationError).code).toBe('CAMERA_DISPOSED');
            }
        });
    });

    describe('Validation', () => {
        it('throws for negative FOV', () => {
            expect(() =>
                Camera3D.perspective({
                    projection: { ...defaultPerspective, verticalFieldOfView: -1 },
                    pose: defaultPose,
                })
            ).toThrow(CameraValidationError);
        });

        it('throws for FOV >= PI', () => {
            expect(() =>
                Camera3D.perspective({
                    projection: { ...defaultPerspective, verticalFieldOfView: Math.PI },
                    pose: defaultPose,
                })
            ).toThrow(CameraValidationError);
        });

        it('throws for near >= far', () => {
            expect(() =>
                Camera3D.perspective({
                    projection: { ...defaultPerspective, near: 100, far: 10 },
                    pose: defaultPose,
                })
            ).toThrow(CameraValidationError);
        });

        it('throws for negative aspect ratio', () => {
            expect(() =>
                Camera3D.perspective({
                    projection: { ...defaultPerspective, aspectRatio: -1 },
                    pose: defaultPose,
                })
            ).toThrow(CameraValidationError);
        });

        it('throws for orthographic left == right', () => {
            expect(() =>
                Camera3D.orthographic({
                    projection: { ...defaultOrtho, left: 5, right: 5 },
                    pose: defaultPose,
                })
            ).toThrow(CameraValidationError);
        });

        it('throws for orthographic bottom == top', () => {
            expect(() =>
                Camera3D.orthographic({
                    projection: { ...defaultOrtho, bottom: 5, top: 5 },
                    pose: defaultPose,
                })
            ).toThrow(CameraValidationError);
        });

        it('throws for orthographic near >= far', () => {
            expect(() =>
                Camera3D.orthographic({
                    projection: { ...defaultOrtho, near: 50, far: 10 },
                    pose: defaultPose,
                })
            ).toThrow(CameraValidationError);
        });

        it('throws for position == target (invalid pose) on matrix access', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: { position: [0, 0, 0], target: [0, 0, 0] },
            });
            // Validation is lazy – triggered on first synchronize()
            expect(() => cam.viewMatrix).toThrow(CameraValidationError);
        });
    });

    describe('Frustum integration', () => {
        it('classify() returns inside for sphere in view', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: {
                    position: [0, 0, 0],
                    target: [0, 0, -1],
                },
            });
            expect(cam.classify(createBoundingSphere([0, 0, -5], 1))).toBe('inside');
        });

        it('classify() returns outside for sphere behind camera', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: {
                    position: [0, 0, 0],
                    target: [0, 0, -1],
                },
            });
            expect(cam.classify(createBoundingSphere([0, 0, 5], 0.5))).toBe('outside');
        });

        it('intersects() returns true for visible aabb', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: {
                    position: [0, 0, 0],
                    target: [0, 0, -1],
                },
            });
            expect(cam.intersects(createBoundingAabb([-1, -1, -6], [1, 1, -4]))).toBe(true);
        });

        it('intersects() returns false for invisible aabb', () => {
            const cam = Camera3D.perspective({
                projection: defaultPerspective,
                pose: {
                    position: [0, 0, 0],
                    target: [0, 0, -1],
                },
            });
            expect(cam.intersects(createBoundingAabb([50, 50, 50], [51, 51, 51]))).toBe(false);
        });
    });
});
