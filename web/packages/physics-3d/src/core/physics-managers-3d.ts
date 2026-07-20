import type { IVec3Like, IQuatLike } from '@axrone/numeric';
import type {
    BodyId3D,
    ConstraintId3D,
    IBoxShapeDef3D,
    ICapsuleShapeDef3D,
    ICollisionFilter3D,
    IConeShapeDef3D,
    IConeTwistConstraintDef3D,
    IConvexHullShapeDef3D,
    IFixedConstraintDef3D,
    IGenericConstraintDef3D,
    IHeightFieldShapeDef3D,
    IHingeConstraintDef3D,
    IPhysicsBodyDef3D,
    ISliderConstraintDef3D,
    ISphereShapeDef3D,
    ISpringConstraintDef3D,
    ICylinderShapeDef3D,
    ITriangleMeshShapeDef3D,
    ShapeId3D,
} from '../types/physics-3d';
import type { BodyType, IMaterial } from '../types';
import { BodyFlags, BodyType as BodyTypeEnum, ShapeType } from '../types';

const BODY_TYPE_DYNAMIC = BodyTypeEnum.Dynamic;

const POSITION_STRIDE = 8;
const VELOCITY_STRIDE = 8;
const MASS_STRIDE = 16;
const SHAPE_STRIDE = 24;
const CONSTRAINT_STRIDE = 32;

const POSITION_OFFSET = 0;
const ROTATION_OFFSET = 3;
const LINEAR_VEL_OFFSET = 0;
const ANGULAR_VEL_OFFSET = 3;

enum BodyManagerError {
    INVALID_STATE = 'INVALID_STATE',
    CAPACITY_EXCEEDED = 'CAPACITY_EXCEEDED',
}

export class PhysicsError3D extends Error {
    readonly code: string;

    constructor(message: string, code: string) {
        super(message);
        this.name = 'PhysicsError3D';
        this.code = code;
    }
}

interface ManagerPool<T> {
    nextId: () => number;
    indexMap: Map<number, T>;
    freeList: number[];
    maxCapacity: number;
}

export class BodyManager3D implements Disposable {
    private readonly _nextBodyId = 1n;
    private readonly _bodyCount = 0n;
    private readonly _maxBodies = 4096n;
    private readonly _bodyIdToIndex = new Map<BodyId3D, number>();

    private readonly _positions = new Float64Array(4096 * POSITION_STRIDE);
    private readonly _velocities = new Float64Array(4096 * VELOCITY_STRIDE);
    private readonly _massData = new Float64Array(4096 * MASS_STRIDE);
    private readonly _bodyTypes = new Uint8Array(4096);
    private readonly _bodyFlags = new Uint32Array(4096);
    private readonly _gravityScales = new Float32Array(4096);
    private readonly _dampings = new Float32Array(4096 * 2);

    constructor(maxBodies: number = 4096) {
        this._maxBodies = maxBodies;
        const posInit = new Float64Array(this._positions.length, 0);
        for (let i = 0; i < maxBodies; i++) {
            posInit[i * POSITION_STRIDE + ROTATION_OFFSET + 3] = 1;
            this._gravityScales[i] = 1;
        }
        this._positions.set(posInit);
        this._velocities.set(new Float64Array(4096 * VELOCITY_STRIDE, 0));
        this._massData.set(new Float64Array(4096 * MASS_STRIDE, 0));
        this._bodyTypes.set(new Uint8Array(4096, 0));
        this._bodyFlags.set(new Uint32Array(4096, 0));
        this._gravityScales.set(new Float32Array(4096, 1));
        this._dampings.set(new Float32Array(4096 * 2, 0));
    }

    get bodyCount(): number { return Number(this._bodyCount); }

