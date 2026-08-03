import { describe, it, expect } from 'vitest';
import { SphereCollider3D } from '../../components/SphereCollider3D';
import { BoxCollider3D } from '../../components/BoxCollider3D';
import { CapsuleCollider3D } from '../../components/CapsuleCollider3D';
import { CylinderCollider3D } from '../../components/CylinderCollider3D';
import { MeshCollider3D } from '../../components/MeshCollider3D';
import { Collider3D } from '../../components/collider3d';

describe('Collider3D base (via SphereCollider3D)', () => {
    function create() { return new SphereCollider3D(); }

    describe('default state', () => {
        it('has isTrigger false', () => { expect(create().isTrigger).toBe(false); });
        it('has providesContacts true', () => { expect(create().providesContacts).toBe(true); });
        it('has contactOffset 0.01', () => { expect(create().contactOffset).toBe(0.01); });
        it('has categoryBits 1', () => { expect(create().categoryBits).toBe(1); });
        it('has maskBits 0xffff', () => { expect(create().maskBits).toBe(0xffff); });
        it('has groupIndex 0', () => { expect(create().groupIndex).toBe(0); });
        it('has center at origin', () => {
            const c = create().center;
            expect(c.x).toBe(0); expect(c.y).toBe(0); expect(c.z).toBe(0);
        });
        it('has attachedRigidbody null', () => { expect(create().attachedRigidbody).toBeNull(); });
    });

    describe('isTrigger', () => {
        it('can be toggled', () => {
            const c = create();
            c.isTrigger = true;
            expect(c.isTrigger).toBe(true);
        });
    });

    describe('material', () => {
        it('has default staticFriction 0.6', () => {
            expect(create().material.staticFriction).toBe(0.6);
        });
        it('has default dynamicFriction 0.6', () => {
            expect(create().material.dynamicFriction).toBe(0.6);
        });
        it('has default bounciness 0', () => {
            expect(create().material.bounciness).toBe(0);
        });
        it('sets partial material', () => {
            const c = create();
            c.material = { staticFriction: 0.9 };
            expect(c.material.staticFriction).toBe(0.9);
            expect(c.material.dynamicFriction).toBe(0.6);
        });
        it('clamps negative friction to 0', () => {
            const c = create();
            c.material = { staticFriction: -1 };
            expect(c.material.staticFriction).toBe(0);
        });
        it('clamps bounciness to [0,1]', () => {
            const c = create();
            c.material = { bounciness: 2 };
            expect(c.material.bounciness).toBe(1);
        });
    });

    describe('filter setters', () => {
        it('sets categoryBits', () => {
            const c = create();
            c.categoryBits = 0x02;
            expect(c.categoryBits).toBe(0x02);
        });
        it('sets maskBits', () => {
            const c = create();
            c.maskBits = 0x04;
            expect(c.maskBits).toBe(0x04);
        });
        it('sets groupIndex', () => {
            const c = create();
            c.groupIndex = -1;
            expect(c.groupIndex).toBe(-1);
        });
    });

    describe('center', () => {
        it('sets center', () => {
            const c = create();
            c.center = { x: 1, y: 2, z: 3 };
            expect(c.center.x).toBe(1);
            expect(c.center.y).toBe(2);
            expect(c.center.z).toBe(3);
        });
    });
});

describe('SphereCollider3D', () => {
    it('has default radius 0.5', () => {
        expect(new SphereCollider3D().radius).toBe(0.5);
    });
    it('sets radius', () => {
        const c = new SphereCollider3D();
        c.radius = 2;
        expect(c.radius).toBe(2);
    });
    it('clamps minimum radius to 0.001', () => {
        const c = new SphereCollider3D();
        c.radius = 0;
        expect(c.radius).toBe(0.001);
    });
});

describe('BoxCollider3D', () => {
    it('has default size (1,1,1)', () => {
        const s = new BoxCollider3D().size;
        expect(s.x).toBe(1); expect(s.y).toBe(1); expect(s.z).toBe(1);
    });
    it('sets size', () => {
        const c = new BoxCollider3D();
        c.size = { x: 3, y: 4, z: 5 };
        expect(c.size.x).toBe(3); expect(c.size.y).toBe(4); expect(c.size.z).toBe(5);
    });
    it('clamps minimum size to 0.001', () => {
        const c = new BoxCollider3D();
        c.size = { x: 0, y: 0, z: 0 };
        expect(c.size.x).toBeCloseTo(0.001, 3);
    });
});

describe('CapsuleCollider3D', () => {
    it('has default radius 0.5', () => {
        expect(new CapsuleCollider3D().radius).toBe(0.5);
    });
    it('has default height 2', () => {
        expect(new CapsuleCollider3D().height).toBe(2);
    });
    it('sets radius', () => {
        const c = new CapsuleCollider3D();
        c.radius = 1;
        expect(c.radius).toBe(1);
    });
    it('sets height', () => {
        const c = new CapsuleCollider3D();
        c.height = 5;
        expect(c.height).toBe(5);
    });
});

describe('CylinderCollider3D', () => {
    it('has default radius 0.5', () => {
        expect(new CylinderCollider3D().radius).toBe(0.5);
    });
    it('has default height 2', () => {
        expect(new CylinderCollider3D().height).toBe(2);
    });
});

describe('MeshCollider3D', () => {
    it('can be created', () => {
        expect(() => new MeshCollider3D()).not.toThrow();
    });
    it('has convex true by default', () => {
        const c = new MeshCollider3D();
        expect((c as any).convex).toBe(true);
    });
});
