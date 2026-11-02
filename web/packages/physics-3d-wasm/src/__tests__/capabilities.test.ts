import { describe, expect, it } from 'vitest';
import { WASM_PHYSICS_CAPABILITIES } from '../index';

describe('WASM physics capabilities', () => {
    it('reports the supported rigid-body subset', () => {
        expect(WASM_PHYSICS_CAPABILITIES.backend).toBe('wasm');
        expect(WASM_PHYSICS_CAPABILITIES.bodyTypes.static).toBe(true);
        expect(WASM_PHYSICS_CAPABILITIES.bodyTypes.dynamic).toBe(true);
        expect(WASM_PHYSICS_CAPABILITIES.bodyTypes.kinematic).toBe(true);
        expect(WASM_PHYSICS_CAPABILITIES.shapes.box).toBe(true);
        expect(WASM_PHYSICS_CAPABILITIES.shapes.sphere).toBe(true);
        expect(WASM_PHYSICS_CAPABILITIES.shapes.capsule).toBe(false);
        expect(WASM_PHYSICS_CAPABILITIES.multiShape).toBe(false);
        expect(WASM_PHYSICS_CAPABILITIES.constraints).toBe(false);
        expect(WASM_PHYSICS_CAPABILITIES.queries).toBe(true);
        expect(WASM_PHYSICS_CAPABILITIES.raycast).toBe(true);
        expect(WASM_PHYSICS_CAPABILITIES.sleeping).toBe(false);
        expect(WASM_PHYSICS_CAPABILITIES.contactEvents).toBe(false);
        expect(Object.isFrozen(WASM_PHYSICS_CAPABILITIES)).toBe(true);
        expect(Object.isFrozen(WASM_PHYSICS_CAPABILITIES.bodyTypes)).toBe(true);
        expect(Object.isFrozen(WASM_PHYSICS_CAPABILITIES.shapes)).toBe(true);
    });
});
