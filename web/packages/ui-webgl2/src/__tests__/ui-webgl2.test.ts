import { createGameLoop } from '@axrone/game-loop';
import { describe, expect, it, vi } from 'vitest';
import type { GlyphAtlasEntry, GlyphAtlasPageSnapshot, TextLayoutResult, UIAsset, UIFrame, UIFrameMetrics, WidgetId } from '@axrone/ui/types';
import { UIHost, createLazySceneUIWidgetRef } from '@axrone/scene-runtime/scene-facade';
import {
    WebGL2UIRenderer,
    attachUIOverlayToScene,
    createSceneUIResourceResolver,
    createManagedWebGL2UIOverlayRenderPipelineBackend,
    createUIOverlayRenderPipelineBackend,
} from '../index';
import {
    bindUIHostToScene,
    bindUIHostToWorld,
    bindUIHostsToScene,
    getUIHostBinding,
    getUIHostRuntime,
    resolveUIWidgetRef,
} from '../scene-host';
import { orientQuadTowardCamera, createUIWorldQuadRenderer } from '../world-quad';
import { createUIWorldSurface } from '../world-surface';
import { dispatchWorldPointerToUIRuntime, intersectRayWithUIQuad } from '../world-input';
import { normalizeShaderSource } from '../shader-source';

/** Column-major identity with an optional translation. */
const columnMajorIdentity = (tx = 0, ty = 0, tz = 0): Float32Array =>
    new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, tx, ty, tz, 1]);

/** Minimal structural scene target shared by the UIHost binding suites. */
const createSceneTarget = () => {
    const gl = createMockWebGL2Context();
    const loop = createGameLoop({
        state: { sceneId: 'scene:ui-host' },
        autoStart: false,
    });
    return {
        gl,
        canvas: { width: 320, height: 180 } as HTMLCanvasElement,
        loop,
    };
};

/** HUD asset with one interactive button filling the top-left 100x100. */
const createHostAsset = (): UIAsset => ({
    id: 'ui.hud',
    name: 'HUD',
    version: 1,
    canvas: {
        referenceWidth: 320,
        referenceHeight: 180,
        scaleMode: 'fill',
        matchBias: 0.5,
    },
    bindings: { button: 'hud-button' },
    root: {
        role: 'root',
        enabled: true,
        interactive: false,
        layout: { display: 'overlay', width: '100%', height: '100%' },
        children: [
            {
                role: 'button',
                key: 'hud-button',
                enabled: true,
                interactive: true,
                layout: { width: 100, height: 100 },
                style: { background: '#112233ff' },
                children: [],
            },
        ],
    } as never,
});

const createMetrics = (): UIFrameMetrics => ({
    widgetCount: 2,
    visibleWidgetCount: 2,
    renderCount: 2,
    customCommandCount: 1,
    imageCommandCount: 0,
    textCommandCount: 1,
    glyphCount: 1,
    layoutPasses: 1,
});

const createGlyphEntry = (): GlyphAtlasEntry => ({
    faceId: 1 as GlyphAtlasEntry['faceId'],
    page: 1 as GlyphAtlasEntry['page'],
    pageWidth: 64,
    pageHeight: 64,
    codePoint: 65,
    x: 4,
    y: 6,
    width: 12,
    height: 16,
    format: 'alpha8',
    rowStride: 12,
    distanceRange: 1,
    u0: 4 / 64,
    v0: 6 / 64,
    u1: 16 / 64,
    v1: 22 / 64,
    data: new Uint8Array(12 * 16).fill(255),
});

const createTextLayout = (entry: GlyphAtlasEntry): TextLayoutResult => ({
    faceId: entry.faceId,
    width: 14,
    height: 16,
    lineHeight: 16,
    baseline: 12,
    lines: [
        {
            index: 0,
            start: 0,
            end: 1,
            x: 0,
            y: 0,
            width: 14,
            height: 16,
            ascent: 12,
            descent: 4,
            gapCount: 0,
        },
    ],
    clusters: [
        {
            index: 0,
            line: 0,
            x: 2,
            y: 0,
            width: 14,
            height: 16,
            text: 'A',
            whitespace: false,
            newline: false,
            spanIndex: 0,
        },
    ],
    carets: [
        { index: 0, line: 0, x: 2, y: 0, height: 16 },
        { index: 1, line: 0, x: 16, y: 0, height: 16 },
    ],
    glyphs: [
        {
            codePoint: 65,
            clusterIndex: 0,
            x: 2,
            y: 3,
            advance: 14,
            line: 0,
            text: 'A',
            atlasEntry: entry,
            spanIndex: 0,
        },
    ],
    truncated: false,
    direction: 'ltr',
    text: 'A',
    spanStyles: [],
});

const createFrame = (): UIFrame<{ readonly kind: 'pulse' }> => {
    const glyphEntry = createGlyphEntry();
    return {
        viewportWidth: 160,
        viewportHeight: 120,
        metrics: createMetrics(),
        commands: [
            {
                kind: 'quad',
                widget: 1 as WidgetId,
                x: 8,
                y: 10,
                width: 48,
                height: 20,
                zIndex: 0,
                color: { r: 0.2, g: 0.4, b: 0.8, a: 1 },
                borderColor: { r: 1, g: 1, b: 1, a: 0.5 },
                borderWidth: 2,
                radius: { topLeft: 4, topRight: 4, bottomRight: 4, bottomLeft: 4 },
                opacity: 1,
                clip: { x: 4, y: 8, width: 80, height: 40 },
            },
            {
                kind: 'text',
                widget: 2 as WidgetId,
                x: 40,
                y: 48,
                zIndex: 1,
                color: { r: 1, g: 1, b: 1, a: 1 },
                outlineColor: { r: 0, g: 0, b: 0, a: 0 },
                outlineWidth: 0,
                edgeSoftness: 1,
                opacity: 0.75,
                clip: { x: 16, y: 20, width: 96, height: 36 },
                layout: createTextLayout(glyphEntry),
            },
            {
                kind: 'custom',
                widget: 2 as WidgetId,
                zIndex: 2,
                clip: { x: 0, y: 0, width: 160, height: 120 },
                payload: { kind: 'pulse' as const },
            },
        ],
    };
};

