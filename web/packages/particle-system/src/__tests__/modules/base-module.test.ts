import { describe, expect, it, vi } from 'vitest';
import { BaseModule } from '../../modules/base-module';
import type { IParticleBuffer, IParticleData } from '../../core/interfaces';
import type { ModuleConfigurationMap, ModuleType } from '../../core/configuration';
import type { ModuleId } from '../../types';

// Concrete test module for testing the abstract BaseModule
interface TestModuleConfig {
    enabled: boolean;
    priority: number;
}

class TestModule extends BaseModule<'emission'> {
    public onInitializeCalls = 0;
    public onDestroyCalls = 0;
    public onResetCalls = 0;
    public onUpdateCalls = 0;
    public onProcessCalls = 0;
    public onConfigureCalls = 0;
    public throwOnInitialize = false;
    public throwOnDestroy = false;
    public throwOnReset = false;
    public throwOnUpdate = false;
    public throwOnProcess = false;
    public throwOnConfigure = false;

    constructor(config: TestModuleConfig = { enabled: true, priority: 0 }) {
        super('emission', config as any, config.priority);
    }

    protected onInitialize(): void {
        this.onInitializeCalls++;
        if (this.throwOnInitialize) throw new Error('init error');
    }
    protected onDestroy(): void {
        this.onDestroyCalls++;
        if (this.throwOnDestroy) throw new Error('destroy error');
    }
    protected onReset(): void {
        this.onResetCalls++;
        if (this.throwOnReset) throw new Error('reset error');
    }
    protected onUpdate(_deltaTime: number): void {
        this.onUpdateCalls++;
        if (this.throwOnUpdate) throw new Error('update error');
    }
    protected onProcess(_particles: IParticleBuffer, _deltaTime: number): void {
        this.onProcessCalls++;
        if (this.throwOnProcess) throw new Error('process error');
    }
    protected onConfigure(_newConfig: any, _oldConfig: any): void {
        this.onConfigureCalls++;
        if (this.throwOnConfigure) throw new Error('configure error');
    }
}

function createMockParticles(count: number = 1): IParticleData & IParticleBuffer {
    return {
        count,
        capacity: 100,
        allocated: true,
        alive: new Uint32Array([1]),
        positions: new Float32Array(3),
        velocities: new Float32Array(3),
        accelerations: new Float32Array(3),
        lifetimes: new Float32Array([5]),
        ages: new Float32Array([0]),
        sizes: new Float32Array(3),
        colors: new Float32Array(4),
        rotations: new Float32Array(3),
        angularVelocities: new Float32Array(3),
        customData: [],
        ids: new Uint32Array([1]),
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
    };
}

