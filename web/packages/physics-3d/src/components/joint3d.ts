import { Vec3, Quat, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import { Component } from '@axrone/ecs-runtime';
import type { ConstraintId3D } from '../types';
import type { ConstraintManager3D, PhysicsWorld3D } from '../core/physics-world-3d';
import type { Rigidbody3D } from './rigidbody3d';

export const INVALID_CONSTRAINT_ID = -1 as ConstraintId3D;

export const enum JointDriveMode3D {
    None = 0,
    Position = 1,
    Velocity = 2,
    PositionAndVelocity = 3,
}

export interface IJointDrive3D {
    positionSpring: number;
    positionDamper: number;
    maximumForce: number;
    useAcceleration: boolean;
}
export interface IJointLimits3D {
    min: number;
    max: number;
    bounciness: number;
    contactDistance: number;
}
export interface IJointMotor3D {
    targetVelocity: number;
    force: number;
    freeSpin: boolean;
}
export interface ISoftJointLimit3D {
    limit: number;
    bounciness: number;
    contactDistance: number;
}
export interface ISoftJointLimitSpring3D {
    spring: number;
    damper: number;
}

export const DEFAULT_JOINT_DRIVE: Readonly<IJointDrive3D> = {
    positionSpring: 0,
    positionDamper: 0,
    maximumForce: Infinity,
    useAcceleration: false,
};
export const DEFAULT_JOINT_MOTOR: Readonly<IJointMotor3D> = {
    targetVelocity: 0,
    force: 0,
    freeSpin: false,
};
export const DEFAULT_SOFT_JOINT_LIMIT: Readonly<ISoftJointLimit3D> = {
    limit: 0,
    bounciness: 0,
    contactDistance: 0,
};
export const DEFAULT_SOFT_JOINT_LIMIT_SPRING: Readonly<ISoftJointLimitSpring3D> = {
    spring: 0,
    damper: 0,
};

export abstract class Joint3D extends Component {
    protected _constraintId: ConstraintId3D = INVALID_CONSTRAINT_ID;
    protected _constraintManager: ConstraintManager3D | null = null;
    protected _world: PhysicsWorld3D | null = null;
    protected _ownerBody: Rigidbody3D | null = null;
    protected _joint3dEnabled: boolean = true;
    protected _connectedBody: Rigidbody3D | null = null;
    protected _autoConfigureConnectedAnchor: boolean = true;
    protected readonly _anchor: Vec3 = Vec3.create();
    protected readonly _connectedAnchor: Vec3 = Vec3.create();
    protected readonly _axis: Vec3 = new Vec3(1, 0, 0);
    protected readonly _secondaryAxis: Vec3 = new Vec3(0, 1, 0);
    protected _breakForce: number = Infinity;
    protected _breakTorque: number = Infinity;
    protected _enableCollision: boolean = false;
    protected _enablePreprocessing: boolean = true;
    protected _massScale: number = 1;
    protected _connectedMassScale: number = 1;
    private readonly _currentForce: Vec3 = Vec3.create();
    private readonly _currentTorque: Vec3 = Vec3.create();

    get constraintId(): ConstraintId3D {
        return this._constraintId;
    }
    get connectedBody(): Rigidbody3D | null {
        return this._connectedBody;
    }
    set connectedBody(value: Rigidbody3D | null) {
        if (this._connectedBody === value) return;
        this._connectedBody = value;
        if (this._autoConfigureConnectedAnchor && value) this._configureConnectedAnchor();
        this._recreateConstraint();
    }
    get autoConfigureConnectedAnchor(): boolean {
        return this._autoConfigureConnectedAnchor;
    }
    set autoConfigureConnectedAnchor(value: boolean) {
        this._autoConfigureConnectedAnchor = value;
        if (value && this._connectedBody) {
            this._configureConnectedAnchor();
            this._recreateConstraint();
        }
    }
    get anchor(): Readonly<Vec3> {
        return this._anchor;
    }
    set anchor(value: IVec3Like) {
        this._anchor.x = value.x;
        this._anchor.y = value.y;
        this._anchor.z = value.z;
        if (this._autoConfigureConnectedAnchor) this._configureConnectedAnchor();
        this._updateConstraint();
    }
    get connectedAnchor(): Readonly<Vec3> {
        return this._connectedAnchor;
    }
    set connectedAnchor(value: IVec3Like) {
        this._autoConfigureConnectedAnchor = false;
        this._connectedAnchor.x = value.x;
        this._connectedAnchor.y = value.y;
        this._connectedAnchor.z = value.z;
        this._updateConstraint();
    }
    get axis(): Readonly<Vec3> {
        return this._axis;
    }
    set axis(value: IVec3Like) {
        if (Vec3.len(value) < 1e-6) return;
        Vec3.normalize(value, this._axis);
        this._updateConstraint();
    }
    get secondaryAxis(): Readonly<Vec3> {
        return this._secondaryAxis;
    }
    set secondaryAxis(value: IVec3Like) {
        if (Vec3.len(value) < 1e-6) return;
        Vec3.normalize(value, this._secondaryAxis);
        this._updateConstraint();
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
    get enableCollision(): boolean {
        return this._enableCollision;
    }
    set enableCollision(value: boolean) {
        this._enableCollision = value;
        this._updateConstraint();
    }
    get enablePreprocessing(): boolean {
        return this._enablePreprocessing;
    }
    set enablePreprocessing(value: boolean) {
        this._enablePreprocessing = value;
    }
    get massScale(): number {
        return this._massScale;
    }
    set massScale(value: number) {
        this._massScale = Math.max(0.0001, value);
    }
    get connectedMassScale(): number {
        return this._connectedMassScale;
    }
    set connectedMassScale(value: number) {
        this._connectedMassScale = Math.max(0.0001, value);
    }
    get currentForce(): Readonly<IVec3Like> {
        return this._currentForce;
    }
    get currentTorque(): Readonly<IVec3Like> {
        return this._currentTorque;
    }

    initialize(world: PhysicsWorld3D, ownerBody: Rigidbody3D, connectedBody?: Rigidbody3D): void {
        this._world = world;
        this._constraintManager = world.getConstraintManager();
        this._ownerBody = ownerBody;
        this._connectedBody = connectedBody ?? null;
        this._createConstraint(ownerBody);
    }

    override fixedUpdate(deltaTime: number): void {
        if (!this._joint3dEnabled) return;
        this._checkBreakForce();
    }

    override onDestroy(): void {
        if (this._constraintManager && this._constraintId !== INVALID_CONSTRAINT_ID) {
            this._constraintManager.destroyConstraint(this._constraintId);
            this._constraintId = INVALID_CONSTRAINT_ID;
        }
        this._constraintManager = null;
        this._world = null;
        this._ownerBody = null;
        this._connectedBody = null;
    }

    protected abstract _createConstraint(ownerBody: Rigidbody3D): void;
    protected abstract _updateConstraint(): void;

    protected _recreateConstraint(): void {
        if (this._constraintId !== INVALID_CONSTRAINT_ID && this._constraintManager) {
            this._constraintManager.destroyConstraint(this._constraintId);
        }

        this._constraintId = INVALID_CONSTRAINT_ID;

        if (!this._constraintManager || !this._ownerBody) {
            return;
        }

        this._createConstraint(this._ownerBody);
    }

    protected _configureConnectedAnchor(): void {
        if (!this._connectedBody || !this.transform) return;
        const worldAnchor = this._getWorldAnchor();
        const connectedPos = this._connectedBody.position;
        this._connectedAnchor.x = worldAnchor.x - connectedPos.x;
        this._connectedAnchor.y = worldAnchor.y - connectedPos.y;
        this._connectedAnchor.z = worldAnchor.z - connectedPos.z;
    }

    protected _getWorldAnchor(): IVec3Like {
        if (!this.transform) return this._anchor;
        const pos = this.transform.worldPosition;
        const rot = this.transform.worldRotation;
        return this._transformPoint(pos, rot, this._anchor);
    }

    protected _transformPoint(pos: IVec3Like, rot: IQuatLike, localPoint: IVec3Like): IVec3Like {
        const rx = rot.x * 2;
        const ry = rot.y * 2;
        const rz = rot.z * 2;
        const wx = rot.w * rx;
        const wy = rot.w * ry;
        const wz = rot.w * rz;
        const xx = rot.x * rx;
        const xy = rot.x * ry;
        const xz = rot.x * rz;
        const yy = rot.y * ry;
        const yz = rot.y * rz;
        const zz = rot.z * rz;
        return {
            x:
                pos.x +
                (1 - (yy + zz)) * localPoint.x +
                (xy - wz) * localPoint.y +
                (xz + wy) * localPoint.z,
            y:
                pos.y +
                (xy + wz) * localPoint.x +
                (1 - (xx + zz)) * localPoint.y +
                (yz - wx) * localPoint.z,
            z:
                pos.z +
                (xz - wy) * localPoint.x +
                (yz + wx) * localPoint.y +
                (1 - (xx + yy)) * localPoint.z,
        };
    }

    protected _calculatePerpendicularAxis(): IVec3Like {
        const ax = this._axis.x;
        const ay = this._axis.y;
        const az = this._axis.z;
        let perpX: number;
        let perpY: number;
        let perpZ: number;
        if (Math.abs(ax) < 0.9) {
            perpX = ay;
            perpY = -ax;
            perpZ = 0;
        } else {
            perpX = 0;
            perpY = az;
            perpZ = -ay;
        }
        const invLen = 1 / Math.sqrt(perpX * perpX + perpY * perpY + perpZ * perpZ);
        return { x: perpX * invLen, y: perpY * invLen, z: perpZ * invLen };
    }

    protected _checkBreakForce(): void {
        if (!this._constraintManager || this._constraintId === INVALID_CONSTRAINT_ID) return;
        const forceLen = Vec3.len(this._currentForce);
        const torqueLen = Vec3.len(this._currentTorque);
        if (forceLen > this._breakForce || torqueLen > this._breakTorque) {
            this._constraintManager.destroyConstraint(this._constraintId);
            this._constraintId = INVALID_CONSTRAINT_ID;
        }
    }
}
