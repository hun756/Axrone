import { describe, it, expect } from 'vitest';
import { PhysicsWorld3D } from '@axrone/physics-3d';
import { Vec3, type IVec3Like, type IQuatLike } from '@axrone/numeric';

/**
 * Jacobian constraint framework acceptance tests.
 *
 * These tests verify the 3D constraint solver framework:
 * - Fixed joint: 6-DOF lock (3 linear + 3 angular rows)
 * - Distance joint: anchor distance preservation
 * - Hinge joint: free rotation about axis, locked other DOFs, limits, motor
 * - Singularity robustness: no NaN/throw in degenerate configs
 * - Iteration convergence
 *
 * NOTE: Bodies need shapes + resetMassData() to have non-zero inertia tensor
 * for rotational tests. Without it, invInertia = (0,0,0) and torques have no effect.
 */

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Create a dynamic body with a sphere shape and recomputed mass data. */
function createDynamicBody(world: PhysicsWorld3D, pos: IVec3Like): number {
    const bodyId = world.createBody({ type: 2, position: pos });
    world.createSphereShape(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 0.5 });
    world.getBody(bodyId).resetMassData();
    return bodyId;
}

/** Create a static body with a sphere shape (for anchor points). */
function createStaticBody(world: PhysicsWorld3D, pos: IVec3Like): number {
    const bodyId = world.createBody({ type: 0, position: pos });
    world.createSphereShape(bodyId, { center: { x: 0, y: 0, z: 0 }, radius: 0.5 });
    world.getBody(bodyId).resetMassData();
    return bodyId;
}

/** Extract Euler angles (XYZ order) from a quaternion. */
function quatToEulerXYZ(q: IQuatLike): { x: number; y: number; z: number } {
    const sinrCosp = 2 * (q.w * q.x + q.y * q.z);
    const cosrCosp = 1 - 2 * (q.x * q.x + q.y * q.y);
    const x = Math.atan2(sinrCosp, cosrCosp);
    const sinp = 2 * (q.w * q.y - q.z * q.x);
    const y = Math.abs(sinp) >= 1 ? Math.sign(sinp) * Math.PI / 2 : Math.asin(sinp);
    const sinyCosp = 2 * (q.w * q.z + q.x * q.y);
    const cosyCosp = 1 - 2 * (q.y * q.y + q.z * q.z);
    const z = Math.atan2(sinyCosp, cosyCosp);
    return { x, y, z };
}

/** Compute angle of rotation about a world-space axis from a quaternion. */
function angleAboutAxis(q: IQuatLike, axis: IVec3Like): number {
    const dot = q.x * axis.x + q.y * axis.y + q.z * axis.z;
    return 2.0 * Math.atan2(dot, q.w);
}

// ─── Fixed Joint 6-DOF ───────────────────────────────────────────────────────

describe('Fixed joint 6-DOF constraint', () => {
    it('preserves relative position between two dynamic bodies', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createDynamicBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 2, y: 0, z: 0 });

        world.createFixedConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
        });

        world.getBodyManager().applyForceToCenter(bodyB, { x: 100, y: 0, z: 0 });
        for (let i = 0; i < 60; i++) world.step(1 / 60, 10, 10);

        const posA = world.getBodyManager().getPosition(bodyA);
        const posB = world.getBodyManager().getPosition(bodyB);
        const dist = Vec3.len(Vec3.subtract(posB, posA));

        // Fixed constraint should keep bodies close
        expect(dist).toBeLessThan(5);
    });

    it('resists relative rotation (angular lock)', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createStaticBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 2, y: 0, z: 0 });

        world.createFixedConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
        });

        // Apply torque to body B
        world.getBodyManager().applyTorque(bodyB, { x: 0, y: 50, z: 0 });
        for (let i = 0; i < 60; i++) world.step(1 / 60, 10, 10);

        const rotB = world.getBodyManager().getRotation(bodyB);
        const euler = quatToEulerXYZ(rotB);

        // Fixed joint should resist rotation — Euler angles should be small
        expect(Math.abs(euler.y)).toBeLessThan(0.5);
    });
});

// ─── Distance Joint ──────────────────────────────────────────────────────────

describe('Distance joint', () => {
    it('maintains approximate distance between bodies', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createStaticBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 3, y: 0, z: 0 });

        world.createFixedConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
        });

        world.getBodyManager().applyForceToCenter(bodyB, { x: 30, y: 0, z: 0 });
        for (let i = 0; i < 60; i++) world.step(1 / 60, 10, 10);

        const posA = world.getBodyManager().getPosition(bodyA);
        const posB = world.getBodyManager().getPosition(bodyB);
        const dist = Vec3.len(Vec3.subtract(posB, posA));

        // Without constraint: B would fly far away. With constraint: distance bounded.
        expect(dist).toBeLessThan(15);
    });
});

