import { describe, expect, it } from 'vitest';
import { Component } from '@axrone/ecs-runtime';
import {
	resolveSceneRegistryFromProfile,
	createSceneRuntimeProfile,
	getDefaultSceneRuntimeProfile,
	DEFAULT_SCENE_RUNTIME_PROFILE_ID,
} from '@axrone/scene-3d';

class CustomComponent extends Component {}

describe('resolveSceneRegistryFromProfile', () => {
	it('falls back to the default profile when profile is undefined', () => {
		const registry = resolveSceneRegistryFromProfile(undefined);

		expect(registry.Camera).toBeDefined();
		expect(registry.MeshRenderer).toBeDefined();
		expect(registry.Transform).toBeDefined();
		expect(registry.Hierarchy).toBeDefined();
	});

	it('falls back to the default profile with an explicit empty context', () => {
		const registry = resolveSceneRegistryFromProfile(undefined, {});

		expect(registry.Camera).toBeDefined();
		expect(registry.MeshRenderer).toBeDefined();
	});

	it('uses the custom profile resolveRegistry when a profile is provided', () => {
		const customProfile = createSceneRuntimeProfile({
			id: 'test/custom-registry-resolution',
			resolveRegistry: ({ registry }) => ({
				...(registry ?? {}),
				CustomComponent,
			}),
		});

		const registry = resolveSceneRegistryFromProfile(customProfile);

		expect(registry.CustomComponent).toBe(CustomComponent);
	});

	it('passes the context through to the custom profile resolveRegistry', () => {
		let receivedContext: unknown;
		const customProfile = createSceneRuntimeProfile({
			id: 'test/context-passthrough',
			resolveRegistry: (ctx) => {
				receivedContext = ctx;
				return { ...(ctx.registry ?? {}) };
			},
		});

		const context = {
			registry: { CustomComponent },
		};
		resolveSceneRegistryFromProfile(customProfile, context);

		expect(receivedContext).toBe(context);
	});

	it('returns the same default registry shape as getDefaultSceneRuntimeProfile', () => {
		const fallbackRegistry = resolveSceneRegistryFromProfile(undefined);
		const defaultRegistry = getDefaultSceneRuntimeProfile().resolveRegistry({});

		expect(Object.keys(fallbackRegistry).sort()).toEqual(Object.keys(defaultRegistry).sort());
	});
});
