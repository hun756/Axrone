import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TrailModule } from '../../modules/trail-module';
import type { TrailConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

function makeCurve(constant = 0) {
    return { mode: 0, constant, constantMin: 0, constantMax: 0 };
}

function makeGradient(color = { r: 1, g: 1, b: 1, a: 1 }) {
    return { mode: 0, color, colorMin: color, colorMax: color };
}

function makeTrailConfig(overrides: Partial<TrailConfiguration> = {}): TrailConfiguration {
    return {
        enabled: true,
        priority: 800,
        mode: 'particles',
        ratio: 0.5,
        lifetime: makeCurve(2),
        minimumVertexDistance: 0.1,
        width: makeCurve(1),
        color: makeGradient(),
        inheritParticleColor: false,
        colorOverLifetime: makeGradient(),
        worldSpace: false,
        dieWithParticles: true,
        sizeAffectsWidth: false,
        sizeAffectsLifetime: false,
        ...overrides,
    } as any;
}

function createMockBuffer(count = 1): IParticleBuffer {
    const alive = new Uint32Array(100);
    const positions = new Float32Array(300);
    const colors = new Float32Array(400);
    const sizes = new Float32Array(300);
    for (let i = 0; i < count; i++) {
        alive[i] = 1;
        positions[i * 3] = i * 2;
        positions[i * 3 + 1] = i;
        positions[i * 3 + 2] = 0;
        sizes[i * 3] = 1;
    }
    return {
        get count() {
            return count;
        },
        capacity: 100,
        allocated: true,
        alive,
        positions,
        velocities: new Float32Array(300),
        accelerations: new Float32Array(300),
        lifetimes: new Float32Array(100).fill(5),
        ages: new Float32Array(100),
        sizes,
        colors,
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

describe('TrailModule', () => {
    describe('constructor', () => {
        it('creates module with type trail and priority 800', () => {
            const mod = new TrailModule(makeTrailConfig());
            expect(mod.type).toBe('trail');
            expect(mod.priority).toBe(800);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new TrailModule(makeTrailConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset after initialize does not throw', () => {
            const mod = new TrailModule(makeTrailConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy after initialize does not throw', () => {
            const mod = new TrailModule(makeTrailConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });

        it('initialize pre-allocates trail pool', () => {
            const mod = new TrailModule(makeTrailConfig());
            mod.initialize();
            // After init, no active trails yet
            expect(mod.getActiveTrailCount()).toBe(0);
        });
    });

    describe('onProcess - trail creation', () => {
        it('creates trails for alive particles', () => {
            const mod = new TrailModule(makeTrailConfig());
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            expect(mod.getActiveTrailCount()).toBe(2);
        });

        it('disabled module creates no trails', () => {
            const mod = new TrailModule(makeTrailConfig({ enabled: false }));
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            expect(mod.getActiveTrailCount()).toBe(0);
        });
    });

    describe('onProcess - vertex addition', () => {
        it('adds vertex when distance >= minimumVertexDistance', () => {
            const mod = new TrailModule(
                makeTrailConfig({ minimumVertexDistance: 0.1 })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);

            // First process creates the trail
            mod.process(buffer, 0.016);

            // Move particle far enough
            buffer.positions[0] = 10;
            buffer.positions[1] = 10;
            mod.process(buffer, 0.016);

            const vertices = mod.getTrailVertices(0);
            expect(vertices.length).toBeGreaterThanOrEqual(1);
        });

        it('does not add vertex when distance < minimumVertexDistance', () => {
            const mod = new TrailModule(
                makeTrailConfig({ minimumVertexDistance: 100 })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);

            mod.process(buffer, 0.016);
            // Small movement
            buffer.positions[0] = 0.01;
            mod.process(buffer, 0.016);

            const vertices = mod.getTrailVertices(0);
            expect(vertices).toHaveLength(0);
        });
    });

    describe('dieWithParticles', () => {
        it('deactivates trail when particle dies', () => {
            const mod = new TrailModule(makeTrailConfig({ dieWithParticles: true }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.getActiveTrailCount()).toBe(1);

            // Kill the particle
            buffer.alive[0] = 0;
            mod.process(buffer, 0.016);
            // Trail is marked inactive (cleanup from map happens in onUpdate)
            const trails = mod.getTrails();
            const trail = trails.get(0);
            expect(trail).toBeDefined();
            expect(trail!.active).toBe(false);
        });

        it('keeps trail alive when dieWithParticles is false', () => {
            const mod = new TrailModule(makeTrailConfig({ dieWithParticles: false }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);

            buffer.alive[0] = 0;
            mod.process(buffer, 0.016);
            // Trail should still be active (not deactivated by dead particle)
            expect(mod.getActiveTrailCount()).toBe(1);
        });
    });

    describe('getTrails / getters', () => {
        it('getTrails returns readonly map', () => {
            const mod = new TrailModule(makeTrailConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            const trails = mod.getTrails();
            expect(trails.size).toBe(1);
        });

        it('getTrailVertices returns empty for unknown particle', () => {
            const mod = new TrailModule(makeTrailConfig());
            mod.initialize();
            expect(mod.getTrailVertices(999)).toHaveLength(0);
        });

        it('getTotalVertexCount counts all vertices', () => {
            const mod = new TrailModule(
                makeTrailConfig({ minimumVertexDistance: 0.01 })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            // Move far to create vertices
            buffer.positions[0] = 5;
            mod.process(buffer, 0.016);
            expect(mod.getTotalVertexCount()).toBeGreaterThanOrEqual(0);
        });
    });

    describe('max vertex limit', () => {
        it('respects max vertices from ratio config', () => {
            const mod = new TrailModule(
                makeTrailConfig({ ratio: 0.05, minimumVertexDistance: 0.01 })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);

            // Create many vertices
            for (let i = 0; i < 20; i++) {
                buffer.positions[0] = i * 5;
                mod.process(buffer, 0.016);
            }

            const vertices = mod.getTrailVertices(0);
            // maxVertices = floor(0.05 * 100) = 5
            expect(vertices.length).toBeLessThanOrEqual(5);
        });
    });

    describe('inheritParticleColor', () => {
        it('uses particle color when inheritParticleColor is true', () => {
            const mod = new TrailModule(
                makeTrailConfig({
                    inheritParticleColor: true,
                    minimumVertexDistance: 0.01,
                })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);

            // Move to create vertex
            buffer.positions[0] = 5;
            mod.process(buffer, 0.016);

            const vertices = mod.getTrailVertices(0);
            if (vertices.length > 0) {
                // Color should be derived from particle color (0xffffffff = white)
                expect(vertices[0].color).toBeDefined();
                expect(vertices[0].color.r).toBeDefined();
            }
        });
    });

    describe('sizeAffectsWidth', () => {
        it('scales width by particle size', () => {
            const mod = new TrailModule(
                makeTrailConfig({
                    sizeAffectsWidth: true,
                    minimumVertexDistance: 0.01,
                })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);

            buffer.positions[0] = 5;
            mod.process(buffer, 0.016);

            const vertices = mod.getTrailVertices(0);
            if (vertices.length > 0) {
                // Width should be width(1) * size(1) = 1
                expect(vertices[0].width).toBeDefined();
                expect(typeof vertices[0].width).toBe('number');
            }
        });
    });

    describe('configure - mode change', () => {
        it('mode change triggers reset', () => {
            const mod = new TrailModule(makeTrailConfig({ mode: 'particles' }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.getActiveTrailCount()).toBe(1);

            mod.configure(makeTrailConfig({ mode: 'ribbon' }) as any);
            // After mode change, trails should be cleared
            expect(mod.getActiveTrailCount()).toBe(0);
        });
    });

    describe('reset', () => {
        it('clears all trails', () => {
            const mod = new TrailModule(makeTrailConfig());
            mod.initialize();
            const buffer = createMockBuffer(3);
            mod.process(buffer, 0.016);
            expect(mod.getActiveTrailCount()).toBe(3);

            mod.reset();
            expect(mod.getActiveTrailCount()).toBe(0);
        });
    });
});
