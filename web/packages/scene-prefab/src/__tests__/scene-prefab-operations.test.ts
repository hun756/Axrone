import { describe, expect, it } from 'vitest';
import {
    createScenePrefabState,
    materializeScenePrefabActors,
    materializeScenePrefabDefinition,
    materializeScenePrefabResolvedDefinition,
    applyScenePrefabOverrideOperations,
    applyScenePrefabOverrides,
    validateScenePrefabState,
    scopeScenePrefabActors,
    scopeScenePrefabOverrideOperations,
    readSceneSerializedValueAtPath,
    mergeScenePrefabActors,
    type ScenePrefabState,
} from '../scene-prefab-operations';
import { ScenePrefabValidationError } from '../errors';
import type {
    SceneActorSnapshot,
    SceneComponentSnapshot,
    ScenePrefabDefinition,
    ScenePrefabOverrideOperation,
    ScenePrefabNestedInstance,
    SceneSerializedValue,
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

const makeComponent = (
    type: string,
    data: SceneSerializedValue = {},
    id?: string,
): SceneComponentSnapshot => ({
    ...(id ? { id } : {}),
    type,
    data,
});

const makeDefinition = (
    id: string,
    actors: SceneActorSnapshot[],
): ScenePrefabDefinition => ({
    id,
    kind: 'prefab',
    actors,
});

const idSelector = (componentId: string) => ({ kind: 'id' as const, componentId });
const typeSelector = (type: string, occurrence?: number) =>
    occurrence !== undefined ? { kind: 'type' as const, type, occurrence } : { kind: 'type' as const, type };

// ---------------------------------------------------------------------------
// createScenePrefabState
// ---------------------------------------------------------------------------
describe('createScenePrefabState', () => {
    it('creates mutable state with actor index', () => {
        const def = makeDefinition('p1', [
            makeActor('root'),
            makeActor('child', 'Child', 'root'),
        ]);
        const state = createScenePrefabState(def);
        expect(state.id).toBe('p1');
        expect(state.actors).toHaveLength(2);
        expect(state.actorIndex.size).toBe(2);
        expect(state.actorIndex.get('root')).toBeDefined();
        expect(state.actorIndex.get('child')).toBeDefined();
    });

    it('throws on duplicate nodeId', () => {
        const def = makeDefinition('p1', [
            makeActor('dup'),
            makeActor('dup'),
        ]);
        expect(() => createScenePrefabState(def)).toThrow(ScenePrefabValidationError);
    });

    it('throws on missing nodeId', () => {
        const def = makeDefinition('p1', [
            { ...makeActor('ok'), nodeId: undefined },
        ]);
        expect(() => createScenePrefabState(def)).toThrow(ScenePrefabValidationError);
    });

    it('throws on empty/whitespace nodeId', () => {
        const def = makeDefinition('p1', [
            makeActor('  '),
        ]);
        expect(() => createScenePrefabState(def)).toThrow(ScenePrefabValidationError);
    });

    it('deep-clones actor data during creation', () => {
        const original = makeActor('root', 'Root', null, [makeComponent('Transform', { x: 1 })]);
        const def = makeDefinition('p1', [original]);
        const state = createScenePrefabState(def);
        // Mutate state actor — should not affect original
        state.actors[0]!.name = 'Mutated';
        expect(original.name).toBe('Root');
    });
});

// ---------------------------------------------------------------------------
// materializeScenePrefabActors
// ---------------------------------------------------------------------------
describe('materializeScenePrefabActors', () => {
    it('produces immutable snapshots', () => {
        const def = makeDefinition('p1', [makeActor('root')]);
        const state = createScenePrefabState(def);
        const actors = materializeScenePrefabActors(state);
        expect(actors).toHaveLength(1);
        expect(actors[0]!.nodeId).toBe('root');
    });

    it('deep-clones data (mutations do not affect state)', () => {
        const def = makeDefinition('p1', [
            makeActor('root', 'Root', null, [makeComponent('Transform', { pos: [1, 2, 3] })]),
        ]);
        const state = createScenePrefabState(def);
        const snap1 = materializeScenePrefabActors(state);
        const snap2 = materializeScenePrefabActors(state);
        expect(snap1[0]!.components[0]).not.toBe(snap2[0]!.components[0]);
    });
});

// ---------------------------------------------------------------------------
// materializeScenePrefabDefinition
// ---------------------------------------------------------------------------
describe('materializeScenePrefabDefinition', () => {
    it('returns a definition with materialized actors', () => {
        const def = makeDefinition('p1', [makeActor('root')]);
        const state = createScenePrefabState(def);
        const result = materializeScenePrefabDefinition(def, state);
        expect(result.id).toBe('p1');
        expect(result.actors).toHaveLength(1);
    });
});

// ---------------------------------------------------------------------------
// materializeScenePrefabResolvedDefinition
// ---------------------------------------------------------------------------
describe('materializeScenePrefabResolvedDefinition', () => {
    it('returns kind: resolved with lineage', () => {
        const def = makeDefinition('p1', [makeActor('root')]);
        const state = createScenePrefabState(def);
        const result = materializeScenePrefabResolvedDefinition(def, state, ['p1', 'base']);
        expect(result.kind).toBe('resolved');
        expect(result.lineage).toEqual(['p1', 'base']);
    });
});

// ---------------------------------------------------------------------------
// mergeScenePrefabActors
// ---------------------------------------------------------------------------
describe('mergeScenePrefabActors', () => {
    it('replaces existing actors by nodeId', () => {
        const def = makeDefinition('p1', [makeActor('root', 'Old')]);
        const state = createScenePrefabState(def);
        mergeScenePrefabActors(state, [makeActor('root', 'New')], 'p1');
        expect(state.actors).toHaveLength(1);
        expect(state.actors[0]!.name).toBe('New');
    });

    it('adds new actors', () => {
        const def = makeDefinition('p1', [makeActor('root')]);
        const state = createScenePrefabState(def);
        mergeScenePrefabActors(state, [makeActor('child', 'Child')], 'p1');
        expect(state.actors).toHaveLength(2);
        expect(state.actorIndex.has('child')).toBe(true);
    });
});

// ---------------------------------------------------------------------------
// Override operations
// ---------------------------------------------------------------------------
describe('override operations', () => {
    const baseActors = (): SceneActorSnapshot[] => [
        makeActor('root', 'Root', null, [makeComponent('Transform', { position: [0, 0, 0] }, 't-1')]),
        makeActor('child', 'Child', 'root', [makeComponent('Script', { enabled: true }, 's-1')]),
    ];

    const createState = () => createScenePrefabState(makeDefinition('test', baseActors()));

    describe('add-actor', () => {
        it('appends actor at end', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'add-actor',
                actor: makeActor('new', 'New'),
            }]);
            expect(state.actors).toHaveLength(3);
            expect(state.actors[2]!.nodeId).toBe('new');
        });

        it('inserts after specified actor', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'add-actor',
                actor: makeActor('inserted', 'Inserted'),
                afterNodeId: 'root',
            }]);
            expect(state.actors).toHaveLength(3);
            expect(state.actors[1]!.nodeId).toBe('inserted');
        });

        it('throws on duplicate nodeId', () => {
            const state = createState();
            expect(() => applyScenePrefabOverrideOperations(state, [{
                kind: 'add-actor',
                actor: makeActor('root', 'Dup'),
            }])).toThrow(ScenePrefabValidationError);
        });
    });

    describe('remove-actor', () => {
        it('removes actor and its children', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'remove-actor',
                nodeId: 'root',
            }]);
            // root + child (child's parent is root) both removed
            expect(state.actors).toHaveLength(0);
        });

        it('removes only the targeted actor when no children', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'remove-actor',
                nodeId: 'child',
            }]);
            expect(state.actors).toHaveLength(1);
            expect(state.actors[0]!.nodeId).toBe('root');
        });

        it('throws on missing nodeId', () => {
            const state = createState();
            expect(() => applyScenePrefabOverrideOperations(state, [{
                kind: 'remove-actor',
                nodeId: 'nonexistent',
            }])).toThrow(ScenePrefabValidationError);
        });
    });

    describe('reparent-actor', () => {
        it('reparents to a valid parent', () => {
            const state = createState();
            // Add a new actor then reparent it under child
            applyScenePrefabOverrideOperations(state, [
                { kind: 'add-actor', actor: makeActor('new', 'New') },
                { kind: 'reparent-actor', nodeId: 'new', parentNodeId: 'child' },
            ]);
            const actor = state.actorIndex.get('new');
            expect(actor!.parentNodeId).toBe('child');
        });

        it('reparents to root (null parentNodeId)', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'reparent-actor',
                nodeId: 'child',
                parentNodeId: null,
            }]);
            expect(state.actorIndex.get('child')!.parentNodeId).toBeNull();
        });

        it('throws when target parent does not exist', () => {
            const state = createState();
            expect(() => applyScenePrefabOverrideOperations(state, [{
                kind: 'reparent-actor',
                nodeId: 'child',
                parentNodeId: 'ghost',
            }])).toThrow(ScenePrefabValidationError);
        });
    });

    describe('set-actor-field', () => {
        it('sets name', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-actor-field', nodeId: 'root', field: 'name', value: 'Renamed',
            }]);
            expect(state.actorIndex.get('root')!.name).toBe('Renamed');
        });

        it('sets layer', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-actor-field', nodeId: 'root', field: 'layer', value: 5,
            }]);
            expect(state.actorIndex.get('root')!.layer).toBe(5);
        });

        it('sets tag', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-actor-field', nodeId: 'root', field: 'tag', value: 'hero',
            }]);
            expect(state.actorIndex.get('root')!.tag).toBe('hero');
        });

        it('sets active', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-actor-field', nodeId: 'root', field: 'active', value: false,
            }]);
            expect(state.actorIndex.get('root')!.active).toBe(false);
        });

        it('sets persistent', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-actor-field', nodeId: 'root', field: 'persistent', value: true,
            }]);
            expect(state.actorIndex.get('root')!.persistent).toBe(true);
        });

        it('sets pooled', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-actor-field', nodeId: 'root', field: 'pooled', value: true,
            }]);
            expect(state.actorIndex.get('root')!.pooled).toBe(true);
        });

        it('throws on type mismatch for name (string expected)', () => {
            const state = createState();
            expect(() => applyScenePrefabOverrideOperations(state, [{
                kind: 'set-actor-field', nodeId: 'root', field: 'name', value: 123 as any,
            }])).toThrow(ScenePrefabValidationError);
        });

        it('throws on type mismatch for layer (number expected)', () => {
            const state = createState();
            expect(() => applyScenePrefabOverrideOperations(state, [{
                kind: 'set-actor-field', nodeId: 'root', field: 'layer', value: 'bad' as any,
            }])).toThrow(ScenePrefabValidationError);
        });

        it('throws on type mismatch for active (boolean expected)', () => {
            const state = createState();
            expect(() => applyScenePrefabOverrideOperations(state, [{
                kind: 'set-actor-field', nodeId: 'root', field: 'active', value: 'yes' as any,
            }])).toThrow(ScenePrefabValidationError);
        });

        it('throws on Infinity for layer', () => {
            const state = createState();
            expect(() => applyScenePrefabOverrideOperations(state, [{
                kind: 'set-actor-field', nodeId: 'root', field: 'layer', value: Infinity,
            }])).toThrow(ScenePrefabValidationError);
        });
    });

    describe('add-component', () => {
        it('appends component to actor', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'add-component',
                nodeId: 'root',
                component: makeComponent('RigidBody', { mass: 10 }),
            }]);
            const actor = state.actorIndex.get('root')!;
            expect(actor.components).toHaveLength(2);
            expect(actor.components[1]!.type).toBe('RigidBody');
        });

        it('inserts at specified index', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'add-component',
                nodeId: 'root',
                component: makeComponent('First'),
                index: 0,
            }]);
            const actor = state.actorIndex.get('root')!;
            expect(actor.components[0]!.type).toBe('First');
        });

        it('clamps negative index to 0', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'add-component',
                nodeId: 'root',
                component: makeComponent('First'),
                index: -5,
            }]);
            const actor = state.actorIndex.get('root')!;
            expect(actor.components[0]!.type).toBe('First');
        });

        it('throws on duplicate component id', () => {
            const state = createState();
            expect(() => applyScenePrefabOverrideOperations(state, [{
                kind: 'add-component',
                nodeId: 'root',
                component: makeComponent('Transform', { x: 0 }, 't-1'), // t-1 already exists
            }])).toThrow(ScenePrefabValidationError);
        });

        it('throws on non-integer index', () => {
            const state = createState();
            expect(() => applyScenePrefabOverrideOperations(state, [{
                kind: 'add-component',
                nodeId: 'root',
                component: makeComponent('X'),
                index: 1.5,
            }])).toThrow(ScenePrefabValidationError);
        });
    });

    describe('remove-component', () => {
        it('removes by id selector', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'remove-component',
                nodeId: 'root',
                selector: idSelector('t-1'),
            }]);
            expect(state.actorIndex.get('root')!.components).toHaveLength(0);
        });

        it('removes by type selector', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'remove-component',
                nodeId: 'root',
                selector: typeSelector('Transform'),
            }]);
            expect(state.actorIndex.get('root')!.components).toHaveLength(0);
        });

        it('throws on missing component', () => {
            const state = createState();
            expect(() => applyScenePrefabOverrideOperations(state, [{
                kind: 'remove-component',
                nodeId: 'root',
                selector: idSelector('nonexistent'),
            }])).toThrow(ScenePrefabValidationError);
        });
    });

    describe('replace-component', () => {
        it('replaces by id', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'replace-component',
                nodeId: 'root',
                selector: idSelector('t-1'),
                component: makeComponent('NewTransform', { y: 99 }, 't-1'),
            }]);
            const comp = state.actorIndex.get('root')!.components[0]!;
            expect(comp.type).toBe('NewTransform');
        });

        it('replaces by type with occurrence', () => {
            const state = createState();
            // Add a second Transform
            applyScenePrefabOverrideOperations(state, [
                { kind: 'add-component', nodeId: 'root', component: makeComponent('Transform', { z: 1 }) },
                {
                    kind: 'replace-component',
                    nodeId: 'root',
                    selector: typeSelector('Transform', 1),
                    component: makeComponent('Replaced', { z: 2 }),
                },
            ]);
            const comps = state.actorIndex.get('root')!.components;
            expect(comps[0]!.type).toBe('Transform'); // first unchanged
            expect(comps[1]!.type).toBe('Replaced'); // second replaced
        });
    });

    describe('set-component-property', () => {
        it('sets nested path value', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-component-property',
                nodeId: 'root',
                selector: idSelector('t-1'),
                path: ['position', 'x'],
                value: 42,
            }]);
            const data = state.actorIndex.get('root')!.components[0]!.data;
            expect((data as any).position.x).toBe(42);
        });

        it('creates intermediate objects for deep paths', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-component-property',
                nodeId: 'root',
                selector: idSelector('t-1'),
                path: ['deep', 'nested', 'value'],
                value: 'hello',
            }]);
            const data = state.actorIndex.get('root')!.components[0]!.data;
            expect((data as any).deep.nested.value).toBe('hello');
        });

        it('replaces root when path is empty', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-component-property',
                nodeId: 'root',
                selector: idSelector('t-1'),
                path: [],
                value: { completely: 'new' },
            }]);
            const data = state.actorIndex.get('root')!.components[0]!.data;
            expect(data).toEqual({ completely: 'new' });
        });

        it('sets array index', () => {
            const state = createState();
            // First set data to an array
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-component-property',
                nodeId: 'root',
                selector: idSelector('t-1'),
                path: [],
                value: [10, 20, 30],
            }]);
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-component-property',
                nodeId: 'root',
                selector: idSelector('t-1'),
                path: [1],
                value: 99,
            }]);
            const data = state.actorIndex.get('root')!.components[0]!.data;
            expect(data).toEqual([10, 99, 30]);
        });
    });

    describe('unset-component-property', () => {
        it('deletes key from object', () => {
            const state = createState();
            // First set data with multiple keys
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-component-property',
                nodeId: 'root',
                selector: idSelector('t-1'),
                path: [],
                value: { a: 1, b: 2, c: 3 },
            }]);
            applyScenePrefabOverrideOperations(state, [{
                kind: 'unset-component-property',
                nodeId: 'root',
                selector: idSelector('t-1'),
                path: ['b'],
            }]);
            const data = state.actorIndex.get('root')!.components[0]!.data;
            expect(data).toEqual({ a: 1, c: 3 });
        });

        it('splices array element', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'set-component-property',
                nodeId: 'root',
                selector: idSelector('t-1'),
                path: [],
                value: [10, 20, 30],
            }]);
            applyScenePrefabOverrideOperations(state, [{
                kind: 'unset-component-property',
                nodeId: 'root',
                selector: idSelector('t-1'),
                path: [1],
            }]);
            const data = state.actorIndex.get('root')!.components[0]!.data;
            expect(data).toEqual([10, 30]);
        });

        it('nullifies when path is empty', () => {
            const state = createState();
            applyScenePrefabOverrideOperations(state, [{
                kind: 'unset-component-property',
                nodeId: 'root',
                selector: idSelector('t-1'),
                path: [],
            }]);
            const data = state.actorIndex.get('root')!.components[0]!.data;
            expect(data).toBeNull();
        });
    });
});

