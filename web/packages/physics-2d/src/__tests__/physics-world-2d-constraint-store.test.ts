import { describe, it, expect, beforeEach } from 'vitest';
import { PhysicsWorld2DConstraintStore } from '../core/physics-world-2d-constraint-store';
import type { BodyId, ConstraintId } from '../types';

function createMockBodyManager() {
    return {
        hasBody: (_id: BodyId) => true,
        getPosition: (_id: BodyId) => ({ x: 0, y: 0 }),
        getRotation: (_id: BodyId) => 0,
        getLocalCenter: (_id: BodyId) => ({ x: 0, y: 0 }),
    } as any;
}

function createMockConstraintManager() {
    const enabled = new Map<ConstraintId, boolean>();
    return {
        hasConstraint: (_id: ConstraintId) => true,
        isEnabled: (id: ConstraintId) => enabled.get(id) ?? true,
        setEnabled: (id: ConstraintId, v: boolean) => { enabled.set(id, v); },
        _enabled: enabled,
    } as any;
}

function makeDescriptor(overrides: Record<string, any> = {}) {
    return {
        type: 0,
        bodyIdA: 1 as BodyId,
        bodyIdB: 2 as BodyId,
        collideConnected: false,
        enabled: true,
        userData: null,
        localAnchorA: null,
        localAnchorB: null,
        localAxisA: null,
        linearOffset: null,
        target: null,
        constraintIdA: null,
        constraintIdB: null,
        referenceAngle: null,
        angularOffset: null,
        length: null,
        minLength: null,
        maxLength: null,
        stiffness: null,
        damping: null,
        lowerTranslation: null,
        upperTranslation: null,
        motorSpeed: null,
        maxMotorTorque: null,
        maxMotorForce: null,
        maxForce: null,
        maxTorque: null,
        correctionFactor: null,
        ratio: null,
        ...overrides,
    };
}

