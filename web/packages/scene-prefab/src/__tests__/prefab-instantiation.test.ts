import { describe, expect, it, beforeEach } from 'vitest';
import {
    Actor,
    Component,
    Hierarchy,
    Transform,
    World,
    property,
    script,
} from '@axrone/ecs-runtime';
import { Vec3 } from '@axrone/numeric';
import { PrefabNodeBinding } from '../prefab-node-binding';
import { ScenePrefabWorkflow } from '../scene-prefab-workflow';
import { SceneActorRuntime, createSceneRegistry } from '@axrone/scene-runtime';
import { SceneComponentCatalog } from '@axrone/scene-runtime/component-catalog';
import { encodeSceneValue } from '@axrone/scene-runtime/serialization';
import type {
    SceneActorSnapshot,
    SceneComponentSnapshot,
    ScenePrefabDefinition,
} from '../types';

// ─── Test fixture components ───────────────────────────────────────────────

@script({ scriptName: 'TestHealth' })
class TestHealth extends Component {
    @property({ type: 'number' })
    public maxHealth = 100;

    @property({ type: 'number' })
    public currentHealth = 100;

    @property({ type: 'boolean' })
    public isInvincible = false;
}

@script({ scriptName: 'TestWeapon' })
class TestWeapon extends Component {
    @property({ type: 'number' })
    public damage = 10;

    @property({ type: 'string' })
    public weaponName = 'Sword';

    @property({ type: Actor })
    public targetActor: Actor | null = null;

    @property({ type: Transform })
    public targetTransform: Transform | null = null;
}

@script({ scriptName: 'TestFollowTarget' })
class TestFollowTarget extends Component {
    @property({ type: Actor })
    public leader: Actor | null = null;

    @property({ type: Transform })
    public leaderTransform: Transform | null = null;

    @property({ type: 'vec3' })
    public offset = new Vec3(0, 0, 0);
}

// ─── Helpers ───────────────────────────────────────────────────────────────

interface PrefabTestHarness {
    readonly actorRuntime: SceneActorRuntime<any>;
    readonly world: World<any>;
    readonly componentCatalog: SceneComponentCatalog;
}

const createPrefabTestHarness = (): PrefabTestHarness => {
    const registry = createSceneRegistry({
        registry: {
            TestHealth,
            TestWeapon,
            TestFollowTarget,
        },
    });
    const world = new World(registry);
    const componentCatalog = new SceneComponentCatalog(registry);
    const actorRuntime = new SceneActorRuntime({
        world,
        componentCatalog,
    });

    actorRuntime.registerComponent(TestHealth);
    actorRuntime.registerComponent(TestWeapon);
    actorRuntime.registerComponent(TestFollowTarget);

    return { actorRuntime, world, componentCatalog };
};

const makeComponent = (
    type: string,
    data: unknown,
    id?: string,
): SceneComponentSnapshot => ({
    ...(id ? { id } : {}),
    type,
    data: encodeSceneValue(data),
});

const makeTransformComponent = (
    position: readonly [number, number, number] = [0, 0, 0],
    rotation: readonly [number, number, number, number] = [0, 0, 0, 1],
    scale: readonly [number, number, number] = [1, 1, 1],
): SceneComponentSnapshot =>
    makeComponent('Transform', { position, rotation, scale });

const makeActorSnapshot = (
    nodeId: string,
    name: string,
    options: {
        parentNodeId?: string | null;
        components?: SceneComponentSnapshot[];
        active?: boolean;
        layer?: number;
        tag?: string;
    } = {},
): SceneActorSnapshot => ({
    nodeId,
    parentNodeId: options.parentNodeId ?? null,
    name,
    layer: options.layer ?? 0,
    tag: options.tag ?? 'default',
    active: options.active ?? true,
    persistent: false,
    pooled: false,
    components: options.components ?? [makeTransformComponent()],
});

