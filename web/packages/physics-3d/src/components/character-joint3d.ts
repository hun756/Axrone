import { Vec3, Quat, type IVec3Like } from '@axrone/numeric';
import { script } from '@axrone/ecs-runtime/decorators';
import type { IConeTwistConstraintDef3D } from '../types';
import type { Rigidbody3D } from './rigidbody3d';
import {
    DEFAULT_SOFT_JOINT_LIMIT,
    DEFAULT_SOFT_JOINT_LIMIT_SPRING,
    Joint3D,
    type ISoftJointLimit3D,
    type ISoftJointLimitSpring3D,
} from './joint3d';

/**
 * Character joint — registers a CONE_TWIST constraint (type 4).
 *
 * **Solver status: FULL.** The cone-twist solver module implements
 * the full 6-row scheme — 3 anchor rows, cone swing limits via quaternion
 * swing-twist decomposition, and independent twist limit/motor rows.
 * Swing, twist limits, and twist motor are fully functional.
 *
 * Motor: The twist motor (`motorSpeed`/`maxMotorTorque`) drives the twist
 * rate toward the target velocity, with impulse capped by maxMotorTorque.
 * The motor row is solved BEFORE the twist limit row (Box2D stall semantics).
 *
 * **Motor overshoot caveat:** When the twist motor drives toward the twist
 * limit, the sequential impulse solver stalls the motor at the limit row,
 * but a small overshoot is expected. This is inherent to the iterative
 * solver — the motor impulse applied before the limit row is resolved
 * causes brief exceedance.
 *
 * **Estimated range** (not directly measured at steady state via the
 * high-level API — the cone-twist motor's effective torque is limited by
 * the relaxation factor and solver coupling, so the twist does not reliably
 * reach the limit in world.step() simulation; low-level solver tests in
 * physics-world-3d-constraints-cone-twist.test.ts scenario 6 confirm stall
 * semantics with maxTwist ≤ limit + 0.05 rad). Overshoot is expected to be
 * in the range of ~0.03–0.05 rad when the motor does reach the limit.
 * For tighter precision, increase `velocityIterations`.
 *
 * All distance units are METRES (ADR 0004). Angles are in radians.
 *
 * @see JOINT_CAPABILITY_3D
 * @remarks Solver: physics-world-3d-constraints-cone-twist.ts (unit-tested
 * against 26 direct-prepare scenarios including cone apex singularity and
 * twist motor stall).
 */
@script({ scriptName: 'CharacterJoint3D' })
export class CharacterJoint3D extends Joint3D {
    private _swingAxis: Vec3 = new Vec3(1, 0, 0);
    private readonly _lowTwistLimit: ISoftJointLimit3D = { ...DEFAULT_SOFT_JOINT_LIMIT };
    private readonly _highTwistLimit: ISoftJointLimit3D = { ...DEFAULT_SOFT_JOINT_LIMIT };
    private readonly _swing1Limit: ISoftJointLimit3D = { ...DEFAULT_SOFT_JOINT_LIMIT };
    private readonly _swing2Limit: ISoftJointLimit3D = { ...DEFAULT_SOFT_JOINT_LIMIT };
    private readonly _twistLimitSpring: ISoftJointLimitSpring3D = {
        ...DEFAULT_SOFT_JOINT_LIMIT_SPRING,
    };
    private readonly _swingLimitSpring: ISoftJointLimitSpring3D = {
        ...DEFAULT_SOFT_JOINT_LIMIT_SPRING,
    };
    private _enableProjection: boolean = false;
    private _projectionDistance: number = 0.1;
    private _projectionAngle: number = 180;
    private _motorSpeed: number = 0;
    private _maxMotorTorque: number = 0;

