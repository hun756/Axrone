import { script } from '@axrone/ecs-runtime/decorators';
import { Vec2 } from '@axrone/numeric';
import { Joint2D } from './joint2d';

@script({
    scriptName: 'HingeJoint2D',
    priority: 80,
    description: 'Revolute/hinge joint for rotation constraints',
    version: '1.0.0',
    author: 'Physics System Team',
    tags: ['physics', 'joint', '2d', 'hinge', 'revolute'],
    singleton: false,
    dependencies: [],
    executeInEditMode: false,
})
export class HingeJoint2D extends Joint2D {
    private _anchor: Vec2 = Vec2.ZERO.clone();
    private _useMotor: boolean = false;
    private _motor: { speed: number; maxTorque: number } = { speed: 0, maxTorque: 10000 };
    private _useLimits: boolean = false;
    private _limits: { min: number; max: number } = { min: 0, max: 360 };

    get anchor(): Vec2 {
        return this._anchor;
    }

    set anchor(value: Vec2) {
        this._anchor.x = value.x;
        this._anchor.y = value.y;
        this.recreateConstraint();
    }

    get useMotor(): boolean {
        return this._useMotor;
    }

    set useMotor(value: boolean) {
        if (this._useMotor !== value) {
            this._useMotor = value;
            this.recreateConstraint();
        }
    }

    get motorSpeed(): number {
        return this._motor.speed;
    }

    set motorSpeed(value: number) {
        this._motor.speed = value;
        this.recreateConstraint();
    }

    get maxMotorTorque(): number {
        return this._motor.maxTorque;
    }

    set maxMotorTorque(value: number) {
        this._motor.maxTorque = Math.max(0, value);
        this.recreateConstraint();
    }

    get useLimits(): boolean {
        return this._useLimits;
    }

    set useLimits(value: boolean) {
        if (this._useLimits !== value) {
            this._useLimits = value;
            this.recreateConstraint();
        }
    }

    get limits(): { min: number; max: number } {
        return { ...this._limits };
    }

    set limits(value: { min: number; max: number }) {
        this._limits = { ...value };
        this.recreateConstraint();
    }

    protected createConstraint(): void {
        if (this._constraintId || !this._rigidbodyA || !this._rigidbodyA.bodyId) return;
        if (!this._connectedBody || !this._connectedBody.bodyId) return;

        this._physicsWorld = this.getPhysicsWorld();
        if (!this._physicsWorld) return;

        this._constraintId = (this._physicsWorld as any)
            .getConstraintManager()
            .createRevoluteConstraint({
                bodyIdA: this._rigidbodyA.bodyId,
                bodyIdB: this._connectedBody.bodyId,
                localAnchorA: { x: this._anchor.x, y: this._anchor.y },
                localAnchorB: { x: 0, y: 0 },
                referenceAngle: 0,
                enableLimit: this._useLimits,
                lowerAngle: this._limits.min * (Math.PI / 180),
                upperAngle: this._limits.max * (Math.PI / 180),
                enableMotor: this._useMotor,
                motorSpeed: this._motor.speed,
                maxMotorTorque: this._motor.maxTorque as any,
                collideConnected: this._enableCollision,
            });
    }

    protected destroyConstraint(): void {
        if (!this._constraintId || !this._physicsWorld) return;
        (this._physicsWorld as any).getConstraintManager().destroyConstraint(this._constraintId);
        this._constraintId = null;
    }

    getJointAngle(): number {
        if (!this._rigidbodyA || !this._connectedBody) return 0;
        return this._rigidbodyA.getRotation() - this._connectedBody.getRotation();
    }

    getJointSpeed(): number {
        if (!this._rigidbodyA || !this._connectedBody) return 0;
        return this._rigidbodyA.angularVelocity - this._connectedBody.angularVelocity;
    }

    serialize(): Record<string, any> {
        return {
            ...super.serialize(),
            anchor: { x: this._anchor.x, y: this._anchor.y },
            useMotor: this._useMotor,
            motorSpeed: this._motor.speed,
            maxMotorTorque: this._motor.maxTorque,
            useLimits: this._useLimits,
            limits: { ...this._limits },
        };
    }

    deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        this._anchor = new Vec2(data.anchor?.x ?? 0, data.anchor?.y ?? 0);
        this._useMotor = data.useMotor ?? false;
        this._motor = {
            speed: data.motorSpeed ?? 0,
            maxTorque: data.maxMotorTorque ?? 10000,
        };
        this._useLimits = data.useLimits ?? false;
        this._limits = data.limits ?? { min: 0, max: 360 };
    }
}
