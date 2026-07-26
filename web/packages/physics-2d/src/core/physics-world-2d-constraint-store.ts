import type { IVec2Like } from '@axrone/numeric';
import type {
    BodyId,
    ConstraintId,
    IConstraint2D,
} from '../types';
import { ConstraintType } from '../types';

import { BodyManager2D } from './body-manager';
import { ConstraintManager2D } from './constraint-manager';
import {
    cloneVec2,
    IConstraintDescriptor2D,
    STANDALONE_CONSTRAINT_ID_START,
    transformPoint2D,
    getBodyWorldCenter,
} from './physics-world-2d-helpers';

export interface IConstraintDescriptorProvider2D {
    getConstraintDescriptor(constraintId: ConstraintId): IConstraintDescriptor2D | null;
    getConstraintsForBody(bodyId: BodyId): readonly ConstraintId[];
    isConstraintEnabled(constraintId: ConstraintId): boolean;
}

type ConstraintRegistration = Omit<IConstraintDescriptor2D, 'storage'>;

export class PhysicsWorld2DConstraintStore implements IConstraintDescriptorProvider2D {
    private readonly _views = new Map<ConstraintId, IConstraint2D>();
    private readonly _descriptors = new Map<ConstraintId, IConstraintDescriptor2D>();
    private readonly _constraintsByBody = new Map<BodyId, Set<ConstraintId>>();
    private _nextStandaloneConstraintId = STANDALONE_CONSTRAINT_ID_START;

    constructor(
        private readonly _bodyManager: BodyManager2D,
        private readonly _constraintManager: ConstraintManager2D
    ) {}

    get size(): number {
        return this._descriptors.size;
    }

    clear(): void {
        this._views.clear();
        this._descriptors.clear();
        this._constraintsByBody.clear();
        this._nextStandaloneConstraintId = STANDALONE_CONSTRAINT_ID_START;
    }

    entries(): IterableIterator<[ConstraintId, IConstraintDescriptor2D]> {
        return this._descriptors.entries();
    }

    hasConstraint(constraintId: ConstraintId): boolean {
        return this._descriptors.has(constraintId);
    }

    getConstraintDescriptor(constraintId: ConstraintId): IConstraintDescriptor2D | null {
        return this._descriptors.get(constraintId) ?? null;
    }

    getConstraintsForBody(bodyId: BodyId): readonly ConstraintId[] {
        const constraints = this._constraintsByBody.get(bodyId);
        return constraints ? Array.from(constraints) : [];
    }

    isConstraintEnabled(constraintId: ConstraintId): boolean {
        const descriptor = this._descriptors.get(constraintId);
        if (!descriptor) {
            return false;
        }

        return descriptor.storage === 'manager'
            ? this._constraintManager.isEnabled(constraintId)
            : descriptor.enabled;
    }

    registerManagedConstraint(constraintId: ConstraintId, descriptor: ConstraintRegistration): void {
        this._registerConstraintDescriptor(constraintId, {
            ...descriptor,
            storage: 'manager',
        });
    }

    createStandaloneConstraint(descriptor: ConstraintRegistration): ConstraintId {
        const constraintId = this._nextStandaloneConstraintId++ as ConstraintId;
        this._registerConstraintDescriptor(constraintId, {
            ...descriptor,
            storage: 'world',
        });
        return constraintId;
    }

    removeConstraint(constraintId: ConstraintId): IConstraintDescriptor2D | null {
        this._views.delete(constraintId);
        const descriptor = this._descriptors.get(constraintId) ?? null;
        if (!descriptor) {
            return null;
        }

        this._removeConstraintFromBody(descriptor.bodyIdA, constraintId);
        this._removeConstraintFromBody(descriptor.bodyIdB, constraintId);
        this._descriptors.delete(constraintId);
        return descriptor;
    }

    getConstraintView(constraintId: ConstraintId): IConstraint2D | null {
        const descriptor = this._descriptors.get(constraintId);
        if (!descriptor) {
            return null;
        }

        const existing = this._views.get(constraintId);
        if (existing) {
            return existing;
        }

        const store = this;
        const constraint: IConstraint2D = {
            get id(): ConstraintId {
                return constraintId;
            },
            get type(): ConstraintType {
                return store._descriptors.get(constraintId)!.type;
            },
            get bodyIdA(): BodyId {
                return store._descriptors.get(constraintId)!.bodyIdA;
            },
            get bodyIdB(): BodyId {
                return store._descriptors.get(constraintId)!.bodyIdB;
            },
            get collideConnected(): boolean {
                return store._descriptors.get(constraintId)!.collideConnected;
            },
            get userData(): unknown {
                return store._descriptors.get(constraintId)!.userData;
            },
            getAnchorA(): IVec2Like {
                return store._getConstraintAnchorA(constraintId);
            },
            getAnchorB(): IVec2Like {
                return store._getConstraintAnchorB(constraintId);
            },
            getReactionForce(_inverseDt: number): IVec2Like {
                return { x: 0, y: 0 };
            },
            getReactionTorque(_inverseDt: number): number {
                return 0;
            },
            isEnabled(): boolean {
                return store.isConstraintEnabled(constraintId);
            },
            setEnabled(enabled: boolean): void {
                const current = store._descriptors.get(constraintId);
                if (!current) {
                    return;
                }

                current.enabled = enabled;
                if (current.storage === 'manager') {
                    store._constraintManager.setEnabled(constraintId, enabled);
                }
            },
        };

        this._views.set(constraintId, constraint);
        return constraint;
    }

