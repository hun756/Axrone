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
            collideConnected: this._enableCollision,
        };
        this._constraintId = this._constraintManager.createConeTwist(def);
    }
    protected override _updateConstraint(): void {
        this._recreateConstraint();
    }
}
