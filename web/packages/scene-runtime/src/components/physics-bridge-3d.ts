import type { GameLoopSystem, FixedUpdateContext, BeforeUpdateContext } from '@axrone/game-loop';
import type { World, ComponentRegistry } from '@axrone/ecs-runtime';
import type { SceneLoopState } from '../types';
import {
    PhysicsWorld3D,
    Rigidbody3D,
    Collider3D,
    Joint3D,
} from '@axrone/physics-3d';
import type {
    IContactListener3D,
    IContactManifold3D,
    BodyId3D,
    IPhysicsWorld3DConfig,
} from '@axrone/physics-core';

type AnyWorld = World<ComponentRegistry>;

interface ContactPair {
    readonly bodyIdA: BodyId3D;
    readonly bodyIdB: BodyId3D;
}

function makePairKey(a: BodyId3D, b: BodyId3D): string {
    return a < b ? `${a}:${b}` : `${b}:${a}`;
}

export interface PhysicsBridge3DOptions {
    readonly worldConfig?: IPhysicsWorld3DConfig;
    readonly velocityIterations?: number;
    readonly positionIterations?: number;
}

export class PhysicsBridge3D implements GameLoopSystem<SceneLoopState>, IContactListener3D {
    readonly id = 'scene.physics-bridge-3d';

    private readonly _ecsWorld: AnyWorld;
    private readonly _physicsWorld: PhysicsWorld3D;
    private readonly _velocityIterations: number;
    private readonly _positionIterations: number;

    private readonly _initializedBodies = new WeakSet<Rigidbody3D>();
    private readonly _initializedColliders = new WeakSet<Collider3D>();
    private readonly _initializedJoints = new WeakSet<Joint3D>();
    private readonly _bodyIdToComponent = new Map<BodyId3D, Rigidbody3D>();
    private readonly _activeContactPairs = new Set<string>();
    private readonly _activeTriggerPairs = new Set<string>();

    private _disposed = false;

    constructor(ecsWorld: AnyWorld, options: PhysicsBridge3DOptions = {}) {
        this._ecsWorld = ecsWorld;
        this._physicsWorld = new PhysicsWorld3D(options.worldConfig);
        this._velocityIterations = options.velocityIterations ?? 10;
        this._positionIterations = options.positionIterations ?? 4;
        this._physicsWorld.setContactListener(this);
    }

    get physicsWorld(): PhysicsWorld3D {
        return this._physicsWorld;
    }

    get isDisposed(): boolean {
        return this._disposed;
    }

    beforeUpdate(_context: BeforeUpdateContext<SceneLoopState>): void {
        if (this._disposed) return;
        this._initializeNewComponents();
    }

    fixedUpdate(context: FixedUpdateContext<SceneLoopState>): void {
        if (this._disposed) return;

        this._physicsWorld.step(
            context.fixedDelta,
            this._velocityIterations,
            this._positionIterations
        );

        this._syncAllTransforms();
    }

    onCollisionBegin(manifold: IContactManifold3D): void {
        const bodyIdA = manifold.bodyIdA as BodyId3D;
        const bodyIdB = manifold.bodyIdB as BodyId3D;
        const key = makePairKey(bodyIdA, bodyIdB);
        this._activeContactPairs.add(key);

        const componentA = this._bodyIdToComponent.get(bodyIdA);
        const componentB = this._bodyIdToComponent.get(bodyIdB);
        if (componentA && componentB) {
            this._dispatchCollisionEvent(componentA, componentB, manifold, 'onCollisionBegin');
        }
    }

    onCollisionStay(manifold: IContactManifold3D): void {
        const bodyIdA = manifold.bodyIdA as BodyId3D;
        const bodyIdB = manifold.bodyIdB as BodyId3D;

        const componentA = this._bodyIdToComponent.get(bodyIdA);
        const componentB = this._bodyIdToComponent.get(bodyIdB);
        if (componentA && componentB) {
            this._dispatchCollisionEvent(componentA, componentB, manifold, 'onCollisionStay');
        }
    }

    onCollisionEnd(bodyIdA: BodyId3D, bodyIdB: BodyId3D): void {
        const key = makePairKey(bodyIdA, bodyIdB);
        this._activeContactPairs.delete(key);

        const componentA = this._bodyIdToComponent.get(bodyIdA);
        const componentB = this._bodyIdToComponent.get(bodyIdB);
        if (componentA && componentB) {
            this._dispatchCollisionEndEvent(componentA, componentB);
        }
    }

    onTriggerEnter(bodyIdA: BodyId3D, bodyIdB: BodyId3D): void {
        const key = makePairKey(bodyIdA, bodyIdB);
        this._activeTriggerPairs.add(key);

        const componentA = this._bodyIdToComponent.get(bodyIdA);
        const componentB = this._bodyIdToComponent.get(bodyIdB);
        if (componentA && componentB) {
            this._dispatchTriggerEvent(componentA, componentB, 'onTriggerEnter');
        }
    }

