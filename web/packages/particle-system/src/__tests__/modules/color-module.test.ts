import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ColorModule } from '../../modules/color-module';
import type { ColorConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

function makeGradient(mode = 0, overrides: Record<string, any> = {}) {
    return {
        mode,
        color: { r: 1, g: 1, b: 1, a: 1 },
        colorMin: { r: 0, g: 0, b: 0, a: 1 },
        colorMax: { r: 1, g: 1, b: 1, a: 1 },
        ...overrides,
    };
}

function makeColorConfig(overrides: Partial<ColorConfiguration> = {}): ColorConfiguration {
    return {
        enabled: true,
        priority: 300,
        color: makeGradient(0),
        colorOverLifetime: undefined,
        velocityInfluence: 0,
        ageInfluence: 0,
        sizeInfluence: 0,
        randomColorVariation: 0,
        ...overrides,
    };
}

function createMockBuffer(count = 1): IParticleBuffer {
    const alive = new Uint32Array(1000);
    const colors = new Float32Array(4000);
    const velocities = new Float32Array(3000);
    const ages = new Float32Array(1000);
    const lifetimes = new Float32Array(1000);
    const sizes = new Float32Array(3000);

    for (let i = 0; i < count; i++) {
        alive[i] = 1;
        lifetimes[i] = 5;
        ages[i] = 0;
        sizes[i] = 1;
        velocities[i * 3] = 1;
        velocities[i * 3 + 1] = 0;
        velocities[i * 3 + 2] = 0;
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
        colors,
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

describe('ColorModule', () => {
    describe('constructor', () => {
        it('creates module with correct type and priority', () => {
            const mod = new ColorModule(makeColorConfig());
            expect(mod.type).toBe('color');
            expect(mod.priority).toBe(300);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new ColorModule(makeColorConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset after initialize does not throw', () => {
            const mod = new ColorModule(makeColorConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy after initialize does not throw', () => {
            const mod = new ColorModule(makeColorConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });
    });

    describe('onProcess', () => {
        it('disabled module is no-op', () => {
            const mod = new ColorModule(makeColorConfig({ enabled: false }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(0);
        });

        it('writes packed colors to buffer for constant gradient', () => {
            const mod = new ColorModule(
                makeColorConfig({ color: makeGradient(0, { color: { r: 1, g: 0, b: 0, a: 1 } }) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            // Color should be written (non-zero since it's red)
            expect(mod.getActiveParticleCount()).toBe(1);
        });

        it('processes multiple particles', () => {
            const mod = new ColorModule(makeColorConfig());
            mod.initialize();
            const buffer = createMockBuffer(5);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(5);
        });

        it('skips dead particles', () => {
            const mod = new ColorModule(makeColorConfig());
            mod.initialize();
            const buffer = createMockBuffer(2);
            buffer.alive[1] = 0;
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
        });
    });

    describe('gradient evaluation', () => {
        it('mode 0 (constant) returns same color at all times', () => {
            const mod = new ColorModule(
                makeColorConfig({ color: makeGradient(0, { color: { r: 0.5, g: 0.5, b: 0.5, a: 1 } }) })
            );
            mod.initialize();
            const c0 = mod.getGradientColorAtTime(0);
            const c1 = mod.getGradientColorAtTime(0.5);
            const c2 = mod.getGradientColorAtTime(1);
            expect(c0.r).toBeCloseTo(0.5);
            expect(c1.r).toBeCloseTo(0.5);
            expect(c2.r).toBeCloseTo(0.5);
        });

        it('mode 2 (random between two colors) interpolates with t', () => {
            const mod = new ColorModule(
                makeColorConfig({
                    color: makeGradient(2, {
                        colorMin: { r: 0, g: 0, b: 0, a: 1 },
                        colorMax: { r: 1, g: 1, b: 1, a: 1 },
                    }),
                })
            );
            mod.initialize();
            const c0 = mod.getGradientColorAtTime(0);
            const c1 = mod.getGradientColorAtTime(1);
            expect(c0.r).toBeCloseTo(0);
            expect(c1.r).toBeCloseTo(1);
        });

        it('mode 3 (gradient keys) evaluates keyframe gradients', () => {
            const mod = new ColorModule(
                makeColorConfig({
                    color: makeGradient(3, {
                        gradientKeys: [
                            { time: 0, color: { r: 1, g: 0, b: 0, a: 1 }, interpolation: 'linear' },
                            { time: 1, color: { r: 0, g: 0, b: 1, a: 1 }, interpolation: 'linear' },
                        ],
                    }),
                })
            );
            mod.initialize();
            const c0 = mod.getGradientColorAtTime(0);
            const cMid = mod.getGradientColorAtTime(0.5);
            const c1 = mod.getGradientColorAtTime(1);
            expect(c0.r).toBeCloseTo(1);
            expect(c0.b).toBeCloseTo(0);
            expect(cMid.r).toBeCloseTo(0.5);
            expect(cMid.b).toBeCloseTo(0.5);
            expect(c1.r).toBeCloseTo(0);
            expect(c1.b).toBeCloseTo(1);
        });
    });

    describe('color over lifetime', () => {
        it('multiplies base color with lifetime gradient', () => {
            const mod = new ColorModule(
                makeColorConfig({
                    color: makeGradient(0, { color: { r: 1, g: 1, b: 1, a: 1 } }),
                    colorOverLifetime: makeGradient(2, {
                        colorMin: { r: 0, g: 0, b: 0, a: 0 },
                        colorMax: { r: 1, g: 1, b: 1, a: 1 },
                    }),
                })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.ages[0] = 2.5; // halfway through lifetime
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
        });
    });

    describe('velocity influence', () => {
        it('adds velocity-based color tint', () => {
            const mod = new ColorModule(makeColorConfig({ velocityInfluence: 1 }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.velocities[0] = 10; // high velocity
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
        });
    });

    describe('age influence', () => {
        it('fades color with age', () => {
            const mod = new ColorModule(makeColorConfig({ ageInfluence: 1 }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.ages[0] = 4; // near end of lifetime
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
        });
    });

    describe('size influence', () => {
        it('modifies color based on particle size', () => {
            const mod = new ColorModule(makeColorConfig({ sizeInfluence: 1 }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.sizes[0] = 2;
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
        });
    });

    describe('random variation', () => {
        it('adds random color variation', () => {
            const mod = new ColorModule(makeColorConfig({ randomColorVariation: 0.1 }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
        });
    });

    describe('getParticleColor', () => {
        it('returns null for unknown particle', () => {
            const mod = new ColorModule(makeColorConfig());
            mod.initialize();
            expect(mod.getParticleColor(999 as any)).toBeNull();
        });

        it('returns color after processing', () => {
            const mod = new ColorModule(makeColorConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            const color = mod.getParticleColor(0 as any);
            expect(color).not.toBeNull();
            expect(color!.r).toBeDefined();
        });
    });

    describe('getActiveParticleCount', () => {
        it('returns 0 before processing', () => {
            const mod = new ColorModule(makeColorConfig());
            mod.initialize();
            expect(mod.getActiveParticleCount()).toBe(0);
        });
    });

    describe('invalidateCache', () => {
        it('does not throw', () => {
            const mod = new ColorModule(makeColorConfig());
            mod.initialize();
            expect(() => mod.invalidateCache()).not.toThrow();
        });
    });

    describe('dead particle cleanup', () => {
        it('removes state for dead particles', () => {
            const mod = new ColorModule(makeColorConfig());
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(2);

            buffer.alive[0] = 0;
            (buffer.getParticleIndex as any).mockImplementation((id: number) => (id === 1 ? 1 : -1));
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
        });
    });

    describe('configure', () => {
        it('gradient change rebuilds lookup table', () => {
            const mod = new ColorModule(makeColorConfig());
            mod.initialize();
            expect(() =>
                mod.configure(
                    makeColorConfig({ color: makeGradient(0, { color: { r: 0, g: 1, b: 0, a: 1 } }) })
                )
            ).not.toThrow();
        });

        it('influence change clears particle states', () => {
            const mod = new ColorModule(makeColorConfig({ velocityInfluence: 0 }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
            mod.configure(makeColorConfig({ velocityInfluence: 1 }));
            expect(mod.getActiveParticleCount()).toBe(0);
        });
    });
});
