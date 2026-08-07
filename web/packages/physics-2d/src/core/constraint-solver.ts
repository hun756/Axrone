import { Vec2, clamp, SOLVER_EPSILON as NUMERIC_SOLVER_EPSILON, type IVec2Like } from '@axrone/numeric';
import type { BodyId, ConstraintId } from '../types';
import { ConstraintType } from '../types';
import type { ConstraintManager2D } from './constraint-manager';
import type { BodyManager2D } from './body-manager';

interface SolverBody {
    bodyId: BodyId;
    invMass: number;
    invI: number;
    linearVelocity: { x: number; y: number };
    angularVelocity: number;
    position: { x: number; y: number };
    rotation: number;
    localCenter: { x: number; y: number };
}

interface JacobianRow {
    bodyIdA: BodyId;
    bodyIdB: BodyId;
    j1: { linear: IVec2Like; angular: number };
    j2: { linear: IVec2Like; angular: number };
    bias: number;
    impulse: number;
    lowerLimit: number;
    upperLimit: number;
    softness?: number;
}

const SOLVER_EPSILON = NUMERIC_SOLVER_EPSILON;
const BAUMGARTE = 0.2;
const POSITION_SLOP = 0.005;

export class ConstraintSolver2D {
    private readonly _constraintManager: ConstraintManager2D;
    private readonly _bodyManager: BodyManager2D;
    private readonly _bodyMap: Map<BodyId, SolverBody> = new Map();
    private readonly _jacobianCache: Map<ConstraintId, JacobianRow[]> = new Map();
    private _lastPreparedConstraintCount = 0;
    private _lastSolvedConstraintCount = 0;

    constructor(constraintManager: ConstraintManager2D, bodyManager: BodyManager2D) {
        this._constraintManager = constraintManager;
        this._bodyManager = bodyManager;
    }

    solveConstraints(
        constraints: readonly ConstraintId[],
        deltaTime: number,
        velocityIterations: number,
        positionIterations: number
    ): void {
        this.prepareConstraints(constraints, deltaTime);

        if (this._jacobianCache.size === 0) {
            this._lastSolvedConstraintCount = 0;
            return;
        }

        this.solveVelocityConstraints(velocityIterations);
        this.solvePositionConstraints(positionIterations);
        this.commitBodies();
        this._lastSolvedConstraintCount = this._jacobianCache.size;
    }

    prepareConstraints(constraints: readonly ConstraintId[], deltaTime: number): void {
        this._bodyMap.clear();
        this._jacobianCache.clear();
        this._lastPreparedConstraintCount = 0;

        for (const constraintId of constraints) {
            if (!this._constraintManager.isEnabled(constraintId)) {
                continue;
            }

            const type = this._constraintManager.getConstraintType(constraintId);

            switch (type) {
                case ConstraintType.Distance:
                    this._prepareDistanceConstraint(constraintId, deltaTime);
                    break;
                case ConstraintType.Revolute:
                    this._prepareRevoluteConstraint(constraintId, deltaTime);
                    break;
                case ConstraintType.Prismatic:
                    this._preparePrismaticConstraint(constraintId, deltaTime);
                    break;
                case ConstraintType.Weld:
                    this._prepareWeldConstraint(constraintId, deltaTime);
                    break;
                case ConstraintType.Wheel:
                    this._prepareWheelConstraint(constraintId, deltaTime);
                    break;
                case ConstraintType.Motor:
                    this._prepareMotorConstraint(constraintId, deltaTime);
                    break;
                case ConstraintType.Mouse:
                    this._prepareMouseConstraint(constraintId, deltaTime);
                    break;
                case ConstraintType.Gear:
                    this._prepareGearConstraint(constraintId, deltaTime);
                    break;
                case ConstraintType.Rope:
                    this._prepareRopeConstraint(constraintId, deltaTime);
                    break;
            }
        }
    }

    solveVelocityConstraints(iterations: number): void {
        for (let iteration = 0; iteration < iterations; iteration++) {
            for (const jacobians of this._jacobianCache.values()) {
                for (const jacobian of jacobians) {
                    this._solveSingleJacobian(jacobian);
                }
            }
        }
    }

