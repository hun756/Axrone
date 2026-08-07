import { Camera3D } from '@axrone/geometry';
import { describe, expect, it } from 'vitest';
import { ParticleSystemRenderer, BlendMode, CullMode } from '../particle-renderer';
import { ParticleSOA } from '../particle-soa';
import { SortMode } from '../types';

const createMaterial = () => ({
    id: 'particle-material',
    shader: {
        id: 'particle-shader',
        vertexSource: '',
        fragmentSource: '',
        uniforms: {},
        attributes: {},
    },
    blendMode: BlendMode.Alpha,
    sortMode: SortMode.Distance,
    priority: 0,
    cullMode: CullMode.None,
    depthTest: true,
    depthWrite: false,
    properties: {},
});

describe('ParticleSystemRenderer', () => {
    it('accepts Camera3D as a frustum source for particle culling', () => {
        const renderer = new ParticleSystemRenderer(8);
        const particles = new ParticleSOA({ capacity: 8, autoResize: false });
        const material = createMaterial();
        const camera = Camera3D.perspective({
            id: 'camera:particles',
            projection: {
                kind: 'perspective',
                verticalFieldOfView: Math.PI / 3,
                aspectRatio: 1,
                near: 0.1,
                far: 50,
            },
            pose: {
                position: [0, 0, 0],
                target: [0, 0, -1],
            },
        });

        (particles as unknown as { _initializeFreeList(): void })._initializeFreeList();
        particles.addParticle({ x: 0, y: 0, z: -3 }, { x: 0, y: 0, z: 0 }, 5, 1);
        particles.addParticle({ x: 18, y: 0, z: -3 }, { x: 0, y: 0, z: 0 }, 5, 1);

        renderer.updateFrustum(camera);
        renderer.createRenderBatches(particles, [material]);

        expect(renderer.getStats().renderedParticles).toBe(1);
    });

    it('constructor creates renderer with correct maxParticles and default settings', () => {
        const renderer = new ParticleSystemRenderer(64);
        const stats = renderer.getStats();
        expect(stats.totalParticles).toBe(0);
        expect(stats.renderedParticles).toBe(0);
        expect(stats.batchCount).toBe(0);
    });

    it('constructor accepts custom settings', () => {
        const renderer = new ParticleSystemRenderer(64, {
            maxBatchSize: 500,
            cullingEnabled: false,
        });
        const settings = renderer.getSettings();
        expect(settings.maxBatchSize).toBe(500);
        expect(settings.cullingEnabled).toBe(false);
    });

    it('updateFrustum is no-op when culling disabled', () => {
        const renderer = new ParticleSystemRenderer(8, { frustumCulling: false });
        const camera = Camera3D.perspective({
            id: 'camera:test',
            projection: {
                kind: 'perspective',
                verticalFieldOfView: Math.PI / 3,
                aspectRatio: 1,
                near: 0.1,
                far: 50,
            },
            pose: { position: [0, 0, 0], target: [0, 0, -1] },
        });
        // Should not throw
        renderer.updateFrustum(camera);
    });

    it('updateFrustum accepts Float32Array source', () => {
        const renderer = new ParticleSystemRenderer(8);
        const frustumData = new Float32Array(24);
        // Should not throw
        renderer.updateFrustum(frustumData);
    });

    it('createRenderBatches returns empty for zero visible particles', () => {
        const renderer = new ParticleSystemRenderer(8);
        const particles = new ParticleSOA({ capacity: 8, autoResize: false });
        const material = createMaterial();
        (particles as unknown as { _initializeFreeList(): void })._initializeFreeList();

        renderer.updateFrustum(
            Camera3D.perspective({
                id: 'camera:test2',
                projection: { kind: 'perspective', verticalFieldOfView: Math.PI / 3, aspectRatio: 1, near: 0.1, far: 50 },
                pose: { position: [0, 0, 0], target: [0, 0, -1] },
            })
        );
        const batches = renderer.createRenderBatches(particles, [material]);
        expect(batches.length).toBe(0);
    });

    it('getStats returns correct stats structure', () => {
        const renderer = new ParticleSystemRenderer(16);
        const stats = renderer.getStats();
        expect(stats).toHaveProperty('totalParticles');
        expect(stats).toHaveProperty('renderedParticles');
        expect(stats).toHaveProperty('batchCount');
        expect(stats).toHaveProperty('drawCalls');
        expect(stats).toHaveProperty('sortTime');
        expect(stats).toHaveProperty('batchTime');
        expect(stats).toHaveProperty('renderTime');
        expect(stats).toHaveProperty('memoryUsage');
    });

    it('resetStats zeroes all stats', () => {
        const renderer = new ParticleSystemRenderer(16);
        renderer.resetStats();
        const stats = renderer.getStats();
        expect(stats.totalParticles).toBe(0);
        expect(stats.renderedParticles).toBe(0);
        expect(stats.batchCount).toBe(0);
    });

    it('updateSettings merges settings correctly', () => {
        const renderer = new ParticleSystemRenderer(16);
        renderer.updateSettings({ maxBatchSize: 250 });
        const settings = renderer.getSettings();
        expect(settings.maxBatchSize).toBe(250);
        // Other settings should remain at defaults
        expect(settings.enableDepthSort).toBe(true);
    });

    it('getSettings returns current settings', () => {
        const renderer = new ParticleSystemRenderer(16, { enableLOD: true });
        const settings = renderer.getSettings();
        expect(settings.enableLOD).toBe(true);
    });

    it('clearCaches clears material and batch caches', () => {
        const renderer = new ParticleSystemRenderer(16);
        // Should not throw
        renderer.clearCaches();
    });
});