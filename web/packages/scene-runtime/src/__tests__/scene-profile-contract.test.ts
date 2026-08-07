import { describe, expect, it } from 'vitest';
import {
    createSceneRuntimeProfile,
    resolveSceneRegistryFromProfileWithFallback,
} from '../scene-profile-contract';

describe('createSceneRuntimeProfile', () => {
    it('returns the profile object as-is', () => {
        const profile = createSceneRuntimeProfile({
            id: 'test-profile',
            resolveRegistry: () => ({}) as any,
        });
        expect(profile.id).toBe('test-profile');
        expect(typeof profile.resolveRegistry).toBe('function');
    });
});

describe('resolveSceneRegistryFromProfileWithFallback', () => {
    it('uses the provided profile when defined', () => {
        const registry = { TestComponent: class {} as any };
        const profile = createSceneRuntimeProfile({
            id: 'custom',
            resolveRegistry: () => registry,
        });

        const result = resolveSceneRegistryFromProfileWithFallback(profile, {
            id: 'fallback',
            resolveRegistry: () => ({}) as any,
        });

        expect(result).toBe(registry);
    });

    it('falls back to default profile when profile is undefined', () => {
        const fallbackRegistry = { FallbackComponent: class {} as any };
        const fallbackProfile = createSceneRuntimeProfile({
            id: 'fallback',
            resolveRegistry: () => fallbackRegistry,
        });

        const result = resolveSceneRegistryFromProfileWithFallback(undefined, fallbackProfile);
        expect(result).toBe(fallbackRegistry);
    });

    it('passes context to the profile resolveRegistry', () => {
        const context = { registry: { MyComp: class {} as any } };
        let receivedContext: any = null;

        const profile = createSceneRuntimeProfile({
            id: 'context-test',
            resolveRegistry: (ctx) => {
                receivedContext = ctx;
                return {} as any;
            },
        });

        resolveSceneRegistryFromProfileWithFallback(profile, {
            id: 'fallback',
            resolveRegistry: () => ({}) as any,
        }, context);

        expect(receivedContext).toBe(context);
    });

    it('uses empty context by default', () => {
        let receivedContext: any = null;

        const profile = createSceneRuntimeProfile({
            id: 'no-context',
            resolveRegistry: (ctx) => {
                receivedContext = ctx;
                return {} as any;
            },
        });

        resolveSceneRegistryFromProfileWithFallback(profile, {
            id: 'fallback',
            resolveRegistry: () => ({}) as any,
        });

        expect(receivedContext).toEqual({});
    });
});