    onTriggerExit(bodyIdA: BodyId3D, bodyIdB: BodyId3D): void {
        const key = makePairKey(bodyIdA, bodyIdB);
        this._activeTriggerPairs.delete(key);

        const componentA = this._bodyIdToComponent.get(bodyIdA);
        const componentB = this._bodyIdToComponent.get(bodyIdB);
        if (componentA && componentB) {
            this._dispatchTriggerEvent(componentA, componentB, 'onTriggerExit');
        }
    }

    dispose(): void {
        if (this._disposed) return;
        this._disposed = true;
        this._physicsWorld.setContactListener(null);
        this._bodyIdToComponent.clear();
        this._activeContactPairs.clear();
        this._activeTriggerPairs.clear();
        this._physicsWorld[Symbol.dispose]();
    }

    private _initializeNewComponents(): void {
        const actors = this._ecsWorld.getAllActors();

        for (const actor of actors) {
            if (!actor.active || actor.isDestroyed) continue;

            const rigidbody = actor.getComponent(Rigidbody3D);
            if (rigidbody && !this._initializedBodies.has(rigidbody)) {
                rigidbody.initialize(this._physicsWorld);
                this._initializedBodies.add(rigidbody);
                this._bodyIdToComponent.set(rigidbody.bodyId, rigidbody);
            }

            const colliders = this._getCollidersFromActor(actor);
            for (const collider of colliders) {
                if (this._initializedColliders.has(collider)) continue;
                collider.initialize(this._physicsWorld, rigidbody ?? undefined);
                this._initializedColliders.add(collider);
            }

            const joints = this._getJointsFromActor(actor);
            for (const joint of joints) {
                if (this._initializedJoints.has(joint)) continue;
                if (!rigidbody) continue;

                const connectedBody = this._resolveConnectedBody(joint);
                joint.initialize(this._physicsWorld, rigidbody, connectedBody ?? undefined);
                this._initializedJoints.add(joint);
            }
        }
    }

    private _getCollidersFromActor(actor: ReturnType<AnyWorld['getAllActors']>[number]): Collider3D[] {
        const result: Collider3D[] = [];
        for (const component of actor.getAllComponents()) {
            if (component instanceof Collider3D) {
                result.push(component);
            }
        }
        return result;
    }

    private _getJointsFromActor(actor: ReturnType<AnyWorld['getAllActors']>[number]): Joint3D[] {
        const result: Joint3D[] = [];
        for (const component of actor.getAllComponents()) {
            if (component instanceof Joint3D) {
                result.push(component);
            }
        }
        return result;
    }

    private _resolveConnectedBody(joint: Joint3D): Rigidbody3D | null {
        const connectedBody = joint.connectedBody;
        if (connectedBody && this._initializedBodies.has(connectedBody)) {
            return connectedBody;
        }
        return null;
    }

    private _syncAllTransforms(): void {
        const actors = this._ecsWorld.getAllActors();
        for (const actor of actors) {
            if (!actor.active || actor.isDestroyed) continue;
            const rigidbody = actor.getComponent(Rigidbody3D);
            if (rigidbody && this._initializedBodies.has(rigidbody)) {
                rigidbody.syncTransformFromWorld();
            }
        }
    }

    private _dispatchCollisionEvent(
        _self: Rigidbody3D,
        _other: Rigidbody3D,
        _manifold: IContactManifold3D,
        _event: 'onCollisionBegin' | 'onCollisionStay'
    ): void {
        // Collision callbacks will be dispatched to user scripts via
        // the component event system once @script collision handlers
        // are wired (onCollisionEnter/Stay/Exit on user components).
    }

    private _dispatchCollisionEndEvent(
        _self: Rigidbody3D,
        _other: Rigidbody3D
    ): void {
        // See _dispatchCollisionEvent — user script collision handlers
        // will be wired in a follow-up.
    }

    private _dispatchTriggerEvent(
        _self: Rigidbody3D,
        _other: Rigidbody3D,
        _event: 'onTriggerEnter' | 'onTriggerExit'
    ): void {
        // Trigger callbacks will be dispatched to user scripts via
        // the component event system once @script trigger handlers
        // are wired (onTriggerEnter/Exit on user components).
    }
}

export function createPhysicsBridge3DSystems(
    ecsWorld: AnyWorld,
    options?: PhysicsBridge3DOptions
): { bridge: PhysicsBridge3D; system: GameLoopSystem<SceneLoopState> } {
    const bridge = new PhysicsBridge3D(ecsWorld, options);
    return {
        bridge,
        system: {
            id: bridge.id,
            beforeUpdate: (ctx) => bridge.beforeUpdate(ctx),
            fixedUpdate: (ctx) => bridge.fixedUpdate(ctx),
            dispose: () => bridge.dispose(),
        },
    };
}
