import { Vec3, type IVec3Like, type IQuatLike } from '@axrone/numeric';
import { Component } from '@axrone/ecs-runtime';
import type { ConstraintId3D } from '../types';
import type { ConstraintManager3D, PhysicsWorld3D } from '../core/physics-world-3d';
import { transformPoint3D } from '../core/physics-world-3d-shared';
import type { Rigidbody3D } from './rigidbody3d';

export const INVALID_CONSTRAINT_ID = -1 as ConstraintId3D;

/**
 * 3D joint solver capability matrix.
 *
 * | Joint type      | Constraint type | Solver status | Notes |
 * |-----------------|-----------------|---------------|-------|
 * | FixedJoint3D    | FIXED (0)       | **FULL** — Baumgarte + sequential impulse, distance preserved | |
 * | DistanceJoint3D | FIXED (0)       | **FULL** — same solver path as Fixed | |
 * | SpringJoint3D   | SPRING (6)      | **FULL** — spring force + Baumgarte | |
 * | HingeJoint3D    | HINGE (2)       | **FULL** — 3 linear + 2 angular lock + 1 axial (limit/motor), Box2D stall semantics | |
 * | SliderJoint3D   | SLIDER (3)      | **PARTIAL** — limit+motor work; `useSpring`/`spring` properties exposed but NOT wired to solver (silent no-op) | |
 * | CharacterJoint3D| CONE_TWIST (4)  | **FULL** — swing/twist limits + twist motor (motorSpeed/maxMotorTorque wired) | |
 * | ConfigurableJoint3D | GENERIC (5) | **FULL** — 6-DOF limit/lock + per-axis linear/angular motors wired | |
 *
 * Units: All distances in METRES (ADR 0004), angles in radians.
 *
 * 2D/3D asymmetry: 2D has 10 joint types all at FULL. 3D has 7 types,
 * Slider PARTIAL due to unwired spring properties (separate scope).
 *
 * @see Joint3D
 */
export const JOINT_CAPABILITY_3D = {
    FIXED: 'full',
    DISTANCE: 'full',
    SPRING: 'full',
    HINGE: 'full',
    SLIDER: 'partial',
    CONE_TWIST: 'full',
    GENERIC: 'full',
} as const;

export type JointCapability3D = typeof JOINT_CAPABILITY_3D[keyof typeof JOINT_CAPABILITY_3D];

export const enum JointDriveMode3D {
    None = 0,
    Position = 1,
    Velocity = 2,
    PositionAndVelocity = 3,
}

export interface IJointDrive3D {
    positionSpring: number;
    positionDamper: number;
    maximumForce: number;
    useAcceleration: boolean;
}
export interface IJointLimits3D {
    min: number;
    max: number;
    bounciness: number;
    contactDistance: number;
}
export interface IJointMotor3D {
    targetVelocity: number;
    force: number;
    freeSpin: boolean;
}
export interface ISoftJointLimit3D {
    limit: number;
    bounciness: number;
    contactDistance: number;
}
export interface ISoftJointLimitSpring3D {
    spring: number;
    damper: number;
}

export const DEFAULT_JOINT_DRIVE: Readonly<IJointDrive3D> = {
    positionSpring: 0,
    positionDamper: 0,
    maximumForce: Infinity,
    useAcceleration: false,
};
export const DEFAULT_JOINT_MOTOR: Readonly<IJointMotor3D> = {
    targetVelocity: 0,
    force: 0,
    freeSpin: false,
};
export const DEFAULT_SOFT_JOINT_LIMIT: Readonly<ISoftJointLimit3D> = {
    limit: 0,
    bounciness: 0,
    contactDistance: 0,
};
export const DEFAULT_SOFT_JOINT_LIMIT_SPRING: Readonly<ISoftJointLimitSpring3D> = {
    spring: 0,
    damper: 0,
};

