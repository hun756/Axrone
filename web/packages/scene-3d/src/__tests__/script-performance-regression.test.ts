import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { Transform } from '@axrone/ecs-runtime';
import { Vec3 } from '@axrone/numeric';
import {
    createSceneOptions,
    installWebGL2Constants,
    ManualScheduler,
} from './test-harness';

let Scene: typeof import('@axrone/scene-3d').Scene;
let FollowCameraController: typeof import('@axrone/scene-3d').FollowCameraController;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Read heap usage from the best available source.
 * Returns null when no memory API is reachable (e.g. some CI envs).
 */
function readHeapBytes(): number | null {
    // Chromium / browser
    const perfMemory = (performance as unknown as Record<string, unknown>).memory as
        | Record<string, number>
        | undefined;
    if (perfMemory && typeof perfMemory.usedJSHeapSize === 'number') {
        return perfMemory.usedJSHeapSize;
    }
    // Node.js
    if (typeof process !== 'undefined' && typeof process.memoryUsage === 'function') {
        return process.memoryUsage().heapUsed;
    }
    return null;
}

/**
 * Run `count` scheduler frames starting at `startMs`, incrementing by `stepMs`.
 */
function runFrames(scheduler: ManualScheduler, count: number, startMs = 0, stepMs = 16): void {
    for (let i = 0; i < count; i++) {
        scheduler.flush(startMs + i * stepMs);
    }
}

/**
 * Returns true when a memory measurement API is available.
 */
function hasMemoryApi(): boolean {
    return readHeapBytes() !== null;
}

// ---------------------------------------------------------------------------
// FollowCameraController — Zero-Allocation Baseline
// ---------------------------------------------------------------------------