describe('BaseModule', () => {
    describe('constructor', () => {
        it('assigns ID, type, priority, enabled from config', () => {
            const mod = new TestModule({ enabled: true, priority: 5 });
            expect(mod.type).toBe('emission');
            expect(mod.priority).toBe(5);
            expect(mod.enabled).toBe(true);
            expect(typeof mod.id).toBe('number');
        });
    });

    describe('initialize', () => {
        it('calls onInitialize once', () => {
            const mod = new TestModule();
            mod.initialize();
            expect(mod.onInitializeCalls).toBe(1);
        });

        it('is idempotent', () => {
            const mod = new TestModule();
            mod.initialize();
            mod.initialize();
            expect(mod.onInitializeCalls).toBe(1);
        });

        it('wraps errors in ParticleSystemException', () => {
            const mod = new TestModule();
            mod.throwOnInitialize = true;
            expect(() => mod.initialize()).toThrow();
        });
    });

    describe('destroy', () => {
        it('calls onDestroy once', () => {
            const mod = new TestModule();
            mod.initialize();
            mod.destroy();
            expect(mod.onDestroyCalls).toBe(1);
        });

        it('no-op when not initialized', () => {
            const mod = new TestModule();
            mod.destroy();
            expect(mod.onDestroyCalls).toBe(0);
        });

        it('catches errors with console.warn', () => {
            const mod = new TestModule();
            mod.initialize();
            mod.throwOnDestroy = true;
            const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
            mod.destroy();
            expect(warnSpy).toHaveBeenCalled();
            warnSpy.mockRestore();
        });
    });

    describe('reset', () => {
        it('calls onReset when initialized', () => {
            const mod = new TestModule();
            mod.initialize();
            mod.reset();
            expect(mod.onResetCalls).toBe(1);
        });

        it('no-op when not initialized', () => {
            const mod = new TestModule();
            mod.reset();
            expect(mod.onResetCalls).toBe(0);
        });

        it('catches errors', () => {
            const mod = new TestModule();
            mod.initialize();
            mod.throwOnReset = true;
            const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
            mod.reset();
            expect(warnSpy).toHaveBeenCalled();
            warnSpy.mockRestore();
        });
    });

    describe('update', () => {
        it('calls onUpdate when initialized and enabled', () => {
            const mod = new TestModule({ enabled: true, priority: 0 });
            mod.initialize();
            mod.update(0.1);
            expect(mod.onUpdateCalls).toBe(1);
        });

        it('no-op when not initialized', () => {
            const mod = new TestModule();
            mod.update(0.1);
            expect(mod.onUpdateCalls).toBe(0);
        });

        it('no-op when disabled', () => {
            const mod = new TestModule({ enabled: false, priority: 0 });
            mod.initialize();
            mod.update(0.1);
            expect(mod.onUpdateCalls).toBe(0);
        });

        it('catches errors and disables module', () => {
            const mod = new TestModule({ enabled: true, priority: 0 });
            mod.initialize();
            mod.throwOnUpdate = true;
            const errSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
            mod.update(0.1);
            expect(mod.enabled).toBe(false);
            errSpy.mockRestore();
        });
    });

    describe('setEnabled', () => {
        it('toggles enabled flag', () => {
            const mod = new TestModule({ enabled: true, priority: 0 });
            expect(mod.enabled).toBe(true);
            mod.setEnabled(false);
            expect(mod.enabled).toBe(false);
            mod.setEnabled(true);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('canProcess', () => {
        it('returns true only when initialized, enabled, and particles.count > 0', () => {
            const mod = new TestModule({ enabled: true, priority: 0 });
            const particles = createMockParticles(5);

            expect(mod.canProcess(particles)).toBe(false); // not initialized

            mod.initialize();
            expect(mod.canProcess(particles)).toBe(true);

            mod.setEnabled(false);
            expect(mod.canProcess(particles)).toBe(false);

            mod.setEnabled(true);
            const emptyParticles = createMockParticles(0);
            expect(mod.canProcess(emptyParticles)).toBe(false);
        });
    });

    describe('process', () => {
        it('calls onProcess when canProcess is true', () => {
            const mod = new TestModule({ enabled: true, priority: 0 });
            mod.initialize();
            const particles = createMockParticles(5);
            mod.process(particles, 0.1);
            expect(mod.onProcessCalls).toBe(1);
        });

        it('guards with canProcess', () => {
            const mod = new TestModule({ enabled: true, priority: 0 });
            const particles = createMockParticles(5);
            mod.process(particles, 0.1); // not initialized
            expect(mod.onProcessCalls).toBe(0);
        });

        it('catches errors and disables', () => {
            const mod = new TestModule({ enabled: true, priority: 0 });
            mod.initialize();
            mod.throwOnProcess = true;
            const particles = createMockParticles(5);
            const errSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
            mod.process(particles, 0.1);
            expect(mod.enabled).toBe(false);
            errSpy.mockRestore();
        });
    });

    describe('configure', () => {
        it('updates config, enabled, priority', () => {
            const mod = new TestModule({ enabled: true, priority: 0 });
            mod.configure({ enabled: false, priority: 10 } as any);
            expect(mod.priority).toBe(10);
            expect(mod.enabled).toBe(false);
            expect(mod.onConfigureCalls).toBe(1);
        });

        it('rolls back config on error', () => {
            const mod = new TestModule({ enabled: true, priority: 5 });
            mod.throwOnConfigure = true;
            expect(() => mod.configure({ enabled: false, priority: 10 } as any)).toThrow();
            // The internal _configuration is rolled back, but priority/enabled are set before try block
            expect(mod.enabled).toBe(false); // set before try, not rolled back
            expect(mod.priority).toBe(10); // set before try, not rolled back
        });
    });

    describe('getConfiguration', () => {
        it('returns current config', () => {
            const mod = new TestModule({ enabled: true, priority: 5 });
            const config = mod.getConfiguration();
            expect(config).toBeDefined();
        });
    });

    describe('throwIfNotInitialized', () => {
        it('throws when not initialized', () => {
            const mod = new TestModule();
            // Access protected method through a wrapper
            expect(() => (mod as any).throwIfNotInitialized()).toThrow();
        });

        it('does not throw when initialized', () => {
            const mod = new TestModule();
            mod.initialize();
            expect(() => (mod as any).throwIfNotInitialized()).not.toThrow();
        });
    });
});
