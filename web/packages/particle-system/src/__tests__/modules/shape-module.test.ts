import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ShapeModule } from '../../modules/shape-module';
import type { ShapeConfiguration } from '../../core/configuration';
import { EmitterShape } from '../../types';
import type { IParticleBuffer } from '../../core/interfaces';

function makeShapeConfig(overrides: Partial<ShapeConfiguration> = {}): ShapeConfiguration {
    return {
        enabled: true,
        priority: 50,
        shape: EmitterShape.Point,
        radius: 5,
        radiusThickness: 1,
        angle: Math.PI / 4,
        length: 10,
        boxSize: { x: 10, y: 10, z: 10 },
        position: { x: 0, y: 0, z: 0 },
        rotation: { x: 0, y: 0, z: 0 },
        scale: { x: 1, y: 1, z: 1 },
        alignToDirection: false,
        randomizeDirection: false,
        spherizeDirection: false,
        randomizePosition: false,
        ...overrides,
    };
}

function createMockBuffer(): IParticleBuffer {
    return {
        get count() { return 1; },
        capacity: 1000,
        allocated: true,
        alive: new Uint32Array(1000),
        positions: new Float32Array(3000),
        velocities: new Float32Array(3000),
        accelerations: new Float32Array(3000),
        lifetimes: new Float32Array(1000),
        ages: new Float32Array(1000),
        sizes: new Float32Array(3000),
        colors: new Float32Array(4000),
        rotations: new Float32Array(3000),
        angularVelocities: new Float32Array(3000),
        customData: [],
        ids: new Uint32Array(1000),
        allocate: vi.fn().mockReturnValue(true),
        deallocate: vi.fn(),
        resize: vi.fn().mockReturnValue(true),
        addParticle: vi.fn().mockReturnValue(1),
        removeParticle: vi.fn().mockReturnValue(true),
        killParticle: vi.fn().mockReturnValue(true),
        getParticleIndex: vi.fn().mockReturnValue(0),
        getParticleId: vi.fn().mockReturnValue(1),
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

describe('ShapeModule', () => {
    describe('constructor', () => {
        it('creates module with correct type and priority', () => {
            const mod = new ShapeModule(makeShapeConfig());
            expect(mod.type).toBe('shape');
            expect(mod.priority).toBe(50);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new ShapeModule(makeShapeConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset does not throw', () => {
            const mod = new ShapeModule(makeShapeConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy does not throw', () => {
            const mod = new ShapeModule(makeShapeConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });
    });

    describe('getEmissionPosition', () => {
        it('Point shape returns base position', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Point,
                    position: { x: 1, y: 2, z: 3 },
                })
            );
            mod.initialize();
            const pos = mod.getEmissionPosition();
            expect(pos.x).toBe(1);
            expect(pos.y).toBe(2);
            expect(pos.z).toBe(3);
        });

        it('Sphere shape stays within radius', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Sphere,
                    radius: 5,
                    radiusThickness: 1,
                    position: { x: 0, y: 0, z: 0 },
                })
            );
            mod.initialize();
            for (let i = 0; i < 100; i++) {
                const pos = mod.getEmissionPosition();
                const dist = Math.sqrt(pos.x ** 2 + pos.y ** 2 + pos.z ** 2);
                expect(dist).toBeLessThanOrEqual(5.01);
            }
        });

        it('Sphere with thickness 1 is shell only', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Sphere,
                    radius: 5,
                    radiusThickness: 1,
                    position: { x: 0, y: 0, z: 0 },
                })
            );
            mod.initialize();
            for (let i = 0; i < 50; i++) {
                const pos = mod.getEmissionPosition();
                const dist = Math.sqrt(pos.x ** 2 + pos.y ** 2 + pos.z ** 2);
                // thickness=1 -> r = radius * (1 + 0 * random) = radius (shell)
                expect(dist).toBeCloseTo(5, 0);
            }
        });

        it('Sphere with thickness 0 fills interior', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Sphere,
                    radius: 5,
                    radiusThickness: 0,
                    position: { x: 0, y: 0, z: 0 },
                })
            );
            mod.initialize();
            let hasInterior = false;
            for (let i = 0; i < 100; i++) {
                const pos = mod.getEmissionPosition();
                const dist = Math.sqrt(pos.x ** 2 + pos.y ** 2 + pos.z ** 2);
                expect(dist).toBeLessThanOrEqual(5.01);
                if (dist < 4) hasInterior = true;
            }
            expect(hasInterior).toBe(true);
        });

        it('Hemisphere shape has y >= 0', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Hemisphere,
                    radius: 5,
                    radiusThickness: 1,
                    position: { x: 0, y: 0, z: 0 },
                })
            );
            mod.initialize();
            for (let i = 0; i < 100; i++) {
                const pos = mod.getEmissionPosition();
                expect(pos.y).toBeGreaterThanOrEqual(0);
            }
        });

        it('Circle shape stays in XZ plane within radius', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Circle,
                    radius: 5,
                    radiusThickness: 1,
                    position: { x: 0, y: 0, z: 0 },
                })
            );
            mod.initialize();
            for (let i = 0; i < 100; i++) {
                const pos = mod.getEmissionPosition();
                const dist2D = Math.sqrt(pos.x ** 2 + pos.z ** 2);
                expect(dist2D).toBeLessThanOrEqual(5.01);
                expect(pos.y).toBe(0); // y unchanged from position
            }
        });

        it('Box shape stays within boxSize', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Box,
                    boxSize: { x: 10, y: 20, z: 30 },
                    position: { x: 0, y: 0, z: 0 },
                })
            );
            mod.initialize();
            for (let i = 0; i < 100; i++) {
                const pos = mod.getEmissionPosition();
                expect(Math.abs(pos.x)).toBeLessThanOrEqual(5.01);
                expect(Math.abs(pos.y)).toBeLessThanOrEqual(10.01);
                expect(Math.abs(pos.z)).toBeLessThanOrEqual(15.01);
            }
        });

        it('Cone shape stays within cone bounds', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Cone,
                    radius: 5,
                    angle: Math.PI / 4,
                    length: 10,
                    position: { x: 0, y: 0, z: 0 },
                })
            );
            mod.initialize();
            for (let i = 0; i < 100; i++) {
                const pos = mod.getEmissionPosition();
                expect(pos.y).toBeGreaterThanOrEqual(0);
                expect(pos.y).toBeLessThanOrEqual(10.01);
            }
        });

        it('Line shape offsets along Y axis', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Line,
                    length: 20,
                    position: { x: 0, y: 0, z: 0 },
                })
            );
            mod.initialize();
            for (let i = 0; i < 100; i++) {
                const pos = mod.getEmissionPosition();
                expect(pos.y).toBeGreaterThanOrEqual(-10.01);
                expect(pos.y).toBeLessThanOrEqual(10.01);
                expect(pos.x).toBe(0);
                expect(pos.z).toBe(0);
            }
        });

        it('randomizePosition adds noise', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Point,
                    position: { x: 0, y: 0, z: 0 },
                    randomizePosition: true,
                })
            );
            mod.initialize();
            // With noise of 0.1, at least some positions should be non-zero
            let hasNonZero = false;
            for (let i = 0; i < 100; i++) {
                const pos = mod.getEmissionPosition();
                if (pos.x !== 0 || pos.y !== 0 || pos.z !== 0) {
                    hasNonZero = true;
                }
                // Noise is ±0.05
                expect(Math.abs(pos.x)).toBeLessThanOrEqual(0.06);
                expect(Math.abs(pos.y)).toBeLessThanOrEqual(0.06);
                expect(Math.abs(pos.z)).toBeLessThanOrEqual(0.06);
            }
            expect(hasNonZero).toBe(true);
        });
    });

    describe('getEmissionDirection', () => {
        it('returns cached direction for Point shape', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Point,
                    rotation: { x: 0, y: 0, z: 0 },
                })
            );
            mod.initialize();
            const dir = mod.getEmissionDirection();
            // Default cached direction: sin(0)=0, 0, cos(0)=1
            expect(dir.x).toBeCloseTo(0);
            expect(dir.y).toBe(0);
            expect(dir.z).toBeCloseTo(1);
        });

        it('cached direction reflects rotation', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Point,
                    rotation: { x: 0, y: Math.PI / 2, z: 0 },
                })
            );
            mod.initialize();
            const dir = mod.getEmissionDirection();
            expect(dir.x).toBeCloseTo(1); // sin(PI/2)
            expect(dir.z).toBeCloseTo(0); // cos(PI/2) ≈ 0
        });

        it('alignToDirection with Sphere gives radial direction', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Sphere,
                    alignToDirection: true,
                })
            );
            mod.initialize();
            const dir = mod.getEmissionDirection();
            // Radial direction should be unit length
            const len = Math.sqrt(dir.x ** 2 + dir.y ** 2 + dir.z ** 2);
            expect(len).toBeCloseTo(1, 4);
        });

        it('alignToDirection with Cone gives cone direction', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Cone,
                    angle: Math.PI / 4,
                    alignToDirection: true,
                })
            );
            mod.initialize();
            const dir = mod.getEmissionDirection();
            const len = Math.sqrt(dir.x ** 2 + dir.y ** 2 + dir.z ** 2);
            expect(len).toBeCloseTo(1, 4);
        });

        it('randomizeDirection adds variation', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Point,
                    randomizeDirection: true,
                })
            );
            mod.initialize();
            const dirs = new Set<string>();
            for (let i = 0; i < 20; i++) {
                const dir = mod.getEmissionDirection();
                dirs.add(`${dir.x.toFixed(3)},${dir.y.toFixed(3)},${dir.z.toFixed(3)}`);
            }
            // Should have multiple unique directions
            expect(dirs.size).toBeGreaterThan(1);
        });

        it('spherizeDirection normalizes to unit length', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Point,
                    spherizeDirection: true,
                    rotation: { x: 0, y: 0.5, z: 0 },
                })
            );
            mod.initialize();
            const dir = mod.getEmissionDirection();
            const len = Math.sqrt(dir.x ** 2 + dir.y ** 2 + dir.z ** 2);
            expect(len).toBeCloseTo(1, 4);
        });
    });

    describe('onConfigure', () => {
        it('updates cached direction when shape changes', () => {
            const mod = new ShapeModule(
                makeShapeConfig({
                    shape: EmitterShape.Point,
                    rotation: { x: 0, y: 0, z: 0 },
                })
            );
            mod.initialize();
            const dir1 = mod.getEmissionDirection();

            mod.configure(
                makeShapeConfig({
                    shape: EmitterShape.Sphere,
                    rotation: { x: 0, y: Math.PI / 2, z: 0 },
                })
            );
            const dir2 = mod.getEmissionDirection();
            // Direction should change with rotation (sin(0)=0 vs sin(PI/2)=1)
            expect(dir2.x).toBeCloseTo(1, 4);
            expect(dir1.x).toBeCloseTo(0, 4);
        });

        it('updates cached direction when rotation changes', () => {
            const mod = new ShapeModule(
                makeShapeConfig({ rotation: { x: 0, y: 0, z: 0 } })
            );
            mod.initialize();

            mod.configure(
                makeShapeConfig({ rotation: { x: 0, y: Math.PI / 2, z: 0 } })
            );
            const dir = mod.getEmissionDirection();
            expect(dir.x).toBeCloseTo(1, 4);
        });
    });

    describe('process', () => {
        it('process is a no-op (does not modify buffer)', () => {
            const mod = new ShapeModule(makeShapeConfig());
            mod.initialize();
            const buffer = createMockBuffer();
            mod.process(buffer, 0.016);
            // ShapeModule doesn't modify buffer in onProcess
            expect(buffer.setPosition).not.toHaveBeenCalled();
        });
    });
});
