import { describe, expect, it } from 'vitest';
import * as raycastPackage from '@axrone/raycast';
import * as physicsPackage from '@axrone/physics';

describe('raycast entry', () => {
    it('surfaces the raycast subsystem ownership', () => {
        expect(raycastPackage.RaycastEngine2D).toBeDefined();
        expect(raycastPackage.RaycastEngine3D).toBeDefined();
        expect(raycastPackage.RayPrimitiveIntersector2D).toBeDefined();
        expect(raycastPackage.RayPrimitiveIntersector3D).toBeDefined();
        expect(raycastPackage.BoundingVolumeHierarchy).toBeDefined();
        expect(raycastPackage.SpatialHashGrid3D).toBeDefined();
        expect(raycastPackage.SpatialHashBroadphase3D).toBeDefined();
        expect(raycastPackage.OctreeBroadphase3D).toBeDefined();
        expect(raycastPackage.RaycastSystem2D).toBeDefined();
        expect(raycastPackage.RaycastSystem3D).toBeDefined();
        expect(raycastPackage.createRaycastSystem2D).toBeDefined();
        expect(raycastPackage.createRaycastSystem3D).toBeDefined();
        expect(raycastPackage.RaycastFlags).toBeDefined();
        expect(raycastPackage.RaycastLayer).toBeDefined();
        // Runtime enum erasure guard: values must survive to runtime.
        expect(raycastPackage.RaycastLayer.All).toBeDefined();
    });

    it('does not own simulation world or body management surfaces', () => {
        expect('PhysicsWorld2D' in raycastPackage).toBe(false);
        expect('PhysicsWorld3D' in raycastPackage).toBe(false);
        expect('BodyManager2D' in raycastPackage).toBe(false);
        expect('BodyManager3D' in raycastPackage).toBe(false);
        expect('DynamicAABBTree2D' in raycastPackage).toBe(false);
    });

    it('stays fully re-exported through the physics facade', () => {
        expect(physicsPackage.RaycastEngine2D).toBe(raycastPackage.RaycastEngine2D);
        expect(physicsPackage.RaycastSystem3D).toBe(raycastPackage.RaycastSystem3D);
        expect(physicsPackage.RaycastFlags).toBe(raycastPackage.RaycastFlags);
        expect(physicsPackage.RaycastLayer).toBe(raycastPackage.RaycastLayer);
        expect(physicsPackage.SpatialHashBroadphase3D).toBe(
            raycastPackage.SpatialHashBroadphase3D
        );
    });
});
