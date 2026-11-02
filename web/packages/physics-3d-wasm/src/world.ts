import { instantiateWasm, loadEmbeddedWasm, WasmPhysicsExports } from "./loader";
import type {
    BodyOptions,
    BodyType,
    ContactSnapshot,
    MutableQuat,
    MutableVec3,
    QuatTuple,
    StepOptions,
    TransformSnapshot,
    Vec3Tuple,
    WorldOptions
} from "./types";

const DEFAULT_FRICTION = 0.6;
const DEFAULT_RESTITUTION = 0;
const DEFAULT_VELOCITY_ITERATIONS = 8;
const DEFAULT_POSITION_ITERATIONS = 3;
const MAX_BODIES = 262144;
const MAX_CONTACTS = 262144;
const TRANSFORM_STRIDE = 7;
const VEC3_STRIDE = 4;
const CONTACT_BYTES = 24;

const TYPE_CODES: Record<BodyType, number> = {
    static: 0,
    dynamic: 1,
    kinematic: 2
};

function assertFinite(value: number, label: string): void {
    if (!Number.isFinite(value)) {
        throw new Error(`${label} must be finite`);
    }
}

function assertInteger(value: number, label: string): void {
    assertFinite(value, label);
    if (!Number.isInteger(value)) {
        throw new Error(`${label} must be an integer`);
    }
}

function assertVec3(value: Vec3Tuple, label: string): void {
    assertFinite(value[0], `${label}[0]`);
    assertFinite(value[1], `${label}[1]`);
    assertFinite(value[2], `${label}[2]`);
}

function assertQuat(value: QuatTuple, label: string): void {
    assertFinite(value[0], `${label}[0]`);
    assertFinite(value[1], `${label}[1]`);
    assertFinite(value[2], `${label}[2]`);
    assertFinite(value[3], `${label}[3]`);
}

function assertPositive(value: number, label: string): void {
    assertFinite(value, label);
    if (value <= 0) {
        throw new Error(`${label} must be positive`);
    }
}

export class WasmPhysicsWorld3D {
    readonly maxBodies: number;
    readonly maxContacts: number;

    private readonly wasm: WasmPhysicsExports;
    private readonly activeBodies = new Set<number>();
    private disposed = false;
    private viewBuffer: ArrayBufferLike | null = null;
    private transformView: Float32Array | null = null;
    private velocityView: Float32Array | null = null;
    private angularVelocityView: Float32Array | null = null;
    private contactView: DataView | null = null;

    private constructor(wasm: WasmPhysicsExports, maxBodies: number, maxContacts: number) {
        this.wasm = wasm;
        this.maxBodies = maxBodies;
        this.maxContacts = maxContacts;
    }

    static async create(options: WorldOptions): Promise<WasmPhysicsWorld3D> {
        const wasm = await loadEmbeddedWasm();
        return WasmPhysicsWorld3D.initialize(wasm, options);
    }

    static async createFromBytes(bytes: Uint8Array, options: WorldOptions): Promise<WasmPhysicsWorld3D> {
        const wasm = await instantiateWasm(bytes);
        return WasmPhysicsWorld3D.initialize(wasm, options);
    }

    private static initialize(wasm: WasmPhysicsExports, options: WorldOptions): WasmPhysicsWorld3D {
        assertInteger(options.maxBodies, "maxBodies");
        if (options.maxBodies <= 0 || options.maxBodies > MAX_BODIES) {
            throw new Error(`maxBodies must be between 1 and ${MAX_BODIES}`);
        }

        const maxContacts = options.maxContacts ?? Math.min(
            MAX_CONTACTS,
            Math.max(1024, options.maxBodies * 16)
        );
        assertInteger(maxContacts, "maxContacts");
        if (maxContacts <= 0 || maxContacts > MAX_CONTACTS) {
            throw new Error(`maxContacts must be between 1 and ${MAX_CONTACTS}`);
        }

        const gravity = options.gravity ?? [0, -9.81, 0];
        assertVec3(gravity, "gravity");

        const status = wasm.worldInit(
            options.maxBodies,
            maxContacts,
            gravity[0],
            gravity[1],
            gravity[2]
        );
        if (status !== 0) {
            throw new Error("physics world initialization failed");
        }

        return new WasmPhysicsWorld3D(wasm, options.maxBodies, maxContacts);
    }

