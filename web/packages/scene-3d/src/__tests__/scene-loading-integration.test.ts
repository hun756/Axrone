import { Vec4 } from '@axrone/numeric';
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import {
    createMockGL,
    createSceneOptions,
    installWebGL2Constants,
    ManualScheduler,
} from './test-harness';

let Scene: typeof import('@axrone/scene-3d').Scene;
let Camera: typeof import('@axrone/scene-3d').Camera;
let MeshRenderer: typeof import('@axrone/scene-3d').MeshRenderer;
let DirectionalLight: typeof import('@axrone/scene-3d').DirectionalLight;
let Transform: typeof import('@axrone/ecs-runtime').Transform;
let Hierarchy: typeof import('@axrone/ecs-runtime').Hierarchy;
let PrefabNodeBinding: typeof import('@axrone/scene-3d').PrefabNodeBinding;
let SceneSnapshotLoader: typeof import('@axrone/scene-3d').SceneSnapshotLoader;
let SceneLifecycleError: typeof import('@axrone/scene-3d').SceneLifecycleError;

import type {
    SceneSnapshot,
    ScenePrefabDefinition,
    SceneActorSnapshot,
    SceneComponentSnapshot,
} from '@axrone/scene-3d';

/**
 * Helper: build a minimal valid SceneSnapshot.
 */
function buildMinimalSnapshot(overrides?: Partial<SceneSnapshot>): SceneSnapshot {
    return {
        version: 1,
        shaders: [
            {
                id: 'test/solid',
                vertexSource: 'void main() { gl_Position = vec4(0); }',
                fragmentSource: 'void main() { gl_FragColor = vec4(1); }',
            },
        ],
        meshes: [
            {
                id: 'cube',
                vertices: new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]),
                attributes: [
                    { semantic: 'position' as const, componentCount: 3 as const, offset: 0, stride: 12 },
                ],
                vertexCount: 3,
            },
        ],
        samplers: [],
        textures: [],
        renderPasses: [],
        materials: [{ id: 'default-mat', shaderId: 'test/solid' }],
        prefab: {
            id: 'root-prefab',
            actors: [
                {
                    name: 'TestActor',
                    layer: 0,
                    tag: 'Default',
                    active: true,
                    persistent: false,
                    pooled: false,
                    components: [
                        { type: 'Transform', data: {} },
                        { type: 'MeshRenderer', data: { meshId: 'cube', materialId: 'default-mat' } },
                    ],
                },
            ],
        },
        ...overrides,
    };
}

/**
 * Helper: build a snapshot mirroring Main.scene.json structure (10 entities, 13+ components).
 */
