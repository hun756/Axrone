import { Component } from '@axrone/ecs-runtime';
import type { ConstraintId } from '../types';
import type { PhysicsWorld2D } from '../core/physics-world';
import { Rigidbody2D } from './rigidbody2d';
import { PhysicsWorld2DComponent } from './physics-world-2d-component';

/**
 * 2D joint solver capability matrix.
 *
 * | Component         | Constraint type | Solver case  | Solver status |
 * |-------------------|-----------------|--------------|---------------|
 * | DistanceJoint2D   | Distance        | Distance     | **FULL** — Jacobian + Baumgarte + soft spring |
 * | SpringJoint2D     | Distance (soft) | Distance     | **FULL** — same path, stiffness/damping active |
 * | HingeJoint2D      | Revolute        | Revolute     | **FULL** — anchor + limit + motor |
 * | SliderJoint2D     | Prismatic       | Prismatic    | **FULL** — lateral lock + rotation lock + limit + motor |
 * | FixedJoint2D      | Weld            | Weld         | **FULL** — 3-row Jacobian (x, y, angle) + soft |
 * | (solver-only)     | Wheel           | Wheel        | **FULL** — lateral + suspension + limit + motor |
 * | (solver-only)     | Motor           | Motor        | **FULL** — linear offset + angular offset |
 * | (solver-only)     | Mouse           | Mouse        | **FULL** — soft target seek + force clamp |
 * | (solver-only)     | Gear            | Gear         | **FULL** — rotation ratio enforcement |
 * | (solver-only)     | Rope            | Rope         | **FULL** — unilateral max-length guard |
 *
 * All 9 constraint types have real solver implementations with Jacobians,
 * bias computation, and impulse solving. No decorative joints.
 *
 * Symmetry note: 3D has 3 UNSUPPORTED joint types (Slider, ConeTwist,
 * Configurable). 2D has 0 UNSUPPORTED — all types are fully solved.
 *
 * TODO(P2-2d-joint-solvers): add component wrappers for Wheel, Motor,
 * Mouse, Gear, Rope (solver support exists, no @script component yet).
 */
export const JOINT_CAPABILITY_2D = {
    DISTANCE: 'full',
    SPRING: 'full',
    REVOLUTE: 'full',
    PRISMATIC: 'full',
    WELD: 'full',
    WHEEL: 'full',
    MOTOR: 'full',
    MOUSE: 'full',
    GEAR: 'full',
    ROPE: 'full',
} as const;

export type JointCapability2D = typeof JOINT_CAPABILITY_2D[keyof typeof JOINT_CAPABILITY_2D];

/**
 * Base class for all 2D joint components.
 *
 * All 2D joint types have fully functional solvers — velocity correction,
 * position correction, and limit/motor support where applicable.
 * See {@link JOINT_CAPABILITY_2D} for the complete capability matrix.
 *
 * @see JOINT_CAPABILITY_2D
 */
export abstract class Joint2D extends Component {
    protected _constraintId: ConstraintId | null = null;
    protected _physicsWorld: PhysicsWorld2D | null = null;
    protected _rigidbodyA: Rigidbody2D | null = null;
    protected _rigidbodyB: Rigidbody2D | null = null;

    protected _connectedBody: Rigidbody2D | null = null;
    protected _enableCollision: boolean = false;
    protected _breakForce: number = Infinity;
    protected _breakTorque: number = Infinity;
    protected _jointEnabled: boolean = true;

    get constraintId(): ConstraintId | null {
        return this._constraintId;
    }

    get connectedBody(): Rigidbody2D | null {
        return this._connectedBody;
    }

    set connectedBody(value: Rigidbody2D | null) {
        if (this._connectedBody !== value) {
            this._connectedBody = value;
            this.recreateConstraint();
        }
    }

    get enableCollision(): boolean {
        return this._enableCollision;
    }

    set enableCollision(value: boolean) {
        if (this._enableCollision !== value) {
            this._enableCollision = value;
            this.recreateConstraint();
        }
    }

    get breakForce(): number {
        return this._breakForce;
    }

    set breakForce(value: number) {
        this._breakForce = Math.max(0, value);
    }

    get breakTorque(): number {
        return this._breakTorque;
    }

    set breakTorque(value: number) {
        this._breakTorque = Math.max(0, value);
    }

    awake(): void {
        this._rigidbodyA = (this.getComponent(Rigidbody2D as any) as Rigidbody2D | null) ?? null;
        if (!this._rigidbodyA) {
            throw new Error('Joint2D requires Rigidbody2D component');
        }
    }

    start(): void {
        this.createConstraint();
    }

    onDestroy(): void {
        this.destroyConstraint();
    }

    protected abstract createConstraint(): void;
    protected abstract destroyConstraint(): void;

    protected recreateConstraint(): void {
        this.destroyConstraint();
        this.createConstraint();
    }

    protected getPhysicsWorld(): PhysicsWorld2D | null {
        if (this._physicsWorld) return this._physicsWorld;
        const worldComponent = PhysicsWorld2DComponent.instance;
        if (worldComponent?.physicsWorld) {
            this._physicsWorld = worldComponent.physicsWorld;
            return this._physicsWorld;
        }
        return null;
    }

    serialize(): Record<string, any> {
        return {
            enableCollision: this._enableCollision,
            breakForce: this._breakForce,
            breakTorque: this._breakTorque,
            enabled: this._jointEnabled,
        };
    }

    deserialize(data: Record<string, any>): void {
        this._enableCollision = data.enableCollision ?? false;
        this._breakForce = data.breakForce ?? Infinity;
        this._breakTorque = data.breakTorque ?? Infinity;
        this._jointEnabled = data.enabled ?? true;
    }
}
