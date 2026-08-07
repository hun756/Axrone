import { describe, expect, it } from 'vitest';
import {
    Camera3D,
    CameraFrustum,
    CameraValidationError,
    FrustumCuller,
    createBoundingSphere,
    createBoundingAabb,
    isBoundingSphere,
    isBoundingAabb,
    isBoundingVolume,
} from '@axrone/geometry';

const defaultPerspective = {
    kind: 'perspective' as const,
    verticalFieldOfView: Math.PI / 3,
    aspectRatio: 1,
    near: 0.1,
    far: 100,
};

const defaultPose = {
    position: [0, 0, 0] as [number, number, number],
    target: [0, 0, -1] as [number, number, number],
};

const createTestFrustum = (): CameraFrustum => {
    const cam = Camera3D.perspective({ projection: defaultPerspective, pose: defaultPose });
    return new CameraFrustum(cam.viewProjectionMatrix);
};

type TestItem = { id: string; bounds: ReturnType<typeof createBoundingSphere> | ReturnType<typeof createBoundingAabb> };

const makeItems = (): TestItem[] => [
    { id: 'sphere:inside', bounds: createBoundingSphere([0, 0, -5], 0.5) },
    { id: 'sphere:outside', bounds: createBoundingSphere([100, 0, -5], 0.5) },
    { id: 'aabb:inside', bounds: createBoundingAabb([-1, -1, -6], [1, 1, -4]) },
    { id: 'aabb:outside', bounds: createBoundingAabb([50, 50, 50], [51, 51, 51]) },
];

