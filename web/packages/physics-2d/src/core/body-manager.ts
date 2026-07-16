import { EPSILON } from '@axrone/numeric';
import type { IVec2Like } from '@axrone/numeric';
import type {
    BodyId,
    ShapeId,
    BodyType,
    Mass,
    Inertia,
    IPhysicsBodyDef2D,
} from '../types';
import { BodyFlags } from '../types';
import {
    SoAManager,
    PhysicsError,
    assertFound,
    assertCapacity,
    type ManagerState,
    type ReadonlyVec2,
    type IVec2Output,
} from './foundation';

const BODY_SCHEMA = {
    posX:      { offset: 0,  size: 1 },
    posY:      { offset: 1,  size: 1 },
    rotation:  { offset: 2,  size: 1 },
    velX:      { offset: 3,  size: 1 },
    velY:      { offset: 4,  size: 1 },
    angVel:    { offset: 5,  size: 1 },
    forceX:    { offset: 6,  size: 1 },
    forceY:    { offset: 7,  size: 1 },
    torque:    { offset: 8,  size: 1 },
    mass:      { offset: 9,  size: 1 },
    invMass:   { offset: 10, size: 1 },
    inertia:   { offset: 11, size: 1 },
    invInertia:{ offset: 12, size: 1 },
    centerX:   { offset: 13, size: 1 },
    centerY:   { offset: 14, size: 1 },
    sleepTime: { offset: 15, size: 1 },
} as const;

type BodySchema = typeof BODY_SCHEMA;
type BodyField = keyof BodySchema;

type BodyManagerState = ManagerState;

class BodyPhysicsError extends PhysicsError {
    constructor(message: string, code: Parameters<typeof PhysicsError.prototype.withContext>[0] extends never ? never : never) {
        super(message, code as any);
        this.name = 'BodyPhysicsError';
    }
}

export class BodyManager2D extends SoAManager<BodySchema> {
    private _nextBodyId: number = 1;
    private readonly _bodyFlags: Uint32Array;
    private readonly _bodyTypes: Uint8Array;
    private readonly _bodyShapes: Map<BodyId, ShapeId[]>;
    private readonly _bodyIdToIndex: Map<BodyId, number>;
    private readonly _indexToBodyId: Map<number, BodyId>;
    private readonly _freeIndices: number[];
    private readonly _gravityScales: Float32Array;
    private readonly _dampingData: Float32Array;
    private readonly _userData: Map<BodyId, unknown>;

    constructor(maxBodies: number = 1024) {
        super(maxBodies, BODY_SCHEMA);
        this._state = 'active';
        this._bodyFlags = new Uint32Array(maxBodies);
        this._bodyTypes = new Uint8Array(maxBodies);
        this._bodyShapes = new Map();
        this._bodyIdToIndex = new Map();
        this._indexToBodyId = new Map();
        this._freeIndices = [];
        this._gravityScales = new Float32Array(maxBodies);
        this._dampingData = new Float32Array(maxBodies * 2);
        this._userData = new Map();
        this._gravityScales.fill(1.0);
    }

    get bodyCount(): number { return this._count; }

