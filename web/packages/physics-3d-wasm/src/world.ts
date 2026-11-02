import { instantiateWasm, loadEmbeddedWasm, WasmPhysicsExports } from "./loader";
import type {
    BodyOptions,
    BodyType,
    CapsuleAttachOptions,
    ContactEventKind,
    ContactEventSnapshot,
    ContactSnapshot,
    CylinderAttachOptions,
    ConeAttachOptions,
    FixedJointOptions,
    HingeJointOptions,
    MutableQuat,
    MutableVec3,
    QuatTuple,
    RaycastHit,
    ShapeFilter,
    ShapeMaterial,
    SliderJointOptions,
    SpringJointOptions,
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
const EVENT_RECORD_STRIDE = 10;
const EVENT_CAPACITY = 4096;
const RAY_HIT_STRIDE = 10;
const DEFAULT_QUERY_CAPACITY = 256;
const SHAPE_ID_MAX = 0x7fffffff;
const SHAPE_MATERIAL_INHERIT = -1;
const INT32_MIN = -2147483648;
const INT32_MAX = 2147483647;

const CONTACT_EVENT_KINDS: readonly ContactEventKind[] = [
    "begin",
    "stay",
    "end",
    "sensorEnter",
    "sensorStay",
    "sensorExit"
];

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

function assertNonNegative(value: number, label: string): void {
    assertFinite(value, label);
    if (value < 0) {
        throw new Error(`${label} must be non-negative`);
    }
}

function assertRayDistance(value: number): void {
    if (Number.isNaN(value) || value <= 0) {
        throw new Error("maxDistance must be positive");
    }
}

function assertShapeId(value: number): void {
    assertInteger(value, "shapeId");
    if (value < 0 || value > SHAPE_ID_MAX) {
        throw new Error(`shapeId must be between 0 and ${SHAPE_ID_MAX}`);
    }
}

function assertShapeMaterialValue(value: number, label: string): void {
    assertFinite(value, label);
    if (value !== SHAPE_MATERIAL_INHERIT && value < 0) {
        throw new Error(`${label} must be non-negative or the inherit sentinel -1`);
    }
}

function assertConstraintId(value: number): void {
    assertInteger(value, "constraintId");
    if (value < 0 || value > INT32_MAX) {
        throw new Error(`constraintId must be between 0 and ${INT32_MAX}`);
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
    private eventIntView: Int32Array | null = null;
    private eventFloatView: Float32Array | null = null;
    private queryHitsView: Float32Array | null = null;
    private queryIdsView: Int32Array | null = null;

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
            } else if (options.shape.kind === "capsule") {
                assertPositive(options.shape.radius, "shape.radius");
                assertNonNegative(options.shape.halfSegment, "shape.halfSegment");
            } else if (options.shape.kind === "cylinder") {
                assertPositive(options.shape.radius, "shape.radius");
                assertPositive(options.shape.halfHeight, "shape.halfHeight");
            } else if (options.shape.kind === "cone") {
                assertPositive(options.shape.radius, "shape.radius");
                assertPositive(options.shape.halfHeight, "shape.halfHeight");
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
            let shapeStatus = -1;
            if (options.shape.kind === "sphere") {
                shapeStatus = this.wasm.bodySetSphere(id, options.shape.radius, density);
            } else if (options.shape.kind === "box") {
                shapeStatus = this.wasm.bodySetBox(
                    id,
                    options.shape.halfExtents[0],
                    options.shape.halfExtents[1],
                    options.shape.halfExtents[2],
                    density
                );
            } else if (options.shape.kind === "capsule") {
                shapeStatus = this.wasm.shapeCreateCapsule(
                    id,
                    options.shape.radius,
                    options.shape.halfSegment,
                    density,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    1
                );
            } else if (options.shape.kind === "cylinder") {
                shapeStatus = this.wasm.shapeCreateCylinder(
                    id,
                    options.shape.radius,
                    options.shape.halfHeight,
                    density,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    1
                );
            } else if (options.shape.kind === "cone") {
                shapeStatus = this.wasm.shapeCreateCone(
                    id,
                    options.shape.radius,
                    options.shape.halfHeight,
                    density,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    1
                );
            }
            if (shapeStatus < 0) {
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

    attachCapsule(id: number, options: CapsuleAttachOptions): number {
        this.ensureAlive();
        this.assertActive(id);
        assertPositive(options.radius, "radius");
        assertNonNegative(options.halfSegment, "halfSegment");
        const density = options.density ?? 1;
        assertFinite(density, "density");
        if (density < 0) {
            throw new Error("density must be non-negative");
        }
        const offset = options.offset ?? [0, 0, 0];
        const rotation = options.rotation ?? [0, 0, 0, 1];
        assertVec3(offset, "offset");
        assertQuat(rotation, "rotation");
        const shapeId = this.wasm.shapeCreateCapsule(
            id,
            options.radius,
            options.halfSegment,
            density,
            offset[0],
            offset[1],
            offset[2],
            rotation[0],
            rotation[1],
            rotation[2],
            rotation[3]
        );
        if (shapeId < 0) {
            throw new Error(`body ${id} capsule attachment failed`);
        }
        return shapeId;
    }

    attachCylinder(id: number, options: CylinderAttachOptions): number {
        this.ensureAlive();
        this.assertActive(id);
        assertPositive(options.radius, "radius");
        assertPositive(options.halfHeight, "halfHeight");
        const density = options.density ?? 1;
        assertFinite(density, "density");
        if (density < 0) {
            throw new Error("density must be non-negative");
        }
        const offset = options.offset ?? [0, 0, 0];
        const rotation = options.rotation ?? [0, 0, 0, 1];
        assertVec3(offset, "offset");
        assertQuat(rotation, "rotation");
        const shapeId = this.wasm.shapeCreateCylinder(
            id,
            options.radius,
            options.halfHeight,
            density,
            offset[0],
            offset[1],
            offset[2],
            rotation[0],
            rotation[1],
            rotation[2],
            rotation[3]
        );
        if (shapeId < 0) {
            throw new Error(`body ${id} cylinder attachment failed`);
        }
        return shapeId;
    }

    attachCone(id: number, options: ConeAttachOptions): number {
        this.ensureAlive();
        this.assertActive(id);
        assertPositive(options.radius, "radius");
        assertPositive(options.halfHeight, "halfHeight");
        const density = options.density ?? 1;
        assertFinite(density, "density");
        if (density < 0) {
            throw new Error("density must be non-negative");
        }
        const offset = options.offset ?? [0, 0, 0];
        const rotation = options.rotation ?? [0, 0, 0, 1];
        assertVec3(offset, "offset");
        assertQuat(rotation, "rotation");
        const shapeId = this.wasm.shapeCreateCone(
            id,
            options.radius,
            options.halfHeight,
            density,
            offset[0],
            offset[1],
            offset[2],
            rotation[0],
            rotation[1],
            rotation[2],
            rotation[3]
        );
        if (shapeId < 0) {
            throw new Error(`body ${id} cone attachment failed`);
        }
        return shapeId;
    }

    addSphere(id: number, radius: number, density = 1, offset: Vec3Tuple = [0, 0, 0]): number {
        this.ensureAlive();
        this.assertActive(id);
        assertPositive(radius, "radius");
        assertFinite(density, "density");
        if (density < 0) {
            throw new Error("density must be non-negative");
        }
        assertVec3(offset, "offset");
        const shapeId = this.wasm.shapeCreateSphere(
            id,
            radius,
            density,
            offset[0],
            offset[1],
            offset[2]
        );
        if (shapeId < 0) {
            throw new Error(`body ${id} sphere creation failed`);
        }
        return shapeId;
    }

    addBox(
        id: number,
        halfExtents: Vec3Tuple,
        density = 1,
        offset: Vec3Tuple = [0, 0, 0],
        rotation: QuatTuple = [0, 0, 0, 1]
    ): number {
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
        assertVec3(offset, "offset");
        assertQuat(rotation, "rotation");
        const shapeId = this.wasm.shapeCreateBox(
            id,
            halfExtents[0],
            halfExtents[1],
            halfExtents[2],
            density,
            offset[0],
            offset[1],
            offset[2],
            rotation[0],
            rotation[1],
            rotation[2],
            rotation[3]
        );
        if (shapeId < 0) {
            throw new Error(`body ${id} box creation failed`);
        }
        return shapeId;
    }

    destroyShape(shapeId: number): void {
        this.ensureAlive();
        assertShapeId(shapeId);
        if (this.wasm.shapeDestroy(shapeId) !== 0) {
            throw new Error(`shape ${shapeId} destruction failed`);
        }
    }

    getShapeBody(shapeId: number): number {
        this.ensureAlive();
        assertShapeId(shapeId);
        const bodyId = this.wasm.shapeGetBody(shapeId);
        if (bodyId < 0) {
            throw new Error(`shape ${shapeId} body query failed`);
        }
        return bodyId;
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

    setDamping(id: number, linear: number, angular: number): void {
        this.ensureAlive();
        this.assertActive(id);
        assertFinite(linear, "linearDamping");
        assertFinite(angular, "angularDamping");
        if (linear < 0 || angular < 0) {
            throw new Error("damping must be non-negative");
        }
        if (this.wasm.bodySetDamping(id, linear, angular) !== 0) {
            throw new Error(`body ${id} damping failed`);
        }
    }

    setGravityScale(id: number, scale: number): void {
        this.ensureAlive();
        this.assertActive(id);
        assertFinite(scale, "gravityScale");
        if (this.wasm.bodySetGravityScale(id, scale) !== 0) {
            throw new Error(`body ${id} gravity scale failed`);
        }
    }

    setFlags(id: number, setMask: number, clearMask = 0): void {
        this.ensureAlive();
        this.assertActive(id);
        assertInteger(setMask, "setMask");
        assertInteger(clearMask, "clearMask");
        if (setMask < 0 || clearMask < 0) {
            throw new Error("flag masks must be non-negative");
        }
        if (this.wasm.bodySetFlags(id, setMask, clearMask) !== 0) {
            throw new Error(`body ${id} flags failed`);
        }
    }

    applyForce(id: number, force: Vec3Tuple, point: Vec3Tuple = [0, 0, 0]): void {
        this.ensureAlive();
        this.assertActive(id);
        assertVec3(force, "force");
        assertVec3(point, "point");
        if (this.wasm.bodyApplyForce(id, force[0], force[1], force[2], point[0], point[1], point[2]) !== 0) {
            throw new Error(`body ${id} force application failed`);
        }
    }

    applyImpulse(id: number, impulse: Vec3Tuple, point: Vec3Tuple = [0, 0, 0]): void {
        this.ensureAlive();
        this.assertActive(id);
        assertVec3(impulse, "impulse");
        assertVec3(point, "point");
        if (this.wasm.bodyApplyImpulse(id, impulse[0], impulse[1], impulse[2], point[0], point[1], point[2]) !== 0) {
            throw new Error(`body ${id} impulse application failed`);
        }
    }

    applyTorque(id: number, torque: Vec3Tuple): void {
        this.ensureAlive();
        this.assertActive(id);
        assertVec3(torque, "torque");
        if (this.wasm.bodyApplyTorque(id, torque[0], torque[1], torque[2]) !== 0) {
            throw new Error(`body ${id} torque application failed`);
        }
    }

    applyAngularImpulse(id: number, impulse: Vec3Tuple): void {
        this.ensureAlive();
        this.assertActive(id);
        assertVec3(impulse, "angularImpulse");
        if (this.wasm.bodyApplyAngularImpulse(id, impulse[0], impulse[1], impulse[2]) !== 0) {
            throw new Error(`body ${id} angular impulse application failed`);
        }
    }

    setMassData(id: number, mass: number, inertia: Vec3Tuple = [0, 0, 0]): void {
        this.ensureAlive();
        this.assertActive(id);
        assertFinite(mass, "mass");
        assertVec3(inertia, "inertia");
        if (this.wasm.bodySetMassData(id, mass, inertia[0], inertia[1], inertia[2]) !== 0) {
            throw new Error(`body ${id} mass data failed`);
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

    createFixedJoint(bodyA: number, bodyB: number, options: FixedJointOptions): number {
        this.ensureAlive();
        this.assertActive(bodyA);
        this.assertActive(bodyB);
        assertVec3(options.localAnchorA, "localAnchorA");
        assertVec3(options.localAnchorB, "localAnchorB");
        const id = this.wasm.constraintCreateFixed(
            bodyA,
            bodyB,
            options.localAnchorA[0],
            options.localAnchorA[1],
            options.localAnchorA[2],
            options.localAnchorB[0],
            options.localAnchorB[1],
            options.localAnchorB[2],
            options.collideConnected ? 1 : 0
        );
        if (id < 0) {
            throw new Error("fixed joint creation failed");
        }
        return id;
    }

    createHingeJoint(bodyA: number, bodyB: number, options: HingeJointOptions): number {
        this.ensureAlive();
        this.assertActive(bodyA);
        this.assertActive(bodyB);
        assertVec3(options.localAnchorA, "localAnchorA");
        assertVec3(options.localAnchorB, "localAnchorB");
        assertVec3(options.localAxisA, "localAxisA");
        assertVec3(options.localAxisB, "localAxisB");
        const lowerLimit = options.lowerLimit ?? 0;
        const upperLimit = options.upperLimit ?? 0;
        assertFinite(lowerLimit, "lowerLimit");
        assertFinite(upperLimit, "upperLimit");
        if (options.enableLimit && lowerLimit > upperLimit) {
            throw new Error("lowerLimit must not exceed upperLimit");
        }
        const motorSpeed = options.motorSpeed ?? 0;
        const maxMotorTorque = options.maxMotorTorque ?? 0;
        assertFinite(motorSpeed, "motorSpeed");
        assertFinite(maxMotorTorque, "maxMotorTorque");
        if (maxMotorTorque < 0) {
            throw new Error("maxMotorTorque must be non-negative");
        }
        const id = this.wasm.constraintCreateHinge(
            bodyA,
            bodyB,
            options.localAnchorA[0],
            options.localAnchorA[1],
            options.localAnchorA[2],
            options.localAnchorB[0],
            options.localAnchorB[1],
            options.localAnchorB[2],
            options.localAxisA[0],
            options.localAxisA[1],
            options.localAxisA[2],
            options.localAxisB[0],
            options.localAxisB[1],
            options.localAxisB[2],
            options.enableLimit ? 1 : 0,
            lowerLimit,
            upperLimit,
            options.enableMotor ? 1 : 0,
            motorSpeed,
            maxMotorTorque,
            options.collideConnected ? 1 : 0
        );
        if (id < 0) {
            throw new Error("hinge joint creation failed");
        }
        return id;
    }

    createSliderJoint(bodyA: number, bodyB: number, options: SliderJointOptions): number {
        this.ensureAlive();
        this.assertActive(bodyA);
        this.assertActive(bodyB);
        assertVec3(options.localAnchorA, "localAnchorA");
        assertVec3(options.localAnchorB, "localAnchorB");
        assertVec3(options.localAxisA, "localAxisA");
        const lowerLimit = options.lowerLimit ?? 0;
        const upperLimit = options.upperLimit ?? 0;
        assertFinite(lowerLimit, "lowerLimit");
        assertFinite(upperLimit, "upperLimit");
        if (options.enableLimit && lowerLimit > upperLimit) {
            throw new Error("lowerLimit must not exceed upperLimit");
        }
        const motorSpeed = options.motorSpeed ?? 0;
        const maxMotorForce = options.maxMotorForce ?? 0;
        assertFinite(motorSpeed, "motorSpeed");
        assertFinite(maxMotorForce, "maxMotorForce");
        if (maxMotorForce < 0) {
            throw new Error("maxMotorForce must be non-negative");
        }
        const id = this.wasm.constraintCreateSlider(
            bodyA,
            bodyB,
            options.localAnchorA[0],
            options.localAnchorA[1],
            options.localAnchorA[2],
            options.localAnchorB[0],
            options.localAnchorB[1],
            options.localAnchorB[2],
            options.localAxisA[0],
            options.localAxisA[1],
            options.localAxisA[2],
            options.enableLimit ? 1 : 0,
            lowerLimit,
            upperLimit,
            options.enableMotor ? 1 : 0,
            motorSpeed,
            maxMotorForce,
            options.collideConnected ? 1 : 0
        );
        if (id < 0) {
            throw new Error("slider joint creation failed");
        }
        return id;
    }

    createSpringJoint(bodyA: number, bodyB: number, options: SpringJointOptions): number {
        this.ensureAlive();
        this.assertActive(bodyA);
        this.assertActive(bodyB);
        assertVec3(options.localAnchorA, "localAnchorA");
        assertVec3(options.localAnchorB, "localAnchorB");
        const restLength = options.restLength ?? 1;
        const stiffness = options.stiffness ?? 10;
        const damping = options.damping ?? 0.5;
        assertFinite(restLength, "restLength");
        assertFinite(stiffness, "stiffness");
        assertFinite(damping, "damping");
        if (restLength < 0 || stiffness < 0 || damping < 0) {
            throw new Error("spring parameters must be non-negative");
        }
        const id = this.wasm.constraintCreateSpring(
            bodyA,
            bodyB,
            options.localAnchorA[0],
            options.localAnchorA[1],
            options.localAnchorA[2],
            options.localAnchorB[0],
            options.localAnchorB[1],
            options.localAnchorB[2],
            restLength,
            stiffness,
            damping,
            options.collideConnected ? 1 : 0
        );
        if (id < 0) {
            throw new Error("spring joint creation failed");
        }
        return id;
    }

    destroyConstraint(id: number): void {
        this.ensureAlive();
        assertConstraintId(id);
        if (this.wasm.constraintDestroy(id) !== 0) {
            throw new Error(`constraint ${id} destruction failed`);
        }
    }

    setConstraintEnabled(id: number, enabled: boolean): void {
        this.ensureAlive();
        assertConstraintId(id);
        if (this.wasm.constraintSetEnabled(id, enabled ? 1 : 0) !== 0) {
            throw new Error(`constraint ${id} enabled state failed`);
        }
    }

    setHingeMotor(id: number, speed: number, maxTorque: number): void {
        this.ensureAlive();
        assertConstraintId(id);
        assertFinite(speed, "motorSpeed");
        assertFinite(maxTorque, "maxMotorTorque");
        if (maxTorque < 0) {
            throw new Error("maxMotorTorque must be non-negative");
        }
        if (this.wasm.constraintSetHingeMotor(id, speed, maxTorque) !== 0) {
            throw new Error(`constraint ${id} hinge motor failed`);
        }
    }

    setHingeLimit(id: number, enable: boolean, lower: number, upper: number): void {
        this.ensureAlive();
        assertConstraintId(id);
        assertFinite(lower, "lowerLimit");
        assertFinite(upper, "upperLimit");
        if (enable && lower > upper) {
            throw new Error("lowerLimit must not exceed upperLimit");
        }
        if (this.wasm.constraintSetHingeLimit(id, enable ? 1 : 0, lower, upper) !== 0) {
            throw new Error(`constraint ${id} hinge limit failed`);
        }
    }

    setSliderMotor(id: number, speed: number, maxForce: number): void {
        this.ensureAlive();
        assertConstraintId(id);
        assertFinite(speed, "motorSpeed");
        assertFinite(maxForce, "maxMotorForce");
        if (maxForce < 0) {
            throw new Error("maxMotorForce must be non-negative");
        }
        if (this.wasm.constraintSetSliderMotor(id, speed, maxForce) !== 0) {
            throw new Error(`constraint ${id} slider motor failed`);
        }
    }

    setSliderLimit(id: number, enable: boolean, lower: number, upper: number): void {
        this.ensureAlive();
        assertConstraintId(id);
        assertFinite(lower, "lowerLimit");
        assertFinite(upper, "upperLimit");
        if (enable && lower > upper) {
            throw new Error("lowerLimit must not exceed upperLimit");
        }
        if (this.wasm.constraintSetSliderLimit(id, enable ? 1 : 0, lower, upper) !== 0) {
            throw new Error(`constraint ${id} slider limit failed`);
        }
    }

    constraintCount(): number {
        this.ensureAlive();
        const count = this.wasm.constraintCount();
        if (count < 0) {
            throw new Error("constraint count query failed");
        }
        return count;
    }

    constraintKind(id: number): number {
        this.ensureAlive();
        assertConstraintId(id);
        const kind = this.wasm.constraintGetKind(id);
        if (kind < 0) {
            throw new Error(`constraint ${id} kind query failed`);
        }
        return kind;
    }

    getJointReactionForce(id: number): MutableVec3 {
        this.ensureAlive();
        assertConstraintId(id);
        return [0, 0, 0];
    }

    getJointReactionTorque(id: number): number {
        this.ensureAlive();
        assertConstraintId(id);
        return 0;
    }

    reset(): void {
        this.ensureAlive();
        this.wasm.worldReset();
        this.activeBodies.clear();
    }

    setAllowSleep(on: boolean): void {
        this.ensureAlive();
        if (this.wasm.worldSetAllowSleep(on ? 1 : 0) !== 0) {
            throw new Error("allow sleep update failed");
        }
    }

    setAwake(id: number, awake: boolean): void {
        this.ensureAlive();
        this.assertActive(id);
        if (this.wasm.bodySetAwake(id, awake ? 1 : 0) !== 0) {
            throw new Error(`body ${id} awake state failed`);
        }
    }

    isSleeping(id: number): boolean {
        this.ensureAlive();
        this.assertActive(id);
        const state = this.wasm.bodyIsSleeping(id);
        if (state < 0) {
            throw new Error(`body ${id} sleep query failed`);
        }
        return state === 1;
    }

    setShapeMaterial(shapeId: number, friction: number, restitution: number): void {
        this.ensureAlive();
        assertShapeId(shapeId);
        assertShapeMaterialValue(friction, "friction");
        assertShapeMaterialValue(restitution, "restitution");
        if (this.wasm.shapeSetMaterial(shapeId, friction, restitution) !== 0) {
            throw new Error(`shape ${shapeId} material failed`);
        }
    }

    getShapeMaterial(shapeId: number): ShapeMaterial {
        this.ensureAlive();
        assertShapeId(shapeId);
        const scratch = this.queryHits();
        if (this.wasm.shapeGetMaterial(shapeId, scratch.byteOffset) !== 0) {
            throw new Error(`shape ${shapeId} material query failed`);
        }
        return [scratch[0], scratch[1]];
    }

    setShapeSensor(shapeId: number, on: boolean): void {
        this.ensureAlive();
        assertShapeId(shapeId);
        if (this.wasm.shapeSetSensor(shapeId, on ? 1 : 0) !== 0) {
            throw new Error(`shape ${shapeId} sensor flag failed`);
        }
    }

    isShapeSensor(shapeId: number): boolean {
        this.ensureAlive();
        assertShapeId(shapeId);
        const state = this.wasm.shapeIsSensor(shapeId);
        if (state < 0) {
            throw new Error(`shape ${shapeId} sensor query failed`);
        }
        return state === 1;
    }

    setShapeFilter(shapeId: number, categoryBits: number, maskBits: number, groupIndex: number): void {
        this.ensureAlive();
        assertShapeId(shapeId);
        assertInteger(categoryBits, "categoryBits");
        assertInteger(maskBits, "maskBits");
        assertInteger(groupIndex, "groupIndex");
        if (categoryBits < INT32_MIN || categoryBits > INT32_MAX ||
            maskBits < INT32_MIN || maskBits > INT32_MAX ||
            groupIndex < INT32_MIN || groupIndex > INT32_MAX) {
            throw new Error("filter values must fit in int32");
        }
        if (this.wasm.shapeSetFilter(shapeId, categoryBits, maskBits, groupIndex) !== 0) {
            throw new Error(`shape ${shapeId} filter failed`);
        }
    }

    getShapeFilter(shapeId: number): ShapeFilter {
        this.ensureAlive();
        assertShapeId(shapeId);
        const scratch = this.queryIds();
        if (this.wasm.shapeGetFilter(shapeId, scratch.byteOffset) !== 0) {
            throw new Error(`shape ${shapeId} filter query failed`);
        }
        return [scratch[0], scratch[1], scratch[2]];
    }

    drainEvents(): ContactEventSnapshot[] {
        this.ensureAlive();
        const count = this.wasm.eventCount();
        const events: ContactEventSnapshot[] = [];
        if (count > 0) {
            const ints = this.getEventInts();
            const floats = this.getEventFloats();
            for (let index = 0; index < count; index += 1) {
                const base = index * EVENT_RECORD_STRIDE;
                const kindCode = ints[base];
                events.push({
                    kind: CONTACT_EVENT_KINDS[kindCode >= 0 && kindCode <= 5 ? kindCode : 1],
                    bodyA: ints[base + 1],
                    bodyB: ints[base + 2],
                    shapeA: ints[base + 3],
                    shapeB: ints[base + 4],
                    normal: [floats[base + 5], floats[base + 6], floats[base + 7]],
                    penetration: floats[base + 8]
                });
            }
        }
        this.wasm.eventsClear();
        return events;
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

    raycast(origin: Vec3Tuple, direction: Vec3Tuple, maxDistance = Number.POSITIVE_INFINITY): RaycastHit[] {
        this.ensureAlive();
        assertVec3(origin, "origin");
        assertVec3(direction, "direction");
        assertRayDistance(maxDistance);

        const hits = this.queryHits();
        const ids = this.queryIds();
        const count = this.wasm.raycastAll(
            origin[0],
            origin[1],
            origin[2],
            direction[0],
            direction[1],
            direction[2],
            maxDistance,
            hits.byteOffset,
            hits.length / RAY_HIT_STRIDE
        );

        const result: RaycastHit[] = new Array(count);
        for (let index = 0; index < count; index += 1) {
            const base = index * RAY_HIT_STRIDE;
            result[index] = {
                body: ids[base],
                shape: ids[base + 1],
                distance: hits[base + 2],
                point: [hits[base + 3], hits[base + 4], hits[base + 5]],
                normal: [hits[base + 6], hits[base + 7], hits[base + 8]]
            };
        }
        return result;
    }

    raycastClosest(origin: Vec3Tuple, direction: Vec3Tuple, maxDistance = Number.POSITIVE_INFINITY): RaycastHit | null {
        this.ensureAlive();
        assertVec3(origin, "origin");
        assertVec3(direction, "direction");
        assertRayDistance(maxDistance);

        const hits = this.queryHits();
        const ids = this.queryIds();
        const written = this.wasm.raycastClosest(
            origin[0],
            origin[1],
            origin[2],
            direction[0],
            direction[1],
            direction[2],
            maxDistance,
            hits.byteOffset
        );
        if (written === 0) {
            return null;
        }
        return {
            body: ids[0],
            shape: ids[1],
            distance: hits[2],
            point: [hits[3], hits[4], hits[5]],
            normal: [hits[6], hits[7], hits[8]]
        };
    }

    overlapSphere(center: Vec3Tuple, radius: number, capacity = DEFAULT_QUERY_CAPACITY): number[] {
        this.ensureAlive();
        assertVec3(center, "center");
        assertFinite(radius, "radius");
        if (radius < 0) {
            throw new Error("radius must be non-negative");
        }
        assertInteger(capacity, "capacity");

        const ids = this.queryIds();
        if (capacity < 1 || capacity > ids.length) {
            throw new Error(`capacity must be between 1 and ${ids.length}`);
        }
        const count = this.wasm.overlapSphere(
            center[0],
            center[1],
            center[2],
            radius,
            ids.byteOffset,
            capacity
        );

        const result: number[] = new Array(count);
        for (let index = 0; index < count; index += 1) {
            result[index] = ids[index];
        }
        return result;
    }

    dispose(): void {
        this.disposed = true;
        this.activeBodies.clear();
        this.transformView = null;
        this.velocityView = null;
        this.angularVelocityView = null;
        this.contactView = null;
        this.eventIntView = null;
        this.eventFloatView = null;
        this.queryHitsView = null;
        this.queryIdsView = null;
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
            this.eventIntView = null;
            this.eventFloatView = null;
            this.queryHitsView = null;
            this.queryIdsView = null;
        }
    }

    private getEventInts(): Int32Array {
        this.refreshViews();
        if (this.eventIntView === null) {
            const pointer = this.wasm.eventsPtr();
            if (pointer === 0 || (pointer & 15) !== 0) {
                throw new Error("event buffer is unavailable");
            }
            this.eventIntView = new Int32Array(this.wasm.memory.buffer, pointer, EVENT_CAPACITY * EVENT_RECORD_STRIDE);
        }
        return this.eventIntView;
    }

    private getEventFloats(): Float32Array {
        this.refreshViews();
        if (this.eventFloatView === null) {
            const pointer = this.wasm.eventsPtr();
            if (pointer === 0 || (pointer & 15) !== 0) {
                throw new Error("event buffer is unavailable");
            }
            this.eventFloatView = new Float32Array(this.wasm.memory.buffer, pointer, EVENT_CAPACITY * EVENT_RECORD_STRIDE);
        }
        return this.eventFloatView;
    }

    private queryHits(): Float32Array {
        this.refreshViews();
        if (this.queryHitsView === null) {
            const pointer = this.wasm.queryScratchPtr();
            const capacity = this.wasm.queryScratchCapacity();
            if (pointer === 0 || capacity <= 0 || (pointer & 15) !== 0) {
                throw new Error("query scratch is unavailable");
            }
            const elements = capacity * RAY_HIT_STRIDE;
            this.queryHitsView = new Float32Array(this.wasm.memory.buffer, pointer, elements);
        }
        return this.queryHitsView;
    }

    private queryIds(): Int32Array {
        this.refreshViews();
        if (this.queryIdsView === null) {
            const pointer = this.wasm.queryScratchPtr();
            const capacity = this.wasm.queryScratchCapacity();
            if (pointer === 0 || capacity <= 0 || (pointer & 15) !== 0) {
                throw new Error("query scratch is unavailable");
            }
            this.queryIdsView = new Int32Array(this.wasm.memory.buffer, pointer, capacity * RAY_HIT_STRIDE);
        }
        return this.queryIdsView;
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