    createBody(def: IPhysicsBodyDef3D): BodyId3D {
        if (Number(this._bodyCount) >= this._maxBodies && this._freeList.length === 0) {
            throw new PhysicsError3D('Body capacity exceeded', BodyManagerError.CAPACITY_EXCEEDED);
        }

        const bodyId = this._nextBodyId++ as BodyId3D;
        const index = this._allocateIndex();

        const posOffset = index * POSITION_STRIDE;
        if (def.position) {
            this._positions[posOffset] = def.position.x;
            this._positions[posOffset + 1] = def.position.y;
            this._positions[posOffset + 2] = def.position.z;
        }

        if (def.rotation) {
            this._writeQuat(posOffset + ROTATION_OFFSET, def.rotation);
        } else {
            this._positions[posOffset + ROTATION_OFFSET + 3] = 1;
        }

        const velOffset = index * VELOCITY_STRIDE;
        if (def.linearVelocity) {
            this._velocities[velOffset] = def.linearVelocity.x;
            this._velocities[velOffset + 1] = def.linearVelocity.y;
            this._velocities[velOffset + 2] = def.linearVelocity.z;
        }

        if (def.angularVelocity) {
            this._velocities[velOffset + ANGULAR_VEL_OFFSET] = def.angularVelocity.x;
            this._velocities[velOffset + ANGULAR_VEL_OFFSET + 1] = def.angularVelocity.y;
            this._velocities[velOffset + ANGULAR_VEL_OFFSET + 2] = def.angularVelocity.z;
        }

        const type = def.type === undefined ? BodyTypeEnum.Dynamic : def.type;
        this._bodyTypes[index] = type;
        this._gravityScales[index] = def.gravityScale ?? 1;
        this._dampings[index * 2] = def.linearDamping ?? 0;
        this._dampings[index * 2 + 1] = def.angularDamping ?? 0;

        let flags = 0;
        if (def.fixedRotation !== false) flags |= BodyFlags.FixedRotation;
        if (def.bullet !== false) flags |= BodyFlags.Bullet;
        if (def.allowSleep !== false) flags |= BodyFlags.AutoSleep;
        if (def.awake !== false) flags |= BodyFlags.Awake;
        if (def.enabled !== false) flags |= BodyFlags.Active;
        this._bodyFlags[index] = flags;

        const massOffset = index * MASS_STRIDE;
        if (type === BODY_TYPE_DYNAMIC) {
            this._massData[massOffset] = 1;
            this._massData[massOffset + 1] = 1;
        } else {
            for (let i = 0; i < MASS_STRIDE; i++) this._massData[massOffset + i] = 0;
        }

        if (def.userData !== undefined) {
            this._userData.set(bodyId, def.userData);
        }

        this._bodyCount += 1n;
        return bodyId;
    }

    destroyBody(bodyId: BodyId3D): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;

        this._bodyIdToIndex.delete(bodyId);
        this._freeList.push(index);

        for (let i = 0; i < POSITION_STRIDE; i++) this._positions[index * POSITION_STRIDE + i] = 0;
        this._velocities[index * VELOCITY_STRIDE].fill(0);
        this._bodyTypes[index] = 0;
        this._bodyFlags[index] = 0;
        this._gravityScales[index] = 1;
        this._dampings[index * 2] = 0;
        this._dampings[index * 2 + 1] = 0;

        const massOffset = index * MASS_STRIDE;
        for (let i = 0; i < MASS_STRIDE; i++) this._massData[massOffset + i] = 0;

        if (this._userData.has(bodyId)) {
            this._userData.delete(bodyId);
        }

