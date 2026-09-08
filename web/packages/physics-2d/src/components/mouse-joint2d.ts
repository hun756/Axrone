import { script } from '@axrone/ecs-runtime/decorators';
import { Vec2 } from '@axrone/numeric';
import { Joint2D } from './joint2d';

/**
 * Mouse joint: drives a body towards a world-space target point using a
 * soft spring constraint.
 *
 * Typical use: drag-and-drop interaction, mouse picking, touch input.
 * The `target` is in world coordinates — update it each frame to follow
 * the pointer.
 *
 * **Units** (METRE campaign):
 * - `target` — metres (world space)
 * - `maxForce` — N (Force)
 * - `stiffness` — N/m (spring constant, default 5)
 * - `damping` — N·s/m (damping coefficient, default 0.7)
 *
 * Maps to solver `Mouse` case.
 * **Solver**: FULL — soft target seek with force clamping.
 *
 * @see JOINT_CAPABILITY_2D
 */
@script({
    scriptName: 'MouseJoint2D',
    priority: 80,
    description: 'Mouse/touch drag joint seeking a world-space target',
    version: '1.0.0',
    author: 'Physics System Team',
    tags: ['physics', 'joint', '2d', 'mouse'],
    singleton: false,
    dependencies: [],
    executeInEditMode: false,
})
export class MouseJoint2D extends Joint2D {
    private _target: Vec2 = Vec2.ZERO.clone();
    private _maxForce: number = 1000;
    private _stiffness: number = 5;
    private _damping: number = 0.7;

    get target(): Vec2 {
        return this._target;
    }

    set target(value: Vec2) {
        this._target.x = value.x;
        this._target.y = value.y;
        this.recreateConstraint();
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
            .createMouseConstraint({
                bodyIdA: this._rigidbodyA.bodyId,
                bodyIdB: this._connectedBody.bodyId,
                target: { x: this._target.x, y: this._target.y },
                maxForce: this._maxForce,
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
            target: { x: this._target.x, y: this._target.y },
            maxForce: this._maxForce,
            stiffness: this._stiffness,
            damping: this._damping,
        };
    }

    deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        // Vec2 fields: Editor writes ARRAY [x,y], engine uses OBJECT {x,y}.
        // normalizeVec2Value accepts both formats for backward compatibility.
        if (data.target !== undefined) {
            const v = this.normalizeVec2Value(data.target);
            this._target.x = v.x;
            this._target.y = v.y;
        }
        this._maxForce = data.maxForce ?? 1000;
        this._stiffness = data.stiffness ?? 5;
        this._damping = data.damping ?? 0.7;
    }
}
