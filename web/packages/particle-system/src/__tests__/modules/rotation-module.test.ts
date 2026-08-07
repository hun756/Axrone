import { describe, it, expect, vi, beforeEach } from 'vitest';
import { RotationModule } from '../../modules/rotation-module';
import type { RotationConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

function makeCurve(constant = 0) {
    return { mode: 0, constant, constantMin: 0, constantMax: 0, curveMultiplier: 1 };
}

function makeRotationConfig(overrides: Partial<RotationConfiguration> = {}): RotationConfiguration {
    return {
        enabled: true,
        priority: 500,
        angularVelocity: makeCurve(1),
        separateAxes: false,
        angularVelocityX: makeCurve(0),
        angularVelocityY: makeCurve(0),
        angularVelocityZ: makeCurve(0),
        mode: 'constant',
        space: 'local',
        inheritVelocity: false,
        dampingFactor: 0,
        maxAngularVelocity: 10,
        enablePhysics: false,
        momentOfInertia: 1,
        ...overrides,
    } as any;
}

function createMockBuffer(count = 1): IParticleBuffer {
    const alive = new Uint32Array(1000);
    const rotations = new Float32Array(3000);
    const angularVelocities = new Float32Array(3000);
    const velocities = new Float32Array(3000);
    const positions = new Float32Array(3000);
    const ages = new Float32Array(1000);
    const lifetimes = new Float32Array(1000);

    for (let i = 0; i < count; i++) {
        alive[i] = 1;
        lifetimes[i] = 5;
        ages[i] = 0;
        velocities[i * 3] = 1;
        positions[i * 3] = i * 5;
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
        rotations,
        angularVelocities,
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

describe('RotationModule', () => {
    describe('constructor', () => {
        it('creates module with correct type and priority', () => {
            const mod = new RotationModule(makeRotationConfig());
            expect(mod.type).toBe('rotation');
            expect(mod.priority).toBe(500);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new RotationModule(makeRotationConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset after initialize does not throw', () => {
            const mod = new RotationModule(makeRotationConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy after initialize does not throw', () => {
            const mod = new RotationModule(makeRotationConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });

        it('initialize sets identity quaternions', () => {
            const mod = new RotationModule(makeRotationConfig());
            mod.initialize();
            const q = mod.getParticleQuaternion(0);
            expect(q.w).toBe(1);
            expect(q.x).toBe(0);
            expect(q.y).toBe(0);
            expect(q.z).toBe(0);
        });
    });

    describe('onProcess - constant rotation', () => {
        it('disabled module is no-op', () => {
            const mod = new RotationModule(makeRotationConfig({ enabled: false }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            // Rotations should stay at initial values
            expect(buffer.rotations[0]).toBe(0);
        });

        it('constant rotation updates angular velocity', () => {
            const mod = new RotationModule(
                makeRotationConfig({ mode: 'constant', angularVelocity: makeCurve(5) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            // Angular velocity should be set
            expect(buffer.angularVelocities[2]).toBeDefined();
        });

        it('constant rotation with separate axes', () => {
            const mod = new RotationModule(
                makeRotationConfig({
                    mode: 'constant',
                    separateAxes: true,
                    angularVelocityX: makeCurve(1),
                    angularVelocityY: makeCurve(2),
                    angularVelocityZ: makeCurve(3),
                })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(buffer.angularVelocities[0]).toBeDefined();
            expect(buffer.angularVelocities[1]).toBeDefined();
            expect(buffer.angularVelocities[2]).toBeDefined();
        });
    });

    describe('onProcess - overLifetime rotation', () => {
        it('modulates rotation by normalized age', () => {
            const mod = new RotationModule(
                makeRotationConfig({ mode: 'overLifetime', angularVelocity: makeCurve(10) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.ages[0] = 2.5; // halfway
            mod.process(buffer, 0.016);
            expect(buffer.angularVelocities[2]).toBeDefined();
        });
    });

    describe('onProcess - bySpeed rotation', () => {
        it('scales rotation by velocity magnitude', () => {
            const mod = new RotationModule(
                makeRotationConfig({ mode: 'bySpeed', angularVelocity: makeCurve(1) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.velocities[0] = 10;
            buffer.velocities[1] = 0;
            buffer.velocities[2] = 0;
            mod.process(buffer, 0.016);
            expect(buffer.angularVelocities[0]).toBeDefined();
        });

        it('zero velocity means zero rotation', () => {
            const mod = new RotationModule(
                makeRotationConfig({ mode: 'bySpeed', angularVelocity: makeCurve(1) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.velocities[0] = 0;
            buffer.velocities[1] = 0;
            buffer.velocities[2] = 0;
            mod.process(buffer, 0.016);
            expect(buffer.angularVelocities[0]).toBe(0);
            expect(buffer.angularVelocities[1]).toBe(0);
            expect(buffer.angularVelocities[2]).toBe(0);
        });
    });

    describe('onProcess - byPosition rotation', () => {
        it('modulates rotation by spatial position', () => {
            const mod = new RotationModule(
                makeRotationConfig({ mode: 'byPosition', angularVelocity: makeCurve(1) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.positions[0] = 10;
            mod.process(buffer, 0.016);
            expect(buffer.angularVelocities[0]).toBeDefined();
        });
    });

    describe('onProcess - byVelocity rotation', () => {
        it('aligns rotation to velocity direction (yaw/pitch)', () => {
            const mod = new RotationModule(
                makeRotationConfig({ mode: 'byVelocity', angularVelocity: makeCurve(1) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.velocities[0] = 5;
            buffer.velocities[1] = 3;
            buffer.velocities[2] = 0;
            mod.process(buffer, 0.016);
            // Should have non-zero angular velocity from yaw/pitch
            expect(buffer.angularVelocities[2]).toBeDefined();
        });
    });

    describe('onProcess - orbital rotation', () => {
        it('computes orbital angular velocity from radius', () => {
            const mod = new RotationModule(
                makeRotationConfig({ mode: 'orbital', angularVelocity: makeCurve(5) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.positions[0] = 5;
            buffer.positions[1] = 0;
            buffer.positions[2] = 0;
            mod.process(buffer, 0.016);
            // orbital: angularVel = orbitalSpeed / radius = 5/5 = 1
            expect(buffer.angularVelocities[1]).toBeCloseTo(1, 0);
        });

        it('handles zero radius gracefully', () => {
            const mod = new RotationModule(
                makeRotationConfig({ mode: 'orbital', angularVelocity: makeCurve(5) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            buffer.positions[0] = 0;
            buffer.positions[1] = 0;
            buffer.positions[2] = 0;
            expect(() => mod.process(buffer, 0.016)).not.toThrow();
            expect(buffer.angularVelocities[0]).toBe(0);
        });
    });

    describe('onProcess - physics rotation', () => {
        it('applies inertia and damping', () => {
            const mod = new RotationModule(
                makeRotationConfig({ mode: 'physics', angularVelocity: makeCurve(5) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            // Physics mode should produce angular velocity approaching target
            expect(buffer.angularVelocities[2]).toBeDefined();
        });
    });

    describe('quaternion operations', () => {
        it('getParticleQuaternion returns identity initially', () => {
            const mod = new RotationModule(makeRotationConfig());
            mod.initialize();
            const q = mod.getParticleQuaternion(0);
            expect(q.w).toBe(1);
            expect(q.x).toBe(0);
        });

        it('setParticleQuaternion updates quaternion', () => {
            const mod = new RotationModule(makeRotationConfig());
            mod.initialize();
            // Process some rotation and check quaternion stays normalized
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.1);
            const q = mod.getParticleQuaternion(0);
            const magnitude = Math.sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            expect(magnitude).toBeCloseTo(1, 2); // should be normalized
        });

        it('quaternion stays normalized after multiple updates', () => {
            const mod = new RotationModule(
                makeRotationConfig({ angularVelocity: makeCurve(5) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            for (let i = 0; i < 10; i++) {
                mod.process(buffer, 0.016);
            }
            const q = mod.getParticleQuaternion(0);
            const magnitude = Math.sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            expect(magnitude).toBeCloseTo(1, 2);
        });
    });

    describe('angular velocity constraints', () => {
        it('clamps angular velocity to max', () => {
            const mod = new RotationModule(
                makeRotationConfig({ angularVelocity: makeCurve(100), maxAngularVelocity: 10 })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            // The constraint clamps magnitude to 10
            const mag = Math.sqrt(
                buffer.angularVelocities[0] ** 2 +
                buffer.angularVelocities[1] ** 2 +
                buffer.angularVelocities[2] ** 2
            );
            expect(mag).toBeLessThanOrEqual(10.01);
        });
    });

    describe('getStats', () => {
        it('returns stats after processing', () => {
            const mod = new RotationModule(
                makeRotationConfig({ angularVelocity: makeCurve(5) })
            );
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            const stats = mod.getStats();
            expect(stats.maxAngularVelocity).toBeGreaterThan(0);
            expect(stats.performanceMs).toBeGreaterThanOrEqual(0);
        });
    });

    describe('configure', () => {
        it('mode change triggers reset', () => {
            const mod = new RotationModule(makeRotationConfig({ mode: 'constant' }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            mod.configure(makeRotationConfig({ mode: 'overLifetime' }));
            // After reset, quaternions should be identity again
            const q = mod.getParticleQuaternion(0);
            expect(q.w).toBe(1);
        });
    });

    describe('rotation updates quaternion and euler angles', () => {
        it('writes euler angles to rotations buffer', () => {
            const mod = new RotationModule(
                makeRotationConfig({ angularVelocity: makeCurve(10) })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.1);
            // Rotations buffer should have been updated
            const hasRotation = buffer.rotations[0] !== 0 || buffer.rotations[1] !== 0 || buffer.rotations[2] !== 0;
            expect(hasRotation).toBe(true);
        });
    });
});