        this._bodyCount -= 1n;
    }

    hasBody(bodyId: BodyId3D): boolean { return this._bodyIdToIndex.has(bodyId); }

    getPosition(bodyId: BodyId3D): IVec3Like {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return { x: 0, y: 0, z: 0 };
        return this._readVec(this._positions, index * POSITION_STRIDE + POSITION_OFFSET);
    }

    setPosition(bodyId: BodyId3D, position: IVec3Like): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;
        const offset = index * POSITION_STRIDE + POSITION_OFFSET;
        this._positions[offset] = position.x;
        this._positions[offset + 1] = position.y;
        this._positions[offset + 2] = position.z;
    }

    getRotation(bodyId: BodyId3D): IQuatLike {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return { x: 0, y: 0, z: 0, w: 1 };
        return this._readQuat(this._positions, index * POSITION_STRIDE + ROTATION_OFFSET);
    }

    setRotation(bodyId: BodyId3D, rotation: IQuatLike): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;
        this._writeQuat(index * POSITION_STRIDE + ROTATION_OFFSET, rotation);
    }

    getLinearVelocity(bodyId: BodyId3D): IVec3Like {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return { x: 0, y: 0, z: 0 };
        return this._readVec(this._velocities, index * VELOCITY_STRIDE + LINEAR_VEL_OFFSET);
    }

    setLinearVelocity(bodyId: BodyId3D, velocity: IVec3Like): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;
        const offset = index * VELOCITY_STRIDE + LINEAR_VEL_OFFSET;
        this._velocities[offset] = velocity.x;
        this._velocities[offset + 1] = velocity.y;
        this._velocities[offset + 2] = velocity.z;
    }

    getAngularVelocity(bodyId: BodyId3D): IVec3Like {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return { x: 0, y: 0, z: 0 };
        return this._readVec(this._velocities, index * VELOCITY_STRIDE + ANGULAR_VEL_OFFSET);
    }

    setAngularVelocity(bodyId: BodyId3D, velocity: IVec3Like): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;
        const offset = index * VELOCITY_STRIDE + ANGULAR_VEL_OFFSET;
        this._velocities[offset] = velocity.x;
        this._velocities[offset + 1] = velocity.y;
        this._velocities[offset + 2] = velocity.z;
    }

    getBodyType(bodyId: BodyId3D): number { return Number(this._bodyTypes[this._getBodyIndex(bodyId)]); }
    setBodyType(bodyId: BodyId3D, type: BodyType): void { this._bodyTypes[this._getBodyIndex(bodyId)] = type; }

    getBodyFlags(bodyId: BodyId3D): number { return Number(this._bodyFlags[this._getBodyIndex(bodyId)]); }
    isEnabled(bodyId: BodyId3D): boolean {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return false;
        return (this._bodyFlags[index] & BodyFlags.Active) !== 0;
    }
    setEnabled(bodyId: BodyId3D, enabled: boolean): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;
        if (enabled) this._bodyFlags[index] |= BodyFlags.Active;
        else this._bodyFlags[index] &= ~BodyFlags.Active;
    }

    isFixedRotation(bodyId: BodyId3D): boolean {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return false;
        return (this._bodyFlags[index] & BodyFlags.FixedRotation) !== 0;
    }
    setFixedRotation(bodyId: BodyId3D, fixed: boolean): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;
        if (fixed) this._bodyFlags[index] |= BodyFlags.FixedRotation;
        else this._bodyFlags[index] &= ~BodyFlags.FixedRotation;
    }

    isBullet(bodyId: BodyId3D): boolean {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return false;
        return (this._bodyFlags[index] & BodyFlags.Bullet) !== 0;
    }
    setBullet(bodyId: BodyId3D, bullet: boolean): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;
        if (bullet) this._bodyFlags[index] |= BodyFlags.Bullet;
        else this._bodyFlags[index] &= ~BodyFlags.Bullet;
    }

    getMass(bodyId: BodyId3D): number { return Number(this._massData[this._getBodyIndex(bodyId) * MASS_STRIDE]); }
    setMass(bodyId: BodyId3D, mass: number): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;
        const offset = index * MASS_STRIDE;
        this._massData[offset] = mass;
        this._massData[offset + 1] = mass > 0 ? 1 / mass : 0;
    }

    getInverseMass(bodyId: BodyId3D): number { return Number(this._massData[this._getBodyIndex(bodyId) * MASS_STRIDE + 1]); }
    getInertiaTensor(bodyId: BodyId3D): IVec3Like {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return { x: 0, y: 0, z: 0 };
        const offset = index * MASS_STRIDE + 2;
        return this._readVec(this._massData as unknown as Float64Array, offset);
    }

    getInverseInertia(bodyId: BodyId3D): IVec3Like {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return { x: 0, y: 0, z: 0 };
        const offset = index * MASS_STRIDE + 5;
        return this._readVec(this._massData as unknown as Float64Array, offset);
    }

    setInertiaTensor(bodyId: BodyId3D, inertia: IVec3Like): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;
        const offset = index * MASS_STRIDE + 2;
        this._massData[offset] = inertia.x;
        this._massData[offset + 1] = inertia.y;
        this._massData[offset + 2] = inertia.z;
        this._massData[offset + 3] = inertia.x > 0 ? 1 / inertia.x : 0;
        this._massData[offset + 4] = inertia.y > 0 ? 1 / inertia.y : 0;
        this._massData[offset + 5] = inertia.z > 0 ? 1 / inertia.z : 0;
    }

    getLinearDamping(bodyId: BodyId3D): number { return Number(this._dampings[this._getBodyIndex(bodyId) * 2]); }
    setLinearDamping(bodyId: BodyId3D, damping: number): void { this._dampings[this._getBodyIndex(bodyId) * 2] = damping; }

    getAngularDamping(bodyId: BodyId3D): number { return Number(this._dampings[this._getBodyIndex(bodyId) * 2 + 1]); }
    setAngularDamping(bodyId: BodyId3D, damping: number): void { this._dampings[this._getBodyIndex(bodyId) * 2 + 1] = damping; }

    getUserData(bodyId: BodyId3D): unknown { return this._userData.get(bodyId); }
    setUserData(bodyId: BodyId3D, userData: unknown): void {
        if (!this._bodyIdToIndex.has(bodyId)) return;
        if (userData === undefined) this._userData.delete(bodyId);
        else this._userData.set(bodyId, userData);
    }

    isAwake(bodyId: BodyId3D): boolean {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return false;
        return (this._bodyFlags[index] & BodyFlags.Awake) !== 0;
    }

    setAwake(bodyId: BodyId3D, awake: boolean): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) return;
        if (awake) this._bodyFlags[index] |= BodyFlags.Awake;
        else this._bodyFlags[index] &= ~BodyFlags.Awake;
    }

    applyForce(bodyId: BodyId3D, force: IVec3Like, point?: IVec3Like): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined || Number(this._bodyTypes[index]) !== BODY_TYPE_DYNAMIC) return;

        const invMass = this._massData[index * MASS_STRIDE + 1];
        if (invMass === 0) return;

        const velOffset = index * VELOCITY_STRIDE + LINEAR_VEL_OFFSET;
        this._velocities[velOffset] += force.x * invMass;
        this._velocities[velOffset + 1] += force.y * invMass;
        this._velocities[velOffset + 2] += force.z * invMass;

        if (point) {
            const posOffset = index * POSITION_STRIDE + POSITION_OFFSET;
            const rx = point.x - this._positions[posOffset];
            const ry = point.y - this._positions[posOffset + 1];
            const rz = point.z - this._positions[posOffset + 2];

            const massOffset = index * MASS_STRIDE + 5;
            const invIx = this._massData[massOffset];
            const invIy = this._massData[massOffset + 1];
            const invIz = this._massData[massOffset + 2];

            const angOffset = index * VELOCITY_STRIDE + ANGULAR_VEL_OFFSET;
            this._velocities[angOffset] += (ry * force.z - rz * force.y) * invIx;
            this._velocities[angOffset + 1] += (rz * force.x - rx * force.z) * invIy;
            this._velocities[angOffset + 2] += (rx * force.y - ry * force.x) * invIz;
        }

        this.setAwake(bodyId, true);
    }

    applyForceToCenter(bodyId: BodyId3D, force: IVec3Like): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined || Number(this._bodyTypes[index]) !== BODY_TYPE_DYNAMIC) return;

        const invMass = this._massData[index * MASS_STRIDE + 1];
        if (invMass === 0) return;

        const velOffset = index * VELOCITY_STRIDE + LINEAR_VEL_OFFSET;
        this._velocities[velOffset] += force.x * invMass;
        this._velocities[velOffset + 1] += force.y * invMass;
        this._velocities[velOffset + 2] += force.z * invMass;

        this.setAwake(bodyId, true);
    }

    applyTorque(bodyId: BodyId3D, torque: IVec3Like): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined || Number(this._bodyTypes[index]) !== BODY_TYPE_DYNAMIC) return;

        const massOffset = index * MASS_STRIDE + 5;
        const invIx = this._massData[massOffset];
        const invIy = this._massData[massOffset + 1];
        const invIz = this._massData[massOffset + 2];

        const angOffset = index * VELOCITY_STRIDE + ANGULAR_VEL_OFFSET;
        this._velocities[angOffset] += torque.x * invIx;
        this._velocities[angOffset + 1] += torque.y * invIy;
        this._velocities[angOffset + 2] += torque.z * invIz;

        this.setAwake(bodyId, true);
    }

    applyImpulse(bodyId: BodyId3D, impulse: IVec3Like, point?: IVec3Like): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined || Number(this._bodyTypes[index]) !== BODY_TYPE_DYNAMIC) return;

        const invMass = this._massData[index * MASS_STRIDE + 1];
        if (invMass === 0) return;

        const velOffset = index * VELOCITY_STRIDE + LINEAR_VEL_OFFSET;
        this._velocities[velOffset] += impulse.x * invMass;
        this._velocities[velOffset + 1] += impulse.y * invMass;
        this._velocities[velOffset + 2] += impulse.z * invMass;

        if (point) {
            const posOffset = index * POSITION_STRIDE + POSITION_OFFSET;
            const rx = point.x - this._positions[posOffset];
            const ry = point.y - this._positions[posOffset + 1];
            const rz = point.z - this._positions[posOffset + 2];

            const massOffset = index * MASS_STRIDE + 5;
            const invIx = this._massData[massOffset];
            const invIy = this._massData[massOffset + 1];
            const invIz = this._massData[massOffset + 2];

            const angOffset = index * VELOCITY_STRIDE + ANGULAR_VEL_OFFSET;
            this._velocities[angOffset] += (ry * impulse.z - rz * impulse.y) * invIx;
            this._velocities[angOffset + 1] += (rz * impulse.x - rx * impulse.z) * invIy;
            this._velocities[angOffset + 2] += (rx * impulse.y - ry * impulse.x) * invIz;
        }

        this.setAwake(bodyId, true);
    }

    applyImpulseToCenter(bodyId: BodyId3D, impulse: IVec3Like): void { this.applyImpulse(bodyId, impulse); }

    applyAngularImpulse(bodyId: BodyId3D, impulse: IVec3Like): void {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined || Number(this._bodyTypes[index]) !== BODY_TYPE_DYNAMIC) return;

        const massOffset = index * MASS_STRIDE + 5;
        const invIx = this._massData[massOffset];
        const invIy = this._massData[massOffset + 1];
        const invIz = this._massData[massOffset + 2];

        const angOffset = index * VELOCITY_STRIDE + ANGULAR_VEL_OFFSET;
        this._velocities[angOffset] += impulse.x * invIx;
        this._velocities[angOffset + 1] += impulse.y * invIy;
        this._velocities[angOffset + 2] += impulse.z * invIz;

        this.setAwake(bodyId, true);
    }

    getBodyIds(): BodyId3D[] { return Array.from(this._bodyIdToIndex.keys()); }

    getGravityScale(bodyId: BodyId3D): number { return Number(this._gravityScales[this._getBodyIndex(bodyId)]); }
    setGravityScale(bodyId: BodyId3D, scale: number): void { this._gravityScales[this._getBodyIndex(bodyId)] = scale; }

    private _allocateIndex(): number {
        if (this._freeList.length > 0) return this._freeList.pop()!;
        return Number(this._bodyCount);
    }

    #getBodyIndex(bodyId: BodyId3D): number {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) throw new PhysicsError3D('Body not found', BodyManagerError.INVALID_STATE);
        return index;
    }

    private _readVec(arr: Float64Array, offset: number): IVec3Like {
        return { x: arr[offset], y: arr[offset + 1], z: arr[offset + 2] };
    }

    private _writeQuat(baseOffset: number, q: IQuatLike): void {
        this._positions[baseOffset] = q.x;
        this._positions[baseOffset + 1] = q.y;
        this._positions[baseOffset + 2] = q.z;
        this._positions[baseOffset + 3] = q.w ?? 1;
    }

    [Symbol.dispose](): void {
        this._bodyIdToIndex.clear();
        this._freeList.length = 0;
        this._userData.clear();
    }
}

