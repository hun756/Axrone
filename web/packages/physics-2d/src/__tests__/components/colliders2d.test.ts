import { describe, it, expect } from 'vitest';
import { BoxCollider2D } from '../../components/BoxCollider2D';
import { CircleCollider2D } from '../../components/CircleCollider2D';
import { CapsuleCollider2D } from '../../components/CapsuleCollider2D';
import { PolygonCollider2D } from '../../components/PolygonCollider2D';
import { Collider2D } from '../../components/collider2d';

describe('Collider2D base (via BoxCollider2D)', () => {
    function create() { return new BoxCollider2D(); }

    describe('default material', () => {
        it('has default friction 0.4', () => {
            expect(create().friction).toBe(0.4);
        });
        it('has default restitution 0', () => {
            expect(create().restitution).toBe(0);
        });
        it('has default density 1', () => {
            expect(create().density).toBe(1);
        });
    });

    describe('isTrigger', () => {
        it('defaults to false', () => {
            expect(create().isTrigger).toBe(false);
        });
        it('can be toggled', () => {
            const c = create();
            c.isTrigger = true;
            expect(c.isTrigger).toBe(true);
        });
    });

    describe('material', () => {
        it('sets material as a copy', () => {
            const c = create();
            c.material = { friction: 0.8, restitution: 0.5, density: 2 };
            expect(c.friction).toBe(0.8);
            expect(c.restitution).toBe(0.5);
            expect(c.density).toBe(2);
        });
        it('clamps negative friction to 0', () => {
            const c = create();
            c.friction = -1;
            expect(c.friction).toBe(0);
        });
        it('clamps restitution to [0,1]', () => {
            const c = create();
            c.restitution = 2;
            expect(c.restitution).toBe(1);
            c.restitution = -1;
            expect(c.restitution).toBe(0);
        });
    });

    describe('offset', () => {
        it('defaults to zero', () => {
            const c = create();
            expect(c.offset.x).toBe(0);
            expect(c.offset.y).toBe(0);
        });
    });

    describe('collisionFilter', () => {
        it('has default filter values', () => {
            const f = create().collisionFilter as any;
            expect(f.categoryBits).toBe(0x0001);
            expect(f.maskBits).toBe(0xffff);
        });
    });

    describe('shapeId', () => {
        it('is null before start', () => {
            expect(create().shapeId).toBeNull();
        });
    });

    describe('serialize / deserialize', () => {
        it('serialize includes base properties', () => {
            const c = create();
            c.isTrigger = true;
            c.friction = 0.9;
            const data = c.serialize();
            expect(data.isTrigger).toBe(true);
            expect(data.material.friction).toBe(0.9);
        });

        it('deserialize restores base properties', () => {
            const c = create();
            c.deserialize({ isTrigger: true, material: { friction: 0.7, restitution: 0.3, density: 2 } });
            expect(c.isTrigger).toBe(true);
            expect(c.friction).toBe(0.7);
        });
    });
});

describe('BoxCollider2D', () => {
    it('has default size (1,1)', () => {
        const c = new BoxCollider2D();
        expect(c.size.x).toBe(1);
        expect(c.size.y).toBe(1);
    });

    it('has default angle 0', () => {
        expect(new BoxCollider2D().angle).toBe(0);
    });

    it('sets size', () => {
        const c = new BoxCollider2D();
        c.size = { x: 3, y: 4 } as any;
        expect(c.size.x).toBe(3);
        expect(c.size.y).toBe(4);
    });

    it('ignores non-positive size', () => {
        const c = new BoxCollider2D();
        c.size = { x: 0, y: 0 } as any;
        expect(c.size.x).toBe(1);
    });

    it('serializes size and angle', () => {
        const c = new BoxCollider2D();
        c.angle = 0.5;
        const data = c.serialize();
        expect(data.size).toEqual({ x: 1, y: 1 });
        expect(data.angle).toBe(0.5);
    });

    it('deserializes size and angle', () => {
        const c = new BoxCollider2D();
        c.deserialize({ size: { x: 5, y: 6 }, angle: 1.2 });
        expect(c.size.x).toBe(5);
        expect(c.size.y).toBe(6);
        expect(c.angle).toBe(1.2);
    });
});

describe('CircleCollider2D', () => {
    it('has default radius 0.5', () => {
        expect(new CircleCollider2D().radius).toBe(0.5);
    });

    it('sets radius', () => {
        const c = new CircleCollider2D();
        c.radius = 2;
        expect(c.radius).toBe(2);
    });

    it('ignores non-positive radius', () => {
        const c = new CircleCollider2D();
        c.radius = 0;
        expect(c.radius).toBe(0.5);
    });

    it('serializes radius', () => {
        const c = new CircleCollider2D();
        c.radius = 3;
        expect(c.serialize().radius).toBe(3);
    });

    it('deserializes radius', () => {
        const c = new CircleCollider2D();
        c.deserialize({ radius: 7 });
        expect(c.radius).toBe(7);
    });
});

describe('CapsuleCollider2D', () => {
    it('has default length 1 and radius 0.25', () => {
        const c = new CapsuleCollider2D();
        expect(c.length).toBe(1);
        expect(c.radius).toBe(0.25);
    });

    it('has default direction vertical', () => {
        expect(new CapsuleCollider2D().direction).toBe('vertical');
    });

    it('sets length', () => {
        const c = new CapsuleCollider2D();
        c.length = 3;
        expect(c.length).toBe(3);
    });

    it('ignores non-positive length', () => {
        const c = new CapsuleCollider2D();
        c.length = 0;
        expect(c.length).toBe(1);
    });

    it('sets direction', () => {
        const c = new CapsuleCollider2D();
        c.direction = 'horizontal';
        expect(c.direction).toBe('horizontal');
    });

    it('serializes all properties', () => {
        const c = new CapsuleCollider2D();
        c.length = 4;
        c.radius = 1;
        c.direction = 'horizontal';
        const data = c.serialize();
        expect(data.length).toBe(4);
        expect(data.radius).toBe(1);
        expect(data.direction).toBe('horizontal');
    });
});

describe('PolygonCollider2D', () => {
    it('has empty vertices by default', () => {
        expect(new PolygonCollider2D().vertices).toEqual([]);
    });

    it('sets vertices when >= 3', () => {
        const c = new PolygonCollider2D();
        const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
        c.vertices = [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 0, y: 1 }] as any;
        expect(c.vertices).toHaveLength(3);
        warn.mockRestore();
    });

    it('rejects vertices with fewer than 3 points', () => {
        const c = new PolygonCollider2D();
        const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
        c.vertices = [{ x: 0, y: 0 }, { x: 1, y: 0 }] as any;
        expect(c.vertices).toHaveLength(0);
        warn.mockRestore();
    });

    it('setBox creates 4 vertices', () => {
        const c = new PolygonCollider2D();
        c.setBox(2, 4);
        expect(c.vertices).toHaveLength(4);
    });

    it('serializes vertices', () => {
        const c = new PolygonCollider2D();
        c.setBox(2, 2);
        const data = c.serialize();
        expect(data.vertices).toHaveLength(4);
    });

    it('deserializes vertices', () => {
        const c = new PolygonCollider2D();
        c.deserialize({ vertices: [{ x: 0, y: 0 }, { x: 1, y: 0 }, { x: 0, y: 1 }] });
        expect(c.vertices).toHaveLength(3);
    });
});
