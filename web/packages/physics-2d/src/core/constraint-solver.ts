import type { IVec2Like } from '@axrone/numeric';
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
}

const SOLVER_EPSILON = 1e-6;

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
        const uniqueConstraints = Array.from(new Set(constraints));
        this.prepareConstraints(uniqueConstraints, deltaTime);

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
                    this._prepareDistanceConstraint(constraintId);
                    break;
                case ConstraintType.Revolute:
                    this._prepareRevoluteConstraint(constraintId);
                    break;
                case ConstraintType.Prismatic:
                    this._preparePrismaticConstraint(constraintId);
                    break;
                case ConstraintType.Weld:
                    this._prepareWeldConstraint(constraintId);
                    break;
                case ConstraintType.Wheel:
                    this._prepareWheelConstraint(constraintId, deltaTime);
                    break;
                case ConstraintType.Motor:
                    this._prepareMotorConstraint(constraintId);
                    break;
                case ConstraintType.Mouse:
                    this._prepareMouseConstraint(constraintId);
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
                    minError = Math.min(minError, Math.abs(jacobian.bias));
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
            dotVec2(jacobian.j1.linear, bodyA.linearVelocity) +
            jacobian.j1.angular * bodyA.angularVelocity +
            dotVec2(jacobian.j2.linear, bodyB.linearVelocity) +
            jacobian.j2.angular * bodyB.angularVelocity;

        const effectiveMass =
            bodyA.invMass * lengthSquared(jacobian.j1.linear) +
            bodyA.invI * jacobian.j1.angular * jacobian.j1.angular +
            bodyB.invMass * lengthSquared(jacobian.j2.linear) +
            bodyB.invI * jacobian.j2.angular * jacobian.j2.angular;

        if (effectiveMass <= SOLVER_EPSILON) {
            return 0;
        }

        const previousImpulse = jacobian.impulse;
        const uncappedImpulse = previousImpulse - (velocityAlongJacobian + jacobian.bias) / effectiveMass;
        jacobian.impulse = clamp(uncappedImpulse, jacobian.lowerLimit, jacobian.upperLimit);
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

    private _prepareDistanceConstraint(constraintId: ConstraintId): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        this._ensureSolverBody(bodyIdA);
        this._ensureSolverBody(bodyIdB);

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 1, y: 0 }, angular: 0 },
                j2: { linear: { x: -1, y: 0 }, angular: 0 },
                bias: 0,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
        ]);
    }

    private _prepareRevoluteConstraint(constraintId: ConstraintId): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        this._ensureSolverBody(bodyIdA);
        this._ensureSolverBody(bodyIdB);

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 1, y: 0 }, angular: 0 },
                j2: { linear: { x: -1, y: 0 }, angular: 0 },
                bias: 0,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 1 }, angular: 0 },
                j2: { linear: { x: 0, y: -1 }, angular: 0 },
                bias: 0,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 0 }, angular: 1 },
                j2: { linear: { x: 0, y: 0 }, angular: -1 },
                bias: 0,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
        ]);
    }

    private _preparePrismaticConstraint(constraintId: ConstraintId): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        this._ensureSolverBody(bodyIdA);
        this._ensureSolverBody(bodyIdB);

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 1 }, angular: 0 },
                j2: { linear: { x: 0, y: -1 }, angular: 0 },
                bias: 0,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 0 }, angular: 1 },
                j2: { linear: { x: 0, y: 0 }, angular: -1 },
                bias: 0,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
        ]);
    }

    private _prepareWeldConstraint(constraintId: ConstraintId): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        this._ensureSolverBody(bodyIdA);
        this._ensureSolverBody(bodyIdB);

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 1, y: 0 }, angular: 0 },
                j2: { linear: { x: -1, y: 0 }, angular: 0 },
                bias: 0,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 1 }, angular: 0 },
                j2: { linear: { x: 0, y: -1 }, angular: 0 },
                bias: 0,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 0 }, angular: 1 },
                j2: { linear: { x: 0, y: 0 }, angular: -1 },
                bias: 0,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
        ]);
    }

    private _prepareWheelConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getWheelConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        const bodyB = this._ensureSolverBody(bodyIdB);

        const anchorA = transformPoint(bodyA.position, bodyA.rotation, data.localAnchorA);
        const anchorB = transformPoint(bodyB.position, bodyB.rotation, data.localAnchorB);
        const axis = normalizeVec2(rotateVec2(data.localAxisA, bodyA.rotation), { x: 1, y: 0 });
        const perpendicular = { x: -axis.y, y: axis.x };
        const delta = subtractVec2(anchorB, anchorA);
        const lateralError = dotVec2(delta, perpendicular);
        const translation = dotVec2(delta, axis);
        const inverseDt = 1 / Math.max(dt, SOLVER_EPSILON);
        const jacobians: JacobianRow[] = [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: perpendicular, angular: 0 },
                j2: { linear: { x: -perpendicular.x, y: -perpendicular.y }, angular: 0 },
                bias: -lateralError * inverseDt,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
            },
        ];

        let axisError = 0;
        if (data.enableLimit) {
            if (translation < data.lowerTranslation) {
                axisError = translation - data.lowerTranslation;
            } else if (translation > data.upperTranslation) {
                axisError = translation - data.upperTranslation;
            }
        } else if (Math.abs(translation) > SOLVER_EPSILON && (data.stiffness > 0 || data.damping > 0)) {
            axisError = translation * Math.max(1, data.stiffness + data.damping);
        }

        if (Math.abs(axisError) > SOLVER_EPSILON || data.stiffness > 0 || data.damping > 0) {
            jacobians.push({
                bodyIdA,
                bodyIdB,
                j1: { linear: axis, angular: 0 },
                j2: { linear: { x: -axis.x, y: -axis.y }, angular: 0 },
                bias: -axisError * inverseDt,
                impulse: 0,
                lowerLimit: -Infinity,
                upperLimit: Infinity,
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
                lowerLimit: -data.maxMotorTorque * Math.max(dt, SOLVER_EPSILON),
                upperLimit: data.maxMotorTorque * Math.max(dt, SOLVER_EPSILON),
            });
        }

        this._cacheJacobians(constraintId, jacobians);
    }

    private _prepareMotorConstraint(constraintId: ConstraintId): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        this._ensureSolverBody(bodyIdA);
        this._ensureSolverBody(bodyIdB);

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 1, y: 0 }, angular: 0 },
                j2: { linear: { x: -1, y: 0 }, angular: 0 },
                bias: 0,
                impulse: 0,
                lowerLimit: -1000,
                upperLimit: 1000,
            },
        ]);
    }

    private _prepareMouseConstraint(constraintId: ConstraintId): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        this._ensureSolverBody(bodyIdA);
        this._ensureSolverBody(bodyIdB);

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 1, y: 0 }, angular: 0 },
                j2: { linear: { x: 0, y: 0 }, angular: 0 },
                bias: 0,
                impulse: 0,
                lowerLimit: -10000,
                upperLimit: 10000,
            },
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 1 }, angular: 0 },
                j2: { linear: { x: 0, y: 0 }, angular: 0 },
                bias: 0,
                impulse: 0,
                lowerLimit: -10000,
                upperLimit: 10000,
            },
        ]);
    }

    private _prepareGearConstraint(constraintId: ConstraintId, dt: number): void {
        const { bodyIdA, bodyIdB } = this._constraintManager.getConstraintBodies(constraintId);
        const data = this._constraintManager.getGearConstraintData(constraintId);
        const bodyA = this._ensureSolverBody(bodyIdA);
        const bodyB = this._ensureSolverBody(bodyIdB);
        const relativeRotationError = bodyB.rotation - data.ratio * bodyA.rotation;

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: { x: 0, y: 0 }, angular: -data.ratio },
                j2: { linear: { x: 0, y: 0 }, angular: 1 },
                bias: -relativeRotationError / Math.max(dt, SOLVER_EPSILON),
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
        const anchorA = transformPoint(bodyA.position, bodyA.rotation, data.localAnchorA);
        const anchorB = transformPoint(bodyB.position, bodyB.rotation, data.localAnchorB);
        const delta = subtractVec2(anchorB, anchorA);
        const distance = Math.sqrt(lengthSquared(delta));

        if (distance <= data.maxLength + SOLVER_EPSILON) {
            return;
        }

        const normal = normalizeVec2(delta, { x: 1, y: 0 });
        const error = distance - data.maxLength;

        this._cacheJacobians(constraintId, [
            {
                bodyIdA,
                bodyIdB,
                j1: { linear: normal, angular: 0 },
                j2: { linear: { x: -normal.x, y: -normal.y }, angular: 0 },
                bias: -error / Math.max(dt, SOLVER_EPSILON),
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
        };

        this._bodyMap.set(bodyId, solverBody);
        return solverBody;
    }
}