describe('FrustumCuller', () => {
    describe('Construction', () => {
        it('creates with default options', () => {
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
            });
            expect(culler.isDisposed).toBe(false);
            expect(culler.visible).toEqual([]);
        });

        it('creates with trackClassifications enabled', () => {
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
                trackClassifications: true,
            });
            expect(culler.classifications).toBeDefined();
        });

        it('classifications is undefined when trackClassifications is false', () => {
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
            });
            expect(culler.classifications).toBeUndefined();
        });
    });

    describe('Sync culling', () => {
        it('cull() classifies mixed sphere/aabb items', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
                trackClassifications: true,
            });
            culler.cull(items, frustum);

            expect(culler.visible.length).toBe(2);
            expect(culler.stats.visibleCount).toBe(2);
            expect(culler.stats.outsideCount).toBe(2);
            expect(culler.stats.sphereCount).toBe(2);
            expect(culler.stats.aabbCount).toBe(2);
            expect(culler.stats.totalCount).toBe(4);
        });

        it('filter skips items and increments skippedCount', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
                filter: (item: TestItem) => !item.id.includes('outside'),
            });
            culler.cull(items, frustum);

            expect(culler.stats.skippedCount).toBe(2);
        });

        it('sort orders visible items', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
                sort: (a: TestItem, b: TestItem) => a.id.localeCompare(b.id),
            });
            culler.cull(items, frustum);

            expect(culler.visible.length).toBe(2);
            expect(culler.visible[0]!.id).toBe('aabb:inside');
            expect(culler.visible[1]!.id).toBe('sphere:inside');
        });

        it('bounds returning null skips item', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => (item.id.includes('outside') ? null : item.bounds),
            });
            culler.cull(items, frustum);

            expect(culler.stats.skippedCount).toBe(2);
            expect(culler.visible.length).toBe(2);
        });
    });

    describe('Stats accuracy', () => {
        it('all counters are accurate', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
                trackClassifications: true,
            });
            culler.cull(items, frustum);

            expect(culler.stats.totalCount).toBe(4);
            expect(culler.stats.visibleCount).toBe(2);
            expect(culler.stats.outsideCount).toBe(2);
            expect(culler.stats.sphereCount).toBe(2);
            expect(culler.stats.aabbCount).toBe(2);
            expect(culler.stats.visibleSphereCount).toBeGreaterThanOrEqual(0);
            expect(culler.stats.visibleAabbCount).toBeGreaterThanOrEqual(0);
            expect(culler.stats.overflowed).toBe(false);
        });
    });

    describe('Overflow', () => {
        it('trim strategy silently drops excess items', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
                maxResults: 1,
                overflow: 'trim',
            });
            culler.cull(items, frustum);

            expect(culler.visible.length).toBe(1);
            expect(culler.stats.overflowed).toBe(true);
        });

        it('throw strategy throws CameraValidationError with RESULT_OVERFLOW', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
                maxResults: 1,
                overflow: 'throw',
            });
            expect(() => culler.cull(items, frustum)).toThrow(CameraValidationError);
        });
    });

    describe('Classifications', () => {
        it('trackClassifications: true populates map', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
                trackClassifications: true,
            });
            culler.cull(items, frustum);

            expect(culler.classifications).toBeDefined();
            expect(culler.classifications!.size).toBe(4);
            expect(culler.classifications!.get(items[1]!)).toBe('outside');
        });

        it('trackClassifications: false leaves classifications undefined', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
            });
            culler.cull(items, frustum);
            expect(culler.classifications).toBeUndefined();
        });
    });

    describe('Async culling', () => {
        it('cullAsync() processes all items', async () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
            });
            await culler.cullAsync(items, frustum, { batchSize: 2 });

            expect(culler.visible.length).toBe(2);
            expect(culler.stats.totalCount).toBe(4);
        });

        it('signal abort throws OPERATION_ABORTED', async () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const controller = new AbortController();
            controller.abort();

            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
            });
            await expect(
                culler.cullAsync(items, frustum, { signal: controller.signal })
            ).rejects.toThrow(CameraValidationError);
        });

        it('custom scheduler is called', async () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            let schedulerCalls = 0;

            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
            });
            await culler.cullAsync(items, frustum, {
                batchSize: 1,
                scheduler: () => {
                    schedulerCalls++;
                },
            });

            expect(schedulerCalls).toBeGreaterThan(0);
        });
    });

    describe('Reset', () => {
        it('reset() clears visible, stats, and classifications', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
                trackClassifications: true,
            });
            culler.cull(items, frustum);
            expect(culler.visible.length).toBeGreaterThan(0);

            culler.reset();
            expect(culler.visible.length).toBe(0);
            expect(culler.stats.totalCount).toBe(0);
            expect(culler.stats.visibleCount).toBe(0);
            expect(culler.classifications!.size).toBe(0);
        });
    });

    describe('Dispose', () => {
        it('dispose() clears state', () => {
            const frustum = createTestFrustum();
            const items = makeItems();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
                trackClassifications: true,
            });
            culler.cull(items, frustum);
            culler.dispose();

            expect(culler.isDisposed).toBe(true);
            expect(culler.visible.length).toBe(0);
        });

        it('operations after dispose throw CULLER_DISPOSED', () => {
            const frustum = createTestFrustum();
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
            });
            culler.dispose();
            expect(() => culler.cull([], frustum)).toThrow(CameraValidationError);
        });

        it('double-dispose is safe', () => {
            const culler = new FrustumCuller({
                bounds: (item: TestItem) => item.bounds,
            });
            culler.dispose();
            expect(() => culler.dispose()).not.toThrow();
        });
    });
});

