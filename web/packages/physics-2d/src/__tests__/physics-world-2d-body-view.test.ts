import { describe, it, expect, beforeEach } from 'vitest';
import { createPhysicsBody2DView } from '../core/physics-world-2d-body-view';
import type { BodyId, IPhysicsBody2D } from '../types';

/**
 * Creates a minimal mock of the deps required by createPhysicsBody2DView.
 * All manager methods are stubbed with sensible defaults that can be overridden.
 */
function createMockDeps(overrides: Record<string, any> = {}) {
    const bodyManager = {
        getBodyType: (_id: BodyId) => 2,
        getPosition: (_id: BodyId) => ({ x: 0, y: 0 }),
        getRotation: (_id: BodyId) => 0,
        getLinearVelocity: (_id: BodyId) => ({ x: 0, y: 0 }),
        getAngularVelocity: (_id: BodyId) => 0,
        getMass: (_id: BodyId) => 1,
        getInertia: (_id: BodyId) => 0.5,
        getInverseMass: (_id: BodyId) => 1,
        getInverseInertia: (_id: BodyId) => 2,
        getLocalCenter: (_id: BodyId) => ({ x: 0, y: 0 }),
        getFlags: (_id: BodyId) => ({ awake: true, enabled: true, fixedRotation: false, bullet: false }),
        getGravityScale: (_id: BodyId) => 1,
        getLinearDamping: (_id: BodyId) => 0,
        getAngularDamping: (_id: BodyId) => 0,
        getSleepTime: (_id: BodyId) => 0,
        getUserData: (_id: BodyId) => undefined,
        isAwake: (_id: BodyId) => true,
        isEnabled: (_id: BodyId) => true,
        isFixedRotation: (_id: BodyId) => false,
        isBullet: (_id: BodyId) => false,
        setPosition: () => {},
        setRotation: () => {},
        setLinearVelocity: () => {},
        setAngularVelocity: () => {},
        setEnabled: () => {},
        setAwake: () => {},
        setFixedRotation: () => {},
        setBullet: () => {},
        applyForce: () => {},
        applyForceToCenter: () => {},
        applyTorque: () => {},
        applyImpulse: () => {},
        applyImpulseToCenter: () => {},
        applyAngularImpulse: () => {},
        setMassData: () => {},
        ...overrides.bodyManager,
    };

    const shapeManager = {
        getShapesForBody: (_id: BodyId) => [] as any[],
        ...overrides.shapeManager,
    };

    const getBodyWorldCenter = overrides.getBodyWorldCenter ?? ((_id: BodyId) => ({ x: 0, y: 0 }));
    const resetBodyMassData = overrides.resetBodyMassData ?? (() => {});

    return { bodyManager, shapeManager, getBodyWorldCenter, resetBodyMassData };
}

