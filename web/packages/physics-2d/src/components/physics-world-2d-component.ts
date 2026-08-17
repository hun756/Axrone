import { Component } from '@axrone/ecs-runtime';
import { script } from '@axrone/ecs-runtime/decorators';
import { Vec2 } from '@axrone/numeric';
import { PhysicsWorld2D } from '../core/physics-world';
import type { IPhysicsWorldConfig } from '../types';

@script({
    scriptName: 'PhysicsWorld2D',
    priority: 50,
    description: 'Singleton component that owns and steps the 2D physics world',
    version: '1.0.0',
    author: 'Physics System Team',
    tags: ['physics', 'world', '2d'],
    singleton: true,
    dependencies: [],
    executeInEditMode: false,
    validateDependencies: false,
    enableMetrics: false,
    enableCaching: false,
})
export class PhysicsWorld2DComponent extends Component {
    private _physicsWorld: PhysicsWorld2D | null = null;
    private _gravity: Vec2 = new Vec2(0, -9.81);
    private _velocityIterations: number = 8;
    private _positionIterations: number = 3;

    get physicsWorld(): PhysicsWorld2D | null {
        return this._physicsWorld;
    }

    get gravity(): Readonly<Vec2> {
        return this._gravity;
    }

    set gravity(value: Readonly<Vec2>) {
        this._gravity.x = value.x;
        this._gravity.y = value.y;
        if (this._physicsWorld) {
            this._physicsWorld.setGravity(this._gravity);
        }
    }

    awake(): void {
        const config: IPhysicsWorldConfig = {
            gravity: { x: this._gravity.x, y: this._gravity.y },
        };
        this._physicsWorld = new PhysicsWorld2D(config);
    }

    fixedUpdate(deltaTime: number): void {
        if (!this._physicsWorld) return;
        this._physicsWorld.step(deltaTime, this._velocityIterations, this._positionIterations);
    }

    onDestroy(): void {
        if (this._physicsWorld) {
            this._physicsWorld[Symbol.dispose]();
            this._physicsWorld = null;
        }
    }

    serialize(): Record<string, any> {
        return {
            gravity: { x: this._gravity.x, y: this._gravity.y },
            velocityIterations: this._velocityIterations,
            positionIterations: this._positionIterations,
        };
    }

    deserialize(data: Record<string, any>): void {
        if (data.gravity) {
            this._gravity.x = data.gravity.x ?? 0;
            this._gravity.y = data.gravity.y ?? -9.81;
        }
        this._velocityIterations = data.velocityIterations ?? 8;
        this._positionIterations = data.positionIterations ?? 3;
    }
}
