import { describe, expect, it } from 'vitest';
import { RenderTextureRegistry, createRenderPassGraph } from '../graph';
import type { RenderResourceAllocator, RenderTextureDescriptor } from '../types';

interface MockNative {
    id: number;
    descriptor: RenderTextureDescriptor;
}

interface AllocatorCalls {
    created: MockNative[];
    destroyed: MockNative[];
}

const createMockAllocator = (): { allocator: RenderResourceAllocator<MockNative>; calls: AllocatorCalls } => {
    const calls: AllocatorCalls = { created: [], destroyed: [] };
    let nextId = 1;
    return {
        calls,
        allocator: {
            createTexture(descriptor, _previous) {
                const native: MockNative = { id: nextId++, descriptor: { ...descriptor } };
                calls.created.push(native);
                if (_previous) {
                    calls.destroyed.push(_previous);
                }
                return native;
            },
            destroyTexture(native) {
                calls.destroyed.push(native);
            },
        },
    };
};

const baseDescriptor: RenderTextureDescriptor = {
    width: 512,
    height: 512,
    format: 'rgba8',
    usage: ['sampled'],
};

describe('RenderTextureRegistry', () => {
    describe('transient textures', () => {
        it('allocates a new transient texture on first acquire', () => {
            const { allocator, calls } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });
            registry.beginFrame(1);

            const snap = registry.acquireTexture('frame:color', baseDescriptor, 'transient');
            expect(snap.id).toBe('frame:color');
            expect(snap.lifetime).toBe('transient');
            expect(snap.version).toBe(1);
            expect(snap.reused).toBe(false);
            expect(calls.created).toHaveLength(1);
        });

        it('returns existing record for same id and same signature within a frame', () => {
            const { allocator, calls } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });
            registry.beginFrame(1);

            registry.acquireTexture('frame:color', baseDescriptor, 'transient');
            registry.acquireTexture('frame:color', baseDescriptor, 'transient');
            expect(calls.created).toHaveLength(1);
        });

        it('throws RESOURCE_CONFLICT for same id but different descriptor', () => {
            const { allocator } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });
            registry.beginFrame(1);

            registry.acquireTexture('frame:color', baseDescriptor, 'transient');
            expect(() =>
                registry.acquireTexture('frame:color', { ...baseDescriptor, width: 256 }, 'transient')
            ).toThrow(/RESOURCE_CONFLICT/);
        });

        it('reuses texture from free pool after endFrame/beginFrame', () => {
            const { allocator, calls } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });

            registry.beginFrame(1);
            registry.acquireTexture('frame:a', baseDescriptor, 'transient');
            registry.endFrame();

            registry.beginFrame(2);
            const snap = registry.acquireTexture('frame:b', baseDescriptor, 'transient');
            expect(snap.reused).toBe(true);
            expect(registry.reuseCount).toBe(1);
            expect(calls.created).toHaveLength(1);
        });
    });

    describe('persistent textures', () => {
        it('allocates persistent texture on first acquire', () => {
            const { allocator } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });
            registry.beginFrame(1);

            const snap = registry.acquireTexture('swap:back', baseDescriptor, 'persistent');
            expect(snap.lifetime).toBe('persistent');
            expect(snap.version).toBe(1);
        });

        it('increments reuseCount when same descriptor is re-acquired', () => {
            const { allocator } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });

            registry.beginFrame(1);
            registry.acquireTexture('swap:back', baseDescriptor, 'persistent');
            registry.endFrame();

            registry.beginFrame(2);
            registry.acquireTexture('swap:back', baseDescriptor, 'persistent');
            expect(registry.reuseCount).toBe(1);
        });

        it('bumps version when descriptor changes for persistent texture', () => {
            const { allocator } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });

            registry.beginFrame(1);
            registry.acquireTexture('swap:back', baseDescriptor, 'persistent');
            registry.endFrame();

            registry.beginFrame(2);
            const snap = registry.acquireTexture('swap:back', { ...baseDescriptor, width: 1024 }, 'persistent');
            expect(snap.version).toBe(2);
            expect(snap.reused).toBe(false);
        });
    });

    describe('normalizeDescriptor validation', () => {
        it('throws for invalid width', () => {
            const registry = new RenderTextureRegistry();
            registry.beginFrame(1);
            expect(() =>
                registry.acquireTexture('t', { ...baseDescriptor, width: 0 }, 'transient')
            ).toThrow(/INVALID_ARGUMENT/);
        });

        it('throws for NaN height', () => {
            const registry = new RenderTextureRegistry();
            registry.beginFrame(1);
            expect(() =>
                registry.acquireTexture('t', { ...baseDescriptor, height: NaN }, 'transient')
            ).toThrow(/INVALID_ARGUMENT/);
        });

        it('floors fractional dimensions', () => {
            const { allocator } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });
            registry.beginFrame(1);
            const snap = registry.acquireTexture('t', { ...baseDescriptor, width: 100.7, height: 200.9 }, 'transient');
            expect(snap.descriptor.width).toBe(100);
            expect(snap.descriptor.height).toBe(200);
        });

        it('defaults empty usage to sampled', () => {
            const { allocator } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });
            registry.beginFrame(1);
            const snap = registry.acquireTexture('t', { ...baseDescriptor, usage: [] }, 'transient');
            expect(snap.descriptor.usage).toEqual(['sampled']);
        });
    });

    describe('endFrame', () => {
        it('moves transient textures to free pool', () => {
            const { allocator, calls } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });
            registry.beginFrame(1);
            registry.acquireTexture('frame:a', baseDescriptor, 'transient');
            registry.endFrame();

            expect(registry.hasTexture('frame:a')).toBe(false);
            expect(calls.destroyed).toHaveLength(0);
        });

        it('keeps persistent textures across frames', () => {
            const { allocator } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });
            registry.beginFrame(1);
            registry.acquireTexture('swap:bb', baseDescriptor, 'persistent');
            registry.endFrame();

            expect(registry.hasTexture('swap:bb')).toBe(true);
        });
    });

    describe('dispose', () => {
        it('destroys all native textures', () => {
            const { allocator, calls } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });

            registry.beginFrame(1);
            registry.acquireTexture('frame:a', baseDescriptor, 'transient');
            registry.acquireTexture('swap:b', baseDescriptor, 'persistent');
            registry.endFrame();

            registry.dispose();
            expect(calls.destroyed.length).toBeGreaterThanOrEqual(2);
            expect(registry.isDisposed).toBe(true);
        });

        it('is idempotent', () => {
            const registry = new RenderTextureRegistry();
            registry.dispose();
            registry.dispose();
            expect(registry.isDisposed).toBe(true);
        });

        it('throws on beginFrame after dispose', () => {
            const registry = new RenderTextureRegistry();
            registry.dispose();
            expect(() => registry.beginFrame(1)).toThrow(/INVALID_ARGUMENT/);
        });

        it('throws on acquireTexture after dispose', () => {
            const registry = new RenderTextureRegistry();
            registry.dispose();
            expect(() => registry.acquireTexture('t', baseDescriptor, 'transient')).toThrow(/INVALID_ARGUMENT/);
        });
    });

    describe('query methods', () => {
        it('hasTexture returns true for active and persistent', () => {
            const registry = new RenderTextureRegistry();
            registry.beginFrame(1);
            registry.acquireTexture('frame:a', baseDescriptor, 'transient');
            registry.acquireTexture('swap:b', baseDescriptor, 'persistent');

            expect(registry.hasTexture('frame:a')).toBe(true);
            expect(registry.hasTexture('swap:b')).toBe(true);
            expect(registry.hasTexture('nonexistent')).toBe(false);
        });

        it('getTexture returns snapshot or null', () => {
            const registry = new RenderTextureRegistry();
            registry.beginFrame(1);
            registry.acquireTexture('frame:a', baseDescriptor, 'transient');

            expect(registry.getTexture('frame:a')).not.toBeNull();
            expect(registry.getTexture('nope')).toBeNull();
        });

        it('listTextures returns frame resources', () => {
            const registry = new RenderTextureRegistry();
            registry.beginFrame(1);
            registry.acquireTexture('frame:a', baseDescriptor, 'transient');
            registry.acquireTexture('swap:b', baseDescriptor, 'persistent');

            expect(registry.listTextures()).toHaveLength(2);
        });

        it('releasePersistent removes and returns true', () => {
            const { allocator, calls } = createMockAllocator();
            const registry = new RenderTextureRegistry({ allocator });
            registry.beginFrame(1);
            registry.acquireTexture('swap:b', baseDescriptor, 'persistent');
            registry.endFrame();

            expect(registry.releasePersistent('swap:b')).toBe(true);
            expect(registry.hasTexture('swap:b')).toBe(false);
            expect(calls.destroyed).toHaveLength(1);
        });

        it('releasePersistent returns false for unknown id', () => {
            const registry = new RenderTextureRegistry();
            expect(registry.releasePersistent('nope')).toBe(false);
        });
    });

    describe('pool capacity', () => {
        it('destroys excess transient textures when pool bucket is full', () => {
            const { allocator, calls } = createMockAllocator();
            // resourcePoolCapacity is clamped to Math.max(16, value), so use 16
            const registry = new RenderTextureRegistry({ allocator, resourcePoolCapacity: 16 });

            // Each frame: acquire 17 transient textures with the SAME signature (same descriptor),
            // then endFrame moves them to the free pool bucket. Bucket capacity is 16, so the 17th
            // should be destroyed.
            for (let frame = 0; frame < 2; frame++) {
                registry.beginFrame(frame + 1);
                for (let i = 0; i < 17; i++) {
                    registry.acquireTexture(`tex-${frame}-${i}`, baseDescriptor, 'transient');
                }
                registry.endFrame();
            }

            expect(calls.destroyed.length).toBeGreaterThanOrEqual(1);
        });
    });
});

describe('createRenderPassGraph', () => {
    it('returns a RenderTextureRegistry instance', () => {
        const graph = createRenderPassGraph();
        expect(graph).toBeInstanceOf(RenderTextureRegistry);
        expect(graph.isDisposed).toBe(false);
    });
});