    createBody(options: BodyOptions): number {
        this.ensureAlive();

        const type = options.type ?? "dynamic";
        const typeCode = TYPE_CODES[type];
        if (typeCode === undefined) {
            throw new Error(`unsupported body type: ${String(type)}`);
        }

        const position = options.position ?? [0, 0, 0];
        const rotation = options.rotation ?? [0, 0, 0, 1];
        assertVec3(position, "position");
        assertQuat(rotation, "rotation");

        if (options.shape !== undefined) {
            if (options.shape.kind === "sphere") {
                assertPositive(options.shape.radius, "shape.radius");
            } else if (options.shape.kind === "box") {
                assertVec3(options.shape.halfExtents, "shape.halfExtents");
                assertPositive(options.shape.halfExtents[0], "shape.halfExtents[0]");
                assertPositive(options.shape.halfExtents[1], "shape.halfExtents[1]");
                assertPositive(options.shape.halfExtents[2], "shape.halfExtents[2]");
            } else {
                throw new Error(`unsupported shape: ${String((options.shape as { kind?: unknown }).kind)}`);
            }
        }

        const density = options.density ?? (type === "static" ? 0 : 1);
        assertFinite(density, "density");
        if (density < 0) {
            throw new Error("density must be non-negative");
        }
        if (type === "dynamic" && density <= 0) {
            throw new Error("dynamic bodies require a positive density");
        }

        const friction = options.friction ?? DEFAULT_FRICTION;
        const restitution = options.restitution ?? DEFAULT_RESTITUTION;
        assertFinite(friction, "friction");
        assertFinite(restitution, "restitution");
        if (friction < 0 || restitution < 0 || restitution > 1) {
            throw new Error("friction must be non-negative and restitution must be between 0 and 1");
        }

        const id = this.wasm.bodyCreate(
            typeCode,
            position[0],
            position[1],
            position[2],
            rotation[0],
            rotation[1],
            rotation[2],
            rotation[3]
        );
        if (id < 0) {
            throw new Error("body creation failed");
        }

        if (options.shape !== undefined) {
            const shapeStatus = options.shape.kind === "sphere"
                ? this.wasm.bodySetSphere(id, options.shape.radius, density)
                : this.wasm.bodySetBox(
                    id,
                    options.shape.halfExtents[0],
                    options.shape.halfExtents[1],
                    options.shape.halfExtents[2],
                    density
                );
            if (shapeStatus !== 0) {
                this.wasm.bodyDestroy(id);
                throw new Error("body shape assignment failed");
            }
        }

        if (this.wasm.bodySetMaterial(id, friction, restitution) !== 0) {
            this.wasm.bodyDestroy(id);
            throw new Error("body material assignment failed");
        }

        this.activeBodies.add(id);
        return id;
    }

    attachSphere(id: number, radius: number, density = 1): void {
        this.ensureAlive();
        this.assertActive(id);
        assertPositive(radius, "radius");
        assertFinite(density, "density");
        if (density < 0) {
            throw new Error("density must be non-negative");
        }
        if (this.wasm.bodySetSphere(id, radius, density) !== 0) {
            throw new Error(`body ${id} sphere attachment failed`);
        }
    }

    attachBox(id: number, halfExtents: Vec3Tuple, density = 1): void {
        this.ensureAlive();
        this.assertActive(id);
        assertVec3(halfExtents, "halfExtents");
        assertPositive(halfExtents[0], "halfExtents[0]");
        assertPositive(halfExtents[1], "halfExtents[1]");
        assertPositive(halfExtents[2], "halfExtents[2]");
        assertFinite(density, "density");
        if (density < 0) {
            throw new Error("density must be non-negative");
        }
        if (this.wasm.bodySetBox(id, halfExtents[0], halfExtents[1], halfExtents[2], density) !== 0) {
            throw new Error(`body ${id} box attachment failed`);
        }
    }

    destroyBody(id: number): void {
        this.ensureAlive();
        this.assertActive(id);
        if (this.wasm.bodyDestroy(id) !== 0) {
            throw new Error(`body ${id} destruction failed`);
        }
        this.activeBodies.delete(id);
    }

    setTransform(id: number, position: Vec3Tuple, rotation?: QuatTuple): void {
        this.ensureAlive();
        this.assertActive(id);
        assertVec3(position, "position");

        const currentRotation = rotation ?? this.readRotation(id);
        assertQuat(currentRotation, "rotation");

        if (this.wasm.bodySetTransform(
            id,
            position[0],
            position[1],
            position[2],
            currentRotation[0],
            currentRotation[1],
            currentRotation[2],
            currentRotation[3]
        ) !== 0) {
            throw new Error(`body ${id} transform failed`);
        }
    }

