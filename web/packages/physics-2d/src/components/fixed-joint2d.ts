import { script } from '@axrone/ecs-runtime/decorators';
import { Vec2 } from '@axrone/numeric';
import { Joint2D } from './joint2d';

@script({
    scriptName: 'FixedJoint2D',
    priority: 80,
    description: 'Fixed/weld joint for rigid attachment',
    version: '1.0.0',
    author: 'Physics System Team',
    tags: ['physics', 'joint', '2d', 'fixed', 'weld'],
    singleton: false,
    dependencies: [],
    executeInEditMode: false,
})
export class FixedJoint2D extends Joint2D {
    private _anchor: Vec2 = Vec2.ZERO.clone();
    private _dampingRatio: number = 0.7;
    private _frequency: number = 0;

    get anchor(): Vec2 {
        return this._anchor;
    }

    set anchor(value: Vec2) {
        this._anchor.x = value.x;
        this._anchor.y = value.y;
        this.recreateConstraint();
    }

    get dampingRatio(): number {
        return this._dampingRatio;
    }

    set dampingRatio(value: number) {
        if (this._dampingRatio !== value && value >= 0 && value <= 1) {
            this._dampingRatio = value;
            this.recreateConstraint();
        }
    }

    get frequency(): number {
        return this._frequency;
    }

    set frequency(value: number) {
        if (this._frequency !== value && value >= 0) {
            this._frequency = value;
            this.recreateConstraint();
        }
    }

    protected createConstraint(): void {
        if (this._constraintId || !this._rigidbodyA || !this._rigidbodyA.bodyId) return;
        if (!this._connectedBody || !this._connectedBody.bodyId) return;

        this._physicsWorld = this.getPhysicsWorld();
        if (!this._physicsWorld) return;

        const stiffness = this._frequency > 0 ? this._frequency * this._frequency : 0;
        const damping = 2 * this._dampingRatio * Math.sqrt(stiffness);

        this._constraintId = (this._physicsWorld as any)
            .getConstraintManager()
            .createWeldConstraint({
                bodyIdA: this._rigidbodyA.bodyId,
                bodyIdB: this._connectedBody.bodyId,
                localAnchorA: { x: this._anchor.x, y: this._anchor.y },
                localAnchorB: { x: 0, y: 0 },
                referenceAngle: 0,
                stiffness,
                damping,
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
            anchor: { x: this._anchor.x, y: this._anchor.y },
            dampingRatio: this._dampingRatio,
            frequency: this._frequency,
        };
    }

    deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        this._anchor = new Vec2(data.anchor?.x ?? 0, data.anchor?.y ?? 0);
        this._dampingRatio = data.dampingRatio ?? 0.7;
        this._frequency = data.frequency ?? 0;
    }
}