const makeSimplePrefab = (
    id = 'prefab/simple',
    actorName = 'Cube',
): ScenePrefabDefinition => ({
    id,
    kind: 'prefab',
    actors: [makeActorSnapshot('root', actorName)],
});

// ---------------------------------------------------------------------------
// Test Group 1: Simple Prefab Instantiation (Cube-like)
// ---------------------------------------------------------------------------
describe('Prefab Instantiation — Simple Prefab', () => {
    let harness: PrefabTestHarness;

    beforeEach(() => {
        harness = createPrefabTestHarness();
    });

    it('creates an actor with the correct name from a minimal prefab', () => {
        const prefab = makeSimplePrefab('prefab/cube', 'Cube');
        const actors = harness.actorRuntime.instantiatePrefab(prefab);

        expect(actors).toHaveLength(1);
        expect(actors[0]!.name).toBe('Cube');
    });

    it('adds a Transform component to the instantiated actor', () => {
        const prefab = makeSimplePrefab();
        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const actor = actors[0]!;

        const transform = actor.getComponent(Transform);
        expect(transform).toBeDefined();
        expect(transform).toBeInstanceOf(Transform);
    });

    it('sets the actor to active after instantiation when snapshot says active', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/active-test',
            kind: 'prefab',
            actors: [makeActorSnapshot('root', 'ActiveActor', { active: true })],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        expect(actors[0]!.active).toBe(true);
    });

    it('sets the actor to inactive when snapshot says inactive', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/inactive-test',
            kind: 'prefab',
            actors: [makeActorSnapshot('root', 'InactiveActor', { active: false })],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        expect(actors[0]!.active).toBe(false);
    });

    it('attaches PrefabNodeBinding that maps nodeId to actor', () => {
        const prefab = makeSimplePrefab('prefab/binding-test', 'Bound');
        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const actor = actors[0]!;

        const binding = actor.getComponent(PrefabNodeBinding);
        expect(binding).toBeDefined();
        expect(binding!.nodeId).toBe('root');
        expect(binding!.instanceId).toBeTruthy();
    });

    it('applies namePrefix option to actor names', () => {
        const prefab = makeSimplePrefab('prefab/prefix-test', 'Enemy');
        const actors = harness.actorRuntime.instantiatePrefab(prefab, {
            namePrefix: 'Instance_1/',
        });

        expect(actors[0]!.name).toBe('Instance_1/Enemy');
    });

    it('creates multiple actors from a prefab with several actor snapshots', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/multi',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('node-a', 'ActorA'),
                makeActorSnapshot('node-b', 'ActorB'),
                makeActorSnapshot('node-c', 'ActorC'),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        expect(actors).toHaveLength(3);
        expect(actors.map((a) => a.name)).toEqual(['ActorA', 'ActorB', 'ActorC']);
    });
});