    solvePositionConstraints(iterations: number): boolean {
        let minError = Infinity;

        for (let iteration = 0; iteration < iterations; iteration++) {
            minError = Infinity;
            for (const jacobians of this._jacobianCache.values()) {
                for (const jacobian of jacobians) {
                    const bodyA = this._bodyMap.get(jacobian.bodyIdA);
                    const bodyB = this._bodyMap.get(jacobian.bodyIdB);
                    if (!bodyA || !bodyB) continue;

                    const error = jacobian.bias;
                    minError = Math.min(minError, Math.abs(error));

                    if (Math.abs(error) <= POSITION_SLOP) continue;

                    const sumJInvJ =
                        bodyA.invMass * Vec2.lengthSquared(jacobian.j1.linear) +
                        bodyA.invI * jacobian.j1.angular * jacobian.j1.angular +
                        bodyB.invMass * Vec2.lengthSquared(jacobian.j2.linear) +
                        bodyB.invI * jacobian.j2.angular * jacobian.j2.angular;

                    if (sumJInvJ <= SOLVER_EPSILON) continue;

                    const totalInvMass = sumJInvJ + (jacobian.softness ?? 0);
                    const effectiveMass = totalInvMass > SOLVER_EPSILON ? 1 / totalInvMass : 0;

                    let impulse = -effectiveMass * error;
                    // Unilateral constraint guard (e.g. Rope lowerLimit: 0)
                    if (!isFinite(jacobian.lowerLimit) || impulse >= jacobian.lowerLimit) {
                        if (isFinite(jacobian.upperLimit)) {
                            impulse = Math.min(impulse, jacobian.upperLimit);
                        }
                    } else {
                        continue;
                    }

                    if (bodyA.invMass > 0) {
                        bodyA.position.x += bodyA.invMass * impulse * jacobian.j1.linear.x;
                        bodyA.position.y += bodyA.invMass * impulse * jacobian.j1.linear.y;
                        bodyA.rotation += bodyA.invI * impulse * jacobian.j1.angular;
                    }
                    if (bodyB.invMass > 0) {
                        bodyB.position.x += bodyB.invMass * impulse * jacobian.j2.linear.x;
                        bodyB.position.y += bodyB.invMass * impulse * jacobian.j2.linear.y;
                        bodyB.rotation += bodyB.invI * impulse * jacobian.j2.angular;
                    }
                }
            }

            if (minError <= 0.001) {
                return true;
            }
        }

        return minError <= 0.005;
    }

    commitBodies(): void {
        for (const solverBody of this._bodyMap.values()) {
            this._bodyManager.setLinearVelocity(solverBody.bodyId, solverBody.linearVelocity);
            this._bodyManager.setAngularVelocity(solverBody.bodyId, solverBody.angularVelocity);
            // Write back position corrections from solvePositionConstraints
            this._bodyManager.setPosition(solverBody.bodyId, solverBody.position);
            this._bodyManager.setRotation(solverBody.bodyId, solverBody.rotation);
        }
    }

    getLastPreparedConstraintCount(): number {
        return this._lastPreparedConstraintCount;
    }

    getLastSolvedConstraintCount(): number {
        return this._lastSolvedConstraintCount;
    }

    getSolverBody(bodyId: BodyId): SolverBody | undefined {
        return this._bodyMap.get(bodyId);
    }

    clearCache(): void {
        this._bodyMap.clear();
        this._jacobianCache.clear();
    }

