import { describe, expect, it, vi } from 'vitest';
import type { GameLoop, GameLoopStatus } from '@axrone/game-loop';
import { SceneLifecycleRuntime } from '../scene-lifecycle-runtime';
import type { SceneLoopState } from '../types';

interface FakeCanvas {
    readonly listeners: Map<string, Set<(event: Event) => void>>;
    addEventListener(type: string, listener: (event: Event) => void): void;
    removeEventListener(type: string, listener: (event: Event) => void): void;
    dispatch(type: string, event?: Partial<Event>): void;
    width: number;
    height: number;
    clientWidth: number;
    clientHeight: number;
    style: Record<string, string>;
    parentNode: null;
}

const createFakeCanvas = (): FakeCanvas => {
    const listeners = new Map<string, Set<(event: Event) => void>>();
    return {
        listeners,
        addEventListener(type, listener) {
            const bucket = listeners.get(type) ?? new Set();
            bucket.add(listener);
            listeners.set(type, bucket);
        },
        removeEventListener(type, listener) {
            listeners.get(type)?.delete(listener);
        },
        dispatch(type, event = {}) {
            for (const listener of listeners.get(type) ?? []) {
                listener({ preventDefault: () => undefined, ...event } as Event);
            }
        },
        width: 0,
        height: 0,
        clientWidth: 0,
        clientHeight: 0,
        style: {},
        parentNode: null,
    };
};

const createFakeLoop = () => {
    let status: GameLoopStatus = 'running';
    const loop = {
        get status() {
            return status;
        },
        pause: vi.fn(() => {
            status = 'paused';
        }),
        resume: vi.fn(() => {
            status = 'running';
            return loop;
        }),
        start: vi.fn(),
        stop: vi.fn(),
        dispose: vi.fn(),
    };
    return loop as unknown as GameLoop<SceneLoopState> & {
        pause: ReturnType<typeof vi.fn>;
        resume: ReturnType<typeof vi.fn>;
    };
};

const createRuntime = (overrides: {
    readonly canvas: FakeCanvas;
    readonly loop: ReturnType<typeof createFakeLoop>;
    readonly onContextLost?: () => void;
    readonly onContextRestored?: () => void;
}) =>
    new SceneLifecycleRuntime({
        canvas: overrides.canvas as unknown as HTMLCanvasElement,
        gl: { viewport: () => undefined } as unknown as WebGL2RenderingContext,
        loop: overrides.loop,
        autoCreatedCanvas: false,
        pixelRatio: 1,
        defaultWidth: 640,
        defaultHeight: 480,
        render: () => undefined,
        disposeAssets: () => undefined,
        disposeWorld: () => undefined,
        onContextLost: overrides.onContextLost,
        onContextRestored: overrides.onContextRestored,
    });

describe('SceneLifecycleRuntime context loss handling', () => {
    it('pauses the loop, prevents default, and flags the lost context', () => {
        const canvas = createFakeCanvas();
        const loop = createFakeLoop();
        const onContextLost = vi.fn();
        const runtime = createRuntime({ canvas, loop, onContextLost });
        const preventDefault = vi.fn();

        canvas.dispatch('webglcontextlost', { preventDefault });

        expect(preventDefault).toHaveBeenCalledTimes(1);
        expect(loop.pause).toHaveBeenCalledTimes(1);
        expect(runtime.isContextLost).toBe(true);
        expect(onContextLost).toHaveBeenCalledTimes(1);
    });

    it('invalidates via hook before resuming on restore', () => {
        const canvas = createFakeCanvas();
        const loop = createFakeLoop();
        const order: string[] = [];
        const runtime = createRuntime({
            canvas,
            loop,
            onContextRestored: () => {
                order.push('invalidate');
            },
        });
        loop.resume.mockImplementation(() => {
            order.push('resume');
        });

        canvas.dispatch('webglcontextlost');
        canvas.dispatch('webglcontextrestored');

        expect(runtime.isContextLost).toBe(false);
        expect(order).toEqual(['invalidate', 'resume']);
    });

    it('does not resume when the loop was not running at loss time', () => {
        const canvas = createFakeCanvas();
        const loop = createFakeLoop();
        loop.pause();
        loop.pause.mockClear();
        createRuntime({ canvas, loop });

        canvas.dispatch('webglcontextlost');
        canvas.dispatch('webglcontextrestored');

        expect(loop.pause).not.toHaveBeenCalled();
        expect(loop.resume).not.toHaveBeenCalled();
    });

    it('removes context listeners on dispose', () => {
        const canvas = createFakeCanvas();
        const loop = createFakeLoop();
        const onContextLost = vi.fn();
        const runtime = createRuntime({ canvas, loop, onContextLost });

        runtime.dispose();
        canvas.dispatch('webglcontextlost');

        expect(onContextLost).not.toHaveBeenCalled();
        expect(canvas.listeners.get('webglcontextlost')?.size ?? 0).toBe(0);
        expect(canvas.listeners.get('webglcontextrestored')?.size ?? 0).toBe(0);
    });
});
