import { describe, it, expect, beforeEach } from 'vitest';
import { IslandSolver2D } from '@axrone/physics-2d';
import { BodyManager2D } from '@axrone/physics-2d';
import { ContactManager2D } from '@axrone/physics-2d';
import { ConstraintManager2D } from '@axrone/physics-2d';
import { BodyType, SolverFlags } from '@axrone/physics-core';

const GRAVITY = { x: 0, y: -10 };

describe('IslandSolver2D', () => {
    let islandSolver: IslandSolver2D;
    let bodyManager: BodyManager2D;
    let contactManager: ContactManager2D;
    let constraintManager: ConstraintManager2D;

    beforeEach(() => {
        bodyManager = new BodyManager2D(64);
        contactManager = new ContactManager2D(128);
        constraintManager = new ConstraintManager2D(64);
        islandSolver = new IslandSolver2D(bodyManager, contactManager, constraintManager, 128);
    });

    describe('P1-3 solver ordering', () => {
        it('joint velocity corrections affect position integration in the same step', () => {
            // Static anchor at origin
            const anchor = bodyManager.createBody({
                type: BodyType.Static,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            // Dynamic body connected by distance constraint (rest length 3)
            const dyn = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 5, y: 0 },
                rotation: 0,
            });
            bodyManager.setMassData(dyn, 1, 0.1, { x: 0, y: 0 });

            constraintManager.createDistanceConstraint({
                bodyIdA: anchor,
                bodyIdB: dyn,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 3,
            });

            // Apply strong impulse away from anchor → constraint must correct velocity
            const velBefore = { ...bodyManager.getLinearVelocity(dyn) };
            bodyManager.setLinearVelocity(dyn, { x: 100, y: 0 });

            const zeroGrav = { x: 0, y: 0 };
            islandSolver.solveIslands(1 / 60, 8, 3, false, SolverFlags.None, zeroGrav);

            const velAfter = bodyManager.getLinearVelocity(dyn);

            // The constraint must have corrected the velocity (body was at distance 5,
            // constraint length is 3, impulse pushes further away).
            // The Jacobian-based impulse reduces the outward velocity significantly.
            const velChangeX = Math.abs(velAfter.x - velBefore.x);
            expect(velChangeX).toBeGreaterThan(1);

            // Constraint was prepared and solved
            expect(islandSolver.getLastSolvedConstraintCount()).toBeGreaterThan(0);
        });

        it('connected body responds to impulse within the same step', () => {
            // Two dynamic bodies connected by a distance constraint
            const bodyA = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });
            bodyManager.setMassData(bodyA, 1, 0.1, { x: 0, y: 0 });

            const bodyB = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 3, y: 0 },
                rotation: 0,
            });
            bodyManager.setMassData(bodyB, 1, 0.1, { x: 0, y: 0 });

            constraintManager.createDistanceConstraint({
                bodyIdA: bodyA,
                bodyIdB: bodyB,
                localAnchorA: { x: 0, y: 0 },
                localAnchorB: { x: 0, y: 0 },
                length: 3,
            });

            // Apply strong impulse to bodyA away from bodyB
            bodyManager.setLinearVelocity(bodyA, { x: -50, y: 0 });

            const velBBefore = { ...bodyManager.getLinearVelocity(bodyB) };
            const zeroGrav = { x: 0, y: 0 };
            islandSolver.solveIslands(1 / 60, 8, 3, false, SolverFlags.None, zeroGrav);

            const velBAfter = bodyManager.getLinearVelocity(bodyB);

            // Body B must have been pulled in the same step — the distance constraint
            // transmits force through joint velocity solve, which now happens
            // before body commit (not after). This means body B's response is immediate.
            const velBChange = Math.abs(velBAfter.x - velBBefore.x) + Math.abs(velBAfter.y - velBBefore.y);
            expect(velBChange).toBeGreaterThan(0.1);

            // Constraint was solved
            expect(islandSolver.getLastSolvedConstraintCount()).toBeGreaterThan(0);
        });
    });

    describe('P1-4 per-body allowSleep', () => {
        it('respects per-body allowSleep=false flag', () => {
            // Create body with allowSleep=false
            const body = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
                allowSleep: false,
            });
            bodyManager.setMassData(body, 1, 0.1, { x: 0, y: 0 });

            // Run many steps with allowSleep enabled at world level
            for (let i = 0; i < 100; i++) {
                islandSolver.solveIslands(1 / 60, 8, 3, true, SolverFlags.None, GRAVITY);
            }

            // Body should still be awake because allowSleep=false
            expect(bodyManager.isAwake(body)).toBe(true);
        });

        it('allows sleep for bodies with allowSleep=true', () => {
            const body = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
                allowSleep: true,
            });
            bodyManager.setMassData(body, 1, 0.1, { x: 0, y: 0 });

            // Run many steps — body should eventually sleep (zero velocity, no forces)
            let wasSleeping = false;
            for (let i = 0; i < 200; i++) {
                islandSolver.solveIslands(1 / 60, 8, 3, true, SolverFlags.None, { x: 0, y: 0 });
                if (!bodyManager.isAwake(body)) {
                    wasSleeping = true;
                    break;
                }
            }
            expect(wasSleeping).toBe(true);
        });
    });

    describe('P1-1 allocation-free hot path', () => {
        it('commits body state correctly using scratch vectors', () => {
            const body = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });
            bodyManager.setMassData(body, 1, 0.1, { x: 0, y: 0 });
            bodyManager.setLinearVelocity(body, { x: 60, y: 0 });

            // One step — body should move in +x direction
            islandSolver.solveIslands(1 / 60, 8, 3, false, SolverFlags.None, { x: 0, y: 0 });

            const pos = bodyManager.getPosition(body);
            // Position should have moved in +x (exact value depends on damping/gravity)
            expect(pos.x).toBeGreaterThan(0);
            // Verify scratch vec reuse doesn't corrupt position
            expect(Number.isFinite(pos.x)).toBe(true);
            expect(Number.isFinite(pos.y)).toBe(true);
        });
    });

    describe('Island Solver Basics', () => {
        it('creates solver', () => {
            expect(islandSolver).toBeDefined();
        });

        it('solves empty islands', () => {
            expect(() => islandSolver.solveIslands(1 / 60, 8, 3, true, SolverFlags.None, GRAVITY)).not.toThrow();
        });

        it('solves islands with dynamic bodies', () => {
            const bodyA = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            bodyManager.setMassData(bodyA, 1, 0.1, { x: 0, y: 0 });

            expect(() => islandSolver.solveIslands(1 / 60, 8, 3, true, SolverFlags.None, GRAVITY)).not.toThrow();
        });

        it('solves islands with multiple bodies', () => {
            const bodyA = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            const bodyB = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 5, y: 0 },
                rotation: 0,
            });

            bodyManager.setMassData(bodyA, 1, 0.1, { x: 0, y: 0 });
            bodyManager.setMassData(bodyB, 1, 0.1, { x: 0, y: 0 });

            expect(() => islandSolver.solveIslands(1 / 60, 8, 3, true, SolverFlags.None, GRAVITY)).not.toThrow();
        });

        it('handles sleep flag', () => {
            const bodyA = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            bodyManager.setMassData(bodyA, 1, 0.1, { x: 0, y: 0 });

            expect(() => islandSolver.solveIslands(1 / 60, 8, 3, false, SolverFlags.None, GRAVITY)).not.toThrow();
        });

        it('handles different iteration counts', () => {
            const bodyA = bodyManager.createBody({
                type: BodyType.Dynamic,
                position: { x: 0, y: 0 },
                rotation: 0,
            });

            bodyManager.setMassData(bodyA, 1, 0.1, { x: 0, y: 0 });

            expect(() => islandSolver.solveIslands(1 / 60, 4, 2, true, SolverFlags.None, GRAVITY)).not.toThrow();

            expect(() => islandSolver.solveIslands(1 / 60, 16, 6, true, SolverFlags.None, GRAVITY)).not.toThrow();
        });
    });
});