// ---------------------------------------------------------------------------
// applyScenePrefabOverrides (end-to-end)
// ---------------------------------------------------------------------------
describe('applyScenePrefabOverrides', () => {
    it('applies multiple operations and returns a new definition', () => {
        const def = makeDefinition('p1', [
            makeActor('root', 'Root', null, [makeComponent('Transform', { x: 0 }, 't-1')]),
        ]);
        const result = applyScenePrefabOverrides(def, [
            { kind: 'set-actor-field', nodeId: 'root', field: 'name', value: 'Renamed' },
            { kind: 'set-component-property', nodeId: 'root', selector: idSelector('t-1'), path: ['x'], value: 42 },
            { kind: 'add-actor', actor: makeActor('child', 'Child', 'root') },
        ]);
        expect(result.actors).toHaveLength(2);
        expect(result.actors[0]!.name).toBe('Renamed');
        expect((result.actors[0]!.components[0]!.data as any).x).toBe(42);
        expect(result.actors[1]!.nodeId).toBe('child');
    });
});

// ---------------------------------------------------------------------------
// validateScenePrefabState
// ---------------------------------------------------------------------------
describe('validateScenePrefabState', () => {
    it('passes for valid state', () => {
        const state = createState();
        expect(() => validateScenePrefabState(state)).not.toThrow();
    });

    it('throws on missing parent reference', () => {
        const def = makeDefinition('p1', [
            makeActor('root'),
            makeActor('orphan', 'Orphan', 'ghost'),
        ]);
        const state = createScenePrefabState(def);
        // Manually bypass — the state creation doesn't validate parents
        // Actually, validateScenePrefabState is called at end of applyScenePrefabOverrideOperations
        // Let's call it directly
        expect(() => validateScenePrefabState(state)).toThrow(ScenePrefabValidationError);
    });

    it('throws on hierarchy cycle', () => {
        // Create a state with a cycle: a -> b -> a
        const def = makeDefinition('p1', [
            makeActor('a', 'A', 'b'),
            makeActor('b', 'B', 'a'),
        ]);
        const state = createScenePrefabState(def);
        expect(() => validateScenePrefabState(state)).toThrow(/cycle/i);
    });
});