// ---------------------------------------------------------------------------
// Test Group 2: Hierarchical Prefab (Parent-Child)
// ---------------------------------------------------------------------------
describe('Prefab Instantiation — Hierarchical Prefab', () => {
    let harness: PrefabTestHarness;

    beforeEach(() => {
        harness = createPrefabTestHarness();
    });

    it('preserves parent-child hierarchy from prefab definition', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/hierarchy',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('parent', 'Parent'),
                makeActorSnapshot('child', 'Child', { parentNodeId: 'parent' }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const parent = actors.find((a) => a.name === 'Parent')!;
        const child = actors.find((a) => a.name === 'Child')!;

        const childHierarchy = child.getComponent(Hierarchy);
        expect(childHierarchy).toBeDefined();
        expect(childHierarchy!.parentActor).toBe(parent);
    });

    it('supports multi-level hierarchy (grandparent -> parent -> child)', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/deep-hierarchy',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('grandparent', 'GrandParent'),
                makeActorSnapshot('parent', 'Parent', { parentNodeId: 'grandparent' }),
                makeActorSnapshot('child', 'Child', { parentNodeId: 'parent' }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const grandParent = actors.find((a) => a.name === 'GrandParent')!;
        const parent = actors.find((a) => a.name === 'Parent')!;
        const child = actors.find((a) => a.name === 'Child')!;

        expect(parent.getComponent(Hierarchy)!.parentActor).toBe(grandParent);
        expect(child.getComponent(Hierarchy)!.parentActor).toBe(parent);
    });

    it('root actors have no parent', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/roots',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root-a', 'RootA'),
                makeActorSnapshot('root-b', 'RootB'),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);

        for (const actor of actors) {
            const hierarchy = actor.getComponent(Hierarchy);
            expect(hierarchy!.parentActor).toBeUndefined();
        }
    });

    it('preserves hierarchy in a skeleton-like structure', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/skeleton',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'Root'),
                makeActorSnapshot('spine', 'Spine', { parentNodeId: 'root' }),
                makeActorSnapshot('head', 'Head', { parentNodeId: 'spine' }),
                makeActorSnapshot('left-arm', 'LeftArm', { parentNodeId: 'spine' }),
                makeActorSnapshot('right-arm', 'RightArm', { parentNodeId: 'spine' }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const spine = actors.find((a) => a.name === 'Spine')!;
        const head = actors.find((a) => a.name === 'Head')!;
        const leftArm = actors.find((a) => a.name === 'LeftArm')!;
        const rightArm = actors.find((a) => a.name === 'RightArm')!;

        expect(head.getComponent(Hierarchy)!.parentActor).toBe(spine);
        expect(leftArm.getComponent(Hierarchy)!.parentActor).toBe(spine);
        expect(rightArm.getComponent(Hierarchy)!.parentActor).toBe(spine);
    });

    it('child transforms are relative to parent after instantiation', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/transform-hierarchy',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('parent', 'Parent', {
                    components: [makeTransformComponent([10, 0, 0])],
                }),
                makeActorSnapshot('child', 'Child', {
                    parentNodeId: 'parent',
                    components: [makeTransformComponent([0, 5, 0])],
                }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const parentTransform = actors[0]!.getComponent(Transform)!;
        const childTransform = actors[1]!.getComponent(Transform)!;

        expect(parentTransform.position.x).toBe(10);
        expect(childTransform.position.y).toBe(5);
    });
});

