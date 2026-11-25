import { script } from '@axrone/ecs-runtime/decorators';
import type { Density, Friction, Restitution } from '@axrone/physics-core';
import { Collider2D } from './collider2d';
import { finiteOr } from '../core/physics-world-2d-helpers';

@script({
    scriptName: 'CapsuleCollider2D',
    priority: 90,
    description: 'Capsule collider for 2D physics',
    version: '1.0.0',
    author: 'Physics System Team',
    tags: ['physics', 'collider', '2d', 'capsule'],
    singleton: false,
    dependencies: [],
    executeInEditMode: false,
})
export class CapsuleCollider2D extends Collider2D {
    private _length: number = 1.0;
    private _radius: number = 0.25;
    private _direction: 'vertical' | 'horizontal' = 'vertical';

    get length(): number {
        return this._length;
    }

    set length(value: number) {
        if (this._length !== value && value > 0) {
            this._length = value;
            this.recreateShape();
        }
    }

    get radius(): number {
        return this._radius;
    }

    set radius(value: number) {
        if (this._radius !== value && value > 0) {
            this._radius = value;
            this.recreateShape();
        }
    }

    get direction(): 'vertical' | 'horizontal' {
        return this._direction;
    }

    set direction(value: 'vertical' | 'horizontal') {
        if (this._direction !== value) {
            this._direction = value;
            this.recreateShape();
        }
    }

    protected createPhysicsShape(): void {
        if (this._shapeId || !this._rigidbody || !this._rigidbody.bodyId) return;

        this._physicsWorld = this.getPhysicsWorld();
        if (!this._physicsWorld) return;

        this._shapeId = this._physicsWorld.createCapsuleShape(this._rigidbody.bodyId, {
            center: { x: this._offset.x, y: this._offset.y },
            length: this._length,
            radius: this._radius,
            rotation: this._direction === 'horizontal' ? Math.PI * 0.5 : 0,
            material: {
                friction: this._material.friction as Friction,
                restitution: this._material.restitution as Restitution,
                density: this._material.density as Density,
            },
            isSensor: this._isTrigger,
            filter: this._collisionFilter,
        });
    }

    protected destroyPhysicsShape(): void {
        if (!this._shapeId || !this._physicsWorld) return;
        this._physicsWorld.destroyShape(this._shapeId);
        this._shapeId = null;
    }

    protected updateTrigger(): void {
        this.recreateShape();
    }
    protected updateMaterial(): void {
        this.recreateShape();
    }
    protected updateOffset(): void {
        this.recreateShape();
    }
    protected updateCollisionFilter(): void {
        this.recreateShape();
    }

    private recreateShape(): void {
        this.destroyPhysicsShape();
        this.createPhysicsShape();
    }

    serialize(): Record<string, any> {
        return {
            ...super.serialize(),
            length: this._length,
            radius: this._radius,
            direction: this._direction,
        };
    }

    deserialize(data: Record<string, any>): void {
        super.deserialize(data);
        this._length = finiteOr(data.length, 1.0);
        this._radius = finiteOr(data.radius, 0.25);
        this._direction = data.direction ?? 'vertical';
    }
}