const createMockWebGL2Context = () => {
    let handleId = 0;
    const makeHandle = (kind: string) => ({ kind, id: ++handleId });
    const state = {
        enabled: new Set<number>(),
        viewport: [0, 0, 0, 0],
        scissorBox: [0, 0, 0, 0],
        framebuffer: null as WebGLFramebuffer | null,
        currentProgram: null as WebGLProgram | null,
        vertexArray: null as WebGLVertexArrayObject | null,
        arrayBuffer: null as WebGLBuffer | null,
        unpackAlignment: 4,
        activeTexture: 0x84c0,
        textureBindings: new Map<number, WebGLTexture | null>(),
        samplerBindings: new Map<number, WebGLSampler | null>(),
        blendFunc: [0x0302, 0x0303, 0x0302, 0x0303] as [number, number, number, number],
        depthMask: true,
    };
    return {
        VERTEX_SHADER: 0x8b31,
        FRAGMENT_SHADER: 0x8b30,
        COMPILE_STATUS: 0x8b81,
        LINK_STATUS: 0x8b82,
        ARRAY_BUFFER: 0x8892,
        STATIC_DRAW: 0x88e4,
        DYNAMIC_DRAW: 0x88e8,
        FLOAT: 0x1406,
        TRIANGLE_STRIP: 0x0005,
        CULL_FACE: 0x0b44,
        DEPTH_TEST: 0x0b71,
        BLEND: 0x0be2,
        SRC_ALPHA: 0x0302,
        ONE_MINUS_SRC_ALPHA: 0x0303,
        SCISSOR_TEST: 0x0c11,
        TEXTURE_2D: 0x0de1,
        TEXTURE0: 0x84c0,
        TEXTURE1: 0x84c1,
        TEXTURE_MIN_FILTER: 0x2801,
        TEXTURE_MAG_FILTER: 0x2800,
        TEXTURE_WRAP_S: 0x2802,
        TEXTURE_WRAP_T: 0x2803,
        VIEWPORT: 0x0ba2,
        SCISSOR_BOX: 0x0c10,
        CURRENT_PROGRAM: 0x8b8d,
        VERTEX_ARRAY_BINDING: 0x85b5,
        ARRAY_BUFFER_BINDING: 0x8894,
        ACTIVE_TEXTURE: 0x84e0,
        TEXTURE_BINDING_2D: 0x8069,
        SAMPLER_BINDING: 0x8919,
        FRAMEBUFFER: 0x8d40,
        FRAMEBUFFER_BINDING: 0x8ca6,
        UNPACK_ALIGNMENT: 0x0cf5,
        BLEND_SRC_RGB: 0x80c9,
        BLEND_DST_RGB: 0x80c8,
        BLEND_SRC_ALPHA: 0x80cb,
        BLEND_DST_ALPHA: 0x80ca,
        CLAMP_TO_EDGE: 0x812f,
        LINEAR: 0x2601,
        NEAREST: 0x2600,
        RGBA8: 0x8058,
        RGBA: 0x1908,
        R8: 0x8229,
        RED: 0x1903,
        UNSIGNED_BYTE: 0x1401,
        createShader: vi.fn(() => makeHandle('shader')),
        shaderSource: vi.fn(),
        compileShader: vi.fn(),
        getShaderParameter: vi.fn(() => true),
        getShaderInfoLog: vi.fn(() => ''),
        deleteShader: vi.fn(),
        createProgram: vi.fn(() => makeHandle('program')),
        attachShader: vi.fn(),
        linkProgram: vi.fn(),
        getProgramParameter: vi.fn(() => true),
        getProgramInfoLog: vi.fn(() => ''),
        deleteProgram: vi.fn(),
        getUniformLocation: vi.fn((program, name) => ({ program, name })),
        createBuffer: vi.fn(() => makeHandle('buffer')),
        deleteBuffer: vi.fn(),
        bindBuffer: vi.fn((target, buffer) => {
            if (target === 0x8892) {
                state.arrayBuffer = buffer as WebGLBuffer | null;
            }
        }),
        bufferData: vi.fn(),
        bufferSubData: vi.fn(),
        enableVertexAttribArray: vi.fn(),
        vertexAttribPointer: vi.fn(),
        vertexAttribDivisor: vi.fn(),
        createVertexArray: vi.fn(() => makeHandle('vao')),
        deleteVertexArray: vi.fn(),
        bindVertexArray: vi.fn((vao) => {
            state.vertexArray = vao as WebGLVertexArrayObject | null;
        }),
        createTexture: vi.fn(() => makeHandle('texture')),
        deleteTexture: vi.fn(),
        bindTexture: vi.fn((_target, texture) => {
            state.textureBindings.set(state.activeTexture, texture as WebGLTexture | null);
        }),
        bindSampler: vi.fn((unit, sampler) => {
            state.samplerBindings.set(unit, sampler as WebGLSampler | null);
        }),
        texParameteri: vi.fn(),
        pixelStorei: vi.fn((parameter, value) => {
            if (parameter === 0x0cf5) {
                state.unpackAlignment = value as number;
            }
        }),
        texImage2D: vi.fn(),
        texSubImage2D: vi.fn(),
        viewport: vi.fn((x, y, width, height) => {
            state.viewport = [x as number, y as number, width as number, height as number];
        }),
        disable: vi.fn((capability) => {
            state.enabled.delete(capability as number);
        }),
        enable: vi.fn((capability) => {
            state.enabled.add(capability as number);
        }),
        blendFunc: vi.fn((src, dst) => {
            state.blendFunc = [src as number, dst as number, src as number, dst as number];
        }),
        blendFuncSeparate: vi.fn((srcRgb, dstRgb, srcAlpha, dstAlpha) => {
            state.blendFunc = [
                srcRgb as number,
                dstRgb as number,
                srcAlpha as number,
                dstAlpha as number,
            ];
        }),
        useProgram: vi.fn((program) => {
            state.currentProgram = program as WebGLProgram | null;
        }),
        uniform2f: vi.fn(),
        uniform1i: vi.fn(),
        activeTexture: vi.fn((textureUnit) => {
            state.activeTexture = textureUnit as number;
        }),
        drawArraysInstanced: vi.fn(),
        scissor: vi.fn((x, y, width, height) => {
            state.scissorBox = [x as number, y as number, width as number, height as number];
        }),
        bindFramebuffer: vi.fn((_target, framebuffer) => {
            state.framebuffer = framebuffer as WebGLFramebuffer | null;
        }),
        // World-space UI surface + quad pass surface area.
        COLOR_ATTACHMENT0: 0x8ce0,
        DEPTH_WRITEMASK: 0x0b98,
        createFramebuffer: vi.fn(() => makeHandle('framebuffer')),
        deleteFramebuffer: vi.fn(),
        framebufferTexture2D: vi.fn(),
        checkFramebufferStatus: vi.fn(() => 0x8cd5),
        isTexture: vi.fn(() => true),
        drawArrays: vi.fn(),
        depthMask: vi.fn((flag: boolean) => {
            state.depthMask = flag;
        }),
        clearColor: vi.fn(),
        clear: vi.fn(),
        uniform1f: vi.fn(),
        uniformMatrix4fv: vi.fn(),
        getParameter: vi.fn((parameter) => {
            switch (parameter) {
                case 0x0b98:
                    return state.depthMask;
                case 0x0ba2:
                    return state.viewport;
                case 0x0c10:
                    return state.scissorBox;
                case 0x8b8d:
                    return state.currentProgram;
                case 0x85b5:
                    return state.vertexArray;
                case 0x8894:
                    return state.arrayBuffer;
                case 0x84e0:
                    return state.activeTexture;
                case 0x8069:
                    return state.textureBindings.get(state.activeTexture) ?? null;
                case 0x8919:
                    return state.samplerBindings.get(state.activeTexture - 0x84c0) ?? null;
                case 0x8ca6:
                    return state.framebuffer;
                case 0x0cf5:
                    return state.unpackAlignment;
                case 0x80c9:
                    return state.blendFunc[0];
                case 0x80c8:
                    return state.blendFunc[1];
                case 0x80cb:
                    return state.blendFunc[2];
                case 0x80ca:
                    return state.blendFunc[3];
                default:
                    return null;
            }
        }),
        isEnabled: vi.fn((capability) => state.enabled.has(capability as number)),
    } as unknown as WebGL2RenderingContext;
};

