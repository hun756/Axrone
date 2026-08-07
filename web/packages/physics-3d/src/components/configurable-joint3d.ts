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
            collideConnected: this._enableCollision,
        };
        this._constraintId = this._constraintManager.createGeneric(def);
    }
    protected override _updateConstraint(): void {
        this._recreateConstraint();
    }
}
