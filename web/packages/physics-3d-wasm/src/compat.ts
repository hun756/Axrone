import { WasmPhysicsWorld3D } from "./world";
import type { BodyType, QuatTuple, Vec3Tuple } from "./types";

export interface CompatVec3 {
    readonly x: number;
    readonly y: number;
    readonly z: number;
}

export interface CompatQuat {
    readonly x: number;
    readonly y: number;
    readonly z: number;
    readonly w: number;
}

export interface CompatBodyDef {
    readonly type?: 0 | 1 | 2;
    readonly position?: Partial<CompatVec3>;
    readonly rotation?: Partial<CompatQuat>;
    readonly allowSleep?: boolean;
}

export interface CompatShapeDef {
    readonly center?: Partial<CompatVec3>;
    readonly halfExtents?: Partial<CompatVec3>;
    readonly radius?: number;
}

export interface CompatMaterial {
    readonly friction?: number;
    readonly restitution?: number;
}

export interface CompatWorldConfig {
    readonly gravity?: Partial<CompatVec3>;
    readonly maxBodies?: number;
    readonly maxContacts?: number;
}

export interface CompatBodyHandle {
    resetMassData(): void;
}

export interface CompatStatistics {
    readonly bodyCount: number;
    readonly contactCount: number;
}

function vector(value: Partial<CompatVec3> | undefined, fallback: number): Vec3Tuple {
    return [
        value?.x ?? fallback,
        value?.y ?? fallback,
        value?.z ?? fallback
    ];
}

function quaternion(value: Partial<CompatQuat> | undefined): QuatTuple {
    return [
        value?.x ?? 0,
        value?.y ?? 0,
        value?.z ?? 0,
        value?.w ?? 1
    ];
}

function isZero(value: Partial<CompatVec3> | undefined): boolean {
    return (value?.x ?? 0) === 0 && (value?.y ?? 0) === 0 && (value?.z ?? 0) === 0;
}

export class WasmPhysicsWorld3DCompat {
    private readonly physics: WasmPhysicsWorld3D;
    private readonly shapeByBody = new Map<number, number>();
    private nextShapeId = 1;
    private disposed = false;

    private constructor(physics: WasmPhysicsWorld3D) {
        this.physics = physics;
    }

    static async create(config: CompatWorldConfig = {}): Promise<WasmPhysicsWorld3DCompat> {
        const maxBodies = config.maxBodies ?? 4096;
        const maxContacts = config.maxContacts ?? Math.min(262144, Math.max(1024, maxBodies * 16));
        const gravity: Vec3Tuple = [
            config.gravity?.x ?? 0,
            config.gravity?.y ?? -9.81,
            config.gravity?.z ?? 0
        ];
        const physics = await WasmPhysicsWorld3D.create({
            maxBodies,
            maxContacts,
            gravity
        });
        return new WasmPhysicsWorld3DCompat(physics);
    }

    get bodyCount(): number {
        this.ensureAlive();
        return this.physics.bodyCount;
    }

    get contactCount(): number {
        this.ensureAlive();
        return this.physics.contactCount;
    }

    createBody(def: CompatBodyDef = {}): number {
        this.ensureAlive();
        const type = def.type ?? 2;
        if (type !== 0 && type !== 1 && type !== 2) {
            throw new Error(`unsupported body type: ${type}`);
        }
        const bodyType: BodyType = type === 0 ? "static" : type === 1 ? "kinematic" : "dynamic";
        return this.physics.createBody({
            type: bodyType,
            position: vector(def.position, 0),
            rotation: quaternion(def.rotation)
        });
    }

    destroyBody(bodyId: number): void {
        this.ensureAlive();
        this.physics.destroyBody(bodyId);
        this.shapeByBody.delete(bodyId);
    }

    createBoxShape(bodyId: number, def: CompatShapeDef, material?: CompatMaterial): number {
        this.ensureAlive();
        if (!isZero(def.center)) {
            throw new Error("offset shape centers are not supported by the wasm backend yet");
        }
        if (this.shapeByBody.has(bodyId)) {
            throw new Error(`body ${bodyId} already has a wasm shape; multiple shapes are not supported yet`);
        }
        const extents = def.halfExtents;
        if (extents === undefined) {
            throw new Error("box shape requires halfExtents");
        }
        this.physics.attachBox(bodyId, [extents.x ?? 0, extents.y ?? 0, extents.z ?? 0], 1);
        this.applyMaterial(bodyId, material);
        const shapeId = this.nextShapeId++;
        this.shapeByBody.set(bodyId, shapeId);
        return shapeId;
    }

    createSphereShape(bodyId: number, def: CompatShapeDef, material?: CompatMaterial): number {
        this.ensureAlive();
        if (!isZero(def.center)) {
            throw new Error("offset shape centers are not supported by the wasm backend yet");
        }
        if (this.shapeByBody.has(bodyId)) {
            throw new Error(`body ${bodyId} already has a wasm shape; multiple shapes are not supported yet`);
        }
        if (def.radius === undefined) {
            throw new Error("sphere shape requires radius");
        }
        this.physics.attachSphere(bodyId, def.radius, 1);
        this.applyMaterial(bodyId, material);
        const shapeId = this.nextShapeId++;
        this.shapeByBody.set(bodyId, shapeId);
        return shapeId;
    }

    getBody(bodyId: number): CompatBodyHandle | null {
        this.ensureAlive();
        if (!this.physics.hasBody(bodyId)) {
            return null;
        }
        return {
            resetMassData(): void {
            }
        };
    }

    step(dt: number, velocityIterations = 10, positionIterations = 4): number {
        this.ensureAlive();
        return this.physics.step(dt, { velocityIterations, positionIterations });
    }

    getStatistics(): CompatStatistics {
        this.ensureAlive();
        return {
            bodyCount: this.physics.bodyCount,
            contactCount: this.physics.contactCount
        };
    }

    reset(): void {
        this.ensureAlive();
        this.physics.reset();
        this.shapeByBody.clear();
        this.nextShapeId = 1;
    }

    dispose(): void {
        if (this.disposed) {
            return;
        }
        this.disposed = true;
        this.shapeByBody.clear();
        this.physics.dispose();
    }

    [Symbol.dispose](): void {
        this.dispose();
    }

    private applyMaterial(bodyId: number, material: CompatMaterial | undefined): void {
        if (material === undefined) {
            return;
        }
        this.physics.setMaterial(bodyId, material.friction ?? 0.6, material.restitution ?? 0);
    }

    private ensureAlive(): void {
        if (this.disposed) {
            throw new Error("physics world compat adapter has been disposed");
        }
    }
}
