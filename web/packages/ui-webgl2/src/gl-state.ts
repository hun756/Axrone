/**
 * Shared lazy GL state shadow for zero-allocation save/restore of WebGL2
 * state groups.
 *
 * Extracted from the renderer's inline implementation so that world-quad and
 * world-surface can share the same capture/restore pattern without issuing
 * redundant getParameter calls on every draw.
 *
 * Each group is captured lazily — only when (and before) the consumer first
 * writes to it — and restored only when it was actually touched. After
 * restoration the shadow remains valid so subsequent calls skip getParameter
 * entirely (steady-state zero getParameter calls).
 */

// ── State group bit flags ───────────────────────────────────────────────────

export const GL_STATE_FRAMEBUFFER = 1 << 0;
export const GL_STATE_VIEWPORT = 1 << 1;
export const GL_STATE_SCISSOR_BOX = 1 << 2;
export const GL_STATE_SCISSOR_TEST = 1 << 3;
export const GL_STATE_PROGRAM = 1 << 4;
export const GL_STATE_VERTEX_ARRAY = 1 << 5;
export const GL_STATE_ARRAY_BUFFER = 1 << 6;
export const GL_STATE_UNPACK_ALIGNMENT = 1 << 7;
export const GL_STATE_CULL_FACE = 1 << 8;
export const GL_STATE_DEPTH_TEST = 1 << 9;
export const GL_STATE_BLEND = 1 << 10;
export const GL_STATE_BLEND_FUNC = 1 << 11;
export const GL_STATE_ACTIVE_TEXTURE = 1 << 12;
export const GL_STATE_UNIT0_TEXTURE = 1 << 13;
export const GL_STATE_UNIT0_SAMPLER = 1 << 14;
export const GL_STATE_DEPTH_WRITEMASK = 1 << 15;

// ── Shadow interface ────────────────────────────────────────────────────────

export interface GLStateShadow {
    framebuffer: WebGLFramebuffer | null;
    program: WebGLProgram | null;
    vertexArray: WebGLVertexArrayObject | null;
    arrayBuffer: WebGLBuffer | null;
    viewportX: number | undefined;
    viewportY: number | undefined;
    viewportWidth: number | undefined;
    viewportHeight: number | undefined;
    scissorX: number | undefined;
    scissorY: number | undefined;
    scissorWidth: number | undefined;
    scissorHeight: number | undefined;
    scissorTest: boolean | undefined;
    cullFace: boolean | undefined;
    depthTest: boolean | undefined;
    depthWriteMask: boolean | undefined;
    blend: boolean | undefined;
    blendSrcRgb: number | undefined;
    blendDstRgb: number | undefined;
    blendSrcAlpha: number | undefined;
    blendDstAlpha: number | undefined;
    unpackAlignment: number | undefined;
    activeTexture: number;
    unit0Texture: WebGLTexture | null;
    unit0Sampler: WebGLSampler | null;
}

export const createGLStateShadow = (): GLStateShadow => ({
    framebuffer: null,
    program: null,
    vertexArray: null,
    arrayBuffer: null,
    viewportX: undefined,
    viewportY: undefined,
    viewportWidth: undefined,
    viewportHeight: undefined,
    scissorX: undefined,
    scissorY: undefined,
    scissorWidth: undefined,
    scissorHeight: undefined,
    scissorTest: undefined,
    cullFace: undefined,
    depthTest: undefined,
    depthWriteMask: undefined,
    blend: undefined,
    blendSrcRgb: undefined,
    blendDstRgb: undefined,
    blendSrcAlpha: undefined,
    blendDstAlpha: undefined,
    unpackAlignment: undefined,
    activeTexture: 0,
    unit0Texture: null,
    unit0Sampler: null,
});

// ── Helper functions ────────────────────────────────────────────────────────

export function readGLParameter<TValue>(
    gl: WebGL2RenderingContext,
    parameter: number,
    fallback: undefined
): TValue | undefined;
export function readGLParameter<TValue>(
    gl: WebGL2RenderingContext,
    parameter: number,
    fallback: null
): TValue | null;
export function readGLParameter<TValue>(
    gl: WebGL2RenderingContext,
    parameter: number,
    fallback: TValue
): TValue;
export function readGLParameter<TValue>(
    gl: WebGL2RenderingContext,
    parameter: number,
    fallback: TValue | null | undefined
): TValue | null | undefined {
    if (typeof gl.getParameter !== 'function') {
        return fallback;
    }

    try {
        return (gl.getParameter(parameter) as TValue | null | undefined) ?? fallback;
    } catch {
        return fallback;
    }
}

export const readGLEnabled = (
    gl: WebGL2RenderingContext,
    capability: number,
    fallback: boolean | undefined = undefined
): boolean | undefined => {
    if (typeof gl.isEnabled !== 'function') {
        return fallback;
    }

    try {
        return gl.isEnabled(capability);
    } catch {
        return fallback;
    }
};

export const restoreGLEnableState = (
    gl: WebGL2RenderingContext,
    capability: number,
    enabled: boolean | undefined
): void => {
    if (enabled === undefined) {
        return;
    }
    if (enabled) {
        gl.enable(capability);
        return;
    }
    gl.disable(capability);
};

