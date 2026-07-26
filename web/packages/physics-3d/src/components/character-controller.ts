import { Vec3, type IVec3Like } from '@axrone/numeric';
import { script } from '@axrone/ecs-runtime/decorators';
import { Component } from '@axrone/ecs-runtime';
import type {
    BodyId3D,
    Density,
    Friction,
    IRaycastResult3D,
    Restitution,
    ShapeId3D,
} from '../types';
import type { BodyManager3D, PhysicsWorld3D, ShapeManager3D } from '../core/physics-world-3d';
import { syncTransformWorldPosition } from '../core/transform-sync';
import type { Rigidbody3D } from './rigidbody3d';

const enum CollisionFlags {
    None = 0,
    Sides = 1 << 0,
    Above = 1 << 1,
    Below = 1 << 2,
}

interface ICharacterControllerConfig {
    radius?: number;
    height?: number;
    center?: IVec3Like;
    slopeLimit?: number;
    stepOffset?: number;
    skinWidth?: number;
    minMoveDistance?: number;
    enableOverlapRecovery?: boolean;
}

const DEFAULT_RADIUS = 0.5;
const DEFAULT_HEIGHT = 2;
const DEFAULT_SLOPE_LIMIT = 45;
const DEFAULT_STEP_OFFSET = 0.3;
const DEFAULT_SKIN_WIDTH = 0.08;
const DEFAULT_MIN_MOVE_DISTANCE = 0.001;
const GRAVITY_ACCELERATION = -9.81;
const MAX_SLOPE_RECOVERY_ITERATIONS = 4;

@script({ scriptName: 'CharacterController' })
export class CharacterController extends Component {
    private _world: PhysicsWorld3D | null = null;
    private _bodyManager: BodyManager3D | null = null;
    private _shapeManager: ShapeManager3D | null = null;
    private _bodyId: BodyId3D = -1 as BodyId3D;
    private _shapeId: ShapeId3D = -1 as ShapeId3D;
    private _ccEnabled: boolean = true;

    private _radius: number = DEFAULT_RADIUS;
    private _height: number = DEFAULT_HEIGHT;
    private readonly _center: Vec3 = Vec3.create();
    private _slopeLimit: number = DEFAULT_SLOPE_LIMIT;
    private _stepOffset: number = DEFAULT_STEP_OFFSET;
    private _skinWidth: number = DEFAULT_SKIN_WIDTH;
    private _minMoveDistance: number = DEFAULT_MIN_MOVE_DISTANCE;
    private _enableOverlapRecovery: boolean = true;

    private readonly _velocity: Vec3 = Vec3.create();
    private readonly _groundNormal: Vec3 = new Vec3(0, 1, 0);
    private _isGrounded: boolean = false;
    private _collisionFlags: CollisionFlags = CollisionFlags.None;
    private _useGravity: boolean = true;
    private _detectCollisions: boolean = true;

    private readonly _pendingMovement: Vec3 = Vec3.create();
    private _verticalVelocity: number = 0;

    get radius(): number {
        return this._radius;
    }
    set radius(value: number) {
        this._radius = Math.max(0.01, value);
        this._recreateShape();
    }
    get height(): number {
        return this._height;
    }
    set height(value: number) {
        this._height = Math.max(this._radius * 2, value);
        this._recreateShape();
    }
    get center(): Readonly<Vec3> {
        return this._center;
    }
    set center(value: IVec3Like) {
        this._center.x = value.x;
        this._center.y = value.y;
        this._center.z = value.z;
    }
    get slopeLimit(): number {
        return this._slopeLimit;
    }
    set slopeLimit(value: number) {
        this._slopeLimit = Math.max(0, Math.min(90, value));
    }
    get stepOffset(): number {
        return this._stepOffset;
    }
    set stepOffset(value: number) {
        this._stepOffset = Math.max(0, value);
    }
    get skinWidth(): number {
        return this._skinWidth;
    }
    set skinWidth(value: number) {
        this._skinWidth = Math.max(0.001, value);
    }
    get minMoveDistance(): number {
        return this._minMoveDistance;
    }
    set minMoveDistance(value: number) {
        this._minMoveDistance = Math.max(0, value);
    }
    get enableOverlapRecovery(): boolean {
        return this._enableOverlapRecovery;
    }
    set enableOverlapRecovery(value: boolean) {
        this._enableOverlapRecovery = value;
    }
    get velocity(): Readonly<Vec3> {
        return this._velocity;
    }
    get isGrounded(): boolean {
        return this._isGrounded;
    }
    get collisionFlags(): CollisionFlags {
        return this._collisionFlags;
    }
    get detectCollisions(): boolean {
        return this._detectCollisions;
    }
    set detectCollisions(value: boolean) {
        this._detectCollisions = value;
    }
    get useGravity(): boolean {
        return this._useGravity;
    }
    set useGravity(value: boolean) {
        this._useGravity = value;
    }

