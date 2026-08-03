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