// ---------------------------------------------------------------------------
// scopeScenePrefabActors
// ---------------------------------------------------------------------------
describe('scopeScenePrefabActors', () => {
    it('prefixes nodeIds with instanceId', () => {
        const actors = [makeActor('root'), makeActor('child', 'Child', 'root')];
        const nested: ScenePrefabNestedInstance = {
            instanceId: 'inst-1',
            reference: { kind: 'registry', prefabId: 'p1' },
        };
        const scoped = scopeScenePrefabActors(actors, nested, 'p1');
        expect(scoped[0]!.nodeId).toBe('inst-1::root');
        expect(scoped[1]!.nodeId).toBe('inst-1::child');
        expect(scoped[1]!.parentNodeId).toBe('inst-1::root');
    });

    it('uses nested parentNodeId for root actors', () => {
        const actors = [makeActor('root')];
        const nested: ScenePrefabNestedInstance = {
            instanceId: 'inst-1',
            reference: { kind: 'registry', prefabId: 'p1' },
            parentNodeId: 'parent-outside',
        };
        const scoped = scopeScenePrefabActors(actors, nested, 'p1');
        expect(scoped[0]!.parentNodeId).toBe('parent-outside');
    });

    it('applies namePrefix', () => {
        const actors = [makeActor('root', 'Root')];
        const nested: ScenePrefabNestedInstance = {
            instanceId: 'inst-1',
            reference: { kind: 'registry', prefabId: 'p1' },
            namePrefix: 'Copy_',
        };
        const scoped = scopeScenePrefabActors(actors, nested, 'p1');
        expect(scoped[0]!.name).toBe('Copy_Root');
    });

    it('sets source instancePath', () => {
        const actors = [makeActor('root')];
        const nested: ScenePrefabNestedInstance = {
            instanceId: 'inst-1',
            reference: { kind: 'registry', prefabId: 'p1' },
        };
        const scoped = scopeScenePrefabActors(actors, nested, 'p1');
        expect(scoped[0]!.source).toBeDefined();
        expect(scoped[0]!.source!.instancePath).toContain('inst-1');
    });
});

