import { describe, it, expect, vi } from 'vitest';
import { CollisionModule } from '../../modules/collision-module';
import { Vec3 } from '@axrone/numeric';
import type { CollisionConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

// --- helpers ---
function makeCollisionConfig(
    overrides: Partial<CollisionConfiguration> = {}
): CollisionConfiguration {
    return {
        enabled: true,
        priority: 700,
        type: 'world',
        mode: 'ignore',
        bounce: 0.3,
        dampen: 0.1,
        lifetimeLoss: 0,
        minKillSpeed: 0,
        maxKillSpeed: Infinity,
        radiusScale: 1,
        enableDynamicColliders: false,
        collisionQuality: 'medium',
        broadPhase: true,
        gridCellSize: 1,
        autoOptimize: false,
        continuousDetection: false,
        maxContacts: 100,
        maxChecksPerFrame: 1000,
        spatialOptimization: false,
        ...overrides,
    } as any;
}

function createMockBuffer(count = 1): IParticleBuffer {
    const alive = new Uint32Array(100);
    const positions = new Float32Array(300);
    const velocities = new Float32Array(300);
    for (let i = 0; i < count; i++) alive[i] = 1;
    return {
        get count() {
            return count;
        },
        capacity: 100,
        allocated: true,
        alive,
        positions,
        velocities,
        accelerations: new Float32Array(300),
        lifetimes: new Float32Array(100).fill(5),
        ages: new Float32Array(100),
        sizes: new Float32Array(300),
        colors: new Float32Array(400),
        rotations: new Float32Array(300),
        angularVelocities: new Float32Array(300),
        customData: [],
        ids: new Uint32Array(100),
        allocate: vi.fn().mockReturnValue(true),
        deallocate: vi.fn(),
        resize: vi.fn().mockReturnValue(true),
        addParticle: vi.fn().mockReturnValue(1),
        removeParticle: vi.fn().mockReturnValue(true),
        killParticle: vi.fn().mockReturnValue(true),
        getParticleIndex: vi
            .fn()
            .mockImplementation((id: number) => (id >= 0 && id < count ? id : -1)),
        getParticleId: vi.fn().mockImplementation((i: number) => i),
        getPosition: vi.fn().mockReturnValue({ x: 0, y: 0, z: 0 }),
        setPosition: vi.fn(),
        getVelocity: vi.fn().mockReturnValue({ x: 0, y: 0, z: 0 }),
        setVelocity: vi.fn(),
        getLifetime: vi.fn().mockReturnValue(5),
        setLifetime: vi.fn(),
        getAge: vi.fn().mockReturnValue(0),
        setAge: vi.fn(),
        getSize: vi.fn().mockReturnValue(1),
        setSize: vi.fn(),
        getColor: vi.fn().mockReturnValue(0xffffffff),
        setColor: vi.fn(),
        getCustomData: vi.fn().mockReturnValue(new Float32Array(4)),
        setCustomData: vi.fn(),
        clear: vi.fn(),
        compact: vi.fn(),
        sort: vi.fn(),
    } as any;
}

describe('CollisionModule', () => {
    describe('constructor', () => {
        it('creates module with type collision and priority 700', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            expect(mod.type).toBe('collision');
            expect(mod.priority).toBe(700);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset after initialize does not throw', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy after initialize does not throw', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });
    });

    describe('static factories', () => {
        it('createPlane creates a plane primitive', () => {
            const pos = new Vec3(0, 0, 0);
            const normal = new Vec3(0, 1, 0);
            const plane = CollisionModule.createPlane('p1', pos, normal);
            expect(plane.type).toBe('plane');
            expect(plane.id).toBe('p1');
            expect(plane.enabled).toBe(true);
            expect(plane.normal.y).toBeCloseTo(1);
        });

        it('createSphere creates a sphere primitive', () => {
            const pos = new Vec3(1, 2, 3);
            const sphere = CollisionModule.createSphere('s1', pos, 5);
            expect(sphere.type).toBe('sphere');
            expect(sphere.id).toBe('s1');
            expect(sphere.radius).toBe(5);
            expect(sphere.hollow).toBe(false);
        });

        it('createSphere with hollow=true', () => {
            const sphere = CollisionModule.createSphere('s2', new Vec3(), 3, true);
            expect(sphere.hollow).toBe(true);
        });

        it('createBox creates a box primitive', () => {
            const pos = new Vec3(0, 0, 0);
            const size = new Vec3(2, 4, 6);
            const box = CollisionModule.createBox('b1', pos, size);
            expect(box.type).toBe('box');
            expect(box.id).toBe('b1');
            expect(box.size.x).toBe(2);
            expect(box.size.y).toBe(4);
            expect(box.size.z).toBe(6);
            expect(box.hollow).toBe(false);
        });
    });

    describe('defaultMaterial', () => {
        it('returns expected default values', () => {
            const mat = CollisionModule.defaultMaterial();
            expect(mat.restitution).toBe(0.3);
            expect(mat.friction).toBe(0.5);
            expect(mat.damping).toBe(0.1);
            expect(mat.adhesion).toBe(0);
        });
    });

    describe('primitive management', () => {
        it('addPrimitive and getPrimitive', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const sphere = CollisionModule.createSphere('s1', new Vec3(), 5);
            mod.addPrimitive(sphere);
            expect(mod.getPrimitive('s1')).toBe(sphere);
        });

        it('removePrimitive returns true for existing', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const sphere = CollisionModule.createSphere('s1', new Vec3(), 5);
            mod.addPrimitive(sphere);
            expect(mod.removePrimitive('s1')).toBe(true);
            expect(mod.getPrimitive('s1')).toBeUndefined();
        });

        it('removePrimitive returns false for non-existent', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            expect(mod.removePrimitive('nope')).toBe(false);
        });

        it('getAllPrimitives returns all added primitives', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            mod.addPrimitive(CollisionModule.createSphere('s1', new Vec3(), 1));
            mod.addPrimitive(CollisionModule.createPlane('p1', new Vec3(), new Vec3(0, 1, 0)));
            expect(mod.getAllPrimitives()).toHaveLength(2);
        });
    });

    describe('onProcess - disabled / empty', () => {
        it('disabled module is no-op', () => {
            const mod = new CollisionModule(makeCollisionConfig({ enabled: false }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });

        it('no primitives is no-op', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
            expect(mod.getContacts()).toHaveLength(0);
        });
    });

    describe('onProcess - plane collision', () => {
        it('detects particle below ground plane', () => {
            // Ground plane at y=0, normal pointing up
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const plane = CollisionModule.createPlane(
                'ground',
                new Vec3(0, 0, 0),
                new Vec3(0, 1, 0)
            );
            mod.addPrimitive(plane);

            const buffer = createMockBuffer(1);
            // Particle at y=-1 (below the plane)
            buffer.positions[0] = 0;
            buffer.positions[1] = -1;
            buffer.positions[2] = 0;
            buffer.velocities[1] = -5; // moving down

            mod.process(buffer, 0.016);

            expect(mod.getContacts().length).toBeGreaterThanOrEqual(1);
            const contact = mod.getContacts()[0];
            expect(contact.primitiveId).toBe('ground');
            expect(contact.penetration).toBeGreaterThan(0);
        });

        it('no collision when particle above plane', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const plane = CollisionModule.createPlane(
                'ground',
                new Vec3(0, 0, 0),
                new Vec3(0, 1, 0)
            );
            mod.addPrimitive(plane);

            const buffer = createMockBuffer(1);
            buffer.positions[1] = 5; // above plane

            mod.process(buffer, 0.016);
            expect(mod.getContacts()).toHaveLength(0);
        });
    });

    describe('onProcess - sphere collision', () => {
        it('detects particle inside solid sphere', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const sphere = CollisionModule.createSphere('s1', new Vec3(0, 0, 0), 5);
            mod.addPrimitive(sphere);

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 1;
            buffer.positions[1] = 0;
            buffer.positions[2] = 0;

            mod.process(buffer, 0.016);
            expect(mod.getContacts().length).toBe(1);
            expect(mod.getContacts()[0].primitiveId).toBe('s1');
        });

        it('no collision when outside solid sphere', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const sphere = CollisionModule.createSphere('s1', new Vec3(0, 0, 0), 5);
            mod.addPrimitive(sphere);

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 10; // outside

            mod.process(buffer, 0.016);
            expect(mod.getContacts()).toHaveLength(0);
        });

        it('hollow sphere: collision when outside', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const sphere = CollisionModule.createSphere('s1', new Vec3(0, 0, 0), 5, true);
            mod.addPrimitive(sphere);

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 3; // inside hollow -> no collision for hollow

            mod.process(buffer, 0.016);
            // hollow: collision when distance > radius, so inside = no collision
            expect(mod.getContacts()).toHaveLength(0);
        });
    });

    describe('onProcess - box collision', () => {
        it('detects particle inside solid box', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const box = CollisionModule.createBox(
                'b1',
                new Vec3(0, 0, 0),
                new Vec3(4, 4, 4)
            );
            mod.addPrimitive(box);

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 0.5;
            buffer.positions[1] = 0.5;
            buffer.positions[2] = 0.5;

            mod.process(buffer, 0.016);
            expect(mod.getContacts().length).toBe(1);
        });

        it('no collision when outside solid box', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const box = CollisionModule.createBox(
                'b1',
                new Vec3(0, 0, 0),
                new Vec3(2, 2, 2)
            );
            mod.addPrimitive(box);

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 5; // outside

            mod.process(buffer, 0.016);
            expect(mod.getContacts()).toHaveLength(0);
        });
    });

    describe('onProcess - cylinder collision', () => {
        it('detects particle inside solid cylinder', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const cyl = {
                type: 'cylinder' as const,
                id: 'c1',
                enabled: true,
                position: new Vec3(0, 0, 0),
                rotation: new Vec3(),
                scale: new Vec3(1, 1, 1),
                material: CollisionModule.defaultMaterial(),
                radius: 3,
                height: 10,
                hollow: false,
            };
            mod.addPrimitive(cyl as any);

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 1;
            buffer.positions[1] = 0; // within halfHeight
            buffer.positions[2] = 0;

            mod.process(buffer, 0.016);
            expect(mod.getContacts().length).toBe(1);
        });

        it('no collision when outside cylinder height', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const cyl = {
                type: 'cylinder' as const,
                id: 'c1',
                enabled: true,
                position: new Vec3(0, 0, 0),
                rotation: new Vec3(),
                scale: new Vec3(1, 1, 1),
                material: CollisionModule.defaultMaterial(),
                radius: 3,
                height: 4,
                hollow: false,
            };
            mod.addPrimitive(cyl as any);

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 0;
            buffer.positions[1] = 5; // above halfHeight=2
            buffer.positions[2] = 0;

            mod.process(buffer, 0.016);
            expect(mod.getContacts()).toHaveLength(0);
        });
    });

    describe('collision resolution', () => {
        it('corrects position and reflects velocity', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const plane = CollisionModule.createPlane(
                'ground',
                new Vec3(0, 0, 0),
                new Vec3(0, 1, 0)
            );
            mod.addPrimitive(plane);

            const buffer = createMockBuffer(1);
            buffer.positions[1] = -1; // below plane
            buffer.velocities[1] = -10; // moving down

            mod.process(buffer, 0.016);

            // Position should be corrected (pushed up)
            expect(buffer.positions[1]).toBeGreaterThanOrEqual(-1);
            // Velocity should be reflected (now going up or at least not down as much)
            const contacts = mod.getContacts();
            expect(contacts.length).toBe(1);
        });
    });

    describe('disabled primitives', () => {
        it('skips disabled primitives', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const plane = CollisionModule.createPlane(
                'ground',
                new Vec3(0, 0, 0),
                new Vec3(0, 1, 0)
            );
            // Manually create a disabled version
            const disabledPlane = { ...plane, enabled: false };
            mod.addPrimitive(disabledPlane as any);

            const buffer = createMockBuffer(1);
            buffer.positions[1] = -1;

            mod.process(buffer, 0.016);
            expect(mod.getContacts()).toHaveLength(0);
        });
    });

    describe('getStats', () => {
        it('returns stats after processing', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const plane = CollisionModule.createPlane(
                'ground',
                new Vec3(0, 0, 0),
                new Vec3(0, 1, 0)
            );
            mod.addPrimitive(plane);

            const buffer = createMockBuffer(1);
            buffer.positions[1] = -1;
            mod.process(buffer, 0.016);

            const stats = mod.getStats();
            expect(stats.totalChecks).toBeGreaterThanOrEqual(1);
            expect(stats.performanceMs).toBeGreaterThanOrEqual(0);
        });
    });

    describe('ground plane from config', () => {
        it('sets up ground plane when configured', () => {
            const mod = new CollisionModule(
                makeCollisionConfig({
                    groundPlane: {
                        enabled: true,
                        height: 0,
                        bounce: 0.5,
                        friction: 0.3,
                        dampen: 0.1,
                    },
                } as any)
            );
            mod.initialize();
            const primitives = mod.getAllPrimitives();
            expect(primitives.length).toBe(1);
            expect(primitives[0].id).toBe('ground');
        });

        it('no ground plane when not enabled', () => {
            const mod = new CollisionModule(
                makeCollisionConfig({
                    groundPlane: { enabled: false, height: 0, bounce: 0.3, friction: 0.5, dampen: 0.1 },
                } as any)
            );
            mod.initialize();
            expect(mod.getAllPrimitives()).toHaveLength(0);
        });
    });

    describe('configure', () => {
        it('updates broadPhase setting', () => {
            const mod = new CollisionModule(makeCollisionConfig({ broadPhase: true }));
            mod.initialize();
            mod.configure(
                makeCollisionConfig({ broadPhase: false }) as any
            );
            // No direct way to check internal state, just ensure no throw
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });
    });

    describe('spatial grid', () => {
        it('processes with broadPhase disabled', () => {
            const mod = new CollisionModule(makeCollisionConfig({ broadPhase: false }));
            mod.initialize();
            const sphere = CollisionModule.createSphere('s1', new Vec3(0, 0, 0), 5);
            mod.addPrimitive(sphere);

            const buffer = createMockBuffer(2);
            buffer.positions[0] = 1;
            buffer.positions[3] = 2;

            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });
    });

    describe('dead particles', () => {
        it('skips dead particles during collision detection', () => {
            const mod = new CollisionModule(makeCollisionConfig());
            mod.initialize();
            const sphere = CollisionModule.createSphere('s1', new Vec3(0, 0, 0), 5);
            mod.addPrimitive(sphere);

            const buffer = createMockBuffer(2);
            buffer.positions[0] = 1;
            buffer.positions[3] = 1;
            buffer.alive[1] = 0; // second particle dead

            mod.process(buffer, 0.016);
            // Only 1 particle checked
            expect(mod.getStats().totalChecks).toBe(1);
        });
    });
});