    setVelocity(id: number, velocity: Vec3Tuple): void {
        this.ensureAlive();
        this.assertActive(id);
        assertVec3(velocity, "velocity");
        if (this.wasm.bodySetVelocity(id, velocity[0], velocity[1], velocity[2]) !== 0) {
            throw new Error(`body ${id} velocity failed`);
        }
    }

    setAngularVelocity(id: number, velocity: Vec3Tuple): void {
        this.ensureAlive();
        this.assertActive(id);
        assertVec3(velocity, "angularVelocity");
        if (this.wasm.bodySetAngularVelocity(id, velocity[0], velocity[1], velocity[2]) !== 0) {
            throw new Error(`body ${id} angular velocity failed`);
        }
    }

    setMaterial(id: number, friction: number, restitution: number): void {
        this.ensureAlive();
        this.assertActive(id);
        assertFinite(friction, "friction");
        assertFinite(restitution, "restitution");
        if (friction < 0 || restitution < 0 || restitution > 1) {
            throw new Error("friction must be non-negative and restitution must be between 0 and 1");
        }
        if (this.wasm.bodySetMaterial(id, friction, restitution) !== 0) {
            throw new Error(`body ${id} material failed`);
        }
    }

    setGravity(gravity: Vec3Tuple): void {
        this.ensureAlive();
        assertVec3(gravity, "gravity");
        if (this.wasm.worldSetGravity(gravity[0], gravity[1], gravity[2]) !== 0) {
            throw new Error("gravity update failed");
        }
    }

    step(dt: number, options: StepOptions = {}): number {
        this.ensureAlive();
        assertFinite(dt, "dt");
        if (dt <= 0) {
            throw new Error("dt must be positive");
        }

        const velocityIterations = options.velocityIterations ?? DEFAULT_VELOCITY_ITERATIONS;
        const positionIterations = options.positionIterations ?? DEFAULT_POSITION_ITERATIONS;
        assertInteger(velocityIterations, "velocityIterations");
        assertInteger(positionIterations, "positionIterations");
        if (velocityIterations < 0 || positionIterations < 0) {
            throw new Error("solver iterations must be non-negative");
        }

        const contacts = this.wasm.step(dt, velocityIterations, positionIterations);
        if (contacts < 0) {
            throw new Error("physics step failed");
        }
        return contacts;
    }

    reset(): void {
        this.ensureAlive();
        this.wasm.worldReset();
        this.activeBodies.clear();
    }

    hasBody(id: number): boolean {
        return this.activeBodies.has(id);
    }

    get bodyCount(): number {
        this.ensureAlive();
        return this.wasm.bodyCount();
    }

    get contactCount(): number {
        this.ensureAlive();
        return this.wasm.contactCount();
    }

    get memoryBytes(): number {
        this.ensureAlive();
        return this.wasm.worldMemoryBytes();
    }

    fingerprint(): string {
        this.ensureAlive();
        return BigInt.asUintN(64, this.wasm.worldFingerprint()).toString(16).padStart(16, "0");
    }

    getTransformsBuffer(): Float32Array {
        this.ensureAlive();
        this.refreshViews();
        if (this.transformView === null) {
            const pointer = this.wasm.transformsPtr();
            if (pointer === 0) {
                throw new Error("transform buffer is unavailable");
            }
            this.transformView = new Float32Array(this.wasm.memory.buffer, pointer, this.maxBodies * TRANSFORM_STRIDE);
        }
        return this.transformView;
    }

    getVelocitiesBuffer(): Float32Array {
        this.ensureAlive();
        this.refreshViews();
        if (this.velocityView === null) {
            const pointer = this.wasm.velocitiesPtr();
            if (pointer === 0) {
                throw new Error("velocity buffer is unavailable");
            }
            this.velocityView = new Float32Array(this.wasm.memory.buffer, pointer, this.maxBodies * VEC3_STRIDE);
        }
        return this.velocityView;
    }

    getAngularVelocitiesBuffer(): Float32Array {
        this.ensureAlive();
        this.refreshViews();
        if (this.angularVelocityView === null) {
            const pointer = this.wasm.angularVelocitiesPtr();
            if (pointer === 0) {
                throw new Error("angular velocity buffer is unavailable");
            }
            this.angularVelocityView = new Float32Array(this.wasm.memory.buffer, pointer, this.maxBodies * VEC3_STRIDE);
        }
        return this.angularVelocityView;
    }

