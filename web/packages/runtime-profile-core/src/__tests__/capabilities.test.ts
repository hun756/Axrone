import { describe, expect, it } from 'vitest';
import { RUNTIME_PROFILE_CORE_CAPABILITY_PACKAGES } from '../capabilities';
import { INPUT_CAPABILITY_PACKAGE } from '@axrone/input';

describe('runtime-profile-core capabilities', () => {
    it('freezes the capability package list to prevent external mutation', () => {
        expect(Object.isFrozen(RUNTIME_PROFILE_CORE_CAPABILITY_PACKAGES)).toBe(true);
    });

    it('exposes the exact core capability seam of scene-runtime and input', () => {
        expect([...RUNTIME_PROFILE_CORE_CAPABILITY_PACKAGES]).toEqual([
            '@axrone/scene-runtime',
            '@axrone/input',
        ]);
    });

    it('contains no duplicate entries', () => {
        const unique = new Set(RUNTIME_PROFILE_CORE_CAPABILITY_PACKAGES);
        expect(unique.size).toBe(RUNTIME_PROFILE_CORE_CAPABILITY_PACKAGES.length);
    });

    it('has exactly two entries', () => {
        expect(RUNTIME_PROFILE_CORE_CAPABILITY_PACKAGES).toHaveLength(2);
    });

    it('resolves the INPUT_CAPABILITY_PACKAGE constant to the expected string', () => {
        expect(INPUT_CAPABILITY_PACKAGE).toBe('@axrone/input');
        expect(RUNTIME_PROFILE_CORE_CAPABILITY_PACKAGES).toContain(INPUT_CAPABILITY_PACKAGE);
    });
});
