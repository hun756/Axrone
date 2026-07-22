import {
	Transform,
	World,
	getComponentPropertyMetadata,
} from '@axrone/ecs-runtime';
import { Vec3 } from '@axrone/numeric';
import { describe, expect, it } from 'vitest';
import { SceneComponentCatalog } from '../component-catalog';
import { createSceneRegistry } from '../scene-registry';
import { encodeSceneValue } from '../serialization';
import { SceneActorRuntime } from '../scene-actor-runtime';
import type { ScenePrefabDefinition } from '../types';
import { HydratedFollower } from './fixtures/HydratedFollower';
import { HydrationMarker } from './fixtures/HydrationMarker';
import { ComponentRefFollower } from './fixtures/ComponentRefFollower';

const createPrefabComponent = (type: string, data: unknown) => ({
	type,
	data: encodeSceneValue(data),
});

const createPrefabHarness = () => {
	const registry = createSceneRegistry({
		registry: {
			HydratedFollower,
			HydrationMarker,
			ComponentRefFollower,
		},
	});
	const world = new World(registry);
	const actors = new SceneActorRuntime({
		world,
		componentCatalog: new SceneComponentCatalog(registry),
	});

	actors.registerComponent(HydratedFollower);
	actors.registerComponent(HydrationMarker);
	actors.registerComponent(ComponentRefFollower);

	return {
		actors,
		world,
	};
};

const createHydrationPrefab = (): ScenePrefabDefinition => ({
	id: 'prefab/property-hydration',
	actors: [
		{
			nodeId: 'node/source',
			parentNodeId: null,
			name: 'Source',
			layer: 0,
			tag: 'default',
			active: true,
			persistent: false,
			pooled: false,
			components: [
				createPrefabComponent('Transform', {
					position: [0, 0, 0],
					rotation: [0, 0, 0, 1],
					scale: [1, 1, 1],
				}),
				createPrefabComponent('HydratedFollower', {
					scriptPath: 'Scripts/hydrated-follower.ts',
					className: 'HydratedFollower',
					scriptName: 'HydratedFollower',
					executeInEditMode: false,
					propertyValues: {
						targetActor: {
							kind: 'entity',
							target: 'node/target',
						},
						targetTransform: {
							kind: 'entity',
							target: 'node/target',
						},
						offset: {
							x: 1,
							y: 2,
							z: 3,
						},
						speed: '4.5',
						enabledFlag: true,
						tintHex: '#ff00aa',
					},
				}),
			],
		},
		{
			nodeId: 'node/target',
			parentNodeId: null,
			name: 'Target',
			layer: 0,
			tag: 'default',
			active: true,
			persistent: false,
			pooled: false,
			components: [
				createPrefabComponent('Transform', {
					position: [5, 1, 2],
					rotation: [0, 0, 0, 1],
					scale: [1, 1, 1],
				}),
			],
		},
	],
});

const createComponentRefPrefab = (): ScenePrefabDefinition => ({
	id: 'prefab/component-reference',
	actors: [
		{
			nodeId: 'node/follower',
			parentNodeId: null,
			name: 'Follower',
			layer: 0,
			tag: 'default',
			active: true,
			persistent: false,
			pooled: false,
			components: [
				createPrefabComponent('Transform', {
					position: [0, 0, 0],
					rotation: [0, 0, 0, 1],
					scale: [1, 1, 1],
				}),
				createPrefabComponent('ComponentRefFollower', {
					scriptPath: 'Scripts/component-ref-follower.ts',
					className: 'ComponentRefFollower',
					scriptName: 'ComponentRefFollower',
					executeInEditMode: false,
					propertyValues: {
						marker: {
							kind: 'entity',
							target: 'node/marker-owner',
						},
					},
				}),
			],
		},
		{
			nodeId: 'node/marker-owner',
			parentNodeId: null,
			name: 'MarkerOwner',
			layer: 0,
			tag: 'default',
			active: true,
			persistent: false,
			pooled: false,
			components: [
				createPrefabComponent('Transform', {
					position: [0, 0, 0],
					rotation: [0, 0, 0, 1],
					scale: [1, 1, 1],
				}),
				createPrefabComponent('HydrationMarker', {}),
			],
		},
	],
});

describe('ScenePrefabRuntime property hydration', () => {
	it('hydrates editor propertyValues into live script instances', () => {
		expect(getComponentPropertyMetadata(HydratedFollower).map((entry) => entry.propertyKey)).toEqual([
			'targetActor',
			'targetTransform',
			'offset',
			'speed',
			'enabledFlag',
			'tintHex',
		]);

		const harness = createPrefabHarness();
		const actors = harness.actors.instantiatePrefab(createHydrationPrefab());
		const sourceActor = actors.find((actor) => actor.name === 'Source');
		const targetActor = actors.find((actor) => actor.name === 'Target');
		const component = sourceActor?.getComponent(HydratedFollower);

		expect(sourceActor).toBeDefined();
		expect(targetActor).toBeDefined();
		expect(component).toBeDefined();
		expect(component?.offset).toBeInstanceOf(Vec3);
		expect([component?.offset.x, component?.offset.y, component?.offset.z]).toEqual([1, 2, 3]);
		expect(component?.speed).toBe(4.5);
		expect(component?.enabledFlag).toBe(true);
		expect(component?.tintHex).toBe('#ff00aa');
		expect(component?.targetActor).toBe(targetActor);
		expect(component?.targetTransform).toBe(targetActor?.getComponent(Transform));
	});

	it('resolves a component-typed property to the target component instance', () => {
		const harness = createPrefabHarness();
		const actors = harness.actors.instantiatePrefab(createComponentRefPrefab());
		const followerActor = actors.find((actor) => actor.name === 'Follower');
		const markerOwnerActor = actors.find((actor) => actor.name === 'MarkerOwner');
		const follower = followerActor?.getComponent(ComponentRefFollower);

		expect(follower).toBeDefined();
		expect(markerOwnerActor).toBeDefined();
		expect(follower?.marker).toBe(markerOwnerActor?.getComponent(HydrationMarker));
	});
});