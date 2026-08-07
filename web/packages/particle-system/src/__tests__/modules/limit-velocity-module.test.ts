import { describe, it, expect, vi, beforeEach } from 'vitest';
import { LimitVelocityModule } from '../../modules/limit-velocity-module';
import type { LimitVelocityConfiguration, CurveConfiguration } from '../../core/configuration';
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

function makeLimitVelocityConfig(
    overrides: Partial<LimitVelocityConfiguration> = {}
): LimitVelocityConfiguration {
    return {
        enabled: true,
        priority: 300,
        separateAxes: false,
        speed: makeCurveConfig({ constant: 10 }),
        speedX: makeCurveConfig({ constant: 10 }),
        speedY: makeCurveConfig({ constant: 10 }),
        speedZ: makeCurveConfig({ constant: 10 }),
        dampen: 0,
        drag: makeCurveConfig({ constant: 0 }),
        multiplyDragByParticleSize: false,
        multiplyDragByParticleVelocity: false,
        ...overrides,
    };
}

function createMockBuffer(opts: {
    count?: number;
    velocities?: Float32Array;
    ages?: Float32Array;
    lifetimes?: Float32Array;
    sizes?: Float32Array;
    alive?: Uint32Array;
} = {}): IParticleBuffer {
    const count = opts.count ?? 2;
    const velocities = opts.velocities ?? new Float32Array(count * 3);
    const ages = opts.ages ?? new Float32Array(count);
    const lifetimes = opts.lifetimes ?? new Float32Array(count).fill(10);
    const sizes = opts.sizes ?? new Float32Array(count).fill(1);
    const alive = opts.alive ?? new Uint32Array(count).fill(1);

    return {
        get count() { return count; },
        capacity: 1000,
        allocated: true,
        alive,
        positions: new Float32Array(count * 3),
        velocities,
        accelerations: new Float32Array(count * 3),
        lifetimes,
        ages,
        sizes,
        colors: new Float32Array(count * 4),
        rotations: new Float32Array(count * 3),
        angularVelocities: new Float32Array(count * 3),
        customData: [],
        ids: new Uint32Array(count),
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
        getLifetime: vi.fn().mockReturnValue(10),
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

describe('LimitVelocityModule', () => {
    describe('constructor', () => {
        it('creates module with correct type and priority', () => {
            const mod = new LimitVelocityModule(makeLimitVelocityConfig());
            expect(mod.type).toBe('limitVelocity');
            expect(mod.priority).toBe(300);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize/reset/destroy do not throw', () => {
            const mod = new LimitVelocityModule(makeLimitVelocityConfig());
            expect(() => mod.initialize()).not.toThrow();
            expect(() => mod.reset()).not.toThrow();
            expect(() => mod.destroy()).not.toThrow();
        });
    });

    describe('onProcess - disabled', () => {
        it('does nothing when enabled=false', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({ enabled: false })
            );
            mod.initialize();
            const velocities = new Float32Array([100, 100, 100]);
            const buffer = createMockBuffer({ count: 1, velocities });
            mod.process(buffer, 0.016);
            // Velocity should be unchanged since module is disabled
            expect(velocities[0]).toBe(100);
        });
    });

    describe('magnitude limiting', () => {
        it('clamps velocity above max speed', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    speed: makeCurveConfig({ constant: 5 }),
                    separateAxes: false,
                })
            );
            mod.initialize();

            const velocities = new Float32Array([10, 0, 0]); // speed = 10, limit = 5
            const buffer = createMockBuffer({ count: 1, velocities });
            mod.process(buffer, 0.016);

            const speed = Math.sqrt(
                velocities[0] ** 2 + velocities[1] ** 2 + velocities[2] ** 2
            );
            expect(speed).toBeLessThanOrEqual(5.01);
        });

        it('does not change velocity below max speed', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    speed: makeCurveConfig({ constant: 100 }),
                    separateAxes: false,
                    drag: makeCurveConfig({ constant: 0 }),
                    dampen: 0,
                })
            );
            mod.initialize();

            const velocities = new Float32Array([3, 4, 0]); // speed = 5, limit = 100
            const buffer = createMockBuffer({ count: 1, velocities });
            mod.process(buffer, 0.016);

            expect(velocities[0]).toBeCloseTo(3, 1);
            expect(velocities[1]).toBeCloseTo(4, 1);
        });

        it('handles zero velocity', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    speed: makeCurveConfig({ constant: 5 }),
                    separateAxes: false,
                })
            );
            mod.initialize();

            const velocities = new Float32Array([0, 0, 0]);
            const buffer = createMockBuffer({ count: 1, velocities });
            mod.process(buffer, 0.016);

            expect(velocities[0]).toBe(0);
            expect(velocities[1]).toBe(0);
            expect(velocities[2]).toBe(0);
        });
    });

    describe('separate axes limiting', () => {
        it('clamps each axis independently', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    separateAxes: true,
                    speedX: makeCurveConfig({ constant: 5 }),
                    speedY: makeCurveConfig({ constant: 3 }),
                    speedZ: makeCurveConfig({ constant: 8 }),
                })
            );
            mod.initialize();

            const velocities = new Float32Array([10, 10, 2]); // x>5, y>3, z<8
            const buffer = createMockBuffer({ count: 1, velocities });
            mod.process(buffer, 0.016);

            expect(Math.abs(velocities[0])).toBeLessThanOrEqual(5.01);
            expect(Math.abs(velocities[1])).toBeLessThanOrEqual(3.01);
            expect(velocities[2]).toBeCloseTo(2, 0);
        });

        it('preserves sign when clamping', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    separateAxes: true,
                    speedX: makeCurveConfig({ constant: 2 }),
                    speedY: makeCurveConfig({ constant: 2 }),
                    speedZ: makeCurveConfig({ constant: 2 }),
                })
            );
            mod.initialize();

            const velocities = new Float32Array([-10, 10, -10]);
            const buffer = createMockBuffer({ count: 1, velocities });
            mod.process(buffer, 0.016);

            expect(velocities[0]).toBeLessThanOrEqual(0);
            expect(velocities[1]).toBeGreaterThanOrEqual(0);
            expect(velocities[2]).toBeLessThanOrEqual(0);
        });
    });

    describe('drag application', () => {
        it('base drag reduces velocity', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    speed: makeCurveConfig({ constant: 1000 }),
                    drag: makeCurveConfig({ constant: 0.5 }),
                    dampen: 0,
                })
            );
            mod.initialize();

            const velocities = new Float32Array([10, 0, 0]);
            const buffer = createMockBuffer({ count: 1, velocities });
            mod.process(buffer, 0.1);

            expect(Math.abs(velocities[0])).toBeLessThan(10);
        });

        it('multiplyDragByParticleSize scales drag', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    speed: makeCurveConfig({ constant: 1000 }),
                    drag: makeCurveConfig({ constant: 0.5 }),
                    dampen: 0,
                    multiplyDragByParticleSize: true,
                })
            );
            mod.initialize();

            // Large particle size -> more drag
            const sizes = new Float32Array([5]);
            const velocities = new Float32Array([10, 0, 0]);
            const buffer = createMockBuffer({ count: 1, velocities, sizes });
            mod.process(buffer, 0.1);
            const speedLarge = Math.abs(velocities[0]);

            // Reset
            velocities[0] = 10;
            const sizes2 = new Float32Array([0.1]);
            const buffer2 = createMockBuffer({ count: 1, velocities: new Float32Array([10, 0, 0]), sizes: sizes2 });
            mod.process(buffer2, 0.1);
            const speedSmall = Math.abs(buffer2.velocities[0]);

            // Larger particle should have more drag applied (lower speed)
            expect(speedLarge).toBeLessThan(speedSmall);
        });

        it('multiplyDragByParticleVelocity scales drag', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    speed: makeCurveConfig({ constant: 1000 }),
                    drag: makeCurveConfig({ constant: 0.1 }),
                    dampen: 0,
                    multiplyDragByParticleVelocity: true,
                })
            );
            mod.initialize();

            const velocities = new Float32Array([10, 0, 0]);
            const buffer = createMockBuffer({ count: 1, velocities });
            mod.process(buffer, 0.1);

            // Drag should be proportional to velocity
            expect(Math.abs(velocities[0])).toBeLessThan(10);
        });
    });

    describe('dampen', () => {
        it('dampen reduces velocity', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    speed: makeCurveConfig({ constant: 1000 }),
                    drag: makeCurveConfig({ constant: 0 }),
                    dampen: 0.5,
                })
            );
            mod.initialize();

            const velocities = new Float32Array([10, 0, 0]);
            const buffer = createMockBuffer({ count: 1, velocities });
            mod.process(buffer, 0.1);

            expect(Math.abs(velocities[0])).toBeLessThan(10);
        });
    });

    describe('dead particles are skipped', () => {
        it('skips particles where alive[i] = 0', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    speed: makeCurveConfig({ constant: 1 }),
                })
            );
            mod.initialize();

            const alive = new Uint32Array([0, 1]);
            const velocities = new Float32Array([100, 0, 0, 100, 0, 0]);
            const buffer = createMockBuffer({ count: 2, velocities, alive });
            mod.process(buffer, 0.016);

            // Dead particle (index 0) should be unchanged
            expect(velocities[0]).toBe(100);
            // Alive particle (index 1) should be limited
            const speed1 = Math.sqrt(
                velocities[3] ** 2 + velocities[4] ** 2 + velocities[5] ** 2
            );
            expect(speed1).toBeLessThanOrEqual(1.01);
        });
    });

    describe('zero lifetime handling', () => {
        it('normalizedAge = 0 when lifetime is 0', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    speed: makeCurveConfig({ constant: 5 }),
                })
            );
            mod.initialize();

            const lifetimes = new Float32Array([0]);
            const velocities = new Float32Array([10, 0, 0]);
            const buffer = createMockBuffer({ count: 1, velocities, lifetimes });
            mod.process(buffer, 0.016);

            const speed = Math.sqrt(
                velocities[0] ** 2 + velocities[1] ** 2 + velocities[2] ** 2
            );
            expect(speed).toBeLessThanOrEqual(5.01);
        });
    });

    describe('getEffectiveSpeedLimit', () => {
        it('returns scalar for magnitude mode', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    separateAxes: false,
                    speed: makeCurveConfig({ constant: 15 }),
                })
            );
            mod.initialize();

            const buffer = createMockBuffer({ count: 1 });
            const result = mod.getEffectiveSpeedLimit(0, buffer);
            expect(result).toBe(15);
        });

        it('returns per-axis object for separate axes mode', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    separateAxes: true,
                    speedX: makeCurveConfig({ constant: 5 }),
                    speedY: makeCurveConfig({ constant: 10 }),
                    speedZ: makeCurveConfig({ constant: 15 }),
                })
            );
            mod.initialize();

            const buffer = createMockBuffer({ count: 1 });
            const result = mod.getEffectiveSpeedLimit(0, buffer) as { x: number; y: number; z: number };
            expect(result.x).toBe(5);
            expect(result.y).toBe(10);
            expect(result.z).toBe(15);
        });
    });

    describe('getEffectiveDrag', () => {
        it('returns base drag value', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    drag: makeCurveConfig({ constant: 0.5 }),
                    multiplyDragByParticleSize: false,
                })
            );
            mod.initialize();

            const buffer = createMockBuffer({ count: 1 });
            const drag = mod.getEffectiveDrag(0, buffer);
            expect(drag).toBe(0.5);
        });

        it('multiplies by particle size when configured', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    drag: makeCurveConfig({ constant: 0.5 }),
                    multiplyDragByParticleSize: true,
                })
            );
            mod.initialize();

            const sizes = new Float32Array([3]);
            const buffer = createMockBuffer({ count: 1, sizes });
            const drag = mod.getEffectiveDrag(0, buffer);
            expect(drag).toBeCloseTo(1.5);
        });
    });

    describe('curve evaluation modes', () => {
        it('mode 2 (two constants) interpolates with normalized age', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    separateAxes: false,
                    speed: makeCurveConfig({
                        mode: 2,
                        constant: 0,
                        constantMin: 5,
                        constantMax: 15,
                    }),
                })
            );
            mod.initialize();

            // At age=0, normalizedAge=0 -> speed = constantMin = 5
            const ages = new Float32Array([0]);
            const lifetimes = new Float32Array([10]);
            const buffer = createMockBuffer({ count: 1, ages, lifetimes });
            const speed = mod.getEffectiveSpeedLimit(0, buffer);
            expect(speed).toBe(5);
        });
    });

    describe('multiple particles', () => {
        it('processes all alive particles', () => {
            const mod = new LimitVelocityModule(
                makeLimitVelocityConfig({
                    speed: makeCurveConfig({ constant: 5 }),
                    separateAxes: false,
                })
            );
            mod.initialize();

            const velocities = new Float32Array([20, 0, 0, 0, 20, 0, 0, 0, 20]);
            const buffer = createMockBuffer({ count: 3, velocities });
            mod.process(buffer, 0.016);

            for (let i = 0; i < 3; i++) {
                const i3 = i * 3;
                const speed = Math.sqrt(
                    velocities[i3] ** 2 + velocities[i3 + 1] ** 2 + velocities[i3 + 2] ** 2
                );
                expect(speed).toBeLessThanOrEqual(5.01);
            }
        });
    });
});