// ---------------------------------------------------------------------------
// scopeScenePrefabOverrideOperations
// ---------------------------------------------------------------------------
describe('scopeScenePrefabOverrideOperations', () => {
    const nested: ScenePrefabNestedInstance = {
        instanceId: 'inst-1',
        reference: { kind: 'registry', prefabId: 'p1' },
    };

    it('scopes remove-actor nodeId', () => {
        const ops: ScenePrefabOverrideOperation[] = [{ kind: 'remove-actor', nodeId: 'root' }];
        const scoped = scopeScenePrefabOverrideOperations(ops, nested, 'p1');
        expect(scoped[0]!.kind).toBe('remove-actor');
        if (scoped[0]!.kind === 'remove-actor') {
            expect(scoped[0]!.nodeId).toBe('inst-1::root');
        }
    });

    it('scopes set-actor-field nodeId', () => {
        const ops: ScenePrefabOverrideOperation[] = [{
            kind: 'set-actor-field', nodeId: 'root', field: 'name', value: 'X',
        }];
        const scoped = scopeScenePrefabOverrideOperations(ops, nested, 'p1');
        if (scoped[0]!.kind === 'set-actor-field') {
            expect(scoped[0]!.nodeId).toBe('inst-1::root');
        }
    });

    it('scopes set-component-property nodeId', () => {
        const ops: ScenePrefabOverrideOperation[] = [{
            kind: 'set-component-property',
            nodeId: 'root',
            selector: idSelector('c'),
            path: ['x'],
            value: 1,
        }];
        const scoped = scopeScenePrefabOverrideOperations(ops, nested, 'p1');
        if (scoped[0]!.kind === 'set-component-property') {
            expect(scoped[0]!.nodeId).toBe('inst-1::root');
        }
    });

    it('scopes add-actor with afterNodeId', () => {
        const ops: ScenePrefabOverrideOperation[] = [{
            kind: 'add-actor',
            actor: makeActor('new'),
            afterNodeId: 'root',
        }];
        const scoped = scopeScenePrefabOverrideOperations(ops, nested, 'p1');
        if (scoped[0]!.kind === 'add-actor') {
            expect(scoped[0]!.afterNodeId).toBe('inst-1::root');
        }
    });

    it('scopes reparent-actor parentNodeId', () => {
        const ops: ScenePrefabOverrideOperation[] = [{
            kind: 'reparent-actor', nodeId: 'child', parentNodeId: 'root',
        }];
        const scoped = scopeScenePrefabOverrideOperations(ops, nested, 'p1');
        if (scoped[0]!.kind === 'reparent-actor') {
            expect(scoped[0]!.nodeId).toBe('inst-1::child');
            expect(scoped[0]!.parentNodeId).toBe('inst-1::root');
        }
    });

    it('scopes reparent-actor to root (null parentNodeId uses nested.parentNodeId)', () => {
        const nestedWithParent: ScenePrefabNestedInstance = {
            ...nested,
            parentNodeId: 'outside-parent',
        };
        const ops: ScenePrefabOverrideOperation[] = [{
            kind: 'reparent-actor', nodeId: 'child', parentNodeId: null,
        }];
        const scoped = scopeScenePrefabOverrideOperations(ops, nestedWithParent, 'p1');
        if (scoped[0]!.kind === 'reparent-actor') {
            expect(scoped[0]!.parentNodeId).toBe('outside-parent');
        }
    });

    it('scopes remove-component', () => {
        const ops: ScenePrefabOverrideOperation[] = [{
            kind: 'remove-component', nodeId: 'root', selector: idSelector('c'),
        }];
        const scoped = scopeScenePrefabOverrideOperations(ops, nested, 'p1');
        if (scoped[0]!.kind === 'remove-component') {
            expect(scoped[0]!.nodeId).toBe('inst-1::root');
        }
    });

    it('scopes add-component', () => {
        const ops: ScenePrefabOverrideOperation[] = [{
            kind: 'add-component', nodeId: 'root', component: makeComponent('X'),
        }];
        const scoped = scopeScenePrefabOverrideOperations(ops, nested, 'p1');
        if (scoped[0]!.kind === 'add-component') {
            expect(scoped[0]!.nodeId).toBe('inst-1::root');
        }
    });

    it('scopes replace-component', () => {
        const ops: ScenePrefabOverrideOperation[] = [{
            kind: 'replace-component', nodeId: 'root', selector: idSelector('c'),
            component: makeComponent('Y'),
        }];
        const scoped = scopeScenePrefabOverrideOperations(ops, nested, 'p1');
        if (scoped[0]!.kind === 'replace-component') {
            expect(scoped[0]!.nodeId).toBe('inst-1::root');
        }
    });

    it('scopes unset-component-property', () => {
        const ops: ScenePrefabOverrideOperation[] = [{
            kind: 'unset-component-property', nodeId: 'root',
            selector: idSelector('c'), path: ['x'],
        }];
        const scoped = scopeScenePrefabOverrideOperations(ops, nested, 'p1');
        if (scoped[0]!.kind === 'unset-component-property') {
            expect(scoped[0]!.nodeId).toBe('inst-1::root');
        }
    });
});

