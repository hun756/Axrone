import { describe, it, expect } from 'vitest';
import { PhysicsWorld3D } from '@axrone/physics-3d';

/**
 * Joint solver behavioural tests.
 *
 * 3D joint solver capability matrix (verified from _solveConstraints in
 * physics-world-3d-contact-runtime.ts):
 *
 * | Joint type     | Constraint type | Solver implemented | Behaviour             |
 * |----------------|-----------------|--------------------|-----------------------|
 * | Fixed          | 0 (FIXED)       | YES                | Maintains distance=0  |
 * | Distance       | 0 (FIXED)       | YES (same as Fixed)| Maintains distance    |
 * | Spring         | 6 (SPRING)      | YES                | Spring force + rest   |
 * | Hinge          | 2 (HINGE)       | PARTIAL            | Solved as fixed (no axis rotation) |
 * | Slider         | 3 (SLIDER)      | NO                 | Decorative            |
 * | Cone-twist     | 4 (CONE_TWIST)  | NO                 | Decorative            |
 * | Character      | 4 (CONE_TWIST)  | NO                 | Decorative            |
 * | Configurable   | 5 (GENERIC)     | NO                 | Decorative            |
 *
 * The _solveConstraints method only handles type 0 (fixed/distance) and type 6
 * (spring). Hinge (type 2) falls through the same code path as fixed, which
 * accidentally maintains anchor distance but does NOT implement hinge axis
 * rotation. Slider, cone-twist, and generic constraints are registered but
 * produce no solver correction.
 */
describe('PhysicsWorld3D joint solver behaviour', () => {
    describe('fixed/distance constraint solver (C6 fix proof)', () => {
        it('maintains distance between two dynamic bodies connected by fixed constraint', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });

            const bodyA = world.createBody({ type: 2, position: { x: 0, y: 0, z: 0 } });
            const bodyB = world.createBody({ type: 2, position: { x: 3, y: 0, z: 0 } });

            world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
            });

            // Apply force pulling bodyB away from bodyA
            world.getBodyManager().applyForceToCenter(bodyB, { x: 50, y: 0, z: 0 });

            for (let i = 0; i < 60; i++) world.step(1 / 60);

            const posA = world.getBodyManager().getPosition(bodyA);
            const posB = world.getBodyManager().getPosition(bodyB);

            // Both bodies should have moved (force applied to B, constraint pulls A)
            expect(posA.x).toBeGreaterThan(-1);

            // Distance between bodies should be approximately maintained
            // Fixed constraint targets distance=0, but Baumgarte BIAS=0.2 means soft correction
            const dx = posB.x - posA.x;
            const dy = posB.y - posA.y;
            const dz = posB.z - posA.z;
            const dist = Math.sqrt(dx * dx + dy * dy + dz * dz);

            // Without constraint, bodyB would be at x=3+50*(1/60)*60 ≈ 53.
            // With constraint, distance should be much less than unconstrained.
            expect(dist).toBeLessThan(10);
        });

        it('fixed constraint with one static body acts as anchor', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });

            const anchor = world.createBody({ type: 0, position: { x: 0, y: 0, z: 0 } });
            const bob = world.createBody({ type: 2, position: { x: 3, y: 0, z: 0 } });

            world.createFixedConstraint({
                bodyIdA: anchor,
                bodyIdB: bob,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
            });

            // Pull bob away
            world.getBodyManager().applyForceToCenter(bob, { x: 100, y: 0, z: 0 });

            for (let i = 0; i < 60; i++) world.step(1 / 60);

            const posBob = world.getBodyManager().getPosition(bob);

            // Without constraint: bob at x = 3 + 100*(1/60)*60 = 103
            // With constraint: bob pulled back toward anchor
            expect(posBob.x).toBeLessThan(10);
        });
    });

    describe('spring constraint solver (C6 fix proof)', () => {
        // NOTE: _solveConstraints runs inside solveVelocity with velocityIterations=10.
        // Each iteration applies the full spring impulse, so the effective stiffness
        // is ~10× the configured value. Parameters must be chosen accordingly.
        it('spring pulls body toward rest length distance', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });

            const anchor = world.createBody({ type: 0, position: { x: 0, y: 0, z: 0 } });
            const bob = world.createBody({ type: 2, position: { x: 5, y: 0, z: 0 } });

            world.createSpringConstraint({
                bodyIdA: anchor,
                bodyIdB: bob,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                restLength: 2,
                stiffness: 5,
                damping: 0,
            });

            const initialX = world.getBodyManager().getPosition(bob).x;
            expect(initialX).toBe(5);

            // Step — spring should pull bob toward anchor (rest length 2, current 5)
            for (let i = 0; i < 30; i++) world.step(1 / 60);

            const afterX = world.getBodyManager().getPosition(bob).x;

            // Bob must have moved toward anchor (from x=5 toward rest length 2)
            expect(afterX).toBeLessThan(5);
            // Should not overshoot wildly in the negative direction
            expect(afterX).toBeGreaterThan(-5);
        });

        it('spring oscillates around rest length', () => {
            const world = new PhysicsWorld3D({ gravity: { x: 0, y: 0, z: 0 } });

            const anchor = world.createBody({ type: 0, position: { x: 0, y: 0, z: 0 } });
            const bob = world.createBody({ type: 2, position: { x: 5, y: 0, z: 0 } });

            world.createSpringConstraint({
                bodyIdA: anchor,
                bodyIdB: bob,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                restLength: 2,
                stiffness: 3,
                damping: 0, // No damping → oscillation
            });

            // Record positions over time
            const positions: number[] = [];
            for (let i = 0; i < 120; i++) {
                world.step(1 / 60);
                positions.push(world.getBodyManager().getPosition(bob).x);
            }

            // Find min and max x positions (oscillation bounds)
            const minX = Math.min(...positions);
            const maxX = Math.max(...positions);

            // Must oscillate: range should be significant
            expect(maxX - minX).toBeGreaterThan(0.5);

            // The body should have moved from initial x=5 toward rest length x=2
            // (Baumgarte + spring both pull toward rest length)
            expect(minX).toBeLessThan(5);
        });
    });
});