describe('createPhysicsBody2DView', () => {
    const BODY_ID = 1 as BodyId;
    let deps: ReturnType<typeof createMockDeps>;
    let view: IPhysicsBody2D;

    beforeEach(() => {
        deps = createMockDeps();
        view = createPhysicsBody2DView(BODY_ID, deps);
    });

    describe('identity', () => {
        it('returns the body id', () => {
            expect(view.id).toBe(BODY_ID);
        });
    });

    describe('type', () => {
        it('delegates to bodyManager.getBodyType', () => {
            expect(view.type).toBe(2);
        });
    });

    describe('transform', () => {
        it('returns position and rotation', () => {
            const t = view.transform;
            expect(t.position).toEqual({ x: 0, y: 0 });
            expect(t.rotation).toBe(0);
        });
    });

    describe('velocity', () => {
        it('returns linear and angular velocity', () => {
            const v = view.velocity;
            expect(v.linear).toEqual({ x: 0, y: 0 });
            expect(v.angular).toBe(0);
        });

        it('returns a clone of linear velocity', () => {
            const v1 = view.velocity;
            const v2 = view.velocity;
            expect(v1.linear).not.toBe(v2.linear);
        });
    });

    describe('massData', () => {
        it('returns mass data from the body manager', () => {
            const md = view.massData;
            expect(md.mass).toBe(1);
            expect(md.inertia).toBe(0.5);
            expect(md.inverseMass).toBe(1);
            expect(md.inverseInertia).toBe(2);
        });
    });

    describe('shapes', () => {
        it('delegates to shapeManager.getShapesForBody', () => {
            expect(view.shapes).toEqual([]);
        });
    });

    describe('flags', () => {
        it('returns flags from body manager', () => {
            expect(view.flags).toEqual({ awake: true, enabled: true, fixedRotation: false, bullet: false });
        });
    });

    describe('damping and gravity', () => {
        it('returns gravityScale', () => {
            expect(view.gravityScale).toBe(1);
        });

        it('returns linearDamping', () => {
            expect(view.linearDamping).toBe(0);
        });

        it('returns angularDamping', () => {
            expect(view.angularDamping).toBe(0);
        });
    });

    describe('sleep', () => {
        it('isSleeping returns false when awake', () => {
            expect(view.isSleeping()).toBe(false);
        });

        it('isAwake returns true when awake', () => {
            expect(view.isAwake()).toBe(true);
        });

        it('setSleeping delegates to setAwake with inverted value', () => {
            let calledWith: boolean | undefined;
            deps.bodyManager.setAwake = (_id: BodyId, awake: boolean) => { calledWith = awake; };
            view.setSleeping(true);
            expect(calledWith).toBe(false);
        });
    });

    describe('position and rotation', () => {
        it('getPosition returns cloned position', () => {
            const p = view.getPosition();
            expect(p).toEqual({ x: 0, y: 0 });
        });

        it('setPosition delegates to bodyManager', () => {
            let calledWith: any;
            deps.bodyManager.setPosition = (_id: BodyId, pos: any) => { calledWith = pos; };
            view.setPosition({ x: 5, y: 10 });
            expect(calledWith).toEqual({ x: 5, y: 10 });
        });

        it('getRotation delegates to bodyManager', () => {
            expect(view.getRotation()).toBe(0);
        });

        it('setRotation delegates to bodyManager', () => {
            let calledWith: number | undefined;
            deps.bodyManager.setRotation = (_id: BodyId, rot: number) => { calledWith = rot; };
            view.setRotation(1.5);
            expect(calledWith).toBe(1.5);
        });

        it('setTransform sets both position and rotation', () => {
            let pos: any; let rot: number | undefined;
            deps.bodyManager.setPosition = (_id: BodyId, p: any) => { pos = p; };
            deps.bodyManager.setRotation = (_id: BodyId, r: number) => { rot = r; };
            view.setTransform({ x: 3, y: 4 }, 0.5);
            expect(pos).toEqual({ x: 3, y: 4 });
            expect(rot).toBe(0.5);
        });
    });

    describe('velocity setters', () => {
        it('setLinearVelocity delegates', () => {
            let calledWith: any;
            deps.bodyManager.setLinearVelocity = (_id: BodyId, v: any) => { calledWith = v; };
            view.setLinearVelocity({ x: 1, y: 2 });
            expect(calledWith).toEqual({ x: 1, y: 2 });
        });

        it('setAngularVelocity delegates', () => {
            let calledWith: number | undefined;
            deps.bodyManager.setAngularVelocity = (_id: BodyId, v: number) => { calledWith = v; };
            view.setAngularVelocity(3);
            expect(calledWith).toBe(3);
        });
    });

    describe('coordinate transforms', () => {
        it('getLocalPoint transforms world point to local', () => {
            const lp = view.getLocalPoint({ x: 5, y: 5 });
            expect(lp.x).toBeCloseTo(5, 4);
            expect(lp.y).toBeCloseTo(5, 4);
        });

        it('getWorldPoint transforms local point to world', () => {
            const wp = view.getWorldPoint({ x: 1, y: 2 });
            expect(wp.x).toBeCloseTo(1, 4);
            expect(wp.y).toBeCloseTo(2, 4);
        });

        it('getLocalVector / getWorldVector roundtrip', () => {
            const wv = view.getWorldVector({ x: 1, y: 0 });
            const lv = view.getLocalVector(wv);
            expect(lv.x).toBeCloseTo(1, 4);
            expect(lv.y).toBeCloseTo(0, 4);
        });
    });

    describe('force and impulse methods', () => {
        it('applyForce delegates to bodyManager', () => {
            let called = false;
            deps.bodyManager.applyForce = () => { called = true; };
            view.applyForce({ x: 1, y: 0 });
            expect(called).toBe(true);
        });

        it('applyImpulseToCenter delegates', () => {
            let called = false;
            deps.bodyManager.applyImpulseToCenter = () => { called = true; };
            view.applyImpulseToCenter({ x: 5, y: 0 });
            expect(called).toBe(true);
        });

        it('applyTorque delegates', () => {
            let calledWith: number | undefined;
            deps.bodyManager.applyTorque = (_id: BodyId, t: number) => { calledWith = t; };
            view.applyTorque(2.5);
            expect(calledWith).toBe(2.5);
        });
    });

    describe('flag toggles', () => {
        it('setEnabled delegates', () => {
            let calledWith: boolean | undefined;
            deps.bodyManager.setEnabled = (_id: BodyId, e: boolean) => { calledWith = e; };
            view.setEnabled(false);
            expect(calledWith).toBe(false);
        });

        it('setFixedRotation delegates', () => {
            let calledWith: boolean | undefined;
            deps.bodyManager.setFixedRotation = (_id: BodyId, f: boolean) => { calledWith = f; };
            view.setFixedRotation(true);
            expect(calledWith).toBe(true);
        });

        it('setBullet delegates', () => {
            let calledWith: boolean | undefined;
            deps.bodyManager.setBullet = (_id: BodyId, b: boolean) => { calledWith = b; };
            view.setBullet(true);
            expect(calledWith).toBe(true);
        });
    });

    describe('mass data', () => {
        it('getMass returns mass from bodyManager', () => {
            expect(view.getMass()).toBe(1);
        });

        it('getInertia returns inertia from bodyManager', () => {
            expect(view.getInertia()).toBe(0.5);
        });

        it('setMassData delegates to bodyManager.setMassData', () => {
            let called = false;
            deps.bodyManager.setMassData = () => { called = true; };
            view.setMassData({ mass: 5 as any, inverseMass: 0.2, inertia: 3 as any, inverseInertia: 1 / 3, center: { x: 0, y: 0 } });
            expect(called).toBe(true);
        });

        it('resetMassData delegates to resetBodyMassData', () => {
            let calledWith: BodyId | undefined;
            deps.resetBodyMassData = (id: BodyId) => { calledWith = id; };
            view.resetMassData();
            expect(calledWith).toBe(BODY_ID);
        });
    });

    describe('world/local center', () => {
        it('getWorldCenter delegates to getBodyWorldCenter', () => {
            deps = createMockDeps({ getBodyWorldCenter: () => ({ x: 7, y: 8 }) });
            view = createPhysicsBody2DView(BODY_ID, deps);
            const c = view.getWorldCenter();
            expect(c).toEqual({ x: 7, y: 8 });
        });

        it('getLocalCenter returns cloned local center', () => {
            const c = view.getLocalCenter();
            expect(c).toEqual({ x: 0, y: 0 });
        });
    });
});