// ---------------------------------------------------------------------------
// readSceneSerializedValueAtPath
// ---------------------------------------------------------------------------
describe('readSceneSerializedValueAtPath', () => {
    const data: SceneSerializedValue = {
        transform: { position: { x: 1, y: 2, z: 3 } },
        items: [10, 20, 30],
    };

    it('reads nested object path', () => {
        expect(readSceneSerializedValueAtPath(data, ['transform', 'position', 'x'])).toBe(1);
    });

    it('reads array index', () => {
        expect(readSceneSerializedValueAtPath(data, ['items', 1])).toBe(20);
    });

    it('returns null for missing path', () => {
        expect(readSceneSerializedValueAtPath(data, ['missing'])).toBeNull();
        expect(readSceneSerializedValueAtPath(data, ['transform', 'missing'])).toBeNull();
    });

    it('returns null for out-of-range array index', () => {
        expect(readSceneSerializedValueAtPath(data, ['items', 10])).toBeNull();
    });

    it('clones the full value when path is empty', () => {
        const result = readSceneSerializedValueAtPath(data, []);
        expect(result).toEqual(data);
        expect(result).not.toBe(data);
    });

    it('returns intermediate object', () => {
        const result = readSceneSerializedValueAtPath(data, ['transform', 'position']);
        expect(result).toEqual({ x: 1, y: 2, z: 3 });
    });
});

// ─── Shared helper ─────────────────────────────────────────────────────────

function createState() {
    return createScenePrefabState(makeDefinition('test', [
        makeActor('root', 'Root', null, [makeComponent('Transform', { position: [0, 0, 0] }, 't-1')]),
        makeActor('child', 'Child', 'root', [makeComponent('Script', { enabled: true }, 's-1')]),
    ]));
}