    createBody(def: IPhysicsBodyDef2D): BodyId {
        this._assertActive();
        assertCapacity(this._count, this._capacity, 'Body');

        const index = this._allocateIndex();
        const bodyId = this._nextBodyId++ as BodyId;

        this._bodyIdToIndex.set(bodyId, index);
        this._indexToBodyId.set(index, bodyId);

        const pos = def.position ?? { x: 0, y: 0 };
        this._writeVec2(index, 'posX', pos);
        this._writeScalar(index, 'rotation', def.rotation ?? 0);

        const linVel = def.linearVelocity ?? { x: 0, y: 0 };
        this._writeVec2(index, 'velX', linVel);
        this._writeScalar(index, 'angVel', def.angularVelocity ?? 0);

        this._writeScalar(index, 'forceX', 0);
        this._writeScalar(index, 'forceY', 0);
        this._writeScalar(index, 'torque', 0);
        this._writeScalar(index, 'mass', 0);
        this._writeScalar(index, 'invMass', 0);
        this._writeScalar(index, 'inertia', 0);
        this._writeScalar(index, 'invInertia', 0);
        this._writeScalar(index, 'centerX', 0);
        this._writeScalar(index, 'centerY', 0);
        this._writeScalar(index, 'sleepTime', 0);

        this._bodyTypes[index] = def.type;

        let flags = 0;
        if (def.awake !== false) flags |= BodyFlags.Awake;
        if (def.enabled !== false) flags |= BodyFlags.Active;
        if (def.fixedRotation) flags |= BodyFlags.FixedRotation;
        if (def.bullet) flags |= BodyFlags.Bullet;
        if (def.allowSleep !== false) flags |= BodyFlags.AutoSleep;
        this._bodyFlags[index] = flags;

        this._gravityScales[index] = def.gravityScale ?? 1.0;
        this._dampingData[index * 2] = def.linearDamping ?? 0;
        this._dampingData[index * 2 + 1] = def.angularDamping ?? 0;

        this._bodyShapes.set(bodyId, []);

        if (def.userData !== undefined) {
            this._userData.set(bodyId, def.userData);
        }

        this._count++;
        return bodyId;
    }

    destroyBody(bodyId: BodyId): void {
        this._assertActive();
        const index = this._bodyIdToIndex.get(bodyId);
        assertFound(index, 'Body', bodyId as unknown as number);

        this._bodyIdToIndex.delete(bodyId);
        this._indexToBodyId.delete(index);
        this._bodyShapes.delete(bodyId);
        this._userData.delete(bodyId);
        this._freeIndices.push(index);
        this._count--;
    }

    private _resolveIndex(bodyId: BodyId): number {
        const index = this._bodyIdToIndex.get(bodyId);
        if (index === undefined) {
            throw new PhysicsError(`Body ${bodyId} not found`, 'NOT_FOUND', { bodyId });
        }
        return index;
    }

    private _resolveIndexOrThrow(bodyId: BodyId): number {
        return this._resolveIndex(bodyId);
    }

    getPosition(bodyId: BodyId, out?: IVec2Output): IVec2Output {
        return this._readVec2(this._resolveIndex(bodyId), 'posX', out);
    }

    setPosition(bodyId: BodyId, position: ReadonlyVec2): void {
        this._writeVec2(this._resolveIndex(bodyId), 'posX', position);
    }

    getRotation(bodyId: BodyId): number {
        return this._readScalar(this._resolveIndex(bodyId), 'rotation');
    }

    setRotation(bodyId: BodyId, rotation: number): void {
        this._writeScalar(this._resolveIndex(bodyId), 'rotation', rotation);
    }

    getLinearVelocity(bodyId: BodyId, out?: IVec2Output): IVec2Output {
        return this._readVec2(this._resolveIndex(bodyId), 'velX', out);
    }

    setLinearVelocity(bodyId: BodyId, velocity: ReadonlyVec2): void {
        this._writeVec2(this._resolveIndex(bodyId), 'velX', velocity);
    }

    getAngularVelocity(bodyId: BodyId): number {
        return this._readScalar(this._resolveIndex(bodyId), 'angVel');
    }

    setAngularVelocity(bodyId: BodyId, velocity: number): void {
        this._writeScalar(this._resolveIndex(bodyId), 'angVel', velocity);
    }

    applyForce(bodyId: BodyId, force: ReadonlyVec2, point?: ReadonlyVec2): void {
        const i = this._resolveIndex(bodyId);
        this._data[this._getFieldOffset(i, 'forceX')] += force.x;
        this._data[this._getFieldOffset(i, 'forceY')] += force.y;

        if (point) {
            const cx = this._readScalar(i, 'posX') + this._readScalar(i, 'centerX');
            const cy = this._readScalar(i, 'posY') + this._readScalar(i, 'centerY');
            const rx = point.x - cx;
            const ry = point.y - cy;
            this._data[this._getFieldOffset(i, 'torque')] += rx * force.y - ry * force.x;
        }
    }