// ── Lazy GL state guard ─────────────────────────────────────────────────────

/**
 * Persistent lazy-capture GL state guard.
 *
 * `capture(groups)` reads only the groups not yet in the shadow. `restore()`
 * writes back every touched group but keeps the shadow valid so subsequent
 * calls skip getParameter entirely. Call `invalidate()` after external state
 * may have changed (e.g. context loss, or another renderer clobbering state).
 */
export class LazyGLStateGuard {
    private readonly shadow: GLStateShadow = createGLStateShadow();
    private capturedGroups = 0;
    private touchedGroups = 0;

    /**
     * Lazily capture the given state groups. Groups already in the shadow are
     * skipped (zero getParameter calls for previously captured groups).
     */
    capture(gl: WebGL2RenderingContext, groups: number): void {
        const pending = groups & ~this.capturedGroups;
        if (pending === 0) {
            this.touchedGroups |= groups;
            return;
        }
        if ((pending & GL_STATE_FRAMEBUFFER) !== 0) {
            this.shadow.framebuffer = readGLParameter<WebGLFramebuffer | null>(gl, gl.FRAMEBUFFER_BINDING, null);
        }
        if ((pending & GL_STATE_VIEWPORT) !== 0) {
            const viewport = readGLParameter<Int32Array | readonly number[] | null>(gl, gl.VIEWPORT, null);
            const valid = viewport !== null && viewport.length >= 4;
            this.shadow.viewportX = valid ? viewport![0] ?? 0 : undefined;
            this.shadow.viewportY = valid ? viewport![1] ?? 0 : undefined;
            this.shadow.viewportWidth = valid ? viewport![2] ?? 0 : undefined;
            this.shadow.viewportHeight = valid ? viewport![3] ?? 0 : undefined;
        }
        if ((pending & GL_STATE_SCISSOR_BOX) !== 0) {
            const scissorBox = readGLParameter<Int32Array | readonly number[] | null>(gl, gl.SCISSOR_BOX, null);
            const valid = scissorBox !== null && scissorBox.length >= 4;
            this.shadow.scissorX = valid ? scissorBox![0] ?? 0 : undefined;
            this.shadow.scissorY = valid ? scissorBox![1] ?? 0 : undefined;
            this.shadow.scissorWidth = valid ? scissorBox![2] ?? 0 : undefined;
            this.shadow.scissorHeight = valid ? scissorBox![3] ?? 0 : undefined;
        }
        if ((pending & GL_STATE_SCISSOR_TEST) !== 0) {
            this.shadow.scissorTest = readGLEnabled(gl, gl.SCISSOR_TEST);
        }
        if ((pending & GL_STATE_PROGRAM) !== 0) {
            this.shadow.program = readGLParameter<WebGLProgram | null>(gl, gl.CURRENT_PROGRAM, null);
        }
        if ((pending & GL_STATE_VERTEX_ARRAY) !== 0) {
            this.shadow.vertexArray = readGLParameter<WebGLVertexArrayObject | null>(gl, gl.VERTEX_ARRAY_BINDING, null);
        }
        if ((pending & GL_STATE_ARRAY_BUFFER) !== 0) {
            this.shadow.arrayBuffer = readGLParameter<WebGLBuffer | null>(gl, gl.ARRAY_BUFFER_BINDING, null);
        }
        if ((pending & GL_STATE_UNPACK_ALIGNMENT) !== 0) {
            this.shadow.unpackAlignment = readGLParameter<number>(gl, gl.UNPACK_ALIGNMENT, undefined);
        }
        if ((pending & GL_STATE_CULL_FACE) !== 0) {
            this.shadow.cullFace = readGLEnabled(gl, gl.CULL_FACE);
        }
        if ((pending & GL_STATE_DEPTH_TEST) !== 0) {
            this.shadow.depthTest = readGLEnabled(gl, gl.DEPTH_TEST);
        }
        if ((pending & GL_STATE_DEPTH_WRITEMASK) !== 0) {
            this.shadow.depthWriteMask = readGLParameter<boolean>(gl, gl.DEPTH_WRITEMASK, undefined);
        }
        if ((pending & GL_STATE_BLEND) !== 0) {
            this.shadow.blend = readGLEnabled(gl, gl.BLEND);
        }
        if ((pending & GL_STATE_BLEND_FUNC) !== 0) {
            this.shadow.blendSrcRgb = readGLParameter<number>(gl, gl.BLEND_SRC_RGB, undefined);
            this.shadow.blendDstRgb = readGLParameter<number>(gl, gl.BLEND_DST_RGB, undefined);
            this.shadow.blendSrcAlpha = readGLParameter<number>(gl, gl.BLEND_SRC_ALPHA, undefined);
            this.shadow.blendDstAlpha = readGLParameter<number>(gl, gl.BLEND_DST_ALPHA, undefined);
        }
        if ((pending & GL_STATE_ACTIVE_TEXTURE) !== 0) {
            this.shadow.activeTexture = readGLParameter<number>(gl, gl.ACTIVE_TEXTURE, gl.TEXTURE0);
        }
        if ((pending & GL_STATE_UNIT0_TEXTURE) !== 0) {
            this.shadow.unit0Texture = readGLParameter<WebGLTexture | null>(gl, gl.TEXTURE_BINDING_2D, null);
        }
        if ((pending & GL_STATE_UNIT0_SAMPLER) !== 0) {
            this.shadow.unit0Sampler = readGLParameter<WebGLSampler | null>(gl, gl.SAMPLER_BINDING, null);
        }
        this.capturedGroups |= pending;
        this.touchedGroups |= groups;
    }

