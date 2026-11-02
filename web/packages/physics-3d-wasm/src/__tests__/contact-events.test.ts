import { describe, expect, it } from 'vitest';
import { WasmPhysicsWorld3D } from '../index';
import { loadEmbeddedWasm } from '../loader';
import type { ContactEventSnapshot } from '../types';

const DT = 1 / 60;

async function createWorld(): Promise<WasmPhysicsWorld3D> {
    return WasmPhysicsWorld3D.create({
        maxBodies: 32,
        maxContacts: 256,
        gravity: [0, -9.81, 0]
    });
}

function addGround(world: WasmPhysicsWorld3D): number {
    return world.createBody({
        type: 'static',
        position: [0, -0.5, 0],
        shape: { kind: 'box', halfExtents: [10, 0.5, 10] }
    });
}

function addDropBox(world: WasmPhysicsWorld3D): number {
    return world.createBody({
        position: [0, 3, 0],
        shape: { kind: 'box', halfExtents: [0.5, 0.5, 0.5] },
        density: 1,
        friction: 0.7
    });
}

function pairOf(event: ContactEventSnapshot): number[] {
    return [event.a, event.b].sort((left, right) => left - right);
}

describe('contact event ring', () => {
    it('reports begin then stay for a landing box', async () => {
        const world = await createWorld();
        const ground = addGround(world);
        const box = addDropBox(world);

        let begin: ContactEventSnapshot | null = null;
        for (let index = 0; index < 240 && begin === null; index += 1) {
            world.step(DT);
            const events = world.drainEvents();
            begin = events.find((event) => event.kind === 'begin') ?? null;
        }

        expect(begin).not.toBeNull();
        expect(pairOf(begin as ContactEventSnapshot)).toEqual([ground, box]);
        expect(Number.isFinite((begin as ContactEventSnapshot).penetration)).toBe(true);
        expect((begin as ContactEventSnapshot).normal.length).toBe(3);

        let stayed = false;
        for (let index = 0; index < 5 && !stayed; index += 1) {
            world.step(DT);
            const events = world.drainEvents();
            stayed = events.some((event) => event.kind === 'stay' && event.a === (begin as ContactEventSnapshot).a && event.b === (begin as ContactEventSnapshot).b);
        }
        expect(stayed).toBe(true);
        world.dispose();
    });

    it('reports end when the box is removed', async () => {
        const world = await createWorld();
        const ground = addGround(world);
        const box = addDropBox(world);

        let began = false;
        for (let index = 0; index < 240 && !began; index += 1) {
            world.step(DT);
            began = world.drainEvents().some((event) => event.kind === 'begin');
        }
        expect(began).toBe(true);

        world.destroyBody(box);
        world.step(DT);
        const events = world.drainEvents();
        const end = events.find((event) => event.kind === 'end');
        expect(end ?? null).not.toBeNull();
        expect(pairOf(end as ContactEventSnapshot)).toEqual([ground, box]);

        expect(world.drainEvents()).toEqual([]);
        world.dispose();
    });

    it('clears the reported count after draining', async () => {
        const world = await createWorld();
        addGround(world);
        addDropBox(world);

        world.step(DT);
        world.drainEvents();
        expect(world.drainEvents()).toEqual([]);

        world.step(DT);
        const events = world.drainEvents();
        expect(Array.isArray(events)).toBe(true);
        world.dispose();
    });

    it('returns no events before any contact exists', async () => {
        const world = await createWorld();
        addGround(world);
        addDropBox(world);

        world.step(DT);
        const first = world.drainEvents();
        expect(first).toEqual([]);
        world.dispose();
    });

    it('keeps the overflow counter at zero for a small scene', async () => {
        const wasm = await loadEmbeddedWasm();
        expect(wasm.worldInit(16, 256, 0, -9.81, 0)).toBe(0);

        const ground = wasm.bodyCreate(0, 0, -0.5, 0, 0, 0, 0, 1);
        expect(wasm.bodySetBox(ground, 10, 0.5, 10, 0)).toBe(0);
        const box = wasm.bodyCreate(1, 0, 3, 0, 0, 0, 0, 1);
        expect(wasm.bodySetBox(box, 0.5, 0.5, 0.5, 1)).toBe(0);

        let sawEvents = false;
        for (let index = 0; index < 240; index += 1) {
            expect(wasm.step(DT, 8, 3)).toBeGreaterThanOrEqual(0);
            if (wasm.eventCount() > 0) {
                sawEvents = true;
                expect(wasm.eventCount()).toBeLessThanOrEqual(4096);
                wasm.eventsClear();
                expect(wasm.eventCount()).toBe(0);
            }
        }

        expect(sawEvents).toBe(true);
        expect(wasm.eventsOverflow()).toBe(0);

        wasm.worldReset();
        expect(wasm.eventCount()).toBe(0);
        expect(wasm.eventsOverflow()).toBe(0);
        expect(wasm.eventsPtr() % 16).toBe(0);
    });
});