    applyForceToCenter(bodyId: BodyId, force: ReadonlyVec2): void {
        this.applyForce(bodyId, force);
    }

    applyTorque(bodyId: BodyId, torque: number): void {
        this._data[this._getFieldOffset(this._resolveIndex(bodyId), 'torque')] += torque;
    }

    applyImpulse(bodyId: BodyId, impulse: ReadonlyVec2, point?: ReadonlyVec2): void {
        const i = this._resolveIndex(bodyId);
        const invMass = this._readScalar(i, 'invMass');
        const velOffset = this._getFieldOffset(i, 'velX');
        this._data[velOffset] += impulse.x * invMass;
        this._data[velOffset + 1] += impulse.y * invMass;

        if (point) {
            const invI = this._readScalar(i, 'invInertia');
            const cx = this._readScalar(i, 'posX') + this._readScalar(i, 'centerX');
            const cy = this._readScalar(i, 'posY') + this._readScalar(i, 'centerY');
            const rx = point.x - cx;
            const ry = point.y - cy;
            this._data[this._getFieldOffset(i, 'angVel')] += invI * (rx * impulse.y - ry * impulse.x);
        }
    }

    applyImpulseToCenter(bodyId: BodyId, impulse: ReadonlyVec2): void {
        this.applyImpulse(bodyId, impulse);
    }

    applyAngularImpulse(bodyId: BodyId, impulse: number): void {
        const i = this._resolveIndex(bodyId);
        this._data[this._getFieldOffset(i, 'angVel')] += impulse * this._readScalar(i, 'invInertia');
    }

    getMass(bodyId: BodyId): Mass {
        return this._readScalar(this._resolveIndex(bodyId), 'mass') as Mass;
    }

    getInverseMass(bodyId: BodyId): number {
        return this._readScalar(this._resolveIndex(bodyId), 'invMass');
    }

    getInertia(bodyId: BodyId): Inertia {
        return this._readScalar(this._resolveIndex(bodyId), 'inertia') as Inertia;
    }

    getInverseInertia(bodyId: BodyId): number {
        return this._readScalar(this._resolveIndex(bodyId), 'invInertia');
    }

    getLocalCenter(bodyId: BodyId, out?: IVec2Output): IVec2Output {
        return this._readVec2(this._resolveIndex(bodyId), 'centerX', out);
    }

    setMassData(bodyId: BodyId, mass: number, inertia: number, center: ReadonlyVec2): void {
        const i = this._resolveIndex(bodyId);
        this._writeScalar(i, 'mass', mass);
        this._writeScalar(i, 'invMass', mass > EPSILON ? 1 / mass : 0);
        this._writeScalar(i, 'inertia', inertia);
        this._writeScalar(i, 'invInertia', inertia > EPSILON ? 1 / inertia : 0);
        this._writeVec2(i, 'centerX', center);
    }

    getBodyType(bodyId: BodyId): BodyType {
        return this._bodyTypes[this._resolveIndex(bodyId)] as BodyType;
    }

    setBodyType(bodyId: BodyId, type: BodyType): void {
        this._bodyTypes[this._resolveIndex(bodyId)] = type;
    }

    getFlags(bodyId: BodyId): BodyFlags {
        return this._bodyFlags[this._resolveIndex(bodyId)] as BodyFlags;
    }

    getGravityScale(bodyId: BodyId): number {
        return this._gravityScales[this._resolveIndex(bodyId)];
    }

    setGravityScale(bodyId: BodyId, gravityScale: number): void {
        this._gravityScales[this._resolveIndex(bodyId)] = gravityScale;
    }

    getLinearDamping(bodyId: BodyId): number {
        return this._dampingData[this._resolveIndex(bodyId) * 2];
    }

    setLinearDamping(bodyId: BodyId, damping: number): void {
        this._dampingData[this._resolveIndex(bodyId) * 2] = damping;
    }

    getAngularDamping(bodyId: BodyId): number {
        return this._dampingData[this._resolveIndex(bodyId) * 2 + 1];
    }

    setAngularDamping(bodyId: BodyId, damping: number): void {
        this._dampingData[this._resolveIndex(bodyId) * 2 + 1] = damping;
    }