    initialize(world: PhysicsWorld3D, config: ICharacterControllerConfig = {}): void {
        this._world = world;
        this._bodyManager = world.getBodyManager();
        this._shapeManager = world.getShapeManager();
        this._applyConfig(config);
        this._createBody();
    }

    move(motion: IVec3Like): CollisionFlags {
        if (!this._bodyManager || this._bodyId === -1 || !this._ccEnabled)
            return CollisionFlags.None;
        const len = Math.sqrt(motion.x * motion.x + motion.y * motion.y + motion.z * motion.z);
        if (len < this._minMoveDistance) return CollisionFlags.None;
        const position = this._bodyManager.getPosition(this._bodyId);
        const newPos = this._performMove(position, motion);
        this._bodyManager.setPosition(this._bodyId, newPos);
        syncTransformWorldPosition(this.transform, newPos);
        this._updateGroundState();
        return this._collisionFlags;
    }

    simpleMove(speed: IVec3Like): boolean {
        if (!this._bodyManager || this._bodyId === -1 || !this._ccEnabled) return false;
        this._pendingMovement.x = speed.x;
        this._pendingMovement.y = speed.y;
        this._pendingMovement.z = speed.z;
        return this._isGrounded;
    }

    override fixedUpdate(deltaTime: number): void {
        if (!this._bodyManager || this._bodyId === -1 || !this._ccEnabled) return;
        const position = this._bodyManager.getPosition(this._bodyId);
        const motion = { x: 0, y: 0, z: 0 };
        if (
            this._pendingMovement.x !== 0 ||
            this._pendingMovement.y !== 0 ||
            this._pendingMovement.z !== 0
        ) {
            motion.x += this._pendingMovement.x * deltaTime;
            motion.y += this._pendingMovement.y * deltaTime;
            motion.z += this._pendingMovement.z * deltaTime;
            this._pendingMovement.x = 0;
            this._pendingMovement.y = 0;
            this._pendingMovement.z = 0;
        }
        if (this._useGravity && !this._isGrounded) {
            this._verticalVelocity += GRAVITY_ACCELERATION * deltaTime;
            motion.y += this._verticalVelocity * deltaTime;
        } else if (this._isGrounded) {
            this._verticalVelocity = 0;
        }
        const newPosition = this._performMove(position, motion);
        if (
            newPosition.x !== position.x ||
            newPosition.y !== position.y ||
            newPosition.z !== position.z
        ) {
            this._bodyManager.setPosition(this._bodyId, newPosition);
            syncTransformWorldPosition(this.transform, newPosition);
            this._velocity.x = (newPosition.x - position.x) / deltaTime;
            this._velocity.y = (newPosition.y - position.y) / deltaTime;
            this._velocity.z = (newPosition.z - position.z) / deltaTime;
        }
        this._updateGroundState();
        if (this._isGrounded && this._verticalVelocity < 0) {
            this._verticalVelocity = 0;
        }
    }

    override onDestroy(): void {
        if (this._shapeId !== -1) {
            if (this._world) {
                this._world.destroyShape(this._shapeId);
            } else if (this._shapeManager) {
                this._shapeManager.destroyShape(this._shapeId);
            }
            this._shapeId = -1 as ShapeId3D;
        }
        if (this._bodyManager && this._bodyId !== -1) {
            this._bodyManager.destroyBody(this._bodyId);
            this._bodyId = -1 as BodyId3D;
        }
        this._bodyManager = null;
        this._shapeManager = null;
        this._world = null;
    }