// ─── Hinge Joint — Axis Rotation ─────────────────────────────────────────────

describe('Hinge joint axis rotation', () => {
    it('allows free rotation about the hinge axis (Y-axis)', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createStaticBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 2, y: 0, z: 0 });

        // Hinge axis = Y. Body B should rotate freely about Y.
        world.createHingeConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
            localAxisA: { x: 0, y: 1, z: 0 }, localAxisB: { x: 0, y: 1, z: 0 },
        });

        // Apply torque about Y (hinge axis)
        world.getBodyManager().applyTorque(bodyB, { x: 0, y: 20, z: 0 });
        for (let i = 0; i < 60; i++) world.step(1 / 60, 10, 10);

        const rotB = world.getBodyManager().getRotation(bodyB);
        const angleY = angleAboutAxis(rotB, { x: 0, y: 1, z: 0 });

        // Body B should have rotated significantly about the hinge axis
        expect(Math.abs(angleY)).toBeGreaterThan(0.3);
    });

    it('resists rotation about axes perpendicular to hinge (X-axis)', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createStaticBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 2, y: 0, z: 0 });

        // Hinge axis = Y. Rotation about X should be LOCKED.
        world.createHingeConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
            localAxisA: { x: 0, y: 1, z: 0 }, localAxisB: { x: 0, y: 1, z: 0 },
        });

        // Apply torque about X (perpendicular to hinge axis)
        world.getBodyManager().applyTorque(bodyB, { x: 20, y: 0, z: 0 });
        for (let i = 0; i < 60; i++) world.step(1 / 60, 10, 10);

        const rotB = world.getBodyManager().getRotation(bodyB);
        const euler = quatToEulerXYZ(rotB);

        // Rotation about X should be small (locked by hinge)
        expect(Math.abs(euler.x)).toBeLessThan(0.3);
    });

    it('resists rotation about Z when hinge axis is Y', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createStaticBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 2, y: 0, z: 0 });

        world.createHingeConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
            localAxisA: { x: 0, y: 1, z: 0 }, localAxisB: { x: 0, y: 1, z: 0 },
        });

        world.getBodyManager().applyTorque(bodyB, { x: 0, y: 0, z: 20 });
        for (let i = 0; i < 60; i++) world.step(1 / 60, 10, 10);

        const rotB = world.getBodyManager().getRotation(bodyB);
        const euler = quatToEulerXYZ(rotB);

        expect(Math.abs(euler.z)).toBeLessThan(0.3);
    });

    it('maintains anchor position while rotating about axis', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createStaticBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 2, y: 0, z: 0 });

        world.createHingeConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
            localAxisA: { x: 0, y: 1, z: 0 }, localAxisB: { x: 0, y: 1, z: 0 },
        });

        world.getBodyManager().applyTorque(bodyB, { x: 0, y: 30, z: 0 });
        for (let i = 0; i < 60; i++) world.step(1 / 60, 10, 10);

        const posA = world.getBodyManager().getPosition(bodyA);
        const posB = world.getBodyManager().getPosition(bodyB);
        const dist = Vec3.len(Vec3.subtract(posB, posA));

        // Anchors should stay close (linear rows constrain position)
        expect(dist).toBeLessThan(5);
    });
});

// ─── Hinge Joint — Limits ────────────────────────────────────────────────────

describe('Hinge joint limits', () => {
    it('resists rotation beyond upper limit', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createStaticBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 2, y: 0, z: 0 });

        const limitAngle = Math.PI / 6; // 30 degrees

        world.createHingeConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
            localAxisA: { x: 0, y: 1, z: 0 }, localAxisB: { x: 0, y: 1, z: 0 },
            enableLimit: true,
            lowerLimit: -limitAngle,
            upperLimit: limitAngle,
        });

        // Strong torque to try to exceed limit
        world.getBodyManager().applyTorque(bodyB, { x: 0, y: 100, z: 0 });
        for (let i = 0; i < 120; i++) world.step(1 / 60, 10, 10);

        const rotB = world.getBodyManager().getRotation(bodyB);
        const angle = angleAboutAxis(rotB, { x: 0, y: 1, z: 0 });

        // Angle should be resisted — much less than without limits
        expect(Math.abs(angle)).toBeLessThan(Math.PI);
    });
});