    /** Restore all touched GL state groups from the shadow. */
    restore(gl: WebGL2RenderingContext): void {
        const touched = this.touchedGroups;
        if (touched === 0) {
            return;
        }
        if ((touched & GL_STATE_FRAMEBUFFER) !== 0) {
            gl.bindFramebuffer(gl.FRAMEBUFFER, this.shadow.framebuffer);
        }
        if ((touched & GL_STATE_VIEWPORT) !== 0 && this.shadow.viewportX !== undefined) {
            gl.viewport(this.shadow.viewportX, this.shadow.viewportY ?? 0, this.shadow.viewportWidth ?? 0, this.shadow.viewportHeight ?? 0);
        }
        if ((touched & GL_STATE_CULL_FACE) !== 0) {
            restoreGLEnableState(gl, gl.CULL_FACE, this.shadow.cullFace);
        }
        if ((touched & GL_STATE_DEPTH_TEST) !== 0) {
            restoreGLEnableState(gl, gl.DEPTH_TEST, this.shadow.depthTest);
        }
        if ((touched & GL_STATE_DEPTH_WRITEMASK) !== 0 && this.shadow.depthWriteMask !== undefined) {
            gl.depthMask(this.shadow.depthWriteMask);
        }
        if ((touched & GL_STATE_BLEND) !== 0) {
            restoreGLEnableState(gl, gl.BLEND, this.shadow.blend);
        }
        if ((touched & GL_STATE_BLEND_FUNC) !== 0 && this.shadow.blendSrcRgb !== undefined) {
            if (
                this.shadow.blendDstRgb !== undefined &&
                this.shadow.blendSrcAlpha !== undefined &&
                this.shadow.blendDstAlpha !== undefined &&
                typeof gl.blendFuncSeparate === 'function'
            ) {
                gl.blendFuncSeparate(this.shadow.blendSrcRgb, this.shadow.blendDstRgb, this.shadow.blendSrcAlpha, this.shadow.blendDstAlpha);
            } else if (this.shadow.blendDstRgb !== undefined) {
                gl.blendFunc(this.shadow.blendSrcRgb, this.shadow.blendDstRgb);
            }
        }
        if ((touched & GL_STATE_SCISSOR_TEST) !== 0) {
            restoreGLEnableState(gl, gl.SCISSOR_TEST, this.shadow.scissorTest);
        }
        if ((touched & GL_STATE_SCISSOR_BOX) !== 0 && this.shadow.scissorX !== undefined) {
            gl.scissor(this.shadow.scissorX, this.shadow.scissorY ?? 0, this.shadow.scissorWidth ?? 0, this.shadow.scissorHeight ?? 0);
        }
        if ((touched & GL_STATE_PROGRAM) !== 0) {
            gl.useProgram(this.shadow.program);
        }
        if ((touched & GL_STATE_VERTEX_ARRAY) !== 0) {
            gl.bindVertexArray(this.shadow.vertexArray);
        }
        if ((touched & GL_STATE_ARRAY_BUFFER) !== 0) {
            gl.bindBuffer(gl.ARRAY_BUFFER, this.shadow.arrayBuffer);
        }
        if ((touched & GL_STATE_UNPACK_ALIGNMENT) !== 0 && this.shadow.unpackAlignment !== undefined) {
            gl.pixelStorei?.(gl.UNPACK_ALIGNMENT, this.shadow.unpackAlignment);
        }
        if ((touched & (GL_STATE_UNIT0_TEXTURE | GL_STATE_UNIT0_SAMPLER)) !== 0) {
            if ((touched & GL_STATE_ACTIVE_TEXTURE) !== 0) {
                gl.activeTexture(gl.TEXTURE0);
            }
            if ((touched & GL_STATE_UNIT0_TEXTURE) !== 0) {
                gl.bindTexture(gl.TEXTURE_2D, this.shadow.unit0Texture);
            }
            if ((touched & GL_STATE_UNIT0_SAMPLER) !== 0) {
                gl.bindSampler?.(0, this.shadow.unit0Sampler);
            }
        }
        if ((touched & GL_STATE_ACTIVE_TEXTURE) !== 0) {
            gl.activeTexture(this.shadow.activeTexture);
        }
        // Clear touched but keep captured — the shadow values are still valid.
        this.touchedGroups = 0;
    }

    /** Invalidate the shadow so the next capture re-reads from GL. */
    invalidate(): void {
        this.capturedGroups = 0;
        this.touchedGroups = 0;
    }
}