// ---------------------------------------------------------------------------
// Test Group 3: Multiple Instances (ID Uniqueness)
// ---------------------------------------------------------------------------
describe('Prefab Instantiation — Multiple Instances', () => {
    let harness: PrefabTestHarness;

    beforeEach(() => {
        harness = createPrefabTestHarness();
    });

    it('each instance gets unique entity/actor IDs', () => {
        const prefab = makeSimplePrefab('prefab/id-test', 'Clone');

        const instance1 = harness.actorRuntime.instantiatePrefab(prefab);
        const instance2 = harness.actorRuntime.instantiatePrefab(prefab);

        expect(instance1[0]!.id).not.toBe(instance2[0]!.id);
    });

    it('each instance gets unique PrefabNodeBinding instanceId', () => {
        const prefab = makeSimplePrefab('prefab/instance-id-test', 'Clone');

        const instance1 = harness.actorRuntime.instantiatePrefab(prefab);
        const instance2 = harness.actorRuntime.instantiatePrefab(prefab);

        const binding1 = instance1[0]!.getComponent(PrefabNodeBinding)!;
        const binding2 = instance2[0]!.getComponent(PrefabNodeBinding)!;

        expect(binding1.instanceId).not.toBe(binding2.instanceId);
    });

    it('each instance gets unique component IDs', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/comp-id-test',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'Actor', {
                    components: [
                        makeTransformComponent(),
                        makeComponent('TestHealth', { maxHealth: 100, currentHealth: 100, isInvincible: false }),
                    ],
                }),
            ],
        };

        const instance1 = harness.actorRuntime.instantiatePrefab(prefab);
        const instance2 = harness.actorRuntime.instantiatePrefab(prefab);

        const components1 = instance1[0]!.getAllComponents();
        const components2 = instance2[0]!.getAllComponents();

        const ids1 = new Set(components1.map((c) => c.id));
        const ids2 = new Set(components2.map((c) => c.id));

        for (const id of ids1) {
            expect(ids2.has(id)).toBe(false);
        }
    });

    it('instances do not interfere with each other', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/isolation-test',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'Actor', {
                    components: [
                        makeTransformComponent([0, 0, 0]),
                        makeComponent('TestHealth', { maxHealth: 100, currentHealth: 50, isInvincible: false }),
                    ],
                }),
            ],
        };

        const instance1 = harness.actorRuntime.instantiatePrefab(prefab);
        const instance2 = harness.actorRuntime.instantiatePrefab(prefab);

        const health1 = instance1[0]!.getComponent(TestHealth)!;
        const health2 = instance2[0]!.getComponent(TestHealth)!;

        health1.currentHealth = 0;

        expect(health1.currentHealth).toBe(0);
        expect(health2.currentHealth).toBe(50);
    });

    it('both instances are functional after creation', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/functional-test',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'Actor', {
                    components: [
                        makeTransformComponent([1, 2, 3]),
                        makeComponent('TestHealth', { maxHealth: 200, currentHealth: 200, isInvincible: true }),
                    ],
                }),
            ],
        };

        const instance1 = harness.actorRuntime.instantiatePrefab(prefab);
        const instance2 = harness.actorRuntime.instantiatePrefab(prefab);

        for (const actors of [instance1, instance2]) {
            const actor = actors[0]!;
            expect(actor.getComponent(Transform)).toBeDefined();
            expect(actor.getComponent(TestHealth)).toBeDefined();
            expect(actor.getComponent(PrefabNodeBinding)).toBeDefined();
            expect(actor.active).toBe(true);
        }
    });

    it('can instantiate the same prefab many times without errors', () => {
        const prefab = makeSimplePrefab('prefab/stress-test', 'Stress');
        const allInstances: Actor[][] = [];

        for (let i = 0; i < 20; i++) {
            allInstances.push(harness.actorRuntime.instantiatePrefab(prefab) as Actor[]);
        }

        const allIds = new Set(allInstances.map((inst) => inst[0]!.id));
        expect(allIds.size).toBe(20);
    });
});

