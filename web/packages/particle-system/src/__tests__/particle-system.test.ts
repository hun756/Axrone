import { describe, expect, it, vi } from 'vitest';
import { ParticleSystem } from '../particle-system';
import type { IParticleEmitter, IModule, IParticleData, IParticleBuffer } from '../core/interfaces';
import type { ParticleId, EmitterId, ModuleId } from '../types';

function createMockEmitter(id: number): IParticleEmitter {
    return {
        id: id as EmitterId,
        initialize: vi.fn(),
        destroy: vi.fn(),
        reset: vi.fn(),
        stop: vi.fn(),
        update: vi.fn(),
        emit: vi.fn().mockReturnValue([]),
    };
}

function createMockModule(id: number, type: string = 'emission', priority = 0): IModule {
    return {
        id: id as ModuleId,
        type: type as any,
        priority,
        enabled: true,
        dependencies: [],
        initialize: vi.fn(),
        destroy: vi.fn(),
        reset: vi.fn(),
        update: vi.fn(),
        setEnabled: vi.fn(),
        canProcess: vi.fn().mockReturnValue(false),
        process: vi.fn(),
        configure: vi.fn(),
        getConfiguration: vi.fn().mockReturnValue({}),
    };
}

describe('ParticleSystem', () => {
    describe('constructor', () => {
        it('assigns unique system ID', () => {
            const sys1 = new ParticleSystem();
            const sys2 = new ParticleSystem();
            expect(sys1.id).not.toBe(sys2.id);
        });

        it('creates default configuration', () => {
            const sys = new ParticleSystem();
            const config = sys.getConfiguration();
            expect(config.maxParticles).toBe(1000);
            expect(config.enableSpatialOptimization).toBe(true);
        });

        it('accepts partial config overrides', () => {
            const sys = new ParticleSystem({ maxParticles: 500 });
            expect(sys.getConfiguration().maxParticles).toBe(500);
        });
    });

    describe('initialize', () => {
        it('allocates buffer and initializes modules/emitters', () => {
            const sys = new ParticleSystem();
            const mod = createMockModule(1);
            const emitter = createMockEmitter(1);
            sys.addModule(mod);
            sys.addEmitter(emitter);

            sys.initialize();
            expect(mod.initialize).toHaveBeenCalled();
            expect(emitter.initialize).toHaveBeenCalled();
        });

        it('is idempotent', () => {
            const sys = new ParticleSystem();
            const mod = createMockModule(1);
            sys.addModule(mod);

            sys.initialize();
            sys.initialize();
            expect(mod.initialize).toHaveBeenCalledTimes(1);
        });
    });

    describe('destroy', () => {
        it('stops system and destroys emitters/modules', () => {
            const sys = new ParticleSystem();
            const mod = createMockModule(1);
            const emitter = createMockEmitter(1);
            sys.addModule(mod);
            sys.addEmitter(emitter);
            sys.initialize();

            sys.destroy();
            expect(mod.destroy).toHaveBeenCalled();
            expect(emitter.destroy).toHaveBeenCalled();
        });

        it('idempotent when not initialized', () => {
            const sys = new ParticleSystem();
            sys.destroy(); // should not throw
        });
    });

    describe('reset', () => {
        it('throws when not initialized', () => {
            const sys = new ParticleSystem();
            expect(() => sys.reset()).toThrow();
        });

        it('clears particles and resets time', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            sys.emit(5);
            sys.reset();
            expect(sys.particleCount).toBe(0);
            expect(sys.time).toBe(0);
        });
    });

    describe('play/pause/stop', () => {
        it('play throws when not initialized', () => {
            const sys = new ParticleSystem();
            expect(() => sys.play()).toThrow();
        });

        it('correct state transitions', () => {
            const sys = new ParticleSystem();
            sys.initialize();

            sys.play();
            expect(sys.isPlaying).toBe(true);
            expect(sys.isPaused).toBe(false);

            sys.pause();
            expect(sys.isPaused).toBe(true);

            sys.stop();
            expect(sys.isPlaying).toBe(false);
            expect(sys.isPaused).toBe(false);
        });

        it('pause does not require initialized', () => {
            const sys = new ParticleSystem();
            sys.pause(); // should not throw
            expect(sys.isPaused).toBe(true);
        });
    });

    describe('restart', () => {
        it('stop + reset + play sequence', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            sys.play();
            sys.emit(3);

            sys.restart();
            expect(sys.isPlaying).toBe(true);
            expect(sys.particleCount).toBe(0);
        });
    });

    describe('update', () => {
        it('no-op when not playing', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            sys.update(0.1);
            expect(sys.time).toBe(0);
        });

        it('no-op when paused', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            sys.play();
            sys.pause();
            sys.update(0.1);
            expect(sys.time).toBe(0);
        });

        it('advances time when playing', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            sys.play();
            sys.update(0.5);
            expect(sys.time).toBe(0.5);
        });

        it('kills expired particles', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            sys.play();
            const ids = sys.emit(3);
            // Set very short lifetime
            const particles = sys.getParticles();
            for (let i = 0; i < particles.capacity; i++) {
                if (particles.alive[i]) {
                    particles.setLifetime(i, 0.1);
                }
            }
            sys.update(0.2);
            expect(sys.particleCount).toBe(0);
        });
    });

    describe('emitter management', () => {
        it('addEmitter/removeEmitter/getEmitter', () => {
            const sys = new ParticleSystem();
            const emitter = createMockEmitter(42);
            sys.addEmitter(emitter);
            expect(sys.getEmitter(42 as EmitterId)).toBe(emitter);

            sys.removeEmitter(42 as EmitterId);
            expect(sys.getEmitter(42 as EmitterId)).toBeNull();
        });

        it('auto-initializes emitter if system already initialized', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            const emitter = createMockEmitter(1);
            sys.addEmitter(emitter);
            expect(emitter.initialize).toHaveBeenCalled();
        });
    });

    describe('module management', () => {
        it('addModule/removeModule/getModule', () => {
            const sys = new ParticleSystem();
            const mod = createMockModule(1, 'emission');
            sys.addModule(mod);
            expect(sys.getModule(1 as ModuleId)).toBe(mod);

            sys.removeModule(1 as ModuleId);
            expect(sys.getModule(1 as ModuleId)).toBeNull();
        });

        it('getModulesByType returns correct modules', () => {
            const sys = new ParticleSystem();
            const mod1 = createMockModule(1, 'emission', 10);
            const mod2 = createMockModule(2, 'emission', 5);
            sys.addModule(mod1);
            sys.addModule(mod2);

            const emissionModules = sys.getModulesByType('emission');
            expect(emissionModules.length).toBe(2);
            // Should be sorted by priority descending
            expect(emissionModules[0].priority).toBe(10);
            expect(emissionModules[1].priority).toBe(5);
        });

        it('auto-initializes module if system initialized', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            const mod = createMockModule(1);
            sys.addModule(mod);
            expect(mod.initialize).toHaveBeenCalled();
        });
    });

    describe('emit', () => {
        it('creates particles with default data', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            const ids = sys.emit(5);
            expect(ids.length).toBe(5);
            expect(sys.particleCount).toBe(5);
        });

        it('emits birth events', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            const listener = vi.fn();
            sys.addEventListener('birth', listener);
            sys.emit(2);
            expect(listener).toHaveBeenCalledTimes(2);
        });

        it('delegates to emitter when emitterId provided', () => {
            const sys = new ParticleSystem();
            const emitter = createMockEmitter(1);
            (emitter.emit as any).mockReturnValue([10, 11]);
            sys.addEmitter(emitter);
            sys.initialize();

            const result = sys.emit(2, 1 as EmitterId);
            expect(emitter.emit).toHaveBeenCalledWith(2);
        });

        it('returns empty array for unknown emitterId', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            const result = sys.emit(2, 999 as EmitterId);
            expect(result).toEqual([]);
        });
    });

    describe('killParticle', () => {
        it('removes particle and emits death event', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            const ids = sys.emit(1);
            const deathListener = vi.fn();
            sys.addEventListener('death', deathListener);

            const result = sys.killParticle(ids[0]);
            expect(result).toBe(true);
            expect(sys.particleCount).toBe(0);
            expect(deathListener).toHaveBeenCalledTimes(1);
            expect(deathListener.mock.calls[0][0].reason).toBe('killed');
        });

        it('returns false for unknown particle', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            expect(sys.killParticle(999 as ParticleId)).toBe(false);
        });
    });

    describe('killAllParticles', () => {
        it('kills all alive particles', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            sys.emit(5);
            expect(sys.particleCount).toBe(5);
            sys.killAllParticles();
            expect(sys.particleCount).toBe(0);
        });
    });

    describe('event system', () => {
        it('addEventListener/removeEventListener', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            const listener = vi.fn();
            sys.addEventListener('birth', listener);
            sys.emit(1);
            expect(listener).toHaveBeenCalledTimes(1);

            sys.removeEventListener('birth', listener);
            sys.emit(1);
            expect(listener).toHaveBeenCalledTimes(1); // not called again
        });
    });

    describe('configure', () => {
        it('merges partial config', () => {
            const sys = new ParticleSystem();
            sys.configure({ maxParticles: 2000 });
            expect(sys.getConfiguration().maxParticles).toBe(2000);
        });

        it('resizes buffer when maxParticles changes after init', () => {
            const sys = new ParticleSystem();
            sys.initialize();
            sys.configure({ maxParticles: 2000 });
            const particles = sys.getParticles();
            expect(particles.capacity).toBe(2000);
        });
    });

    describe('getConfiguration', () => {
        it('returns copy of current config', () => {
            const sys = new ParticleSystem({ maxParticles: 500 });
            const config = sys.getConfiguration();
            expect(config.maxParticles).toBe(500);
        });
    });
});
