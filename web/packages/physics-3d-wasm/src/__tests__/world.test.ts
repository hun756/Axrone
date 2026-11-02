import { describe, expect, it } from 'vitest';
import {
    WasmPhysicsWorld3D,
    decodeBase64,
    loadEmbeddedWasm
} from '../index';
import { WASM_BASE64 } from '../generated/wasm';

const DT = 1 / 60;

async function createWorld(maxBodies = 64): Promise<WasmPhysicsWorld3D> {
    return WasmPhysicsWorld3D.create({
        maxBodies,
        maxContacts: Math.max(1024, maxBodies * 16),
        gravity: [0, -9.81, 0]
    });
}

function addGround(world: WasmPhysicsWorld3D): number {
    return world.createBody({
        type: 'static',
        position: [0, -0.5, 0],
        shape: { kind: 'box', halfExtents: [20, 0.5, 20] },
        friction: 0.8
    });
}

function addBox(world: WasmPhysicsWorld3D, x: number, y: number, z = 0): number {
    return world.createBody({
        position: [x, y, z],
        shape: { kind: 'box', halfExtents: [0.5, 0.5, 0.5] },
        density: 1,
        friction: 0.7
    });
}

describe('WasmPhysicsWorld3D', () => {
    it('loads the embedded zero-import module and initializes a world', async () => {
        const wasm = await loadEmbeddedWasm();
        expect(WebAssembly.Module.imports(await WebAssembly.compile(decodeBase64(WASM_BASE64)))).toHaveLength(0);

        const world = await createWorld();
        expect(world.bodyCount).toBe(0);
        expect(world.contactCount).toBe(0);
        expect(world.memoryBytes).toBeGreaterThan(0);
        expect(world.getTransformsBuffer()).toHaveLength(world.maxBodies * 7);
        world.dispose();
        expect(wasm).toBeDefined();
    });

    it('rests a dynamic sphere on a static box', async () => {
        const world = await createWorld();
        addGround(world);

        const sphere = world.createBody({
            position: [0, 2, 0],
            shape: { kind: 'sphere', radius: 0.5 },
            density: 1
        });

        for (let index = 0; index < 180; index += 1) {
            world.step(DT, { velocityIterations: 8, positionIterations: 3 });
        }

        const transform = world.readTransform(sphere);
        expect(transform.position[1]).toBeCloseTo(0.5, 1);
        expect(Math.abs(world.readVelocity(sphere)[1])).toBeLessThan(0.3);
        expect(world.contactCount).toBeGreaterThanOrEqual(1);
        world.dispose();
    });

    it('keeps a three-box stack finite and in contact', async () => {
        const world = await createWorld();
        addGround(world);

        const boxes = [0, 1, 2].map((index) => addBox(world, 0, 0.5 + index));
        for (let index = 0; index < 240; index += 1) {
            world.step(DT, { velocityIterations: 8, positionIterations: 3 });
        }

        for (const id of boxes) {
            const transform = world.readTransform(id);
            expect(Number.isFinite(transform.position[0])).toBe(true);
            expect(Number.isFinite(transform.position[1])).toBe(true);
            expect(transform.position[1]).toBeGreaterThan(0.2);
            expect(transform.position[1]).toBeLessThan(5);
        }
        expect(world.contactCount).toBeGreaterThanOrEqual(1);
        world.dispose();
    });

    it('is deterministic for identical scenes', async () => {
        const first = await createWorld(32);
        const second = await createWorld(32);

        for (const world of [first, second]) {
            addGround(world);
            addBox(world, -0.6, 1);
            addBox(world, 0.6, 1);
            const sphere = world.createBody({
                position: [0, 2, 0],
                shape: { kind: 'sphere', radius: 0.5 }
            });
            expect(world.hasBody(sphere)).toBe(true);
        }

        for (let index = 0; index < 120; index += 1) {
            first.step(DT);
            second.step(DT);
        }

        expect(first.fingerprint()).toBe(second.fingerprint());
        expect(first.contactCount).toBe(second.contactCount);
        first.dispose();
        second.dispose();
    });

    it('rejects invalid construction and body inputs', async () => {
        await expect(WasmPhysicsWorld3D.create({ maxBodies: 0 })).rejects.toThrow();
        await expect(WasmPhysicsWorld3D.create({ maxBodies: 8, maxContacts: 0 })).rejects.toThrow();
        await expect(WasmPhysicsWorld3D.create({
            maxBodies: 8,
            gravity: [0, Number.NaN, 0]
        })).rejects.toThrow();

        const world = await createWorld(8);
        expect(() => world.createBody({
            shape: { kind: 'sphere', radius: 0 }
        })).toThrow();
        expect(() => world.createBody({
            shape: { kind: 'sphere', radius: 0.5 },
            density: 0
        })).toThrow();
        expect(() => world.createBody({
            shape: { kind: 'box', halfExtents: [0, 0.5, 0.5] }
        })).toThrow();
        expect(() => world.createBody({
            shape: { kind: 'box', halfExtents: [0.5, 0.5, 0.5] },
            restitution: 2
        })).toThrow();
        expect(() => world.step(0)).toThrow();
        expect(() => world.destroyBody(99)).toThrow();
        expect(() => world.setVelocity(99, [0, 0, 0])).toThrow();
        world.dispose();
        expect(() => world.bodyCount).toThrow();
    });

    it('destroys bodies and reuses their slots', async () => {
        const world = await createWorld(8);
        const first = addBox(world, 0, 1);
        const second = addBox(world, 1, 1);
        const third = addBox(world, 2, 1);

        expect(world.bodyCount).toBe(3);
        world.destroyBody(second);
        expect(world.bodyCount).toBe(2);
        expect(world.hasBody(second)).toBe(false);

        const reused = addBox(world, 3, 1);
        expect(reused).toBe(second);
        expect(world.bodyCount).toBe(3);
        expect(world.hasBody(reused)).toBe(true);
        expect(first).not.toBe(third);
        world.dispose();
    });

    it('copies transforms and velocities into caller-owned buffers', async () => {
        const world = await createWorld(8);
        addGround(world);
        const box = addBox(world, 0, 2);

        const transform = new Float32Array(7);
        world.copyTransform(box, transform);
        expect(transform[1]).toBeCloseTo(2, 5);

        const velocity = new Float32Array(3);
        world.setVelocity(box, [1, 2, 3]);
        world.copyVelocity(box, velocity);
        expect(Array.from(velocity)).toEqual([1, 2, 3]);

        const angular = new Float32Array(3);
        world.setAngularVelocity(box, [0, 1, 0]);
        world.copyAngularVelocity(box, angular);
        expect(Array.from(angular)).toEqual([0, 1, 0]);

        expect(() => world.copyTransform(box, new Float32Array(3))).toThrow();
        world.dispose();
    });

    it('exposes packed contact data', async () => {
        const world = await createWorld(16);
        addGround(world);
        const box = addBox(world, 0, 0.4);
        world.step(DT);

        expect(world.contactCount).toBeGreaterThan(0);
        const contacts = world.readContacts();
        expect(contacts).toHaveLength(world.contactCount);
        for (const contact of contacts) {
            expect(contact.a).toBeGreaterThanOrEqual(0);
            expect(contact.b).toBeGreaterThanOrEqual(0);
            expect(Number.isFinite(contact.normal[0])).toBe(true);
            expect(Number.isFinite(contact.normal[1])).toBe(true);
            expect(Number.isFinite(contact.normal[2])).toBe(true);
            expect(Number.isFinite(contact.penetration)).toBe(true);
        }
        expect(world.readContact(0).a).toBeGreaterThanOrEqual(0);
        expect(() => world.readContact(world.contactCount)).toThrow();
        world.dispose();
    });

    it('honors gravity changes and position-only transforms', async () => {
        const world = await createWorld(8);
        const body = addBox(world, 0, 3);

        world.setGravity([0, 0, 0]);
        for (let index = 0; index < 30; index += 1) {
            world.step(DT);
        }
        expect(world.readTransform(body).position[1]).toBeCloseTo(3, 3);

        world.setTransform(body, [2, 3, 0]);
        expect(world.readTransform(body).position[0]).toBeCloseTo(2, 5);
        expect(world.readTransform(body).rotation[3]).toBeCloseTo(1, 3);
        world.dispose();
    });

    it('keeps a kinematic body immune to gravity while integrating its velocities', async () => {
        const world = await createWorld();
        const platform = world.createBody({
            type: 'kinematic',
            position: [0, 3, 0],
            shape: { kind: 'box', halfExtents: [0.5, 0.5, 0.5] },
            friction: 0.7
        });

        for (let index = 0; index < 60; index += 1) {
            world.step(DT);
        }
        expect(world.readTransform(platform).position).toEqual([0, 3, 0]);
        expect(world.readVelocity(platform)).toEqual([0, 0, 0]);

        world.setVelocity(platform, [1, 0, 0]);
        for (let index = 0; index < 60; index += 1) {
            world.step(DT);
        }
        expect(world.readTransform(platform).position[0]).toBeCloseTo(1, 2);
        expect(world.readTransform(platform).position[1]).toBeCloseTo(3, 2);
        expect(world.readVelocity(platform)).toEqual([1, 0, 0]);

        world.setAngularVelocity(platform, [0, 2, 0]);
        const spinBefore = world.readTransform(platform).rotation[1];
        world.step(DT);
        expect(Math.abs(world.readTransform(platform).rotation[1] - spinBefore)).toBeGreaterThan(1e-4);
        expect(world.readAngularVelocity(platform)).toEqual([0, 2, 0]);
        world.dispose();
    });

    it('carries a dynamic box on an upward-moving kinematic platform', async () => {
        const world = await createWorld();
        const platform = world.createBody({
            type: 'kinematic',
            position: [0, 0, 0],
            shape: { kind: 'box', halfExtents: [2, 0.1, 2] },
            friction: 0.9
        });
        world.setVelocity(platform, [0, 1, 0]);

        const box = world.createBody({
            position: [0, 0.6, 0],
            shape: { kind: 'box', halfExtents: [0.5, 0.5, 0.5] },
            density: 1,
            friction: 0.9
        });

        for (let index = 0; index < 120; index += 1) {
            world.step(DT, { velocityIterations: 8, positionIterations: 3 });
        }

        expect(world.readTransform(platform).position[1]).toBeCloseTo(2, 1);
        const raised = world.readTransform(box).position[1];
        expect(raised).toBeGreaterThan(2.2);
        expect(raised).toBeLessThan(2.9);
        const velocity = world.readVelocity(box);
        expect(velocity[1]).toBeGreaterThan(0.4);
        expect(velocity[1]).toBeLessThan(1.6);
        expect(world.contactCount).toBeGreaterThanOrEqual(1);
        world.dispose();
    });

    it('does not let a dynamic body push a kinematic body', async () => {
        const world = await WasmPhysicsWorld3D.create({
            maxBodies: 16,
            maxContacts: 256,
            gravity: [0, 0, 0]
        });
        const wall = world.createBody({
            type: 'kinematic',
            position: [2, 0, 0],
            shape: { kind: 'box', halfExtents: [0.2, 1, 1] },
            friction: 0.5
        });
        const ram = world.createBody({
            position: [0, 0, 0],
            shape: { kind: 'box', halfExtents: [0.5, 0.5, 0.5] },
            density: 1,
            friction: 0
        });
        world.setVelocity(ram, [5, 0, 0]);

        let contacted = false;
        for (let index = 0; index < 120; index += 1) {
            world.step(DT, { velocityIterations: 8, positionIterations: 3 });
            if (world.contactCount > 0) {
                contacted = true;
            }
        }

        expect(contacted).toBe(true);
        expect(world.readTransform(wall).position).toEqual([2, 0, 0]);
        expect(world.readVelocity(wall)).toEqual([0, 0, 0]);
        expect(world.readTransform(ram).position[0]).toBeLessThan(1.5);
        world.dispose();
    });

    it('collides kinematic bodies with the static ground', async () => {
        const world = await createWorld();
        addGround(world);
        world.createBody({
            type: 'kinematic',
            position: [0, 0, 0],
            shape: { kind: 'box', halfExtents: [0.5, 0.5, 0.5] }
        });

        expect(world.step(DT)).toBeGreaterThanOrEqual(1);
        expect(world.readTransform(1).position).toEqual([0, 0, 0]);
        world.dispose();
    });

    it('matches the native standard-scene fingerprint', async () => {
        const world = await WasmPhysicsWorld3D.create({
            maxBodies: 512,
            maxContacts: 16384,
            gravity: [0, -9.81, 0]
        });

        world.createBody({
            type: 'static',
            position: [0, -0.5, 0],
            shape: { kind: 'box', halfExtents: [20, 0.5, 20] },
            friction: 0.8
        });

        for (let index = 0; index < 256; index += 1) {
            const layer = Math.floor(index / 64);
            const within = index % 64;
            const xIndex = within % 8;
            const zIndex = Math.floor(within / 8);
            world.createBody({
                position: [xIndex - 3, 1 + layer, zIndex - 3],
                shape: { kind: 'box', halfExtents: [0.4, 0.4, 0.4] },
                density: 1,
                friction: 0.7
            });
        }

        for (let index = 0; index < 240; index += 1) {
            world.step(DT, { velocityIterations: 8, positionIterations: 3 });
        }

        expect(world.fingerprint()).toBe('5d828f20ece8af81');
        expect(world.contactCount).toBe(1177);
        world.dispose();
    });

    it('resets without reallocating the world', async () => {
        const world = await createWorld(16);
        addGround(world);
        addBox(world, 0, 2);
        world.step(DT);

        const memoryBefore = world.memoryBytes;
        world.reset();
        expect(world.bodyCount).toBe(0);
        expect(world.contactCount).toBe(0);
        expect(world.memoryBytes).toBe(memoryBefore);

        const body = addBox(world, 0, 1);
        expect(world.hasBody(body)).toBe(true);
        world.step(DT);
        expect(world.bodyCount).toBe(1);
        world.dispose();
    });

    it('exposes v2 aliases and packed views', async () => {
        const wasm = await loadEmbeddedWasm();
        expect(typeof wasm.world_init).toBe('function');
        expect(typeof wasm.body_create).toBe('function');
        expect(typeof wasm.get_transforms_view).toBe('function');
        expect(wasm.world_init(8, 128, 0, -9.81, 0)).toBe(0);

        const emptyView = wasm.get_transforms_view();
        expect(Number(emptyView >> 32n)).toBe(wasm.transformsPtr());
        expect(Number(emptyView & 0xffffffffn)).toBe(0);

        const body = wasm.body_create(1, 0, 1, 0, 0, 0, 0, 1);
        expect(body).toBe(0);
        expect(wasm.body_set_box(body, 0.5, 0.5, 0.5, 1)).toBe(0);
        expect(Number(wasm.get_transforms_view() & 0xffffffffn)).toBe(28);
        expect(wasm.body_count()).toBe(1);

        wasm.world_reset();
        expect(wasm.body_count()).toBe(0);
        expect(Number(wasm.get_transforms_view() & 0xffffffffn)).toBe(0);
    });

    it('supports custom wasm bytes through createFromBytes', async () => {
        const bytes = decodeBase64(WASM_BASE64);
        const world = await WasmPhysicsWorld3D.createFromBytes(bytes, {
            maxBodies: 8,
            maxContacts: 128,
            gravity: [0, 0, 0]
        });
        const body = addBox(world, 0, 0);
        expect(world.hasBody(body)).toBe(true);
        world.dispose();
    });
});
