import { describe, expect, it } from 'vitest';
import {
    mergeScenePrefabDefinitions,
    diffScenePrefabDefinitions,
} from '../scene-prefab-diff';
import type {
    ScenePrefabDefinition,
    ScenePrefabOverrideOperation,
    ScenePrefabMergeOptions,
    ScenePrefabConflict,
    SceneActorSnapshot,
} from '../types';

const makeActor = (
    nodeId: string,
    name: string,
    position: readonly [number, number, number],
    parentNodeId: string | null = null,
): SceneActorSnapshot => ({
    nodeId,
    parentNodeId,
    name,
    layer: 0,
    tag: 'default',
    active: true,
    persistent: false,
    pooled: false,
    components: [
        {
            type: 'Transform',
            data: { position: [...position], rotation: [0, 0, 0, 1], scale: [1, 1, 1] },
        },
    ],
});

const baseDefinition: ScenePrefabDefinition = {
    id: 'prefab/base',
    kind: 'prefab',
    actors: [
        makeActor('root', 'Root', [0, 0, 0]),
        makeActor('child', 'Child', [1, 0, 0], 'root'),
    ],
};

describe('scene-prefab conflict resolution', () => {
    describe('mergeScenePrefabDefinitions', () => {
        it('produces no conflicts when local and incoming changes are disjoint', () => {
            // Local changes root position, incoming changes child position
            const local: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/local',
                actors: [
                    makeActor('root', 'Root', [5, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };
            const incoming: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/incoming',
                actors: [
                    makeActor('root', 'Root', [0, 0, 0]),
                    makeActor('child', 'Child', [1, 10, 0], 'root'),
                ],
            };

            const result = mergeScenePrefabDefinitions(baseDefinition, local, incoming);
            expect(result.conflicts).toHaveLength(0);
            expect(result.resolved).toBe(true);
        });

        it('detects a conflict when local and incoming modify the same property', () => {
            // Both change root position.x to different values
            const local: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/local',
                actors: [
                    makeActor('root', 'Root', [5, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };
            const incoming: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/incoming',
                actors: [
                    makeActor('root', 'Root', [99, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };

            const result = mergeScenePrefabDefinitions(baseDefinition, local, incoming);
            expect(result.conflicts.length).toBeGreaterThanOrEqual(1);
            expect(result.resolved).toBe(false);
        });

        it('resolves conflicts with "prefer-local" policy', () => {
            const local: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/local',
                actors: [
                    makeActor('root', 'Root', [5, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };
            const incoming: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/incoming',
                actors: [
                    makeActor('root', 'Root', [99, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };

            const options: ScenePrefabMergeOptions = { conflictPolicy: 'prefer-local' };
            const result = mergeScenePrefabDefinitions(baseDefinition, local, incoming, options);
            expect(result.resolved).toBe(true);
            // prefer-local silently resolves; conflicts array may be empty
            // because resolved conflicts are not recorded.
        });

        it('resolves conflicts with "prefer-incoming" policy', () => {
            const local: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/local',
                actors: [
                    makeActor('root', 'Root', [5, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };
            const incoming: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/incoming',
                actors: [
                    makeActor('root', 'Root', [99, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };

            const options: ScenePrefabMergeOptions = { conflictPolicy: 'prefer-incoming' };
            const result = mergeScenePrefabDefinitions(baseDefinition, local, incoming, options);
            expect(result.resolved).toBe(true);
        });

        it('resolves conflicts with "prefer-base" policy', () => {
            const local: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/local',
                actors: [
                    makeActor('root', 'Root', [5, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };
            const incoming: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/incoming',
                actors: [
                    makeActor('root', 'Root', [99, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };

            const options: ScenePrefabMergeOptions = { conflictPolicy: 'prefer-base' };
            const result = mergeScenePrefabDefinitions(baseDefinition, local, incoming, options);
            expect(result.resolved).toBe(true);
        });

        it('uses custom conflict resolver when provided', () => {
            const local: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/local',
                actors: [
                    makeActor('root', 'Root', [5, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };
            const incoming: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/incoming',
                actors: [
                    makeActor('root', 'Root', [99, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };

            const resolverCalls: ScenePrefabConflict[] = [];
            const options: ScenePrefabMergeOptions = {
                conflictResolver: (conflict) => {
                    resolverCalls.push(conflict);
                    return 'incoming';
                },
            };
            const result = mergeScenePrefabDefinitions(baseDefinition, local, incoming, options);
            expect(resolverCalls.length).toBeGreaterThanOrEqual(1);
            expect(result.resolved).toBe(true);
        });

        it('"manual" policy reports conflicts without resolving them', () => {
            const local: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/local',
                actors: [
                    makeActor('root', 'Root', [5, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };
            const incoming: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/incoming',
                actors: [
                    makeActor('root', 'Root', [99, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };

            const options: ScenePrefabMergeOptions = { conflictPolicy: 'manual' };
            const result = mergeScenePrefabDefinitions(baseDefinition, local, incoming, options);
            expect(result.resolved).toBe(false);
            expect(result.conflicts.length).toBeGreaterThanOrEqual(1);
        });

        it('merge completes without throwing for add-actor vs remove-actor on same nodeId', () => {
            const local: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/local',
                actors: [makeActor('root', 'Root', [0, 0, 0])],
            };
            const incoming: ScenePrefabDefinition = {
                ...baseDefinition,
                id: 'prefab/incoming',
                actors: [
                    makeActor('root', 'Root', [0, 0, 0]),
                    makeActor('child', 'NewChild', [2, 0, 0], 'root'),
                ],
            };

            const result = mergeScenePrefabDefinitions(baseDefinition, local, incoming);
            expect(result).toBeDefined();
            expect(result.overrides).toBeDefined();
        });
    });

    describe('diffScenePrefabDefinitions', () => {
        it('produces an empty diff for identical definitions', () => {
            const result = diffScenePrefabDefinitions(baseDefinition, baseDefinition);
            expect(result.overrides).toHaveLength(0);
            expect(result.basePrefabId).toBe(baseDefinition.id);
            expect(result.targetPrefabId).toBe(baseDefinition.id);
        });

        it('detects added actors', () => {
            const target: ScenePrefabDefinition = {
                ...baseDefinition,
                actors: [
                    ...baseDefinition.actors,
                    makeActor('new-node', 'NewNode', [3, 0, 0], 'root'),
                ],
            };

            const result = diffScenePrefabDefinitions(baseDefinition, target);
            expect(result.overrides.length).toBeGreaterThanOrEqual(1);
            const addOps = result.overrides.filter((op) => op.kind === 'add-actor');
            expect(addOps.length).toBeGreaterThanOrEqual(1);
        });

        it('detects removed actors', () => {
            const target: ScenePrefabDefinition = {
                ...baseDefinition,
                actors: [baseDefinition.actors[0]!],
            };

            const result = diffScenePrefabDefinitions(baseDefinition, target);
            const removeOps = result.overrides.filter((op) => op.kind === 'remove-actor');
            expect(removeOps.length).toBeGreaterThanOrEqual(1);
        });

        it('detects property changes as set-component-property overrides', () => {
            const target: ScenePrefabDefinition = {
                ...baseDefinition,
                actors: [
                    makeActor('root', 'Root', [42, 0, 0]),
                    makeActor('child', 'Child', [1, 0, 0], 'root'),
                ],
            };

            const result = diffScenePrefabDefinitions(baseDefinition, target);
            const setOps = result.overrides.filter(
                (op) => op.kind === 'set-component-property',
            );
            expect(setOps.length).toBeGreaterThanOrEqual(1);
        });
    });
});
