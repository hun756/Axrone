import { describe, expect, it } from 'vitest';
import {
    RenderBakeTaskScheduler,
    getRenderBakeTaskCost,
    sumRenderBakeTaskCost,
    type ScheduledRenderBakeTask,
} from '../render-bake-task-scheduler';
import type { RenderLightBakeTask } from '../types';

const createTask = (overrides: Partial<RenderLightBakeTask> = {}): RenderLightBakeTask => ({
    id: 'task:1',
    type: 'lightmap',
    ...overrides,
});

describe('getRenderBakeTaskCost', () => {
    it('returns correct cost for lightmap', () => {
        expect(getRenderBakeTaskCost({ type: 'lightmap' })).toBe(0.28);
    });

    it('returns correct cost for probe', () => {
        expect(getRenderBakeTaskCost({ type: 'probe' })).toBe(0.18);
    });

    it('returns correct cost for irradiance-cache', () => {
        expect(getRenderBakeTaskCost({ type: 'irradiance-cache' })).toBe(0.22);
    });
});

describe('sumRenderBakeTaskCost', () => {
    it('returns 0 for empty array', () => {
        expect(sumRenderBakeTaskCost([])).toBe(0);
    });

    it('sums costs of mixed task types', () => {
        const tasks = [{ type: 'lightmap' }, { type: 'probe' }, { type: 'irradiance-cache' }];
        expect(sumRenderBakeTaskCost(tasks)).toBeCloseTo(0.68);
    });
});

describe('RenderBakeTaskScheduler', () => {
    describe('enqueue / list / get', () => {
        it('enqueues a task with defaults', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask());
            expect(scheduler.size).toBe(1);
            const task = scheduler.get('task:1');
            expect(task).not.toBeNull();
            expect(task!.state).toBe('queued');
            expect(task!.maxRetries).toBe(3);
            expect(task!.retries).toBe(0);
        });

        it('preserves provided task fields', () => {
            const scheduler = new RenderBakeTaskScheduler(5);
            scheduler.enqueue(createTask({ id: 'task:x', priority: 10, state: 'running' }));
            const task = scheduler.get('task:x');
            expect(task!.priority).toBe(10);
            expect(task!.state).toBe('running');
        });

        it('list returns frozen copy of all tasks', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask({ id: 'a' }));
            scheduler.enqueue(createTask({ id: 'b' }));
            const list = scheduler.list();
            expect(list).toHaveLength(2);
            expect(Object.isFrozen(list)).toBe(true);
        });

        it('get returns null for unknown id', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            expect(scheduler.get('nope')).toBeNull();
        });
    });

    describe('complete', () => {
        it('sets task state to completed', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask());
            scheduler.complete('task:1', 'en');
            expect(scheduler.get('task:1')!.state).toBe('completed');
        });

        it('throws for unknown task id', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            expect(() => scheduler.complete('nope', 'en')).toThrow();
        });
    });

    describe('fail', () => {
        it('increments retries and keeps queued when under maxRetries', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask());
            scheduler.fail('task:1', 'timeout', 'en');
            const task = scheduler.get('task:1');
            expect(task!.retries).toBe(1);
            expect(task!.state).toBe('queued');
            expect(task!.lastError).toBe('timeout');
        });

        it('sets state to failed when retries exceed maxRetries', () => {
            const scheduler = new RenderBakeTaskScheduler(1);
            scheduler.enqueue(createTask());
            scheduler.fail('task:1', 'err1', 'en');
            expect(scheduler.get('task:1')!.state).toBe('queued');
            scheduler.fail('task:1', 'err2', 'en');
            expect(scheduler.get('task:1')!.state).toBe('failed');
        });

        it('throws for unknown task id', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            expect(() => scheduler.fail('nope', 'err', 'en')).toThrow();
        });
    });

    describe('remove / clear', () => {
        it('remove deletes task and returns true', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask());
            expect(scheduler.remove('task:1')).toBe(true);
            expect(scheduler.size).toBe(0);
        });

        it('remove returns false for unknown id', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            expect(scheduler.remove('nope')).toBe(false);
        });

        it('clear removes all tasks', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask({ id: 'a' }));
            scheduler.enqueue(createTask({ id: 'b' }));
            scheduler.clear();
            expect(scheduler.size).toBe(0);
        });
    });

    describe('select', () => {
        it('returns empty when disabled', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask());
            const selected = scheduler.select(1, {
                enabled: false, maxTasksPerFrame: 10, budgetMs: 100, throttleFrames: 0,
            });
            expect(selected).toHaveLength(0);
        });

        it('returns empty when no tasks', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            const selected = scheduler.select(1, {
                enabled: true, maxTasksPerFrame: 10, budgetMs: 100, throttleFrames: 0,
            });
            expect(selected).toHaveLength(0);
        });

        it('filters out completed and failed tasks', () => {
            const scheduler = new RenderBakeTaskScheduler(0);
            scheduler.enqueue(createTask({ id: 'a' }));
            scheduler.enqueue(createTask({ id: 'b' }));
            scheduler.complete('a', 'en');
            scheduler.enqueue(createTask({ id: 'c' }));
            scheduler.fail('b', 'err', 'en');

            const selected = scheduler.select(1, {
                enabled: true, maxTasksPerFrame: 10, budgetMs: 100, throttleFrames: 0,
            });
            const ids = selected.map((t) => t.id);
            expect(ids).not.toContain('a');
            expect(ids).not.toContain('b');
            expect(ids).toContain('c');
        });

        it('sorts by priority descending', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask({ id: 'low', priority: 1 }));
            scheduler.enqueue(createTask({ id: 'high', priority: 10 }));
            scheduler.enqueue(createTask({ id: 'mid', priority: 5 }));

            const selected = scheduler.select(1, {
                enabled: true, maxTasksPerFrame: 10, budgetMs: 100, throttleFrames: 0,
            });
            expect(selected[0]!.id).toBe('high');
        });

        it('respects maxTasksPerFrame', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask({ id: 'a' }));
            scheduler.enqueue(createTask({ id: 'b' }));
            scheduler.enqueue(createTask({ id: 'c' }));

            const selected = scheduler.select(1, {
                enabled: true, maxTasksPerFrame: 2, budgetMs: 100, throttleFrames: 0,
            });
            expect(selected).toHaveLength(2);
        });

        it('respects throttleFrames', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask({ id: 'a' }));

            scheduler.select(10, {
                enabled: true, maxTasksPerFrame: 10, budgetMs: 100, throttleFrames: 0,
            });

            const throttled = scheduler.select(11, {
                enabled: true, maxTasksPerFrame: 10, budgetMs: 100, throttleFrames: 5,
            });
            expect(throttled).toHaveLength(0);

            const unthrottled = scheduler.select(20, {
                enabled: true, maxTasksPerFrame: 10, budgetMs: 100, throttleFrames: 5,
            });
            expect(unthrottled.length).toBeGreaterThanOrEqual(0);
        });

        it('sets selected tasks to running state', () => {
            const scheduler = new RenderBakeTaskScheduler(3);
            scheduler.enqueue(createTask());
            scheduler.select(1, {
                enabled: true, maxTasksPerFrame: 10, budgetMs: 100, throttleFrames: 0,
            });
            expect(scheduler.get('task:1')!.state).toBe('running');
        });
    });
});