export class ShapeManager3D implements Disposable {
    private readonly _nextShapeId = 1n;
    private readonly _shapeCount = 0n;
    private readonly _maxShapes = 8192n;
    private readonly _shapeIdToIndex = new Map<ShapeId3D, number>();
    private readonly _shapeToBody = new Map<ShapeId3D, BodyId3D>();
    private readonly _bodyToShapes = new Map<BodyId3D, Set<ShapeId3D>>();

    private readonly _shapeTypes = new Uint8Array(8192);
    private readonly _shapeData = new Float64Array(8192 * SHAPE_STRIDE);
    private readonly _materials = new Float32Array(8192 * 4);
    private readonly _filters = new Uint32Array(8192 * 3);

    constructor(maxShapes: number = 8192) {
        this._maxShapes = maxShapes;
        this._shapeTypes.set(new Uint8Array(8192, 0));
        this._shapeData.set(new Float64Array(8192 * SHAPE_STRIDE, 0));
        this._materials.set(new Float32Array(8192 * 4, 0));
        this._filters.set(new Uint32Array(8192 * 3, 0));
    }

    get shapeCount(): number { return Number(this._shapeCount); }

    createSphere(bodyId: BodyId3D, def: ISphereShapeDef3D): ShapeId3D {
        return this._createShape(bodyId, ShapeType.Sphere, (offset) => {
            this._shapeData[offset] = def.center.x;
            this._shapeData[offset + 1] = def.center.y;
            this._shapeData[offset + 2] = def.center.z;
            this._shapeData[offset + 3] = def.radius;
        });
    }

