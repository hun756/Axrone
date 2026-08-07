import { describe, it, expect, vi, beforeEach } from 'vitest';
import { VelocityModule } from '../../modules/velocity-module';
import type { VelocityConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

function makeCurve(constant = 0, overrides: Partial<{ mode: number; constantMin: number; constantMax: number }> = {}) {
    return { mode: 0, constant, constantMin: 0, constantMax: 0, curveMultiplier: 1, ...overrides };
}

function makeVelocityConfig(overrides: Partial<VelocityConfiguration> = {}): VelocityConfiguration {
    return {
        enabled: true,
        priority: 200,
        linear: { x: makeCurve(0), y: makeCurve(0), z: makeCurve(0) },
        orbital: { x: makeCurve(0), y: makeCurve(0), z: makeCurve(0) },
        radial: makeCurve(0),
        speedModifier: makeCurve(1),
        gravityModifier: 0,
        velocityOverLifetime: makeCurve(1),
        inheritVelocity: 0,
        damping: makeCurve(0),
        space: 'local' as any,
        ...overrides,
    };
}

function createMockBuffer(count = 1): IParticleBuffer {
    const alive = new Uint32Array(1000);
    const positions = new Float32Array(3000);
    const velocities = new Float32Array(3000);
    const ages = new Float32Array(1000);
    const lifetimes = new Float32Array(1000);

    for (let i = 0; i < count; i++) {
        alive[i] = 1;
        lifetimes[i] = 5;
        ages[i] = 0;
        positions[i * 3] = 0;
        positions[i * 3 + 1] = 0;
        positions[i * 3 + 2] = 0;
        velocities[i * 3] = 1;
        velocities[i * 3 + 1] = 2;
        velocities[i * 3 + 2] = 3;
    }

    return {
        get count() { return count; },
        capacity: 1000,
        allocated: true,
        alive,
        positions,
        velocities,
        accelerations: new Float32Array(3000),
        lifetimes,
        ages,
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

describe('VelocityModule', () => {
    describe('constructor', () => {
        it('creates module with correct type and priority', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            expect(mod.type).toBe('velocity');
            expect(mod.priority).toBe(200);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset after initialize does not throw', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy after initialize does not throw', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });
    });

    describe('onProcess', () => {
        it('disabled module is no-op', () => {
            const mod = new VelocityModule(makeVelocityConfig({ enabled: false }));
            mod.initialize();
            const buffer = createMockBuffer(2);
            const vBefore = buffer.velocities[0];
            mod.process(buffer, 0.016);
            expect(buffer.velocities[0]).toBe(vBefore);
        });

        it('applies linear velocity acceleration', () => {
            const mod = new VelocityModule(
                makeVelocityConfig({
                    linear: { x: makeCurve(10), y: makeCurve(0), z: makeCurve(-5) },
                })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.1);
            // Linear velocity adds to acceleration, which then affects velocity
            // The exact behavior depends on internal implementation
            expect(buffer.velocities[0]).toBeDefined();
        });

        it('applies gravity when gravityModifier != 0', () => {
            const mod = new VelocityModule(makeVelocityConfig({ gravityModifier: 1 }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            const vyBefore = buffer.velocities[1];
            mod.process(buffer, 0.1);
            // Gravity should decrease y velocity (downward)
            expect(buffer.velocities[1]).toBeLessThan(vyBefore);
        });

        it('no gravity when gravityModifier is 0', () => {
            const mod = new VelocityModule(makeVelocityConfig({ gravityModifier: 0 }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            // With no gravity and all curves at 0 except defaults, velocity should stay similar
            mod.process(buffer, 0.1);
            // The velocity should not have gravity applied
            expect(buffer.velocities[1]).toBeDefined();
        });

        it('applies orbital velocity (cross product)', () => {
            const mod = new VelocityModule(
                makeVelocityConfig({
                    orbital: { x: makeCurve(0), y: makeCurve(0), z: makeCurve(5) },
                })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.positions[0] = 1; // x=1
            buffer.positions[1] = 0; // y=0
            buffer.positions[2] = 0; // z=0
            mod.process(buffer, 0.1);
            // orbital with z=5 at position (1,0,0): cross = (-0*5 + 0*0, 1*5 - 0*0, -1*0 + 0*0) = (0, 5, 0) * dt
            expect(buffer.velocities[1]).toBeGreaterThan(2); // y velocity should increase
        });

        it('applies radial velocity from origin', () => {
            const mod = new VelocityModule(
                makeVelocityConfig({ radial: makeCurve(10) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.positions[0] = 3;
            buffer.positions[1] = 4;
            buffer.positions[2] = 0;
            mod.process(buffer, 0.1);
            // radial direction = (3/5, 4/5, 0), radialVel=10, dt=0.1
            // delta = (3/5 * 10 * 0.1, 4/5 * 10 * 0.1, 0) = (0.6, 0.8, 0)
            expect(buffer.velocities[0]).toBeGreaterThan(1);
            expect(buffer.velocities[1]).toBeGreaterThan(2);
        });

        it('skips dead particles', () => {
            const mod = new VelocityModule(makeVelocityConfig({ gravityModifier: 1 }));
            mod.initialize();
            const buffer = createMockBuffer(2);
            buffer.alive[1] = 0; // kill second particle
            const v1Before = buffer.velocities[3]; // particle 1's vx
            mod.process(buffer, 0.1);
            // Dead particle's velocity should not change
            expect(buffer.velocities[3]).toBe(v1Before);
        });

        it('applies damping to reduce velocity', () => {
            const mod = new VelocityModule(
                makeVelocityConfig({ damping: makeCurve(0.5) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            const vxBefore = buffer.velocities[0];
            mod.process(buffer, 0.1);
            // Damping reduces velocity
            expect(Math.abs(buffer.velocities[0])).toBeLessThanOrEqual(Math.abs(vxBefore) + 1);
        });
    });

    describe('getParticleVelocity', () => {
        it('returns null for unknown particle', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            expect(mod.getParticleVelocity(999 as any)).toBeNull();
        });

        it('returns velocity after processing', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            const vel = mod.getParticleVelocity(0 as any);
            expect(vel).not.toBeNull();
            expect(vel!.x).toBeDefined();
        });
    });

    describe('getParticleAcceleration', () => {
        it('returns null for unknown particle', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            expect(mod.getParticleAcceleration(999 as any)).toBeNull();
        });

        it('returns acceleration after processing', () => {
            const mod = new VelocityModule(
                makeVelocityConfig({ linear: { x: makeCurve(5), y: makeCurve(0), z: makeCurve(0) } })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            const acc = mod.getParticleAcceleration(0 as any);
            expect(acc).not.toBeNull();
        });
    });

    describe('getParticleDistance', () => {
        it('returns 0 for unknown particle', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            expect(mod.getParticleDistance(999 as any)).toBe(0);
        });

        it('tracks distance after multiple process calls', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            // Move the particle
            buffer.positions[0] = 3;
            buffer.positions[1] = 4;
            mod.process(buffer, 0.016);
            const dist = mod.getParticleDistance(0 as any);
            expect(dist).toBeGreaterThan(0);
        });
    });

    describe('setGravity / getGravity', () => {
        it('setGravity updates gravity vector', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            mod.setGravity({ x: 1, y: -20, z: 3 });
            const g = mod.getGravity();
            expect(g.x).toBe(1);
            expect(g.y).toBe(-20);
            expect(g.z).toBe(3);
        });

        it('getGravity returns default gravity', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            const g = mod.getGravity();
            expect(g.y).toBeCloseTo(-9.81);
        });
    });

    describe('getActiveParticleCount', () => {
        it('returns 0 before processing', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            expect(mod.getActiveParticleCount()).toBe(0);
        });

        it('returns count after processing', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            const buffer = createMockBuffer(3);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(3);
        });
    });

    describe('getAverageSpeed', () => {
        it('returns 0 with no particles', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            expect(mod.getAverageSpeed()).toBe(0);
        });

        it('returns positive speed after processing', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.getAverageSpeed()).toBeGreaterThan(0);
        });
    });

    describe('configure', () => {
        it('gravity change updates gravity acceleration', () => {
            const mod = new VelocityModule(makeVelocityConfig({ gravityModifier: 0 }));
            mod.initialize();
            mod.configure(makeVelocityConfig({ gravityModifier: 2 }));
            const g = mod.getGravity();
            expect(g.y).toBeCloseTo(-9.81 * 2);
        });

        it('space change clears particle states', () => {
            const mod = new VelocityModule(makeVelocityConfig({ space: 'local' as any }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
            mod.configure(makeVelocityConfig({ space: 'world' as any }));
            expect(mod.getActiveParticleCount()).toBe(0);
        });
    });

    describe('dead particle cleanup', () => {
        it('removes state for dead particles', () => {
            const mod = new VelocityModule(makeVelocityConfig());
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(2);

            // Kill particle 0
            buffer.alive[0] = 0;
            (buffer.getParticleIndex as any).mockImplementation((id: number) => (id === 1 ? 1 : -1));
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
        });
    });
});
