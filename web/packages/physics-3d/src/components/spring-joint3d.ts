import { script } from '@axrone/ecs-runtime/decorators';
import type { ISpringConstraintDef3D } from '../types';
import type { Rigidbody3D } from './rigidbody3d';
import { Joint3D } from './joint3d';

@script({ scriptName: 'SpringJoint3D' })
export class SpringJoint3D extends Joint3D {
    private _minDistance: number = 0;
    private _maxDistance: number = 0;
    private _spring: number = 0;
    private _damper: number = 0;
    private _tolerance: number = 0.025;
    private _autoConfigureDistance: boolean = true;

    get minDistance(): number {
        return this._minDistance;
    }
    set minDistance(value: number) {
        this._minDistance = Math.max(0, value);
        this._updateConstraint();
    }
    get maxDistance(): number {
        return this._maxDistance;
    }
    set maxDistance(value: number) {
        this._maxDistance = Math.max(0, value);
        this._updateConstraint();
    }
    get springValue(): number {
        return this._spring;
    }
    set springValue(value: number) {
        this._spring = Math.max(0, value);
        this._updateConstraint();
    }
    get damper(): number {
        return this._damper;
    }
    set damper(value: number) {
        this._damper = Math.max(0, value);
        this._updateConstraint();
    }
    get tolerance(): number {
        return this._tolerance;
    }
    set tolerance(value: number) {
        this._tolerance = Math.max(0, value);
    }
    get autoConfigureDistance(): boolean {
        return this._autoConfigureDistance;
    }
    set autoConfigureDistance(value: boolean) {
        this._autoConfigureDistance = value;
        if (value) this._configureDistance();
    }

    protected override _createConstraint(ownerBody: Rigidbody3D): void {
        if (!this._constraintManager || !this._connectedBody) return;
        const def: ISpringConstraintDef3D = {
            bodyIdA: ownerBody.bodyId,
            bodyIdB: this._connectedBody.bodyId,
            localAnchorA: this._anchor,
            localAnchorB: this._connectedAnchor,
            restLength: (this._minDistance + this._maxDistance) * 0.5,
            stiffness: this._spring,
            damping: this._damper,
            collideConnected: this._enableCollision,
        };
        this._constraintId = this._constraintManager.createSpring(def);
    }
    protected override _updateConstraint(): void {
        this._recreateConstraint();
    }

    private _configureDistance(): void {
        if (!this._connectedBody || !this.transform) return;
        const worldAnchor = this._getWorldAnchor();
        const connectedWorldAnchor = this._transformPoint(
            this._connectedBody.position,
            this._connectedBody.rotation,
            this._connectedAnchor
        );
        const dx = worldAnchor.x - connectedWorldAnchor.x;
        const dy = worldAnchor.y - connectedWorldAnchor.y;
        const dz = worldAnchor.z - connectedWorldAnchor.z;
        const dist = Math.sqrt(dx * dx + dy * dy + dz * dz);
        this._minDistance = dist;
        this._maxDistance = dist;
    }
}