describe('PhysicsWorld2DConstraintStore', () => {
    let bm: any;
    let cm: any;
    let store: PhysicsWorld2DConstraintStore;

    beforeEach(() => {
        bm = createMockBodyManager();
        cm = createMockConstraintManager();
        store = new PhysicsWorld2DConstraintStore(bm, cm);
    });

    describe('initial state', () => {
        it('starts with size 0', () => {
            expect(store.size).toBe(0);
        });

        it('hasConstraint returns false for unknown id', () => {
            expect(store.hasConstraint(999 as ConstraintId)).toBe(false);
        });
    });

    describe('registerManagedConstraint', () => {
        it('adds a constraint and increments size', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor());
            expect(store.size).toBe(1);
            expect(store.hasConstraint(10 as ConstraintId)).toBe(true);
        });

        it('descriptor has storage "manager"', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor());
            const desc = store.getConstraintDescriptor(10 as ConstraintId);
            expect(desc?.storage).toBe('manager');
        });
    });

    describe('createStandaloneConstraint', () => {
        it('returns a unique constraint id starting from STANDALONE_CONSTRAINT_ID_START', () => {
            const id1 = store.createStandaloneConstraint(makeDescriptor());
            const id2 = store.createStandaloneConstraint(makeDescriptor());
            expect(id1).toBeGreaterThanOrEqual(1_000_000);
            expect(id2).toBe(id1 + 1);
        });

        it('descriptor has storage "world"', () => {
            const id = store.createStandaloneConstraint(makeDescriptor());
            expect(store.getConstraintDescriptor(id)?.storage).toBe('world');
        });
    });

    describe('removeConstraint', () => {
        it('removes the constraint and returns its descriptor', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor());
            const removed = store.removeConstraint(10 as ConstraintId);
            expect(removed).not.toBeNull();
            expect(store.size).toBe(0);
            expect(store.hasConstraint(10 as ConstraintId)).toBe(false);
        });

        it('returns null for unknown constraint', () => {
            expect(store.removeConstraint(999 as ConstraintId)).toBeNull();
        });

        it('removes constraint from body index', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor({ bodyIdA: 1 as BodyId, bodyIdB: 2 as BodyId }));
            store.removeConstraint(10 as ConstraintId);
            expect(store.getConstraintsForBody(1 as BodyId)).toEqual([]);
            expect(store.getConstraintsForBody(2 as BodyId)).toEqual([]);
        });
    });

    describe('getConstraintsForBody', () => {
        it('returns constraints linked to a body', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor({ bodyIdA: 1 as BodyId, bodyIdB: 2 as BodyId }));
            store.registerManagedConstraint(20 as ConstraintId, makeDescriptor({ bodyIdA: 1 as BodyId, bodyIdB: 3 as BodyId }));

            const forBody1 = store.getConstraintsForBody(1 as BodyId);
            expect(forBody1).toContain(10);
            expect(forBody1).toContain(20);

            const forBody3 = store.getConstraintsForBody(3 as BodyId);
            expect(forBody3).toContain(20);
            expect(forBody3).not.toContain(10);
        });

        it('returns empty array for unlinked body', () => {
            expect(store.getConstraintsForBody(999 as BodyId)).toEqual([]);
        });
    });

    describe('getConstraintDescriptor', () => {
        it('returns null for unknown constraint', () => {
            expect(store.getConstraintDescriptor(999 as ConstraintId)).toBeNull();
        });

        it('returns the descriptor for registered constraint', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor({ type: 1 }));
            const desc = store.getConstraintDescriptor(10 as ConstraintId);
            expect(desc?.type).toBe(1);
        });
    });

    describe('isConstraintEnabled', () => {
        it('delegates to constraintManager for managed constraints', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor());
            expect(store.isConstraintEnabled(10 as ConstraintId)).toBe(true);
            cm._enabled.set(10 as ConstraintId, false);
            expect(store.isConstraintEnabled(10 as ConstraintId)).toBe(false);
        });

        it('uses descriptor.enabled for standalone constraints', () => {
            const id = store.createStandaloneConstraint(makeDescriptor({ enabled: false }));
            expect(store.isConstraintEnabled(id)).toBe(false);
        });

        it('returns false for unknown constraint', () => {
            expect(store.isConstraintEnabled(999 as ConstraintId)).toBe(false);
        });
    });

    describe('clear', () => {
        it('removes all constraints and resets id counter', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor());
            store.createStandaloneConstraint(makeDescriptor());
            store.clear();
            expect(store.size).toBe(0);
            expect(store.hasConstraint(10 as ConstraintId)).toBe(false);
        });
    });

    describe('entries', () => {
        it('iterates over all registered descriptors', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor());
            store.registerManagedConstraint(20 as ConstraintId, makeDescriptor());
            const all = [...store.entries()];
            expect(all).toHaveLength(2);
            expect(all[0][0]).toBe(10);
        });
    });

    describe('getConstraintView', () => {
        it('returns null for unknown constraint', () => {
            expect(store.getConstraintView(999 as ConstraintId)).toBeNull();
        });

        it('returns a view with correct id and type', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor({ type: 1 }));
            const view = store.getConstraintView(10 as ConstraintId)!;
            expect(view.id).toBe(10);
            expect(view.type).toBe(1);
        });

        it('returns the same view instance on subsequent calls', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor());
            const v1 = store.getConstraintView(10 as ConstraintId);
            const v2 = store.getConstraintView(10 as ConstraintId);
            expect(v1).toBe(v2);
        });

        it('view.isEnabled delegates correctly', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor());
            const view = store.getConstraintView(10 as ConstraintId)!;
            expect(view.isEnabled()).toBe(true);
        });

        it('view.setEnabled updates descriptor for standalone constraints', () => {
            const id = store.createStandaloneConstraint(makeDescriptor({ enabled: true }));
            const view = store.getConstraintView(id)!;
            view.setEnabled(false);
            expect(store.getConstraintDescriptor(id)?.enabled).toBe(false);
        });
    });

    describe('validate', () => {
        it('returns true when all referenced bodies exist', () => {
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor());
            expect(store.validate()).toBe(true);
        });

        it('returns false when a body does not exist', () => {
            bm.hasBody = (id: BodyId) => id !== (2 as BodyId);
            store.registerManagedConstraint(10 as ConstraintId, makeDescriptor({ bodyIdA: 1 as BodyId, bodyIdB: 2 as BodyId }));
            expect(store.validate()).toBe(false);
        });
    });
});