// ---------------------------------------------------------------------------
// Test Group 4: Component Data Hydration
// ---------------------------------------------------------------------------
describe('Prefab Instantiation — Component Data Hydration', () => {
    let harness: PrefabTestHarness;

    beforeEach(() => {
        harness = createPrefabTestHarness();
    });

    it('applies Transform position/rotation/scale from serialized data', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/transform-data',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'PositionedActor', {
                    components: [
                        makeTransformComponent([5, 10, -3], [0, 0.707, 0, 0.707], [2, 2, 2]),
                    ],
                }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const transform = actors[0]!.getComponent(Transform)!;

        expect(transform.position.x).toBeCloseTo(5);
        expect(transform.position.y).toBeCloseTo(10);
        expect(transform.position.z).toBeCloseTo(-3);
        expect(transform.rotation.y).toBeCloseTo(0.707);
        expect(transform.scale.x).toBeCloseTo(2);
        expect(transform.scale.y).toBeCloseTo(2);
        expect(transform.scale.z).toBeCloseTo(2);
    });

    it('hydrates custom component scalar properties', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/health-data',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'Tank', {
                    components: [
                        makeTransformComponent(),
                        makeComponent('TestHealth', {
                            maxHealth: 500,
                            currentHealth: 350,
                            isInvincible: true,
                        }),
                    ],
                }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const health = actors[0]!.getComponent(TestHealth)!;

        expect(health.maxHealth).toBe(500);
        expect(health.currentHealth).toBe(350);
        expect(health.isInvincible).toBe(true);
    });

    it('hydrates string properties on custom components', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/weapon-string',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'Warrior', {
                    components: [
                        makeTransformComponent(),
                        makeComponent('TestWeapon', {
                            damage: 25,
                            weaponName: 'Flamebrand',
                        }),
                    ],
                }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const weapon = actors[0]!.getComponent(TestWeapon)!;

        expect(weapon.damage).toBe(25);
        expect(weapon.weaponName).toBe('Flamebrand');
    });

    it('resolves entity references (nodeId to Actor)', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/entity-ref',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('node/weapon', 'WeaponHolder', {
                    components: [
                        makeTransformComponent(),
                        makeComponent('TestWeapon', {
                            damage: 15,
                            weaponName: 'Dagger',
                            targetActor: { kind: 'entity', target: 'node/enemy' },
                        }),
                    ],
                }),
                makeActorSnapshot('node/enemy', 'Enemy', {
                    components: [makeTransformComponent([10, 0, 0])],
                }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const weaponHolder = actors.find((a) => a.name === 'WeaponHolder')!;
        const enemy = actors.find((a) => a.name === 'Enemy')!;
        const weapon = weaponHolder.getComponent(TestWeapon)!;

        expect(weapon.targetActor).toBe(enemy);
    });

    it('resolves transform references (nodeId to Transform)', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/transform-ref',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('node/follower', 'Follower', {
                    components: [
                        makeTransformComponent(),
                        makeComponent('TestFollowTarget', {
                            leaderTransform: { kind: 'entity', target: 'node/leader' },
                            offset: { x: 1, y: 2, z: 3 },
                        }),
                    ],
                }),
                makeActorSnapshot('node/leader', 'Leader', {
                    components: [makeTransformComponent([5, 0, 0])],
                }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const follower = actors.find((a) => a.name === 'Follower')!;
        const leader = actors.find((a) => a.name === 'Leader')!;
        const follow = follower.getComponent(TestFollowTarget)!;

        expect(follow.leaderTransform).toBe(leader.getComponent(Transform));
    });

    it('hydrates Vec3 properties from serialized data', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/vec3-hydration',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('node/follower', 'Follower', {
                    components: [
                        makeTransformComponent(),
                        makeComponent('TestFollowTarget', {
                            offset: { x: 7, y: -3, z: 12 },
                        }),
                    ],
                }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab);
        const follow = actors[0]!.getComponent(TestFollowTarget)!;

        expect(follow.offset).toBeInstanceOf(Vec3);
        expect(follow.offset.x).toBeCloseTo(7);
        expect(follow.offset.y).toBeCloseTo(-3);
        expect(follow.offset.z).toBeCloseTo(12);
    });
});

