import { describe, it, expect, beforeEach } from 'vitest';
import { ConstraintSolver2D } from '@axrone/physics-2d';
import { ConstraintManager2D } from '@axrone/physics-2d';
import { BodyManager2D } from '@axrone/physics-2d';
import { BodyType } from '@axrone/physics-core';
import type { BodyId, ConstraintId } from '@axrone/physics-2d';

/**
 * Behavioral tests for 2D joint constraint solvers.
 *
 * Each test verifies that the constraint produces REAL physical corrections
 * (velocity impulses + position corrections), not just that it can be created.
 *
 * Strategy: For each joint type, set up bodies with a KNOWN error state,
 * run the solver, and verify that the solver produced measurable corrections.
 *
 * @see JOINT_CAPABILITY_2D
 */

const DT = 1 / 60;
const VEL_ITERS = 8;
const POS_ITERS = 4;

function makeBodies(
    bodyManager: BodyManager2D,
    posA: { x: number; y: number },
    posB: { x: number; y: number }
): { a: BodyId; b: BodyId } {
    const a = bodyManager.createBody({ type: BodyType.Dynamic, position: posA, rotation: 0 });
    const b = bodyManager.createBody({ type: BodyType.Dynamic, position: posB, rotation: 0 });
    bodyManager.setMassData(a, 1, 0.5, { x: 0, y: 0 });
    bodyManager.setMassData(b, 1, 0.5, { x: 0, y: 0 });
    return { a, b };
}

function solveOneStep(solver: ConstraintSolver2D, constraints: ConstraintId[]): void {
    solver.prepareConstraints(constraints, DT);
    solver.solveVelocityConstraints(VEL_ITERS);
    solver.solvePositionConstraints(POS_ITERS);
}

/**
 * Capture solver body velocities BEFORE and AFTER solve to measure correction.
 */
function measureCorrection(
    solver: ConstraintSolver2D,
    bodyManager: BodyManager2D,
    constraints: ConstraintId[],
    bodyId: BodyId
): { beforeVx: number; beforeVy: number; beforeW: number; afterVx: number; afterVy: number; afterW: number; posBeforeX: number; posAfterX: number } {
    // Snapshot before
    const velBefore = bodyManager.getLinearVelocity(bodyId);
    const wBefore = bodyManager.getAngularVelocity(bodyId);
    const posBefore = bodyManager.getPosition(bodyId);

    solver.prepareConstraints(constraints, DT);
    solver.solveVelocityConstraints(VEL_ITERS);

    const sbAfter = solver.getSolverBody(bodyId)!;

    return {
        beforeVx: velBefore.x, beforeVy: velBefore.y, beforeW: wBefore,
        afterVx: sbAfter.linearVelocity.x, afterVy: sbAfter.linearVelocity.y, afterW: sbAfter.angularVelocity,
        posBeforeX: posBefore.x, posAfterX: sbAfter.position.x,
    };
}

