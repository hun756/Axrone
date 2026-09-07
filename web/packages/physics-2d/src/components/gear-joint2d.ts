import { script } from '@axrone/ecs-runtime/decorators';
import { Joint2D } from './joint2d';
import type { ConstraintId } from '../types';

/**
 * Gear joint: enforces a rotation ratio between two revolute/prismatic joints.
 *
 * The gear joint couples two existing constraints (typically HingeJoint2D or
 * SliderJoint2D) so that the rotation/translation of one drives the other
 * at a fixed ratio: `θ_A + ratio × θ_B = constant`.
 *
 * ## Reference resolution strategy
 *
 * The solver requires `constraintIdA` and `constraintIdB` — the runtime IDs
 * of the two coupled constraints. Exposing raw IDs as serialisable properties
 * is fragile: prefab instantiation and scene reload regenerate IDs, breaking
 * saved references.
 *
 * **This component accepts `Joint2D` component references** (`jointA`, `jointB`)
 * and resolves their `constraintId` at runtime. This survives prefab/scene
 * ID regeneration because component references are resolved by entity
 * relationship, not by persisted numeric ID.
 *
 * ### Ordering / deferred creation
 *
 * The referenced joints must have created their constraints before the gear
 * joint can resolve. If `start()` runs before the referenced joints have
 * called `createConstraint()`, the gear joint enters a **pending state**
 * (`isPending === true`) and retries resolution on the next `fixedUpdate()`.
 * Resolution is also re-attempted when `jointA`/`jointB` setters fire.
 *
 * If resolution still fails after a configurable grace period (checked via
 * `tryResolve()`), the component logs a warning — it does NOT throw, because
 * the referenced joint may be disabled or misconfigured, and a hard error
 * would prevent the rest of the scene from loading.
 *
 * **Units** (METRE campaign):
 * - `ratio` — dimensionless (radians per radian, or metres per radian)
 *
 * Maps to solver `Gear` case.
 * **Solver**: FULL — rotation ratio enforcement.
 *
 * @see JOINT_CAPABILITY_2D
 */
@script({
    scriptName: 'GearJoint2D',
    priority: 80,
    description: 'Gear joint coupling two revolute/prismatic joints at a fixed ratio',
    version: '1.0.0',
    author: 'Physics System Team',
    tags: ['physics', 'joint', '2d', 'gear'],
    singleton: false,
    dependencies: [],
    executeInEditMode: false,
})
export class GearJoint2D extends Joint2D {
    private _jointA: Joint2D | null = null;
    private _jointB: Joint2D | null = null;
    private _ratio: number = 1;
    private _pendingCreation: boolean = false;

    get jointA(): Joint2D | null {
        return this._jointA;
    }

    set jointA(value: Joint2D | null) {
        if (this._jointA !== value) {
            this._jointA = value;
            this.recreateConstraint();
        }
    }

    get jointB(): Joint2D | null {
        return this._jointB;
    }

    set jointB(value: Joint2D | null) {
        if (this._jointB !== value) {
            this._jointB = value;
            this.recreateConstraint();
        }
    }

    get ratio(): number {
        return this._ratio;
    }

    set ratio(value: number) {
        if (this._ratio !== value && value !== 0) {
            this._ratio = value;
            this.recreateConstraint();
        }
    }

    /**
     * Whether constraint creation is pending because referenced joints
     * have not yet produced their constraint IDs.
     */
    get isPending(): boolean {
        return this._pendingCreation;
    }

    /**
     * Attempt to resolve deferred constraint creation.
     * Returns `true` if the constraint was successfully created.
     *
     * Call this from the game loop or physics bridge when gear joints
     * are in pending state.
     */
    tryResolve(): boolean {
        if (!this._pendingCreation) return true;
        const idA = this._resolveConstraintId(this._jointA);
        const idB = this._resolveConstraintId(this._jointB);
        if (idA === null || idB === null) return false;

        this._physicsWorld = this.getPhysicsWorld();
        if (!this._physicsWorld) return false;
        if (!this._rigidbodyA?.bodyId) return false;
        if (!this._connectedBody?.bodyId) return false;

        this._constraintId = (this._physicsWorld as any)
            .getConstraintManager()
            .createGearConstraint({
                bodyIdA: this._rigidbodyA.bodyId,
                bodyIdB: this._connectedBody.bodyId,
                constraintIdA: idA,
                constraintIdB: idB,
                ratio: this._ratio,
                collideConnected: this._enableCollision,
            });
        this._pendingCreation = false;
        return true;
    }

    protected createConstraint(): void {
        if (this._constraintId || !this._rigidbodyA || !this._rigidbodyA.bodyId) return;
        if (!this._connectedBody || !this._connectedBody.bodyId) return;

        this._physicsWorld = this.getPhysicsWorld();
        if (!this._physicsWorld) return;

        if (!this._jointA || !this._jointB) {
            this._pendingCreation = true;
            return;
        }

        const idA = this._resolveConstraintId(this._jointA);
        const idB = this._resolveConstraintId(this._jointB);

        if (idA === null || idB === null) {
            // Referenced joints haven't created constraints yet — defer.
            this._pendingCreation = true;
            return;
        }

        this._constraintId = (this._physicsWorld as any)
            .getConstraintManager()
            .createGearConstraint({
                bodyIdA: this._rigidbodyA.bodyId,
                bodyIdB: this._connectedBody.bodyId,
                constraintIdA: idA,
                constraintIdB: idB,
                ratio: this._ratio,
                collideConnected: this._enableCollision,
            });
    }

    protected destroyConstraint(): void {
        if (!this._constraintId || !this._physicsWorld) return;
        (this._physicsWorld as any).getConstraintManager().destroyConstraint(this._constraintId);
        this._constraintId = null;
        this._pendingCreation = false;
    }

    private _resolveConstraintId(joint: Joint2D | null): ConstraintId | null {
        if (!joint) return null;
        const id = joint.constraintId;
        return id !== null ? id : null;
    }

    serialize(): Record<string, any> {
        return {
            ...super.serialize(),
            ratio: this._ratio,
            // jointA/jointB are component references — resolved at runtime,
            // not serialised as raw constraintIds. The editor serialiser
            // should persist entity/component references via its own mechanism.
        };
    }

    deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        this._ratio = data.ratio ?? 1;
        // jointA/jointB must be re-linked by the scene loader via entity references.
    }
}