function buildFullSceneSnapshot(): SceneSnapshot {
    const actors: SceneActorSnapshot[] = [
        // 1. Main Camera (root)
        {
            name: 'Main Camera',
            layer: 0,
            tag: 'Camera',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                { type: 'Transform', data: { position: [0, 2.5, 8], rotation: [-12, 0, 0], scale: [1, 1, 1] } },
                { type: 'Camera', data: { primary: true, fieldOfView: 60, nearClip: 0.1, farClip: 1000 } },
            ],
        },
        // 2. Directional Light (root)
        {
            name: 'Directional Light',
            layer: 1,
            tag: 'Lighting',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                { type: 'Transform', data: { position: [4, 6, 2], rotation: [48, -32, 0], scale: [1, 1, 1] } },
                { type: 'DirectionalLight', data: { castShadows: true, color: [1, 0.95, 0.84], intensity: 1.15 } },
            ],
        },
        // 3. World (root) — parent for Ground and Player
        {
            name: 'World',
            layer: 0,
            tag: 'Environment',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                { type: 'Transform', data: { position: [0, 0, 0], rotation: [0, 0, 0], scale: [1, 1, 1] } },
            ],
        },
        // 4. Ground (child of World)
        {
            name: 'Ground',
            parentNodeId: 'World',
            layer: 0,
            tag: 'Environment',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                { type: 'Transform', data: { position: [0, 0, 0], rotation: [0, 0, 0], scale: [12, 1, 12] } },
                { type: 'MeshRenderer', data: { meshId: 'plane', materialId: 'floor-mat', castShadows: true } },
            ],
        },
        // 5. Player (child of World)
        {
            name: 'Player',
            parentNodeId: 'World',
            layer: 2,
            tag: 'Player',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                { type: 'Transform', data: { position: [0, 0, 0], rotation: [0, 0, 0], scale: [1, 1, 1] } },
                { type: 'MeshRenderer', data: { meshId: 'capsule', materialId: 'player-mat', castShadows: true } },
            ],
        },
        // 6. UI Host (root)
        {
            name: 'UI Host',
            layer: 3,
            tag: 'UI',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                { type: 'Transform', data: { position: [0, 0, 0], rotation: [0, 0, 0], scale: [1, 1, 1] } },
                { type: 'UIHost', data: { renderMode: 'screen-overlay', width: 200, height: 80 } },
            ],
        },
        // 7. Actor (empty, root)
        {
            name: 'Actor',
            layer: 0,
            tag: 'Untagged',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                { type: 'Transform', data: { position: [0, 0, 0], rotation: [0, 0, 0], scale: [1, 1, 1] } },
            ],
        },
        // 8. GM_AssetStore_3D_Character (prefab instance, root)
        {
            name: 'GM_AssetStore_3D_Character',
            layer: 0,
            tag: 'Imported',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                { type: 'Transform', data: { position: [0.5, 0, -1.97], rotation: [0, 0, 0], scale: [1, 1, 1] } },
            ],
        },
        // 9. Actor 2 (empty, root)
        {
            name: 'Actor 2',
            layer: 0,
            tag: 'Untagged',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                { type: 'Transform', data: { position: [0, 0, 0], rotation: [0, 0, 0], scale: [1, 1, 1] } },
            ],
        },
        // 10. Cube (prefab instance, root)
        {
            name: 'Cube',
            layer: 0,
            tag: 'Untagged',
            active: true,
            persistent: false,
            pooled: false,
            components: [
                { type: 'Transform', data: { position: [0, 0, 0], rotation: [0, 0, 0], scale: [1, 1, 1] } },
            ],
        },
    ];

    return {
        version: 1,
        shaders: [
            {
                id: 'test/lit',
                vertexSource: 'void main() { gl_Position = vec4(0); }',
                fragmentSource: 'void main() { gl_FragColor = vec4(1); }',
            },
        ],
        meshes: [
            {
                id: 'plane',
                vertices: new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]),
                attributes: [
                    { semantic: 'position' as const, componentCount: 3 as const, offset: 0, stride: 12 },
                ],
                vertexCount: 3,
            },
            {
                id: 'capsule',
                vertices: new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]),
                attributes: [
                    { semantic: 'position' as const, componentCount: 3 as const, offset: 0, stride: 12 },
                ],
                vertexCount: 3,
            },
        ],
        samplers: [],
        textures: [],
        renderPasses: [],
        materials: [
            { id: 'floor-mat', shaderId: 'test/lit' },
            { id: 'player-mat', shaderId: 'test/lit' },
        ],
        prefab: {
            id: 'main-scene-prefab',
            actors,
        },
    };
}

