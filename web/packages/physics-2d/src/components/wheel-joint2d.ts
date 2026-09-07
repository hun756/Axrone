import { script } from '@axrone/ecs-runtime/decorators';
import { Vec2 } from '@axrone/numeric';
import { Joint2D } from './joint2d';

/**
 * Wheel joint: constrains a body to move along a local axis with optional
 * suspension spring, translation limits, and rotational motor.
 *
 * Typical use: vehicle wheels — the axis defines the suspension direction,
 * stiffness/damping simulate the spring, limits bound suspension travel,
 * and the motor drives rotation.
 *
 * **Units** (METRE campaign):
 * - `anchorA`, `anchorB` — metres (local space)
 * - `lowerTranslation`, `upperTranslation` — metres
 * - `motorSpeed` — rad/s
 * - `maxMotorTorque` — N·m (Torque)
 * - `stiffness` — N/m (spring constant)
 * - `damping` — N·s/m (damping coefficient)
 *
 * Maps to solver `Wheel` case.
 * **Solver**: FULL — lateral constraint + suspension spring + translation limit + motor.
 *
 * @see JOINT_CAPABILITY_2D
 */
@script({
    scriptName: 'WheelJoint2D',
    priority: 80,
    description: 'Wheel/suspension joint with spring, limits, and motor',
    version: '1.0.0',
    author: 'Physics System Team',
    tags: ['physics', 'joint', '2d', 'wheel', 'suspension'],
    singleton: false,
    dependencies: [],
    executeInEditMode: false,
})
export class WheelJoint2D extends Joint2D {
    private _anchorA: Vec2 = Vec2.ZERO.clone();
    private _anchorB: Vec2 = Vec2.ZERO.clone();
    private _axis: Vec2 = new Vec2(0, 1);
    private _enableLimit: boolean = false;
    private _lowerTranslation: number = 0;
    private _upperTranslation: number = 0;
    private _enableMotor: boolean = false;
    private _motorSpeed: number = 0;
    private _maxMotorTorque: number = 0;
    private _stiffness: number = 0;
    private _damping: number = 0;

    get anchorA(): Vec2 {
        return this._anchorA;
    }

    set anchorA(value: Vec2) {
        this._anchorA.x = value.x;
        this._anchorA.y = value.y;
        this.recreateConstraint();
    }

    get anchorB(): Vec2 {
        return this._anchorB;
    }

    set anchorB(value: Vec2) {
        this._anchorB.x = value.x;
        this._anchorB.y = value.y;
        this.recreateConstraint();
    }

    get axis(): Vec2 {
        return this._axis;
    }

    set axis(value: Vec2) {
        const len = Math.sqrt(value.x * value.x + value.y * value.y);
        if (len > 1e-6) {
            this._axis.x = value.x / len;
            this._axis.y = value.y / len;
            this.recreateConstraint();
        }
    }

    get enableLimit(): boolean {
        return this._enableLimit;
    }

    set enableLimit(value: boolean) {
        if (this._enableLimit !== value) {
            this._enableLimit = value;
            this.recreateConstraint();
        }
    }

    get lowerTranslation(): number {
        return this._lowerTranslation;
    }

    set lowerTranslation(value: number) {
        if (this._lowerTranslation !== value) {
            this._lowerTranslation = value;
            this.recreateConstraint();
        }
    }

    get upperTranslation(): number {
        return this._upperTranslation;
    }

    set upperTranslation(value: number) {
        if (this._upperTranslation !== value) {
            this._upperTranslation = value;
            this.recreateConstraint();
        }
    }

    get enableMotor(): boolean {
        return this._enableMotor;
    }

    set enableMotor(value: boolean) {
        if (this._enableMotor !== value) {
            this._enableMotor = value;
            this.recreateConstraint();
        }
    }

    get motorSpeed(): number {
        return this._motorSpeed;
    }

    set motorSpeed(value: number) {
        if (this._motorSpeed !== value) {
            this._motorSpeed = value;
            this.recreateConstraint();
        }
    }

    get maxMotorTorque(): number {
        return this._maxMotorTorque;
    }

    set maxMotorTorque(value: number) {
        if (this._maxMotorTorque !== value && value >= 0) {
            this._maxMotorTorque = value;
            this.recreateConstraint();
        }
    }

    get stiffness(): number {
        return this._stiffness;
    }

    set stiffness(value: number) {
        if (this._stiffness !== value && value >= 0) {
            this._stiffness = value;
            this.recreateConstraint();
        }
    }

    get damping(): number {
        return this._damping;
    }

    set damping(value: number) {
        if (this._damping !== value && value >= 0) {
            this._damping = value;
            this.recreateConstraint();
        }
    }

    protected createConstraint(): void {
        if (this._constraintId || !this._rigidbodyA || !this._rigidbodyA.bodyId) return;
        if (!this._connectedBody || !this._connectedBody.bodyId) return;

        this._physicsWorld = this.getPhysicsWorld();
        if (!this._physicsWorld) return;

        this._constraintId = (this._physicsWorld as any)
            .getConstraintManager()
            .createWheelConstraint({
                bodyIdA: this._rigidbodyA.bodyId,
                bodyIdB: this._connectedBody.bodyId,
                localAnchorA: { x: this._anchorA.x, y: this._anchorA.y },
                localAnchorB: { x: this._anchorB.x, y: this._anchorB.y },
                localAxisA: { x: this._axis.x, y: this._axis.y },
                enableLimit: this._enableLimit,
                lowerTranslation: this._lowerTranslation,
                upperTranslation: this._upperTranslation,
                enableMotor: this._enableMotor,
                motorSpeed: this._motorSpeed,
                maxMotorTorque: this._maxMotorTorque,
                stiffness: this._stiffness,
                damping: this._damping,
                collideConnected: this._enableCollision,
            });
    }

    protected destroyConstraint(): void {
        if (!this._constraintId || !this._physicsWorld) return;
        (this._physicsWorld as any).getConstraintManager().destroyConstraint(this._constraintId);
        this._constraintId = null;
    }

    serialize(): Record<string, any> {
        return {
            ...super.serialize(),
            anchorA: { x: this._anchorA.x, y: this._anchorA.y },
            anchorB: { x: this._anchorB.x, y: this._anchorB.y },
            axis: { x: this._axis.x, y: this._axis.y },
            enableLimit: this._enableLimit,
            lowerTranslation: this._lowerTranslation,
            upperTranslation: this._upperTranslation,
            enableMotor: this._enableMotor,
            motorSpeed: this._motorSpeed,
            maxMotorTorque: this._maxMotorTorque,
            stiffness: this._stiffness,
            damping: this._damping,
        };
    }

    deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        this._anchorA = new Vec2(data.anchorA?.x ?? 0, data.anchorA?.y ?? 0);
        this._anchorB = new Vec2(data.anchorB?.x ?? 0, data.anchorB?.y ?? 0);
        this._axis = new Vec2(data.axis?.x ?? 0, data.axis?.y ?? 1);
        this._enableLimit = data.enableLimit ?? false;
        this._lowerTranslation = data.lowerTranslation ?? 0;
        this._upperTranslation = data.upperTranslation ?? 0;
        this._enableMotor = data.enableMotor ?? false;
        this._motorSpeed = data.motorSpeed ?? 0;
        this._maxMotorTorque = data.maxMotorTorque ?? 0;
        this._stiffness = data.stiffness ?? 0;
        this._damping = data.damping ?? 0;
    }
}