/**
 * Base class for all 3D joint components.
 *
 * IMPORTANT: Not all joint capabilities are fully wired. See {@link JOINT_CAPABILITY_3D}
 * for the current solver capability matrix. Joints marked as "partial" have some
 * features implemented in the solver but not exposed through the component API,
 * or expose properties that are not yet wired to the solver (silent no-ops).
 *
 * All distance units are METRES (ADR 0004). Angles are in radians.
 *
 * ## Reference serialisation contract (Editor ↔ Engine)
 *
 * Component references (`connectedBody`) are stored as **strings** in the
 * Editor (entity/component IDs), with `""` meaning "no reference". The engine
 * stores them as typed object references (`Rigidbody3D | null`).
 *
 * - `serialize()` emits `""` when the reference is `null`, matching the
 *   Editor's expected default.
 * - `deserialize()` normalises `""`, `undefined`, `null`, and whitespace-only
 *   strings to `null`. Non-empty string values are stored as-is (the scene
 *   loader resolves them to component references via entity relationships
 *   after `deserialize()` completes).
 * - Use {@link normalizeReferenceValue} to apply this rule in subclasses.
 *
 * This contract is **identical** to the 2D `Joint2D` base class contract.
 * The helper is duplicated locally (not in `physics-core`) — see 2D/3D
 * asymmetry note in `joint2d.ts`.
 *
 * @see JOINT_CAPABILITY_3D
 */
export abstract class Joint3D extends Component {
    protected _constraintId: ConstraintId3D = INVALID_CONSTRAINT_ID;
    protected _constraintManager: ConstraintManager3D | null = null;
    protected _world: PhysicsWorld3D | null = null;
    protected _ownerBody: Rigidbody3D | null = null;
    protected _joint3dEnabled: boolean = true;
    protected _connectedBody: Rigidbody3D | null = null;
    protected _autoConfigureConnectedAnchor: boolean = true;
    protected readonly _anchor: Vec3 = Vec3.create();
    protected readonly _connectedAnchor: Vec3 = Vec3.create();
    protected readonly _axis: Vec3 = new Vec3(1, 0, 0);
    protected readonly _secondaryAxis: Vec3 = new Vec3(0, 1, 0);
    protected _breakForce: number = Infinity;
    protected _breakTorque: number = Infinity;
    protected _enableCollision: boolean = false;
    protected _enablePreprocessing: boolean = true;
    protected _massScale: number = 1;
    protected _connectedMassScale: number = 1;
    private readonly _currentForce: Vec3 = Vec3.create();
    private readonly _currentTorque: Vec3 = Vec3.create();

