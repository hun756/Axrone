import { describe, expect, it } from 'vitest';
import {
    WasmBodyType,
    WasmPhysicsWorld3D,
    WasmPhysicsWorld3DCompat,
    loadEmbeddedWasm
} from '../index';

const DT = 1 / 60;

describe('body type codes', () => {
    it('pins the reference order used by the TypeScript surface', () => {
        expect(WasmBodyType.static).toBe(0);
        expect(WasmBodyType.kinematic).toBe(1);
        expect(WasmBodyType.dynamic).toBe(2);
        expect(Object.isFrozen(WasmBodyType)).toBe(true);
        expect({ ...WasmBodyType }).toEqual({ static: 0, kinematic: 1, dynamic: 2 });
    });

    it('pins the current C ABI numbering through observed behavior', async () => {
        const wasm = await loadEmbeddedWasm();
        expect(wasm.worldInit(16, 256, 0, -9.81, 0)).toBe(0);

        const staticBody = wasm.bodyCreate(0, 0, 5, 0, 0, 0, 0, 1);
        expect(wasm.bodySetSphere(staticBody, 0.5, 0)).toBe(0);
        const abiDynamic = wasm.bodyCreate(1, 0, 5, 0, 0, 0, 0, 1);
        expect(wasm.bodySetSphere(abiDynamic, 0.5, 1)).toBe(0);
        const abiKinematic = wasm.bodyCreate(2, 0, 5, 0, 0, 0, 0, 1);
        expect(wasm.bodySetSphere(abiKinematic, 0.5, 0)).toBe(0);

        for (let index = 0; index < 60; index += 1) {
            expect(wasm.step(DT, 8, 3)).toBeGreaterThanOrEqual(0);
        }

        const transforms = new Float32Array(wasm.memory.buffer, wasm.transformsPtr(), 16 * 7);
        expect(transforms[abiDynamic * 7 + 1]).toBeLessThan(4.5);
        expect(transforms[abiKinematic * 7 + 1]).toBe(5);
        expect(transforms[staticBody * 7 + 1]).toBe(5);

        expect(abiDynamic).not.toBe(WasmBodyType.dynamic);
        expect(WasmBodyType.dynamic).toBe(2);
    });

    it('translates reference codes through the facade and compat layers', async () => {
        const world = await WasmPhysicsWorld3D.create({
            maxBodies: 16,
            maxContacts: 256,
            gravity: [0, -9.81, 0]
        });
        const facadeDynamic = world.createBody({ type: 'dynamic', position: [0, 5, 0] });
        world.attachSphere(facadeDynamic, 0.5, 1);
        const facadeKinematic = world.createBody({ type: 'kinematic', position: [0, 5, 0] });
        world.attachSphere(facadeKinematic, 0.5, 0);
        for (let index = 0; index < 60; index += 1) {
            world.step(DT);
        }
        expect(world.readTransform(facadeDynamic).position[1]).toBeLessThan(4.5);
        expect(world.readTransform(facadeKinematic).position[1]).toBe(5);
        world.dispose();

        const compat = await WasmPhysicsWorld3DCompat.create({ maxBodies: 16 });
        const compatKinematic = compat.createBody({
            type: WasmBodyType.kinematic,
            position: { x: 0, y: 5, z: 0 }
        });
        compat.createSphereShape(compatKinematic, { radius: 0.5 });
        const compatDynamic = compat.createBody({
            type: WasmBodyType.dynamic,
            position: { x: 2, y: 5, z: 0 }
        });
        compat.createSphereShape(compatDynamic, { radius: 0.5 });
        const compatStatic = compat.createBody({
            type: WasmBodyType.static,
            position: { x: 4, y: 5, z: 0 }
        });
        compat.createSphereShape(compatStatic, { radius: 0.5 });

        for (let index = 0; index < 60; index += 1) {
            compat.step(DT, 8, 3);
        }

        expect(compat.getTransform(compatKinematic).position[1]).toBe(5);
        expect(compat.getTransform(compatDynamic).position[1]).toBeLessThan(4.5);
        expect(compat.getTransform(compatStatic).position[1]).toBe(5);
        compat.dispose();
    });
});
