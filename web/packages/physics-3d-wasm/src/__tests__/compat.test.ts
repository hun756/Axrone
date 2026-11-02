import { describe, expect, it } from 'vitest';
import { WasmPhysicsWorld3DCompat } from '../index';

async function createCompat(maxBodies = 32): Promise<WasmPhysicsWorld3DCompat> {
    return WasmPhysicsWorld3DCompat.create({
        maxBodies,
        maxContacts: Math.max(1024, maxBodies * 16),
        gravity: { x: 0, y: -9.81, z: 0 }
    });
}

describe('WasmPhysicsWorld3DCompat', () => {
    it('runs a benchmark-style box scene', async () => {
        const world = await createCompat();
        const ground = world.createBody({
            type: 0,
            position: { x: 0, y: -0.5, z: 0 },
            allowSleep: false
        });
        expect(ground).toBe(0);
        world.createBoxShape(ground, { halfExtents: { x: 10, y: 0.5, z: 10 } });

        const body = world.createBody({
            type: 2,
            position: { x: 0, y: 1, z: 0 },
            allowSleep: false
        });
        const shape = world.createBoxShape(body, { halfExtents: { x: 0.5, y: 0.5, z: 0.5 } });
        expect(shape).toBeGreaterThan(0);
        world.getBody(body)?.resetMassData();

        for (let index = 0; index < 120; index += 1) {
            world.step(1 / 60, 8, 3);
        }

        expect(world.bodyCount).toBe(2);
        expect(world.contactCount).toBeGreaterThan(0);
        expect(world.getStatistics().contactCount).toBe(world.contactCount);
        expect(world.getBody(999)).toBe(null);
        world.dispose();
    });

    it('accepts kinematic bodies', async () => {
        const world = await createCompat(8);
        const platform = world.createBody({
            type: 1,
            position: { x: 0, y: 0, z: 0 },
            allowSleep: false
        });
        expect(platform).toBe(0);
        const shape = world.createBoxShape(platform, { halfExtents: { x: 1, y: 0.1, z: 1 } });
        expect(shape).toBeGreaterThan(0);
        world.step(1 / 60, 8, 3);
        expect(world.bodyCount).toBe(1);
        expect(world.contactCount).toBe(0);
        expect(world.getStatistics().bodyCount).toBe(1);
        world.dispose();
    });

    it('rejects unsupported body and shape combinations', async () => {
        const world = await createCompat(8);
        const body = world.createBody({ type: 2 });
        expect(() => world.createBoxShape(body, {
            center: { x: 1, y: 0, z: 0 },
            halfExtents: { x: 0.5, y: 0.5, z: 0.5 }
        })).toThrow(/offset/);
        world.createBoxShape(body, {
            halfExtents: { x: 0.5, y: 0.5, z: 0.5 }
        });
        expect(() => world.createSphereShape(body, { radius: 0.5 })).toThrow(/multiple shapes/);
        world.dispose();
        expect(() => world.bodyCount).toThrow(/disposed/);
    });

    it('resets the compatibility world', async () => {
        const world = await createCompat(8);
        const body = world.createBody({ type: 2 });
        world.createSphereShape(body, { radius: 0.5 });
        world.step(1 / 60);
        expect(world.bodyCount).toBe(1);
        world.reset();
        expect(world.bodyCount).toBe(0);
        expect(world.contactCount).toBe(0);
        world.dispose();
    });
});