describe('Script Performance Regression', () => {
    let scheduler: ManualScheduler;

    beforeAll(async () => {
        installWebGL2Constants();
        const sceneModule = await import('@axrone/scene-3d');
        Scene = sceneModule.Scene;
        FollowCameraController = sceneModule.FollowCameraController;
    });

    beforeEach(() => {
        scheduler = new ManualScheduler();
    });

    afterEach(() => {
        document.body.innerHTML = '';
    });

    // ─── Group 1: Zero-Allocation Validation (FollowCameraController) ─────

    describe('Zero-Allocation Validation — FollowCameraController', () => {
        it('produces zero heap growth over 100 frames (baseline reference)', () => {
            if (!hasMemoryApi()) {
                return;
            }

            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'Target' });
            const targetTransform = target.requireComponent(Transform);
            targetTransform.position = new Vec3(5, 0, 0);

            const camera = scene.createCameraActor({ name: 'Camera' }, { primary: true });
            const controller = camera.addComponent(FollowCameraController, {
                distance: 6,
                azimuth: 0,
                elevation: 0.3,
                targetOffset: [0, 1, 0],
                positionDamping: 10,
                targetDamping: 14,
            });
            controller.setTarget(targetTransform);

            scene.start(0);

            // Warm-up: 10 frames to let transient allocations settle
            runFrames(scheduler, 10, 0);

            const heapBefore = readHeapBytes()!;
            // Measure over 100 steady-state frames
            runFrames(scheduler, 100, 160);
            const heapAfter = readHeapBytes()!;

            const growthBytes = heapAfter - heapBefore;
            // Allow up to 1 KB tolerance for measurement noise
            expect(growthBytes).toBeLessThan(1024);

            scene.dispose();
        });

        it('produces zero heap growth when target moves each frame', () => {
            if (!hasMemoryApi()) {
                return;
            }

            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'MovingTarget' });
            const targetTransform = target.requireComponent(Transform);

            const camera = scene.createCameraActor({ name: 'Camera' }, { primary: true });
            const controller = camera.addComponent(FollowCameraController, {
                distance: 8,
                azimuth: 0.5,
                elevation: 0.2,
                targetOffset: [0, 1.5, 0],
            });
            controller.setTarget(targetTransform);

            scene.start(0);

            // Warm-up
            runFrames(scheduler, 10, 0);
            const heapBefore = readHeapBytes()!;

            // Move target every frame for 100 frames
            for (let i = 0; i < 100; i++) {
                targetTransform.position = new Vec3(
                    Math.sin(i * 0.1) * 10,
                    1,
                    Math.cos(i * 0.1) * 10,
                );
                scheduler.flush(160 + i * 16);
            }

            const heapAfter = readHeapBytes()!;
            const growthBytes = heapAfter - heapBefore;
            expect(growthBytes).toBeLessThan(2048);

            scene.dispose();
        });

        it('produces zero heap growth during orbit/zoom operations', () => {
            if (!hasMemoryApi()) {
                return;
            }

            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'Target' });
            const targetTransform = target.requireComponent(Transform);
            targetTransform.position = new Vec3(0, 0, 0);

            const camera = scene.createCameraActor({ name: 'Camera' }, { primary: true });
            const controller = camera.addComponent(FollowCameraController, {
                distance: 7,
                azimuth: 0,
                elevation: 0,
            });
            controller.setTarget(targetTransform);

            scene.start(0);
            runFrames(scheduler, 10, 0);
            const heapBefore = readHeapBytes()!;

            for (let i = 0; i < 100; i++) {
                controller.orbit(0.01, 0.005).zoom(-0.02);
                scheduler.flush(160 + i * 16);
            }

            const heapAfter = readHeapBytes()!;
            expect(heapAfter - heapBefore).toBeLessThan(1024);

            scene.dispose();
        });
    });

    // ─── Group 2: Scene Lifecycle Memory Stability ───────────────────────

    describe('Scene Lifecycle Memory Stability', () => {
        it('does not leak memory across start/stop cycles', () => {
            if (!hasMemoryApi()) {
                return;
            }

            const canvas = document.createElement('canvas');

            const heapBefore = readHeapBytes()!;

            for (let cycle = 0; cycle < 10; cycle++) {
                const scene = new Scene(createSceneOptions(scheduler, canvas));
                const target = scene.createActor({ name: 'Target' });
                const targetTransform = target.requireComponent(Transform);

                const camera = scene.createCameraActor({ name: 'Camera' }, { primary: true });
                const controller = camera.addComponent(FollowCameraController, {
                    distance: 5,
                });
                controller.setTarget(targetTransform);

                scene.start(0);
                runFrames(scheduler, 20, cycle * 320);
                scene.stop();
                scene.dispose();
            }

            const heapAfter = readHeapBytes()!;
            const growthBytes = heapAfter - heapBefore;
            // 10 start/stop cycles — allow 4 KB tolerance
            expect(growthBytes).toBeLessThan(4096);
        });

        it('does not leak memory across scene load/dispose cycles', async () => {
            if (!hasMemoryApi()) {
                return;
            }

            const heapBefore = readHeapBytes()!;

            for (let cycle = 0; cycle < 5; cycle++) {
                const canvas = document.createElement('canvas');
                const scene = new Scene(createSceneOptions(scheduler, canvas));

                const snapshot = {
                    version: 1 as const,
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
                                {
                                    semantic: 'position' as const,
                                    componentCount: 3 as const,
                                    offset: 0,
                                    stride: 12,
                                },
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
                                    {
                                        type: 'MeshRenderer',
                                        data: { meshId: 'cube', materialId: 'default-mat' },
                                    },
                                ],
                            },
                        ],
                    },
                };

                await scene.loadScene(snapshot);
                scene.start(0);
                runFrames(scheduler, 10, cycle * 160);
                scene.dispose();
            }

            const heapAfter = readHeapBytes()!;
            expect(heapAfter - heapBefore).toBeLessThan(8192);
        });
    });

    // ─── Group 3: Steady-State Memory (1000 frames) ─────────────────────

    describe('Steady-State Memory', () => {
        it('shows no monotonic heap growth over 1000 frames', () => {
            if (!hasMemoryApi()) {
                return;
            }

            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'Target' });
            const targetTransform = target.requireComponent(Transform);
            targetTransform.position = new Vec3(3, 0, 0);

            const camera = scene.createCameraActor({ name: 'Camera' }, { primary: true });
            const controller = camera.addComponent(FollowCameraController, {
                distance: 6,
                azimuth: 0,
                elevation: 0.3,
            });
            controller.setTarget(targetTransform);

            scene.start(0);

            // Warm-up
            runFrames(scheduler, 50, 0);

            // Sample heap every 100 frames for 1000 total frames
            const samples: number[] = [];
            for (let batch = 0; batch < 10; batch++) {
                runFrames(scheduler, 100, 800 + batch * 1600);
                samples.push(readHeapBytes()!);
            }

            // Check that the last sample is not significantly larger than the first
            const firstSample = samples[0]!;
            const lastSample = samples[samples.length - 1]!;
            const totalGrowth = lastSample - firstSample;

            // Over 1000 frames, growth should be < 4 KB
            expect(totalGrowth).toBeLessThan(4096);

            // Also check that no intermediate sample shows a monotonic ramp
            // by verifying the max sample is within 8 KB of the min sample
            const minSample = Math.min(...samples);
            const maxSample = Math.max(...samples);
            expect(maxSample - minSample).toBeLessThan(8192);

            scene.dispose();
        });

        it('heap samples remain within a bounded range during active simulation', () => {
            if (!hasMemoryApi()) {
                return;
            }

            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'Target' });
            const targetTransform = target.requireComponent(Transform);

            const camera = scene.createCameraActor({ name: 'Camera' }, { primary: true });
            const controller = camera.addComponent(FollowCameraController, {
                distance: 10,
                positionDamping: 5,
                targetDamping: 8,
            });
            controller.setTarget(targetTransform);

            scene.start(0);
            runFrames(scheduler, 50, 0);

            const samples: number[] = [];
            for (let batch = 0; batch < 10; batch++) {
                // Move target each frame to keep simulation active
                for (let f = 0; f < 100; f++) {
                    const t = (batch * 100 + f) * 0.05;
                    targetTransform.position = new Vec3(
                        Math.sin(t) * 5,
                        1,
                        Math.cos(t) * 5,
                    );
                    scheduler.flush(800 + (batch * 100 + f) * 16);
                }
                samples.push(readHeapBytes()!);
            }

            const minSample = Math.min(...samples);
            const maxSample = Math.max(...samples);
            // Bounded range: max 16 KB fluctuation
            expect(maxSample - minSample).toBeLessThan(16384);

            scene.dispose();
        });
    });

    // ─── Group 4: Frame Time Budget ─────────────────────────────────────

    describe('Frame Time Budget', () => {
        it('average frame time stays under 5ms for logic/update phase', () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'Target' });
            const targetTransform = target.requireComponent(Transform);
            targetTransform.position = new Vec3(2, 0, 0);

            const camera = scene.createCameraActor({ name: 'Camera' }, { primary: true });
            const controller = camera.addComponent(FollowCameraController, {
                distance: 6,
            });
            controller.setTarget(targetTransform);

            scene.start(0);

            // Warm-up
            runFrames(scheduler, 10, 0);

            const frameTimes: number[] = [];
            for (let i = 0; i < 60; i++) {
                const t0 = performance.now();
                scheduler.flush(160 + i * 16);
                const t1 = performance.now();
                frameTimes.push(t1 - t0);
            }

            const avgFrameTime =
                frameTimes.reduce((sum, t) => sum + t, 0) / frameTimes.length;
            // Desktop budget: < 5ms per logic frame
            expect(avgFrameTime).toBeLessThan(5);

            scene.dispose();
        });

        it('no single frame exceeds 16.6ms in the logic phase', () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'Target' });
            const targetTransform = target.requireComponent(Transform);

            const camera = scene.createCameraActor({ name: 'Camera' }, { primary: true });
            const controller = camera.addComponent(FollowCameraController, {
                distance: 6,
            });
            controller.setTarget(targetTransform);

            scene.start(0);
            runFrames(scheduler, 10, 0);

            for (let i = 0; i < 60; i++) {
                targetTransform.position = new Vec3(
                    Math.sin(i * 0.1) * 8,
                    1,
                    Math.cos(i * 0.1) * 8,
                );
                const t0 = performance.now();
                scheduler.flush(160 + i * 16);
                const elapsed = performance.now() - t0;
                // 16.6ms = 60fps budget
                expect(elapsed).toBeLessThan(16.6);
            }

            scene.dispose();
        });

        it('frame time remains stable (no degradation) over 200 frames', () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'Target' });
            const targetTransform = target.requireComponent(Transform);

            const camera = scene.createCameraActor({ name: 'Camera' }, { primary: true });
            const controller = camera.addComponent(FollowCameraController, {
                distance: 6,
            });
            controller.setTarget(targetTransform);

            scene.start(0);
            runFrames(scheduler, 10, 0);

            // Measure first 50 frames
            const firstBatch: number[] = [];
            for (let i = 0; i < 50; i++) {
                const t0 = performance.now();
                scheduler.flush(160 + i * 16);
                firstBatch.push(performance.now() - t0);
            }

            // Measure last 50 frames (frames 150-199)
            const lastBatch: number[] = [];
            for (let i = 150; i < 200; i++) {
                const t0 = performance.now();
                scheduler.flush(160 + i * 16);
                lastBatch.push(performance.now() - t0);
            }

            const avgFirst = firstBatch.reduce((s, t) => s + t, 0) / firstBatch.length;
            const avgLast = lastBatch.reduce((s, t) => s + t, 0) / lastBatch.length;

            // Last batch should not be more than 2x the first batch (no degradation)
            expect(avgLast).toBeLessThan(avgFirst * 2 + 1);

            scene.dispose();
        });
    });

    // ─── Group 5: Pool Metrics Tracking ─────────────────────────────────

    describe('Pool Metrics', () => {
        it('tracks allocation and release counts through spawn/despawn cycles', async () => {
            const { PoolMetricsCollector } = await import('@axrone/memory');

            const metrics = new PoolMetricsCollector('test-pool', true);

            // Simulate 1000 spawn/despawn cycles
            const warmUpCount = 50;
            const measureCount = 1000;

            // Warm-up phase
            for (let i = 0; i < warmUpCount; i++) {
                metrics.recordAllocation(true, false); // miss (new allocation)
                metrics.recordCreation(0.01);
            }
            for (let i = 0; i < warmUpCount; i++) {
                metrics.recordRelease();
                metrics.recordReleaseTime(0.005);
            }

            // Measure phase — all should be pool hits
            for (let i = 0; i < measureCount; i++) {
                metrics.recordAllocation(true, true); // hit (from pool)
            }
            for (let i = 0; i < measureCount; i++) {
                metrics.recordRelease();
            }

            const snapshot = metrics.snapshot(
                warmUpCount, // capacity
                0, // all released back
                warmUpCount * 64, // estimated memory
                0, // no fragmentation
            );

            expect(snapshot.allocations).toBe(warmUpCount + measureCount);
            expect(snapshot.releases).toBe(warmUpCount + measureCount);
            expect(snapshot.hitRatio).toBeGreaterThan(0.95);
            expect(snapshot.missRate).toBeLessThan(0.05);
        });

        it('reports zero misses after warm-up when pool is pre-warmed', async () => {
            const { PoolMetricsCollector } = await import('@axrone/memory');

            const metrics = new PoolMetricsCollector('prewarmed-pool', true);

            // Pre-warm: create 100 objects
            for (let i = 0; i < 100; i++) {
                metrics.recordAllocation(true, false);
                metrics.recordCreation(0.01);
            }
            // Return all to pool
            for (let i = 0; i < 100; i++) {
                metrics.recordRelease();
            }

            // Now all allocations should be hits
            for (let i = 0; i < 500; i++) {
                metrics.recordAllocation(true, true);
            }

            const snapshot = metrics.snapshot(100, 0, 6400, 0);
            expect(snapshot.missRate).toBe(0);
            expect(snapshot.hitRatio).toBe(1);
        });

        it('tracks high water mark correctly', async () => {
            const { PoolMetricsCollector } = await import('@axrone/memory');

            const metrics = new PoolMetricsCollector('hwm-pool', true);

            // Allocate 50 objects
            for (let i = 0; i < 50; i++) {
                metrics.recordAllocation(true, false);
                metrics.recordHighWaterMark(i + 1);
            }
            // Release 30
            for (let i = 0; i < 30; i++) {
                metrics.recordRelease();
            }
            // Allocate 20 more (total 40 active, but high water mark was 50)
            for (let i = 0; i < 20; i++) {
                metrics.recordAllocation(true, true);
                metrics.recordHighWaterMark(40 + i + 1);
            }

            const snapshot = metrics.snapshot(70, 30, 4480, 0);
            expect(snapshot.highWaterMark).toBe(60);
        });

        it('disabled metrics collector throws on snapshot', async () => {
            const { PoolMetricsCollector } = await import('@axrone/memory');

            const metrics = new PoolMetricsCollector('disabled-pool', false);
            expect(metrics.isEnabled).toBe(false);

            expect(() => metrics.snapshot(10, 5, 640, 0)).toThrow();
        });
    });

    // ─── Group 6: GC Pressure Detection ─────────────────────────────────

    describe('GC Pressure Detection', () => {
        it('node heapUsed stays bounded over 200 frames with active simulation', () => {
            if (typeof process === 'undefined' || typeof process.memoryUsage !== 'function') {
                return;
            }

            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'Target' });
            const targetTransform = target.requireComponent(Transform);

            const camera = scene.createCameraActor({ name: 'Camera' }, { primary: true });
            const controller = camera.addComponent(FollowCameraController, {
                distance: 6,
            });
            controller.setTarget(targetTransform);

            scene.start(0);
            runFrames(scheduler, 50, 0);

            const heapBefore = process.memoryUsage().heapUsed;

            for (let i = 0; i < 200; i++) {
                targetTransform.position = new Vec3(
                    Math.sin(i * 0.05) * 5,
                    1,
                    Math.cos(i * 0.05) * 5,
                );
                scheduler.flush(800 + i * 16);
            }

            const heapAfter = process.memoryUsage().heapUsed;
            const growthMB = (heapAfter - heapBefore) / (1024 * 1024);

            // 200 frames of active simulation should not grow heap more than 2 MB
            expect(growthMB).toBeLessThan(2);

            scene.dispose();
        });

        it('heapUsed does not grow after repeated actor creation and disposal', () => {
            if (typeof process === 'undefined' || typeof process.memoryUsage !== 'function') {
                return;
            }

            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));
            scene.start(0);

            // Warm-up
            runFrames(scheduler, 10, 0);

            const heapBefore = process.memoryUsage().heapUsed;

            // Create and destroy 50 actors
            for (let i = 0; i < 50; i++) {
                const actor = scene.createActor({ name: `Temp_${i}` });
                const transform = actor.requireComponent(Transform);
                transform.position = new Vec3(i, 0, 0);
                actor.destroy();
            }

            runFrames(scheduler, 20, 160);

            const heapAfter = process.memoryUsage().heapUsed;
            const growthMB = (heapAfter - heapBefore) / (1024 * 1024);

            // Allow 4 MB tolerance for actor churn
            expect(growthMB).toBeLessThan(4);

            scene.dispose();
        });
    });

    // ─── Group 7: Code Pattern Validation ───────────────────────────────

    describe('Code Pattern Validation — Zero-Allocation in Hot Paths', () => {
        it('FollowCameraController.lateUpdate does not contain `new` expressions', async () => {
            // Read the source of the FollowCameraController module
            const controllerSource = FollowCameraController.toString();

            // The lateUpdate method (and its private helpers called from it) should
            // not contain `new` expressions. We check the class source for the
            // pattern. Pre-allocated temp fields (_tempForward, _tempUp, etc.)
            // are used instead.
            const lateUpdateMatch = controllerSource.match(
                /lateUpdate\s*\([^)]*\)\s*\{[\s\S]*?\n\s{4}\}/,
            );

            if (lateUpdateMatch) {
                const lateUpdateBody = lateUpdateMatch[0];
                // Check that there are no `new ` expressions in lateUpdate
                const newExpressions = lateUpdateBody.match(/\bnew\s+\w+/g);
                expect(newExpressions).toBeNull();
            }
            // If we can't extract the method (minified), pass with a note
        });

        it('FollowCameraController pre-allocates all temporary vectors in constructor', () => {
            // Verify the class declares pre-allocated temp fields
            const source = FollowCameraController.toString();

            // The zero-allocation pattern uses readonly temp fields
            expect(source).toContain('_smoothedTarget');
            expect(source).toContain('_desiredTarget');
            expect(source).toContain('_desiredPosition');
            expect(source).toContain('_tempForward');
            expect(source).toContain('_tempUp');
            expect(source).toContain('_tempBackward');
            expect(source).toContain('_tempRotation');
        });

        it('FollowCameraController uses out-parameter pattern instead of creating new vectors', () => {
            const source = FollowCameraController.toString();

            // The _resolveDesiredTarget and _composeDesiredPosition methods should
            // write into an `out` parameter rather than returning `new Vec3(...)`
            expect(source).toContain('_resolveDesiredTarget');
            expect(source).toContain('_composeDesiredPosition');

            // Verify no `new Vec3` in the class body (all temps are pre-allocated)
            const classSource = FollowCameraController.toString();
            // The constructor is allowed to use `new`, but hot-path methods are not
            // We check that the lateUpdate-related private methods don't use `new`
            const privateMethodPattern =
                /_(?:resolveDesiredTarget|composeDesiredPosition|resolveUp|applyCameraTransform)\s*\([^)]*\)\s*\{[\s\S]*?\n\s{4}\}/g;
            const matches = classSource.match(privateMethodPattern);

            if (matches) {
                for (const methodBody of matches) {
                    expect(methodBody.match(/\bnew\s+Vec3\b/)).toBeNull();
                    expect(methodBody.match(/\bnew\s+Quat\b/)).toBeNull();
                }
            }
        });
    });

    // ─── Group 8: Multi-Component Frame Stress ──────────────────────────

    describe('Multi-Component Frame Stress', () => {
        it('handles 10 cameras with FollowCameraController without excessive allocation', () => {
            if (!hasMemoryApi()) {
                return;
            }

            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'Target' });
            const targetTransform = target.requireComponent(Transform);
            targetTransform.position = new Vec3(0, 0, 0);

            // Create 10 cameras all following the same target
            const controllers: InstanceType<typeof FollowCameraController>[] = [];
            for (let i = 0; i < 10; i++) {
                const cam = scene.createCameraActor(
                    { name: `Camera_${i}` },
                    { primary: i === 0 },
                );
                const ctrl = cam.addComponent(FollowCameraController, {
                    distance: 5 + i * 2,
                    azimuth: (i * Math.PI * 2) / 10,
                    elevation: 0.2 + i * 0.05,
                    targetOffset: [0, 1, 0],
                });
                ctrl.setTarget(targetTransform);
                controllers.push(ctrl);
            }

            scene.start(0);
            runFrames(scheduler, 50, 0); // warm-up

            const heapBefore = readHeapBytes()!;

            for (let i = 0; i < 100; i++) {
                targetTransform.position = new Vec3(
                    Math.sin(i * 0.05) * 3,
                    1,
                    Math.cos(i * 0.05) * 3,
                );
                // Orbit a random controller each frame
                controllers[i % 10].orbit(0.01, 0.005);
                scheduler.flush(800 + i * 16);
            }

            const heapAfter = readHeapBytes()!;
            const growthBytes = heapAfter - heapBefore;

            // 10 cameras x 100 frames — allow 4 KB
            expect(growthBytes).toBeLessThan(4096);

            scene.dispose();
        });

        it('maintains stable frame times with 10 active controllers', () => {
            const canvas = document.createElement('canvas');
            const scene = new Scene(createSceneOptions(scheduler, canvas));

            const target = scene.createActor({ name: 'Target' });
            const targetTransform = target.requireComponent(Transform);

            for (let i = 0; i < 10; i++) {
                const cam = scene.createCameraActor(
                    { name: `Camera_${i}` },
                    { primary: i === 0 },
                );
                const ctrl = cam.addComponent(FollowCameraController, {
                    distance: 5 + i,
                });
                ctrl.setTarget(targetTransform);
            }

            scene.start(0);
            runFrames(scheduler, 50, 0);

            const frameTimes: number[] = [];
            for (let i = 0; i < 60; i++) {
                targetTransform.position = new Vec3(
                    Math.sin(i * 0.1) * 5,
                    1,
                    Math.cos(i * 0.1) * 5,
                );
                const t0 = performance.now();
                scheduler.flush(800 + i * 16);
                frameTimes.push(performance.now() - t0);
            }

            const avgFrameTime =
                frameTimes.reduce((s, t) => s + t, 0) / frameTimes.length;
            const maxFrameTime = Math.max(...frameTimes);

            // 10 controllers should still be well under budget
            expect(avgFrameTime).toBeLessThan(5);
            expect(maxFrameTime).toBeLessThan(16.6);

            scene.dispose();
        });
    });

    // ─── Group 9: Memory API Availability Guard ────────────────────────

    describe('Memory API Availability', () => {
        it('readHeapBytes returns a positive number or null', () => {
            const result = readHeapBytes();
            if (result !== null) {
                expect(result).toBeGreaterThan(0);
            }
        });

        it('hasMemoryApi returns a boolean', () => {
            expect(typeof hasMemoryApi()).toBe('boolean');
        });
    });
});
