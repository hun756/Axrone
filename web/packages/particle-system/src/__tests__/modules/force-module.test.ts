import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ForceModule, type ForceField } from '../../modules/force-module';
import type { ForceConfiguration } from '../../core/configuration';

function makeCurve(constant = 0) {
    return { mode: 0, constant, constantMin: 0, constantMax: 0, curveMultiplier: 1 };
}

function makeForceConfig(overrides: Partial<ForceConfiguration> = {}): ForceConfiguration {
    return {
        enabled: true,
        priority: 150,
        forces: [],
        ...overrides,
    };
}

function makeVec3Array(count: number) {
    return {
        x: new Float32Array(count),
        y: new Float32Array(count),
        z: new Float32Array(count),
    };
}

describe('ForceModule', () => {
    describe('constructor', () => {
        it('creates module with forces from config', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'gravity',
                            strength: makeCurve(1),
                            direction: { x: 0, y: -1, z: 0 },
                        },
                    ],
                })
            );
            expect(mod.getStats().totalForces).toBe(1);
            expect(mod.getStats().activeForces).toBe(1);
        });

        it('starts with no forces when config is empty', () => {
            const mod = new ForceModule(makeForceConfig());
            expect(mod.getStats().totalForces).toBe(0);
        });
    });

    describe('applyForces - gravity', () => {
        it('applies gravity force to velocities', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'gravity',
                            strength: makeCurve(1),
                            direction: { x: 0, y: -9.81, z: 0 },
                        },
                    ],
                })
            );
            const count = 2;
            const positions = makeVec3Array(count);
            const velocities = makeVec3Array(count);
            const ages = new Float32Array(count);
            const lifetimes = new Float32Array(count).fill(5);
            const masses = new Float32Array(count).fill(1);

            velocities.y[0] = 10;
            mod.applyForces(positions, velocities, ages, lifetimes, masses, count, 0.1);

            // Gravity should decrease y velocity (direction is -9.81)
            expect(velocities.y[0]).toBeLessThan(10);
        });

        it('gravity scales with mass', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'gravity',
                            strength: makeCurve(1),
                            direction: { x: 0, y: -1, z: 0 },
                        },
                    ],
                })
            );
            const positions = makeVec3Array(2);
            const velocities = makeVec3Array(2);
            const ages = new Float32Array(2);
            const lifetimes = new Float32Array(2).fill(5);
            const masses = new Float32Array([1, 2]);

            mod.applyForces(positions, velocities, ages, lifetimes, masses, 2, 0.1);

            // Heavier particle should have more velocity change
            const dv0 = Math.abs(velocities.y[0]);
            const dv1 = Math.abs(velocities.y[1]);
            expect(dv1).toBeGreaterThan(dv0);
        });
    });

    describe('applyForces - drag', () => {
        it('reduces velocity magnitude', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'drag',
                            strength: makeCurve(0.5),
                            direction: { x: 0, y: 0, z: 0 },
                        },
                    ],
                })
            );
            const count = 1;
            const positions = makeVec3Array(count);
            const velocities = makeVec3Array(count);
            velocities.x[0] = 10;
            velocities.y[0] = 5;
            velocities.z[0] = 3;
            const ages = new Float32Array(count);
            const lifetimes = new Float32Array(count).fill(5);
            const masses = new Float32Array(count).fill(1);

            mod.applyForces(positions, velocities, ages, lifetimes, masses, count, 0.1);

            const speed = Math.sqrt(velocities.x[0] ** 2 + velocities.y[0] ** 2 + velocities.z[0] ** 2);
            expect(speed).toBeLessThan(Math.sqrt(10 ** 2 + 5 ** 2 + 3 ** 2));
        });

        it('does not affect stationary particles', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'drag',
                            strength: makeCurve(0.5),
                            direction: { x: 0, y: 0, z: 0 },
                        },
                    ],
                })
            );
            const count = 1;
            const positions = makeVec3Array(count);
            const velocities = makeVec3Array(count); // all zero
            const ages = new Float32Array(count);
            const lifetimes = new Float32Array(count).fill(5);
            const masses = new Float32Array(count).fill(1);

            mod.applyForces(positions, velocities, ages, lifetimes, masses, count, 0.1);

            expect(velocities.x[0]).toBe(0);
            expect(velocities.y[0]).toBe(0);
            expect(velocities.z[0]).toBe(0);
        });
    });

    describe('applyForces - turbulence', () => {
        it('adds noise-based velocity', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'turbulence',
                            strength: makeCurve(1),
                            direction: { x: 0, y: 0, z: 0 },
                        } as any,
                    ],
                })
            );
            const count = 1;
            const positions = makeVec3Array(count);
            positions.x[0] = 5;
            positions.y[0] = 5;
            positions.z[0] = 5;
            const velocities = makeVec3Array(count);
            const ages = new Float32Array(count);
            const lifetimes = new Float32Array(count).fill(5);
            const masses = new Float32Array(count).fill(1);

            mod.applyForces(positions, velocities, ages, lifetimes, masses, count, 0.1);

            // Turbulence should add some velocity
            const speed = Math.sqrt(velocities.x[0] ** 2 + velocities.y[0] ** 2 + velocities.z[0] ** 2);
            expect(speed).toBeGreaterThan(0);
        });
    });

    describe('applyForces - vortex', () => {
        it('applies tangential force around axis', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'vortex',
                            strength: makeCurve(5),
                            direction: { x: 0, y: 1, z: 0 },
                            position: { x: 0, y: 0, z: 0 },
                            falloffRadius: 100,
                        } as any,
                    ],
                })
            );
            const count = 1;
            const positions = makeVec3Array(count);
            positions.x[0] = 5;
            const velocities = makeVec3Array(count);
            const ages = new Float32Array(count);
            const lifetimes = new Float32Array(count).fill(5);
            const masses = new Float32Array(count).fill(1);

            mod.applyForces(positions, velocities, ages, lifetimes, masses, count, 0.1);

            // Vortex should create tangential velocity
            const speed = Math.sqrt(velocities.x[0] ** 2 + velocities.y[0] ** 2 + velocities.z[0] ** 2);
            expect(speed).toBeGreaterThan(0);
        });

        it('skips when no position is set', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'vortex',
                            strength: makeCurve(5),
                            direction: { x: 0, y: 1, z: 0 },
                        } as any,
                    ],
                })
            );
            const count = 1;
            const positions = makeVec3Array(count);
            positions.x[0] = 5;
            const velocities = makeVec3Array(count);
            const ages = new Float32Array(count);
            const lifetimes = new Float32Array(count).fill(5);
            const masses = new Float32Array(count).fill(1);

            mod.applyForces(positions, velocities, ages, lifetimes, masses, count, 0.1);

            // No position on force -> no effect
            expect(velocities.x[0]).toBe(0);
        });
    });

    describe('applyForces - point force', () => {
        it('attracts/repels from point', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'point',
                            strength: makeCurve(10),
                            direction: { x: 1, y: 0, z: 0 },
                            position: { x: 0, y: 0, z: 0 },
                            falloffRadius: 100,
                        } as any,
                    ],
                })
            );
            const count = 1;
            const positions = makeVec3Array(count);
            positions.x[0] = 5;
            const velocities = makeVec3Array(count);
            const ages = new Float32Array(count);
            const lifetimes = new Float32Array(count).fill(5);
            const masses = new Float32Array(count).fill(1);

            mod.applyForces(positions, velocities, ages, lifetimes, masses, count, 0.1);

            const speed = Math.sqrt(velocities.x[0] ** 2 + velocities.y[0] ** 2 + velocities.z[0] ** 2);
            expect(speed).toBeGreaterThan(0);
        });
    });

    describe('applyForces - custom force', () => {
        it('calls custom function when added via addForce', () => {
            const customFn = vi.fn().mockReturnValue({ x: 1, y: 2, z: 3 });
            const mod = new ForceModule(makeForceConfig());
            mod.addForce({
                type: 'custom',
                strength: 1,
                direction: { x: 0, y: 0, z: 0 },
                falloff: 'none',
                customFunction: customFn,
            });
            const count = 1;
            const positions = makeVec3Array(count);
            const velocities = makeVec3Array(count);
            const ages = new Float32Array(count);
            const lifetimes = new Float32Array(count).fill(5);
            const masses = new Float32Array(count).fill(1);

            mod.applyForces(positions, velocities, ages, lifetimes, masses, count, 0.1);

            expect(customFn).toHaveBeenCalled();
            expect(velocities.x[0]).not.toBe(0);
        });
    });

    describe('addForce / removeForce', () => {
        it('addForce increases force count', () => {
            const mod = new ForceModule(makeForceConfig());
            expect(mod.getStats().totalForces).toBe(0);
            const idx = mod.addForce({
                type: 'gravity',
                strength: 1,
                direction: { x: 0, y: -1, z: 0 },
                falloff: 'none',
            });
            expect(idx).toBe(0);
            expect(mod.getStats().totalForces).toBe(1);
            expect(mod.getStats().activeForces).toBe(1);
        });

        it('removeForce decreases active count', () => {
            const mod = new ForceModule(makeForceConfig());
            const idx = mod.addForce({
                type: 'gravity',
                strength: 1,
                direction: { x: 0, y: -1, z: 0 },
                falloff: 'none',
            });
            expect(mod.removeForce(idx)).toBe(true);
            expect(mod.getStats().activeForces).toBe(0);
        });

        it('removeForce returns false for invalid index', () => {
            const mod = new ForceModule(makeForceConfig());
            expect(mod.removeForce(99)).toBe(false);
        });
    });

    describe('setForceEnabled', () => {
        it('disables and re-enables a force', () => {
            const mod = new ForceModule(makeForceConfig());
            const idx = mod.addForce({
                type: 'gravity',
                strength: 1,
                direction: { x: 0, y: -1, z: 0 },
                falloff: 'none',
            });
            mod.setForceEnabled(idx, false);
            expect(mod.getStats().activeForces).toBe(0);
            mod.setForceEnabled(idx, true);
            expect(mod.getStats().activeForces).toBe(1);
        });
    });

    describe('updateForceStrength', () => {
        it('updates force strength', () => {
            const mod = new ForceModule(makeForceConfig());
            const idx = mod.addForce({
                type: 'gravity',
                strength: 1,
                direction: { x: 0, y: -1, z: 0 },
                falloff: 'none',
            });
            mod.updateForceStrength(idx, 5);
            const forces = mod.getActiveForces();
            expect(forces[0].strength).toBe(5);
        });
    });

    describe('getStats / resetStats', () => {
        it('getStats returns copy of stats', () => {
            const mod = new ForceModule(makeForceConfig());
            const stats = mod.getStats();
            expect(stats.totalForces).toBe(0);
            expect(stats.activeForces).toBe(0);
        });

        it('resetStats zeros computation stats', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'gravity',
                            strength: makeCurve(1),
                            direction: { x: 0, y: -1, z: 0 },
                        },
                    ],
                })
            );
            const count = 1;
            const positions = makeVec3Array(count);
            const velocities = makeVec3Array(count);
            const ages = new Float32Array(count);
            const lifetimes = new Float32Array(count).fill(5);
            const masses = new Float32Array(count).fill(1);
            mod.applyForces(positions, velocities, ages, lifetimes, masses, count, 0.1);

            mod.resetStats();
            const stats = mod.getStats();
            expect(stats.computationsPerFrame).toBe(0);
        });
    });

    describe('getActiveForces / clearForces', () => {
        it('getActiveForces returns active force list', () => {
            const mod = new ForceModule(makeForceConfig());
            mod.addForce({ type: 'gravity', strength: 1, direction: { x: 0, y: -1, z: 0 }, falloff: 'none' });
            mod.addForce({ type: 'drag', strength: 0.5, direction: { x: 0, y: 0, z: 0 }, falloff: 'linear' });
            expect(mod.getActiveForces().length).toBe(2);
        });

        it('clearForces removes all forces', () => {
            const mod = new ForceModule(makeForceConfig());
            mod.addForce({ type: 'gravity', strength: 1, direction: { x: 0, y: -1, z: 0 }, falloff: 'none' });
            mod.clearForces();
            expect(mod.getStats().totalForces).toBe(0);
            expect(mod.getStats().activeForces).toBe(0);
            expect(mod.getActiveForces().length).toBe(0);
        });
    });

    describe('edge cases', () => {
        it('applyForces with 0 particles is no-op', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [{ type: 'gravity', strength: makeCurve(1), direction: { x: 0, y: -1, z: 0 } }],
                })
            );
            const positions = makeVec3Array(0);
            const velocities = makeVec3Array(0);
            expect(() =>
                mod.applyForces(positions, velocities, new Float32Array(0), new Float32Array(0), new Float32Array(0), 0, 0.1)
            ).not.toThrow();
        });

        it('applyForces with no active forces is no-op', () => {
            const mod = new ForceModule(makeForceConfig());
            const positions = makeVec3Array(1);
            const velocities = makeVec3Array(1);
            velocities.x[0] = 5;
            const ages = new Float32Array(1);
            const lifetimes = new Float32Array(1).fill(5);
            const masses = new Float32Array(1).fill(1);
            mod.applyForces(positions, velocities, ages, lifetimes, masses, 1, 0.1);
            expect(velocities.x[0]).toBe(5);
        });
    });

    describe('falloff calculations', () => {
        it('point force respects range limit', () => {
            const mod = new ForceModule(
                makeForceConfig({
                    forces: [
                        {
                            type: 'point',
                            strength: makeCurve(10),
                            direction: { x: 1, y: 0, z: 0 },
                            position: { x: 0, y: 0, z: 0 },
                            falloffRadius: 5,
                        } as any,
                    ],
                })
            );
            const count = 1;
            const positions = makeVec3Array(count);
            positions.x[0] = 10; // beyond range
            const velocities = makeVec3Array(count);
            const ages = new Float32Array(count);
            const lifetimes = new Float32Array(count).fill(5);
            const masses = new Float32Array(count).fill(1);

            mod.applyForces(positions, velocities, ages, lifetimes, masses, count, 0.1);

            // Beyond range -> no effect
            expect(velocities.x[0]).toBe(0);
        });
    });
});