describe('@axrone/ui-webgl2', () => {
    it('renders quad and text batches and uploads glyph pages once', () => {
        const gl = createMockWebGL2Context();
        const customCommandRenderer = vi.fn();
        const renderer = new WebGL2UIRenderer({ gl, customCommandRenderer });
        const frame = createFrame();

        renderer.render(frame);

        expect(gl.drawArraysInstanced).toHaveBeenCalledTimes(2);
        expect(gl.drawArraysInstanced.mock.calls.map((call) => call[3])).toEqual([1, 1]);
        expect(gl.texImage2D).toHaveBeenCalledTimes(1);
        expect(gl.texSubImage2D).toHaveBeenCalledTimes(1);
        expect(gl.scissor).toHaveBeenCalled();
        expect(customCommandRenderer).toHaveBeenCalledTimes(1);
        expect(renderer.getStats()).toEqual({
            drawCalls: 2,
            quadCount: 1,
            imageCount: 0,
            materialImageCount: 0,
            glyphCount: 1,
            customCommandCount: 1,
            uploadedGlyphCount: 1,
            atlasPageCount: 1,
        });

        renderer.render(frame);

        expect(gl.texSubImage2D).toHaveBeenCalledTimes(1);
        expect(renderer.getStats().uploadedGlyphCount).toBe(0);

        renderer.dispose();
    });

    it('restores the previous WebGL state after rendering a UI frame', () => {
        const gl = createMockWebGL2Context();
        const previousProgram = { id: 'previous-program' } as unknown as WebGLProgram;
        const previousVao = { id: 'previous-vao' } as unknown as WebGLVertexArrayObject;
        const previousBuffer = { id: 'previous-buffer' } as unknown as WebGLBuffer;
        const previousTexture = { id: 'previous-texture' } as unknown as WebGLTexture;
        const previousSampler = { id: 'previous-sampler' } as unknown as WebGLSampler;
        const previousFramebuffer = { id: 'previous-framebuffer' } as unknown as WebGLFramebuffer;
        const renderer = new WebGL2UIRenderer({ gl });

        gl.bindFramebuffer(gl.FRAMEBUFFER, previousFramebuffer);
        gl.viewport(3, 4, 320, 180);
        gl.enable(gl.CULL_FACE);
        gl.enable(gl.DEPTH_TEST);
        gl.disable(gl.BLEND);
        gl.enable(gl.SCISSOR_TEST);
        gl.scissor(5, 6, 70, 80);
        gl.useProgram(previousProgram);
        gl.bindVertexArray(previousVao);
        gl.bindBuffer(gl.ARRAY_BUFFER, previousBuffer);
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 4);
        gl.blendFuncSeparate(gl.ONE_MINUS_SRC_ALPHA, gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.SRC_ALPHA);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, previousTexture);
        gl.bindSampler(0, previousSampler);
        gl.activeTexture(gl.TEXTURE1);

        renderer.render(createFrame());

        expect(gl.getParameter(gl.FRAMEBUFFER_BINDING)).toBe(previousFramebuffer);
        expect(gl.getParameter(gl.CURRENT_PROGRAM)).toBe(previousProgram);
        expect(gl.getParameter(gl.VERTEX_ARRAY_BINDING)).toBe(previousVao);
        expect(gl.getParameter(gl.ARRAY_BUFFER_BINDING)).toBe(previousBuffer);
        expect(gl.getParameter(gl.ACTIVE_TEXTURE)).toBe(gl.TEXTURE1);
        gl.activeTexture(gl.TEXTURE0);
        expect(gl.getParameter(gl.TEXTURE_BINDING_2D)).toBe(previousTexture);
        expect(gl.getParameter(gl.SAMPLER_BINDING)).toBe(previousSampler);
        expect(gl.getParameter(gl.VIEWPORT)).toEqual([3, 4, 320, 180]);
        expect(gl.getParameter(gl.SCISSOR_BOX)).toEqual([5, 6, 70, 80]);
        expect(gl.isEnabled(gl.CULL_FACE)).toBe(true);
        expect(gl.isEnabled(gl.DEPTH_TEST)).toBe(true);
        expect(gl.isEnabled(gl.BLEND)).toBe(false);
        expect(gl.isEnabled(gl.SCISSOR_TEST)).toBe(true);
        expect(gl.getParameter(gl.UNPACK_ALIGNMENT)).toBe(4);
        expect(gl.getParameter(gl.BLEND_SRC_RGB)).toBe(gl.ONE_MINUS_SRC_ALPHA);
        expect(gl.getParameter(gl.BLEND_DST_RGB)).toBe(gl.SRC_ALPHA);
        expect(gl.getParameter(gl.BLEND_SRC_ALPHA)).toBe(gl.ONE_MINUS_SRC_ALPHA);
        expect(gl.getParameter(gl.BLEND_DST_ALPHA)).toBe(gl.SRC_ALPHA);
    });

    it('renders into an offscreen framebuffer and restores the previous binding', () => {
        const gl = createMockWebGL2Context();
        const previousFramebuffer = { id: 'previous-framebuffer' } as unknown as WebGLFramebuffer;
        const offscreen = { id: 'offscreen-framebuffer' } as unknown as WebGLFramebuffer;
        const renderer = new WebGL2UIRenderer({ gl });

        gl.bindFramebuffer(gl.FRAMEBUFFER, previousFramebuffer);
        renderer.render(createFrame(), { framebuffer: offscreen });

        // The UI must have been drawn against the offscreen target...
        expect(gl.bindFramebuffer).toHaveBeenCalledWith(gl.FRAMEBUFFER, offscreen);
        // ...and the scene's binding restored on the way out.
        expect(gl.getParameter(gl.FRAMEBUFFER_BINDING)).toBe(previousFramebuffer);
    });

    it('draws to the default framebuffer when the render options omit a target', () => {
        const gl = createMockWebGL2Context();
        const previousFramebuffer = { id: 'previous-framebuffer' } as unknown as WebGLFramebuffer;
        const renderer = new WebGL2UIRenderer({ gl });

        gl.bindFramebuffer(gl.FRAMEBUFFER, previousFramebuffer);
        renderer.render(createFrame());

        // No target override: the binding is left untouched during the pass.
        expect(gl.bindFramebuffer).not.toHaveBeenCalledWith(gl.FRAMEBUFFER, null);
        expect(gl.getParameter(gl.FRAMEBUFFER_BINDING)).toBe(previousFramebuffer);
    });

    it('decorates the pipeline backend and renders UI after the base backend ends the frame', async () => {
        const order: string[] = [];
        const frame = createFrame();
        const ui = vi.fn(() => frame);
        const renderer = {
            render: vi.fn(() => {
                order.push('ui');
            }),
        };
        const backend = createUIOverlayRenderPipelineBackend({
            base: {
                beginFrame: vi.fn(async () => {
                    order.push('begin');
                }),
                executePass: vi.fn(async () => {
                    order.push('pass');
                }),
                endFrame: vi.fn(async () => {
                    order.push('base-end');
                }),
            },
            renderer,
            ui,
        });
        const context = {
            viewport: { width: 320, height: 180 },
        } as Parameters<NonNullable<typeof backend.beginFrame>>[0];

        await backend.beginFrame?.(context);
        await backend.executePass?.({} as never, context);
        await backend.endFrame?.({} as never, context);

        expect(order).toEqual(['begin', 'pass', 'base-end', 'ui']);
        expect(ui).toHaveBeenCalledWith({ width: 320, height: 180 });
        expect(renderer.render).toHaveBeenCalledWith(frame);
    });

    it('lazily creates, reuses, and disposes managed renderers across WebGL context changes', async () => {
        const glA = createMockWebGL2Context();
        const glB = createMockWebGL2Context();
        const frame = createFrame();
        const backend = createManagedWebGL2UIOverlayRenderPipelineBackend({
            ui: () => frame,
            getGL: ({ context }) => (context.frame < 2 ? glA : glB),
        });
        const contextFor = (frameIndex: number) =>
            ({
                frame: frameIndex,
                viewport: { width: 160, height: 120 },
            }) as Parameters<NonNullable<typeof backend.endFrame>>[1];
        const result = {
            frame: 0,
            viewport: { width: 160, height: 120 },
            passes: [],
            resources: [],
            statistics: createMetrics() as never,
            degraded: false,
            warnings: [],
        } as Parameters<NonNullable<typeof backend.endFrame>>[0];

        await backend.endFrame?.(result, contextFor(0));
        await backend.endFrame?.(result, contextFor(1));

        expect(glA.createProgram).toHaveBeenCalledTimes(3);
        expect(glA.deleteProgram).not.toHaveBeenCalled();

        await backend.endFrame?.(result, contextFor(2));

        expect(glB.createProgram).toHaveBeenCalledTimes(3);
        expect(glA.deleteProgram).toHaveBeenCalledTimes(3);

        backend.dispose();

        expect(glB.deleteProgram).toHaveBeenCalledTimes(3);
    });

    it('attaches UI rendering to the scene after-frame hook', () => {
        const gl = createMockWebGL2Context();
        const loop = createGameLoop({
            state: { sceneId: 'scene:test' },
            autoStart: false,
        });
        const frame = createFrame();
        const overlay = attachUIOverlayToScene(
            {
                gl,
                canvas: { width: 320, height: 180 } as HTMLCanvasElement,
                loop,
            },
            {
                systemId: 'ui.overlay.test',
                ui: () => frame,
            }
        );
        const system = loop.getSystem('ui.overlay.test');

        expect(system).toBeDefined();
        system?.afterFrame?.({} as never);

        expect(gl.drawArraysInstanced).toHaveBeenCalledTimes(2);
        expect(overlay.render()).toBe(frame);

        overlay.dispose();

        expect(loop.getSystem('ui.overlay.test')).toBeUndefined();
        expect(gl.deleteProgram).toHaveBeenCalledTimes(3);
    });

    it('renders texture images and delegates material-backed image commands', () => {
        const gl = createMockWebGL2Context();
        const texture = { id: 'texture:image' } as unknown as WebGLTexture;
        const sampler = { id: 'sampler:image' } as unknown as WebGLSampler;
        const materialRender = vi.fn();
        const renderer = new WebGL2UIRenderer({
            gl,
            resolveImageResource(source) {
                if (source.kind === 'material') {
                    return {
                        kind: 'material',
                        render: materialRender,
                    };
                }
                return {
                    kind: 'texture',
                    texture,
                    sampler,
                };
            },
        });
        const frame: UIFrame<never> = {
            viewportWidth: 128,
            viewportHeight: 96,
            metrics: {
                ...createMetrics(),
                renderCount: 2,
                imageCommandCount: 2,
                textCommandCount: 0,
                customCommandCount: 0,
                glyphCount: 0,
            },
            commands: [
                {
                    kind: 'image',
                    widget: 1 as WidgetId,
                    source: {
                        kind: 'texture',
                        resourceId: 'ui:texture',
                        width: 32,
                        height: 32,
                    },
                    x: 8,
                    y: 10,
                    width: 32,
                    height: 32,
                    zIndex: 0,
                    tint: { r: 1, g: 1, b: 1, a: 1 },
                    opacity: 1,
                    sampling: 'linear',
                    radius: { topLeft: 4, topRight: 4, bottomRight: 4, bottomLeft: 4 },
                    clip: null,
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                },
                {
                    kind: 'image',
                    widget: 2 as WidgetId,
                    source: {
                        kind: 'material',
                        materialId: 'ui:material',
                        width: 48,
                        height: 24,
                    },
                    x: 40,
                    y: 18,
                    width: 48,
                    height: 24,
                    zIndex: 1,
                    tint: { r: 1, g: 1, b: 1, a: 1 },
                    opacity: 0.85,
                    sampling: 'nearest',
                    radius: { topLeft: 0, topRight: 0, bottomRight: 0, bottomLeft: 0 },
                    clip: { x: 0, y: 0, width: 96, height: 80 },
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                },
            ],
        };

        renderer.render(frame);

        expect(gl.drawArraysInstanced).toHaveBeenCalledTimes(1);
    expect(gl.bindSampler).toHaveBeenCalledWith(0, sampler);
        expect(materialRender).toHaveBeenCalledTimes(1);
        expect(renderer.getStats()).toEqual({
            drawCalls: 1,
            quadCount: 0,
            imageCount: 2,
            materialImageCount: 1,
            glyphCount: 0,
            customCommandCount: 0,
            uploadedGlyphCount: 0,
            atlasPageCount: 0,
        });
    });

    it('resolves scene texture and material image sources into native WebGL handles', () => {
        const nativeTexture = { id: 'native:texture' } as unknown as WebGLTexture;
        const nativeSampler = { id: 'native:sampler' } as unknown as WebGLSampler;
        const scene = {
            getTextureResource: vi.fn((id: string) =>
                id === 'scene:icon'
                    ? {
                          id,
                          width: 64,
                          height: 64,
                          samplerId: 'ui',
                          nativeTexture,
                          nativeSampler,
                      }
                    : null
            ),
            getMaterialTextureBinding: vi.fn((materialId: string, uniformName?: string) =>
                materialId === 'scene:card' && uniformName === 'u_BaseColor'
                    ? {
                          materialId,
                          uniformName,
                          textureId: 'scene:albedo',
                          samplerId: 'ui',
                          unit: 0,
                          width: 128,
                          height: 128,
                          nativeTexture,
                          nativeSampler,
                      }
                    : null
            ),
        };
        const resolver = createSceneUIResourceResolver(scene, {
            materialTextureBinding: 'u_BaseColor',
        });
        const context = {
            gl: createMockWebGL2Context(),
            frame: createFrame(),
            command: {
                kind: 'image',
                widget: 1 as WidgetId,
                source: {
                    kind: 'texture',
                    resourceId: 'scene:icon',
                    width: 64,
                    height: 64,
                },
                x: 0,
                y: 0,
                width: 64,
                height: 64,
                zIndex: 0,
                tint: { r: 1, g: 1, b: 1, a: 1 },
                opacity: 1,
                sampling: 'linear' as const,
                radius: { topLeft: 0, topRight: 0, bottomRight: 0, bottomLeft: 0 },
                clip: null,
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
            },
        };

        const textureResource = resolver(
            {
                kind: 'texture',
                resourceId: 'scene:icon',
                width: 64,
                height: 64,
            },
            context
        );
        const materialResource = resolver(
            {
                kind: 'material',
                materialId: 'scene:card',
                textureBinding: 'u_BaseColor',
                width: 128,
                height: 128,
            },
            {
                ...context,
                command: {
                    ...context.command,
                    source: {
                        kind: 'material',
                        materialId: 'scene:card',
                        textureBinding: 'u_BaseColor',
                        width: 128,
                        height: 128,
                    },
                },
            }
        );

        expect(textureResource).toEqual({ kind: 'texture', texture: nativeTexture, sampler: nativeSampler });
        expect(materialResource).toEqual({ kind: 'texture', texture: nativeTexture, sampler: nativeSampler });
        expect(scene.getTextureResource).toHaveBeenCalledWith('scene:icon');
        expect(scene.getMaterialTextureBinding).toHaveBeenCalledWith('scene:card', 'u_BaseColor');
    });

    it('packs sdf text styling into the text batch for outline rendering', () => {
        const gl = createMockWebGL2Context();
        const renderer = new WebGL2UIRenderer({ gl });
        const sdfGlyph = {
            ...createGlyphEntry(),
            format: 'sdf8' as const,
            distanceRange: 6,
            data: new Uint8Array(12 * 16).fill(127),
        };
        const frame: UIFrame<never> = {
            viewportWidth: 128,
            viewportHeight: 64,
            metrics: {
                ...createMetrics(),
                renderCount: 1,
                textCommandCount: 1,
                glyphCount: 1,
                customCommandCount: 0,
            },
            commands: [
                {
                    kind: 'text',
                    widget: 1 as WidgetId,
                    x: 12,
                    y: 20,
                    zIndex: 0,
                    color: { r: 1, g: 1, b: 1, a: 1 },
                    outlineColor: { r: 0.1, g: 0.8, b: 1, a: 1 },
                    outlineWidth: 1.5,
                    edgeSoftness: 1.25,
                    opacity: 1,
                    clip: null,
                    layout: createTextLayout(sdfGlyph),
                },
            ],
        };

        renderer.render(frame);

        const dynamicFloatUploads = gl.bufferSubData.mock.calls
            .map((call) => call[2])
            .filter((value): value is Float32Array => value instanceof Float32Array && value.length === 26);

        expect(dynamicFloatUploads).toHaveLength(1);
        expect(dynamicFloatUploads[0]?.[16]).toBe(1);
        expect(dynamicFloatUploads[0]?.[17]).toBe(6);
        expect(dynamicFloatUploads[0]?.[18]).toBe(1.5);
        expect(dynamicFloatUploads[0]?.[19]).toBe(1.25);
        expect(Array.from(dynamicFloatUploads[0]?.slice(20, 26) ?? [])).toEqual([1, 0, 0, 0, 1, 0]);
    });

    it('uploads distinct raster sizes for the same glyph code point on a shared atlas page', () => {
        const gl = createMockWebGL2Context();
        const renderer = new WebGL2UIRenderer({ gl });
        const smallGlyph: GlyphAtlasEntry = {
            ...createGlyphEntry(),
            rasterSize: 18,
            x: 4,
            y: 6,
            width: 12,
            height: 16,
            rowStride: 12,
            u0: 4 / 64,
            v0: 6 / 64,
            u1: 16 / 64,
            v1: 22 / 64,
            data: new Uint8Array(12 * 16).fill(80),
        };
        const largeGlyph: GlyphAtlasEntry = {
            ...createGlyphEntry(),
            rasterSize: 32,
            x: 20,
            y: 6,
            width: 20,
            height: 24,
            rowStride: 20,
            u0: 20 / 64,
            v0: 6 / 64,
            u1: 40 / 64,
            v1: 30 / 64,
            data: new Uint8Array(20 * 24).fill(160),
        };
        const frame: UIFrame<never> = {
            viewportWidth: 160,
            viewportHeight: 96,
            metrics: {
                ...createMetrics(),
                renderCount: 1,
                textCommandCount: 1,
                glyphCount: 2,
                customCommandCount: 0,
            },
            commands: [
                {
                    kind: 'text',
                    widget: 1 as WidgetId,
                    x: 12,
                    y: 20,
                    zIndex: 0,
                    color: { r: 1, g: 1, b: 1, a: 1 },
                    outlineColor: { r: 0, g: 0, b: 0, a: 0 },
                    outlineWidth: 0,
                    edgeSoftness: 1,
                    opacity: 1,
                    clip: null,
                    layout: {
                        faceId: smallGlyph.faceId,
                        width: 32,
                        height: 24,
                        lineHeight: 24,
                        baseline: 18,
                        lines: [
                            {
                                index: 0,
                                start: 0,
                                end: 2,
                                x: 0,
                                y: 0,
                                width: 32,
                                height: 24,
                                ascent: 18,
                                descent: 6,
                                gapCount: 0,
                            },
                        ],
                        clusters: [
                            {
                                index: 0,
                                line: 0,
                                x: 0,
                                y: 0,
                                width: 12,
                                height: 24,
                                text: 'A',
                                whitespace: false,
                                newline: false,
                                spanIndex: 0,
                            },
                            {
                                index: 1,
                                line: 0,
                                x: 12,
                                y: 0,
                                width: 20,
                                height: 24,
                                text: 'A',
                                whitespace: false,
                                newline: false,
                                spanIndex: 0,
                            },
                        ],
                        carets: [
                            { index: 0, line: 0, x: 0, y: 0, height: 24 },
                            { index: 1, line: 0, x: 12, y: 0, height: 24 },
                            { index: 2, line: 0, x: 32, y: 0, height: 24 },
                        ],
                        glyphs: [
                            {
                                codePoint: 65,
                                clusterIndex: 0,
                                x: 0,
                                y: 4,
                                advance: 12,
                                width: 12,
                                height: 16,
                                line: 0,
                                text: 'A',
                                atlasEntry: smallGlyph,
                                spanIndex: 0,
                            },
                            {
                                codePoint: 65,
                                clusterIndex: 1,
                                x: 12,
                                y: 0,
                                advance: 20,
                                width: 20,
                                height: 24,
                                line: 0,
                                text: 'A',
                                atlasEntry: largeGlyph,
                                spanIndex: 0,
                            },
                        ],
                        truncated: false,
                        direction: 'ltr',
                        text: 'AA',
                        spanStyles: [],
                    },
                },
            ],
        };

        renderer.render(frame);

        expect(gl.texSubImage2D).toHaveBeenCalledTimes(2);
        expect(renderer.getStats().uploadedGlyphCount).toBe(2);
    });

    it('creates distinct glyph pages for faceId >= 65536 without overflow', () => {
        const gl = createMockWebGL2Context();
        const renderer = new WebGL2UIRenderer({ gl });
        const baseEntry = createGlyphEntry();
        const smallFaceEntry: GlyphAtlasEntry = { ...baseEntry, faceId: 1 as GlyphAtlasEntry['faceId'], page: 0 as GlyphAtlasEntry['page'] };
        const largeFaceEntry: GlyphAtlasEntry = { ...baseEntry, faceId: 70000 as GlyphAtlasEntry['faceId'], page: 0 as GlyphAtlasEntry['page'] };
        const frame: UIFrame<never> = {
            viewportWidth: 160,
            viewportHeight: 96,
            metrics: { ...createMetrics(), renderCount: 1, textCommandCount: 2, glyphCount: 2, customCommandCount: 0 },
            commands: [
                {
                    kind: 'text',
                    widget: 1 as WidgetId,
                    x: 0, y: 0, zIndex: 0,
                    color: { r: 1, g: 1, b: 1, a: 1 },
                    outlineColor: { r: 0, g: 0, b: 0, a: 0 },
                    outlineWidth: 0, edgeSoftness: 0, opacity: 1, clip: null,
                    layout: { ...createTextLayout(smallFaceEntry), glyphs: [{ ...createTextLayout(smallFaceEntry).glyphs[0], atlasEntry: smallFaceEntry }] },
                },
                {
                    kind: 'text',
                    widget: 2 as WidgetId,
                    x: 0, y: 20, zIndex: 1,
                    color: { r: 1, g: 1, b: 1, a: 1 },
                    outlineColor: { r: 0, g: 0, b: 0, a: 0 },
                    outlineWidth: 0, edgeSoftness: 0, opacity: 1, clip: null,
                    layout: { ...createTextLayout(largeFaceEntry), glyphs: [{ ...createTextLayout(largeFaceEntry).glyphs[0], atlasEntry: largeFaceEntry }] },
                },
            ],
        };
        renderer.render(frame);
        // Both glyphs uploaded separately means distinct page keys.
        expect(renderer.getStats().uploadedGlyphCount).toBe(2);
        expect(renderer.getStats().atlasPageCount).toBe(2);
        renderer.dispose();
    });
});