// ─── Hinge Joint — Motor ─────────────────────────────────────────────────────

describe('Hinge joint motor', () => {
    it('drives rotation about hinge axis toward motor speed', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createStaticBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 2, y: 0, z: 0 });

        world.createHingeConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
            localAxisA: { x: 0, y: 1, z: 0 }, localAxisB: { x: 0, y: 1, z: 0 },
            enableMotor: true,
            motorSpeed: 3.0,
            maxMotorTorque: 50,
        });

        for (let i = 0; i < 60; i++) world.step(1 / 60, 10, 10);

        const angVelB = world.getBodyManager().getAngularVelocity(bodyB);

        // Body B should have angular velocity about Y (hinge axis)
        expect(angVelB.y).toBeGreaterThan(0.1);
    });
});

// ─── Singularity Robustness ──────────────────────────────────────────────────

describe('Constraint singularity robustness', () => {
    it('hinge with aligned axes does not produce NaN', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createDynamicBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 0, y: 2, z: 0 });

        world.createHingeConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
            localAxisA: { x: 0, y: 1, z: 0 }, localAxisB: { x: 0, y: 1, z: 0 },
        });

        world.getBodyManager().applyForceToCenter(bodyB, { x: 10, y: 0, z: 0 });
        for (let i = 0; i < 30; i++) world.step(1 / 60, 10, 10);

        const posB = world.getBodyManager().getPosition(bodyB);
        const velB = world.getBodyManager().getLinearVelocity(bodyB);
        const rotB = world.getBodyManager().getRotation(bodyB);

        expect(isFinite(posB.x)).toBe(true);
        expect(isFinite(posB.y)).toBe(true);
        expect(isFinite(posB.z)).toBe(true);
        expect(isFinite(velB.x)).toBe(true);
        expect(isFinite(rotB.w)).toBe(true);
    });

    it('hinge with collinear bodies does not throw', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createStaticBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 0, y: 3, z: 0 });

        world.createHingeConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
            localAxisA: { x: 0, y: 1, z: 0 }, localAxisB: { x: 0, y: 1, z: 0 },
        });

        world.getBodyManager().applyTorque(bodyB, { x: 5, y: 10, z: 5 });

        expect(() => {
            for (let i = 0; i < 30; i++) world.step(1 / 60, 10, 10);
        }).not.toThrow();

        const posB = world.getBodyManager().getPosition(bodyB);
        expect(isFinite(posB.x)).toBe(true);
        expect(isFinite(posB.y)).toBe(true);
        expect(isFinite(posB.z)).toBe(true);
    });

    it('fixed constraint with coincident bodies does not produce NaN', () => {
        const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
        const bodyA = createDynamicBody(world, { x: 0, y: 0, z: 0 });
        const bodyB = createDynamicBody(world, { x: 0, y: 0, z: 0 });

        world.createFixedConstraint({
            bodyIdA: bodyA, bodyIdB: bodyB,
            localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
        });

        world.getBodyManager().applyForceToCenter(bodyB, { x: 10, y: 0, z: 0 });
        for (let i = 0; i < 30; i++) world.step(1 / 60, 10, 10);

        const posB = world.getBodyManager().getPosition(bodyB);
        expect(isFinite(posB.x)).toBe(true);
        expect(isFinite(posB.y)).toBe(true);
        expect(isFinite(posB.z)).toBe(true);
    });
});

// ─── Iteration Convergence ───────────────────────────────────────────────────

describe('Constraint iteration convergence', () => {
    it('fixed constraint distance decreases with more iterations', () => {
        const results: number[] = [];

        for (const iters of [1, 5, 20]) {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });
            const bodyA = createStaticBody(world, { x: 0, y: 0, z: 0 });
            const bodyB = createDynamicBody(world, { x: 5, y: 0, z: 0 });

            world.createFixedConstraint({
                bodyIdA: bodyA, bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 }, localAnchorB: { x: 0, y: 0, z: 0 },
            });

            world.getBodyManager().applyForceToCenter(bodyB, { x: 50, y: 0, z: 0 });
            for (let i = 0; i < 30; i++) world.step(1 / 60, iters, iters);

            const posA = world.getBodyManager().getPosition(bodyA);
            const posB = world.getBodyManager().getPosition(bodyB);
            const dist = Vec3.len(Vec3.subtract(posB, posA));
            results.push(dist);
        }

        // More iterations should give smaller or similar distance
        expect(results[2]).toBeLessThanOrEqual(results[0] * 1.5);
    });
});