    get swingAxis(): Readonly<Vec3> {
        return this._swingAxis;
    }
    set swingAxis(value: IVec3Like) {
        if (Vec3.len(value) < 1e-6) return;
        Vec3.normalize(value, this._swingAxis);
        this._updateConstraint();
    }
    get lowTwistLimit(): Readonly<ISoftJointLimit3D> {
        return this._lowTwistLimit;
    }
    set lowTwistLimit(value: Partial<ISoftJointLimit3D>) {
        if (value.limit !== undefined) this._lowTwistLimit.limit = value.limit;
        if (value.bounciness !== undefined) this._lowTwistLimit.bounciness = value.bounciness;
        if (value.contactDistance !== undefined)
            this._lowTwistLimit.contactDistance = value.contactDistance;
        this._updateConstraint();
    }
    get highTwistLimit(): Readonly<ISoftJointLimit3D> {
        return this._highTwistLimit;
    }
    set highTwistLimit(value: Partial<ISoftJointLimit3D>) {
        if (value.limit !== undefined) this._highTwistLimit.limit = value.limit;
        if (value.bounciness !== undefined) this._highTwistLimit.bounciness = value.bounciness;
        if (value.contactDistance !== undefined)
            this._highTwistLimit.contactDistance = value.contactDistance;
        this._updateConstraint();
    }
    get swing1Limit(): Readonly<ISoftJointLimit3D> {
        return this._swing1Limit;
    }
    set swing1Limit(value: Partial<ISoftJointLimit3D>) {
        if (value.limit !== undefined) this._swing1Limit.limit = value.limit;
        if (value.bounciness !== undefined) this._swing1Limit.bounciness = value.bounciness;
        if (value.contactDistance !== undefined)
            this._swing1Limit.contactDistance = value.contactDistance;
        this._updateConstraint();
    }
    get swing2Limit(): Readonly<ISoftJointLimit3D> {
        return this._swing2Limit;
    }
    set swing2Limit(value: Partial<ISoftJointLimit3D>) {
        if (value.limit !== undefined) this._swing2Limit.limit = value.limit;
        if (value.bounciness !== undefined) this._swing2Limit.bounciness = value.bounciness;
        if (value.contactDistance !== undefined)
            this._swing2Limit.contactDistance = value.contactDistance;
        this._updateConstraint();
    }
    get twistLimitSpring(): Readonly<ISoftJointLimitSpring3D> {
        return this._twistLimitSpring;
    }
    set twistLimitSpring(value: Partial<ISoftJointLimitSpring3D>) {
        if (value.spring !== undefined) this._twistLimitSpring.spring = value.spring;
        if (value.damper !== undefined) this._twistLimitSpring.damper = value.damper;
        this._updateConstraint();
    }
    get swingLimitSpring(): Readonly<ISoftJointLimitSpring3D> {
        return this._swingLimitSpring;
    }
    set swingLimitSpring(value: Partial<ISoftJointLimitSpring3D>) {
        if (value.spring !== undefined) this._swingLimitSpring.spring = value.spring;
        if (value.damper !== undefined) this._swingLimitSpring.damper = value.damper;
        this._updateConstraint();
    }
    get enableProjection(): boolean {
        return this._enableProjection;
    }
    set enableProjection(value: boolean) {
        this._enableProjection = value;
    }
    get projectionDistance(): number {
        return this._projectionDistance;
    }
    set projectionDistance(value: number) {
        this._projectionDistance = Math.max(0, value);
    }
    get projectionAngle(): number {
        return this._projectionAngle;
    }
    set projectionAngle(value: number) {
        this._projectionAngle = Math.max(0, value);
    }
    /** Twist motor target velocity (rad/s). */
    get motorSpeed(): number {
        return this._motorSpeed;
    }
    set motorSpeed(value: number) {
        this._motorSpeed = value;
        this._updateConstraint();
    }
    /** Maximum twist motor torque (N·m). Zero disables the motor. */
    get maxMotorTorque(): number {
        return this._maxMotorTorque;
    }
    set maxMotorTorque(value: number) {
        this._maxMotorTorque = Math.max(0, value);
        this._updateConstraint();
    }

    protected override _createConstraint(ownerBody: Rigidbody3D): void {
        if (!this._constraintManager || !this._connectedBody) return;
        const def: IConeTwistConstraintDef3D = {
            bodyIdA: ownerBody.bodyId,
            bodyIdB: this._connectedBody.bodyId,
            localFrameA: { position: this._anchor, rotation: Quat.IDENTITY },
            localFrameB: { position: this._connectedAnchor, rotation: Quat.IDENTITY },
            swingSpan1: this._swing1Limit.limit,
            swingSpan2: this._swing2Limit.limit,
            twistSpan: this._highTwistLimit.limit - this._lowTwistLimit.limit,
            softness: 1,
            biasFactor: 0.3,
            relaxationFactor: 1,
            motorSpeed: this._motorSpeed,
            maxMotorTorque: this._maxMotorTorque,
            collideConnected: this._enableCollision,
        };
        this._constraintId = this._constraintManager.createConeTwist(def);
    }
    protected override _updateConstraint(): void {
        this._recreateConstraint();
    }

