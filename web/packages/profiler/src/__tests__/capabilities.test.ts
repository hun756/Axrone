import { describe, it, expect } from 'vitest';
import { PROFILER_CAPABILITY_PACKAGES } from '../capabilities';

describe('capabilities', () => {
    it('should export a frozen array', () => {
        expect(Object.isFrozen(PROFILER_CAPABILITY_PACKAGES)).toBe(true);
    });

    it('should contain @axrone/scene-runtime', () => {
        expect(PROFILER_CAPABILITY_PACKAGES).toContain('@axrone/scene-runtime');
    });

    it('should be read-only', () => {
        expect(() => {
            (PROFILER_CAPABILITY_PACKAGES as string[]).push('test');
        }).toThrow();
    });
});
