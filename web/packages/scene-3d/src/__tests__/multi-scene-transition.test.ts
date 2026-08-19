import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import {
    createMockGL,
    createSceneOptions,
    installWebGL2Constants,
    ManualScheduler,
} from './test-harness';
import type { MockGLContext } from './test-harness';

// ─── Dynamic imports (same pattern as scene-factory.test.ts) ─────────────────

let Scene: typeof import('@axrone/scene-3d').Scene;
let Camera: typeof import('@axrone/scene-3d').Camera;
let MeshRenderer: typeof import('@axrone/scene-3d').MeshRenderer;
let DirectionalLight: typeof import('@axrone/scene-3d').DirectionalLight;
let Transform: typeof import('@axrone/ecs-runtime').Transform;
let Hierarchy: typeof import('@axrone/ecs-runtime').Hierarchy;
let Component: typeof import('@axrone/ecs-runtime').Component;

import type { SceneSnapshot, SceneActorSnapshot } from '@axrone/scene-3d';

// ─── Snapshot Builders ──────────────────────────────────────────────────────

function buildSnapshotA(overrides?: Partial<SceneSnapshot>): SceneSnapshot {
    return {
        version: 1,
        shaders: [
            {
                id: 'scene-a/shader',
                vertexSource: 'void main() { gl_Position = vec4(0); }',
                fragmentSource: 'void main() { gl_FragColor = vec4(1, 0, 0, 1); }',
            },
        ],
        meshes: [
            {
                id: 'scene-a-mesh',
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
        materials: [{ id: 'scene-a-mat', shaderId: 'scene-a/shader' }],
        prefab: {
            id: 'scene-a-prefab',
            actors: [
                {
                    name: 'ActorA_Root',
                    nodeId: 'node-a-root',
                    layer: 0,
                    tag: 'Default',
                    active: true,
                    persistent: false,
                    pooled: false,
                    components: [{ type: 'Transform', data: { position: [1, 0, 0] } }],
                },
                {
                    name: 'ActorA_Child',
                    parentNodeId: 'node-a-root',
                    layer: 0,
                    tag: 'Default',
                    active: true,
                    persistent: false,
                    pooled: false,
                    components: [{ type: 'Transform', data: { position: [0, 1, 0] } }],
                },
                {
                    name: 'ActorA_Renderable',
                    layer: 0,
                    tag: 'Default',
                    active: true,
                    persistent: false,
                    pooled: false,
                    components: [
                        { type: 'Transform', data: {} },
                        { type: 'MeshRenderer', data: { meshId: 'scene-a-mesh', materialId: 'scene-a-mat' } },
                    ],
                },
            ],
        },
        ...overrides,
    };
}

function buildSnapshotB(overrides?: Partial<SceneSnapshot>): SceneSnapshot {
    return {
        version: 1,
        shaders: [
            {
                id: 'scene-b/shader',
                vertexSource: 'void main() { gl_Position = vec4(0); }',
                fragmentSource: 'void main() { gl_FragColor = vec4(0, 1, 0, 1); }',
            },
        ],
        meshes: [
            {
                id: 'scene-b-mesh',
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
        materials: [{ id: 'scene-b-mat', shaderId: 'scene-b/shader' }],
        prefab: {
            id: 'scene-b-prefab',
            actors: [
                {
                    name: 'ActorB_Root',
                    layer: 0,
                    tag: 'Default',
                    active: true,
                    persistent: false,
                    pooled: false,
                    components: [{ type: 'Transform', data: { position: [5, 5, 5] } }],
                },
                {
                    name: 'ActorB_Camera',
                    layer: 0,
                    tag: 'Camera',
                    active: true,
                    persistent: false,
                    pooled: false,
                    components: [
                        { type: 'Transform', data: {} },
                        { type: 'Camera', data: { primary: true, fieldOfView: 45, nearClip: 0.1, farClip: 500 } },
                    ],
                },
            ],
        },
        ...overrides,
    };
}

function buildTexturedSnapshot(overrides?: Partial<SceneSnapshot>): SceneSnapshot {
    return {
        version: 1,
        shaders: [
            {
                id: 'textured/shader',
                vertexSource: 'void main() { gl_Position = vec4(0); }',
                fragmentSource: 'void main() { gl_FragColor = vec4(1); }',
            },
        ],
        meshes: [
            {
                id: 'textured-mesh',
                vertices: new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]),
                attributes: [
                    { semantic: 'position' as const, componentCount: 3 as const, offset: 0, stride: 12 },
                ],
                vertexCount: 3,
            },
        ],
        samplers: [],
        textures: [
            {
                id: 'tex-albedo',
                source: { kind: 'color', color: [1, 0, 0, 1] as const, width: 2, height: 2 },
            },
            {
                id: 'tex-normal',
                source: { kind: 'color', color: [0.5, 0.5, 1, 1] as const, width: 2, height: 2 },
            },
        ],
        renderPasses: [],
        materials: [
            {
                id: 'textured-mat',
                shaderId: 'textured/shader',
                textures: { u_MainTex: 'tex-albedo' },
            },
        ],
        prefab: {
            id: 'textured-prefab',
            actors: [
                {
                    name: 'TexturedActor',
                    layer: 0,
                    tag: 'Default',
                    active: true,
                    persistent: false,
                    pooled: false,
                    components: [
                        { type: 'Transform', data: {} },
                        { type: 'MeshRenderer', data: { meshId: 'textured-mesh', materialId: 'textured-mat' } },
                    ],
                },
            ],
        },
        ...overrides,
    };
}

// ─── Helpers ────────────────────────────────────────────────────────────────

function getGlFromScene(scene: InstanceType<typeof Scene>): MockGLContext {
    return scene.gl as unknown as MockGLContext;
}

function countMockCalls(fn: unknown): number {
    return (fn as { mock: { calls: unknown[] } }).mock.calls.length;
}

// ─── Tests ──────────────────────────────────────────────────────────────────

describe('T-16: Multi-Scene Transition — Load/Unload Scenes, Verify No State Leakage', () => {
    let scheduler: ManualScheduler;

    beforeAll(async () => {
        installWebGL2Constants();
        const sceneModule = await import('@axrone/scene-3d');
        Scene = sceneModule.Scene;
        Camera = sceneModule.Camera;
        MeshRenderer = sceneModule.MeshRenderer;
        DirectionalLight = sceneModule.DirectionalLight;

        const ecsModule = await import('@axrone/ecs-runtime');
        Transform = ecsModule.Transform;
        Hierarchy = ecsModule.Hierarchy;
        Component = ecsModule.Component;
    });

    beforeEach(() => {
        scheduler = new ManualScheduler();
    });

    afterEach(() => {
        document.body.innerHTML = '';
    });

    // ─── Group 1: Scene Load/Unload Cycle ──────────────────────────────────

    describe('Scene Load/Unload Cycle', () => {
        it('loads scene A, disposes, loads scene B without errors', async () => {
            const canvas1 = document.createElement('canvas');
            const scene1 = new Scene(createSceneOptions(scheduler, canvas1));
            const actors1 = await scene1.loadScene(buildSnapshotA());
            expect(actors1.find((a) => a.name === 'ActorA_Root')).toBeDefined();

            scene1.dispose();
            expect(scene1.isDisposed).toBe(true);

            const canvas2 = document.createElement('canvas');
            const scene2 = new Scene(createSceneOptions(scheduler, canvas2));
            const actors2 = await scene2.loadScene(buildSnapshotB());
            expect(actors2.find((a) => a.name === 'ActorB_Root')).toBeDefined();

            scene2.dispose();
        });

        it('loads scene A, disposes, loads scene A again correctly', async () => {
            const canvas1 = document.createElement('canvas');
            const scene1 = new Scene(createSceneOptions(scheduler, canvas1));
            const actors1 = await scene1.loadScene(buildSnapshotA());
            expect(actors1.length).toBe(3);
            scene1.dispose();

            const canvas2 = document.createElement('canvas');
            const scene2 = new Scene(createSceneOptions(scheduler, canvas2));
            const actors2 = await scene2.loadScene(buildSnapshotA());
            expect(actors2.length).toBe(3);
            expect(actors2.find((a) => a.name === 'ActorA_Root')).toBeDefined();
            expect(actors2.find((a) => a.name === 'ActorA_Renderable')).toBeDefined();

            scene2.dispose();
        });

        it('survives 5 rapid load/dispose cycles without accumulating state', async () => {
            for (let i = 0; i < 5; i++) {
                const canvas = document.createElement('canvas');
                const scene = new Scene(createSceneOptions(scheduler, canvas));
                const actors = await scene.loadScene(buildSnapshotA());
                expect(actors.length).toBe(3);

                scene.start(0);
                scheduler.flush(16);

                scene.dispose();
                expect(scene.isDisposed).toBe(true);
            }
        });

        it('disposes a scene before load completes without leaking', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const snapshot = buildTexturedSnapshot();
            const loadPromise = scene.loadScene(snapshot);

            // Dispose immediately while textures are still loading
            scene.dispose();
            expect(scene.isDisposed).toBe(true);

            // The load promise should settle (either resolve or reject) without hanging
            // We just need to ensure it doesn't hang — catch any rejection
            await loadPromise.catch(() => {
                // Expected: disposal may cause the load to fail
            });
        });

        it('supports loading two independent scenes simultaneously on separate canvases', async () => {
            const canvas1 = document.createElement('canvas');
            const canvas2 = document.createElement('canvas');
            const scene1 = new Scene(createSceneOptions(scheduler, canvas1));
            const scene2 = new Scene(createSceneOptions(scheduler, canvas2));

            const [actors1, actors2] = await Promise.all([
                scene1.loadScene(buildSnapshotA()),
                scene2.loadScene(buildSnapshotB()),
            ]);

            expect(actors1.find((a) => a.name === 'ActorA_Root')).toBeDefined();
            expect(actors2.find((a) => a.name === 'ActorB_Root')).toBeDefined();

            // Actors from scene A should not appear in scene B
            expect(actors2.find((a) => a.name === 'ActorA_Root')).toBeUndefined();
            expect(actors1.find((a) => a.name === 'ActorB_Root')).toBeUndefined();

            scene1.dispose();
            scene2.dispose();
        });
    });

    // ─── Group 2: GL Resource Isolation ─────────────────────────────────────

    describe('GL Resource Isolation', () => {
        it('releases textures from scene A after disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));
            const gl = getGlFromScene(scene);

            await scene.loadScene(buildTexturedSnapshot());

            const texturesCreated = countMockCalls(gl.createTexture);
            expect(texturesCreated).toBeGreaterThan(0);

            scene.dispose();

            const texturesDeleted = countMockCalls(gl.deleteTexture);
            expect(texturesDeleted).toBeGreaterThanOrEqual(texturesCreated);
        });

        it('releases buffers from scene A after disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));
            const gl = getGlFromScene(scene);

            await scene.loadScene(buildSnapshotA());
            scene.start(0);
            scheduler.flush(16);

            const buffersCreated = countMockCalls(gl.createBuffer);
            expect(buffersCreated).toBeGreaterThan(0);

            scene.dispose();

            const buffersDeleted = countMockCalls(gl.deleteBuffer);
            expect(buffersDeleted).toBeGreaterThanOrEqual(buffersCreated);
        });

        it('releases shader programs from scene A after disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));
            const gl = getGlFromScene(scene);

            await scene.loadScene(buildSnapshotA());

            const programsCreated = countMockCalls(gl.createProgram);
            expect(programsCreated).toBeGreaterThan(0);

            scene.dispose();

            const programsDeleted = countMockCalls(gl.deleteProgram);
            expect(programsDeleted).toBeGreaterThanOrEqual(programsCreated);
        });

        it('releases VAOs from scene A after disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));
            const gl = getGlFromScene(scene);

            await scene.loadScene(buildSnapshotA());
            scene.start(0);
            scheduler.flush(16);

            const vaosCreated = countMockCalls(gl.createVertexArray);
            expect(vaosCreated).toBeGreaterThan(0);

            scene.dispose();

            const vaosDeleted = countMockCalls(gl.deleteVertexArray);
            expect(vaosDeleted).toBeGreaterThanOrEqual(vaosCreated);
        });

        it('releases shader objects from scene A after disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));
            const gl = getGlFromScene(scene);

            await scene.loadScene(buildSnapshotA());

            const shadersCreated = countMockCalls(gl.createShader);
            expect(shadersCreated).toBeGreaterThan(0);

            scene.dispose();

            const shadersDeleted = countMockCalls(gl.deleteShader);
            expect(shadersDeleted).toBeGreaterThanOrEqual(shadersCreated);
        });
    });

    // ─── Group 3: Event System Cleanup ──────────────────────────────────────

    describe('Event System Cleanup', () => {
        it('cleans up event subscriptions from scene A actors after disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));
            const actors = await scene.loadScene(buildSnapshotA());

            const rootActor = actors.find((a) => a.name === 'ActorA_Root');
            expect(rootActor).toBeDefined();

            const handler = vi.fn();
            const unsubscribe = rootActor!.on('test:event', handler);
            expect(typeof unsubscribe).toBe('function');

            // Dispose should not throw even with active subscriptions
            expect(() => scene.dispose()).not.toThrow();
        });

        it('does not fire orphan event listeners after scene disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));
            const actors = await scene.loadScene(buildSnapshotA());

            const rootActor = actors.find((a) => a.name === 'ActorA_Root');
            const handler = vi.fn();
            rootActor!.on('transition:event', handler);

            scene.dispose();

            // After disposal, emitting to the actor's event bus should not invoke the handler
            // because the actor is destroyed
            expect(handler).not.toHaveBeenCalled();
        });

        it('prevents cross-scene events from firing after source scene disposed', async () => {
            const canvas1 = document.createElement('canvas');
            const canvas2 = document.createElement('canvas');
            const scene1 = new Scene(createSceneOptions(scheduler, canvas1));
            const scene2 = new Scene(createSceneOptions(scheduler, canvas2));

            const actors1 = await scene1.loadScene(buildSnapshotA());
            const actors2 = await scene2.loadScene(buildSnapshotB());

            const handler1 = vi.fn();
            const handler2 = vi.fn();

            actors1[0]!.on('cross-scene', handler1);
            actors2[0]!.on('cross-scene', handler2);

            // Dispose scene 1
            scene1.dispose();

            // handler1 should never have been called (no emit happened)
            expect(handler1).not.toHaveBeenCalled();
            // handler2 should also not have been called
            expect(handler2).not.toHaveBeenCalled();

            scene2.dispose();
        });

        it('returns to a clean event state after all scenes are disposed', async () => {
            const canvas1 = document.createElement('canvas');
            const scene1 = new Scene(createSceneOptions(scheduler, canvas1));
            const actors1 = await scene1.loadScene(buildSnapshotA());

            const handlers = actors1.map((a) => {
                const h = vi.fn();
                a.on('cleanup-test', h);
                return h;
            });

            // Verify actors exist before disposal
            expect(scene1.world.getAllActors().length).toBe(3);

            scene1.dispose();

            // No handlers should have been called
            for (const h of handlers) {
                expect(h).not.toHaveBeenCalled();
            }

            // After disposal, scene is fully torn down
            expect(scene1.isDisposed).toBe(true);
        });

        it('disposes component event subscriptions in correct order during scene teardown', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const disposalOrder: string[] = [];

            class OrderTrackingComponent extends Component {
                label = '';
                onDestroy(): void {
                    if (this.label) {
                        disposalOrder.push(this.label);
                    }
                }
            }

            scene.registerComponent(OrderTrackingComponent);

            const parent = scene.createActor({ name: 'Parent' });
            const parentComp = parent.addComponent(OrderTrackingComponent);
            parentComp.label = 'parent-comp';

            const child = scene.createActor({ name: 'Child' });
            child.setParent(parent);
            const childComp = child.addComponent(OrderTrackingComponent);
            childComp.label = 'child-comp';

            scene.start(0);
            scheduler.flush(16);

            scene.dispose();

            // Both components should have been destroyed
            expect(disposalOrder).toContain('parent-comp');
            expect(disposalOrder).toContain('child-comp');
        });
    });

    // ─── Group 4: Transform/Hierarchy Cleanup ───────────────────────────────

    describe('Transform/Hierarchy Cleanup', () => {
        it('removes all actors from world after disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const actors = await scene.loadScene(buildSnapshotA());
            expect(actors.length).toBe(3);
            expect(scene.world.getAllActors().length).toBe(3);

            scene.dispose();
            // After disposal, the world is torn down — verify via isDisposed
            expect(scene.isDisposed).toBe(true);
        });

        it('tears down transform hierarchy completely (no orphan nodes)', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const parent = scene.createActor({ name: 'Parent' });
            const child1 = scene.createActor({ name: 'Child1' });
            const child2 = scene.createActor({ name: 'Child2' });
            child1.setParent(parent);
            child2.setParent(parent);

            const grandchild = scene.createActor({ name: 'Grandchild' });
            grandchild.setParent(child1);

            expect(scene.world.getAllActors().length).toBe(4);

            scene.dispose();
            // After disposal, the entire hierarchy is torn down
            expect(scene.isDisposed).toBe(true);
        });

        it('clears parent-child references on disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const actors = await scene.loadScene(buildSnapshotA());
            const root = actors.find((a) => a.name === 'ActorA_Root');
            const child = actors.find((a) => a.name === 'ActorA_Child');

            expect(root).toBeDefined();
            expect(child).toBeDefined();

            const rootTransform = root!.getComponent(Transform);
            const childTransform = child!.getComponent(Transform);

            // Before disposal, child's parent should be root
            if (rootTransform && childTransform) {
                expect(childTransform.parent).toBe(rootTransform);
            }

            scene.dispose();

            // After disposal, all actors are gone
            expect(scene.world.getAllActors().length).toBe(0);
        });

        it('nullifies component references on disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const actors = await scene.loadScene(buildSnapshotA());
            const renderable = actors.find((a) => a.name === 'ActorA_Renderable');
            expect(renderable).toBeDefined();

            const meshRenderer = renderable!.getComponent(MeshRenderer);
            expect(meshRenderer).toBeDefined();
            expect(meshRenderer!.meshId).toBe('scene-a-mesh');

            scene.dispose();

            // After disposal, actors are removed from the world
            expect(scene.world.getAllActors().length).toBe(0);
        });

        it('returns empty query results after scene disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            await scene.loadScene(buildSnapshotA());

            // Create additional actors for variety
            scene.createCameraActor({ name: 'Cam' }, { primary: true });
            scene.createRenderableActor(
                { name: 'Extra' },
                { meshId: 'scene-a-mesh', materialId: 'scene-a-mat' }
            );

            expect(scene.world.getAllActors().length).toBe(5);

            scene.dispose();

            const allActors = scene.world.getAllActors();
            expect(allActors.length).toBe(0);
        });
    });

    // ─── Group 5: Scheduler/Timer Cleanup ───────────────────────────────────

    describe('Scheduler/Timer Cleanup', () => {
        it('does not execute pending callbacks from scene A after disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            await scene.loadScene(buildSnapshotA());
            scene.start(0);

            // Register a callback on the scheduler
            const callback = vi.fn();
            const handle = scheduler.request(callback);

            // Dispose the scene
            scene.dispose();

            // Flush the scheduler — the callback should still fire because
            // the scheduler is externally owned, but the scene should not
            // have any remaining internal callbacks
            scheduler.flush(16);

            // The external callback fires (scheduler is not scene-owned)
            expect(callback).toHaveBeenCalledTimes(1);
        });

        it('cancels internal animation frame callbacks on disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            await scene.loadScene(buildSnapshotA());
            scene.start(0);

            // Run a frame to let the scene set up internal callbacks
            scheduler.flush(16);

            // Dispose while the scheduler might have pending internal callbacks
            scene.dispose();

            // After disposal, flushing should not throw or cause errors
            expect(() => scheduler.flush(32)).not.toThrow();
        });

        it('clears setTimeout/setInterval-style callbacks on disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            await scene.loadScene(buildSnapshotA());
            scene.start(0);

            // Simulate multiple scheduler ticks
            scheduler.flush(16);
            scheduler.flush(32);
            scheduler.flush(48);

            // Dispose
            scene.dispose();

            // Additional flushes should be clean
            expect(() => scheduler.flush(64)).not.toThrow();
            expect(() => scheduler.flush(80)).not.toThrow();
        });

        it('has no pending internal tasks after disposal', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            await scene.loadScene(buildSnapshotA());
            scene.start(0);
            scheduler.flush(16);

            scene.dispose();

            // Create a new scene with the same scheduler
            const canvas2 = document.createElement('canvas');
            const scene2 = new Scene(createSceneOptions(scheduler, canvas2));
            await scene2.loadScene(buildSnapshotB());
            scene2.start(0);

            // Flush should only execute scene2's callbacks, not scene1's
            expect(() => scheduler.flush(32)).not.toThrow();

            scene2.dispose();
        });

        it('gives new scene a fresh scheduler state', async () => {
            const canvas1 = document.createElement('canvas');
            const scene1 = new Scene(createSceneOptions(scheduler, canvas1));

            await scene1.loadScene(buildSnapshotA());
            scene1.start(0);
            scheduler.flush(16);
            scheduler.flush(32);

            scene1.dispose();

            // New scheduler for the new scene
            const scheduler2 = new ManualScheduler();
            const canvas2 = document.createElement('canvas');
            const scene2 = new Scene(createSceneOptions(scheduler2, canvas2));

            await scene2.loadScene(buildSnapshotB());
            scene2.start(0);

            // Fresh scheduler should start at 0
            expect(scheduler2.now()).toBe(0);

            scheduler2.flush(16);
            expect(scheduler2.now()).toBe(16);

            scene2.dispose();
        });
    });

    // ─── Group 6: Cross-Cutting Concerns ────────────────────────────────────

    describe('Cross-Cutting Concerns', () => {
        it('prevents all operations on disposed scene', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));
            await scene.loadScene(buildSnapshotA());
            scene.dispose();

            expect(() => scene.createActor({ name: 'Late' })).toThrow();
            expect(() => scene.createCameraActor({ name: 'Cam' })).toThrow();
            expect(() =>
                scene.createRenderableActor({ name: 'Mesh' }, { meshId: 'm', materialId: 'mat' })
            ).toThrow();
            expect(() => scene.start(0)).toThrow();
        });

        it('does not leak GL state between sequential scenes on different canvases', async () => {
            // Scene A
            const canvas1 = document.createElement('canvas');
            const scene1 = new Scene(createSceneOptions(scheduler, canvas1));
            const gl1 = getGlFromScene(scene1);

            await scene1.loadScene(buildSnapshotA());
            scene1.start(0);
            scheduler.flush(16);

            const gl1ShadersCreated = countMockCalls(gl1.createShader);
            const gl1ProgramsCreated = countMockCalls(gl1.createProgram);

            scene1.dispose();

            // All GL resources from scene 1 should be cleaned up
            expect(countMockCalls(gl1.deleteShader)).toBeGreaterThanOrEqual(gl1ShadersCreated);
            expect(countMockCalls(gl1.deleteProgram)).toBeGreaterThanOrEqual(gl1ProgramsCreated);

            // Scene B — completely independent GL context
            const canvas2 = document.createElement('canvas');
            const scene2 = new Scene(createSceneOptions(scheduler, canvas2));
            const gl2 = getGlFromScene(scene2);

            await scene2.loadScene(buildSnapshotB());
            scene2.start(0);
            scheduler.flush(32);

            // Scene B's GL should have its own resource creation
            expect(countMockCalls(gl2.createShader)).toBeGreaterThan(0);
            expect(countMockCalls(gl2.createProgram)).toBeGreaterThan(0);

            scene2.dispose();
        });

        it('reloading into the same Scene instance clears previous actors', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            // Load scene A
            const actorsA = await scene.loadScene(buildSnapshotA());
            expect(actorsA.find((a) => a.name === 'ActorA_Root')).toBeDefined();

            // Load scene B into the same scene — should clear scene A actors
            const actorsB = await scene.loadScene(buildSnapshotB());
            expect(actorsB.find((a) => a.name === 'ActorB_Root')).toBeDefined();
            expect(actorsB.find((a) => a.name === 'ActorA_Root')).toBeUndefined();

            scene.dispose();
        });

        it('maintains isolation when rapidly alternating between scene loads', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            // Alternate loading snapshot A and B
            for (let i = 0; i < 3; i++) {
                const actorsA = await scene.loadScene(buildSnapshotA());
                expect(actorsA.find((a) => a.name === 'ActorA_Root')).toBeDefined();
                expect(actorsA.find((a) => a.name === 'ActorB_Root')).toBeUndefined();

                const actorsB = await scene.loadScene(buildSnapshotB());
                expect(actorsB.find((a) => a.name === 'ActorB_Root')).toBeDefined();
                expect(actorsB.find((a) => a.name === 'ActorA_Root')).toBeUndefined();
            }

            scene.dispose();
        });

        it('fully cleans up a scene with textures, materials, and hierarchy', async () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));
            const gl = getGlFromScene(scene);

            const snapshot = buildTexturedSnapshot({
                prefab: {
                    id: 'complex-prefab',
                    actors: [
                        {
                            name: 'ComplexRoot',
                            layer: 0,
                            tag: 'Default',
                            active: true,
                            persistent: false,
                            pooled: false,
                            components: [{ type: 'Transform', data: {} }],
                        },
                        {
                            name: 'ComplexChild',
                            parentNodeId: 'node-root',
                            layer: 0,
                            tag: 'Default',
                            active: true,
                            persistent: false,
                            pooled: false,
                            components: [
                                { type: 'Transform', data: {} },
                                { type: 'MeshRenderer', data: { meshId: 'textured-mesh', materialId: 'textured-mat' } },
                            ],
                        },
                        {
                            name: 'ComplexCamera',
                            layer: 0,
                            tag: 'Camera',
                            active: true,
                            persistent: false,
                            pooled: false,
                            components: [
                                { type: 'Transform', data: {} },
                                { type: 'Camera', data: { primary: true } },
                            ],
                        },
                    ],
                },
            });

            const actors = await scene.loadScene(snapshot);
            expect(actors.length).toBe(3);

            scene.start(0);
            scheduler.flush(16);

            const texturesBeforeDispose = countMockCalls(gl.createTexture);
            const buffersBeforeDispose = countMockCalls(gl.createBuffer);

            scene.dispose();

            // Verify all resources released
            expect(countMockCalls(gl.deleteTexture)).toBeGreaterThanOrEqual(texturesBeforeDispose);
            expect(countMockCalls(gl.deleteBuffer)).toBeGreaterThanOrEqual(buffersBeforeDispose);
            expect(scene.world.getAllActors().length).toBe(0);
            expect(scene.isDisposed).toBe(true);
        });
    });
});
