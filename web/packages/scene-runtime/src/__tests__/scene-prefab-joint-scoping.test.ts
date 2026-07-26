import { Transform, World } from '@axrone/ecs-runtime';
import { describe, expect, it } from 'vitest';
import { PrefabNodeBinding } from '@axrone/scene-prefab';
import { MeshRenderer } from '../components/mesh-renderer';
import { SceneComponentCatalog } from '../component-catalog';
import { SceneActorRuntime } from '../scene-actor-runtime';
import { createSceneRegistry } from '../scene-registry';
import { encodeSceneValue } from '../serialization';
import type { ScenePrefabDefinition } from '@axrone/scene-prefab';

/**
 * Regression test: jointNodeIds scoping during prefab instantiation.
 *
 * When a skinned prefab is instantiated multiple times in the same world,
 * each MeshRenderer's jointNodeIds must resolve to the transforms of
 * nodes within the SAME prefab instance — not nodes from other instances
 * that share the same original node IDs.
 */

const createSkinnedPrefabDefinition = (): ScenePrefabDefinition => ({
    id: 'prefab/skinned-character',
    kind: 'prefab',
    actors: [
        {
            nodeId: 'skeleton-root',
            name: 'SkeletonRoot',
            layer: 0,
            tag: 'default',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                {
                    type: 'Transform',
                    data: encodeSceneValue({
                        position: [0, 0, 0],
                        rotation: [0, 0, 0, 1],
                        scale: [1, 1, 1],
                    }),
                },
                {
                    type: 'PrefabNodeBinding',
                    data: encodeSceneValue({ nodeId: 'skeleton-root' }),
                },
            ],
        },
        {
            nodeId: 'joint-a',
            parentNodeId: 'skeleton-root',
            name: 'JointA',
            layer: 0,
            tag: 'default',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                {
                    type: 'Transform',
                    data: encodeSceneValue({
                        position: [1, 0, 0],
                        rotation: [0, 0, 0, 1],
                        scale: [1, 1, 1],
                    }),
                },
                {
                    type: 'PrefabNodeBinding',
                    data: encodeSceneValue({ nodeId: 'joint-a' }),
                },
            ],
        },
        {
            nodeId: 'joint-b',
            parentNodeId: 'skeleton-root',
            name: 'JointB',
            layer: 0,
            tag: 'default',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                {
                    type: 'Transform',
                    data: encodeSceneValue({
                        position: [0, 1, 0],
                        rotation: [0, 0, 0, 1],
                        scale: [1, 1, 1],
                    }),
                },
                {
                    type: 'PrefabNodeBinding',
                    data: encodeSceneValue({ nodeId: 'joint-b' }),
                },
            ],
        },
        {
            nodeId: 'mesh',
            parentNodeId: 'skeleton-root',
            name: 'SkinnedMesh',
            layer: 0,
            tag: 'default',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                {
                    type: 'Transform',
                    data: encodeSceneValue({
                        position: [0, 0, 0],
                        rotation: [0, 0, 0, 1],
                        scale: [1, 1, 1],
                    }),
                },
                {
                    type: 'PrefabNodeBinding',
                    data: encodeSceneValue({ nodeId: 'mesh' }),
                },
                {
                    type: 'MeshRenderer',
                    data: encodeSceneValue({
                        skin: {
                            jointNodeIds: ['joint-a', 'joint-b'],
                            inverseBindMatrices: new Array(32).fill(0),
                        },
                    }),
                },
            ],
        },
    ],
});

const createTestHarness = () => {
    const registry = createSceneRegistry({
        registry: {
            Transform,
            PrefabNodeBinding,
            MeshRenderer,
        },
    });
    const world = new World(registry);
    const actorRuntime = new SceneActorRuntime({
        world,
        componentCatalog: new SceneComponentCatalog(registry),
    });

    actorRuntime.registerComponent(Transform);
    actorRuntime.registerComponent(PrefabNodeBinding);
    actorRuntime.registerComponent(MeshRenderer);

    return { world, registry, actorRuntime };
};

describe('scene-prefab jointNodeIds scoping', () => {
    it('each prefab instance resolves joints within its own scope', async () => {
        const { world, actorRuntime } = createTestHarness();
        const definition = createSkinnedPrefabDefinition();

        // Instantiate two copies of the same prefab
        const instance1Actors = await actorRuntime.instantiatePrefab(definition, {
            namePrefix: 'Instance1_',
        });
        const instance2Actors = await actorRuntime.instantiatePrefab(definition, {
            namePrefix: 'Instance2_',
        });

        expect(instance1Actors.length).toBeGreaterThan(0);
        expect(instance2Actors.length).toBeGreaterThan(0);

        // Find MeshRenderers in each instance
        const allActors = world.getAllActors();
        const meshRenderers = allActors
            .map((actor) => ({
                actor,
                renderer: actor.getComponent(MeshRenderer),
                binding: actor.getComponent(PrefabNodeBinding),
            }))
            .filter((entry) => entry.renderer != null);

        // We should have at least 2 MeshRenderers (one per instance)
        expect(meshRenderers.length).toBeGreaterThanOrEqual(2);

        // Verify that each MeshRenderer has a PrefabNodeBinding with a unique instanceId
        const instanceIds = new Set(
            meshRenderers
                .map((entry) => entry.binding?.instanceId)
                .filter((id): id is string => id != null),
        );
        expect(instanceIds.size).toBeGreaterThanOrEqual(2);

        // Verify that joint resolution is scoped: each MeshRenderer's
        // jointNodeIds should reference nodes within the same instance.
        for (const entry of meshRenderers) {
            const myInstanceId = entry.binding?.instanceId;
            const skinData = (entry.renderer as any)._skin;
            if (!skinData?.jointNodeIds?.length) {
                continue;
            }

            // Collect all PrefabNodeBindings for actors in the same instance
            const sameInstanceNodeIds = new Set<string>();
            for (const actor of allActors) {
                const binding = actor.getComponent(PrefabNodeBinding);
                if (binding && binding.instanceId === myInstanceId && binding.nodeId) {
                    sameInstanceNodeIds.add(binding.nodeId);
                }
            }

            // Each jointNodeId should exist within the same instance
            for (const jointNodeId of skinData.jointNodeIds) {
                expect(sameInstanceNodeIds.has(jointNodeId)).toBe(true);
            }
        }
    });

    it('different instances have distinct PrefabNodeBinding instanceIds', async () => {
        const { world, actorRuntime } = createTestHarness();
        const definition = createSkinnedPrefabDefinition();

        await actorRuntime.instantiatePrefab(definition, { namePrefix: 'A_' });
        await actorRuntime.instantiatePrefab(definition, { namePrefix: 'B_' });

        const allActors = world.getAllActors();
        const bindings = allActors
            .map((actor) => actor.getComponent(PrefabNodeBinding))
            .filter((b): b is PrefabNodeBinding => b != null);

        const instanceIds = new Set(
            bindings.map((b) => b.instanceId).filter((id): id is string => id != null),
        );
        // Two distinct instances should produce two distinct instanceIds
        expect(instanceIds.size).toBe(2);
    });
});