describe('2D Joint Behavioral Tests', () => {
    let bodyManager: BodyManager2D;
    let constraintManager: ConstraintManager2D;
    let solver: ConstraintSolver2D;

    beforeEach(() => {
        bodyManager = new BodyManager2D(128);
        constraintManager = new ConstraintManager2D(128);
        solver = new ConstraintSolver2D(constraintManager, bodyManager);
    });

    // ─── DISTANCE ────────────────────────────────────────────────────
    describe('Distance constraint', () => {
        it('produces velocity corrections when distance error exists', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
            const c = constraintManager.createDistanceConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                length: 3,
            });

            const corr = measureCorrection(solver, bodyManager, [c], a);
            // Velocity was modified by solver
            expect(corr.afterVx).not.toBe(corr.beforeVx);
        });

        it('produces position corrections when distance error exists', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
            const c = constraintManager.createDistanceConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                length: 3,
            });

            solver.prepareConstraints([c], DT);
            solver.solvePositionConstraints(POS_ITERS);

            const sbA = solver.getSolverBody(a)!;
            const sbB = solver.getSolverBody(b)!;
            // Position corrections should have been applied
            // At least one body should have moved from its original position
            const moved = Math.abs(sbA.position.x) > 0.001 || Math.abs(sbB.position.x - 5) > 0.001;
            expect(moved).toBe(true);
        });

        it('negative control: no constraint → solver bodies not created', () => {
            const { a } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
            bodyManager.setLinearVelocity(a, { x: -5, y: 0 });

            solveOneStep(solver, []);

            // Without constraints, no solver bodies are created
            expect(solver.getSolverBody(a)).toBeUndefined();
            // Original body velocity is unchanged
            expect(bodyManager.getLinearVelocity(a).x).toBe(-5);
        });
    });

    // ─── REVOLUTE (Hinge) ────────────────────────────────────────────
    describe('Revolute constraint', () => {
        it('produces corrections to maintain anchor coincidence', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 4, y: 0 });
            const c = constraintManager.createRevoluteConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 2, y: 0 }, localAnchorB: { x: -2, y: 0 },
            });

            // Push B away — this creates anchor error on next step
            bodyManager.setLinearVelocity(b, { x: 10, y: 5 });
            bodyManager.setLinearVelocity(a, { x: -3, y: 2 });
            const corr = measureCorrection(solver, bodyManager, [c], b);
            // Both linear velocity components should be corrected
            const changed = corr.afterVx !== corr.beforeVx || corr.afterVy !== corr.beforeVy;
            expect(changed).toBe(true);
        });

        it('limit: produces angular correction when beyond limits', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 2, y: 0 });
            const c = constraintManager.createRevoluteConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 1, y: 0 }, localAnchorB: { x: -1, y: 0 },
                referenceAngle: 0,
                enableLimit: true,
                lowerAngle: -0.1,
                upperAngle: 0.1,
            });

            bodyManager.setRotation(b, 1.0); // beyond ±0.1 limit
            const corr = measureCorrection(solver, bodyManager, [c], b);
            // Angular velocity was modified by limit
            expect(corr.afterW).not.toBe(corr.beforeW);
        });

        it('motor: produces angular impulse', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 2, y: 0 });
            const c = constraintManager.createRevoluteConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 1, y: 0 }, localAnchorB: { x: -1, y: 0 },
                enableMotor: true,
                motorSpeed: 5.0,
                maxMotorTorque: 100,
            });

            const corr = measureCorrection(solver, bodyManager, [c], b);
            // Motor creates angular velocity from rest
            expect(Math.abs(corr.afterW)).toBeGreaterThan(0);
        });
    });

    // ─── PRISMATIC (Slider) ──────────────────────────────────────────
    describe('Prismatic constraint', () => {
        it('produces lateral correction (perpendicular to axis)', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 3, y: 0 });
            const c = constraintManager.createPrismaticConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                localAxisA: { x: 1, y: 0 },
                referenceAngle: 0,
            });

            bodyManager.setLinearVelocity(b, { x: 0, y: 10 });
            const corr = measureCorrection(solver, bodyManager, [c], b);
            // Lateral velocity was corrected
            expect(corr.afterVy).not.toBe(corr.beforeVy);
        });

        it('produces angular correction (locks rotation)', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 3, y: 0 });
            const c = constraintManager.createPrismaticConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                localAxisA: { x: 1, y: 0 },
                referenceAngle: 0,
            });

            // Set different rotations to create angle error
            bodyManager.setRotation(a, 0.2);
            bodyManager.setRotation(b, -0.3);
            bodyManager.setAngularVelocity(b, 5);
            const corr = measureCorrection(solver, bodyManager, [c], b);
            // Angular velocity was corrected due to angle error
            expect(corr.afterW).not.toBe(corr.beforeW);
        });

        it('limit: produces correction when beyond translation bounds', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
            const c = constraintManager.createPrismaticConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                localAxisA: { x: 1, y: 0 },
                referenceAngle: 0,
                enableLimit: true,
                lowerTranslation: -1,
                upperTranslation: 1,
            });

            const corr = measureCorrection(solver, bodyManager, [c], b);
            // Velocity was modified (limit jacobian was added)
            expect(corr.afterVx).not.toBe(corr.beforeVx);
        });
    });

    // ─── WELD (Fixed) ────────────────────────────────────────────────
    describe('Weld constraint', () => {
        it('produces corrections for both linear and angular', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 3, y: 0 });
            const c = constraintManager.createWeldConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 1.5, y: 0 }, localAnchorB: { x: -1.5, y: 0 },
                referenceAngle: 0,
            });

            bodyManager.setLinearVelocity(b, { x: 10, y: 5 });
            bodyManager.setAngularVelocity(b, 8);

            const corr = measureCorrection(solver, bodyManager, [c], b);
            expect(corr.afterVx).not.toBe(corr.beforeVx);
            expect(corr.afterW).not.toBe(corr.beforeW);
        });
    });

    // ─── WHEEL ───────────────────────────────────────────────────────
    describe('Wheel constraint', () => {
        it('produces lateral correction (perpendicular to suspension axis)', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 0, y: 2 });
            const c = constraintManager.createWheelConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                localAxisA: { x: 0, y: 1 },
                stiffness: 20, damping: 5,
            });

            bodyManager.setLinearVelocity(b, { x: 10, y: 0 });
            const corr = measureCorrection(solver, bodyManager, [c], b);
            expect(corr.afterVx).not.toBe(corr.beforeVx);
        });

        it('limit: produces correction along suspension axis', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 0, y: 5 });
            const c = constraintManager.createWheelConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                localAxisA: { x: 0, y: 1 },
                enableLimit: true,
                lowerTranslation: -1,
                upperTranslation: 1,
            });

            bodyManager.setLinearVelocity(b, { x: 0, y: 10 });
            const corr = measureCorrection(solver, bodyManager, [c], b);
            expect(corr.afterVy).not.toBe(corr.beforeVy);
        });

        it('motor: produces angular impulse', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 0, y: 2 });
            const c = constraintManager.createWheelConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                localAxisA: { x: 0, y: 1 },
                enableMotor: true,
                motorSpeed: 5,
                maxMotorTorque: 50,
            });

            const corr = measureCorrection(solver, bodyManager, [c], b);
            expect(Math.abs(corr.afterW)).toBeGreaterThan(0);
        });
    });

    // ─── MOTOR ───────────────────────────────────────────────────────
    describe('Motor constraint', () => {
        it('produces linear corrections', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 8, y: 0 });
            const c = constraintManager.createMotorConstraint({
                bodyIdA: a, bodyIdB: b,
                linearOffset: { x: 3, y: 0 },
                angularOffset: 0,
                maxForce: 100,
                maxTorque: 100,
            });

            bodyManager.setLinearVelocity(b, { x: 10, y: 5 });
            const corr = measureCorrection(solver, bodyManager, [c], b);
            expect(corr.afterVx).not.toBe(corr.beforeVx);
        });

        it('produces angular corrections', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 3, y: 0 });
            const c = constraintManager.createMotorConstraint({
                bodyIdA: a, bodyIdB: b,
                linearOffset: { x: 3, y: 0 },
                angularOffset: Math.PI / 4,
                maxForce: 100,
                maxTorque: 50,
            });

            bodyManager.setAngularVelocity(b, 10);
            const corr = measureCorrection(solver, bodyManager, [c], b);
            expect(corr.afterW).not.toBe(corr.beforeW);
        });
    });

    // ─── MOUSE ───────────────────────────────────────────────────────
    describe('Mouse constraint', () => {
        it('produces corrections toward target point', () => {
            const a = bodyManager.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 }, rotation: 0 });
            const b = bodyManager.createBody({ type: BodyType.Static, position: { x: 0, y: 0 }, rotation: 0 });
            bodyManager.setMassData(a, 1, 0.5, { x: 0, y: 0 });

            const c = constraintManager.createMouseConstraint({
                bodyIdA: a, bodyIdB: b,
                target: { x: 5, y: 5 },
                maxForce: 500,
                stiffness: 50,
                damping: 10,
            });

            const corr = measureCorrection(solver, bodyManager, [c], a);
            // Body A receives velocity correction (from zero toward target)
            expect(Math.abs(corr.afterVx) + Math.abs(corr.afterVy)).toBeGreaterThan(0);
        });

        it('negative control: body at target → minimal correction', () => {
            const a = bodyManager.createBody({ type: BodyType.Dynamic, position: { x: 5, y: 5 }, rotation: 0 });
            const b = bodyManager.createBody({ type: BodyType.Static, position: { x: 0, y: 0 }, rotation: 0 });
            bodyManager.setMassData(a, 1, 0.5, { x: 0, y: 0 });

            const c = constraintManager.createMouseConstraint({
                bodyIdA: a, bodyIdB: b,
                target: { x: 5, y: 5 },
                maxForce: 500,
                stiffness: 50,
                damping: 10,
            });

            const corr = measureCorrection(solver, bodyManager, [c], a);
            // At target → soft constraint produces minimal velocity
            expect(Math.abs(corr.afterVx)).toBeLessThan(1);
            expect(Math.abs(corr.afterVy)).toBeLessThan(1);
        });
    });

    // ─── GEAR ────────────────────────────────────────────────────────
    describe('Gear constraint', () => {
        it('produces angular corrections to enforce ratio', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
            const ratio = 2.0;
            const c = constraintManager.createGearConstraint({
                bodyIdA: a, bodyIdB: b,
                constraintIdA: 0 as ConstraintId,
                constraintIdB: 0 as ConstraintId,
                ratio,
            });

            bodyManager.setAngularVelocity(a, 5);
            const corrA = measureCorrection(solver, bodyManager, [c], a);
            const sbB = solver.getSolverBody(b)!;

            // Gear error should decrease: ω_B - ratio * ω_A should approach 0
            const initialError = 0 - ratio * 5;
            const currentError = sbB.angularVelocity - ratio * corrA.afterW;
            expect(Math.abs(currentError)).toBeLessThan(Math.abs(initialError));
        });
    });

    // ─── ROPE ────────────────────────────────────────────────────────
    describe('Rope constraint', () => {
        it('produces corrections when distance exceeds max length', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 10, y: 0 });
            const c = constraintManager.createRopeConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                maxLength: 3,
            });

            bodyManager.setLinearVelocity(a, { x: -5, y: 0 });
            bodyManager.setLinearVelocity(b, { x: 5, y: 0 });

            const corrA = measureCorrection(solver, bodyManager, [c], a);
            // Velocity was modified by rope constraint
            expect(corrA.afterVx).not.toBe(corrA.beforeVx);
        });

        it('does NOT produce corrections when within max length', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 2, y: 0 });
            const c = constraintManager.createRopeConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                maxLength: 5,
            });

            solver.prepareConstraints([c], DT);
            expect(solver.getLastPreparedConstraintCount()).toBe(0);
        });

        it('negative control: no constraint → no solver bodies created', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
            bodyManager.setLinearVelocity(a, { x: -10, y: 0 });
            bodyManager.setLinearVelocity(b, { x: 10, y: 0 });

            solveOneStep(solver, []);

            expect(solver.getSolverBody(a)).toBeUndefined();
            expect(solver.getSolverBody(b)).toBeUndefined();
        });
    });

    // ─── SPRING (soft Distance) ──────────────────────────────────────
    describe('Spring (soft Distance) constraint', () => {
        it('produces corrections with soft constraint formulation', () => {
            const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
            const c = constraintManager.createDistanceConstraint({
                bodyIdA: a, bodyIdB: b,
                localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                length: 3,
                stiffness: 20,
                damping: 5,
            });

            bodyManager.setLinearVelocity(b, { x: -5, y: 0 });
            const corr = measureCorrection(solver, bodyManager, [c], b);
            expect(corr.afterVx).not.toBe(corr.beforeVx);
        });
    });

    // ─── ALL 9 SOLVER TYPES PREPARE JACOBIANS ───────────────────────
    describe('All solver types prepare Jacobians', () => {
        it.each([
            ['Distance', () => {
                const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
                return constraintManager.createDistanceConstraint({
                    bodyIdA: a, bodyIdB: b,
                    localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 }, length: 2,
                });
            }],
            ['Revolute', () => {
                const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
                return constraintManager.createRevoluteConstraint({
                    bodyIdA: a, bodyIdB: b,
                    localAnchorA: { x: 1, y: 0 }, localAnchorB: { x: -1, y: 0 },
                });
            }],
            ['Prismatic', () => {
                const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
                return constraintManager.createPrismaticConstraint({
                    bodyIdA: a, bodyIdB: b,
                    localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                    localAxisA: { x: 1, y: 0 },
                });
            }],
            ['Weld', () => {
                const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
                return constraintManager.createWeldConstraint({
                    bodyIdA: a, bodyIdB: b,
                    localAnchorA: { x: 1, y: 0 }, localAnchorB: { x: -1, y: 0 },
                });
            }],
            ['Wheel', () => {
                const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 0, y: 3 });
                return constraintManager.createWheelConstraint({
                    bodyIdA: a, bodyIdB: b,
                    localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 },
                    localAxisA: { x: 0, y: 1 }, stiffness: 20, damping: 5,
                });
            }],
            ['Motor', () => {
                const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
                return constraintManager.createMotorConstraint({
                    bodyIdA: a, bodyIdB: b,
                    linearOffset: { x: 3, y: 0 }, angularOffset: 0, maxForce: 100, maxTorque: 100,
                });
            }],
            ['Mouse', () => {
                const a = bodyManager.createBody({ type: BodyType.Dynamic, position: { x: 0, y: 0 }, rotation: 0 });
                const b = bodyManager.createBody({ type: BodyType.Static, position: { x: 0, y: 0 }, rotation: 0 });
                bodyManager.setMassData(a, 1, 0.5, { x: 0, y: 0 });
                return constraintManager.createMouseConstraint({
                    bodyIdA: a, bodyIdB: b,
                    target: { x: 5, y: 5 }, maxForce: 500, stiffness: 50, damping: 10,
                });
            }],
            ['Gear', () => {
                const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 5, y: 0 });
                return constraintManager.createGearConstraint({
                    bodyIdA: a, bodyIdB: b,
                    constraintIdA: 0 as ConstraintId, constraintIdB: 0 as ConstraintId, ratio: 2,
                });
            }],
            ['Rope', () => {
                const { a, b } = makeBodies(bodyManager, { x: 0, y: 0 }, { x: 10, y: 0 });
                return constraintManager.createRopeConstraint({
                    bodyIdA: a, bodyIdB: b,
                    localAnchorA: { x: 0, y: 0 }, localAnchorB: { x: 0, y: 0 }, maxLength: 3,
                });
            }],
        ])('%s constraint prepares Jacobians (has real solver)', (_name, createFn) => {
            const c = createFn();
            solver.prepareConstraints([c], DT);
            expect(solver.getLastPreparedConstraintCount()).toBeGreaterThan(0);
        });
    });
});