function clamp(value: number, min: number, max: number): number {
    return Math.max(min, Math.min(max, value));
}

function dotVec2(a: Readonly<IVec2Like>, b: Readonly<IVec2Like>): number {
    return a.x * b.x + a.y * b.y;
}

function lengthSquared(vector: Readonly<IVec2Like>): number {
    return dotVec2(vector, vector);
}

function subtractVec2(a: Readonly<IVec2Like>, b: Readonly<IVec2Like>): IVec2Like {
    return {
        x: a.x - b.x,
        y: a.y - b.y,
    };
}

function rotateVec2(vector: Readonly<IVec2Like>, angle: number): IVec2Like {
    const cosine = Math.cos(angle);
    const sine = Math.sin(angle);
    return {
        x: cosine * vector.x - sine * vector.y,
        y: sine * vector.x + cosine * vector.y,
    };
}

function transformPoint(
    position: Readonly<IVec2Like>,
    rotation: number,
    localPoint: Readonly<IVec2Like>
): IVec2Like {
    const rotated = rotateVec2(localPoint, rotation);
    return {
        x: position.x + rotated.x,
        y: position.y + rotated.y,
    };
}

function normalizeVec2(vector: Readonly<IVec2Like>, fallback: Readonly<IVec2Like>): IVec2Like {
    const length = Math.sqrt(lengthSquared(vector));
    if (length <= SOLVER_EPSILON) {
        return { x: fallback.x, y: fallback.y };
    }

    return {
        x: vector.x / length,
        y: vector.y / length,
    };
}