// ---------------------------------------------------------------------------
// Test Group 5: Prefab Overrides
// ---------------------------------------------------------------------------
describe('Prefab Instantiation — Prefab Overrides', () => {
    let harness: PrefabTestHarness;

    beforeEach(() => {
        harness = createPrefabTestHarness();
    });

    it('applies set-actor-field override to change actor name', () => {
        const prefab = makeSimplePrefab('prefab/override-name', 'Original');

        const actors = harness.actorRuntime.instantiatePrefab(prefab, {
            liveOverrides: [
                {
                    kind: 'set-actor-field',
                    nodeId: 'root',
                    field: 'name',
                    value: 'Overridden',
                },
            ],
        });

        expect(actors[0]!.name).toBe('Overridden');
    });

    it('applies set-actor-field override to change active state', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/override-active',
            kind: 'prefab',
            actors: [makeActorSnapshot('root', 'Actor', { active: true })],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab, {
            liveOverrides: [
                {
                    kind: 'set-actor-field',
                    nodeId: 'root',
                    field: 'active',
                    value: false,
                },
            ],
        });

        expect(actors[0]!.active).toBe(false);
    });

    it('applies set-component-property override to modify component data', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/override-comp',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'Actor', {
                    components: [
                        makeTransformComponent(),
                        makeComponent('TestHealth', {
                            maxHealth: 100,
                            currentHealth: 100,
                            isInvincible: false,
                        }, 'cmp-health'),
                    ],
                }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab, {
            liveOverrides: [
                {
                    kind: 'set-component-property',
                    nodeId: 'root',
                    selector: { kind: 'id', componentId: 'cmp-health', type: 'TestHealth' },
                    path: ['maxHealth'],
                    value: encodeSceneValue(999),
                },
            ],
        });

        const health = actors[0]!.getComponent(TestHealth)!;
        expect(health.maxHealth).toBe(999);
    });

    it('applies add-actor override to add a new actor to the prefab', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/override-add-actor',
            kind: 'prefab',
            actors: [makeActorSnapshot('root', 'Root')],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab, {
            liveOverrides: [
                {
                    kind: 'add-actor',
                    actor: makeActorSnapshot('extra', 'ExtraActor', {
                        parentNodeId: 'root',
                    }),
                },
            ],
        });

        expect(actors).toHaveLength(2);
        const extra = actors.find((a) => a.name === 'ExtraActor');
        expect(extra).toBeDefined();
    });

    it('applies remove-actor override to remove an actor from the prefab', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/override-remove',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'Root'),
                makeActorSnapshot('disposable', 'Disposable', { parentNodeId: 'root' }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab, {
            liveOverrides: [
                { kind: 'remove-actor', nodeId: 'disposable' },
            ],
        });

        expect(actors).toHaveLength(1);
        expect(actors[0]!.name).toBe('Root');
    });

    it('applies multiple overrides simultaneously', () => {
        const prefab: ScenePrefabDefinition = {
            id: 'prefab/multi-override',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'Original', {
                    components: [
                        makeTransformComponent([0, 0, 0]),
                        makeComponent('TestHealth', {
                            maxHealth: 100,
                            currentHealth: 100,
                            isInvincible: false,
                        }, 'cmp-health'),
                    ],
                }),
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(prefab, {
            liveOverrides: [
                {
                    kind: 'set-actor-field',
                    nodeId: 'root',
                    field: 'name',
                    value: 'Renamed',
                },
                {
                    kind: 'set-component-property',
                    nodeId: 'root',
                    selector: { kind: 'id', componentId: 'cmp-health', type: 'TestHealth' },
                    path: ['isInvincible'],
                    value: encodeSceneValue(true),
                },
            ],
        });

        expect(actors[0]!.name).toBe('Renamed');
        expect(actors[0]!.getComponent(TestHealth)!.isInvincible).toBe(true);
    });
});

