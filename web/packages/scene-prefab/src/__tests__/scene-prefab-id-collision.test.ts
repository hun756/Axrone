import { describe, expect, it } from 'vitest';
import {
    createScenePrefabState,
    validateScenePrefabState,
    applyScenePrefabOverrideOperations,
} from '../scene-prefab-operations';
import { ScenePrefabWorkflow, resolveScenePrefab } from '../scene-prefab-workflow';
import { ScenePrefabValidationError, ScenePrefabResolutionError } from '../errors';
import type {
    SceneActorSnapshot,
    SceneComponentSnapshot,
    ScenePrefabDefinition,
} from '../types';

// ─── Helpers ───────────────────────────────────────────────────────────────

const makeActor = (
    nodeId: string,
    name = 'Actor',
    parentNodeId: string | null = null,
    components: SceneComponentSnapshot[] = [],
): SceneActorSnapshot => ({
    nodeId,
    parentNodeId,
    name,
    layer: 0,
    tag: 'default',
    active: true,
    persistent: false,
    pooled: false,
    components,
});

const makeDefinition = (
    id: string,
    actors: SceneActorSnapshot[] = [makeActor('root', 'Root')],
): ScenePrefabDefinition => ({
    id,
    kind: 'prefab',
    actors,
});

// ---------------------------------------------------------------------------
// Duplicate nodeId detection in createScenePrefabState
// ---------------------------------------------------------------------------
describe('Prefab ID collision — createScenePrefabState', () => {
    it('throws ScenePrefabValidationError when two actors share the same nodeId', () => {
        const definition = makeDefinition('dup-prefab', [
            makeActor('shared-id', 'Actor-A'),
            makeActor('shared-id', 'Actor-B'),
        ]);

        expect(() => createScenePrefabState(definition)).toThrow(ScenePrefabValidationError);
    });

    it('includes the duplicate nodeId in the error message', () => {
        const definition = makeDefinition('dup-prefab', [
            makeActor('node-42', 'Actor-A'),
            makeActor('node-42', 'Actor-B'),
        ]);

        expect(() => createScenePrefabState(definition)).toThrowError(/node-42/);
    });

    it('includes the prefab id in the error message', () => {
        const definition = makeDefinition('my-prefab', [
            makeActor('dup', 'A'),
            makeActor('dup', 'B'),
        ]);

        expect(() => createScenePrefabState(definition)).toThrowError(/my-prefab/);
    });

    it('succeeds when all nodeIds are unique', () => {
        const definition = makeDefinition('ok-prefab', [
            makeActor('node-a', 'A'),
            makeActor('node-b', 'B'),
            makeActor('node-c', 'C'),
        ]);

        const state = createScenePrefabState(definition);
        expect(state.actorIndex.size).toBe(3);
    });
});

// ---------------------------------------------------------------------------
// Duplicate nodeId detection in validateScenePrefabState
// ---------------------------------------------------------------------------
describe('Prefab ID collision — validateScenePrefabState', () => {
    it('throws ScenePrefabValidationError on duplicate nodeIds', () => {
        const definition = makeDefinition('val-prefab', [
            makeActor('same-id', 'A'),
            makeActor('same-id', 'B'),
        ]);

        const state = {
            id: definition.id,
            actors: definition.actors.map((actor) => ({ ...actor, parentNodeId: actor.parentNodeId ?? null, components: [...actor.components] })),
            actorIndex: new Map(),
        };

        expect(() => validateScenePrefabState(state as any)).toThrow(ScenePrefabValidationError);
    });

    it('throws when actor references a missing parent nodeId', () => {
        const definition = makeDefinition('orphan-prefab', [
            makeActor('child', 'Child', 'missing-parent'),
        ]);

        const state = createScenePrefabState({ id: 'orphan-prefab', actors: [makeActor('root')] });
        // Manually add an actor with a missing parent to bypass createScenePrefabState's index check
        state.actors.push({
            nodeId: 'child',
            parentNodeId: 'missing-parent',
            name: 'Child',
            layer: 0,
            tag: 'default',
            active: true,
            persistent: false,
            pooled: false,
            components: [],
        });

        expect(() => validateScenePrefabState(state)).toThrow(ScenePrefabValidationError);
        expect(() => {
            try {
                validateScenePrefabState(state);
            } catch (e) {
                throw e;
            }
        }).toThrowError(/missing-parent/);
    });
});

// ---------------------------------------------------------------------------
// Workflow registration: duplicate prefabId behavior
// ---------------------------------------------------------------------------
describe('Prefab ID collision — workflow registration', () => {
    it('second registration with same prefabId silently overwrites the first', () => {
        const wf = new ScenePrefabWorkflow();
        const first = makeDefinition('shared-id', [makeActor('root', 'First')]);
        const second = makeDefinition('shared-id', [makeActor('root', 'Second')]);

        wf.register(first);
        expect(wf.getPrefab('shared-id')!.actors[0]!.name).toBe('First');

        wf.register(second);
        expect(wf.getPrefab('shared-id')!.actors[0]!.name).toBe('Second');
    });

    it('registerAll with duplicate ids keeps the last entry', () => {
        const wf = new ScenePrefabWorkflow();
        const defs = [
            makeDefinition('dup', [makeActor('root', 'A')]),
            makeDefinition('dup', [makeActor('root', 'B')]),
        ];

        wf.registerAll(defs);
        expect(wf.getPrefab('dup')!.actors[0]!.name).toBe('B');
    });

    it('constructor prefabs with duplicate ids keeps the last entry', () => {
        const wf = new ScenePrefabWorkflow({
            prefabs: [
                makeDefinition('dup', [makeActor('root', 'First')]),
                makeDefinition('dup', [makeActor('root', 'Last')]),
            ],
        });

        expect(wf.getPrefab('dup')!.actors[0]!.name).toBe('Last');
    });
});