    private _solveSingleJacobian(jacobian: JacobianRow): number {
        const bodyA = this._ensureSolverBody(jacobian.bodyIdA);
        const bodyB = this._ensureSolverBody(jacobian.bodyIdB);

        const velocityAlongJacobian =
            Vec2.dot(jacobian.j1.linear, bodyA.linearVelocity) +
            jacobian.j1.angular * bodyA.angularVelocity +
            Vec2.dot(jacobian.j2.linear, bodyB.linearVelocity) +
            jacobian.j2.angular * bodyB.angularVelocity;

        const sumJInvJ =
            bodyA.invMass * Vec2.lengthSquared(jacobian.j1.linear) +
            bodyA.invI * jacobian.j1.angular * jacobian.j1.angular +
            bodyB.invMass * Vec2.lengthSquared(jacobian.j2.linear) +
            bodyB.invI * jacobian.j2.angular * jacobian.j2.angular;

        const softness = jacobian.softness ?? 0;
        const totalInvMass = sumJInvJ + softness;
        if (totalInvMass <= SOLVER_EPSILON) {
            return 0;
        }

        const effectiveMass = 1 / totalInvMass;
        const previousImpulse = jacobian.impulse;
        const uncappedImpulse = previousImpulse - effectiveMass * (velocityAlongJacobian + jacobian.bias + softness * previousImpulse);
        let clampedImpulse = uncappedImpulse;
        if (isFinite(jacobian.lowerLimit) && isFinite(jacobian.upperLimit)) {
            clampedImpulse = clamp(uncappedImpulse, jacobian.lowerLimit, jacobian.upperLimit);
        } else if (isFinite(jacobian.lowerLimit)) {
            clampedImpulse = Math.max(uncappedImpulse, jacobian.lowerLimit);
        } else if (isFinite(jacobian.upperLimit)) {
            clampedImpulse = Math.min(uncappedImpulse, jacobian.upperLimit);
        }
        jacobian.impulse = clampedImpulse;
        const deltaImpulse = jacobian.impulse - previousImpulse;

        if (Math.abs(deltaImpulse) <= SOLVER_EPSILON) {
            return 0;
        }

        bodyA.linearVelocity.x += bodyA.invMass * deltaImpulse * jacobian.j1.linear.x;
        bodyA.linearVelocity.y += bodyA.invMass * deltaImpulse * jacobian.j1.linear.y;
        bodyA.angularVelocity += bodyA.invI * deltaImpulse * jacobian.j1.angular;

        bodyB.linearVelocity.x += bodyB.invMass * deltaImpulse * jacobian.j2.linear.x;
        bodyB.linearVelocity.y += bodyB.invMass * deltaImpulse * jacobian.j2.linear.y;
        bodyB.angularVelocity += bodyB.invI * deltaImpulse * jacobian.j2.angular;

        return deltaImpulse;
    }

    private _prepareDistanceConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getDistanceConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        const bodyB = this._ensureSolverBody(bodyIdB);

        const worldAnchorA = Vec2.add(bodyA.position, Vec2.rotate(data.localAnchorA, bodyA.rotation));
        const worldAnchorB = Vec2.add(bodyB.position, Vec2.rotate(data.localAnchorB, bodyB.rotation));
        const delta = Vec2.subtract(worldAnchorB, worldAnchorA);
        const distance = Math.sqrt(Vec2.lengthSquared(delta));
        if (distance <= SOLVER_EPSILON) {
            return;
        }

        const ndir = { x: delta.x / distance, y: delta.y / distance };
        const rA = Vec2.rotate(Vec2.subtract(data.localAnchorA, bodyA.localCenter), bodyA.rotation);
        const rB = Vec2.rotate(Vec2.subtract(data.localAnchorB, bodyB.localCenter), bodyB.rotation);
        const h = Math.max(dt, SOLVER_EPSILON);

        const error = distance - data.length;

