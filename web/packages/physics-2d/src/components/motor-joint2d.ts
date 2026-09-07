import { script } from '@axrone/ecs-runtime/decorators';
import { Vec2 } from '@axrone/numeric';
import { Joint2D } from './joint2d';

/**
 * Motor joint: drives body B to a target linear/angular offset relative to body A.
 *
 * Unlike other joints there are no anchor points — the joint uses a linear
 * offset (target position of B relative to A) and an angular offset (target
 * rotation delta). The solver applies forces/torques to minimise the error.
 *
 * **Units** (METRE campaign):
 * - `linearOffset` — metres (local space offset)
 * - `angularOffset` — radians
 * - `maxForce` — N (Force)
 * - `maxTorque` — N·m (Torque)
 * - `correctionFactor` — dimensionless [0, 1] (Baumgarte scaling)
 *
 * Maps to solver `Motor` case.
 * **Solver**: FULL — linear offset + angular offset with force/torque clamping.
 *
 * @see JOINT_CAPABILITY_2D
 */
@script({
    scriptName: 'MotorJoint2D',
    priority: 80,
    description: 'Motor joint driving body to target offset',
    version: '1.0.0',
    author: 'Physics System Team',
    tags: ['physics', 'joint', '2d', 'motor'],
    singleton: false,
    dependencies: [],
    executeInEditMode: false,
})
export class MotorJoint2D extends Joint2D {
    private _linearOffset: Vec2 = Vec2.ZERO.clone();
    private _angularOffset: number = 0;
    private _maxForce: number = 1;
    private _maxTorque: number = 1;
    private _correctionFactor: number = 0.3;

    get linearOffset(): Vec2 {
        return this._linearOffset;
    }

    set linearOffset(value: Vec2) {
        this._linearOffset.x = value.x;
        this._linearOffset.y = value.y;
        this.recreateConstraint();
    }

    get angularOffset(): number {
        return this._angularOffset;
    }

    set angularOffset(value: number) {
        if (this._angularOffset !== value) {
            this._angularOffset = value;
            this.recreateConstraint();
        }
    }

    get maxForce(): number {
        return this._maxForce;
    }

    set maxForce(value: number) {
        if (this._maxForce !== value && value >= 0) {
            this._maxForce = value;
            this.recreateConstraint();
        }
    }

    get maxTorque(): number {
        return this._maxTorque;
    }

    set maxTorque(value: number) {
        if (this._maxTorque !== value && value >= 0) {
            this._maxTorque = value;
            this.recreateConstraint();
        }
    }

    get correctionFactor(): number {
        return this._correctionFactor;
    }

    set correctionFactor(value: number) {
        if (this._correctionFactor !== value && value >= 0 && value <= 1) {
            this._correctionFactor = value;
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
            .createMotorConstraint({
                bodyIdA: this._rigidbodyA.bodyId,
                bodyIdB: this._connectedBody.bodyId,
                linearOffset: { x: this._linearOffset.x, y: this._linearOffset.y },
                angularOffset: this._angularOffset,
                maxForce: this._maxForce,
                maxTorque: this._maxTorque,
                correctionFactor: this._correctionFactor,
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
            linearOffset: { x: this._linearOffset.x, y: this._linearOffset.y },
            angularOffset: this._angularOffset,
            maxForce: this._maxForce,
            maxTorque: this._maxTorque,
            correctionFactor: this._correctionFactor,
        };
    }

    deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        this._linearOffset = new Vec2(data.linearOffset?.x ?? 0, data.linearOffset?.y ?? 0);
        this._angularOffset = data.angularOffset ?? 0;
        this._maxForce = data.maxForce ?? 1;
        this._maxTorque = data.maxTorque ?? 1;
        this._correctionFactor = data.correctionFactor ?? 0.3;
    }
}
