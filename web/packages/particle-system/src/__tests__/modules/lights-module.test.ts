import { describe, it, expect, vi, beforeEach } from 'vitest';
import { LightsModule } from '../../modules/lights-module';
import type { LightsConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

function makeCurve(constant = 0) {
    return { mode: 0, constant, constantMin: 0, constantMax: 0 };
}

function makeLightsConfig(
    overrides: Partial<LightsConfiguration> = {}
): LightsConfiguration {
    return {
        enabled: true,
        priority: 900,
        maxLights: 32,
        range: makeCurve(10),
        intensity: makeCurve(1),
        useParticleColors: false,
        shadowCasting: false,
        defaultLights: false,
        animateLights: false,
        affectParticleColor: false,
        maxInfluencesPerParticle: 4,
        lightInfluenceMultiplier: 1,
        lightBlendFactor: 0.5,
        attenuationMode: 'inverseSquare',
        ...overrides,
    } as any;
}

function createMockBuffer(count = 1): IParticleBuffer {
    const alive = new Uint32Array(100);
    const positions = new Float32Array(300);
    const colors = new Float32Array(400);
    for (let i = 0; i < count; i++) {
        alive[i] = 1;
        positions[i * 3] = i * 2;
        positions[i * 3 + 1] = i;
        positions[i * 3 + 2] = 0;
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
        sizes: new Float32Array(300),
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

describe('LightsModule', () => {
    describe('constructor', () => {
        it('creates module with type lights and priority 900', () => {
            const mod = new LightsModule(makeLightsConfig());
            expect(mod.type).toBe('lights');
            expect(mod.priority).toBe(900);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new LightsModule(makeLightsConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset after initialize does not throw', () => {
            const mod = new LightsModule(makeLightsConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy after initialize does not throw', () => {
            const mod = new LightsModule(makeLightsConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });
    });

    describe('addLight / removeLight', () => {
        it('addLight returns a light id', () => {
            const mod = new LightsModule(makeLightsConfig());
            mod.initialize();
            const id = mod.addLight({
                position: { x: 0, y: 5, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 2,
                range: 20,
                type: 'point',
            });
            expect(id).toBeTruthy();
            expect(typeof id).toBe('string');
        });

        it('removeLight returns true for existing light', () => {
            const mod = new LightsModule(makeLightsConfig());
            mod.initialize();
            const id = mod.addLight({
                position: { x: 0, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 10,
                type: 'point',
            });
            expect(mod.removeLight(id)).toBe(true);
        });

        it('removeLight returns false for non-existent', () => {
            const mod = new LightsModule(makeLightsConfig());
            mod.initialize();
            expect(mod.removeLight('nope')).toBe(false);
        });
    });

    describe('updateLight', () => {
        it('updates light properties', () => {
            const mod = new LightsModule(makeLightsConfig());
            mod.initialize();
            const id = mod.addLight({
                position: { x: 0, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 10,
                type: 'point',
            });
            expect(mod.updateLight(id, { intensity: 5 })).toBe(true);
        });

        it('returns false for non-existent light', () => {
            const mod = new LightsModule(makeLightsConfig());
            mod.initialize();
            expect(mod.updateLight('nope', { intensity: 5 })).toBe(false);
        });
    });

    describe('getActiveLights / getActiveLightCount', () => {
        it('returns correct count', () => {
            const mod = new LightsModule(makeLightsConfig());
            mod.initialize();
            expect(mod.getActiveLightCount()).toBe(0);
            mod.addLight({
                position: { x: 0, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 10,
                type: 'point',
            });
            expect(mod.getActiveLightCount()).toBe(1);
        });

        it('getActiveLights returns map', () => {
            const mod = new LightsModule(makeLightsConfig());
            mod.initialize();
            mod.addLight({
                position: { x: 0, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 10,
                type: 'point',
            });
            expect(mod.getActiveLights().size).toBe(1);
        });
    });

    describe('max lights limit', () => {
        it('returns empty string when max lights reached', () => {
            const mod = new LightsModule(makeLightsConfig()); // default _maxLights=32
            mod.initialize();
            // Reconfigure to set _maxLights=2 (triggers because old=32 != new=2)
            mod.configure(makeLightsConfig({ maxLights: 2 }) as any);
            const id1 = mod.addLight({
                position: { x: 0, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 10,
                type: 'point',
            });
            const id2 = mod.addLight({
                position: { x: 1, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 10,
                type: 'point',
            });
            expect(id1).toBeTruthy();
            expect(id2).toBeTruthy();
            const id3 = mod.addLight({
                position: { x: 2, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 10,
                type: 'point',
            });
            expect(id3).toBe('');
        });
    });

    describe('onProcess - point light', () => {
        it('calculates influence for nearby particles', () => {
            const mod = new LightsModule(
                makeLightsConfig({ affectParticleColor: false })
            );
            mod.initialize();
            mod.addLight({
                position: { x: 0, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 2,
                range: 20,
                type: 'point',
            });

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 1;
            buffer.positions[1] = 0;
            buffer.positions[2] = 0;

            mod.process(buffer, 0.016);

            const influences = mod.getLightInfluences(0);
            expect(influences.length).toBeGreaterThanOrEqual(1);
            expect(influences[0].influence).toBeGreaterThan(0);
        });

        it('no influence for far particles', () => {
            const mod = new LightsModule(
                makeLightsConfig({ affectParticleColor: false })
            );
            mod.initialize();
            mod.addLight({
                position: { x: 0, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 5,
                type: 'point',
            });

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 100; // far away

            mod.process(buffer, 0.016);
            const influences = mod.getLightInfluences(0);
            expect(influences).toHaveLength(0);
        });
    });

    describe('onProcess - directional light', () => {
        it('affects all particles within range', () => {
            const mod = new LightsModule(
                makeLightsConfig({ affectParticleColor: false })
            );
            mod.initialize();
            mod.addLight({
                position: { x: 0, y: 10, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 100,
                type: 'directional',
                direction: { x: 0, y: -1, z: 0 },
            });

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 5;

            mod.process(buffer, 0.016);
            const influences = mod.getLightInfluences(0);
            expect(influences.length).toBeGreaterThanOrEqual(1);
        });
    });

    describe('onProcess - spot light', () => {
        it('calculates cone influence', () => {
            const mod = new LightsModule(
                makeLightsConfig({ affectParticleColor: false })
            );
            mod.initialize();
            mod.addLight({
                position: { x: 0, y: 5, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 2,
                range: 20,
                type: 'spot',
                direction: { x: 0, y: -1, z: 0 },
                innerCone: 0.3,
                outerCone: 0.6,
            });

            const buffer = createMockBuffer(1);
            // Particle directly below spot light (within inner cone)
            buffer.positions[0] = 0;
            buffer.positions[1] = 0;
            buffer.positions[2] = 0;

            mod.process(buffer, 0.016);
            const influences = mod.getLightInfluences(0);
            expect(influences.length).toBeGreaterThanOrEqual(1);
        });
    });

    describe('default lights', () => {
        it('adds default lights on initialize', () => {
            const mod = new LightsModule(makeLightsConfig({ defaultLights: true }));
            mod.initialize();
            expect(mod.getActiveLightCount()).toBeGreaterThanOrEqual(1);
        });

        it('no default lights when disabled', () => {
            const mod = new LightsModule(makeLightsConfig({ defaultLights: false }));
            mod.initialize();
            expect(mod.getActiveLightCount()).toBe(0);
        });
    });

    describe('dead particle cleanup', () => {
        it('removes influences for dead particles', () => {
            const mod = new LightsModule(
                makeLightsConfig({ affectParticleColor: false })
            );
            mod.initialize();
            mod.addLight({
                position: { x: 0, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 2,
                range: 20,
                type: 'point',
            });

            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);

            // Kill the particle
            buffer.alive[0] = 0;
            mod.process(buffer, 0.016);

            const influences = mod.getLightInfluences(0);
            expect(influences).toHaveLength(0);
        });
    });

    describe('disabled module', () => {
        it('does nothing when disabled', () => {
            const mod = new LightsModule(makeLightsConfig({ enabled: false }));
            mod.initialize();
            mod.addLight({
                position: { x: 0, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 2,
                range: 20,
                type: 'point',
            });

            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.getLightInfluences(0)).toHaveLength(0);
        });
    });

    describe('configure', () => {
        it('maxLights change updates limit', () => {
            const mod = new LightsModule(makeLightsConfig({ maxLights: 4 }));
            mod.initialize();
            mod.configure(makeLightsConfig({ maxLights: 2 }) as any);
            // Adding lights beyond new limit should fail
            mod.addLight({
                position: { x: 0, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 10,
                type: 'point',
            });
            mod.addLight({
                position: { x: 1, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 10,
                type: 'point',
            });
            const id3 = mod.addLight({
                position: { x: 2, y: 0, z: 0 },
                color: { r: 1, g: 1, b: 1 },
                intensity: 1,
                range: 10,
                type: 'point',
            });
            expect(id3).toBe('');
        });
    });

    describe('influence sorting', () => {
        it('limits influences to maxInfluencesPerParticle', () => {
            const mod = new LightsModule(
                makeLightsConfig({
                    affectParticleColor: false,
                    maxInfluencesPerParticle: 2,
                })
            );
            mod.initialize();

            // Add 3 lights close to particle
            for (let i = 0; i < 3; i++) {
                mod.addLight({
                    position: { x: i, y: 0, z: 0 },
                    color: { r: 1, g: 1, b: 1 },
                    intensity: 1 + i * 0.5,
                    range: 50,
                    type: 'point',
                });
            }

            const buffer = createMockBuffer(1);
            buffer.positions[0] = 0;
            mod.process(buffer, 0.016);

            const influences = mod.getLightInfluences(0);
            expect(influences.length).toBeLessThanOrEqual(2);
        });
    });
});