describe('scene-host UIHost binding', () => {
    const createInputTarget = () => {
        const listeners = new Map<string, Set<(event: unknown) => void>>();
        return {
            addEventListener: vi.fn((type: string, listener: (event: never) => void) => {
                const bucket = listeners.get(type) ?? new Set();
                bucket.add(listener as (event: unknown) => void);
                listeners.set(type, bucket);
            }),
            removeEventListener: vi.fn((type: string, listener: (event: never) => void) => {
                listeners.get(type)?.delete(listener as (event: unknown) => void);
            }),
            getBoundingClientRect: () => ({ left: 0, top: 0, width: 320, height: 180 }),
            emit(type: string, event: unknown) {
                for (const listener of listeners.get(type) ?? []) {
                    listener(event);
                }
            },
            listenerCount(type: string) {
                return listeners.get(type)?.size ?? 0;
            },
        };
    };

    it('wires pointer input with coordinate mapping when receiveInput is enabled', () => {
        const scene = createSceneTarget();
        const target = createInputTarget();
        const host = new UIHost({ assetId: 'ui.hud', receiveInput: true });
        const resolveAsset = vi.fn(() => createHostAsset());

        const handle = bindUIHostToScene({
            scene,
            host,
            resolveAsset,
            input: { target },
        });

        expect(handle).not.toBeNull();
        expect(resolveAsset).toHaveBeenCalledWith('ui.hud');
        expect(target.listenerCount('pointerdown')).toBe(1);
        expect(target.listenerCount('pointermove')).toBe(1);
        expect(target.listenerCount('keydown')).toBe(0);

        const pointerDown = vi.fn(() => true);
        const button = handle!.runtime.getBoundWidget('button');
        expect(button).not.toBeNull();
        handle!.runtime.updateWidget(button!, { handlers: { pointerDown } });
        handle!.render(320, 180);

        // CSS rect is 320x180 and framebuffer is 320x180 => 1:1 mapping
        target.emit('pointerdown', { clientX: 40, clientY: 40 });
        expect(pointerDown).toHaveBeenCalledTimes(1);

        target.emit('pointerdown', { clientX: 200, clientY: 150 });
        expect(pointerDown).toHaveBeenCalledTimes(1);

        handle!.dispose();
        expect(target.listenerCount('pointerdown')).toBe(0);
        expect(target.listenerCount('pointermove')).toBe(0);
    });

    it('fires widget handlers when pointerdown arrives before the first render', () => {
        // Regression: the original bug had input ordering-coupled to a prior
        // commitToViewport() call via dispatchViewportInput. After the fix,
        // dispatchInput is used directly with reference-space coords, so input
        // works as soon as layout is available (one commitToViewport call)
        // without needing the overlay render pipeline to run first.
        const scene = createSceneTarget();
        const target = createInputTarget();
        const host = new UIHost({ assetId: 'ui.hud', receiveInput: true });
    
        const handle = bindUIHostToScene({
            scene,
            host,
            resolveAsset: () => createHostAsset(),
            input: { target },
        });
        expect(handle).not.toBeNull();
    
        const pointerDown = vi.fn(() => true);
        handle!.runtime.updateWidget(handle!.runtime.getBoundWidget('button')!, {
            handlers: { pointerDown },
        });
    
        // Trigger layout computation without going through the overlay pipeline.
        handle!.runtime.commitToViewport(320, 180);
    
        // Input must work without handle.render() or the overlay system running.
        target.emit('pointerdown', { clientX: 40, clientY: 40 });
        expect(pointerDown).toHaveBeenCalledTimes(1);
    
        handle!.dispose();
    });
    
    it('hits widgets with non-zero radius and borderWidth before first render', () => {
        // Ensures the input path works for styled widgets (radius/border)
        // without going through the full overlay render pipeline.
        const styledAsset = (): UIAsset => ({
            ...createHostAsset(),
            root: {
                role: 'root',
                enabled: true,
                interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'button',
                        key: 'hud-button',
                        enabled: true,
                        interactive: true,
                        layout: { width: 100, height: 100 },
                        style: { background: '#112233ff', radius: 12, borderWidth: 3, borderColor: '#ffffffff' },
                        children: [],
                    },
                ],
            } as never,
        });
        const scene = createSceneTarget();
        const target = createInputTarget();
        const host = new UIHost({ assetId: 'ui.hud', receiveInput: true });
    
        const handle = bindUIHostToScene({
            scene,
            host,
            resolveAsset: () => styledAsset(),
            input: { target },
        });
    
        const pointerDown = vi.fn(() => true);
        handle!.runtime.updateWidget(handle!.runtime.getBoundWidget('button')!, {
            handlers: { pointerDown },
        });
    
        // Trigger layout computation directly.
        handle!.runtime.commitToViewport(320, 180);
    
        // Inside the 100x100 styled button
        target.emit('pointerdown', { clientX: 50, clientY: 50 });
        expect(pointerDown).toHaveBeenCalledTimes(1);
    
        // Outside the button
        target.emit('pointerdown', { clientX: 200, clientY: 150 });
        expect(pointerDown).toHaveBeenCalledTimes(1);
    
        handle!.dispose();
    });
    
    it('mapsCSS-scaled client coordinates into framebuffer pixels', () => {
        const scene = createSceneTarget();
        const target = createInputTarget();
        // CSS size is half the framebuffer: 160x90 vs 320x180 => 2x scale
        target.getBoundingClientRect = () => ({ left: 0, top: 0, width: 160, height: 90 });
        const host = new UIHost({ assetId: 'ui.hud', receiveInput: true });

        const handle = bindUIHostToScene({
            scene,
            host,
            resolveAsset: () => createHostAsset(),
            input: { target },
        });
        expect(handle).not.toBeNull();

        const pointerDown = vi.fn(() => true);
        handle!.runtime.updateWidget(handle!.runtime.getBoundWidget('button')!, {
            handlers: { pointerDown },
        });
        handle!.render(320, 180);

        // client (40, 40) -> framebuffer (80, 80): inside the 100x100 button
        target.emit('pointerdown', { clientX: 40, clientY: 40 });
        expect(pointerDown).toHaveBeenCalledTimes(1);

        // client (70, 70) -> framebuffer (140, 140): outside the button
        target.emit('pointerdown', { clientX: 70, clientY: 70 });
        expect(pointerDown).toHaveBeenCalledTimes(1);

        handle!.dispose();
    });

    it('correctly maps input under match-width-or-height letterboxing for all anchor positions', () => {
        // Regression test: verifies that commitToViewport + dispatchViewportInput
        // produce consistent coordinate mapping when the viewport aspect ratio differs
        // from the reference, causing letterbox/pillarbox offsets.
        const letterboxScene = {
            gl: createMockWebGL2Context(),
            canvas: { width: 905, height: 961 } as HTMLCanvasElement,
            loop: createGameLoop({ state: { sceneId: 'scene:ui-host' }, autoStart: false }),
        };
        const target = createInputTarget();
        // CSS size matches the framebuffer (devicePixelRatio = 1)
        target.getBoundingClientRect = () => ({ left: 0, top: 0, width: 905, height: 961 });

        // Asset with 1920x1080 reference and match-width-or-height scaling (bias 0.45)
        // This matches the real UI-test.ui.json canvas config.
        const letterboxAsset = (): UIAsset => ({
            id: 'ui.letterbox',
            name: 'LetterboxTest',
            version: 1,
            canvas: {
                referenceWidth: 1920,
                referenceHeight: 1080,
                scaleMode: 'match-width-or-height',
                matchBias: 0.45,
            },
            bindings: {
                centerButton: 'center-btn',
                leftButton: 'left-btn',
                rightButton: 'right-btn',
            },
            root: {
                role: 'root',
                enabled: true,
                interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'button',
                        key: 'center-btn',
                        enabled: true,
                        interactive: true,
                        layout: {
                            position: 'absolute',
                            width: 200,
                            height: 60,
                            anchor: { x: 0.5, y: 0.5, maxX: 0.5, maxY: 0.5, pivotX: 0.5, pivotY: 0.5 },
                        },
                        style: { background: '#0a74daff' },
                        children: [],
                    },
                    {
                        role: 'button',
                        key: 'left-btn',
                        enabled: true,
                        interactive: true,
                        layout: {
                            position: 'absolute',
                            width: 200,
                            height: 60,
                            anchor: { x: 0, y: 0.5, maxX: 0, maxY: 0.5, pivotX: 0, pivotY: 0.5, offsetX: 100, offsetY: 0 },
                        },
                        style: { background: '#0a74daff' },
                        children: [],
                    },
                    {
                        role: 'button',
                        key: 'right-btn',
                        enabled: true,
                        interactive: true,
                        layout: {
                            position: 'absolute',
                            width: 200,
                            height: 60,
                            anchor: { x: 1, y: 0.5, maxX: 1, maxY: 0.5, pivotX: 1, pivotY: 0.5, offsetX: -100, offsetY: 0 },
                        },
                        style: { background: '#0a74daff' },
                        children: [],
                    },
                ],
            } as never,
        });

        const host = new UIHost({ assetId: 'ui.letterbox', receiveInput: true });
        const handle = bindUIHostToScene({
            scene: letterboxScene,
            host,
            resolveAsset: () => letterboxAsset(),
            input: { target },
        });
        expect(handle).not.toBeNull();

        // Render at the framebuffer size (905x961) — this sets lastViewport.
        handle!.render(905, 961);

        const centerHandler = vi.fn(() => true);
        const leftHandler = vi.fn(() => true);
        const rightHandler = vi.fn(() => true);
        handle!.runtime.updateWidget(handle!.runtime.getBoundWidget('centerButton')!, { handlers: { pointerDown: centerHandler } });
        handle!.runtime.updateWidget(handle!.runtime.getBoundWidget('leftButton')!, { handlers: { pointerDown: leftHandler } });
        handle!.runtime.updateWidget(handle!.runtime.getBoundWidget('rightButton')!, { handlers: { pointerDown: rightHandler } });

        // Verify layout boxes to know exact reference positions
        const centerBox = handle!.runtime.getLayoutBox(handle!.runtime.getBoundWidget('centerButton')!);
        const leftBox = handle!.runtime.getLayoutBox(handle!.runtime.getBoundWidget('leftButton')!);
        const rightBox = handle!.runtime.getLayoutBox(handle!.runtime.getBoundWidget('rightButton')!);

        // Compute the canvas scale for this viewport
        // scaleW = 905/1920 ≈ 0.4714, scaleH = 961/1080 ≈ 0.8898
        // blended = 0.4714 * 0.55 + 0.8898 * 0.45 ≈ 0.6597
        const scale = 0.4714 * 0.55 + 0.8898 * 0.45;
        const effectiveW = 1920 * scale;
        const offsetX = (905 - effectiveW) / 2;
        const effectiveH = 1080 * scale;
        const offsetY = (961 - effectiveH) / 2;

        // Helper: convert reference point to framebuffer point
        const refToFramebuffer = (refX: number, refY: number) => ({
            x: refX * scale + offsetX,
            y: refY * scale + offsetY,
        });

        // Test CENTER button (anchored at center of 1920x1080 reference)
        const centerRef = { x: centerBox.x + centerBox.width / 2, y: centerBox.y + centerBox.height / 2 };
        const centerFb = refToFramebuffer(centerRef.x, centerRef.y);
        target.emit('pointerdown', { clientX: centerFb.x, clientY: centerFb.y });
        expect(centerHandler).toHaveBeenCalledTimes(1);

        // Test LEFT button (anchored at left side, offsetX=100)
        const leftRef = { x: leftBox.x + leftBox.width / 2, y: leftBox.y + leftBox.height / 2 };
        const leftFb = refToFramebuffer(leftRef.x, leftRef.y);
        target.emit('pointerdown', { clientX: leftFb.x, clientY: leftFb.y });
        expect(leftHandler).toHaveBeenCalledTimes(1);

        // Test RIGHT button (anchored at right side, offsetX=-100)
        const rightRef = { x: rightBox.x + rightBox.width / 2, y: rightBox.y + rightBox.height / 2 };
        const rightFb = refToFramebuffer(rightRef.x, rightRef.y);
        target.emit('pointerdown', { clientX: rightFb.x, clientY: rightFb.y });
        expect(rightHandler).toHaveBeenCalledTimes(1);

        handle!.dispose();
    });

    it('correctly maps input under match-height pillarboxing', () => {
        // When the viewport is wider than the reference aspect ratio,
        // match-height scale mode produces pillarbox (vertical bars).
        const pillarboxScene = {
            gl: createMockWebGL2Context(),
            canvas: { width: 1920, height: 800 } as HTMLCanvasElement,
            loop: createGameLoop({ state: { sceneId: 'scene:ui-host' }, autoStart: false }),
        };
        const target = createInputTarget();
        target.getBoundingClientRect = () => ({ left: 0, top: 0, width: 1920, height: 800 });

        const pillarboxAsset = (): UIAsset => ({
            id: 'ui.pillarbox',
            name: 'PillarboxTest',
            version: 1,
            canvas: {
                referenceWidth: 1920,
                referenceHeight: 1080,
                scaleMode: 'match-height',
                matchBias: 1.0,
            },
            bindings: { centerButton: 'center-btn' },
            root: {
                role: 'root',
                enabled: true,
                interactive: false,
                layout: { display: 'overlay', width: '100%', height: '100%' },
                children: [
                    {
                        role: 'button',
                        key: 'center-btn',
                        enabled: true,
                        interactive: true,
                        layout: {
                            position: 'absolute',
                            width: 200,
                            height: 60,
                            anchor: { x: 0.5, y: 0.5, maxX: 0.5, maxY: 0.5, pivotX: 0.5, pivotY: 0.5 },
                        },
                        style: { background: '#0a74daff' },
                        children: [],
                    },
                ],
            } as never,
        });

        const host = new UIHost({ assetId: 'ui.pillarbox', receiveInput: true });
        const handle = bindUIHostToScene({
            scene: pillarboxScene,
            host,
            resolveAsset: () => pillarboxAsset(),
            input: { target },
        });
        expect(handle).not.toBeNull();

        const centerHandler = vi.fn(() => true);
        handle!.runtime.updateWidget(handle!.runtime.getBoundWidget('centerButton')!, {
            handlers: { pointerDown: centerHandler },
        });

        // match-height: scale = 800/1080 ≈ 0.7407
        // effectiveW = 1920 * 0.7407 ≈ 1422.2
        // offsetX = (1920 - 1422.2) / 2 ≈ 248.9
        // effectiveH = 1080 * 0.7407 = 800
        // offsetY = 0
        const scale = 800 / 1080;
        const effectiveW = 1920 * scale;
        const offsetX = (1920 - effectiveW) / 2;

        const centerBox = handle!.runtime.getLayoutBox(handle!.runtime.getBoundWidget('centerButton')!);
        const centerRefX = centerBox.x + centerBox.width / 2;
        const centerRefY = centerBox.y + centerBox.height / 2;
        const fbX = centerRefX * scale + offsetX;
        const fbY = centerRefY * scale;

        target.emit('pointerdown', { clientX: fbX, clientY: fbY });
        expect(centerHandler).toHaveBeenCalledTimes(1);

        handle!.dispose();
    });

    it('does not attach input listeners when receiveInput is false', () => {
        const scene = createSceneTarget();
        const target = createInputTarget();
        const host = new UIHost({ assetId: 'ui.hud', receiveInput: false });

        const handle = bindUIHostToScene({
            scene,
            host,
            resolveAsset: () => createHostAsset(),
            input: { target },
        });

        expect(handle).not.toBeNull();
        expect(target.addEventListener).not.toHaveBeenCalled();
        handle!.dispose();
    });

    it('prefers resolveAsset over resolveAssetJson when both are provided', () => {
        const scene = createSceneTarget();
        const host = new UIHost({ assetId: 'ui.hud' });
        const resolveAsset = vi.fn(() => createHostAsset());
        const resolveAssetJson = vi.fn(() => JSON.stringify(createHostAsset()));

        const handle = bindUIHostToScene({ scene, host, resolveAsset, resolveAssetJson });

        expect(handle).not.toBeNull();
        expect(resolveAsset).toHaveBeenCalledTimes(1);
        expect(resolveAssetJson).not.toHaveBeenCalled();
        handle!.dispose();
    });

    it('binds multiple hosts, skipping unresolvable ones, and disposes them together', () => {
        const scene = createSceneTarget();
        const bindable = new UIHost({ assetId: 'ui.hud' });
        const emptyAssetId = new UIHost({ assetId: '' });
        const unresolvable = new UIHost({ assetId: 'ui.missing' });

        const bindings = bindUIHostsToScene({
            scene,
            hosts: [bindable, emptyAssetId, unresolvable],
            resolveAsset: (assetId) => (assetId === 'ui.hud' ? createHostAsset() : null),
        });

        expect(bindings.handles).toHaveLength(1);
        expect(bindings.handles[0].asset.id).toBe('ui.hud');

        bindings.dispose();
        expect(() => bindings.dispose()).not.toThrow();
    });

    it('registers bound hosts so getUIHostRuntime resolves the live runtime', () => {
        const scene = createSceneTarget();
        const host = new UIHost({ assetId: 'ui.hud' });

        expect(getUIHostRuntime(host)).toBeNull();

        const handle = bindUIHostToScene({ scene, host, resolveAsset: () => createHostAsset() });
        expect(handle).not.toBeNull();
        expect(getUIHostBinding(host)).toBe(handle);
        expect(getUIHostRuntime(host)).toBe(handle!.runtime);

        handle!.dispose();
        expect(getUIHostBinding(host)).toBeNull();
        expect(getUIHostRuntime(host)).toBeNull();
    });

    it('resolves UIWidgetRefs that mutate the bound widget and go inert on dispose', () => {
        const scene = createSceneTarget();
        const target = createInputTarget();
        const host = new UIHost({ assetId: 'ui.hud', receiveInput: true });

        const handle = bindUIHostToScene({
            scene,
            host,
            resolveAsset: () => createHostAsset(),
            input: { target },
        });
        expect(handle).not.toBeNull();

        expect(resolveUIWidgetRef(host, 'missing-key')).toBeNull();

        const ref = resolveUIWidgetRef(host, 'button');
        expect(ref).not.toBeNull();
        expect(ref!.isValid()).toBe(true);
        expect(ref!.widgetId).toBe(handle!.runtime.getBoundWidget('button'));

        // Mutations through the ref reach the live runtime: attach a handler
        // and confirm the real input path invokes it.
        const pointerDown = vi.fn(() => true);
        expect(ref!.setHandlers({ pointerDown })).toBe(true);
        expect(ref!.setStyle({ background: '#ff0000ff' })).toBe(true);
        handle!.render(320, 180);
        target.emit('pointerdown', { clientX: 40, clientY: 40 });
        expect(pointerDown).toHaveBeenCalledTimes(1);

        handle!.dispose();
        expect(ref!.isValid()).toBe(false);
        expect(ref!.setText('gone')).toBe(false);
    });

    it('lazily resolves scene-runtime ui-widget refs once the host is bound', () => {
        const scene = createSceneTarget();
        const target = createInputTarget();
        const host = new UIHost({ assetId: 'ui.hud', receiveInput: true });

        // Created before any binding exists — exactly how hydrated script
        // properties behave when scripts instantiate before bindUIHostsToScene.
        const lazyRef = createLazySceneUIWidgetRef(() => host, 'button');
        expect(lazyRef.isValid()).toBe(false);
        expect(lazyRef.setText('early')).toBe(false);

        const bindings = bindUIHostsToScene({
            scene,
            hosts: [host],
            resolveAsset: () => createHostAsset(),
            input: { target },
        });
        expect(bindings.handles).toHaveLength(1);

        expect(lazyRef.isValid()).toBe(true);
        const pointerDown = vi.fn(() => true);
        expect(lazyRef.setHandlers({ pointerDown })).toBe(true);
        bindings.handles[0].render(320, 180);
        target.emit('pointerdown', { clientX: 40, clientY: 40 });
        expect(pointerDown).toHaveBeenCalledTimes(1);

        bindings.dispose();
        expect(lazyRef.isValid()).toBe(false);
    });
});

