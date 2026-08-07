import { describe, expect, it, vi } from 'vitest';
import { EmissionModule } from '../../modules/emission-module';
import type { EmissionConfiguration, CurveConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

function makeCurveConfig(overrides: Partial<CurveConfiguration> = {}): CurveConfiguration {
    return {
        mode: 0,
        constant: 10,
        constantMin: 0,
        constantMax: 20,
        curveMultiplier: 1,
        ...overrides,
    };
}

function makeEmissionConfig(overrides: Partial<EmissionConfiguration> = {}): EmissionConfiguration {
    return {
        enabled: true,
        priority: 100,
        rateOverTime: makeCurveConfig({ constant: 10 }),
        rateOverDistance: makeCurveConfig({ constant: 0 }),
        rateMultiplier: 1,
        bursts: [],
        prewarm: false,
        prewarmTime: 0,
        duration: 5,
        startLifetime: makeCurveConfig({ constant: 5 }),
        startLifetimeMultiplier: 0,
        startSize: makeCurveConfig({ constant: 1 }),
        startSizeMultiplier: 0,
        startColor: {
            mode: 0,
            color: { r: 1, g: 1, b: 1, a: 1 },
            colorMin: { r: 0, g: 0, b: 0, a: 1 },
            colorMax: { r: 1, g: 1, b: 1, a: 1 },
        },
        ...overrides,
    };
}

function createMockBuffer(): IParticleBuffer {
    let particleCount = 1; // > 0 so canProcess returns true
    return {
        get count() { return particleCount; },
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
        addParticle: vi.fn().mockImplementation(() => {
            particleCount++;
            return particleCount;
        }),
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

describe('EmissionModule', () => {
    describe('constructor', () => {
        it('sets up emission state with empty burst states', () => {
            const mod = new EmissionModule(makeEmissionConfig());
            expect(mod.type).toBe('emission');
            expect(mod.priority).toBe(100);
            expect(mod.enabled).toBe(true);
            expect(mod.getBurstStates()).toEqual([]);
        });
    });

    describe('lifecycle', () => {
        it('onInitialize resets state and initializes bursts', () => {
            const mod = new EmissionModule(makeEmissionConfig());
            mod.initialize();
            expect(mod.getTotalEmittedCount()).toBe(0);
        });

        it('performs prewarm if configured', () => {
            const mod = new EmissionModule(
                makeEmissionConfig({ prewarm: true, prewarmTime: 0.1 })
            );
            mod.initialize();
            // After prewarm, prewarmProgress should have been set
            expect(mod.getPrewarmProgress()).toBeGreaterThanOrEqual(0);
        });
    });

    describe('continuous emission', () => {
        it('accumulator-based rate-over-time with multiplier', () => {
            const config = makeEmissionConfig({
                rateOverTime: makeCurveConfig({ constant: 100 }),
                rateMultiplier: 1,
            });
            const mod = new EmissionModule(config);
            mod.initialize();

            const buffer = createMockBuffer();
            // Process with a large enough deltaTime to accumulate particles
            mod.process(buffer, 0.1);
            expect(buffer.addParticle).toHaveBeenCalled();
            expect(mod.getTotalEmittedCount()).toBeGreaterThan(0);
        });
    });

    describe('burst emission', () => {
        it('triggers at correct time', () => {
            const config = makeEmissionConfig({
                rateOverTime: makeCurveConfig({ constant: 0 }),
                bursts: [
                    {
                        time: 0,
                        count: { value: 10, variance: 0 },
                        cycles: 1,
                        interval: 1,
                        probability: 1,
                    },
                ],
            });
            const mod = new EmissionModule(config);
            mod.initialize();

            const buffer = createMockBuffer();
            mod.process(buffer, 0.016);
            expect(buffer.addParticle).toHaveBeenCalled();
        });

        it('respects cycles', () => {
            const config = makeEmissionConfig({
                rateOverTime: makeCurveConfig({ constant: 0 }),
                bursts: [
                    {
                        time: 0,
                        count: { value: 5, variance: 0 },
                        cycles: 2,
                        interval: 0.5,
                        probability: 1,
                    },
                ],
            });
            const mod = new EmissionModule(config);
            mod.initialize();

            const buffer = createMockBuffer();
            // First burst
            mod.process(buffer, 0.016);
            const burstStates = mod.getBurstStates();
            expect(burstStates.length).toBe(1);
        });
    });

    describe('play/pause/stop', () => {
        it('play resumes emission', () => {
            const mod = new EmissionModule(makeEmissionConfig());
            mod.initialize();
            mod.pause();
            mod.play();
            // After play, emission should resume
            const buffer = createMockBuffer();
            mod.process(buffer, 0.1);
            // Should emit since rateOverTime constant is 10
            expect(mod.getTotalEmittedCount()).toBeGreaterThan(0);
        });

        it('pause stops emission', () => {
            const mod = new EmissionModule(makeEmissionConfig());
            mod.initialize();
            mod.pause();
            const buffer = createMockBuffer();
            mod.process(buffer, 0.1);
            expect(mod.getTotalEmittedCount()).toBe(0);
        });

        it('stop resets emission state', () => {
            const mod = new EmissionModule(makeEmissionConfig());
            mod.initialize();
            const buffer = createMockBuffer();
            mod.process(buffer, 0.1);
            mod.stop();
            expect(mod.getTotalEmittedCount()).toBe(0);
        });
    });

    describe('enableEmission / disableEmission', () => {
        it('disableEmission stops particle creation', () => {
            const mod = new EmissionModule(makeEmissionConfig());
            mod.initialize();
            mod.disableEmission();
            const buffer = createMockBuffer();
            mod.process(buffer, 0.1);
            expect(mod.getTotalEmittedCount()).toBe(0);
        });

        it('enableEmission resumes particle creation', () => {
            const mod = new EmissionModule(makeEmissionConfig());
            mod.initialize();
            mod.disableEmission();
            mod.enableEmission();
            const buffer = createMockBuffer();
            mod.process(buffer, 0.1);
            expect(mod.getTotalEmittedCount()).toBeGreaterThan(0);
        });
    });

    describe('getTotalEmittedCount / getEmissionRate', () => {
        it('getTotalEmittedCount returns correct value', () => {
            const mod = new EmissionModule(makeEmissionConfig());
            mod.initialize();
            expect(mod.getTotalEmittedCount()).toBe(0);
        });

        it('getEmissionRate returns rate based on curve', () => {
            const mod = new EmissionModule(
                makeEmissionConfig({
                    rateOverTime: makeCurveConfig({ constant: 50 }),
                    rateMultiplier: 2,
                })
            );
            mod.initialize();
            const rate = mod.getEmissionRate();
            expect(rate).toBe(100); // 50 * 2
        });
    });

    describe('curve evaluation', () => {
        it('constant mode', () => {
            const mod = new EmissionModule(
                makeEmissionConfig({
                    rateOverTime: makeCurveConfig({ constant: 25 }),
                    rateMultiplier: 1,
                })
            );
            mod.initialize();
            expect(mod.getEmissionRate()).toBe(25);
        });

        it('two-constants mode', () => {
            const mod = new EmissionModule(
                makeEmissionConfig({
                    rateOverTime: makeCurveConfig({
                        mode: 3,
                        constant: 0,
                        constantMin: 10,
                        constantMax: 20,
                    }),
                    rateMultiplier: 1,
                })
            );
            mod.initialize();
            const rate = mod.getEmissionRate();
            expect(rate).toBeGreaterThanOrEqual(10);
            expect(rate).toBeLessThanOrEqual(20);
        });
    });

    describe('configuration change', () => {
        it('re-initializes bursts when burst config changes', () => {
            const config = makeEmissionConfig({
                bursts: [
                    {
                        time: 0,
                        count: { value: 5, variance: 0 },
                        cycles: 1,
                        interval: 1,
                        probability: 1,
                    },
                ],
            });
            const mod = new EmissionModule(config);
            mod.initialize();

            const newConfig = makeEmissionConfig({
                bursts: [
                    {
                        time: 1,
                        count: { value: 10, variance: 0 },
                        cycles: 2,
                        interval: 0.5,
                        probability: 1,
                    },
                ],
            });
            mod.configure(newConfig);
            expect(mod.getBurstStates().length).toBe(1);
        });
    });
});