    get constraintId(): ConstraintId3D {
        return this._constraintId;
    }
    get connectedBody(): Rigidbody3D | null {
        return this._connectedBody;
    }
    set connectedBody(value: Rigidbody3D | null) {
        if (this._connectedBody === value) return;
        this._connectedBody = value;
        if (this._autoConfigureConnectedAnchor && value) this._configureConnectedAnchor();
        this._recreateConstraint();
    }
    get autoConfigureConnectedAnchor(): boolean {
        return this._autoConfigureConnectedAnchor;
    }
    set autoConfigureConnectedAnchor(value: boolean) {
        this._autoConfigureConnectedAnchor = value;
        if (value && this._connectedBody) {
            this._configureConnectedAnchor();
            this._recreateConstraint();
        }
    }
    get anchor(): Readonly<Vec3> {
        return this._anchor;
    }
    set anchor(value: IVec3Like) {
        this._anchor.x = value.x;
        this._anchor.y = value.y;
        this._anchor.z = value.z;
        if (this._autoConfigureConnectedAnchor) this._configureConnectedAnchor();
        this._updateConstraint();
    }
    get connectedAnchor(): Readonly<Vec3> {
        return this._connectedAnchor;
    }
    set connectedAnchor(value: IVec3Like) {
        this._autoConfigureConnectedAnchor = false;
        this._connectedAnchor.x = value.x;
        this._connectedAnchor.y = value.y;
        this._connectedAnchor.z = value.z;
        this._updateConstraint();
    }
    get axis(): Readonly<Vec3> {
        return this._axis;
    }
    set axis(value: IVec3Like) {
        if (Vec3.len(value) < 1e-6) return;
        Vec3.normalize(value, this._axis);
        this._updateConstraint();
    }
    get secondaryAxis(): Readonly<Vec3> {
        return this._secondaryAxis;
    }
    set secondaryAxis(value: IVec3Like) {
        if (Vec3.len(value) < 1e-6) return;
        Vec3.normalize(value, this._secondaryAxis);
        this._updateConstraint();
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
    get enableCollision(): boolean {
        return this._enableCollision;
    }
    set enableCollision(value: boolean) {
        this._enableCollision = value;
        this._updateConstraint();
    }
    get enablePreprocessing(): boolean {
        return this._enablePreprocessing;
    }
    set enablePreprocessing(value: boolean) {
        this._enablePreprocessing = value;
    }
    get massScale(): number {
        return this._massScale;
    }
    set massScale(value: number) {
        this._massScale = Math.max(0.0001, value);
    }
    get connectedMassScale(): number {
        return this._connectedMassScale;
    }
    set connectedMassScale(value: number) {
        this._connectedMassScale = Math.max(0.0001, value);
    }
    get currentForce(): Readonly<IVec3Like> {
        return this._currentForce;
    }
    get currentTorque(): Readonly<IVec3Like> {
        return this._currentTorque;
    }

    initialize(world: PhysicsWorld3D, ownerBody: Rigidbody3D, connectedBody?: Rigidbody3D): void {
        this._world = world;
        this._constraintManager = world.getConstraintManager();
        this._ownerBody = ownerBody;
        this._connectedBody = connectedBody ?? null;
        this._createConstraint(ownerBody);
    }

    override fixedUpdate(deltaTime: number): void {
        if (!this._joint3dEnabled) return;
        this._checkBreakForce();
    }

    override onDestroy(): void {
        if (this._constraintManager && this._constraintId !== INVALID_CONSTRAINT_ID) {
            this._constraintManager.destroyConstraint(this._constraintId);
            this._constraintId = INVALID_CONSTRAINT_ID;
        }
        this._constraintManager = null;
        this._world = null;
        this._ownerBody = null;
        this._connectedBody = null;
    }

    protected abstract _createConstraint(ownerBody: Rigidbody3D): void;
    protected abstract _updateConstraint(): void;

    protected _recreateConstraint(): void {
        if (this._constraintId !== INVALID_CONSTRAINT_ID) {
            // Use world's destroyConstraint to also clean up the constraint descriptor
            if (this._world) {
                this._world.destroyConstraint(this._constraintId);
            } else if (this._constraintManager) {
                this._constraintManager.destroyConstraint(this._constraintId);
            }
        }

        this._constraintId = INVALID_CONSTRAINT_ID;

        if (!this._constraintManager || !this._ownerBody) {
            return;
        }

        this._createConstraint(this._ownerBody);
    }

    protected _configureConnectedAnchor(): void {
        if (!this._connectedBody || !this.transform) return;
        const worldAnchor = this._getWorldAnchor();
        const connectedPos = this._connectedBody.position;
        this._connectedAnchor.x = worldAnchor.x - connectedPos.x;
        this._connectedAnchor.y = worldAnchor.y - connectedPos.y;
        this._connectedAnchor.z = worldAnchor.z - connectedPos.z;
    }

    protected _getWorldAnchor(): IVec3Like {
        if (!this.transform) return this._anchor;
        const pos = this.transform.worldPosition;
        const rot = this.transform.worldRotation;
        return this._transformPoint(pos, rot, this._anchor);
    }

    protected _transformPoint(pos: IVec3Like, rot: IQuatLike, localPoint: IVec3Like): IVec3Like {
        return transformPoint3D(localPoint, pos, rot);
    }

    protected _calculatePerpendicularAxis(): IVec3Like {
        const ax = this._axis.x;
        const ay = this._axis.y;
        const az = this._axis.z;
        let perpX: number;
        let perpY: number;
        let perpZ: number;
        if (Math.abs(ax) < 0.9) {
            perpX = ay;
            perpY = -ax;
            perpZ = 0;
        } else {
            perpX = 0;
            perpY = az;
            perpZ = -ay;
        }
        const invLen = 1 / Math.sqrt(perpX * perpX + perpY * perpY + perpZ * perpZ);
        return { x: perpX * invLen, y: perpY * invLen, z: perpZ * invLen };
    }

    protected _checkBreakForce(): void {
        if (!this._constraintManager || this._constraintId === INVALID_CONSTRAINT_ID) return;
        const forceLen = Vec3.len(this._currentForce);
        const torqueLen = Vec3.len(this._currentTorque);
        if (forceLen > this._breakForce || torqueLen > this._breakTorque) {
            this._constraintManager.destroyConstraint(this._constraintId);
            this._constraintId = INVALID_CONSTRAINT_ID;
        }
    }

    /**
     * Normalise a serialised Vec3 value to {x, y, z} object format.
     *
     * The Editor stores Vec3 values as **arrays** `[x, y, z]` in scene JSON
     * (source: `Editor/src-tauri/src/scene/components.rs` `*_properties()`).
     * The engine's internal representation uses `{x, y, z}` objects (IVec3Like).
     *
     * This helper accepts BOTH formats for backward compatibility:
     * - Array: `[x, y, z]` → `{x, y, z}` (Editor contract)
     * - Object: `{x, y, z}` → pass-through (engine round-trip)
     *
     * @see normalizeReferenceValue for the reference normalisation pattern.
     * @see JOINT_CAPABILITY_3D for the joint capability matrix.
     */
    protected normalizeVec3Value(
        value: unknown,
        fallbackX: number = 0,
        fallbackY: number = 0,
        fallbackZ: number = 0
    ): IVec3Like {
        if (Array.isArray(value) && value.length >= 3) {
            return { x: value[0], y: value[1], z: value[2] };
        }
        if (value && typeof value === 'object') {
            const v = value as Record<string, unknown>;
            return {
                x: typeof v.x === 'number' ? v.x : fallbackX,
                y: typeof v.y === 'number' ? v.y : fallbackY,
                z: typeof v.z === 'number' ? v.z : fallbackZ,
            };
        }
        return { x: fallbackX, y: fallbackY, z: fallbackZ };
    }

    /**
     * Normalise a serialised Quat value to {x, y, z, w} object format.
     *
     * Same dual-format support as {@link normalizeVec3Value}.
     */
    protected normalizeQuatValue(
        value: unknown,
        fallbackX: number = 0,
        fallbackY: number = 0,
        fallbackZ: number = 0,
        fallbackW: number = 1
    ): { x: number; y: number; z: number; w: number } {
        if (Array.isArray(value) && value.length >= 4) {
            return { x: value[0], y: value[1], z: value[2], w: value[3] };
        }
        if (value && typeof value === 'object') {
            const v = value as Record<string, unknown>;
            return {
                x: typeof v.x === 'number' ? v.x : fallbackX,
                y: typeof v.y === 'number' ? v.y : fallbackY,
                z: typeof v.z === 'number' ? v.z : fallbackZ,
                w: typeof v.w === 'number' ? v.w : fallbackW,
            };
        }
        return { x: fallbackX, y: fallbackY, z: fallbackZ, w: fallbackW };
    }

    /**
     * Normalise a serialised reference value to the engine's internal
     * representation.
     *
     * The Editor stores component references as strings (`""` = no reference).
     * This method converts `""`, `null`, `undefined`, and whitespace-only
     * strings to `null`. Any other value (entity ID string, or already-resolved
     * component reference) is returned as-is for the scene loader to resolve.
     *
     * NOTE: This is a local duplicate of the identical helper in
     * `physics-2d/src/components/joint2d.ts` (Joint2D.normalizeReferenceValue).
     * Both MUST change together. Not moved to physics-core to avoid cross-package
     * dependency churn for a 7-line pure function.
     *
     * @param value - Raw value from serialised data (Editor contract: `string`).
     * @returns `null` when the reference is empty/missing, otherwise the
     *   original value for downstream resolution.
     */
    protected normalizeReferenceValue(value: unknown): unknown {
        if (value === null || value === undefined) return null;
        if (typeof value === 'string') {
            return value.trim() === '' ? null : value;
        }
        // Already a resolved component reference — pass through.
        return value;
    }

    serialize(): Record<string, any> {
        return {
            // Reference serialisation contract: emit "" for null references
            // (Editor convention). Non-null references are resolved by the
            // scene loader — the serialised placeholder keeps the key present.
            connectedBody: '',
            anchor: { x: this._anchor.x, y: this._anchor.y, z: this._anchor.z },
            connectedAnchor: { x: this._connectedAnchor.x, y: this._connectedAnchor.y, z: this._connectedAnchor.z },
            autoConfigureConnectedAnchor: this._autoConfigureConnectedAnchor,
            axis: { x: this._axis.x, y: this._axis.y, z: this._axis.z },
            secondaryAxis: { x: this._secondaryAxis.x, y: this._secondaryAxis.y, z: this._secondaryAxis.z },
            breakForce: this._breakForce,
            breakTorque: this._breakTorque,
            enableCollision: this._enableCollision,
            enablePreprocessing: this._enablePreprocessing,
            massScale: this._massScale,
            connectedMassScale: this._connectedMassScale,
            enabled: this._joint3dEnabled,
        };
    }

    deserialize(data: Record<string, any>): void {
        // connectedBody: Editor sends "" for no reference, or an entity ID
        // string. Normalise empties to null; non-empty values are resolved
        // by the scene loader after this call.
        const rawCB = data.connectedBody;
        if (rawCB !== undefined) {
            const normalised = this.normalizeReferenceValue(rawCB);
            this._connectedBody = (normalised as Rigidbody3D) ?? null;
        }
        // Vec3 fields: Editor writes ARRAY [x,y,z], engine uses OBJECT {x,y,z}.
        // normalizeVec3Value accepts both formats for backward compatibility.
        if (data.anchor !== undefined) {
            const v = this.normalizeVec3Value(data.anchor);
            this._anchor.x = v.x;
            this._anchor.y = v.y;
            this._anchor.z = v.z;
        }
        if (data.connectedAnchor !== undefined) {
            const v = this.normalizeVec3Value(data.connectedAnchor);
            this._connectedAnchor.x = v.x;
            this._connectedAnchor.y = v.y;
            this._connectedAnchor.z = v.z;
        }
        if (data.autoConfigureConnectedAnchor !== undefined) {
            this._autoConfigureConnectedAnchor = data.autoConfigureConnectedAnchor;
        }
        if (data.axis !== undefined) {
            const v = this.normalizeVec3Value(data.axis, 1, 0, 0);
            this._axis.x = v.x;
            this._axis.y = v.y;
            this._axis.z = v.z;
        }
        if (data.secondaryAxis !== undefined) {
            const v = this.normalizeVec3Value(data.secondaryAxis, 0, 1, 0);
            this._secondaryAxis.x = v.x;
            this._secondaryAxis.y = v.y;
            this._secondaryAxis.z = v.z;
        }
        if (data.breakForce !== undefined) this._breakForce = data.breakForce;
        if (data.breakTorque !== undefined) this._breakTorque = data.breakTorque;
        if (data.enableCollision !== undefined) this._enableCollision = data.enableCollision;
        // Preprocessing: Editor's Hinge joint writes "preprocessing" (legacy key),
        // other joints write "enablePreprocessing" (canonical). Accept both.
        // If both are present, "enablePreprocessing" wins (canonical takes precedence).
        if (data.enablePreprocessing !== undefined) {
            this._enablePreprocessing = data.enablePreprocessing;
        } else if (data.preprocessing !== undefined) {
            this._enablePreprocessing = data.preprocessing;
        }
        if (data.massScale !== undefined) this._massScale = data.massScale;
        if (data.connectedMassScale !== undefined) this._connectedMassScale = data.connectedMassScale;
        if (data.enabled !== undefined) this._joint3dEnabled = data.enabled;
    }
}
