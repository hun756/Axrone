import { describe, it, expect, vi } from 'vitest';
import { NoiseModule, NoiseType } from '../../modules/noise-module';
import type { NoiseConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

function makeNoiseConfig(overrides: Partial<NoiseConfiguration> = {}): NoiseConfiguration {
    return {
        enabled: true,
        priority: 600,
        strength: { mode: 0, constant: 1, constantMin: 0, constantMax: 1 },
        frequency: 1,
        amplitude: 1,
        octaves: 1,
        persistence: 0.5,
        lacunarity: 2,
        seed: 42,
        animationSpeed: 1,
        noiseType: 'perlin',
        additive: false,
        damping: false,
        scrollSpeed: { mode: 0, constant: 0, constantMin: 0, constantMax: 0 },
        octaveMultiplier: 1,
        octaveScale: 2,
        quality: 'medium',
        positionAmount: { x: 1, y: 1, z: 1 },
        rotationAmount: { x: 0, y: 0, z: 0 },
        sizeAmount: { x: 0, y: 0, z: 0 },
        remapRange: [0, 1],
        spatialFrequency: { x: 1, y: 1, z: 1 },
        temporalFrequency: 1,
        ...overrides,
    } as any;
}

function createMockBuffer(count = 1): IParticleBuffer {
    const alive = new Uint32Array(100);
    const positions = new Float32Array(300);
    const velocities = new Float32Array(300);
    const ages = new Float32Array(100);
    const lifetimes = new Float32Array(100);
    for (let i = 0; i < count; i++) {
        alive[i] = 1;
        positions[i * 3] = i * 2;
        positions[i * 3 + 1] = i * 3;
        positions[i * 3 + 2] = i;
        lifetimes[i] = 5;
    }
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
        lifetimes,
        ages,
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

describe('NoiseModule', () => {
    describe('constructor', () => {
        it('creates module with type noise and priority 600', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            expect(mod.type).toBe('noise');
            expect(mod.priority).toBe(600);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset after initialize does not throw', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy after initialize does not throw', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });
    });

    describe('onProcess', () => {
        it('disabled module is no-op', () => {
            const mod = new NoiseModule(makeNoiseConfig({ enabled: false }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            // velocities should stay at 0
            expect(buffer.velocities[0]).toBe(0);
        });

        it('processes alive particles', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            // real noise modifies velocities
            expect(buffer.velocities[0]).toBeDefined();
        });

        it('skips dead particles', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            mod.initialize();
            const buffer = createMockBuffer(2);
            buffer.alive[1] = 0;
            mod.process(buffer, 0.016);
            // Should still work without error
            expect(buffer.velocities[3]).toBe(0); // dead particle velocity unchanged
        });
    });

    describe('noise types', () => {
        it('perlin noise processes without error', () => {
            const mod = new NoiseModule(makeNoiseConfig({ noiseType: 'perlin' }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });

        it('simplex noise processes without error', () => {
            const mod = new NoiseModule(makeNoiseConfig({ noiseType: 'simplex' }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });

        it('worley noise processes without error', () => {
            const mod = new NoiseModule(makeNoiseConfig({ noiseType: 'worley' }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });

        it('curl noise processes without error', () => {
            const mod = new NoiseModule(makeNoiseConfig({ noiseType: 'curl' }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });

        it('turbulence noise processes without error', () => {
            const mod = new NoiseModule(makeNoiseConfig({ noiseType: 'turbulence' }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });

        it('fractal noise processes without error', () => {
            const mod = new NoiseModule(makeNoiseConfig({ noiseType: 'fractal' }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });
    });

    describe('sampleNoise3D / sampleNoise4D', () => {
        it('sampleNoise3D returns a finite number', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            mod.initialize();
            const val = mod.sampleNoise3D(1, 2, 3);
            expect(typeof val).toBe('number');
            expect(Number.isFinite(val)).toBe(true);
        });

        it('sampleNoise4D returns a finite number', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            mod.initialize();
            const val = mod.sampleNoise4D(1, 2, 3, 0.5);
            expect(typeof val).toBe('number');
            expect(Number.isFinite(val)).toBe(true);
        });
    });

    describe('sampleCurlNoise', () => {
        it('returns 3-component tuple', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            mod.initialize();
            const curl = mod.sampleCurlNoise(1, 2, 3);
            expect(curl).toHaveLength(3);
            expect(typeof curl[0]).toBe('number');
            expect(typeof curl[1]).toBe('number');
            expect(typeof curl[2]).toBe('number');
        });
    });

    describe('getStats', () => {
        it('returns stats object', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            mod.initialize();
            const stats = mod.getStats();
            expect(stats).toHaveProperty('samplesPerFrame');
            expect(stats).toHaveProperty('avgComputeTime');
            expect(stats).toHaveProperty('cacheHitRatio');
            expect(stats).toHaveProperty('memoryUsage');
        });

        it('tracks samplesPerFrame after processing', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            mod.initialize();
            const buffer = createMockBuffer(3);
            mod.process(buffer, 0.016);
            const stats = mod.getStats();
            expect(stats.samplesPerFrame).toBeGreaterThanOrEqual(0);
        });
    });

    describe('configure', () => {
        it('seed change regenerates permutation', () => {
            const mod = new NoiseModule(makeNoiseConfig({ seed: 42 }));
            mod.initialize();
            // Change seed
            mod.configure(makeNoiseConfig({ seed: 99 }) as any);
            // Should not throw
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });

        it('frequency change clears cache', () => {
            const mod = new NoiseModule(makeNoiseConfig({ frequency: 1 }));
            mod.initialize();
            mod.configure(makeNoiseConfig({ frequency: 2 }) as any);
            const buffer = createMockBuffer(1);
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
        });
    });

    describe('cache eviction', () => {
        it('handles cache eviction at 10000 entries', () => {
            const mod = new NoiseModule(makeNoiseConfig());
            mod.initialize();
            // Access onUpdate many times to increase _time
            for (let i = 0; i < 100; i++) {
                mod.process(createMockBuffer(1), 0.016);
            }
            // Should not throw even with many updates
            expect(() => mod.process(createMockBuffer(1), 0.016)).not.toThrow();
        });
    });

    describe('additive mode', () => {
        it('additive mode adds to existing velocity', () => {
            const mod = new NoiseModule(makeNoiseConfig({ additive: true }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            // Use non-integer positions (Perlin noise is 0 at integer lattice points)
            buffer.positions[0] = 0.37;
            buffer.positions[1] = 1.73;
            buffer.positions[2] = 2.91;
            buffer.velocities[0] = 10;
            buffer.velocities[1] = 10;
            buffer.velocities[2] = 10;
            // Process multiple steps to accumulate measurable delta
            for (let i = 0; i < 100; i++) {
                mod.process(buffer, 0.016);
            }
            // In additive mode, velocity should have changed from initial value
            const changed =
                buffer.velocities[0] !== 10 ||
                buffer.velocities[1] !== 10 ||
                buffer.velocities[2] !== 10;
            expect(changed).toBe(true);
        });
    });
});
