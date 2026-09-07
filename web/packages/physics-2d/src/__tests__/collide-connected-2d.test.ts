import { describe, it, expect, beforeEach } from 'vitest';
import { PhysicsWorld2D } from '@axrone/physics-2d';
import { BodyType } from '@axrone/physics-core';
import type { BodyId, ConstraintId } from '@axrone/physics-2d';

/**
 * Acceptance tests for `collideConnected` joint flag in 2D.
 *
 * Verifies that when a joint has `collideConnected=false`, the connected
 * body pair does NOT generate contacts (Box2D semantic A — no contact,
 * no manifold, no event). When `collideConnected=true`, contacts ARE
 * generated normally.
 */

const DT = 1 / 60;

function createWorld(): PhysicsWorld2D {
    return new PhysicsWorld2D({
        gravity: { x: 0, y: 0 },
        bodyCapacity: 64,
        shapeCapacity: 64,
        contactCapacity: 64,
        constraintCapacity: 64,
    });
}

/** Create two overlapping dynamic bodies. */
function createOverlappingBodies(world: PhysicsWorld2D): { bodyA: BodyId; bodyB: BodyId } {
    const bodyA = world.createBody({
        type: BodyType.Dynamic,
        position: { x: 0, y: 0 },
    });
    world.createCircleShape(bodyA, { radius: 1 });

    const bodyB = world.createBody({
        type: BodyType.Dynamic,
        position: { x: 0.5, y: 0 },
    });
    world.createCircleShape(bodyB, { radius: 1 });

    return { bodyA, bodyB };
}

describe('collideConnected filtering (2D)', () => {
    let world: PhysicsWorld2D;

    beforeEach(() => {
        world = createWorld();
    });

    describe('Basic filtering', () => {
        it('collideConnected=false: no contact between joined bodies', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 1,
                collideConnected: false,
            });

            world.step(DT);
            expect(world.getContactManager().contactCount).toBe(0);
        });

        it('collideConnected=true: contact IS generated between joined bodies (positive control)', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 1,
                collideConnected: true,
            });

            world.step(DT);
            expect(world.getContactManager().contactCount).toBeGreaterThan(0);
        });

        it('no joint: contact IS generated between overlapping bodies (baseline)', () => {
            createOverlappingBodies(world);

            world.step(DT);
            expect(world.getContactManager().contactCount).toBeGreaterThan(0);
        });
    });

    describe('Event behavior', () => {
        it('collideConnected=false: onCollisionBegin does NOT fire', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 1,
                collideConnected: false,
            });

            let began = false;
            world.setContactListener({
                onCollisionBegin: () => { began = true; },
            });

            world.step(DT);
            expect(began).toBe(false);
        });

        it('collideConnected=true: onCollisionBegin DOES fire', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 1,
                collideConnected: true,
            });

            let began = false;
            world.setContactListener({
                onCollisionBegin: () => { began = true; },
            });

            world.step(DT);
            expect(began).toBe(true);
        });
    });

    describe('Cleanup on constraint destroy', () => {
        it('destroying constraint before first step removes filter, contacts appear', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            // Create joint with collideConnected=false → filter registered
            const jointId = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 1,
                collideConnected: false,
            });

            // Verify filter is active (step with joint → no contacts)
            // But first, verify the joint exists
            expect(world.getConstraintManager().hasConstraint(jointId)).toBe(true);

            // Destroy the joint BEFORE any stepping
            // This removes the filter entry. On the first step, the broadphase
            // creates fresh proxies and the pair should be detected.
            world.destroyConstraint(jointId);

            // Now step — bodies are overlapping, no filter, contacts should form
            world.step(DT);
            expect(world.getContactManager().contactCount).toBeGreaterThan(0);
        });

        it('after destroyConstraint post-step, filter entry is cleared', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            const jointId = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 1,
                collideConnected: false,
            });

            // Step with joint: no contacts
            world.step(DT);
            expect(world.getContactManager().contactCount).toBe(0);

            // Destroy the joint
            world.destroyConstraint(jointId);

            // Create fresh overlapping bodies — they should collide freely
            // (proves the old joint's filter entry doesn't affect new body pairs)
            const bodyC = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 10, y: 0 },
            });
            world.createCircleShape(bodyC, { radius: 1 });
            const bodyD = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 10.5, y: 0 },
            });
            world.createCircleShape(bodyD, { radius: 1 });

            world.step(DT);
            // bodyC and bodyD should have contacts (unrelated pair, no filter)
            expect(world.getContactManager().contactCount).toBeGreaterThan(0);
        });
    });

    describe('Cleanup on body destroy', () => {
        it('destroying a body with a collideConnected joint does not leak index entries', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 1,
                collideConnected: false,
            });

            // Destroy body B (should also destroy its constraints)
            world.destroyBody(bodyB);

            // Create a new body C overlapping with A
            const bodyC = world.createBody({
                type: BodyType.Dynamic,
                position: { x: 0.5, y: 0 },
            });
            world.createCircleShape(bodyC, { radius: 1 });

            // bodyA and bodyC should collide (no stale suppression)
            world.step(DT);
            expect(world.getContactManager().contactCount).toBeGreaterThan(0);
        });
    });

    describe('Multi-joint edge case', () => {
        it('two joints between same bodies: if ANY has collideConnected=false, no contact', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            // Joint 1: collideConnected=true
            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 1,
                collideConnected: true,
            });

            // Joint 2: collideConnected=false → counter=1, suppressed
            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 1, y: 0 },
                localAnchorB: { x: 1, y: 0 },
                length: 2,
                collideConnected: false,
            });

            world.step(DT);
            // With counter-based approach: count=1 (one false), so suppressed
            expect(world.getContactManager().contactCount).toBe(0);
        });

        it('two joints both collideConnected=false: removing one still suppresses', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            const joint1 = world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 1,
                collideConnected: false,
            });

            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 1, y: 0 },
                localAnchorB: { x: 1, y: 0 },
                length: 2,
                collideConnected: false,
            });

            // counter=2
            world.step(DT);
            expect(world.getContactManager().contactCount).toBe(0);

            // Remove one: counter=1, still suppressed
            world.destroyConstraint(joint1);
            world.step(DT);
            expect(world.getContactManager().contactCount).toBe(0);
        });
    });

    describe('Joint types', () => {
        it('revolute joint with collideConnected=false suppresses contact', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createRevoluteConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                collideConnected: false,
            });

            world.step(DT);
            expect(world.getContactManager().contactCount).toBe(0);
        });

        it('weld joint with collideConnected=false suppresses contact', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createWeldConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                collideConnected: false,
            });

            world.step(DT);
            expect(world.getContactManager().contactCount).toBe(0);
        });
    });

    describe('Independence from other filters', () => {
        it('collideConnected=true does not suppress contacts (filter independent)', () => {
            const { bodyA, bodyB } = createOverlappingBodies(world);

            world.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 1,
                collideConnected: true,
            });

            world.step(DT);
            expect(world.getContactManager().contactCount).toBeGreaterThan(0);
        });
    });
});