// ---------------------------------------------------------------------------
// Resolving prefab reference with empty string prefabId
// ---------------------------------------------------------------------------
describe('Prefab ID collision — empty prefabId resolution', () => {
    it('throws ScenePrefabResolutionError when resolving empty-string prefabId', () => {
        const wf = new ScenePrefabWorkflow();
        expect(() => wf.resolvePrefab({ kind: 'registry', prefabId: '' }))
            .toThrow(ScenePrefabResolutionError);
    });

    it('throws ScenePrefabResolutionError via standalone resolveScenePrefab with empty id', () => {
        expect(() => resolveScenePrefab({ kind: 'registry', prefabId: '' }))
            .toThrow(ScenePrefabResolutionError);
    });

    it('empty-string prefabId does not match a registered prefab', () => {
        const wf = new ScenePrefabWorkflow({ prefabs: [makeDefinition('p1')] });
        expect(wf.getPrefab('')).toBeUndefined();
    });
});

// ---------------------------------------------------------------------------
// Nested prefab instances — scoped nodeId collision
// ---------------------------------------------------------------------------
describe('Prefab ID collision — nested instances', () => {
    it('scoped nodeIds prevent collision between two nested instances of the same prefab', () => {
        const child = makeDefinition('child-prefab', [
            makeActor('inner-root', 'InnerRoot'),
            makeActor('inner-child', 'InnerChild', 'inner-root'),
        ]);

        const parent: ScenePrefabDefinition = {
            id: 'parent',
            kind: 'prefab',
            actors: [makeActor('root', 'Root')],
            nested: [
                {
                    instanceId: 'inst-A',
                    reference: { kind: 'registry', prefabId: 'child-prefab' },
                    parentNodeId: 'root',
                },
                {
                    instanceId: 'inst-B',
                    reference: { kind: 'registry', prefabId: 'child-prefab' },
                    parentNodeId: 'root',
                },
            ],
        };

        const wf = new ScenePrefabWorkflow({ prefabs: [child, parent] });
        const result = wf.resolvePrefab({ kind: 'registry', prefabId: 'parent' });

        const nodeIds = result.definition.actors.map((a) => a.nodeId);
        // Each nested instance gets its own scoped nodeId — no collision
        expect(nodeIds).toContain('inst-A::inner-root');
        expect(nodeIds).toContain('inst-B::inner-root');
        expect(nodeIds).toContain('inst-A::inner-child');
        expect(nodeIds).toContain('inst-B::inner-child');

        // All nodeIds should be unique
        const uniqueIds = new Set(nodeIds);
        expect(uniqueIds.size).toBe(nodeIds.length);
    });

    it('nested instance with same instanceId causes nodeId collision', () => {
        const child = makeDefinition('child-prefab', [
            makeActor('inner-root', 'InnerRoot'),
        ]);

        const parent: ScenePrefabDefinition = {
            id: 'parent',
            kind: 'prefab',
            actors: [makeActor('root', 'Root')],
            nested: [
                {
                    instanceId: 'same-inst',
                    reference: { kind: 'registry', prefabId: 'child-prefab' },
                    parentNodeId: 'root',
                },
                {
                    instanceId: 'same-inst',
                    reference: { kind: 'registry', prefabId: 'child-prefab' },
                    parentNodeId: 'root',
                },
            ],
        };

        const wf = new ScenePrefabWorkflow({ prefabs: [child, parent] });
        // The second nested instance merges over the first (same scoped nodeId),
        // so it does NOT throw — mergeScenePrefabActors replaces on collision.
        const result = wf.resolvePrefab({ kind: 'registry', prefabId: 'parent' });
        expect(result.definition.kind).toBe('resolved');

        // Only one instance of the scoped nodeId should exist
        const scopedIds = result.definition.actors
            .map((a) => a.nodeId)
            .filter((id) => id.startsWith('same-inst::'));
        expect(scopedIds.length).toBe(1);
    });
});

// ---------------------------------------------------------------------------
// rebuildActorIndex duplicate guard via add-actor override
// ---------------------------------------------------------------------------
describe('Prefab ID collision — add-actor override duplicate guard', () => {
    it('throws when add-actor override uses an existing nodeId', () => {
        const definition = makeDefinition('guard-prefab', [
            makeActor('root', 'Root'),
        ]);

        const state = createScenePrefabState(definition);

        expect(() =>
            applyScenePrefabOverrideOperations(state, [
                {
                    kind: 'add-actor',
                    actor: makeActor('root', 'Duplicate'),
                },
            ]),
        ).toThrow(ScenePrefabValidationError);
    });

    it('error message mentions the conflicting nodeId for add-actor', () => {
        const definition = makeDefinition('guard-prefab', [
            makeActor('existing-node', 'Root'),
        ]);

        const state = createScenePrefabState(definition);

        expect(() =>
            applyScenePrefabOverrideOperations(state, [
                {
                    kind: 'add-actor',
                    actor: makeActor('existing-node', 'Duplicate'),
                },
            ]),
        ).toThrowError(/existing-node/);
    });

    it('succeeds when add-actor uses a unique nodeId', () => {
        const definition = makeDefinition('ok-prefab', [
            makeActor('root', 'Root'),
        ]);

        const state = createScenePrefabState(definition);

        expect(() =>
            applyScenePrefabOverrideOperations(state, [
                {
                    kind: 'add-actor',
                    actor: makeActor('new-node', 'NewActor'),
                },
            ]),
        ).not.toThrow();

        expect(state.actorIndex.size).toBe(2);
        expect(state.actorIndex.has('new-node')).toBe(true);
    });
});