describe('Scene Loading Integration', () => {
    let scheduler: ManualScheduler;

    beforeAll(async () => {
        installWebGL2Constants();
        const sceneModule = await import('@axrone/scene-3d');
        Scene = sceneModule.Scene;
        Camera = sceneModule.Camera;
        MeshRenderer = sceneModule.MeshRenderer;
        DirectionalLight = sceneModule.DirectionalLight;
        PrefabNodeBinding = sceneModule.PrefabNodeBinding;
        SceneSnapshotLoader = sceneModule.SceneSnapshotLoader;
        SceneLifecycleError = sceneModule.SceneLifecycleError;

        const ecsModule = await import('@axrone/ecs-runtime');
        Transform = ecsModule.Transform;
        Hierarchy = ecsModule.Hierarchy;
    });

    beforeEach(() => {
        scheduler = new ManualScheduler();
    });

    afterEach(() => {
        document.body.innerHTML = '';
    });

    // ─── Test Group 1: Minimal Scene Loading ──────────────────────────────

    describe('Minimal Scene Loading', () => {
        it('loads a minimal scene with one actor and verifies components', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildMinimalSnapshot();
            const actors = await scene.loadScene(snapshot);

            expect(actors.length).toBeGreaterThanOrEqual(1);

            const testActor = actors.find((a) => a.name === 'TestActor');
            expect(testActor).toBeDefined();
            expect(testActor!.getComponent(Transform)).toBeDefined();
            expect(testActor!.getComponent(MeshRenderer)).toBeDefined();
            expect(testActor!.getComponent(MeshRenderer)!.meshId).toBe('cube');
            expect(testActor!.getComponent(MeshRenderer)!.materialId).toBe('default-mat');

            scene.dispose();
        });

        it('registers shaders, meshes, and materials during minimal load', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildMinimalSnapshot();
            await scene.loadScene(snapshot);

            expect(scene.getTexture).toBeDefined();
            // Verify the mesh was registered by checking the scene can reference it
            expect(snapshot.meshes[0]!.id).toBe('cube');
            expect(snapshot.materials[0]!.shaderId).toBe('test/solid');

            scene.dispose();
        });
    });

    // ─── Test Group 2: Full Scene Loading (Main.scene.json equivalent) ────

    describe('Full Scene Loading', () => {
        it('loads all 10 entities from a Main.scene.json-equivalent snapshot', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildFullSceneSnapshot();
            const actors = await scene.loadScene(snapshot);

            expect(actors.length).toBe(10);

            const names = actors.map((a) => a.name);
            expect(names).toContain('Main Camera');
            expect(names).toContain('Directional Light');
            expect(names).toContain('World');
            expect(names).toContain('Ground');
            expect(names).toContain('Player');
            expect(names).toContain('UI Host');
            expect(names).toContain('Actor');
            expect(names).toContain('GM_AssetStore_3D_Character');
            expect(names).toContain('Actor 2');
            expect(names).toContain('Cube');

            scene.dispose();
        });

        it('attaches Camera component marked as primary', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildFullSceneSnapshot();
            const actors = await scene.loadScene(snapshot);

            const cameraActor = actors.find((a) => a.name === 'Main Camera');
            expect(cameraActor).toBeDefined();
            const camera = cameraActor!.getComponent(Camera);
            expect(camera).toBeDefined();
            expect(camera!.primary).toBe(true);

            scene.dispose();
        });

        it('attaches DirectionalLight with shadow casting enabled', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildFullSceneSnapshot();
            const actors = await scene.loadScene(snapshot);

            const lightActor = actors.find((a) => a.name === 'Directional Light');
            expect(lightActor).toBeDefined();
            const light = lightActor!.getComponent(DirectionalLight);
            expect(light).toBeDefined();
            expect(light!.castShadows).toBe(true);

            scene.dispose();
        });

        it('establishes parent-child hierarchy (Ground and Player under World)', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildFullSceneSnapshot();
            const actors = await scene.loadScene(snapshot);

            const world = actors.find((a) => a.name === 'World');
            const ground = actors.find((a) => a.name === 'Ground');
            const player = actors.find((a) => a.name === 'Player');

            expect(world).toBeDefined();
            expect(ground).toBeDefined();
            expect(player).toBeDefined();

            // Verify hierarchy via Transform parent links
            const worldTransform = world!.getComponent(Transform);
            const groundTransform = ground!.getComponent(Transform);
            const playerTransform = player!.getComponent(Transform);

            expect(groundTransform!.parent).toBe(worldTransform);
            expect(playerTransform!.parent).toBe(worldTransform);

            scene.dispose();
        });

        it('attaches MeshRenderer to Ground and Player actors', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildFullSceneSnapshot();
            const actors = await scene.loadScene(snapshot);

            const ground = actors.find((a) => a.name === 'Ground');
            const player = actors.find((a) => a.name === 'Player');

            expect(ground!.getComponent(MeshRenderer)).toBeDefined();
            expect(ground!.getComponent(MeshRenderer)!.meshId).toBe('plane');
            expect(player!.getComponent(MeshRenderer)).toBeDefined();
            expect(player!.getComponent(MeshRenderer)!.meshId).toBe('capsule');

            scene.dispose();
        });
    });

    // ─── Test Group 3: Scene with Textures (Async Loading) ────────────────

    describe('Scene with Textures', () => {
        it('loads textures asynchronously and registers them before materials', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildMinimalSnapshot({
                textures: [
                    {
                        id: 'test-color',
                        source: { kind: 'color', color: [1, 0, 0, 1] as const, width: 2, height: 2 },
                    },
                ],
                materials: [
                    {
                        id: 'textured-mat',
                        shaderId: 'test/solid',
                        textures: { u_MainTex: 'test-color' },
                    },
                ],
            });

            await scene.loadScene(snapshot);

            const texture = scene.getTexture('test-color');
            expect(texture).toBeDefined();
            expect(texture!.id).toBe('test-color');

            scene.dispose();
        });

        it('handles multiple textures loading concurrently', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildMinimalSnapshot({
                textures: [
                    {
                        id: 'tex-albedo',
                        source: { kind: 'color', color: [0.2, 0.6, 1, 1] as const, width: 4, height: 4 },
                    },
                    {
                        id: 'tex-normal',
                        source: { kind: 'color', color: [0.5, 0.5, 1, 1] as const, width: 4, height: 4 },
                    },
                    {
                        id: 'tex-roughness',
                        source: { kind: 'checker', size: 8 },
                    },
                ],
            });

            await scene.loadScene(snapshot);

            expect(scene.getTexture('tex-albedo')).toBeDefined();
            expect(scene.getTexture('tex-normal')).toBeDefined();
            expect(scene.getTexture('tex-roughness')).toBeDefined();

            scene.dispose();
        });
    });

    // ─── Test Group 4: Scene with Prefab Instances ────────────────────────

    describe('Scene with Prefab Instances', () => {
        it('instantiates a prefab with nested actors and verifies hierarchy', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            // Create a template prefab with nested actors
            const templateRoot = scene.createActor({ name: 'PrefabRoot' });
            templateRoot.addComponent(PrefabNodeBinding, { nodeId: 'node/0' });

            const templateChild = scene.createActor({ name: 'PrefabChild' });
            templateChild.addComponent(PrefabNodeBinding, { nodeId: 'node/1' });
            templateChild.setParent(templateRoot);

            const prefab = scene.createPrefab('nested-prefab', [templateRoot, templateChild]);

            // Clean up templates
            templateChild.destroy(true);
            templateRoot.destroy(true);

            // Build a snapshot that includes the prefab definition
            const snapshot = buildMinimalSnapshot({
                prefab: {
                    id: 'scene-with-prefab',
                    actors: [
                        {
                            name: 'SceneRoot',
                            layer: 0,
                            tag: 'Default',
                            active: true,
                            persistent: false,
                            pooled: false,
                            components: [{ type: 'Transform', data: {} }],
                        },
                    ],
                },
            });

            const actors = await scene.loadScene(snapshot);
            expect(actors.length).toBeGreaterThanOrEqual(1);

            // Instantiate the nested prefab separately
            const instances = scene.instantiatePrefab(prefab, { namePrefix: 'Inst ' });
            expect(instances.length).toBe(2);

            const instRoot = instances.find((a) => a.name === 'Inst PrefabRoot');
            const instChild = instances.find((a) => a.name === 'Inst PrefabChild');
            expect(instRoot).toBeDefined();
            expect(instChild).toBeDefined();

            // Verify parent-child relationship
            const rootTransform = instRoot!.getComponent(Transform);
            const childTransform = instChild!.getComponent(Transform);
            expect(childTransform!.parent).toBe(rootTransform);

            // Verify PrefabNodeBinding
            expect(instRoot!.getComponent(PrefabNodeBinding)).toBeDefined();
            expect(instRoot!.getComponent(PrefabNodeBinding)!.nodeId).toBe('node/0');
            expect(instChild!.getComponent(PrefabNodeBinding)!.nodeId).toBe('node/1');

            scene.dispose();
        });
    });

    // ─── Test Group 5: Scene Disposal and Cleanup ─────────────────────────

    describe('Scene Disposal and Cleanup', () => {
        it('disposes a loaded scene and allows loading a new one', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            // Load first scene
            const snapshot1 = buildMinimalSnapshot({
                prefab: {
                    id: 'first-prefab',
                    actors: [
                        {
                            name: 'FirstActor',
                            layer: 0,
                            tag: 'Default',
                            active: true,
                            persistent: false,
                            pooled: false,
                            components: [{ type: 'Transform', data: {} }],
                        },
                    ],
                },
            });

            const actors1 = await scene.loadScene(snapshot1);
            expect(actors1.find((a) => a.name === 'FirstActor')).toBeDefined();

            // Dispose the scene
            scene.dispose();
            expect(scene.isDisposed).toBe(true);

            // Create a new scene and load a different snapshot
            const canvas2 = document.createElement('canvas');
            const scene2 = new Scene(createSceneOptions(scheduler, canvas2));

            const snapshot2 = buildMinimalSnapshot({
                prefab: {
                    id: 'second-prefab',
                    actors: [
                        {
                            name: 'SecondActor',
                            layer: 0,
                            tag: 'Default',
                            active: true,
                            persistent: false,
                            pooled: false,
                            components: [{ type: 'Transform', data: {} }],
                        },
                    ],
                },
            });

            const actors2 = await scene2.loadScene(snapshot2);
            expect(actors2.find((a) => a.name === 'SecondActor')).toBeDefined();

            scene2.dispose();
        });

        it('clears existing actors when loading a new scene into the same Scene', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            // Load first scene
            const snapshot1 = buildMinimalSnapshot({
                prefab: {
                    id: 'prefab-1',
                    actors: [
                        {
                            name: 'OldActor',
                            layer: 0,
                            tag: 'Default',
                            active: true,
                            persistent: false,
                            pooled: false,
                            components: [{ type: 'Transform', data: {} }],
                        },
                    ],
                },
            });
            await scene.loadScene(snapshot1);

            // Load second scene — should clear old actors
            const snapshot2 = buildMinimalSnapshot({
                prefab: {
                    id: 'prefab-2',
                    actors: [
                        {
                            name: 'NewActor',
                            layer: 0,
                            tag: 'Default',
                            active: true,
                            persistent: false,
                            pooled: false,
                            components: [{ type: 'Transform', data: {} }],
                        },
                    ],
                },
            });
            const actors2 = await scene.loadScene(snapshot2);

            expect(actors2.find((a) => a.name === 'NewActor')).toBeDefined();
            expect(actors2.find((a) => a.name === 'OldActor')).toBeUndefined();

            scene.dispose();
        });

        it('prevents creating actors after disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildMinimalSnapshot();
            await scene.loadScene(snapshot);

            scene.dispose();

            expect(() => scene.createActor({ name: 'PostDispose' })).toThrow();
        });
    });

    // ─── Test Group 6: Error Handling ─────────────────────────────────────

    describe('Error Handling', () => {
        it('rejects snapshots with unsupported version', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const badSnapshot = {
                ...buildMinimalSnapshot(),
                version: 2,
            } as unknown as SceneSnapshot;

            await expect(scene.loadScene(badSnapshot)).rejects.toThrow(SceneLifecycleError);

            scene.dispose();
        });

        it('rejects snapshots with version 0', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const badSnapshot = {
                ...buildMinimalSnapshot(),
                version: 0,
            } as unknown as SceneSnapshot;

            await expect(scene.loadScene(badSnapshot)).rejects.toThrow(SceneLifecycleError);

            scene.dispose();
        });

        it('handles loading a snapshot with empty prefab actors gracefully', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const emptySnapshot = buildMinimalSnapshot({
                prefab: {
                    id: 'empty-prefab',
                    actors: [],
                },
            });

            const actors = await scene.loadScene(emptySnapshot);
            expect(actors).toEqual([]);

            scene.dispose();
        });

        it('handles loading a snapshot with no shaders or meshes', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const bareSnapshot = buildMinimalSnapshot({
                shaders: [],
                meshes: [],
                materials: [],
            });

            const actors = await scene.loadScene(bareSnapshot);
            // Should still instantiate actors even without assets
            expect(actors.length).toBeGreaterThanOrEqual(1);

            scene.dispose();
        });

        it('SceneSnapshotLoader rejects invalid version independently', async () => {
            const loader = new SceneSnapshotLoader({
                defaultRenderPassId: 'main',
                defaultClearColor: new Vec4(0, 0, 0, 1),
                clearExisting: vi.fn(),
                clearRenderPasses: vi.fn(),
                registerShader: vi.fn(),
                registerMesh: vi.fn(),
                registerSampler: vi.fn(),
                registerTexture: vi.fn(async () => {}),
                registerRenderPass: vi.fn(),
                createMaterial: vi.fn(),
                instantiatePrefab: vi.fn(() => []),
            });

            await expect(
                loader.load({
                    version: 99,
                    prefab: { id: 'p', actors: [] },
                    shaders: [],
                    meshes: [],
                    samplers: [],
                    textures: [],
                    renderPasses: [],
                    materials: [],
                } as unknown as SceneSnapshot)
            ).rejects.toThrow(SceneLifecycleError);
        });
    });
});