    createBox(bodyId: BodyId3D, def: IBoxShapeDef3D): ShapeId3D {
        return this._createShape(bodyId, ShapeType.Box, (offset) => {
            this._shapeData[offset] = def.center.x;
            this._shapeData[offset + 1] = def.center.y;
            this._shapeData[offset + 2] = def.center.z;
            this._shapeData[offset + 3] = def.halfExtents.x;
            this._shapeData[offset + 4] = def.halfExtents.y;
            this._shapeData[offset + 5] = def.halfExtents.z;
        });
    }

    createCapsule(bodyId: BodyId3D, def: ICapsuleShapeDef3D): ShapeId3D {
        return this._createShape(bodyId, ShapeType.Capsule, (offset) => {
            this._shapeData[offset] = def.p1.x;
            this._shapeData[offset + 1] = def.p1.y;
            this._shapeData[offset + 2] = def.p1.z;
            this._shapeData[offset + 3] = def.p2.x;
            this._shapeData[offset + 4] = def.p2.y;
            this._shapeData[offset + 5] = def.p2.z;
            this._shapeData[offset + 6] = def.radius;
        });
    }

    createCylinder(bodyId: BodyId3D, def: ICylinderShapeDef3D): ShapeId3D {
        return this._createShape(bodyId, ShapeType.Cylinder, (offset) => {
            this._shapeData[offset] = def.center.x;
            this._shapeData[offset + 1] = def.center.y;
            this._shapeData[offset + 2] = def.center.z;
            this._shapeData[offset + 3] = def.radius;
            this._shapeData[offset + 4] = def.height;
            this._shapeData[offset + 5] = def.axis ?? 1;
        });
    }