describe('world-space UIHost binding', () => {
    const worldSources = () => ({
        viewProjection: () => columnMajorIdentity(),
        entityWorldMatrix: () => columnMajorIdentity(0, 0, -3),
        cameraWorldMatrix: () => columnMajorIdentity(0, 0, 5),
    });

    const createWorldInputTarget = () => {
        const listeners = new Map<string, Set<(event: unknown) => void>>();
        return {
            addEventListener: vi.fn((type: string, listener: (event: never) => void) => {
                const bucket = listeners.get(type) ?? new Set();
                bucket.add(listener as (event: unknown) => void);
                listeners.set(type, bucket);
            }),
            removeEventListener: vi.fn((type: string, listener: (event: never) => void) => {
                listeners.get(type)?.delete(listener as (event: unknown) => void);
            }),
            getBoundingClientRect: () => ({ left: 0, top: 0, width: 320, height: 180 }),
            emit(type: string, event: unknown) {
                for (const listener of listeners.get(type) ?? []) {
                    listener(event);
                }
            },
            listenerCount(type: string) {
                return listeners.get(type)?.size ?? 0;
            },
        };
    };

    it('binds a world-space host, registers it, and drives the quad every frame', () => {
        const scene = createSceneTarget();
        const host = new UIHost({
            assetId: 'ui.hud',
            renderMode: 'world-space',
            worldWidth: 1.2,
            worldHeight: 0.4,
        });

        const handle = bindUIHostToWorld({
            scene,
            host,
            resolveAsset: () => createHostAsset(),
            world: worldSources(),
        });

        expect(handle).not.toBeNull();
        // The registry must work in world-space too, so @property('ui-widget')
        // references keep resolving.
        expect(getUIHostRuntime(host)).toBe(handle!.runtime);
        expect(handle!.runtime.getBoundWidget('button')).not.toBeNull();

        // A frame tick renders UI into the surface and then draws the quad.
        const drawCallsBefore = (scene.gl.drawArrays as unknown as { mock: { calls: unknown[] } })
            .mock.calls.length;
        handle!.render(0, 0);
        const drawCallsAfter = (scene.gl.drawArrays as unknown as { mock: { calls: unknown[] } })
            .mock.calls.length;
        expect(drawCallsAfter).toBeGreaterThan(drawCallsBefore);

        handle!.dispose();
        expect(getUIHostBinding(host)).toBeNull();
        // Idempotent dispose keeps the loop clean.
        expect(() => handle!.dispose()).not.toThrow();
    });

    it('routes hosts by render mode and skips world hosts without matrix sources', () => {
        const scene = createSceneTarget();
        const screenHost = new UIHost({ assetId: 'ui.hud', renderMode: 'screen-overlay' });
        const worldHost = new UIHost({ assetId: 'ui.hud', renderMode: 'world-space' });

        // No `world` option: the world-space host is skipped, the overlay binds.
        const withoutWorld = bindUIHostsToScene({
            scene,
            hosts: [screenHost, worldHost],
            resolveAsset: () => createHostAsset(),
        });
        expect(withoutWorld.handles).toHaveLength(1);
        expect(getUIHostBinding(worldHost)).toBeNull();
        withoutWorld.dispose();

        const withWorld = bindUIHostsToScene({
            scene,
            hosts: [screenHost, worldHost],
            resolveAsset: () => createHostAsset(),
            world: worldSources(),
        });
        expect(withWorld.handles).toHaveLength(2);
        expect(getUIHostBinding(worldHost)).not.toBeNull();
        withWorld.dispose();
        expect(getUIHostBinding(worldHost)).toBeNull();
    });

    it('returns null for a world host without an asset', () => {
        const scene = createSceneTarget();
        const host = new UIHost({ renderMode: 'world-space' });

        expect(
            bindUIHostToWorld({
                scene,
                host,
                resolveAsset: () => createHostAsset(),
                world: worldSources(),
            })
        ).toBeNull();
    });

    it('maps input using the offscreen surface size when surface != canvas', () => {
        // World-space hosts render to an offscreen surface whose size is
        // determined by resolveWorldSurfaceSize (host.worldWidth * textureScale,
        // host.worldHeight * textureScale), which can differ from the main
        // canvas framebuffer. Input must use the surface dimensions, not the
        // canvas dimensions.
        const scene = createSceneTarget();
        // Main canvas is 320x180, but the world surface will be different.
        const host = new UIHost({
            assetId: 'ui.hud',
            renderMode: 'world-space',
            worldWidth: 2.0,
            worldHeight: 1.0,
            textureScale: 200, // surface = 400x200, != canvas 320x180
            receiveInput: true,
        });

        const target = createWorldInputTarget();
        // CSS rect matches the canvas size (devicePixelRatio = 1)
        target.getBoundingClientRect = () => ({ left: 0, top: 0, width: 320, height: 180 });

        const handle = bindUIHostToWorld({
            scene,
            host,
            resolveAsset: () => createHostAsset(),
            world: worldSources(),
            input: { target },
        });
        expect(handle).not.toBeNull();

        // Trigger layout computation via a render cycle.
        handle!.render(0, 0);

        const pointerDown = vi.fn(() => true);
        handle!.runtime.updateWidget(handle!.runtime.getBoundWidget('button')!, {
            handlers: { pointerDown },
        });

        // The surface is 400x200, the reference is 320x180.
        // With 'fill' scale mode (bias 0.5):
        //   scaleW = 400/320 = 1.25, scaleH = 200/180 ≈ 1.111
        //   blended = 1.25*0.5 + 1.111*0.5 ≈ 1.1806
        //   effectiveW = 320*1.1806 ≈ 377.8, offsetX = (400-377.8)/2 ≈ 11.1
        //   effectiveH = 180*1.1806 ≈ 212.5, offsetY = (200-212.5)/2 ≈ -6.25
        // Button center in reference: (50, 50)
        // Viewport point: (50*1.1806+11.1, 50*1.1806-6.25) = (70.1, 52.8)
        // CSS point: (70.1/1.25, 52.8/1.111) = (56.1, 47.5)
        const scale = 1.25 * 0.5 + (200 / 180) * 0.5;
        const effectiveW = 320 * scale;
        const offsetX = (400 - effectiveW) / 2;
        const effectiveH = 180 * scale;
        const offsetY = (200 - effectiveH) / 2;
        const vpX = 50 * scale + offsetX;
        const vpY = 50 * scale + offsetY;
        const cssX = vpX / (400 / 320);
        const cssY = vpY / (200 / 180);

        target.emit('pointerdown', { clientX: cssX, clientY: cssY });
        expect(pointerDown).toHaveBeenCalledTimes(1);

        // A point clearly outside the button in reference space
        target.emit('pointerdown', { clientX: 250, clientY: 160 });
        expect(pointerDown).toHaveBeenCalledTimes(1);

        handle!.dispose();
    });
});