    validate(): boolean {
        for (const [constraintId, descriptor] of this._descriptors) {
            if (!this._bodyManager.hasBody(descriptor.bodyIdA)) {
                return false;
            }

            if (!this._bodyManager.hasBody(descriptor.bodyIdB)) {
                return false;
            }

            if (descriptor.storage === 'manager' && !this._constraintManager.hasConstraint(constraintId)) {
                return false;
            }
        }

        return true;
    }

    private _registerConstraintDescriptor(
        constraintId: ConstraintId,
        descriptor: IConstraintDescriptor2D
    ): void {
        this._descriptors.set(constraintId, descriptor);
        this._addConstraintToBody(descriptor.bodyIdA, constraintId);
        this._addConstraintToBody(descriptor.bodyIdB, constraintId);
    }

    private _addConstraintToBody(bodyId: BodyId, constraintId: ConstraintId): void {
        let constraints = this._constraintsByBody.get(bodyId);
        if (!constraints) {
            constraints = new Set();
            this._constraintsByBody.set(bodyId, constraints);
        }

        constraints.add(constraintId);
    }

    private _removeConstraintFromBody(bodyId: BodyId, constraintId: ConstraintId): void {
        const constraints = this._constraintsByBody.get(bodyId);
        if (!constraints) {
            return;
        }

        constraints.delete(constraintId);
        if (constraints.size === 0) {
            this._constraintsByBody.delete(bodyId);
        }
    }

    private _getConstraintAnchorA(constraintId: ConstraintId): IVec2Like {
        const descriptor = this._descriptors.get(constraintId)!;
        switch (descriptor.type) {
            case ConstraintType.Distance:
            case ConstraintType.Revolute:
            case ConstraintType.Prismatic:
            case ConstraintType.Weld:
            case ConstraintType.Wheel:
            case ConstraintType.Rope:
                return this._getAnchorWorldPosition(descriptor.bodyIdA, descriptor.localAnchorA);
            case ConstraintType.Mouse:
            case ConstraintType.Motor:
                return getBodyWorldCenter(this._bodyManager, descriptor.bodyIdA);
            case ConstraintType.Gear:
                return descriptor.constraintIdA !== null
                    ? this._getConstraintAnchorA(descriptor.constraintIdA)
                    : getBodyWorldCenter(this._bodyManager, descriptor.bodyIdA);
            default:
                return getBodyWorldCenter(this._bodyManager, descriptor.bodyIdA);
        }
    }

    private _getConstraintAnchorB(constraintId: ConstraintId): IVec2Like {
        const descriptor = this._descriptors.get(constraintId)!;
        switch (descriptor.type) {
            case ConstraintType.Distance:
            case ConstraintType.Revolute:
            case ConstraintType.Prismatic:
            case ConstraintType.Weld:
            case ConstraintType.Wheel:
            case ConstraintType.Rope:
                return this._getAnchorWorldPosition(descriptor.bodyIdB, descriptor.localAnchorB);
            case ConstraintType.Mouse:
                return cloneVec2(descriptor.target ?? getBodyWorldCenter(this._bodyManager, descriptor.bodyIdB));
            case ConstraintType.Motor: {
                const center = getBodyWorldCenter(this._bodyManager, descriptor.bodyIdB);
                const offset = descriptor.linearOffset ?? { x: 0, y: 0 };
                return {
                    x: center.x + offset.x,
                    y: center.y + offset.y,
                };
            }
            case ConstraintType.Gear:
                return descriptor.constraintIdB !== null
                    ? this._getConstraintAnchorB(descriptor.constraintIdB)
                    : getBodyWorldCenter(this._bodyManager, descriptor.bodyIdB);
            default:
                return getBodyWorldCenter(this._bodyManager, descriptor.bodyIdB);
        }
    }

    private _getAnchorWorldPosition(bodyId: BodyId, localAnchor: IVec2Like | null): IVec2Like {
        if (!localAnchor) {
            return getBodyWorldCenter(this._bodyManager, bodyId);
        }

        const position = this._bodyManager.getPosition(bodyId);
        const rotation = this._bodyManager.getRotation(bodyId);
        return transformPoint2D(position, rotation, localAnchor);
    }
}