    createCone(bodyId: BodyId3D, def: IConeShapeDef3D): ShapeId3D {
        return this._createShape(bodyId, ShapeType.Cone, (offset) => {
            this._shapeData[offset] = def.center.x;
            this._shapeData[offset + 1] = def.center.y;
            this._shapeData[offset + 2] = def.center.z;
            this._shapeData[offset + 3] = def.radius;
            this._shapeData[offset + 4] = def.height;
            this._shapeData[offset + 5] = def.axis ?? 1;
        });
    }

    createConvexHull(bodyId: BodyId3D, def: IConvexHullShapeDef3D): ShapeId3D {
        return this._createShape(bodyId, ShapeType.ConvexHull, (offset) => {
            this._shapeData[offset] = def.vertices.length;
        });
    }

    createTriangleMesh(bodyId: BodyId3D, def: ITriangleMeshShapeDef3D): ShapeId3D {
        return this._createShape(bodyId, ShapeType.TriangleMesh, (offset) => {
            this._shapeData[offset] = def.vertices.length;
            this._shapeData[offset + 1] = def.indices.length;
        });
    }

    createHeightField(bodyId: BodyId3D, def: IHeightFieldShapeDef3D): ShapeId3D {
        let minHeight = Infinity;
        let maxHeight = -Infinity;
        for (let index = 0; index < def.heights.length; index += 1) {
            const height = def.heights[index];
            if (height < minHeight) minHeight = height;
            if (height > maxHeight) maxHeight = height;
        }

        return this._createShape(bodyId, ShapeType.HeightField, (offset) => {
            this._shapeData[offset] = def.width;
            this._shapeData[offset + 1] = def.depth;
            this._shapeData[offset + 2] = def.scaleX;
            this._shapeData[offset + 3] = def.scaleY;
            this._shapeData[offset + 4] = def.scaleZ;
            this._shapeData[offset + 5] = Number.isFinite(minHeight) ? minHeight : 0;
            this._shapeData[offset + 6] = Number.isFinite(maxHeight) ? maxHeight : 0;
        });
    }

    destroyShape(shapeId: ShapeId3D): void {
        const index = this._shapeIdToIndex.get(shapeId);
        if (index === undefined) return;

        const bodyId = this._shapeToBody.get(shapeId);
        if (bodyId !== undefined) {
            const shapes = this._bodyToShapes.get(bodyId);
            if (shapes) {
                shapes.delete(shapeId);
                if (shapes.size === 0) this._bodyToShapes.delete(bodyId);
            }
        }

        this._shapeIdToIndex.delete(shapeId);
        this._shapeToBody.delete(shapeId);
        this._freeList.push(index);

        for (let i = 0; i < SHAPE_STRIDE; i++) {
            this._shapeData[index * SHAPE_STRIDE + i] = 0;
        }
        this._shapeTypes[index] = 0;
        this._shapeCount -= 1n;
    }