    getContactsView(): DataView {
        this.ensureAlive();
        this.refreshViews();
        if (this.contactView === null) {
            const pointer = this.wasm.contactsPtr();
            if (pointer === 0) {
                throw new Error("contact buffer is unavailable");
            }
            this.contactView = new DataView(this.wasm.memory.buffer, pointer, this.maxContacts * CONTACT_BYTES);
        }
        return this.contactView;
    }

    copyTransform(id: number, output: Float32Array, offset = 0): void {
        this.ensureAlive();
        this.assertActive(id);
        if (offset < 0 || offset + TRANSFORM_STRIDE > output.length) {
            throw new Error("transform output buffer is too small");
        }
        const source = (id * TRANSFORM_STRIDE);
        const transforms = this.getTransformsBuffer();
        for (let index = 0; index < TRANSFORM_STRIDE; index += 1) {
            output[offset + index] = transforms[source + index];
        }
    }

    copyVelocity(id: number, output: Float32Array, offset = 0): void {
        this.ensureAlive();
        this.assertActive(id);
        if (offset < 0 || offset + 3 > output.length) {
            throw new Error("velocity output buffer is too small");
        }
        const source = id * VEC3_STRIDE;
        const velocities = this.getVelocitiesBuffer();
        output[offset] = velocities[source];
        output[offset + 1] = velocities[source + 1];
        output[offset + 2] = velocities[source + 2];
    }

    copyAngularVelocity(id: number, output: Float32Array, offset = 0): void {
        this.ensureAlive();
        this.assertActive(id);
        if (offset < 0 || offset + 3 > output.length) {
            throw new Error("angular velocity output buffer is too small");
        }
        const source = id * VEC3_STRIDE;
        const velocities = this.getAngularVelocitiesBuffer();
        output[offset] = velocities[source];
        output[offset + 1] = velocities[source + 1];
        output[offset + 2] = velocities[source + 2];
    }

    readTransform(id: number): TransformSnapshot {
        const values = new Float32Array(TRANSFORM_STRIDE);
        this.copyTransform(id, values);
        return {
            position: [values[0], values[1], values[2]],
            rotation: [values[3], values[4], values[5], values[6]]
        };
    }

    readVelocity(id: number): MutableVec3 {
        const values = new Float32Array(3);
        this.copyVelocity(id, values);
        return [values[0], values[1], values[2]];
    }

    readAngularVelocity(id: number): MutableVec3 {
        const values = new Float32Array(3);
        this.copyAngularVelocity(id, values);
        return [values[0], values[1], values[2]];
    }

    readContact(index: number): ContactSnapshot {
        this.ensureAlive();
        if (index < 0 || index >= this.contactCount) {
            throw new Error(`contact index ${index} is out of range`);
        }
        const view = this.getContactsView();
        const offset = index * CONTACT_BYTES;
        return {
            a: view.getInt32(offset, true),
            b: view.getInt32(offset + 4, true),
            normal: [
                view.getFloat32(offset + 8, true),
                view.getFloat32(offset + 12, true),
                view.getFloat32(offset + 16, true)
            ],
            penetration: view.getFloat32(offset + 20, true)
        };
    }

    readContacts(): ContactSnapshot[] {
        const count = this.contactCount;
        const contacts: ContactSnapshot[] = new Array(count);
        for (let index = 0; index < count; index += 1) {
            contacts[index] = this.readContact(index);
        }
        return contacts;
    }

    dispose(): void {
        this.disposed = true;
        this.activeBodies.clear();
        this.transformView = null;
        this.velocityView = null;
        this.angularVelocityView = null;
        this.contactView = null;
    }

    private readRotation(id: number): MutableQuat {
        const values = new Float32Array(TRANSFORM_STRIDE);
        this.copyTransform(id, values);
        return [values[3], values[4], values[5], values[6]];
    }

    private refreshViews(): void {
        const buffer = this.wasm.memory.buffer;
        if (buffer !== this.viewBuffer) {
            this.viewBuffer = buffer;
            this.transformView = null;
            this.velocityView = null;
            this.angularVelocityView = null;
            this.contactView = null;
        }
    }

    private ensureAlive(): void {
        if (this.disposed) {
            throw new Error("physics world has been disposed");
        }
    }

    private assertActive(id: number): void {
        if (!Number.isInteger(id) || !this.activeBodies.has(id)) {
            throw new Error(`body ${id} is not active`);
        }
    }
}
