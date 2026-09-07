import { script } from '@axrone/ecs-runtime/decorators';
import type { Force, ISliderConstraintDef3D } from '../types';
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
 * Slider (Prismatic) joint — registers a SLIDER constraint (type 3).
 *
 * **Solver status: PARTIAL.** The Jacobian constraint solver enforces all 5 DOF:
 * - 2 lateral linear rows: lock translation perpendicular to the slide axis
 * - 3 angular lock rows: lock all rotation (slider must not rotate)
 * - 1 axial limit/motor row: translation along slide axis with optional
 *   distance limits (`lowerLimit`/`upperLimit`) and a linear motor
 *   (`motorSpeed`/`maxMotorForce`).
 *
 * The slide axis is defined by `localAxisA` in body A's local frame.
 * Body B slides along this axis relative to body A. Lateral motion and
 * all rotation are constrained.
 *
 * **Motor + limit interaction:** When the motor is active and the slider
 * reaches a limit boundary, the motor cannot push past the limit (Box2D
 * behavior). The motor can still pull away from the limit.
 *
 * **CAVEAT — `useSpring`/`spring` NOT wired:** The component exposes
 * `useSpring` and `spring` (spring/damper) properties, but these are
 * NOT passed to the constraint definition. Setting them has NO EFFECT.
 * This is a known gap — the solver does not yet implement slider spring.
 *
 * @see JOINT_CAPABILITY_3D
 */
@script({ scriptName: 'SliderJoint3D' })
export class SliderJoint3D extends Joint3D {
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

    protected override _createConstraint(ownerBody: Rigidbody3D): void {
        if (!this._constraintManager || !this._connectedBody) return;
        const def: ISliderConstraintDef3D = {
            bodyIdA: ownerBody.bodyId,
            bodyIdB: this._connectedBody.bodyId,
            localAnchorA: this._anchor,
            localAnchorB: this._connectedAnchor,
            localAxisA: this._axis,
            enableLimit: this._useLimits,
            lowerLimit: this._limits.min,
            upperLimit: this._limits.max,
            enableMotor: this._useMotor,
            motorSpeed: this._motor.targetVelocity,
            maxMotorForce: this._motor.force as unknown as Force,
            collideConnected: this._enableCollision,
        };
        this._constraintId = this._constraintManager.createSlider(def);
    }
    protected override _updateConstraint(): void {
        this._recreateConstraint();
    }
}