    getShapeType(shapeId: ShapeId3D): number { return Number(this._shapeTypes[this._getShapeIndex(shapeId)]); }
    getBodyForShape(shapeId: ShapeId3D): BodyId3D | undefined { return this._shapeToBody.get(shapeId); }
    getShapesForBody(bodyId: BodyId3D): readonly ShapeId3D[] {
        const shapes = this._bodyToShapes.get(bodyId);
        return shapes ? Array.from(shapes) : [];
    }

    private _createShape(
        bodyId: BodyId3D,
        type: ShapeType,
        initData: (offset: number) => void
    ): ShapeId3D {
        if (Number(this._shapeCount) >= this._maxShapes && this._freeList.length === 0) {
            throw new PhysicsError3D('Shape capacity exceeded', BodyManagerError.CAPACITY_EXCEEDED);
        }

        const shapeId = this._nextShapeId++ as ShapeId3D;
        const index = this._freeList.length > 0 ? this._freeList.pop()! : Number(this._shapeCount);

        this._shapeIdToIndex.set(shapeId, index);
        this._shapeToBody.set(shapeId, bodyId);

        let shapes = this._bodyToShapes.get(bodyId);
        if (!shapes) {
            shapes = new Set();
            this._bodyToShapes.set(bodyId, shapes);
        }
        shapes.add(shapeId);

        this._shapeTypes[index] = type;
        const dataOffset = index * SHAPE_STRIDE;
        initData(dataOffset);

        this._shapeCount += 1n;
        return shapeId;
    }

    #getShapeIndex(shapeId: ShapeId3D): number {
        const index = this._shapeIdToIndex.get(shapeId);
        if (index === undefined) throw new PhysicsError3D('Shape not found', BodyManagerError.INVALID_STATE);
        return index;
    }

    [Symbol.dispose](): void {
        this._shapeIdToIndex.clear();
        this._shapeToBody.clear();
        this._bodyToShapes.clear();
    }
}

export class ConstraintManager3D implements Disposable {
    private readonly _nextConstraintId = 1n;
    private readonly _constraintCount = 0n;
    private readonly _maxConstraints = 2048n;
    private readonly _constraintIdToIndex = new Map<ConstraintId3D, number>();
    private readonly _bodyToConstraints = new Map<BodyId3D, Set<ConstraintId3D>>();

    private readonly _constraintTypes = new Uint8Array(2048);
    private readonly _constraintData = new Float64Array(2048 * CONSTRAINT_STRIDE);

    constructor(maxConstraints: number = 2048) {
        this._maxConstraints = maxConstraints;
        this._constraintTypes.set(new Uint8Array(2048, 0));
        this._constraintData.set(new Float64Array(2048 * CONSTRAINT_STRIDE, 0));
    }

    get constraintCount(): number { return Number(this._constraintCount); }

    createFixedConstraint(def: IFixedConstraintDef3D): ConstraintId3D {
        return this._createConstraint(0, def.bodyIdA, def.bodyIdB, (offset) => {
            this._writeVec(offset, def.localAnchorA);
            this._writeVec(offset + 3, def.localAnchorB);
        });
    }

    createHingeConstraint(def: IHingeConstraintDef3D): ConstraintId3D {
        return this._createConstraint(2, def.bodyIdA, def.bodyIdB, (offset) => {
            this._writeVec(offset, def.localAnchorA);
            this._writeVec(offset + 3, def.localAnchorB);
            if (def.rotation) {
                this._writeQuat(offset + 6, def.rotation);
            } else {
                this._constraintData[offset + 9] = 1;
            }
        });
    }

    createSliderConstraint(def: ISliderConstraintDef3D): ConstraintId3D {
        return this._createConstraint(3, def.bodyIdA, def.bodyIdB, (offset) => {
            this._writeVec(offset, def.localAnchorA);
            this._writeVec(offset + 3, def.localAnchorB);
            if (def.rotation) {
                this._writeQuat(offset + 6, def.rotation);
            } else {
                this._constraintData[offset + 9] = 1;
            }
        });
    }

    createSpringConstraint(def: ISpringConstraintDef3D): ConstraintId3D {
        return this._createConstraint(6, def.bodyIdA, def.bodyIdB, (offset) => {
            this._writeVec(offset, def.localAnchorA);
            this._writeVec(offset + 3, def.localAnchorB);
            this._constraintData[offset + 6] = def.restLength ?? 1;
            this._constraintData[offset + 7] = def.stiffness ?? 10;
            this._constraintData[offset + 8] = def.damping ?? 0.5;
        });
    }