        let bias: number;
        let softness: number | undefined;
        if (data.stiffness > 0) {
            softness = 1 / (data.damping + h * data.stiffness);
            bias = error * h * data.stiffness * softness;
        } else {
            bias = BAUMGARTE * error / h;
        }

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: -ndir.x, y: -ndir.y }, angular: -Vec2.cross(rA, ndir) },
                j2: { linear: { x: ndir.x, y: ndir.y }, angular: Vec2.cross(rB, ndir) },
                bias: -bias,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
                softness,
            },
        ]);
    }

    private _prepareRevoluteConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getRevoluteConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        const bodyB = this._ensureSolverBody(bodyIdB);

        const rA = Vec2.rotate(Vec2.subtract(data.localAnchorA, bodyA.localCenter), bodyA.rotation);
        const rB = Vec2.rotate(Vec2.subtract(data.localAnchorB, bodyB.localCenter), bodyB.rotation);
        const worldAnchorA = Vec2.add(bodyA.position, Vec2.rotate(data.localAnchorA, bodyA.rotation));
        const worldAnchorB = Vec2.add(bodyB.position, Vec2.rotate(data.localAnchorB, bodyB.rotation));
        const delta = Vec2.subtract(worldAnchorB, worldAnchorA);
        const h = Math.max(dt, SOLVER_EPSILON);

        const jacobians: JacobianRow[] = [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: -1, y: 0 }, angular: -rA.y },
                j2: { linear: { x: 1, y: 0 }, angular: rB.y },
                bias: -BAUMGARTE * delta.x / h,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: -1 }, angular: rA.x },
                j2: { linear: { x: 0, y: 1 }, angular: -rB.x },
                bias: -BAUMGARTE * delta.y / h,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
        ];

        const angleError = bodyB.rotation - bodyA.rotation - data.referenceAngle;

        if (data.enableLimit) {
            let limitError = 0;
            if (angleError < data.lowerAngle) {
                limitError = angleError - data.lowerAngle;
            } else if (angleError > data.upperAngle) {
                limitError = angleError - data.upperAngle;
            }
            if (Math.abs(limitError) > SOLVER_EPSILON) {
                jacobians.push({
                    bodyIdA,
                    bodyIdB,
                    j1: { linear: { x: 0, y: 0 }, angular: -1 },
                    j2: { linear: { x: 0, y: 0 }, angular: 1 },
                    bias: -BAUMGARTE * limitError / h,
                    impulse: 0,
                    lowerLimit: -Infinity,
                    upperLimit: Infinity,
                });
            }
        }

        if (data.enableMotor && Math.abs(data.maxMotorTorque) > SOLVER_EPSILON) {
            jacobians.push({
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 0 }, angular: -1 },
                j2: { linear: { x: 0, y: 0 }, angular: 1 },
                bias: -data.motorSpeed,
                impulse: 0,
                lowerLimit: -data.maxMotorTorque * h,
                upperLimit: data.maxMotorTorque * h,
            });
        }

        this._cacheJacobians(constraintId, jacobians);
    }

    private _preparePrismaticConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getPrismaticConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        const bodyB = this._ensureSolverBody(bodyIdB);

        const rA = Vec2.rotate(Vec2.subtract(data.localAnchorA, bodyA.localCenter), bodyA.rotation);
        const rB = Vec2.rotate(Vec2.subtract(data.localAnchorB, bodyB.localCenter), bodyB.rotation);
        const worldAnchorA = Vec2.add(bodyA.position, Vec2.rotate(data.localAnchorA, bodyA.rotation));
        const worldAnchorB = Vec2.add(bodyB.position, Vec2.rotate(data.localAnchorB, bodyB.rotation));
        const worldAxisA = Vec2.normalize(Vec2.rotate(data.localAxisA, bodyA.rotation), { x: 1, y: 0 });
        const perp = { x: -worldAxisA.y, y: worldAxisA.x };
        const delta = Vec2.subtract(worldAnchorB, worldAnchorA);
        const lateralError = Vec2.dot(perp, delta);
        const h = Math.max(dt, SOLVER_EPSILON);

        const jacobians: JacobianRow[] = [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: -perp.x, y: -perp.y }, angular: -Vec2.cross(rA, perp) },
                j2: { linear: { x: perp.x, y: perp.y }, angular: Vec2.cross(rB, perp) },
                bias: -BAUMGARTE * lateralError / h,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
        ];

        const angleError = bodyB.rotation - bodyA.rotation - data.referenceAngle;
        if (Math.abs(angleError) > SOLVER_EPSILON) {
            jacobians.push({
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 0 }, angular: -1 },
                j2: { linear: { x: 0, y: 0 }, angular: 1 },
                bias: -BAUMGARTE * angleError / h,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            });
        }

        const translation = Vec2.dot(delta, worldAxisA);
        if (data.enableLimit) {
            let limitError = 0;
            if (translation < data.lowerTranslation) {
                limitError = translation - data.lowerTranslation;
            } else if (translation > data.upperTranslation) {
                limitError = translation - data.upperTranslation;
            }
            if (Math.abs(limitError) > SOLVER_EPSILON) {
                jacobians.push({
                    bodyIdA,
                    bodyIdB,
                    j1: { linear: { x: -worldAxisA.x, y: -worldAxisA.y }, angular: -Vec2.cross(rA, worldAxisA) },
                    j2: { linear: { x: worldAxisA.x, y: worldAxisA.y }, angular: Vec2.cross(rB, worldAxisA) },
                    bias: -BAUMGARTE * limitError / h,
                    impulse: 0,
                    lowerLimit: -Infinity,
                    upperLimit: Infinity,
                });
            }
        }

        if (data.enableMotor && Math.abs(data.maxMotorForce) > SOLVER_EPSILON) {
            jacobians.push({
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: -worldAxisA.x, y: -worldAxisA.y }, angular: -Vec2.cross(rA, worldAxisA) },
                j2: { linear: { x: worldAxisA.x, y: worldAxisA.y }, angular: Vec2.cross(rB, worldAxisA) },
                bias: -data.motorSpeed,
                impulse: 0,
                lowerLimit: -data.maxMotorForce * h,
                upperLimit: data.maxMotorForce * h,
            });
        }

        this._cacheJacobians(constraintId, jacobians);
    }

    private _prepareWeldConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getWeldConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        const bodyB = this._ensureSolverBody(bodyIdB);

        const rA = Vec2.rotate(Vec2.subtract(data.localAnchorA, bodyA.localCenter), bodyA.rotation);
        const rB = Vec2.rotate(Vec2.subtract(data.localAnchorB, bodyB.localCenter), bodyB.rotation);
        const worldAnchorA = Vec2.add(bodyA.position, Vec2.rotate(data.localAnchorA, bodyA.rotation));
        const worldAnchorB = Vec2.add(bodyB.position, Vec2.rotate(data.localAnchorB, bodyB.rotation));
        const delta = Vec2.subtract(worldAnchorB, worldAnchorA);
        const angleError = bodyB.rotation - bodyA.rotation - data.referenceAngle;
        const h = Math.max(dt, SOLVER_EPSILON);

        let softness: number | undefined;
        if (data.stiffness > 0) {
            softness = 1 / (data.damping + h * data.stiffness);
        }

        const computeBias = (error: number): number => {
            if (softness !== undefined) {
                return error * h * data.stiffness * softness;
            }
            return BAUMGARTE * error / h;
        };

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: -1, y: 0 }, angular: -rA.y },
                j2: { linear: { x: 1, y: 0 }, angular: rB.y },
                bias: -computeBias(delta.x),
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
                softness,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: -1 }, angular: rA.x },
                j2: { linear: { x: 0, y: 1 }, angular: -rB.x },
                bias: -computeBias(delta.y),
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
                softness,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 0 }, angular: -1 },
                j2: { linear: { x: 0, y: 0 }, angular: 1 },
                bias: -computeBias(angleError),
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
                softness,
            },
        ]);
    }

    private _prepareWheelConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getWheelConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        const bodyB = this._ensureSolverBody(bodyIdB);

        const rA = Vec2.rotate(Vec2.subtract(data.localAnchorA, bodyA.localCenter), bodyA.rotation);
        const rB = Vec2.rotate(Vec2.subtract(data.localAnchorB, bodyB.localCenter), bodyB.rotation);
        const worldAnchorA = Vec2.add(bodyA.position, Vec2.rotate(data.localAnchorA, bodyA.rotation));
        const worldAnchorB = Vec2.add(bodyB.position, Vec2.rotate(data.localAnchorB, bodyB.rotation));
        const worldAxisA = Vec2.normalize(Vec2.rotate(data.localAxisA, bodyA.rotation), { x: 1, y: 0 });
        const perp = { x: -worldAxisA.y, y: worldAxisA.x };
        const delta = Vec2.subtract(worldAnchorB, worldAnchorA);
        const lateralError = Vec2.dot(perp, delta);
        const h = Math.max(dt, SOLVER_EPSILON);

        const jacobians: JacobianRow[] = [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: -perp.x, y: -perp.y }, angular: -Vec2.cross(rA, perp) },
                j2: { linear: { x: perp.x, y: perp.y }, angular: Vec2.cross(rB, perp) },
                bias: -BAUMGARTE * lateralError / h,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
        ];

        const translation = Vec2.dot(delta, worldAxisA);
        let axisError = 0;
        if (data.enableLimit) {
            if (translation < data.lowerTranslation) {
                axisError = translation - data.lowerTranslation;
            } else if (translation > data.upperTranslation) {
                axisError = translation - data.upperTranslation;
            }
        } else if (Math.abs(translation) > SOLVER_EPSILON && (data.stiffness > 0 || data.damping > 0)) {
            axisError = translation;
        }

        if (Math.abs(axisError) > SOLVER_EPSILON || data.stiffness > 0 || data.damping > 0) {
            let bias: number;
            let softness: number | undefined;
            if (data.stiffness > 0) {
                softness = 1 / (data.damping + h * data.stiffness);
                bias = axisError * h * data.stiffness * softness;
            } else {
                bias = BAUMGARTE * axisError / h;
            }
            jacobians.push({
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: -worldAxisA.x, y: -worldAxisA.y }, angular: -Vec2.cross(rA, worldAxisA) },
                j2: { linear: { x: worldAxisA.x, y: worldAxisA.y }, angular: Vec2.cross(rB, worldAxisA) },
                bias: -bias,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
                softness,
            });
        }

        if (data.enableMotor && Math.abs(data.maxMotorTorque) > SOLVER_EPSILON) {
            jacobians.push({
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 0 }, angular: -1 },
                j2: { linear: { x: 0, y: 0 }, angular: 1 },
                bias: -data.motorSpeed,
                impulse: 0,
                lowerLimit: -data.maxMotorTorque * h,
                upperLimit: data.maxMotorTorque * h,
            });
        }

        this._cacheJacobians(constraintId, jacobians);
    }

    private _prepareMotorConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getMotorConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        const bodyB = this._ensureSolverBody(bodyIdB);

        const rA = Vec2.rotate(Vec2.subtract({ x: 0, y: 0 }, bodyA.localCenter), bodyA.rotation);
        const rB = Vec2.rotate(Vec2.subtract({ x: 0, y: 0 }, bodyB.localCenter), bodyB.rotation);
        const h = Math.max(dt, SOLVER_EPSILON);

        const worldTarget = Vec2.add(bodyA.position, Vec2.rotate(data.linearOffset, bodyA.rotation));
        const worldAnchorB = { x: bodyB.position.x, y: bodyB.position.y };
        const delta = Vec2.subtract(worldAnchorB, worldTarget);

        const jacobians: JacobianRow[] = [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: -1, y: 0 }, angular: -rA.y },
                j2: { linear: { x: 1, y: 0 }, angular: rB.y },
                bias: -BAUMGARTE * delta.x / h,
                impulse: 0,
                lowerLimit: -data.maxForce * h,
                upperLimit: data.maxForce * h,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: -1 }, angular: rA.x },
                j2: { linear: { x: 0, y: 1 }, angular: -rB.x },
                bias: -BAUMGARTE * delta.y / h,
                impulse: 0,
                lowerLimit: -data.maxForce * h,
                upperLimit: data.maxForce * h,
            },
        ];

        const angleError = bodyB.rotation - bodyA.rotation - data.angularOffset;
        if (Math.abs(data.maxTorque) > SOLVER_EPSILON) {
            jacobians.push({
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 0 }, angular: -1 },
                j2: { linear: { x: 0, y: 0 }, angular: 1 },
                bias: -BAUMGARTE * angleError / h,
                impulse: 0,
                lowerLimit: -data.maxTorque * h,
                upperLimit: data.maxTorque * h,
            });
        }

        this._cacheJacobians(constraintId, jacobians);
    }

    private _prepareMouseConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getMouseConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        this._ensureSolverBody(bodyIdB);

        const anchorA = Vec2.add(bodyA.position, Vec2.rotate({ x: 0, y: 0 }, bodyA.rotation));
        const rA = Vec2.rotate(Vec2.subtract({ x: 0, y: 0 }, bodyA.localCenter), bodyA.rotation);
        const delta = Vec2.subtract(data.target, anchorA);
        const h = Math.max(dt, SOLVER_EPSILON);

        let softness: number | undefined;
        if (data.stiffness > 0) {
            softness = 1 / (data.damping + h * data.stiffness);
        }

        const computeBias = (error: number): number => {
            if (softness !== undefined) {
                return error * h * data.stiffness * softness;
            }
            return BAUMGARTE * error / h;
        };

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: -1, y: 0 }, angular: -rA.y },
                j2: { linear: { x: 1, y: 0 }, angular: 0 },
                bias: -computeBias(delta.x),
                impulse: 0,
                lowerLimit: -data.maxForce * h,
                upperLimit: data.maxForce * h,
                softness,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: -1 }, angular: rA.x },
                j2: { linear: { x: 0, y: 1 }, angular: 0 },
                bias: -computeBias(delta.y),
                impulse: 0,
                lowerLimit: -data.maxForce * h,
                upperLimit: data.maxForce * h,
                softness,
            },
        ]);
    }

    private _prepareGearConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getGearConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        const bodyB = this._ensureSolverBody(bodyIdB);
        const relativeRotationError = bodyB.rotation - data.ratio * bodyA.rotation;
        const h = Math.max(dt, SOLVER_EPSILON);

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 0 }, angular: -data.ratio },
                j2: { linear: { x: 0, y: 0 }, angular: 1 },
                bias: -BAUMGARTE * relativeRotationError / h,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
        ]);
    }

    private _prepareRopeConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getRopeConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        const bodyB = this._ensureSolverBody(bodyIdB);

        const worldAnchorA = Vec2.add(bodyA.position, Vec2.rotate(data.localAnchorA, bodyA.rotation));
        const worldAnchorB = Vec2.add(bodyB.position, Vec2.rotate(data.localAnchorB, bodyB.rotation));
        const delta = Vec2.subtract(worldAnchorB, worldAnchorA);
        const distance = Math.sqrt(Vec2.lengthSquared(delta));

        if (distance <= data.maxLength + SOLVER_EPSILON) {
            return;
        }

        const ndir = Vec2.normalize(delta, { x: 1, y: 0 });
        const rA = Vec2.rotate(Vec2.subtract(data.localAnchorA, bodyA.localCenter), bodyA.rotation);
        const rB = Vec2.rotate(Vec2.subtract(data.localAnchorB, bodyB.localCenter), bodyB.rotation);
        const error = distance - data.maxLength;
        const h = Math.max(dt, SOLVER_EPSILON);

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: -ndir.x, y: -ndir.y }, angular: -Vec2.cross(rA, ndir) },
                j2: { linear: { x: ndir.x, y: ndir.y }, angular: Vec2.cross(rB, ndir) },
                bias: -BAUMGARTE * error / h,
                impulse: 0,
                lowerLimit: 0,
                upperLimit: Infinity,
            },
        ]);
    }

    private _cacheJacobians(constraintId: ConstraintId, jacobians: JacobianRow[]): void {
        if (jacobians.length === 0) {
            return;
        }

        this._jacobianCache.set(constraintId, jacobians);
        this._lastPreparedConstraintCount++;
    }

    private _ensureSolverBody(bodyId: BodyId): SolverBody {
        const existing = this._bodyMap.get(bodyId);
        if (existing) {
            return existing;
        }

        const solverBody: SolverBody = {
            bodyId,
            invMass: this._bodyManager.getInverseMass(bodyId),
            invI: this._bodyManager.getInverseInertia(bodyId),
            linearVelocity: { ...this._bodyManager.getLinearVelocity(bodyId) },
            angularVelocity: this._bodyManager.getAngularVelocity(bodyId),
            position: { ...this._bodyManager.getPosition(bodyId) },
            rotation: this._bodyManager.getRotation(bodyId),
            localCenter: { ...this._bodyManager.getLocalCenter(bodyId) },
        };

        this._bodyMap.set(bodyId, solverBody);
        return solverBody;
    }
}