// ---------------------------------------------------------------------------
// Test Group 6: Prefab Variant (Base + Derived)
// ---------------------------------------------------------------------------
describe('Prefab Instantiation — Prefab Variant', () => {
    let harness: PrefabTestHarness;

    beforeEach(() => {
        harness = createPrefabTestHarness();
    });

    it('instantiates a variant that extends a base prefab', () => {
        const basePrefab: ScenePrefabDefinition = {
            id: 'prefab/base-enemy',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'BaseEnemy', {
                    components: [
                        makeTransformComponent([0, 0, 0]),
                        makeComponent('TestHealth', {
                            maxHealth: 100,
                            currentHealth: 100,
                            isInvincible: false,
                        }, 'cmp-health'),
                    ],
                }),
            ],
        };

        const variant: ScenePrefabDefinition = {
            id: 'prefab/elite-enemy',
            kind: 'variant',
            base: { kind: 'registry', prefabId: 'prefab/base-enemy' },
            actors: [],
            overrides: [
                {
                    kind: 'set-actor-field',
                    nodeId: 'root',
                    field: 'name',
                    value: 'EliteEnemy',
                },
                {
                    kind: 'set-component-property',
                    nodeId: 'root',
                    selector: { kind: 'id', componentId: 'cmp-health', type: 'TestHealth' },
                    path: ['maxHealth'],
                    value: encodeSceneValue(500),
                },
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(variant, {
            prefabResolver: {
                resolvePrefab: (prefab) => {
                    const workflow = new ScenePrefabWorkflow({
                        prefabs: [basePrefab, variant],
                    });
                    return workflow.resolvePrefab(prefab);
                },
            },
        });

        expect(actors).toHaveLength(1);
        expect(actors[0]!.name).toBe('EliteEnemy');
        expect(actors[0]!.getComponent(TestHealth)!.maxHealth).toBe(500);
    });

    it('variant inherits base actors when no actor overrides exist', () => {
        const basePrefab: ScenePrefabDefinition = {
            id: 'prefab/base-structure',
            kind: 'prefab',
            actors: [
                makeActorSnapshot('root', 'BaseRoot'),
                makeActorSnapshot('child', 'BaseChild', { parentNodeId: 'root' }),
            ],
        };

        const variant: ScenePrefabDefinition = {
            id: 'prefab/derived-structure',
            kind: 'variant',
            base: { kind: 'registry', prefabId: 'prefab/base-structure' },
            actors: [],
            overrides: [],
        };

        const actors = harness.actorRuntime.instantiatePrefab(variant, {
            prefabResolver: {
                resolvePrefab: (prefab) => {
                    const workflow = new ScenePrefabWorkflow({
                        prefabs: [basePrefab, variant],
                    });
                    return workflow.resolvePrefab(prefab);
                },
            },
        });

        expect(actors).toHaveLength(2);
        expect(actors.map((a) => a.name)).toEqual(['BaseRoot', 'BaseChild']);
    });

    it('variant can add new actors on top of base', () => {
        const basePrefab: ScenePrefabDefinition = {
            id: 'prefab/base-with-actor',
            kind: 'prefab',
            actors: [makeActorSnapshot('root', 'BaseRoot')],
        };

        const variant: ScenePrefabDefinition = {
            id: 'prefab/variant-extra-actor',
            kind: 'variant',
            base: { kind: 'registry', prefabId: 'prefab/base-with-actor' },
            actors: [],
            overrides: [
                {
                    kind: 'add-actor',
                    actor: makeActorSnapshot('extra', 'ExtraFromVariant', {
                        parentNodeId: 'root',
                    }),
                },
            ],
        };

        const actors = harness.actorRuntime.instantiatePrefab(variant, {
            prefabResolver: {
                resolvePrefab: (prefab) => {
                    const workflow = new ScenePrefabWorkflow({
                        prefabs: [basePrefab, variant],
                    });
                    return workflow.resolvePrefab(prefab);
                },
            },
        });

        expect(actors).toHaveLength(2);
        const extra = actors.find((a) => a.name === 'ExtraFromVariant');
        expect(extra).toBeDefined();
        expect(extra!.getComponent(Hierarchy)!.parentActor).toBe(actors[0]);
    });

    it('resolved variant has lineage including both base and variant IDs', () => {
        const basePrefab: ScenePrefabDefinition = {
            id: 'prefab/lineage-base',
            kind: 'prefab',
            actors: [makeActorSnapshot('root', 'Base')],
        };

        const variant: ScenePrefabDefinition = {
            id: 'prefab/lineage-variant',
            kind: 'variant',
            base: { kind: 'registry', prefabId: 'prefab/lineage-base' },
            actors: [],
        };

        let resolvedLineage: readonly string[] = [];

        harness.actorRuntime.instantiatePrefab(variant, {
            prefabResolver: {
                resolvePrefab: (prefab) => {
                    const workflow = new ScenePrefabWorkflow({
                        prefabs: [basePrefab, variant],
                    });
                    const result = workflow.resolvePrefab(prefab);
                    resolvedLineage = result.definition.lineage;
                    return result;
                },
            },
        });

        expect(resolvedLineage).toContain('prefab/lineage-base');
        expect(resolvedLineage).toContain('prefab/lineage-variant');
    });
});