    createConeTwistConstraint(def: IConeTwistConstraintDef3D): ConstraintId3D {
        return this._createConstraint(4, def.bodyIdA, def.bodyIdB, (offset) => {
            this._writeVec(offset, def.localFrameA.position);
            this._writeVec(offset + 3, def.localFrameB.position);
            this._constraintData[offset + 6] = def.swingSpan1 ?? Math.PI * 0.25;
            this._constraintData[offset + 7] = def.swingSpan2 ?? Math.PI * 0.25;
            this._constraintData[offset + 8] = def.twistSpan ?? Math.PI * 0.5;
            this._constraintData[offset + 9] = def.softness ?? 1;
        });
    }

    createGenericConstraint(def: IGenericConstraintDef3D): ConstraintId3D {
        return this._createConstraint(5, def.bodyIdA, def.bodyIdB, (offset) => {
            this._writeVec(offset, def.localFrameA.position);
            this._writeVec(offset + 3, def.localFrameB.position);
        });
    }

    createFixed(def: IFixedConstraintDef3D): ConstraintId3D { return this.createFixedConstraint(def); }
    createHinge(def: IHingeConstraintDef3D): ConstraintId3D { return this.createHingeConstraint(def); }
    createSlider(def: ISliderConstraintDef3D): ConstraintId3D { return this.createSliderConstraint(def); }
    createSpring(def: ISpringConstraintDef3D): ConstraintId3D { return this.createSpringConstraint(def); }
    createConeTwist(def: IConeTwistConstraintDef3D): ConstraintId3D { return this.createConeTwistConstraint(def); }
    createGeneric(def: IGenericConstraintDef3D): ConstraintId3D { return this.createGenericConstraint(def); }

    destroyConstraint(constraintId: ConstraintId3D): void {
        const index = this._constraintIdToIndex.get(constraintId);
        if (index === undefined) return;

        // Find both bodies that own this constraint and remove the reference
        let bodyIdsFound = 0;
        for (const [body, constraints] of this._bodyToConstraints.entries()) {
            if (constraints.has(constraintId)) {
                constraints.delete(constraintId);
                bodyIdsFound++;
                if (constraints.size === 0) this._bodyToConstraints.delete(body);
                if (bodyIdsFound >= 2) break;
            }
        }

        this._constraintIdToIndex.delete(constraintId);
        this._freeList.push(index);
        this._constraintTypes[index] = 0;
        this._constraintCount -= 1n;
    }

    getConstraintType(constraintId: ConstraintId3D): number { return Number(this._constraintTypes[this._getConstraintIndex(constraintId)]); }
    getConstraintsForBody(bodyId: BodyId3D): readonly ConstraintId3D[] {
        const constraints = this._bodyToConstraints.get(bodyId);
        return constraints ? Array.from(constraints) : [];
    }

    private _createConstraint(
        type: number,
        bodyIdA: BodyId3D,
        bodyIdB: BodyId3D,
        initData: (offset: number) => void
    ): ConstraintId3D {
        if (Number(this._constraintCount) >= this._maxConstraints && this._freeList.length === 0) {
            throw new PhysicsError3D('Constraint capacity exceeded', BodyManagerError.CAPACITY_EXCEEDED);
        }

        const constraintId = this._nextConstraintId++ as ConstraintId3D;
        const index = this._freeList.length > 0 ? this._freeList.pop()! : Number(this._constraintCount);

        this._constraintIdToIndex.set(constraintId, index);
        this._constraintTypes[index] = type;

        for (const bodyId of [bodyIdA, bodyIdB]) {
            let constraints = this._bodyToConstraints.get(bodyId);
            if (!constraints) {
                constraints = new Set();
                this._bodyToConstraints.set(bodyId, constraints);
            }
            constraints.add(constraintId);
        }

        const dataOffset = index * CONSTRAINT_STRIDE;
        initData(dataOffset);

        this._constraintCount += 1n;
        return constraintId;
    }

    #getConstraintIndex(constraintId: ConstraintId3D): number {
        const index = this._constraintIdToIndex.get(constraintId);
        if (index === undefined) throw new PhysicsError3D('Constraint not found', BodyManagerError.INVALID_STATE);
        return index;
    }

    [Symbol.dispose](): void {
        this._constraintIdToIndex.clear();
        this._bodyToConstraints.clear();
        this._freeList.length = 0;
    }
}
