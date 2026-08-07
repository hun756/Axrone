import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Vec3 } from '@axrone/numeric';
import { SizeModule } from '../../modules/size-module';
import type { SizeConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

function makeCurve(constant = 1, overrides: Record<string, any> = {}) {
    return { mode: 0, constant, constantMin: 0, constantMax: 0, curveMultiplier: 1, ...overrides };
}

function makeSizeConfig(overrides: Partial<SizeConfiguration> = {}): SizeConfiguration {
    return {
        enabled: true,
        priority: 400,
        size: makeCurve(1),
        sizeX: makeCurve(1),
        sizeY: makeCurve(1),
        sizeZ: makeCurve(1),
        separateAxes: false,
        minSize: 0.01,
        maxSize: 100,
        speedInfluence: 0,
        sizeDamping: 0,
        sizeAcceleration: 0,
        randomVariation: 0,
        animationMode: 'constant',
        inheritFromParent: false,
        scaleWithDistance: false,
        distanceScaleFactor: 0,
        ...overrides,
    };
}

function createMockBuffer(count = 1): IParticleBuffer {
    const alive = new Uint32Array(1000);
    const sizes = new Float32Array(3000);
    const ages = new Float32Array(1000);
    const lifetimes = new Float32Array(1000);
    const velocities = new Float32Array(3000);

    for (let i = 0; i < count; i++) {
        alive[i] = 1;
        lifetimes[i] = 5;
        ages[i] = 0;
    }

    return {
        get count() { return count; },
        capacity: 1000,
        allocated: true,
        alive,
        positions: new Float32Array(3000),
        velocities,
        accelerations: new Float32Array(3000),
        lifetimes,
        ages,
        sizes,
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
        getParticleIndex: vi.fn().mockImplementation((id) => (id >= 0 && id < count ? id : -1)),
        getParticleId: vi.fn().mockImplementation((i) => i),
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

describe('SizeModule', () => {
    describe('constructor', () => {
        it('creates module with correct type and priority', () => {
            const mod = new SizeModule(makeSizeConfig());
            expect(mod.type).toBe('size');
            expect(mod.priority).toBe(400);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new SizeModule(makeSizeConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset after initialize does not throw', () => {
            const mod = new SizeModule(makeSizeConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy after initialize does not throw', () => {
            const mod = new SizeModule(makeSizeConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });
    });

    describe('onProcess - uniform size', () => {
        it('disabled module is no-op', () => {
            const mod = new SizeModule(makeSizeConfig({ enabled: false }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(buffer.sizes[0]).toBe(0);
        });

        it('sets uniform size from constant curve', () => {
            const mod = new SizeModule(makeSizeConfig({ size: makeCurve(2.5) }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(buffer.sizes[0]).toBeCloseTo(2.5);
            expect(buffer.sizes[1]).toBeCloseTo(2.5);
            expect(buffer.sizes[2]).toBeCloseTo(2.5);
        });

        it('clamps size to min/max', () => {
            const mod = new SizeModule(makeSizeConfig({
                size: makeCurve(200),
                minSize: 0.01,
                maxSize: 50,
            }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(buffer.sizes[0]).toBeLessThanOrEqual(50);
        });

        it('processes multiple particles', () => {
            const mod = new SizeModule(makeSizeConfig({ size: makeCurve(3) }));
            mod.initialize();
            const buffer = createMockBuffer(3);
            mod.process(buffer, 0.016);
            expect(buffer.sizes[0]).toBeCloseTo(3);
            expect(buffer.sizes[3]).toBeCloseTo(3);
            expect(buffer.sizes[6]).toBeCloseTo(3);
        });

        it('skips dead particles', () => {
            const mod = new SizeModule(makeSizeConfig({ size: makeCurve(5) }));
            mod.initialize();
            const buffer = createMockBuffer(2);
            buffer.alive[1] = 0;
            mod.process(buffer, 0.016);
            expect(buffer.sizes[0]).toBeCloseTo(5);
            expect(buffer.sizes[3]).toBe(0); // dead particle unchanged
        });
    });

    describe('onProcess - separate axes', () => {
        it('sets different sizes per axis', () => {
            const mod = new SizeModule(makeSizeConfig({
                separateAxes: true,
                sizeX: makeCurve(1),
                sizeY: makeCurve(2),
                sizeZ: makeCurve(3),
            }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(buffer.sizes[0]).toBeCloseTo(1);
            expect(buffer.sizes[1]).toBeCloseTo(2);
            expect(buffer.sizes[2]).toBeCloseTo(3);
        });
    });

    describe('speed influence', () => {
        it('modifies size based on velocity', () => {
            const mod = new SizeModule(makeSizeConfig({
                size: makeCurve(1),
                speedInfluence: 1,
            }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.velocities[0] = 10;
            mod.process(buffer, 0.016);
            // Speed influence increases size
            expect(buffer.sizes[0]).toBeGreaterThanOrEqual(1);
        });
    });

    describe('size damping', () => {
        it('exponentially approaches target size', () => {
            const mod = new SizeModule(makeSizeConfig({
                size: makeCurve(5),
                sizeDamping: 2,
            }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.sizes[0] = 1; // start at 1
            mod.process(buffer, 0.016);
            // Should move towards 5 but not reach it immediately
            expect(buffer.sizes[0]).toBeGreaterThan(1);
        });
    });

    describe('getStats', () => {
        it('returns stats object', () => {
            const mod = new SizeModule(makeSizeConfig());
            mod.initialize();
            const stats = mod.getStats();
            expect(stats.minSize).toBeDefined();
            expect(stats.maxSize).toBeDefined();
            expect(stats.avgSize).toBeDefined();
        });

        it('updates stats after processing', () => {
            const mod = new SizeModule(makeSizeConfig({ size: makeCurve(5) }));
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            const stats = mod.getStats();
            expect(stats.avgSize).toBeGreaterThan(0);
        });
    });

    describe('setInitialSize', () => {
        it('sets uniform size for particle', () => {
            const mod = new SizeModule(makeSizeConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016); // ensure buffers are allocated
            mod.setInitialSize(0, 3);
            // Internal state, tested indirectly
        });

        it('sets Vec3 size for particle', () => {
            const mod = new SizeModule(makeSizeConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            mod.setInitialSize(0, new Vec3(1, 2, 3));
        });

        it('ignores out-of-range index', () => {
            const mod = new SizeModule(makeSizeConfig());
            mod.initialize();
            expect(() => mod.setInitialSize(-1, 5)).not.toThrow();
        });
    });

    describe('applyAdvancedEffects', () => {
        it('returns base size when no effects', () => {
            const mod = new SizeModule(makeSizeConfig());
            mod.initialize();
            const result = mod.applyAdvancedEffects(0, 5, 0);
            expect(result).toBe(5);
        });

        it('applies random variation', () => {
            const mod = new SizeModule(makeSizeConfig({ randomVariation: 0.5 }));
            mod.initialize();
            const result = mod.applyAdvancedEffects(0, 5, 0);
            expect(result).not.toBe(5);
            expect(result).toBeGreaterThan(0);
        });

        it('applies custom animation pulse', () => {
            const mod = new SizeModule(makeSizeConfig({ animationMode: 'custom' }));
            mod.initialize();
            const r1 = mod.applyAdvancedEffects(0, 5, 0);
            const r2 = mod.applyAdvancedEffects(0, 5, 0.125);
            // t=0: sin(0)=0 -> size=5*(1+0)=5
            // t=0.125: sin(0.125*2*PI*2)=sin(PI/2)=1 -> size=5*(1+0.2)=6
            expect(r1).toBeCloseTo(5, 0);
            expect(r2).toBeCloseTo(6, 0);
        });
    });

    describe('calculateDistanceScale', () => {
        it('returns 1 when scaleWithDistance is false', () => {
            const mod = new SizeModule(makeSizeConfig({ scaleWithDistance: false }));
            mod.initialize();
            const scale = mod.calculateDistanceScale(new Vec3(0, 0, 0), new Vec3(10, 0, 0));
            expect(scale).toBe(1);
        });

        it('returns smaller value for farther distance', () => {
            const mod = new SizeModule(makeSizeConfig({
                scaleWithDistance: true,
                distanceScaleFactor: 0.1,
            }));
            mod.initialize();
            const near = mod.calculateDistanceScale(new Vec3(0, 0, 0), new Vec3(1, 0, 0));
            const far = mod.calculateDistanceScale(new Vec3(0, 0, 0), new Vec3(100, 0, 0));
            expect(far).toBeLessThan(near);
        });
    });

    describe('buffer capacity', () => {
        it('auto-grows buffer for large particle counts', () => {
            const mod = new SizeModule(makeSizeConfig());
            mod.initialize();
            const buffer = createMockBuffer(100);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });
    });

    describe('configure', () => {
        it('curve change rebuilds caches', () => {
            const mod = new SizeModule(makeSizeConfig({ size: makeCurve(1) }));
            mod.initialize();
            expect(() =>
                mod.configure(makeSizeConfig({ size: makeCurve(5) }))
            ).not.toThrow();
        });
    });
});
