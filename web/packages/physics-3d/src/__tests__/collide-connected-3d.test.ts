import { describe, it, expect, beforeEach } from 'vitest';
import { PhysicsWorld3D } from '@axrone/physics-3d';
import type { BodyId3D, ConstraintId3D } from '@axrone/physics-3d';

/**
 * Acceptance tests for `collideConnected` joint flag in 3D.
 *
 * Verifies that when a joint has `collideConnected=false`, the connected
 * body pair does NOT generate contacts (Box2D semantic A — no contact,
 * no manifold, no event). When `collideConnected=true`, contacts ARE
 * generated normally.
 */

const DT = 1 / 60;

function createWorld(): PhysicsWorld3D {
    return new PhysicsWorld3D({
        gravity: { x: 0, y: 0, z: 0 },
        maxBodies: 64,
        maxShapes: 64,
        maxConstraints: 64,
    });
}

/** Create two overlapping dynamic bodies with sphere shapes. */
function createOverlappingBodies(world: PhysicsWorld3D): { bodyA: BodyId3D; bodyB: BodyId3D } {
    const bodyA = world.createBody({
        type: 2,
        position: { x: 0, y: 0, z: 0 },
    });
    world.createSphereShape(bodyA, { center: { x: 0, y: 0, z: 0 }, radius: 1 });

    const bodyB = world.createBody({
        type: 2,
        position: { x: 0.5, y: 0, z: 0 },
    });
    world.createSphereShape(bodyB, { center: { x: 0, y: 0, z: 0 }, radius: 1 });

    return { bodyA, bodyB };
}

describe('collideConnected filtering (3D)', () => {
    let world: PhysicsWorld3D;

    beforeEach(() => {
        world = createWorld();
    });

    describe('Basic filtering', () => {
        it('collideConnected=false: no contact between joined bodies', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: false,
            });

            world.step(DT);
            expect(world.getStatistics().contactCount).toBe(0);
        });

        it('collideConnected=true: contact IS generated between joined bodies (positive control)', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: true,
            });

            world.step(DT);
            expect(world.getStatistics().contactCount).toBeGreaterThan(0);
        });

        it('no joint: contact IS generated between overlapping bodies (baseline)', () => {
            createOverlappingBodies(world);

            world.step(DT);
            expect(world.getStatistics().contactCount).toBeGreaterThan(0);
        });
    });

    describe('Event behavior', () => {
        it('collideConnected=false: onCollisionBegin does NOT fire', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: false,
            });

            let began = false;
            world.setContactListener({
                onCollisionBegin: () => { began = true; },
            } as any);

            world.step(DT);
            expect(began).toBe(false);
        });

        it('collideConnected=true: onCollisionBegin DOES fire', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: true,
            });

            let began = false;
            world.setContactListener({
                onCollisionBegin: () => { began = true; },
            } as any);

            world.step(DT);
            expect(began).toBe(true);
        });
    });

    describe('Cleanup on constraint destroy', () => {
        it('destroying constraint before first step removes filter, contacts appear', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            const jointId = world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: false,
            });

            expect(world.getConstraintManager().constraintCount).toBe(1);

            // Destroy the joint BEFORE any stepping
            world.destroyConstraint(jointId);

            // Now step — bodies are overlapping, no filter, contacts should form
            world.step(DT);
            expect(world.getStatistics().contactCount).toBeGreaterThan(0);
        });

        it('after destroyConstraint post-step, filter entry is cleared', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            const jointId = world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: false,
            });

            // Step with joint: no contacts
            world.step(DT);
            expect(world.getStatistics().contactCount).toBe(0);

            // Destroy the joint
            world.destroyConstraint(jointId);

            // Create fresh overlapping bodies — they should collide freely
            const bodyC = world.createBody({
                type: 2,
                position: { x: 10, y: 0, z: 0 },
            });
            world.createSphereShape(bodyC, { center: { x: 0, y: 0, z: 0 }, radius: 1 });
            const bodyD = world.createBody({
                type: 2,
                position: { x: 10.5, y: 0, z: 0 },
            });
            world.createSphereShape(bodyD, { center: { x: 0, y: 0, z: 0 }, radius: 1 });

            world.step(DT);
            expect(world.getStatistics().contactCount).toBeGreaterThan(0);
        });
    });

    describe('Cleanup on body destroy', () => {
        it('destroying a body with a collideConnected joint does not leak index entries', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: false,
            });

            // Destroy body B (should also destroy its constraints)
            world.destroyBody(bodyB);

            // Create a new body C overlapping with A
            const bodyC = world.createBody({
                type: 2,
                position: { x: 0.5, y: 0, z: 0 },
            });
            world.createSphereShape(bodyC, { center: { x: 0, y: 0, z: 0 }, radius: 1 });

            // bodyA and bodyC should collide (no stale suppression)
            world.step(DT);
            expect(world.getStatistics().contactCount).toBeGreaterThan(0);
        });
    });

    describe('Multi-joint edge case', () => {
        it('two joints between same bodies: if ANY has collideConnected=false, no contact', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            // Joint 1: collideConnected=true
            world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: true,
            });

            // Joint 2: collideConnected=false → counter=1, suppressed
            world.createSpringConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 1, y: 0, z: 0 },
                localAnchorB: { x: 1, y: 0, z: 0 },
                collideConnected: false,
            });

            world.step(DT);
            expect(world.getStatistics().contactCount).toBe(0);
        });

        it('two joints both collideConnected=false: removing one still suppresses', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            const joint1 = world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: false,
            });

            world.createSpringConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 1, y: 0, z: 0 },
                localAnchorB: { x: 1, y: 0, z: 0 },
                collideConnected: false,
            });

            // counter=2
            world.step(DT);
            expect(world.getStatistics().contactCount).toBe(0);

            // Remove one: counter=1, still suppressed
            world.destroyConstraint(joint1);
            world.step(DT);
            expect(world.getStatistics().contactCount).toBe(0);
        });
    });

    describe('Joint types', () => {
        it('hinge joint with collideConnected=false suppresses contact', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createHingeConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                localAxisA: { x: 0, y: 1, z: 0 },
                localAxisB: { x: 0, y: 1, z: 0 },
                collideConnected: false,
            });

            world.step(DT);
            expect(world.getStatistics().contactCount).toBe(0);
        });

        it('spring joint with collideConnected=false suppresses contact', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createSpringConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: false,
            });

            world.step(DT);
            expect(world.getStatistics().contactCount).toBe(0);
        });
    });

    describe('Independence from other filters', () => {
        it('collideConnected=true does not suppress contacts (filter independent)', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createFixedConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0, z: 0 },
                localAnchorB: { x: 0, y: 0, z: 0 },
                collideConnected: true,
            });

            world.step(DT);
            expect(world.getStatistics().contactCount).toBeGreaterThan(0);
        });
    });
});