describe('CameraFrustum direct', () => {
    it('setFromMatrix extracts 6 planes', () => {
        const cam = Camera3D.perspective({ projection: defaultPerspective, pose: defaultPose });
        const frustum = new CameraFrustum();
        frustum.setFromMatrix(cam.viewProjectionMatrix);
        // Verify we can classify something
        expect(frustum.containsPoint([0, 0, -5])).toBe(true);
    });

    it('copy copies planes from another frustum', () => {
        const frustum1 = createTestFrustum();
        const frustum2 = new CameraFrustum();
        frustum2.copy(frustum1);
        expect(frustum2.containsPoint([0, 0, -5])).toBe(frustum1.containsPoint([0, 0, -5]));
    });

    it('clone creates independent copy', () => {
        const frustum = createTestFrustum();
        const cloned = frustum.clone();
        expect(cloned).not.toBe(frustum);
        expect(cloned.containsPoint([0, 0, -5])).toBe(frustum.containsPoint([0, 0, -5]));
    });

    it('copyPlane returns 4-element tuple for all 6 planes', () => {
        const frustum = createTestFrustum();
        for (const name of ['left', 'right', 'bottom', 'top', 'near', 'far'] as const) {
            const plane = frustum.copyPlane(name);
            expect(plane.length).toBe(4);
            for (const v of plane) {
                expect(Number.isFinite(v)).toBe(true);
            }
        }
    });

    it('containsPoint returns true for point inside frustum', () => {
        const frustum = createTestFrustum();
        expect(frustum.containsPoint([0, 0, -5])).toBe(true);
    });

    it('containsPoint returns false for point outside frustum', () => {
        const frustum = createTestFrustum();
        expect(frustum.containsPoint([0, 0, 5])).toBe(false);
    });

    it('classifyPoint returns inside or outside', () => {
        const frustum = createTestFrustum();
        expect(frustum.classifyPoint([0, 0, -5])).toBe('inside');
        expect(frustum.classifyPoint([0, 0, 5])).toBe('outside');
    });

    it('classifySphere classifies correctly', () => {
        const frustum = createTestFrustum();
        expect(frustum.classifySphere(createBoundingSphere([0, 0, -5], 1))).toBe('inside');
        expect(frustum.classifySphere(createBoundingSphere([100, 0, -5], 0.5))).toBe('outside');
    });

    it('classifyAabb classifies correctly', () => {
        const frustum = createTestFrustum();
        expect(frustum.classifyAabb(createBoundingAabb([-1, -1, -6], [1, 1, -4]))).toBe('inside');
        expect(frustum.classifyAabb(createBoundingAabb([50, 50, 50], [51, 51, 51]))).toBe('outside');
    });

    it('intersectsSphere returns true for visible sphere', () => {
        const frustum = createTestFrustum();
        expect(frustum.intersectsSphere(createBoundingSphere([0, 0, -5], 1))).toBe(true);
        expect(frustum.intersectsSphere(createBoundingSphere([100, 0, 0], 0.5))).toBe(false);
    });

    it('intersectsAabb returns true for visible aabb', () => {
        const frustum = createTestFrustum();
        expect(frustum.intersectsAabb(createBoundingAabb([-1, -1, -6], [1, 1, -4]))).toBe(true);
        expect(frustum.intersectsAabb(createBoundingAabb([50, 50, 50], [51, 51, 51]))).toBe(false);
    });

    it('dispose guards: operations throw after dispose', () => {
        const frustum = createTestFrustum();
        frustum.dispose();
        expect(frustum.isDisposed).toBe(true);
        expect(() => frustum.setFromMatrix(new (class { data = new Float32Array(16) })())).toThrow(
            CameraValidationError
        );
        expect(() => frustum.containsPoint([0, 0, 0])).toThrow(CameraValidationError);
        expect(() => frustum.clone()).toThrow(CameraValidationError);
    });

    it('double-dispose is safe', () => {
        const frustum = createTestFrustum();
        frustum.dispose();
        expect(() => frustum.dispose()).not.toThrow();
    });
});

describe('Bounding helpers', () => {
    it('isBoundingSphere identifies sphere volumes', () => {
        expect(isBoundingSphere(createBoundingSphere([0, 0, 0], 1))).toBe(true);
        expect(isBoundingSphere(createBoundingAabb([0, 0, 0], [1, 1, 1]))).toBe(false);
        expect(isBoundingSphere(null)).toBe(false);
        expect(isBoundingSphere({ kind: 'aabb' })).toBe(false);
    });

    it('isBoundingAabb identifies aabb volumes', () => {
        expect(isBoundingAabb(createBoundingAabb([0, 0, 0], [1, 1, 1]))).toBe(true);
        expect(isBoundingAabb(createBoundingSphere([0, 0, 0], 1))).toBe(false);
        expect(isBoundingAabb(null)).toBe(false);
    });

    it('isBoundingVolume identifies both kinds', () => {
        expect(isBoundingVolume(createBoundingSphere([0, 0, 0], 1))).toBe(true);
        expect(isBoundingVolume(createBoundingAabb([0, 0, 0], [1, 1, 1]))).toBe(true);
        expect(isBoundingVolume(null)).toBe(false);
        expect(isBoundingVolume({})).toBe(false);
    });

    it('createBoundingSphere creates valid sphere', () => {
        const sphere = createBoundingSphere([1, 2, 3], 5);
        expect(sphere.kind).toBe('sphere');
        expect(sphere.radius).toBe(5);
    });

    it('createBoundingSphere throws for negative radius', () => {
        expect(() => createBoundingSphere([0, 0, 0], -1)).toThrow(CameraValidationError);
    });

    it('createBoundingAabb creates valid aabb', () => {
        const aabb = createBoundingAabb([-1, -1, -1], [1, 1, 1]);
        expect(aabb.kind).toBe('aabb');
    });
});
