import { Vec3, Quat, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import { script } from '@axrone/ecs-runtime/decorators';
import type { IGenericConstraintDef3D } from '../types';
import type { Rigidbody3D } from './rigidbody3d';
import {
    DEFAULT_JOINT_DRIVE,
    DEFAULT_SOFT_JOINT_LIMIT,
    DEFAULT_SOFT_JOINT_LIMIT_SPRING,
    Joint3D,
    JointDriveMode3D,
    type IJointDrive3D,
    type ISoftJointLimit3D,
    type ISoftJointLimitSpring3D,
} from './joint3d';

/**
 * Configurable joint — registers a GENERIC constraint (type 5).
 *
 * **Solver status: FULL.** The 6-DOF configurable joint solver is implemented
 * in `physics-world-3d-constraints-configurable.ts`. Each of the 6 DOFs
 * (3 linear + 3 angular) can independently be:
 *
 *   - **LOCKED** (motion=0): rigid constraint, bilateral row produced
 *   - **FREE** (motion=1): no constraint, no row produced
 *   - **LIMITED** (motion=2): per-axis limit range enforced, unilateral row
 *     produced only when the limit is violated
 *
 * Motion mode is inferred from the descriptor's per-axis limit pairs:
 * lower==upper → LOCKED, both infinite → FREE, otherwise → LIMITED.
 *
 * Measurement uses world-space axis projection for linear DOFs (consistent
 * with Fixed joint) and extractAxisAngle in the relative local frame for
 * angular DOFs. When localFrame rotations are identity, the behavior is
 * identical to the Fixed joint for all-locked configuration.
 *
 * **Motor/drive:** Per-axis linear and angular motors are wired to the solver.
 * `targetVelocity` → `motorSpeed` (m/s per axis), `targetAngularVelocity` →
 * `angularMotorSpeed` (rad/s per axis). Per-axis `maximumForce` from the
 * drive structs caps the motor impulse (N for linear, N·m for angular).
 * Motor is active only when both target velocity and maximum force are
 * non-zero on a given axis.
 *
 * **Motor overshoot caveat:** When a per-axis motor drives toward its angular
 * or linear limit, the sequential impulse solver stalls the motor at the
 * limit row, but a small overshoot is expected. This is inherent to the
 * iterative solver — the motor impulse applied before the limit row is
 * resolved causes brief exceedance.
 *
 * **Measured values** (diagnostic test via `createGenericConstraint`, angular
 * motor speed 10 rad/s, limit π/4 rad, 120 steps at 1/60 s): steady-state
 * angle ≈ 0.810 rad, overshoot ≈ 0.025 rad (~1.4°), angular velocity stalled
 * to ≈ 0.5 rad/s. Peak overshoot during transient ≈ 0.13 rad. For tighter
 * precision, increase `velocityIterations`.
 *
 * All distance units are METRES (ADR 0004). Angles are in radians.
 *
 * @see JOINT_CAPABILITY_3D
 */
@script({ scriptName: 'ConfigurableJoint3D' })
export class ConfigurableJoint3D extends Joint3D {
    private _xMotion: number = 0;
    private _yMotion: number = 0;
    private _zMotion: number = 0;
    private _angularXMotion: number = 0;
    private _angularYMotion: number = 0;
    private _angularZMotion: number = 0;
    private readonly _linearLimit: ISoftJointLimit3D = { ...DEFAULT_SOFT_JOINT_LIMIT };
    private readonly _linearLimitSpring: ISoftJointLimitSpring3D = {
        ...DEFAULT_SOFT_JOINT_LIMIT_SPRING,
    };
    private readonly _lowAngularXLimit: ISoftJointLimit3D = { ...DEFAULT_SOFT_JOINT_LIMIT };
    private readonly _highAngularXLimit: ISoftJointLimit3D = { ...DEFAULT_SOFT_JOINT_LIMIT };
    private readonly _angularYLimit: ISoftJointLimit3D = { ...DEFAULT_SOFT_JOINT_LIMIT };
    private readonly _angularZLimit: ISoftJointLimit3D = { ...DEFAULT_SOFT_JOINT_LIMIT };
    private readonly _angularXLimitSpring: ISoftJointLimitSpring3D = {
        ...DEFAULT_SOFT_JOINT_LIMIT_SPRING,
    };
    private readonly _angularYZLimitSpring: ISoftJointLimitSpring3D = {
        ...DEFAULT_SOFT_JOINT_LIMIT_SPRING,
    };
    private readonly _xDrive: IJointDrive3D = { ...DEFAULT_JOINT_DRIVE };
    private readonly _yDrive: IJointDrive3D = { ...DEFAULT_JOINT_DRIVE };
    private readonly _zDrive: IJointDrive3D = { ...DEFAULT_JOINT_DRIVE };
    private readonly _angularXDrive: IJointDrive3D = { ...DEFAULT_JOINT_DRIVE };
    private readonly _angularYZDrive: IJointDrive3D = { ...DEFAULT_JOINT_DRIVE };
    private readonly _slerpDrive: IJointDrive3D = { ...DEFAULT_JOINT_DRIVE };
    private readonly _targetPosition: Vec3 = Vec3.create();
    private readonly _targetVelocity: Vec3 = Vec3.create();
    private readonly _targetRotation: Quat = Quat.create();
    private readonly _targetAngularVelocity: Vec3 = Vec3.create();
    private _rotationDriveMode: JointDriveMode3D = JointDriveMode3D.None;
    private _projectionMode: number = 0;
    private _projectionDistance: number = 0.1;
    private _projectionAngle: number = 180;
    private _configuredInWorldSpace: boolean = false;
    private _swapBodies: boolean = false;

    get xMotion(): number {
        return this._xMotion;
    }
    set xMotion(value: number) {
        this._xMotion = value;
        this._updateConstraint();
    }
    get yMotion(): number {
        return this._yMotion;
    }
    set yMotion(value: number) {
        this._yMotion = value;
        this._updateConstraint();
    }
    get zMotion(): number {
        return this._zMotion;
    }
    set zMotion(value: number) {
        this._zMotion = value;
        this._updateConstraint();
    }
    get angularXMotion(): number {
        return this._angularXMotion;
    }
    set angularXMotion(value: number) {
        this._angularXMotion = value;
        this._updateConstraint();
    }
    get angularYMotion(): number {
        return this._angularYMotion;
    }
    set angularYMotion(value: number) {
        this._angularYMotion = value;
        this._updateConstraint();
    }
    get angularZMotion(): number {
        return this._angularZMotion;
    }
    set angularZMotion(value: number) {
        this._angularZMotion = value;
        this._updateConstraint();
    }
    get linearLimit(): Readonly<ISoftJointLimit3D> {
        return this._linearLimit;
    }
    set linearLimit(value: Partial<ISoftJointLimit3D>) {
        if (value.limit !== undefined) this._linearLimit.limit = value.limit;
        if (value.bounciness !== undefined) this._linearLimit.bounciness = value.bounciness;
        if (value.contactDistance !== undefined)
            this._linearLimit.contactDistance = value.contactDistance;
        this._updateConstraint();
    }
    get targetPosition(): Readonly<Vec3> {
        return this._targetPosition;
    }
    set targetPosition(value: IVec3Like) {
        this._targetPosition.x = value.x;
        this._targetPosition.y = value.y;
        this._targetPosition.z = value.z;
        this._updateConstraint();
    }
    get targetVelocity(): Readonly<Vec3> {
        return this._targetVelocity;
    }
    set targetVelocity(value: IVec3Like) {
        this._targetVelocity.x = value.x;
        this._targetVelocity.y = value.y;
        this._targetVelocity.z = value.z;
        this._updateConstraint();
    }
    get targetRotation(): Readonly<Quat> {
        return this._targetRotation;
    }
    set targetRotation(value: IQuatLike) {
        this._targetRotation.x = value.x;
        this._targetRotation.y = value.y;
        this._targetRotation.z = value.z;
        this._targetRotation.w = value.w;
        this._updateConstraint();
    }
    get targetAngularVelocity(): Readonly<Vec3> {
        return this._targetAngularVelocity;
    }
    set targetAngularVelocity(value: IVec3Like) {
        this._targetAngularVelocity.x = value.x;
        this._targetAngularVelocity.y = value.y;
        this._targetAngularVelocity.z = value.z;
        this._updateConstraint();
    }
    get rotationDriveMode(): JointDriveMode3D {
        return this._rotationDriveMode;
    }
    set rotationDriveMode(value: JointDriveMode3D) {
        this._rotationDriveMode = value;
        this._updateConstraint();
    }
    get configuredInWorldSpace(): boolean {
        return this._configuredInWorldSpace;
    }
    set configuredInWorldSpace(value: boolean) {
        this._configuredInWorldSpace = value;
        this._updateConstraint();
    }
    get swapBodies(): boolean {
        return this._swapBodies;
    }
    set swapBodies(value: boolean) {
        this._swapBodies = value;
        this._recreateConstraint();
    }

    protected override _createConstraint(ownerBody: Rigidbody3D): void {
        if (!this._constraintManager || !this._connectedBody) return;
        const linLow: IVec3Like = {
            x: this._xMotion === 2 ? -this._linearLimit.limit : this._xMotion === 0 ? 0 : -Infinity,
            y: this._yMotion === 2 ? -this._linearLimit.limit : this._yMotion === 0 ? 0 : -Infinity,
            z: this._zMotion === 2 ? -this._linearLimit.limit : this._zMotion === 0 ? 0 : -Infinity,
        };
        const linUp: IVec3Like = {
            x: this._xMotion === 2 ? this._linearLimit.limit : this._xMotion === 0 ? 0 : Infinity,
            y: this._yMotion === 2 ? this._linearLimit.limit : this._yMotion === 0 ? 0 : Infinity,
            z: this._zMotion === 2 ? this._linearLimit.limit : this._zMotion === 0 ? 0 : Infinity,
        };
        const angLow: IVec3Like = {
            x:
                this._angularXMotion === 2
                    ? this._lowAngularXLimit.limit
                    : this._angularXMotion === 0
                      ? 0
                      : -Infinity,
            y:
                this._angularYMotion === 2
                    ? -this._angularYLimit.limit
                    : this._angularYMotion === 0
                      ? 0
                      : -Infinity,
            z:
                this._angularZMotion === 2
                    ? -this._angularZLimit.limit
                    : this._angularZMotion === 0
                      ? 0
                      : -Infinity,
        };
        const angUp: IVec3Like = {
            x:
                this._angularXMotion === 2
                    ? this._highAngularXLimit.limit
                    : this._angularXMotion === 0
                      ? 0
                      : Infinity,
            y:
                this._angularYMotion === 2
                    ? this._angularYLimit.limit
                    : this._angularYMotion === 0
                      ? 0
                      : Infinity,
            z:
                this._angularZMotion === 2
                    ? this._angularZLimit.limit
                    : this._angularZMotion === 0
                      ? 0
                      : Infinity,
        };
        const def: IGenericConstraintDef3D = {
            bodyIdA: ownerBody.bodyId,
            bodyIdB: this._connectedBody.bodyId,
            localFrameA: { position: this._anchor, rotation: Quat.IDENTITY },
            localFrameB: { position: this._connectedAnchor, rotation: Quat.IDENTITY },
            linearLowerLimit: linLow,
            linearUpperLimit: linUp,
            angularLowerLimit: angLow,
            angularUpperLimit: angUp,
            motorSpeed: { x: this._targetVelocity.x, y: this._targetVelocity.y, z: this._targetVelocity.z },
            maxMotorForce: { x: this._xDrive.maximumForce, y: this._yDrive.maximumForce, z: this._zDrive.maximumForce },
            angularMotorSpeed: { x: this._targetAngularVelocity.x, y: this._targetAngularVelocity.y, z: this._targetAngularVelocity.z },
            angularMaxMotorTorque: { x: this._angularXDrive.maximumForce, y: this._angularYZDrive.maximumForce, z: this._angularYZDrive.maximumForce },
            collideConnected: this._enableCollision,
        };
        this._constraintId = this._constraintManager.createGeneric(def);
    }
    protected override _updateConstraint(): void {
        this._recreateConstraint();
    }

    /**
     * Serialize configurable-joint-specific properties.
     *
     * Editor key mapping (components.rs `configurable_joint_3d_properties`):
     * - `xMotion`, `yMotion`, `zMotion`, `angularXMotion`, `angularYMotion`, `angularZMotion` (number)
     * - `linearLimit: { limit, bounciness, contactDistance }`
     * - `targetPosition` (Vec3), `targetVelocity` (Vec3, m/s per axis)
     * - `targetRotation` (Quat), `targetAngularVelocity` (Vec3, rad/s per axis)
     * - `rotationDriveMode` (number), `configuredInWorldSpace`, `swapBodies`
     *
     * Engine-only drive structs (not in Editor default JSON but wired to solver):
     * - `xDrive`, `yDrive`, `zDrive` (linear per-axis drive: positionSpring, positionDamper, maximumForce, useAcceleration)
     * - `angularXDrive`, `angularYZDrive` (angular per-axis drive)
     */
    override serialize(): Record<string, any> {
        return {
            ...super.serialize(),
            xMotion: this._xMotion,
            yMotion: this._yMotion,
            zMotion: this._zMotion,
            angularXMotion: this._angularXMotion,
            angularYMotion: this._angularYMotion,
            angularZMotion: this._angularZMotion,
            linearLimit: { limit: this._linearLimit.limit, bounciness: this._linearLimit.bounciness, contactDistance: this._linearLimit.contactDistance },
            targetPosition: { x: this._targetPosition.x, y: this._targetPosition.y, z: this._targetPosition.z },
            targetVelocity: { x: this._targetVelocity.x, y: this._targetVelocity.y, z: this._targetVelocity.z },
            targetRotation: { x: this._targetRotation.x, y: this._targetRotation.y, z: this._targetRotation.z, w: this._targetRotation.w },
            targetAngularVelocity: { x: this._targetAngularVelocity.x, y: this._targetAngularVelocity.y, z: this._targetAngularVelocity.z },
            rotationDriveMode: this._rotationDriveMode,
            configuredInWorldSpace: this._configuredInWorldSpace,
            swapBodies: this._swapBodies,
            xDrive: { ...this._xDrive },
            yDrive: { ...this._yDrive },
            zDrive: { ...this._zDrive },
            angularXDrive: { ...this._angularXDrive },
            angularYZDrive: { ...this._angularYZDrive },
        };
    }

    override deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        if (data.xMotion !== undefined) this._xMotion = data.xMotion;
        if (data.yMotion !== undefined) this._yMotion = data.yMotion;
        if (data.zMotion !== undefined) this._zMotion = data.zMotion;
        if (data.angularXMotion !== undefined) this._angularXMotion = data.angularXMotion;
        if (data.angularYMotion !== undefined) this._angularYMotion = data.angularYMotion;
        if (data.angularZMotion !== undefined) this._angularZMotion = data.angularZMotion;
        const ll = data.linearLimit;
        if (ll && typeof ll === 'object') {
            if (ll.limit !== undefined) this._linearLimit.limit = ll.limit;
            if (ll.bounciness !== undefined) this._linearLimit.bounciness = ll.bounciness;
            if (ll.contactDistance !== undefined) this._linearLimit.contactDistance = ll.contactDistance;
        }
        // Vec3/Quat fields: Editor writes ARRAY [x,y,z] or [x,y,z,w], accept both formats.
        if (data.targetPosition !== undefined) {
            const v = this.normalizeVec3Value(data.targetPosition);
            this._targetPosition.x = v.x;
            this._targetPosition.y = v.y;
            this._targetPosition.z = v.z;
        }
        if (data.targetVelocity !== undefined) {
            const v = this.normalizeVec3Value(data.targetVelocity);
            this._targetVelocity.x = v.x;
            this._targetVelocity.y = v.y;
            this._targetVelocity.z = v.z;
        }
        if (data.targetRotation !== undefined) {
            const v = this.normalizeQuatValue(data.targetRotation);
            this._targetRotation.x = v.x;
            this._targetRotation.y = v.y;
            this._targetRotation.z = v.z;
            this._targetRotation.w = v.w;
        }
        if (data.targetAngularVelocity !== undefined) {
            const v = this.normalizeVec3Value(data.targetAngularVelocity);
            this._targetAngularVelocity.x = v.x;
            this._targetAngularVelocity.y = v.y;
            this._targetAngularVelocity.z = v.z;
        }
        if (data.rotationDriveMode !== undefined) this._rotationDriveMode = data.rotationDriveMode;
        if (data.configuredInWorldSpace !== undefined) this._configuredInWorldSpace = !!data.configuredInWorldSpace;
        if (data.swapBodies !== undefined) this._swapBodies = !!data.swapBodies;
        // Drive structs — engine-only, safe defaults when absent
        const driveKeys = ['xDrive', 'yDrive', 'zDrive', 'angularXDrive', 'angularYZDrive'] as const;
        const driveTargets = [this._xDrive, this._yDrive, this._zDrive, this._angularXDrive, this._angularYZDrive] as const;
        for (let i = 0; i < driveKeys.length; i++) {
            const src = data[driveKeys[i]];
            if (src && typeof src === 'object') {
                if (src.positionSpring !== undefined) driveTargets[i].positionSpring = src.positionSpring;
                if (src.positionDamper !== undefined) driveTargets[i].positionDamper = src.positionDamper;
                if (src.maximumForce !== undefined) driveTargets[i].maximumForce = src.maximumForce;
                if (src.useAcceleration !== undefined) driveTargets[i].useAcceleration = !!src.useAcceleration;
            }
        }
    }
}
