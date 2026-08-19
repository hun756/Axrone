import { describe, expect, it } from 'vitest';
import {
    ScenePrefabWorkflow,
    applyScenePrefabOverrides,
    createScenePrefabScopedNodeId,
    PrefabNodeBinding,
    type SceneActorSnapshot,
    type SceneComponentSnapshot,
    type ScenePrefabDefinition,
    type ScenePrefabNestedInstance,
    type ScenePrefabOverrideOperation,
} from '../index';
import { scopeScenePrefabActors } from '../scene-prefab-operations';

// ─── Helpers ───────────────────────────────────────────────────────────────

function makeComponent(
    type: string,
    data: Record<string, unknown> = {},
    id?: string,
): SceneComponentSnapshot {
    return {
        ...(id ? { id } : {}),
        type,
        data,
    };
}

function makeActor(
    nodeId: string,
    name?: string,
    parentNodeId?: string | null,
    components?: SceneComponentSnapshot[],
): SceneActorSnapshot {
    return {
        nodeId,
        parentNodeId: parentNodeId ?? null,
        name: name ?? nodeId,
        layer: 0,
        tag: 'default',
        active: true,
        persistent: false,
        pooled: false,
        components: components ?? [],
    };
}

function makeDefinition(
    id: string,
    actors?: SceneActorSnapshot[],
    overrides?: ScenePrefabOverrideOperation[],
    nested?: ScenePrefabNestedInstance[],
    base?: ScenePrefabDefinition['base'],
): ScenePrefabDefinition {
    return {
        id,
        kind: 'prefab',
        actors: actors ?? [makeActor('root', 'Root')],
        ...(overrides ? { overrides } : {}),
        ...(nested ? { nested } : {}),
        ...(base ? { base } : {}),
    };
}

/**
 * Simulates instantiating a prefab by resolving it and then scoping its actors
 * with a unique instanceId, mirroring what the runtime instantiation pipeline does.
 */
function simulateInstantiation(
    workflow: ScenePrefabWorkflow,
    prefabId: string,
    instanceId: string,
    namePrefix?: string,
): readonly SceneActorSnapshot[] {
    const result = workflow.resolvePrefab({ kind: 'registry', prefabId });
    const nested: ScenePrefabNestedInstance = {
        instanceId,
        reference: { kind: 'registry', prefabId },
        ...(namePrefix ? { namePrefix } : {}),
    };
    return scopeScenePrefabActors(result.definition.actors, nested, prefabId);
}

// ---------------------------------------------------------------------------
// T-02: Prefab Instantiation Integration Tests
// ---------------------------------------------------------------------------

