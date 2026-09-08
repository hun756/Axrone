import { script } from '@axrone/ecs-runtime/decorators';
import type { IHingeConstraintDef3D, Torque } from '../types';
import type { Rigidbody3D } from './rigidbody3d';
import {
    DEFAULT_JOINT_MOTOR,
    DEFAULT_SOFT_JOINT_LIMIT_SPRING,
    Joint3D,
    type IJointLimits3D,
    type IJointMotor3D,
    type ISoftJointLimitSpring3D,
} from './joint3d';

/**
 * Hinge joint — registers a HINGE constraint (type 2).
 *
 * **Solver status: FULL.** The Jacobian constraint solver maintains anchor
 * alignment (3 linear rows), locks rotation about the two axes perpendicular
 * to the hinge axis (2 angular lock rows), and supports angle limits and
 * motor about the hinge axis (1 axial row).
 *
 * **Motor overshoot caveat:** When the motor drives toward a limit, the
 * sequential impulse solver stalls the motor at the limit row (Box2D
 * semantics), but a small overshoot is expected. This is inherent to the
 * iterative solver — the motor impulse applied before the limit row is
 * resolved causes brief exceedance.
 *
 * **Measured value** (constraints-jacobian-acceptance test): motor speed
 * 10 rad/s, limit π/4 rad (0.7854 rad), 120 steps at 1/60 s → steady-state
 * angle ≈ 0.867 rad, overshoot ≈ 0.08 rad (~4.7°), angular velocity stalled
 * to ≈ 1.2 rad/s (target 10 rad/s → stall successful). For tighter precision,
 * increase `velocityIterations` or use a penalty-based controller externally.
 *
 * @see JOINT_CAPABILITY_3D
 */
@script({ scriptName: 'HingeJoint3D' })
export class HingeJoint3D extends Joint3D {
    private _useLimits: boolean = false;
    private _useMotor: boolean = false;
    private readonly _limits: IJointLimits3D = {
        min: 0,
        max: 0,
        bounciness: 0,
        contactDistance: 0,
    };
    private readonly _motor: IJointMotor3D = { ...DEFAULT_JOINT_MOTOR };
    private _useSpring: boolean = false;
    private readonly _spring: ISoftJointLimitSpring3D = { ...DEFAULT_SOFT_JOINT_LIMIT_SPRING };
    private _angle: number = 0;
    private _velocity: number = 0;

    get useLimits(): boolean {
        return this._useLimits;
    }
    set useLimits(value: boolean) {
        this._useLimits = value;
        this._updateConstraint();
    }
    get limits(): Readonly<IJointLimits3D> {
        return this._limits;
    }
    set limits(value: Partial<IJointLimits3D>) {
        if (value.min !== undefined) this._limits.min = value.min;
        if (value.max !== undefined) this._limits.max = value.max;
        if (value.bounciness !== undefined)
            this._limits.bounciness = Math.max(0, Math.min(1, value.bounciness));
        if (value.contactDistance !== undefined)
            this._limits.contactDistance = Math.max(0, value.contactDistance);
        this._updateConstraint();
    }
    get useMotor(): boolean {
        return this._useMotor;
    }
    set useMotor(value: boolean) {
        this._useMotor = value;
        this._updateConstraint();
    }
    get motor(): Readonly<IJointMotor3D> {
        return this._motor;
    }
    set motor(value: Partial<IJointMotor3D>) {
        if (value.targetVelocity !== undefined) this._motor.targetVelocity = value.targetVelocity;
        if (value.force !== undefined) this._motor.force = Math.max(0, value.force);
        if (value.freeSpin !== undefined) this._motor.freeSpin = value.freeSpin;
        this._updateConstraint();
    }
    get useSpring(): boolean {
        return this._useSpring;
    }
    set useSpring(value: boolean) {
        this._useSpring = value;
        this._updateConstraint();
    }
    get spring(): Readonly<ISoftJointLimitSpring3D> {
        return this._spring;
    }
    set spring(value: Partial<ISoftJointLimitSpring3D>) {
        if (value.spring !== undefined) this._spring.spring = Math.max(0, value.spring);
        if (value.damper !== undefined) this._spring.damper = Math.max(0, value.damper);
        this._updateConstraint();
    }
    get angle(): number {
        return this._angle;
    }
    get velocity(): number {
        return this._velocity;
    }

