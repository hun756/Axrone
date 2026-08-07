import { describe, expect, it, vi } from 'vitest';
import { SceneDirectGlPassGuard } from '../rendering/internal/render-state-guard';

type GuardGl = Pick<
    WebGL2RenderingContext,
    'depthMask' | 'disable' | 'enable' | 'bindVertexArray' | 'bindBuffer'
> & {
    readonly BLEND: number;
    readonly CULL_FACE: number;
    readonly SCISSOR_TEST: number;
    readonly ARRAY_BUFFER: number;
};

const createGlStub = () => {
    const calls: string[] = [];
    const gl: GuardGl = {
        BLEND: 1,
        CULL_FACE: 2,
        SCISSOR_TEST: 3,
        ARRAY_BUFFER: 4,
        depthMask: vi.fn((value: boolean) => {
            calls.push(`depthMask:${value}`);
        }),
        disable: vi.fn((capability: number) => {
            calls.push(`disable:${capability}`);
        }),
        enable: vi.fn((capability: number) => {
            calls.push(`enable:${capability}`);
        }),
        bindVertexArray: vi.fn(() => {
            calls.push('bindVertexArray:null');
        }),
        bindBuffer: vi.fn(() => {
            calls.push('bindBuffer:null');
        }),
    };

    return { gl: gl as unknown as WebGL2RenderingContext, calls };
};

describe('SceneDirectGlPassGuard', () => {
    it('returns the body result and restores the GL baseline on success', () => {
        const { gl, calls } = createGlStub();
        const reset = vi.fn();
        const guard = new SceneDirectGlPassGuard({
            gl,
            renderStateApplier: { reset },
            label: 'test-pass',
        });

        const result = guard.run(-1, () => 42);

        expect(result).toBe(42);
        expect(guard.isDisabled).toBe(false);
        expect(reset).toHaveBeenCalledTimes(1);
        expect(calls).toContain('depthMask:true');
        expect(calls).toContain('disable:1');
        expect(calls).toContain('enable:2');
        expect(calls).toContain('disable:3');
        expect(calls).toContain('bindVertexArray:null');
        expect(calls).toContain('bindBuffer:null');
    });

    it('never propagates a body exception and disables itself one-shot', () => {
        const { gl } = createGlStub();
        const reset = vi.fn();
        const onDisabled = vi.fn();
        const guard = new SceneDirectGlPassGuard({
            gl,
            renderStateApplier: { reset },
            label: 'test-pass',
            onDisabled,
        });

        const body = vi.fn(() => {
            throw new Error('boom');
        });

        expect(guard.run('fallback', body)).toBe('fallback');
        expect(guard.isDisabled).toBe(true);
        expect(onDisabled).toHaveBeenCalledTimes(1);
        expect(reset).toHaveBeenCalledTimes(1);

        // Subsequent runs skip the body entirely and surface nothing new.
        expect(guard.run('fallback', body)).toBe('fallback');
        expect(body).toHaveBeenCalledTimes(1);
        expect(onDisabled).toHaveBeenCalledTimes(1);
    });

    it('resets the render-state-applier cache even when the body throws', () => {
        const { gl, calls } = createGlStub();
        const reset = vi.fn(() => {
            calls.push('applier:reset');
        });
        const guard = new SceneDirectGlPassGuard({
            gl,
            renderStateApplier: { reset },
            label: 'test-pass',
            onDisabled: () => undefined,
        });

        guard.run(undefined, () => {
            throw new Error('boom');
        });

        expect(calls[calls.length - 1]).toBe('applier:reset');
        expect(calls).toContain('disable:3');
    });
});
