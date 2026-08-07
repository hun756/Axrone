import { describe, it, expect, vi, beforeEach } from 'vitest';
import { CustomDataModule } from '../../modules/custom-data-module';
import type { CustomDataConfiguration } from '../../core/configuration';
import type { IParticleBuffer } from '../../core/interfaces';

function makeCustomDataConfig(
    overrides: Partial<CustomDataConfiguration> = {}
): CustomDataConfiguration {
    return {
        enabled: true,
        priority: 1000,
        slot1: { type: 'float' },
        slot2: { type: 'vector2' },
        slot3: { type: 'vector3' },
        slot4: { type: 'vector4' },
        ...overrides,
    } as any;
}

function createMockBuffer(count = 1): IParticleBuffer {
    const alive = new Uint32Array(100);
    for (let i = 0; i < count; i++) alive[i] = 1;
    return {
        get count() {
            return count;
        },
        capacity: 100,
        allocated: true,
        alive,
        positions: new Float32Array(300),
        velocities: new Float32Array(300),
        accelerations: new Float32Array(300),
        lifetimes: new Float32Array(100).fill(5),
        ages: new Float32Array(100),
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

describe('CustomDataModule', () => {
    describe('constructor', () => {
        it('creates module with type custom and priority 1000', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            expect(mod.type).toBe('custom');
            expect(mod.priority).toBe(1000);
            expect(mod.enabled).toBe(true);
        });
    });

    describe('lifecycle', () => {
        it('initialize succeeds', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            expect(() => mod.initialize()).not.toThrow();
        });

        it('reset after initialize does not throw', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            expect(() => mod.reset()).not.toThrow();
        });

        it('destroy after initialize does not throw', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            expect(() => mod.destroy()).not.toThrow();
        });
    });

    describe('slot initialization', () => {
        it('creates slots from config', () => {
            const mod = new CustomDataModule(
                makeCustomDataConfig({
                    slot1: { type: 'float' },
                    slot2: { type: 'vector3' },
                    slot3: { type: 'color' },
                    slot4: { type: 'vector4' },
                })
            );
            mod.initialize();
            const names = mod.getAllSlotNames();
            expect(names).toContain('slot1');
            expect(names).toContain('slot2');
            expect(names).toContain('slot3');
            expect(names).toContain('slot4');
        });

        it('getSlotType returns correct types', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            expect(mod.getSlotType('slot1')).toBe('float');
            expect(mod.getSlotType('slot2')).toBe('vector2');
            expect(mod.getSlotType('slot3')).toBe('vector3');
            expect(mod.getSlotType('slot4')).toBe('vector4');
        });
    });

    describe('setParticleData / getParticleData', () => {
        it('sets and gets float data', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016); // creates particle data entry

            expect(mod.setParticleData(0, 'slot1', 42)).toBe(true);
            expect(mod.getParticleData(0, 'slot1')).toBe(42);
        });

        it('sets and gets vector data', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);

            const vec = new Float32Array([1, 2]);
            expect(mod.setParticleData(0, 'slot2', vec)).toBe(true);
            const result = mod.getParticleData(0, 'slot2');
            expect(result).toBeInstanceOf(Float32Array);
            expect((result as Float32Array)[0]).toBe(1);
            expect((result as Float32Array)[1]).toBe(2);
        });

        it('returns false for non-existent particle', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            expect(mod.setParticleData(999, 'slot1', 42)).toBe(false);
        });

        it('returns false for non-existent slot', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.setParticleData(0, 'noSlot', 42)).toBe(false);
        });
    });

    describe('typed setters', () => {
        it('setParticleFloat works', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.setParticleFloat(0, 'slot1', 3.14)).toBe(true);
            expect(mod.getParticleFloat(0, 'slot1')).toBeCloseTo(3.14);
        });

        it('setParticleVector2 works', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.setParticleVector2(0, 'slot2', 1, 2)).toBe(true);
            const vec = mod.getParticleVector(0, 'slot2');
            expect(vec).toBeDefined();
            expect(vec![0]).toBe(1);
            expect(vec![1]).toBe(2);
        });

        it('setParticleVector3 works', () => {
            const mod = new CustomDataModule(
                makeCustomDataConfig({ slot1: { type: 'vector3' } })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.setParticleVector3(0, 'slot1', 1, 2, 3)).toBe(true);
        });

        it('setParticleVector4 works', () => {
            const mod = new CustomDataModule(
                makeCustomDataConfig({ slot1: { type: 'vector4' } })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.setParticleVector4(0, 'slot1', 1, 2, 3, 4)).toBe(true);
        });

        it('setParticleColor works', () => {
            const mod = new CustomDataModule(
                makeCustomDataConfig({ slot1: { type: 'color' } })
            );
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.setParticleColor(0, 'slot1', 1, 0.5, 0.25, 1)).toBe(true);
            const vec = mod.getParticleVector(0, 'slot1');
            expect(vec).toBeDefined();
            expect(vec![0]).toBeCloseTo(1);
            expect(vec![3]).toBeCloseTo(1);
        });
    });

    describe('type mismatch rejection', () => {
        it('rejects number for vector slot', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            // slot2 is vector2, passing a number should fail
            expect(mod.setParticleData(0, 'slot2', 42)).toBe(false);
        });

        it('rejects wrong-size Float32Array', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            // slot2 is vector2 (2 components), passing 3-component array should fail
            expect(mod.setParticleData(0, 'slot2', new Float32Array([1, 2, 3]))).toBe(false);
        });
    });

    describe('getSlotBuffer', () => {
        it('returns buffer for valid slot', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buf = mod.getSlotBuffer('slot1');
            expect(buf).toBeInstanceOf(Float32Array);
        });

        it('returns undefined for invalid slot', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            expect(mod.getSlotBuffer('noSlot')).toBeUndefined();
        });
    });

    describe('dirty tracking', () => {
        it('slot starts clean', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            expect(mod.isSlotDirty('slot1')).toBe(false);
        });

        it('slot becomes dirty after process', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.isSlotDirty('slot1')).toBe(true);
        });

        it('markSlotClean resets dirty flag', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.isSlotDirty('slot1')).toBe(true);
            mod.markSlotClean('slot1');
            expect(mod.isSlotDirty('slot1')).toBe(false);
        });

        it('isSlotDirty returns false for non-existent slot', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            expect(mod.isSlotDirty('noSlot')).toBe(false);
        });
    });

    describe('active particle count', () => {
        it('tracks active particles', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(3);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(3);
        });
    });

    describe('dead particle cleanup', () => {
        it('removes data for dead particles', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(2);

            // Kill particle 1
            buffer.alive[1] = 0;
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(1);
        });
    });

    describe('configure - slot type change', () => {
        it('re-initializes slots when types change', () => {
            const mod = new CustomDataModule(
                makeCustomDataConfig({ slot1: { type: 'float' } })
            );
            mod.initialize();
            expect(mod.getSlotType('slot1')).toBe('float');

            mod.configure(
                makeCustomDataConfig({ slot1: { type: 'vector3' } }) as any
            );
            expect(mod.getSlotType('slot1')).toBe('vector3');
        });
    });

    describe('disabled module', () => {
        it('does nothing when disabled', () => {
            const mod = new CustomDataModule(makeCustomDataConfig({ enabled: false }));
            mod.initialize();
            const buffer = createMockBuffer(1);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(0);
        });
    });

    describe('reset', () => {
        it('clears particle data', () => {
            const mod = new CustomDataModule(makeCustomDataConfig());
            mod.initialize();
            const buffer = createMockBuffer(2);
            mod.process(buffer, 0.016);
            expect(mod.getActiveParticleCount()).toBe(2);
            mod.reset();
            expect(mod.getActiveParticleCount()).toBe(0);
        });
    });
});