    /**
     * Serialize character-joint-specific properties.
     *
     * Editor key mapping (components.rs `character_joint_3d_properties`):
     * - `swingAxis: [x,y,z]`
     * - `lowTwistLimit`, `highTwistLimit`, `swing1Limit`, `swing2Limit`:
     *   `{ limit, bounciness, contactDistance }`
     * - `twistLimitSpring`, `swingLimitSpring`: `{ spring, damper }`
     * - `enableProjection`, `projectionDistance` (metres), `projectionAngle`
     *
     * Engine-only (not in Editor default JSON but wired to solver):
     * - `motorSpeed` (rad/s), `maxMotorTorque` (N·m)
     */
    override serialize(): Record<string, any> {
        return {
            ...super.serialize(),
            swingAxis: { x: this._swingAxis.x, y: this._swingAxis.y, z: this._swingAxis.z },
            lowTwistLimit: { limit: this._lowTwistLimit.limit, bounciness: this._lowTwistLimit.bounciness, contactDistance: this._lowTwistLimit.contactDistance },
            highTwistLimit: { limit: this._highTwistLimit.limit, bounciness: this._highTwistLimit.bounciness, contactDistance: this._highTwistLimit.contactDistance },
            swing1Limit: { limit: this._swing1Limit.limit, bounciness: this._swing1Limit.bounciness, contactDistance: this._swing1Limit.contactDistance },
            swing2Limit: { limit: this._swing2Limit.limit, bounciness: this._swing2Limit.bounciness, contactDistance: this._swing2Limit.contactDistance },
            twistLimitSpring: { spring: this._twistLimitSpring.spring, damper: this._twistLimitSpring.damper },
            swingLimitSpring: { spring: this._swingLimitSpring.spring, damper: this._swingLimitSpring.damper },
            enableProjection: this._enableProjection,
            projectionDistance: this._projectionDistance,
            projectionAngle: this._projectionAngle,
            motorSpeed: this._motorSpeed,
            maxMotorTorque: this._maxMotorTorque,
        };
    }

    override deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        // swingAxis: Vec3 — Editor writes ARRAY [x,y,z], accept both formats.
        if (data.swingAxis !== undefined) {
            const v = this.normalizeVec3Value(data.swingAxis, 1, 0, 0);
            this._swingAxis.x = v.x;
            this._swingAxis.y = v.y;
            this._swingAxis.z = v.z;
        }
        const softLimitKeys = ['lowTwistLimit', 'highTwistLimit', 'swing1Limit', 'swing2Limit'] as const;
        const softLimitTargets = [this._lowTwistLimit, this._highTwistLimit, this._swing1Limit, this._swing2Limit] as const;
        for (let i = 0; i < softLimitKeys.length; i++) {
            const src = data[softLimitKeys[i]];
            if (src && typeof src === 'object') {
                if (src.limit !== undefined) softLimitTargets[i].limit = src.limit;
                if (src.bounciness !== undefined) softLimitTargets[i].bounciness = src.bounciness;
                if (src.contactDistance !== undefined) softLimitTargets[i].contactDistance = src.contactDistance;
            }
        }
        const springKeys = ['twistLimitSpring', 'swingLimitSpring'] as const;
        const springTargets = [this._twistLimitSpring, this._swingLimitSpring] as const;
        for (let i = 0; i < springKeys.length; i++) {
            const src = data[springKeys[i]];
            if (src && typeof src === 'object') {
                if (src.spring !== undefined) springTargets[i].spring = src.spring;
                if (src.damper !== undefined) springTargets[i].damper = src.damper;
            }
        }
        if (data.enableProjection !== undefined) this._enableProjection = !!data.enableProjection;
        if (data.projectionDistance !== undefined) this._projectionDistance = Math.max(0, data.projectionDistance);
        if (data.projectionAngle !== undefined) this._projectionAngle = Math.max(0, data.projectionAngle);
        if (data.motorSpeed !== undefined) this._motorSpeed = data.motorSpeed;
        if (data.maxMotorTorque !== undefined) this._maxMotorTorque = Math.max(0, data.maxMotorTorque);
    }
}
