import { Component } from '@axrone/ecs-runtime';
import type { ConstraintId } from '../types';
import type { PhysicsWorld2D } from '../core/physics-world';
import { Rigidbody2D } from './rigidbody2d';
import { PhysicsWorld2DComponent } from './physics-world-2d-component';

export abstract class Joint2D extends Component {
    protected _constraintId: ConstraintId | null = null;
    protected _physicsWorld: PhysicsWorld2D | null = null;
    protected _rigidbodyA: Rigidbody2D | null = null;
    protected _rigidbodyB: Rigidbody2D | null = null;

    protected _connectedBody: Rigidbody2D | null = null;
    protected _enableCollision: boolean = false;
    protected _breakForce: number = Infinity;
    protected _breakTorque: number = Infinity;
    protected _jointEnabled: boolean = true;

    get constraintId(): ConstraintId | null {
        return this._constraintId;
    }

    get connectedBody(): Rigidbody2D | null {
        return this._connectedBody;
    }

    set connectedBody(value: Rigidbody2D | null) {
        if (this._connectedBody !== value) {
            this._connectedBody = value;
            this.recreateConstraint();
        }
    }

    get enableCollision(): boolean {
        return this._enableCollision;
    }

    set enableCollision(value: boolean) {
        if (this._enableCollision !== value) {
            this._enableCollision = value;
            this.recreateConstraint();
        }
    }

    get breakForce(): number {
        return this._breakForce;
    }

    set breakForce(value: number) {
        this._breakForce = Math.max(0, value);
    }

    get breakTorque(): number {
        return this._breakTorque;
    }

    set breakTorque(value: number) {
        this._breakTorque = Math.max(0, value);
    }

    awake(): void {
        this._rigidbodyA = (this.getComponent(Rigidbody2D as any) as Rigidbody2D | null) ?? null;
        if (!this._rigidbodyA) {
            throw new Error('Joint2D requires Rigidbody2D component');
        }
    }

    start(): void {
        this.createConstraint();
    }

    onDestroy(): void {
        this.destroyConstraint();
    }

    protected abstract createConstraint(): void;
    protected abstract destroyConstraint(): void;

    protected recreateConstraint(): void {
        this.destroyConstraint();
        this.createConstraint();
    }

    protected getPhysicsWorld(): PhysicsWorld2D | null {
        if (this._physicsWorld) return this._physicsWorld;
        const worldComponent = PhysicsWorld2DComponent.instance;
        if (worldComponent?.physicsWorld) {
            this._physicsWorld = worldComponent.physicsWorld;
            return this._physicsWorld;
        }
        return null;
    }

    serialize(): Record<string, any> {
        return {
            enableCollision: this._enableCollision,
            breakForce: this._breakForce,
            breakTorque: this._breakTorque,
            enabled: this._jointEnabled,
        };
    }

    deserialize(data: Record<string, any>): void {
        this._enableCollision = data.enableCollision ?? false;
        this._breakForce = data.breakForce ?? Infinity;
        this._breakTorque = data.breakTorque ?? Infinity;
        this._jointEnabled = data.enabled ?? true;
    }
}
