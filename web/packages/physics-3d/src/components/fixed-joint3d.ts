import { script } from '@axrone/ecs-runtime/decorators';
import type { IFixedConstraintDef3D } from '../types';
import type { Rigidbody3D } from './rigidbody3d';
import { Joint3D } from './joint3d';

@script({ scriptName: 'FixedJoint3D' })
export class FixedJoint3D extends Joint3D {
    protected override _createConstraint(ownerBody: Rigidbody3D): void {
        if (!this._constraintManager || !this._connectedBody) return;
        const def: IFixedConstraintDef3D = {
            bodyIdA: ownerBody.bodyId,
            bodyIdB: this._connectedBody.bodyId,
            localAnchorA: this._anchor,
            localAnchorB: this._connectedAnchor,
            collideConnected: this._enableCollision,
        };
        this._constraintId = this._constraintManager.createFixed(def);
    }
    protected override _updateConstraint(): void {
        this._recreateConstraint();
    }
}
