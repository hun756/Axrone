import { afterEach, beforeAll, beforeEach, describe, expect, it } from 'vitest';
import { Transform } from '@axrone/ecs-runtime';
import { createSceneOptions, installWebGL2Constants, ManualScheduler } from './test-harness';

let Scene2D: typeof import('@axrone/scene-2d').Scene2D;
let SpriteRenderer: typeof import('@axrone/scene-2d').SpriteRenderer;

describe('Scene2D bulk sprite creation', () => {
    let scheduler: ManualScheduler;

    beforeAll(async () => {
        installWebGL2Constants();
        const sceneModule = await import('@axrone/scene-2d');
        Scene2D = sceneModule.Scene2D;
        SpriteRenderer = sceneModule.SpriteRenderer;
    });

    beforeEach(() => {
        scheduler = new ManualScheduler();
    });

    afterEach(() => {
        document.body.innerHTML = '';
    });

    it('creates sprites with components and optional initial transforms', () => {
        const canvas = document.createElement('canvas');
        const scene = new Scene2D(createSceneOptions(scheduler, canvas));

        try {
            const actors = scene.createSpriteActors([
                {
                    actorConfig: { name: 'A' },
                    spriteConfig: { textureId: 'hero', size: [2, 3] },
                    position: [1, 2, 0],
                },
                {
                    actorConfig: { name: 'B' },
                    // Non-homogeneous config still creates; only the render
                    // fast path declines it.
                    spriteConfig: { textureId: 'hero', sliceBorder: [1, 1, 1, 1] },
                },
                {
                    actorConfig: { name: 'C' },
                    spriteConfig: { textureId: 'hero', size: [1, 1] },
                    position: [4, 5, 0],
                    rotation: [0, 0, Math.SQRT1_2, Math.SQRT1_2],
                    scale: [2, 2, 1],
                },
            ]);

            expect(actors.length).toBe(3);
            expect(actors.map((actor) => actor.name)).toEqual(['A', 'B', 'C']);
            for (const actor of actors) {
                expect(actor.getComponent(Transform)).toBeDefined();
                expect(actor.getComponent(SpriteRenderer)).toBeDefined();
            }

            const first = actors[0]!.getComponent(Transform)!;
            expect(first.position.x).toBe(1);
            expect(first.position.y).toBe(2);
            expect(first.position.z).toBe(0);
            expect(actors[0]!.getComponent(SpriteRenderer)!.textureId).toBe('hero');

            const third = actors[2]!.getComponent(Transform)!;
            expect(third.position.x).toBe(4);
            expect(third.position.y).toBe(5);
            expect(third.scale.x).toBe(2);
            expect(third.rotation.z).toBeCloseTo(Math.SQRT1_2);

            expect(scene.createSpriteActors([])).toEqual([]);
        } finally {
            scene.dispose();
        }
    });
});
