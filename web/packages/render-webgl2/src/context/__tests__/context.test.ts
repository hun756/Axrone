import { describe, it, expect } from 'vitest';
import { createMockGL, createMockGLContext } from '../mock';
import { GLContext, isGLContext } from '../gl-context';
import {
    getOrCreateGLContext,
    resolveContext,
    resolveContextNullable,
    resolveRawGL,
} from '../factory';
import type { ContextSource } from '../types';

describe('IGLContext', () => {
    it('creates mock GL with required constants', () => {
        const gl = createMockGL();
        expect(gl.ARRAY_BUFFER).toBe(0x8892);
        expect(gl.TEXTURE_2D).toBe(0x0de1);
        expect(typeof gl.getParameter).toBe('function');
        expect(typeof gl.getExtension).toBe('function');
    });

    it('creates IGLContext from mock GL', () => {
        const ctx = createMockGLContext();
        expect(ctx).toBeDefined();
        expect(isGLContext(ctx)).toBe(true);
        expect(ctx.isDisposed).toBe(false);
        expect(ctx.isLost).toBe(false);
        expect(ctx.constants.ARRAY_BUFFER).toBe(0x8892);
        expect(ctx.capabilities.maxTextureSize).toBeGreaterThan(0);
        expect(ctx.extensions).toBeDefined();
        expect(ctx.state).toBeDefined();
    });

    it('exposes capabilities snapshot', () => {
        const ctx = createMockGLContext();
        const snap = ctx.capabilities.snapshot;
        expect(snap.maxTextureSize).toBe(16384);
        expect(snap.isWebGL2).toBe(true);
        expect(snap.version).toContain('WebGL');
    });

    it('handles extensions', () => {
        const ctx = createMockGLContext({
            extensions: { KHR_debug: { labelObject: () => {} } as any },
        });
        expect(ctx.extensions.has('KHR_debug')).toBe(true);
        expect(ctx.extensions.get('KHR_debug')).toBeDefined();
        expect(ctx.extensions.has('WEBGL_lose_context')).toBe(false);
    });

    it('supports state cache', () => {
        const ctx = createMockGLContext();
        const state = ctx.state;
        expect(state).toBeDefined();
        state.bindBuffer(ctx.constants.ARRAY_BUFFER, null);
        state.viewport(0, 0, 800, 600);
        expect(state.snapshot.viewport).toEqual([0, 0, 800, 600]);
    });

    it('supports lifecycle events', () => {
        const ctx = createMockGLContext();
        let disposed = false;
        const unsub = ctx.onDisposed(() => { disposed = true; });
        ctx.dispose();
        expect(ctx.isDisposed).toBe(true);
        expect(disposed).toBe(true);
        unsub();
    });

    it('getOrCreateGLContext caches per gl', () => {
        const gl = createMockGL();
        const a = getOrCreateGLContext(gl);
        const b = getOrCreateGLContext(gl);
        expect(a).toBe(b);
    });

    it('isGLContext type guard', () => {
        const ctx = createMockGLContext();
        const gl = createMockGL();
        expect(isGLContext(ctx)).toBe(true);
        expect(isGLContext(gl)).toBe(false);
        expect(isGLContext(null)).toBe(false);
    });
});

describe('IGLContext backward compatibility', () => {
    it('GLContext accepts raw mock GL via factory', () => {
        const gl = createMockGL();
        const ctx = new GLContext(gl, gl.canvas as HTMLCanvasElement);
        expect(ctx.gl).toBe(gl);
        expect(ctx.constants.TEXTURE_2D).toBe(gl.TEXTURE_2D);
    });

    it('resolveContext wraps raw gl and passes IGLContext through', () => {
        const gl = createMockGL();
        const fromRaw = resolveContext(gl as ContextSource);
        expect(isGLContext(fromRaw)).toBe(true);
        const again = resolveContext(fromRaw as ContextSource);
        expect(again).toBe(fromRaw);
    });

    it('resolveContextNullable handles null/undefined', () => {
        expect(resolveContextNullable(null)).toBeNull();
        expect(resolveContextNullable(undefined)).toBeNull();
        const ctx = createMockGLContext();
        expect(resolveContextNullable(ctx)).toBe(ctx);
    });

    it('resolveRawGL unwraps both source kinds', () => {
        const gl = createMockGL();
        const ctx = getOrCreateGLContext(gl);
        expect(resolveRawGL(ctx)).toBe(ctx.gl);
        expect(resolveRawGL(gl as ContextSource)).toBe(gl);
    });

    it('does not mutate the input raw gl object', () => {
        const gl: Record<string, unknown> = {
            createTexture: () => ({}),
            createBuffer: () => ({}),
            bindBuffer: () => {},
            bufferData: () => {},
            deleteBuffer: () => {},
        };
        const before = Object.keys(gl).sort().join(',');
        const canvas = { width: 4, height: 4 } as unknown as HTMLCanvasElement;
        new GLContext(gl as unknown as WebGL2RenderingContext, canvas);
        const after = Object.keys(gl).sort().join(',');
        expect(after).toBe(before);
    });

    it('throws on garbage objects without any GL surface', () => {
        const canvas = { width: 1, height: 1 } as unknown as HTMLCanvasElement;
        expect(
            () =>
                new GLContext(
                    { foo: 'bar' } as unknown as WebGL2RenderingContext,
                    canvas
                )
        ).toThrow();
    });
});