    private _applyConfig(config: ICharacterControllerConfig): void {
        if (config.radius !== undefined) this._radius = Math.max(0.01, config.radius);
        if (config.height !== undefined) this._height = Math.max(this._radius * 2, config.height);
        if (config.center) {
            this._center.x = config.center.x;
            this._center.y = config.center.y;
            this._center.z = config.center.z;
        }
        if (config.slopeLimit !== undefined)
            this._slopeLimit = Math.max(0, Math.min(90, config.slopeLimit));
        if (config.stepOffset !== undefined) this._stepOffset = Math.max(0, config.stepOffset);
        if (config.skinWidth !== undefined) this._skinWidth = Math.max(0.001, config.skinWidth);
        if (config.minMoveDistance !== undefined)
            this._minMoveDistance = Math.max(0, config.minMoveDistance);
        if (config.enableOverlapRecovery !== undefined)
            this._enableOverlapRecovery = config.enableOverlapRecovery;
    }

    private _createBody(): void {
        if (!this._bodyManager || !this._shapeManager) return;
        const worldPos = this.transform?.worldPosition ?? Vec3.ZERO;
        this._bodyId = this._bodyManager.createBody({
            type: 1,
            position: worldPos,
            gravityScale: 0,
            fixedRotation: true,
            allowSleep: false,
            awake: true,
            enabled: this._ccEnabled,
        });
        this._createOrReplaceShape();
    }

    private _createOrReplaceShape(): void {
        if (!this._shapeManager || this._bodyId === -1) return;

        const halfHeight = (this._height - this._radius * 2) * 0.5;
        const shapeDef = {
            p1: { x: this._center.x, y: this._center.y - halfHeight, z: this._center.z },
            p2: { x: this._center.x, y: this._center.y + halfHeight, z: this._center.z },
            radius: this._radius,
        };
        const material = {
            friction: 0 as unknown as Friction,
            restitution: 0 as unknown as Restitution,
            density: 1 as unknown as Density,
        };
        const filter = { categoryBits: 1, maskBits: 0xffff, groupIndex: 0 };

        if (this._world) {
            this._shapeId = this._world.createCapsuleShape(
                this._bodyId,
                shapeDef,
                material,
                filter
            );
            return;
        }

        this._shapeId = this._shapeManager.createCapsule(this._bodyId, shapeDef, material, filter);
    }

    private _recreateShape(): void {
        if (this._shapeId !== -1) {
            if (this._world) {
                this._world.destroyShape(this._shapeId);
            } else if (this._shapeManager) {
                this._shapeManager.destroyShape(this._shapeId);
            }
            this._shapeId = -1 as ShapeId3D;
        }
        if (this._bodyId !== -1) this._createOrReplaceShape();
    }

    private _updateGroundState(): void {
        if (!this._bodyManager || !this._world || this._bodyId === -1) return;
        this._groundNormal.x = 0;
        this._groundNormal.y = 1;
        this._groundNormal.z = 0;
        this._isGrounded = false;
        this._collisionFlags &= ~CollisionFlags.Below;

        const position = this._bodyManager.getPosition(this._bodyId);
        const groundHit = this._getGroundHit(position);
        if (!groundHit) {
            return;
        }

        this._groundNormal.x = groundHit.normal.x;
        this._groundNormal.y = groundHit.normal.y;
        this._groundNormal.z = groundHit.normal.z;

        const cosSlope = Math.cos((this._slopeLimit * Math.PI) / 180);
        this._isGrounded = groundHit.normal.y >= cosSlope;
        if (this._isGrounded) {
            this._collisionFlags |= CollisionFlags.Below;
        }
    }