    getSleepTime(bodyId: BodyId): number {
        return this._readScalar(this._resolveIndex(bodyId), 'sleepTime');
    }

    setSleepTime(bodyId: BodyId, time: number): void {
        this._writeScalar(this._resolveIndex(bodyId), 'sleepTime', time);
    }

    getUserData(bodyId: BodyId): unknown {
        return this._userData.get(bodyId);
    }

    setUserData(bodyId: BodyId, userData: unknown): void {
        if (userData === undefined) {
            this._userData.delete(bodyId);
        } else {
            this._userData.set(bodyId, userData);
        }
    }

    isAwake(bodyId: BodyId): boolean {
        return (this._bodyFlags[this._resolveIndex(bodyId)] & BodyFlags.Awake) !== 0;
    }

    isEnabled(bodyId: BodyId): boolean {
        return (this._bodyFlags[this._resolveIndex(bodyId)] & BodyFlags.Active) !== 0;
    }

    setEnabled(bodyId: BodyId, enabled: boolean): void {
        const i = this._resolveIndex(bodyId);
        if (enabled) {
            this._bodyFlags[i] |= BodyFlags.Active;
        } else {
            this._bodyFlags[i] &= ~BodyFlags.Active;
        }
    }

    isFixedRotation(bodyId: BodyId): boolean {
        return (this._bodyFlags[this._resolveIndex(bodyId)] & BodyFlags.FixedRotation) !== 0;
    }

    setFixedRotation(bodyId: BodyId, fixedRotation: boolean): void {
        const i = this._resolveIndex(bodyId);
        if (fixedRotation) {
            this._bodyFlags[i] |= BodyFlags.FixedRotation;
        } else {
            this._bodyFlags[i] &= ~BodyFlags.FixedRotation;
        }
    }

    isBullet(bodyId: BodyId): boolean {
        return (this._bodyFlags[this._resolveIndex(bodyId)] & BodyFlags.Bullet) !== 0;
    }

    setBullet(bodyId: BodyId, bullet: boolean): void {
        const i = this._resolveIndex(bodyId);
        if (bullet) {
            this._bodyFlags[i] |= BodyFlags.Bullet;
        } else {
            this._bodyFlags[i] &= ~BodyFlags.Bullet;
        }
    }

    setAwake(bodyId: BodyId, awake: boolean): void {
        const i = this._resolveIndex(bodyId);
        if (awake) {
            this._bodyFlags[i] |= BodyFlags.Awake;
            this._writeScalar(i, 'sleepTime', 0);
        } else {
            this._bodyFlags[i] &= ~BodyFlags.Awake;
            const velOffset = this._getFieldOffset(i, 'velX');
            this._data[velOffset] = 0;
            this._data[velOffset + 1] = 0;
            this._writeScalar(i, 'angVel', 0);
            const forceOffset = this._getFieldOffset(i, 'forceX');
            this._data[forceOffset] = 0;
            this._data[forceOffset + 1] = 0;
            this._writeScalar(i, 'torque', 0);
        }
    }

    clearForces(): void {
        for (const [, index] of this._bodyIdToIndex) {
            const forceOffset = this._getFieldOffset(index, 'forceX');
            this._data[forceOffset] = 0;
            this._data[forceOffset + 1] = 0;
            this._writeScalar(index, 'torque', 0);
        }
    }

    getBodyIds(): IterableIterator<BodyId> {
        return this._bodyIdToIndex.keys();
    }

    hasBody(bodyId: BodyId): boolean {
        return this._bodyIdToIndex.has(bodyId);
    }

    private _allocateIndex(): number {
        if (this._freeIndices.length > 0) {
            return this._freeIndices.pop()!;
        }
        return this._count;
    }

    [Symbol.dispose](): void {
        if (this._state === 'disposed') return;
        this._state = 'disposed';
        this._bodyIdToIndex.clear();
        this._indexToBodyId.clear();
        this._bodyShapes.clear();
        this._userData.clear();
        this._freeIndices.length = 0;
    }
}
