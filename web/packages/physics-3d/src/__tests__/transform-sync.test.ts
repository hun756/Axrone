import { describe, it, expect } from 'vitest';
import {
    syncTransformWorldPosition,
    syncTransformWorldRotation,
    syncTransformWorldPose,
} from '../core/transform-sync';

interface MockTransform {
    position: { x: number; y: number; z: number };
    rotation: { x: number; y: number; z: number; w: number };
    parent: MockTransformParent | null;
    readonly worldPosition: { x: number; y: number; z: number };
    readonly worldRotation: { x: number; y: number; z: number; w: number };
    readonly worldScale: { x: number; y: number; z: number };
}

interface MockTransformParent {
    readonly worldPosition: { x: number; y: number; z: number };
    readonly worldRotation: { x: number; y: number; z: number; w: number };
    readonly worldScale: { x: number; y: number; z: number };
}

function makeTransform(parent: MockTransformParent | null = null): MockTransform {
    const t: MockTransform = {
        position: { x: 0, y: 0, z: 0 },
        rotation: { x: 0, y: 0, z: 0, w: 1 },
        parent,
        get worldPosition() { return this.position; },
        get worldRotation() { return this.rotation; },
        get worldScale() { return { x: 1, y: 1, z: 1 }; },
    };
    return t;
}

function makeParent(opts: {
    pos?: { x: number; y: number; z: number };
    rot?: { x: number; y: number; z: number; w: number };
    scale?: { x: number; y: number; z: number };
}): MockTransformParent {
    return {
        worldPosition: opts.pos ?? { x: 0, y: 0, z: 0 },
        worldRotation: opts.rot ?? { x: 0, y: 0, z: 0, w: 1 },
        worldScale: opts.scale ?? { x: 1, y: 1, z: 1 },
    };
}

describe('transform-sync', () => {
    describe('syncTransformWorldPosition', () => {
        it('sets position directly when no parent', () => {
            const t = makeTransform();
            syncTransformWorldPosition(t as any, { x: 5, y: 10, z: -3 });
            expect(t.position.x).toBeCloseTo(5, 5);
            expect(t.position.y).toBeCloseTo(10, 5);
            expect(t.position.z).toBeCloseTo(-3, 5);
        });

        it('does nothing when transform is undefined', () => {
            expect(() => syncTransformWorldPosition(undefined, { x: 1, y: 2, z: 3 })).not.toThrow();
        });

        it('subtracts parent world position when parent exists', () => {
            const parent = makeParent({ pos: { x: 10, y: 0, z: 0 } });
            const t = makeTransform(parent);
            syncTransformWorldPosition(t as any, { x: 15, y: 0, z: 0 });
            expect(t.position.x).toBeCloseTo(5, 3);
        });

        it('accounts for parent scale', () => {
            const parent = makeParent({ scale: { x: 2, y: 2, z: 2 } });
            const t = makeTransform(parent);
            syncTransformWorldPosition(t as any, { x: 10, y: 0, z: 0 });
            expect(t.position.x).toBeCloseTo(5, 3);
        });

        it('guards against zero parent scale', () => {
            const parent = makeParent({ scale: { x: 0, y: 0, z: 0 } });
            const t = makeTransform(parent);
            expect(() => syncTransformWorldPosition(t as any, { x: 5, y: 5, z: 5 })).not.toThrow();
            expect(t.position.x).toBe(0);
            expect(t.position.y).toBe(0);
            expect(t.position.z).toBe(0);
        });
    });

    describe('syncTransformWorldRotation', () => {
        it('sets rotation directly when no parent', () => {
            const t = makeTransform();
            syncTransformWorldRotation(t as any, { x: 0, y: 0.707, z: 0, w: 0.707 });
            expect(t.rotation.y).toBeCloseTo(0.707, 3);
            expect(t.rotation.w).toBeCloseTo(0.707, 3);
        });

        it('does nothing when transform is undefined', () => {
            expect(() => syncTransformWorldRotation(undefined, { x: 0, y: 0, z: 0, w: 1 })).not.toThrow();
        });

        it('composes with parent inverse rotation', () => {
            const parent = makeParent({ rot: { x: 0, y: 0, z: 0, w: 1 } });
            const t = makeTransform(parent);
            const target = { x: 0, y: 0, z: 0, w: 1 };
            syncTransformWorldRotation(t as any, target);
            expect(t.rotation.w).toBeCloseTo(1, 3);
        });
    });

    describe('syncTransformWorldPose', () => {
        it('sets both position and rotation when no parent', () => {
            const t = makeTransform();
            syncTransformWorldPose(t as any, { x: 1, y: 2, z: 3 }, { x: 0, y: 0, z: 0, w: 1 });
            expect(t.position.x).toBeCloseTo(1, 5);
            expect(t.position.y).toBeCloseTo(2, 5);
            expect(t.position.z).toBeCloseTo(3, 5);
            expect(t.rotation.w).toBeCloseTo(1, 5);
        });

        it('does nothing when transform is undefined', () => {
            expect(() =>
                syncTransformWorldPose(undefined, { x: 1, y: 2, z: 3 }, { x: 0, y: 0, z: 0, w: 1 })
            ).not.toThrow();
        });

        it('applies parent inverse transform for pose', () => {
            const parent = makeParent({ pos: { x: 5, y: 0, z: 0 }, scale: { x: 2, y: 2, z: 2 } });
            const t = makeTransform(parent);
            syncTransformWorldPose(t as any, { x: 15, y: 0, z: 0 }, { x: 0, y: 0, z: 0, w: 1 });
            expect(t.position.x).toBeCloseTo(5, 3);
        });
    });
});