    private _performMove(from: IVec3Like, motion: IVec3Like): IVec3Like {
        let resultX = from.x;
        let resultY = from.y;
        let resultZ = from.z;
        this._collisionFlags = CollisionFlags.None;

        if (!this._detectCollisions || !this._world) {
            return { x: from.x + motion.x, y: from.y + motion.y, z: from.z + motion.z };
        }

        if (motion.x !== 0) {
            const candidate = { x: from.x + motion.x, y: from.y, z: from.z };
            if (this._hasBlockingOverlap(candidate, true)) {
                this._collisionFlags |= CollisionFlags.Sides;
            } else {
                resultX = candidate.x;
            }
        }

        if (motion.z !== 0) {
            const candidate = { x: resultX, y: from.y, z: from.z + motion.z };
            if (this._hasBlockingOverlap(candidate, true)) {
                this._collisionFlags |= CollisionFlags.Sides;
            } else {
                resultZ = candidate.z;
            }
        }

        const verticalCandidate = { x: resultX, y: from.y + motion.y, z: resultZ };
        if (motion.y > 0 && this._hasBlockingOverlap(verticalCandidate, false)) {
            this._collisionFlags |= CollisionFlags.Above;
        } else {
            resultY = verticalCandidate.y;
        }

        const snapped = this._snapToGround({ x: resultX, y: resultY, z: resultZ }, motion.y <= 0);
        if (snapped.y !== resultY) {
            resultY = snapped.y;
            this._collisionFlags |= CollisionFlags.Below;
        }

        return { x: resultX, y: resultY, z: resultZ };
    }

    private _hasBlockingOverlap(position: IVec3Like, horizontalOnly: boolean): boolean {
        if (!this._world) return false;

        const bounds = this._getControllerBounds(position, horizontalOnly);
        const shapeIds = this._world.queryAABBAll(bounds.min, bounds.max);
        return shapeIds.some((shapeId) => shapeId !== this._shapeId);
    }

    private _getControllerBounds(
        position: IVec3Like,
        horizontalOnly: boolean
    ): { min: IVec3Like; max: IVec3Like } {
        const halfBodyHeight = this._radius + (this._height - this._radius * 2) * 0.5;
        const center = {
            x: position.x + this._center.x,
            y: position.y + this._center.y,
            z: position.z + this._center.z,
        };
        const minY = horizontalOnly
            ? center.y - Math.max(0.001, halfBodyHeight - this._skinWidth * 2)
            : center.y - halfBodyHeight;
        const maxY = horizontalOnly
            ? center.y + Math.max(0.001, halfBodyHeight - this._skinWidth * 2)
            : center.y + halfBodyHeight;

        return {
            min: {
                x: center.x - this._radius,
                y: minY,
                z: center.z - this._radius,
            },
            max: {
                x: center.x + this._radius,
                y: maxY,
                z: center.z + this._radius,
            },
        };
    }

    private _getGroundHit(position: IVec3Like): IRaycastResult3D | null {
        if (!this._world) return null;

        const halfBodyHeight = this._radius + (this._height - this._radius * 2) * 0.5;
        const origin = {
            x: position.x + this._center.x,
            y: position.y + this._center.y,
            z: position.z + this._center.z,
        };
        const maxDistance = halfBodyHeight + this._stepOffset + this._skinWidth + 0.05;
        const hits = this._world.rayCastAll(origin, { x: 0, y: -1, z: 0 }, maxDistance);

        for (const hit of hits) {
            if (hit.shapeId !== this._shapeId) {
                return hit;
            }
        }

        return null;
    }

    private _snapToGround(position: IVec3Like, allowSnap: boolean): IVec3Like {
        if (!allowSnap) return position;

        const hit = this._getGroundHit(position);
        if (!hit) return position;

        const cosSlope = Math.cos((this._slopeLimit * Math.PI) / 180);
        if (hit.normal.y < cosSlope) {
            return position;
        }

        const halfBodyHeight = this._radius + (this._height - this._radius * 2) * 0.5;
        const targetY = hit.point.y + halfBodyHeight - this._center.y;
        if (Math.abs(targetY - position.y) > this._stepOffset + this._skinWidth + 0.05) {
            return position;
        }

        return { x: position.x, y: targetY, z: position.z };
    }
}

export { CollisionFlags };
export type { ICharacterControllerConfig };