describe('T-02: Prefab Instantiation Integration', () => {
    // ── 1. Load a prefab definition → verify structure is valid ──────────
    describe('prefab definition loading', () => {
        it('loads a valid prefab definition with correct structure', () => {
            const def = makeDefinition('enemy-prefab', [
                makeActor('body', 'EnemyBody', null, [
                    makeComponent('Transform', { x: 0, y: 0, z: 0 }),
                    makeComponent('MeshRenderer', { mesh: 'enemy.mesh' }),
                ]),
                makeActor('weapon', 'Weapon', 'body', [
                    makeComponent('Transform', { x: 1, y: 0, z: 0 }),
                ]),
            ]);

            expect(def.id).toBe('enemy-prefab');
            expect(def.kind).toBe('prefab');
            expect(def.actors).toHaveLength(2);
            expect(def.actors[0]!.nodeId).toBe('body');
            expect(def.actors[0]!.components).toHaveLength(2);
            expect(def.actors[1]!.parentNodeId).toBe('body');
        });
    });

    // ── 2. Resolve a prefab from registry → verify lineage ───────────────
    describe('prefab resolution and lineage', () => {
        it('resolves a simple prefab with single-entry lineage', () => {
            const def = makeDefinition('simple-prefab');
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });

            const result = workflow.resolvePrefab({ kind: 'registry', prefabId: 'simple-prefab' });

            expect(result.definition.kind).toBe('resolved');
            expect(result.definition.lineage).toEqual(['simple-prefab']);
            expect(result.definition.actors).toHaveLength(1);
            expect(result.conflicts).toEqual([]);
        });

        it('resolves a variant chain with multi-entry lineage', () => {
            const base = makeDefinition('base-prefab', [
                makeActor('root', 'BaseRoot', null, [
                    makeComponent('Transform', { x: 0 }),
                ]),
            ]);
            const variant: ScenePrefabDefinition = {
                id: 'variant-prefab',
                kind: 'variant',
                actors: [makeActor('root', 'VariantRoot')],
                base: { kind: 'registry', prefabId: 'base-prefab' },
            };

            const workflow = new ScenePrefabWorkflow({ prefabs: [base, variant] });
            const result = workflow.resolvePrefab({ kind: 'registry', prefabId: 'variant-prefab' });

            expect(result.definition.lineage).toEqual(['base-prefab', 'variant-prefab']);
            expect(result.definition.actors[0]!.name).toBe('VariantRoot');
        });
    });

    // ── 3. Instantiate a simple prefab → verify actor count ──────────────
    describe('prefab instantiation — actor count', () => {
        it('creates the correct number of scoped actors from a simple prefab', () => {
            const def = makeDefinition('tree-prefab', [
                makeActor('trunk', 'Trunk'),
                makeActor('branch-1', 'Branch1', 'trunk'),
                makeActor('branch-2', 'Branch2', 'trunk'),
                makeActor('leaves', 'Leaves', 'trunk'),
            ]);
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });

            const actors = simulateInstantiation(workflow, 'tree-prefab', 'instance-001');

            expect(actors).toHaveLength(4);
        });

        it('creates correct number of actors from a prefab with nested instances', () => {
            const inner = makeDefinition('inner-prefab', [
                makeActor('inner-root', 'InnerRoot'),
                makeActor('inner-child', 'InnerChild', 'inner-root'),
            ]);
            const outer: ScenePrefabDefinition = {
                id: 'outer-prefab',
                kind: 'prefab',
                actors: [makeActor('outer-root', 'OuterRoot')],
                nested: [
                    {
                        instanceId: 'nested-1',
                        reference: { kind: 'registry', prefabId: 'inner-prefab' },
                        parentNodeId: 'outer-root',
                    },
                ],
            };

            const workflow = new ScenePrefabWorkflow({ prefabs: [inner, outer] });
            const result = workflow.resolvePrefab({ kind: 'registry', prefabId: 'outer-prefab' });

            // outer-root + 2 inner actors (scoped)
            expect(result.definition.actors).toHaveLength(3);
        });
    });

    // ── 4. Verify entity hierarchy preservation ──────────────────────────
    describe('hierarchy preservation', () => {
        it('preserves parent-child relationships after instantiation', () => {
            const def = makeDefinition('hierarchy-prefab', [
                makeActor('parent', 'Parent'),
                makeActor('child-a', 'ChildA', 'parent'),
                makeActor('child-b', 'ChildB', 'parent'),
                makeActor('grandchild', 'Grandchild', 'child-a'),
            ]);
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });

            const actors = simulateInstantiation(workflow, 'hierarchy-prefab', 'inst-h');

            const parentActor = actors.find((a) => a.nodeId === 'inst-h::parent');
            const childA = actors.find((a) => a.nodeId === 'inst-h::child-a');
            const childB = actors.find((a) => a.nodeId === 'inst-h::child-b');
            const grandchild = actors.find((a) => a.nodeId === 'inst-h::grandchild');

            expect(parentActor!.parentNodeId).toBeNull();
            expect(childA!.parentNodeId).toBe('inst-h::parent');
            expect(childB!.parentNodeId).toBe('inst-h::parent');
            expect(grandchild!.parentNodeId).toBe('inst-h::child-a');
        });

        it('roots nested instance actors under the specified parent', () => {
            const inner = makeDefinition('inner', [
                makeActor('inner-root', 'InnerRoot'),
            ]);
            const outer: ScenePrefabDefinition = {
                id: 'outer',
                kind: 'prefab',
                actors: [makeActor('outer-root', 'OuterRoot')],
                nested: [
                    {
                        instanceId: 'n1',
                        reference: { kind: 'registry', prefabId: 'inner' },
                        parentNodeId: 'outer-root',
                    },
                ],
            };

            const workflow = new ScenePrefabWorkflow({ prefabs: [inner, outer] });
            const result = workflow.resolvePrefab({ kind: 'registry', prefabId: 'outer' });

            const nestedRoot = result.definition.actors.find((a) => a.nodeId === 'n1::inner-root');
            expect(nestedRoot).toBeDefined();
            expect(nestedRoot!.parentNodeId).toBe('outer-root');
        });
    });

    // ── 5. Verify component data is cloned ───────────────────────────────
    describe('component data cloning', () => {
        it('clones component data from prefab to resolved instance', () => {
            const def = makeDefinition('comp-prefab', [
                makeActor('root', 'Root', null, [
                    makeComponent('Transform', { position: { x: 10, y: 20, z: 30 } }),
                    makeComponent('Health', { hp: 100, maxHp: 100 }),
                ]),
            ]);
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });
            const result = workflow.resolvePrefab({ kind: 'registry', prefabId: 'comp-prefab' });

            const actor = result.definition.actors[0]!;
            expect(actor.components).toHaveLength(2);
            expect(actor.components[0]!.type).toBe('Transform');
            expect(actor.components[0]!.data).toEqual({ position: { x: 10, y: 20, z: 30 } });
            expect(actor.components[1]!.type).toBe('Health');
            expect(actor.components[1]!.data).toEqual({ hp: 100, maxHp: 100 });
        });

        it('resolved component data is independent from the original definition input', () => {
            const originalData = { position: { x: 5, y: 10 } };
            const def = makeDefinition('clone-test', [
                makeActor('root', 'Root', null, [makeComponent('Transform', originalData)]),
            ]);
            // Disable cache so each resolve produces a fresh clone
            const workflow = new ScenePrefabWorkflow({ prefabs: [def], enableCache: false });

            const result1 = workflow.resolvePrefab({ kind: 'registry', prefabId: 'clone-test' });
            const result2 = workflow.resolvePrefab({ kind: 'registry', prefabId: 'clone-test' });

            // Both should have equal data
            expect(result1.definition.actors[0]!.components[0]!.data).toEqual(
                result2.definition.actors[0]!.components[0]!.data,
            );
            // But not be the same reference (each resolution deep-clones)
            expect(result1.definition.actors[0]!.components[0]!.data).not.toBe(
                result2.definition.actors[0]!.components[0]!.data,
            );
            // And neither should be the original input reference
            expect(result1.definition.actors[0]!.components[0]!.data).not.toBe(originalData);
        });
    });

    // ── 6. Verify ID remapping — unique nodeIds across instances ─────────
    describe('ID remapping and uniqueness', () => {
        it('generates unique scoped nodeIds for each instance', () => {
            const def = makeDefinition('shared-prefab', [
                makeActor('root', 'Root'),
                makeActor('child', 'Child', 'root'),
            ]);
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });

            const instance1 = simulateInstantiation(workflow, 'shared-prefab', 'inst-A');
            const instance2 = simulateInstantiation(workflow, 'shared-prefab', 'inst-B');

            const ids1 = new Set(instance1.map((a) => a.nodeId));
            const ids2 = new Set(instance2.map((a) => a.nodeId));

            // No overlap between instances
            for (const id of ids1) {
                expect(ids2.has(id)).toBe(false);
            }

            // Verify the scoped format
            expect(ids1.has('inst-A::root')).toBe(true);
            expect(ids1.has('inst-A::child')).toBe(true);
            expect(ids2.has('inst-B::root')).toBe(true);
            expect(ids2.has('inst-B::child')).toBe(true);
        });

        it('createScenePrefabScopedNodeId produces correct format', () => {
            expect(createScenePrefabScopedNodeId('inst-1', 'node-a')).toBe('inst-1::node-a');
            expect(createScenePrefabScopedNodeId('abc', 'xyz')).toBe('abc::xyz');
        });

        it('three instances of the same prefab all have distinct IDs', () => {
            const def = makeDefinition('triple', [
                makeActor('a', 'A'),
                makeActor('b', 'B', 'a'),
            ]);
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });

            const i1 = simulateInstantiation(workflow, 'triple', 'x');
            const i2 = simulateInstantiation(workflow, 'triple', 'y');
            const i3 = simulateInstantiation(workflow, 'triple', 'z');

            const allIds = [
                ...i1.map((a) => a.nodeId),
                ...i2.map((a) => a.nodeId),
                ...i3.map((a) => a.nodeId),
            ];
            const uniqueIds = new Set(allIds);

            expect(uniqueIds.size).toBe(allIds.length);
        });
    });

    // ── 7. PrefabNodeBinding maps entities back to source nodes ──────────
    describe('PrefabNodeBinding', () => {
        it('stores nodeId and instanceId for entity-to-prefab mapping', () => {
            const binding = new PrefabNodeBinding({
                nodeId: 'root',
                instanceId: 'inst-42',
            });

            expect(binding.nodeId).toBe('root');
            expect(binding.instanceId).toBe('inst-42');
        });

        it('defaults to null when no config provided', () => {
            const binding = new PrefabNodeBinding();

            expect(binding.nodeId).toBeNull();
            expect(binding.instanceId).toBeNull();
        });

        it('serializes nodeId for persistence', () => {
            const binding = new PrefabNodeBinding({ nodeId: 'body', instanceId: 'inst-1' });
            const serialized = binding.serialize();

            expect(serialized.nodeId).toBe('body');
        });

        it('deserializes nodeId and instanceId from data', () => {
            const binding = new PrefabNodeBinding();
            binding.deserialize({ nodeId: 'weapon', instanceId: 'inst-99' });

            expect(binding.nodeId).toBe('weapon');
            expect(binding.instanceId).toBe('inst-99');
        });

        it('resolved actors carry source metadata linking back to prefab', () => {
            const def = makeDefinition('source-track', [
                makeActor('root', 'Root'),
            ]);
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });
            const result = workflow.resolvePrefab({ kind: 'registry', prefabId: 'source-track' });

            const actor = result.definition.actors[0]!;
            expect(actor.source).toBeDefined();
            expect(actor.source!.prefabId).toBe('source-track');
            expect(actor.source!.nodeId).toBe('root');
        });

        it('scoped actors carry instancePath in source metadata', () => {
            const def = makeDefinition('tracked-prefab', [
                makeActor('root', 'Root'),
            ]);
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });

            const actors = simulateInstantiation(workflow, 'tracked-prefab', 'inst-77');
            const actor = actors[0]!;

            expect(actor.source).toBeDefined();
            expect(actor.source!.prefabId).toBe('tracked-prefab');
            expect(actor.source!.nodeId).toBe('root');
            expect(actor.source!.instancePath).toContain('inst-77');
        });
    });

    // ── 8. Override application ──────────────────────────────────────────
    describe('override application', () => {
        it('applies set-actor-field override to change name', () => {
            const def = makeDefinition('override-name', [
                makeActor('root', 'OriginalName'),
            ]);

            const result = applyScenePrefabOverrides(def, [
                { kind: 'set-actor-field', nodeId: 'root', field: 'name', value: 'Renamed' },
            ]);

            expect(result.actors[0]!.name).toBe('Renamed');
        });

        it('applies set-component-property override to change position', () => {
            const def = makeDefinition('override-pos', [
                makeActor('root', 'Root', null, [
                    makeComponent('Transform', { x: 0, y: 0, z: 0 }, 'Transform'),
                ]),
            ]);

            const result = applyScenePrefabOverrides(def, [
                {
                    kind: 'set-component-property',
                    nodeId: 'root',
                    selector: { kind: 'id', componentId: 'Transform' },
                    path: ['x'],
                    value: 42,
                },
            ]);

            const transformData = result.actors[0]!.components[0]!.data as Record<string, unknown>;
            expect(transformData.x).toBe(42);
            // y and z remain unchanged
            expect(transformData.y).toBe(0);
            expect(transformData.z).toBe(0);
        });

        it('applies add-component override', () => {
            const def = makeDefinition('override-add-comp', [
                makeActor('root', 'Root'),
            ]);

            const result = applyScenePrefabOverrides(def, [
                {
                    kind: 'add-component',
                    nodeId: 'root',
                    component: makeComponent('Rigidbody', { mass: 5 }),
                },
            ]);

            expect(result.actors[0]!.components).toHaveLength(1);
            expect(result.actors[0]!.components[0]!.type).toBe('Rigidbody');
        });

        it('applies multiple overrides in sequence', () => {
            const def = makeDefinition('multi-override', [
                makeActor('root', 'Root', null, [
                    makeComponent('Transform', { x: 0 }, 'Transform'),
                ]),
                makeActor('child', 'Child', 'root'),
            ]);

            const result = applyScenePrefabOverrides(def, [
                { kind: 'set-actor-field', nodeId: 'root', field: 'name', value: 'Parent' },
                { kind: 'set-actor-field', nodeId: 'child', field: 'layer', value: 5 },
                {
                    kind: 'set-component-property',
                    nodeId: 'root',
                    selector: { kind: 'id', componentId: 'Transform' },
                    path: ['x'],
                    value: 100,
                },
            ]);

            expect(result.actors[0]!.name).toBe('Parent');
            expect(result.actors[1]!.layer).toBe(5);
            expect((result.actors[0]!.components[0]!.data as Record<string, unknown>).x).toBe(100);
        });

        it('workflow applyOverrides resolves and applies in one step', () => {
            const def = makeDefinition('wf-override', [
                makeActor('root', 'Original'),
            ]);
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });

            const result = workflow.applyOverrides(
                { kind: 'registry', prefabId: 'wf-override' },
                [{ kind: 'set-actor-field', nodeId: 'root', field: 'name', value: 'Modified' }],
            );

            expect(result.kind).toBe('resolved');
            expect(result.actors[0]!.name).toBe('Modified');
        });
    });

    // ── 9. Multiple instances coexist without interference ───────────────
    describe('multiple instance coexistence', () => {
        it('multiple instances of the same prefab coexist without interference', () => {
            const def = makeDefinition('coexist-prefab', [
                makeActor('root', 'Root', null, [
                    makeComponent('Transform', { x: 0, y: 0 }),
                ]),
                makeActor('arm', 'Arm', 'root'),
            ]);
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });

            const inst1 = simulateInstantiation(workflow, 'coexist-prefab', 'enemy-1');
            const inst2 = simulateInstantiation(workflow, 'coexist-prefab', 'enemy-2');
            const inst3 = simulateInstantiation(workflow, 'coexist-prefab', 'enemy-3');

            // All three instances have the expected number of actors
            expect(inst1).toHaveLength(2);
            expect(inst2).toHaveLength(2);
            expect(inst3).toHaveLength(2);

            // All nodeIds across all instances are unique
            const allIds = [
                ...inst1.map((a) => a.nodeId),
                ...inst2.map((a) => a.nodeId),
                ...inst3.map((a) => a.nodeId),
            ];
            expect(new Set(allIds).size).toBe(allIds.length);

            // Parent-child relationships are scoped per instance
            for (const inst of [inst1, inst2, inst3]) {
                const root = inst.find((a) => a.name === 'Root')!;
                const arm = inst.find((a) => a.name === 'Arm')!;
                expect(root.parentNodeId).toBeNull();
                expect(arm.parentNodeId).toBe(root.nodeId);
            }
        });

        it('applying overrides to one resolved instance does not affect another', () => {
            const def = makeDefinition('isolated', [
                makeActor('root', 'Base', null, [
                    makeComponent('Transform', { x: 0 }, 'Transform'),
                ]),
            ]);
            const workflow = new ScenePrefabWorkflow({ prefabs: [def] });

            const overridden = workflow.applyOverrides(
                { kind: 'registry', prefabId: 'isolated' },
                [
                    {
                        kind: 'set-component-property',
                        nodeId: 'root',
                        selector: { kind: 'id', componentId: 'Transform' },
                        path: ['x'],
                        value: 999,
                    },
                ],
            );

            const fresh = workflow.resolvePrefab({ kind: 'registry', prefabId: 'isolated' });

            // Overridden instance has the new value
            expect((overridden.actors[0]!.components[0]!.data as Record<string, unknown>).x).toBe(999);
            // Fresh resolution still has the original value
            expect((fresh.definition.actors[0]!.components[0]!.data as Record<string, unknown>).x).toBe(0);
        });
    });

    // ── 10. Nested prefab references resolve correctly ───────────────────
    describe('nested prefab resolution', () => {
        it('resolves deeply nested prefab references', () => {
            const leaf = makeDefinition('leaf', [
                makeActor('leaf-node', 'Leaf'),
            ]);
            const branch: ScenePrefabDefinition = {
                id: 'branch',
                kind: 'prefab',
                actors: [makeActor('branch-root', 'BranchRoot')],
                nested: [
                    {
                        instanceId: 'leaf-inst',
                        reference: { kind: 'registry', prefabId: 'leaf' },
                        parentNodeId: 'branch-root',
                    },
                ],
            };
            const tree: ScenePrefabDefinition = {
                id: 'tree',
                kind: 'prefab',
                actors: [makeActor('tree-root', 'TreeRoot')],
                nested: [
                    {
                        instanceId: 'branch-inst',
                        reference: { kind: 'registry', prefabId: 'branch' },
                        parentNodeId: 'tree-root',
                    },
                ],
            };

            const workflow = new ScenePrefabWorkflow({ prefabs: [leaf, branch, tree] });
            const result = workflow.resolvePrefab({ kind: 'registry', prefabId: 'tree' });

            // tree-root + branch-root (scoped) + leaf-node (double-scoped)
            expect(result.definition.actors).toHaveLength(3);

            const treeRoot = result.definition.actors.find((a) => a.nodeId === 'tree-root');
            const branchRoot = result.definition.actors.find((a) => a.nodeId === 'branch-inst::branch-root');
            const leafNode = result.definition.actors.find((a) => a.nodeId === 'branch-inst::leaf-inst::leaf-node');

            expect(treeRoot).toBeDefined();
            expect(branchRoot).toBeDefined();
            expect(leafNode).toBeDefined();

            // Hierarchy chain
            expect(treeRoot!.parentNodeId).toBeNull();
            expect(branchRoot!.parentNodeId).toBe('tree-root');
            expect(leafNode!.parentNodeId).toBe('branch-inst::branch-root');
        });

        it('nested instances with namePrefix apply prefix to actor names', () => {
            const inner = makeDefinition('inner-name', [
                makeActor('root', 'InnerRoot'),
                makeActor('child', 'InnerChild', 'root'),
            ]);
            const outer: ScenePrefabDefinition = {
                id: 'outer-name',
                kind: 'prefab',
                actors: [makeActor('root', 'OuterRoot')],
                nested: [
                    {
                        instanceId: 'n1',
                        reference: { kind: 'registry', prefabId: 'inner-name' },
                        parentNodeId: 'root',
                        namePrefix: '[Nested] ',
                    },
                ],
            };

            const workflow = new ScenePrefabWorkflow({ prefabs: [inner, outer] });
            const result = workflow.resolvePrefab({ kind: 'registry', prefabId: 'outer-name' });

            const nestedRoot = result.definition.actors.find((a) => a.nodeId === 'n1::root');
            const nestedChild = result.definition.actors.find((a) => a.nodeId === 'n1::child');

            expect(nestedRoot!.name).toBe('[Nested] InnerRoot');
            expect(nestedChild!.name).toBe('[Nested] InnerChild');
        });

        it('nested instances with overrides apply them to scoped actors', () => {
            const inner = makeDefinition('inner-ov', [
                makeActor('root', 'OriginalName', null, [
                    makeComponent('Transform', { x: 0 }, 'Transform'),
                ]),
            ]);
            const outer: ScenePrefabDefinition = {
                id: 'outer-ov',
                kind: 'prefab',
                actors: [makeActor('root', 'OuterRoot')],
                nested: [
                    {
                        instanceId: 'n1',
                        reference: { kind: 'registry', prefabId: 'inner-ov' },
                        parentNodeId: 'root',
                        overrides: [
                            {
                                kind: 'set-component-property',
                                nodeId: 'root',
                                selector: { kind: 'id', componentId: 'Transform' },
                                path: ['x'],
                                value: 77,
                            },
                        ],
                    },
                ],
            };

            const workflow = new ScenePrefabWorkflow({ prefabs: [inner, outer] });
            const result = workflow.resolvePrefab({ kind: 'registry', prefabId: 'outer-ov' });

            const nestedActor = result.definition.actors.find((a) => a.nodeId === 'n1::root');
            expect(nestedActor).toBeDefined();
            expect((nestedActor!.components[0]!.data as Record<string, unknown>).x).toBe(77);
        });
    });
});