describe('orientQuadTowardCamera', () => {
    it('adopts the camera rotation while keeping the entity position', () => {
        const model = columnMajorIdentity(2, 3, 4);
        // Camera rotated 90 degrees about Y, positioned elsewhere.
        const cameraWorld = new Float32Array([
            0, 0, -1, 0,
            0, 1, 0, 0,
            1, 0, 0, 0,
            9, 9, 9, 1,
        ]);

        const result = orientQuadTowardCamera(model, cameraWorld);

        // Rotation basis comes from the camera...
        expect(result[0]).toBeCloseTo(0);
        expect(result[2]).toBeCloseTo(-1);
        expect(result[8]).toBeCloseTo(1);
        // ...translation stays on the entity.
        expect(result[12]).toBe(2);
        expect(result[13]).toBe(3);
        expect(result[14]).toBe(4);
        expect(result[15]).toBe(1);
    });

    it('preserves the entity scale rather than inheriting the camera scale', () => {
        // Entity scaled 3x on X, camera scaled 2x on X.
        const model = new Float32Array([3, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);
        const cameraWorld = new Float32Array([2, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);

        const result = orientQuadTowardCamera(model, cameraWorld);

        expect(Math.hypot(result[0], result[1], result[2])).toBeCloseTo(3);
    });

    it('keeps the billboarded quad facing a camera-aligned ray', () => {
        // A quad turned away from the camera still gets hit once billboarded.
        const turnedAway = new Float32Array([
            0, 0, -1, 0,
            0, 1, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1,
        ]);
        const cameraWorld = columnMajorIdentity(0, 0, 6);
        const ray = { origin: [0, 0, 6] as const, direction: [0, 0, -1] as const };

        expect(intersectRayWithUIQuad(ray, turnedAway, 2, 1)).toBeNull();

        const billboarded = orientQuadTowardCamera(turnedAway, cameraWorld);
        const hit = intersectRayWithUIQuad(ray, billboarded, 2, 1);

        expect(hit).not.toBeNull();
        expect(hit!.u).toBeCloseTo(0.5);
        expect(hit!.v).toBeCloseTo(0.5);
    });
});

describe('dispatchWorldPointerToUIRuntime', () => {
    it('forwards a quad hit as a canvas-space pointer event', () => {
        const scene = createSceneTarget();
        const host = new UIHost({ assetId: 'ui.hud' });
        const handle = bindUIHostToScene({
            scene,
            host,
            resolveAsset: () => createHostAsset(),
        });
        const runtime = handle!.runtime;
        handle!.render(320, 180);

        const pointerDown = vi.fn(() => true);
        const widget = runtime.getBoundWidget('button')!;
        runtime.updateWidget(widget, { handlers: { pointerDown } });

        // The asset's button occupies the top-left 100x100 of a 320x180 canvas,
        // so a hit at 10% / 10% lands inside it.
        const handled = dispatchWorldPointerToUIRuntime(
            runtime,
            { u: 0.1, v: 0.1, distance: 4 },
            { phase: 'down' },
            320,
            180
        );

        expect(handled).toBe(true);
        expect(pointerDown).toHaveBeenCalledTimes(1);

        handle!.dispose();
    });

    it('sends a leave when the pointer slides off the quad', () => {
        const scene = createSceneTarget();
        const host = new UIHost({ assetId: 'ui.hud' });
        const handle = bindUIHostToScene({
            scene,
            host,
            resolveAsset: () => createHostAsset(),
        });
        const runtime = handle!.runtime;
        handle!.render(320, 180);

        const pointerLeave = vi.fn(() => true);
        const widget = runtime.getBoundWidget('button')!;
        runtime.updateWidget(widget, { handlers: { pointerLeave } });

        // Hover the widget, then miss the quad entirely.
        dispatchWorldPointerToUIRuntime(runtime, { u: 0.1, v: 0.1, distance: 4 }, { phase: 'move' }, 320, 180);
        const missed = dispatchWorldPointerToUIRuntime(runtime, null, { phase: 'move' }, 320, 180);

        expect(missed).toBe(false);
        expect(pointerLeave).toHaveBeenCalled();

        handle!.dispose();
    });

    it('is a no-op without a runtime', () => {
        expect(
            dispatchWorldPointerToUIRuntime(null, { u: 0.5, v: 0.5, distance: 1 }, { phase: 'down' }, 320, 180)
        ).toBe(false);
    });
});

describe('stroke batch flush re-emit', () => {
    it('preserves all stroke segments when the quad batch overflows', () => {
        const gl = createMockWebGL2Context();
        const segmentCount = 5;
        const renderer = new WebGL2UIRenderer({ gl, quadBatchCapacity: 2 });
        const points: [number, number][] = [];
        for (let i = 0; i <= segmentCount; i++) {
            points.push([i / segmentCount, 0.5]);
        }
        const frame: UIFrame = {
            viewportWidth: 160,
            viewportHeight: 120,
            metrics: createMetrics(),
            commands: [
                {
                    kind: 'stroke',
                    widget: 1 as WidgetId,
                    x: 0,
                    y: 0,
                    width: 100,
                    height: 100,
                    zIndex: 0,
                    opacity: 1,
                    clip: null,
                    strokes: [
                        {
                            color: { r: 1, g: 0, b: 0, a: 1 },
                            weight: 2,
                            points,
                        },
                    ],
                },
            ],
        };
        renderer.render(frame);
        // drawArraysInstanced is called each time the batch flushes + final flush.
        // With 5 segments and capacity 2: flush at 2, flush at 2, flush remaining 1 = 3 draw calls.
        expect(gl.drawArraysInstanced).toHaveBeenCalledTimes(3);
        // All 5 segments must be rendered (statistics track total quads emitted).
        expect(renderer.getStats().quadCount).toBe(segmentCount);
        renderer.dispose();
    });
});

describe('world-quad active texture preservation', () => {
    it('restores the caller active texture unit after draw', () => {
        const gl = createMockWebGL2Context();
        const callerUnit = gl.TEXTURE1;
        // Simulate caller having TEXTURE1 active before draw.
        gl.activeTexture(callerUnit);
        const quadRenderer = createUIWorldQuadRenderer(gl);
        const texture = gl.createTexture()!;
        const vp = columnMajorIdentity();
        quadRenderer.draw(texture, {
            modelMatrix: vp,
            viewProjection: vp,
            width: 64,
            height: 64,
        });
        // The last activeTexture call must restore the caller's unit.
        const activeTextureCalls = (gl.activeTexture as ReturnType<typeof vi.fn>).mock.calls;
        const lastCall = activeTextureCalls[activeTextureCalls.length - 1][0];
        expect(lastCall).toBe(callerUnit);
        quadRenderer.dispose();
    });
});

describe('world-surface resize guard invalidation', () => {
    it('restores the current caller framebuffer binding after resize, not the construction-time one', () => {
        const gl = createMockWebGL2Context();
        const callerFbo = { id: 'caller-fbo' } as unknown as WebGLFramebuffer;
        // Construction captures null framebuffer.
        const surface = createUIWorldSurface(gl, 64, 64);
        // External code binds a different framebuffer.
        gl.bindFramebuffer(gl.FRAMEBUFFER, callerFbo);
        // Resize should capture and restore the CURRENT caller binding.
        surface.resize(128, 128);
        // After resize+restore, the framebuffer should be back to the caller's binding.
        const bindFramebufferCalls = (gl.bindFramebuffer as ReturnType<typeof vi.fn>).mock.calls;
        const lastBind = bindFramebufferCalls[bindFramebufferCalls.length - 1];
        expect(lastBind[1]).toBe(callerFbo);
        surface.dispose();
    });
});

describe('context loss handling', () => {
    const createMockCanvas = () => {
        const listeners = new Map<string, Set<(event: Event) => void>>();
        return {
            width: 320,
            height: 180,
            addEventListener: vi.fn((type: string, listener: (event: Event) => void) => {
                if (!listeners.has(type)) listeners.set(type, new Set());
                listeners.get(type)!.add(listener);
            }),
            removeEventListener: vi.fn((type: string, listener: (event: Event) => void) => {
                listeners.get(type)?.delete(listener);
            }),
            dispatchEvent(event: Event) {
                for (const listener of listeners.get(event.type) ?? []) {
                    listener(event);
                }
                return true;
            },
        };
    };

    const createGLWithCanvas = () => {
        const gl = createMockWebGL2Context();
        const canvas = createMockCanvas();
        (gl as unknown as { canvas: object }).canvas = canvas;
        return { gl, canvas };
    };

    it('skips world-quad draw while context is lost and recreates resources on restore', () => {
        const { gl, canvas } = createGLWithCanvas();
        const quadRenderer = createUIWorldQuadRenderer(gl);
        const vp = columnMajorIdentity();
        const texture = gl.createTexture()!;

        // Dispatch context lost.
        canvas.dispatchEvent(new Event('webglcontextlost'));

        // Draw should be skipped.
        const drawCallsBefore = (gl.drawArrays as ReturnType<typeof vi.fn>).mock.calls.length;
        quadRenderer.draw(texture, { modelMatrix: vp, viewProjection: vp, width: 32, height: 32 });
        expect((gl.drawArrays as ReturnType<typeof vi.fn>).mock.calls.length).toBe(drawCallsBefore);

        // Dispatch context restored.
        canvas.dispatchEvent(new Event('webglcontextrestored'));

        // Draw should work again.
        quadRenderer.draw(texture, { modelMatrix: vp, viewProjection: vp, width: 32, height: 32 });
        expect((gl.drawArrays as ReturnType<typeof vi.fn>).mock.calls.length).toBe(drawCallsBefore + 1);

        quadRenderer.dispose();
        // Listeners removed.
        expect(canvas.removeEventListener).toHaveBeenCalledWith('webglcontextlost', expect.any(Function));
        expect(canvas.removeEventListener).toHaveBeenCalledWith('webglcontextrestored', expect.any(Function));
    });

    it('skips world-surface resize while context is lost and recreates on restore', () => {
        const { gl, canvas } = createGLWithCanvas();
        const surface = createUIWorldSurface(gl, 64, 64);

        canvas.dispatchEvent(new Event('webglcontextlost'));

        const texImageCallsBefore = (gl.texImage2D as ReturnType<typeof vi.fn>).mock.calls.length;
        surface.resize(128, 128);
        // No new texImage2D calls because resize is skipped.
        expect((gl.texImage2D as ReturnType<typeof vi.fn>).mock.calls.length).toBe(texImageCallsBefore);

        canvas.dispatchEvent(new Event('webglcontextrestored'));
        // After restore, resources are recreated (new texture allocated).
        expect((gl.texImage2D as ReturnType<typeof vi.fn>).mock.calls.length).toBeGreaterThan(texImageCallsBefore);

        surface.dispose();
        expect(canvas.removeEventListener).toHaveBeenCalledWith('webglcontextlost', expect.any(Function));
    });
});

describe('atlas page eviction flushes text batch', () => {
    it('flushes batched glyphs before deleting the evicted active text page', () => {
        const gl = createMockWebGL2Context();
        const renderer = new WebGL2UIRenderer({ gl });
        const entry = createGlyphEntry();
        const frame: UIFrame<never> = {
            viewportWidth: 160,
            viewportHeight: 120,
            metrics: { ...createMetrics(), renderCount: 1, textCommandCount: 1, glyphCount: 1, customCommandCount: 0 },
            commands: [
                {
                    kind: 'text',
                    widget: 1 as WidgetId,
                    x: 0, y: 0, zIndex: 0,
                    color: { r: 1, g: 1, b: 1, a: 1 },
                    outlineColor: { r: 0, g: 0, b: 0, a: 0 },
                    outlineWidth: 0, edgeSoftness: 0, opacity: 1, clip: null,
                    layout: createTextLayout(entry),
                },
            ],
        };
        renderer.render(frame);
        const drawCallsAfterFirst = (gl.drawArraysInstanced as ReturnType<typeof vi.fn>).mock.calls.length;
        expect(drawCallsAfterFirst).toBeGreaterThan(0);
        const snapshot: GlyphAtlasPageSnapshot = {
            id: entry.page as number,
            width: entry.pageWidth,
            height: entry.pageHeight,
            entries: [entry],
        };
        renderer.handleAtlasPageEviction(snapshot);
        expect(renderer.getStats().atlasPageCount).toBe(0);
        renderer.dispose();
    });
});

describe('normalizeShaderSource version directive', () => {
    it('locates the #version directive precisely without matching es in comments', () => {
        const corrupted = '// testprecision esprecision mediump float;\n#version 300 esprecision mediump float;\nvoid main() {}';
        const result = normalizeShaderSource(corrupted);
        expect(result).toContain('#version 300 es');
        expect(result.indexOf('#version 300 es')).toBeGreaterThan(0);
    });

    it('returns intact source unchanged', () => {
        const intact = '#version 300 es\nprecision mediump float;\nvoid main() {}';
        expect(normalizeShaderSource(intact)).toBe(intact);
    });
});

describe('normalizeStrokeColor scratch buffer', () => {
    it('returns a Float32Array with correct RGBA values from a hex string', async () => {
        const { normalizeStrokeColor } = await import('../webgl-utils');
        const result = normalizeStrokeColor('#ff8040');
        expect(result).toBeInstanceOf(Float32Array);
        expect(result[0]).toBeCloseTo(1, 1);
        expect(result[1]).toBeCloseTo(0.502, 1);
        expect(result[2]).toBeCloseTo(0.251, 1);
        expect(result[3]).toBeCloseTo(1, 1);
    });

    it('reuses the same scratch buffer across calls', async () => {
        const { normalizeStrokeColor } = await import('../webgl-utils');
        const first = normalizeStrokeColor('#ff0000');
        const second = normalizeStrokeColor('#00ff00');
        expect(first).toBe(second);
        expect(second[0]).toBeCloseTo(0, 1);
        expect(second[1]).toBeCloseTo(1, 1);
        expect(second[2]).toBeCloseTo(0, 1);
    });
});
