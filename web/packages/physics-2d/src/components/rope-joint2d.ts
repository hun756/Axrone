import { script } from '@axrone/ecs-runtime/decorators';
import { Vec2 } from '@axrone/numeric';
import { Joint2D } from './joint2d';

/**
 * Rope joint: enforces a maximum distance between two anchor points.
 *
 * Unlike a distance joint (which maintains an exact distance), the rope joint
 * is **unilateral** — it only constrains when the distance exceeds `maxLength`.
 * Bodies are free to move closer but cannot move farther apart.
 *
 * Typical use: chains, leashes, cables, cloth edges.
 *
 * **Units** (METRE campaign):
 * - `anchorA`, `anchorB` — metres (local space)
 * - `maxLength` — metres
 *
 * Maps to solver `Rope` case.
 * **Solver**: FULL — unilateral max-length guard with position correction.
 *
 * @see JOINT_CAPABILITY_2D
 */
@script({
    scriptName: 'RopeJoint2D',
    priority: 80,
    description: 'Rope constraint enforcing maximum distance between two points',
    version: '1.0.0',
    author: 'Physics System Team',
    tags: ['physics', 'joint', '2d', 'rope'],
    singleton: false,
    dependencies: [],
    executeInEditMode: false,
})
export class RopeJoint2D extends Joint2D {
    private _anchorA: Vec2 = Vec2.ZERO.clone();
    private _anchorB: Vec2 = Vec2.ZERO.clone();
    private _maxLength: number = 1;

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

    get maxLength(): number {
        return this._maxLength;
    }

    set maxLength(value: number) {
        if (this._maxLength !== value && value > 0) {
            this._maxLength = value;
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
            .createRopeConstraint({
                bodyIdA: this._rigidbodyA.bodyId,
                bodyIdB: this._connectedBody.bodyId,
                localAnchorA: { x: this._anchorA.x, y: this._anchorA.y },
                localAnchorB: { x: this._anchorB.x, y: this._anchorB.y },
                maxLength: this._maxLength,
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
            maxLength: this._maxLength,
        };
    }

    deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        // Vec2 fields: Editor writes ARRAY [x,y], engine uses OBJECT {x,y}.
        // normalizeVec2Value accepts both formats for backward compatibility.
        if (data.anchorA !== undefined) {
            const v = this.normalizeVec2Value(data.anchorA);
            this._anchorA.x = v.x;
            this._anchorA.y = v.y;
        }
        if (data.anchorB !== undefined) {
            const v = this.normalizeVec2Value(data.anchorB);
            this._anchorB.x = v.x;
            this._anchorB.y = v.y;
        }
        this._maxLength = data.maxLength ?? 1;
    }
}