    protected override _createConstraint(ownerBody: Rigidbody3D): void {
        if (!this._constraintManager || !this._connectedBody || !this._world) return;
        const def: IHingeConstraintDef3D = {
            bodyIdA: ownerBody.bodyId,
            bodyIdB: this._connectedBody.bodyId,
            localAnchorA: this._anchor,
            localAnchorB: this._connectedAnchor,
            localAxisA: this._axis,
            localAxisB: this._axis,
            enableLimit: this._useLimits,
            lowerLimit: this._limits.min,
            upperLimit: this._limits.max,
            enableMotor: this._useMotor,
            motorSpeed: this._motor.targetVelocity,
            maxMotorTorque: this._motor.force as unknown as Torque,
            collideConnected: this._enableCollision,
        };
        // Use world API to ensure constraint descriptor is registered for the solver framework
        this._constraintId = this._world.createHingeConstraint(def);
    }
    protected override _updateConstraint(): void {
        this._recreateConstraint();
    }

    /**
     * Serialize hinge-specific properties.
     *
     * Editor key mapping (components.rs `hinge_joint_3d_properties`):
     * - `limits: { enabled, min, max, bounciness, bounceThresholdVelocity, contactDistance }`
     * - `motor: { enabled, targetVelocity, force, freeSpin }`
     * - `spring: { enabled, spring, damper, targetPosition }`
     *
     * Engine uses flat `_useLimits`/`_useMotor`/`_useSpring` booleans; the
     * Editor nests them as `enabled` inside the corresponding sub-object.
     */
    override serialize(): Record<string, any> {
        return {
            ...super.serialize(),
            limits: {
                enabled: this._useLimits,
                min: this._limits.min,
                max: this._limits.max,
                bounciness: this._limits.bounciness,
                bounceThresholdVelocity: 0,
                contactDistance: this._limits.contactDistance,
            },
            motor: {
                enabled: this._useMotor,
                targetVelocity: this._motor.targetVelocity,
                force: this._motor.force,
                freeSpin: this._motor.freeSpin,
            },
            spring: {
                enabled: this._useSpring,
                spring: this._spring.spring,
                damper: this._spring.damper,
                targetPosition: 0,
            },
        };
    }

    override deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        const limits = data.limits;
        if (limits && typeof limits === 'object') {
            if (limits.enabled !== undefined) this._useLimits = !!limits.enabled;
            if (limits.min !== undefined) this._limits.min = limits.min;
            if (limits.max !== undefined) this._limits.max = limits.max;
            if (limits.bounciness !== undefined) this._limits.bounciness = Math.max(0, Math.min(1, limits.bounciness));
            if (limits.contactDistance !== undefined) this._limits.contactDistance = Math.max(0, limits.contactDistance);
        }
        const motor = data.motor;
        if (motor && typeof motor === 'object') {
            if (motor.enabled !== undefined) this._useMotor = !!motor.enabled;
            if (motor.targetVelocity !== undefined) this._motor.targetVelocity = motor.targetVelocity;
            if (motor.force !== undefined) this._motor.force = Math.max(0, motor.force);
            if (motor.freeSpin !== undefined) this._motor.freeSpin = !!motor.freeSpin;
        }
        const spring = data.spring;
        if (spring && typeof spring === 'object') {
            if (spring.enabled !== undefined) this._useSpring = !!spring.enabled;
            if (spring.spring !== undefined) this._spring.spring = Math.max(0, spring.spring);
            if (spring.damper !== undefined) this._spring.damper = Math.max(0, spring.damper);
            // targetPosition is Editor-only (hinge spring target), not consumed by solver
        }
    }
}
