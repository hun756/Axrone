import { Actor } from '@axrone/ecs-runtime/actor';
import { Component, Transform, World } from '@axrone/ecs-runtime';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

class TestComponent extends Component {
    value: number = 0;
}

describe('World tag and layer queries', () => {
    let world: World<any>;

    beforeEach(() => {
        const registry = {
            Transform,
            TestComponent,
        };
        world = new World(registry);
    });

    afterEach(() => {
        if (world && !world.isDisposed) {
            world.clear();
        }
    });

    const createActorWithTag = (name: string, tag: string): Actor => {
        return new Actor(world, { name, tag: tag as any });
    };

    const createActorWithLayer = (name: string, layer: number): Actor => {
        return new Actor(world, { name, layer: layer as any });
    };

    const createActorWithTagAndLayer = (name: string, tag: string, layer: number): Actor => {
        return new Actor(world, { name, tag: tag as any, layer: layer as any });
    };

    // ─── getActorsByTag ──────────────────────────────────────────────

    describe('getActorsByTag', () => {
        it('returns actors matching the given tag', () => {
            const enemy1 = createActorWithTag('Enemy1', 'Enemy');
            const enemy2 = createActorWithTag('Enemy2', 'Enemy');
            createActorWithTag('Player', 'Player');

            const enemies = world.getActorsByTag('Enemy');
            expect(enemies).toHaveLength(2);
            expect(enemies).toContain(enemy1);
            expect(enemies).toContain(enemy2);
        });

        it('returns empty array for unknown tag', () => {
            createActorWithTag('Actor1', 'SomeTag');
            const result = world.getActorsByTag('NonExistentTag');
            expect(result).toEqual([]);
        });

        it('returns empty array when no actors exist', () => {
            const result = world.getActorsByTag('AnyTag');
            expect(result).toEqual([]);
        });

        it('returns all actors with the Default tag when none specified', () => {
            const actor1 = new Actor(world, { name: 'Default1' });
            const actor2 = new Actor(world, { name: 'Default2' });

            const defaults = world.getActorsByTag('Default');
            expect(defaults).toHaveLength(2);
            expect(defaults).toContain(actor1);
            expect(defaults).toContain(actor2);
        });

        it('updates results when actor tag changes', () => {
            const actor = createActorWithTag('Actor1', 'TagA');

            expect(world.getActorsByTag('TagA')).toHaveLength(1);
            expect(world.getActorsByTag('TagB')).toHaveLength(0);

            actor.tag = 'TagB' as any;

            expect(world.getActorsByTag('TagA')).toHaveLength(0);
            expect(world.getActorsByTag('TagB')).toHaveLength(1);
            expect(world.getActorsByTag('TagB')).toContain(actor);
        });

        it('updates results when actor is unregistered', () => {
            const actor = createActorWithTag('Actor1', 'SomeTag');
            expect(world.getActorsByTag('SomeTag')).toHaveLength(1);

            actor.destroy();

            expect(world.getActorsByTag('SomeTag')).toHaveLength(0);
        });
    });

    // ─── getActorsByLayer ────────────────────────────────────────────

    describe('getActorsByLayer', () => {
        it('returns actors matching the given layer', () => {
            const bg1 = createActorWithLayer('Background1', 0);
            const bg2 = createActorWithLayer('Background2', 0);
            createActorWithLayer('Foreground', 1);

            const backgrounds = world.getActorsByLayer(0);
            expect(backgrounds).toHaveLength(2);
            expect(backgrounds).toContain(bg1);
            expect(backgrounds).toContain(bg2);
        });

        it('returns empty array for unknown layer', () => {
            createActorWithLayer('Actor1', 5);
            const result = world.getActorsByLayer(99);
            expect(result).toEqual([]);
        });

        it('returns empty array when no actors exist', () => {
            const result = world.getActorsByLayer(0);
            expect(result).toEqual([]);
        });

        it('returns actors on default layer 0 when none specified', () => {
            const actor1 = new Actor(world, { name: 'Layer0-1' });
            const actor2 = new Actor(world, { name: 'Layer0-2' });

            const layer0 = world.getActorsByLayer(0);
            expect(layer0).toHaveLength(2);
            expect(layer0).toContain(actor1);
            expect(layer0).toContain(actor2);
        });

        it('updates results when actor layer changes', () => {
            const actor = createActorWithLayer('Actor1', 1);

            expect(world.getActorsByLayer(1)).toHaveLength(1);
            expect(world.getActorsByLayer(2)).toHaveLength(0);

            actor.layer = 2 as any;

            expect(world.getActorsByLayer(1)).toHaveLength(0);
            expect(world.getActorsByLayer(2)).toHaveLength(1);
            expect(world.getActorsByLayer(2)).toContain(actor);
        });

        it('updates results when actor is unregistered', () => {
            const actor = createActorWithLayer('Actor1', 3);
            expect(world.getActorsByLayer(3)).toHaveLength(1);

            actor.destroy();

            expect(world.getActorsByLayer(3)).toHaveLength(0);
        });
    });

    // ─── Combined tag and layer queries ──────────────────────────────

    describe('combined tag and layer', () => {
        it('supports actors with different tags and layers independently', () => {
            const enemyGround = createActorWithTagAndLayer('EnemyGround', 'Enemy', 1);
            const enemyAir = createActorWithTagAndLayer('EnemyAir', 'Enemy', 2);
            const playerGround = createActorWithTagAndLayer('PlayerGround', 'Player', 1);

            const enemies = world.getActorsByTag('Enemy');
            expect(enemies).toHaveLength(2);
            expect(enemies).toContain(enemyGround);
            expect(enemies).toContain(enemyAir);

            const groundUnits = world.getActorsByLayer(1);
            expect(groundUnits).toHaveLength(2);
            expect(groundUnits).toContain(enemyGround);
            expect(groundUnits).toContain(playerGround);

            const airEnemies = world.getActorsByTag('Enemy');
            expect(airEnemies).toHaveLength(2);
        });
    });

    // ─── WorldActorRegistry direct tests ─────────────────────────────

    describe('WorldActorRegistry indices', () => {
        it('clear removes all index entries', () => {
            createActorWithTagAndLayer('Actor1', 'TagA', 1);
            createActorWithTagAndLayer('Actor2', 'TagA', 2);

            expect(world.getActorsByTag('TagA')).toHaveLength(2);
            expect(world.getActorsByLayer(1)).toHaveLength(1);

            world.clear();

            // After clear, world is disposed, so we create a new one
            const newRegistry = { Transform, TestComponent };
            const newWorld = new World(newRegistry);

            expect(newWorld.getActorsByTag('TagA')).toEqual([]);
            expect(newWorld.getActorsByLayer(1)).toEqual([]);

            newWorld.clear();
        });

        it('handles multiple actors with same tag and layer', () => {
            const actors = [];
            for (let i = 0; i < 5; i++) {
                actors.push(createActorWithTagAndLayer(`Actor${i}`, 'Shared', 0));
            }

            const shared = world.getActorsByTag('Shared');
            expect(shared).toHaveLength(5);

            const layer0 = world.getActorsByLayer(0);
            expect(layer0).toHaveLength(5);
        });

        it('unregistering one actor does not affect others with same tag', () => {
            const actor1 = createActorWithTag('Actor1', 'Shared');
            const actor2 = createActorWithTag('Actor2', 'Shared');
            const actor3 = createActorWithTag('Actor3', 'Shared');

            expect(world.getActorsByTag('Shared')).toHaveLength(3);

            actor2.destroy();

            const remaining = world.getActorsByTag('Shared');
            expect(remaining).toHaveLength(2);
            expect(remaining).toContain(actor1);
            expect(remaining).toContain(actor3);
        });
    });
});
