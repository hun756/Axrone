import { describe, it, expect } from 'vitest';
import { ParticleSystem } from '../particle-system';
import { ParticleSystemRenderer } from '../particle-renderer';

describe('ParticleSystemRenderer attribute helpers', () => {
    it('extracts positions, colors, sizes and indices for active particles', () => {
        const sys = new ParticleSystem({ maxParticles: 8 });
        sys.emit(3);

        const particles = sys.getParticles() as any;
        const renderer = new ParticleSystemRenderer(8);

        const pos = renderer.getPositionArray(particles);
        const colors = renderer.getColorArray(particles);
        const sizes = renderer.getSizeArray(particles);
        const indices = renderer.getIndexArray(particles);

        // 3 active particles -> lengths
        expect(pos.length).toBe(3 * 3);
        expect(colors.length).toBe(3 * 4);
        expect(sizes.length).toBe(3);
        expect(indices.length).toBe(3);

        // Basic content checks
        expect(indices[0]).toBeGreaterThanOrEqual(0);
        expect(sizes[0]).toBeGreaterThan(0);
        expect(colors[0]).toBeGreaterThanOrEqual(0);
    });
});
