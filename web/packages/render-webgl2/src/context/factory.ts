import { GLContext, isGLContext, unwrapGL } from './gl-context';
import { GLContextError } from './errors';
import type {
    ContextSource,
    CreateGLContextOptions,
    GLContextAttributes,
    GLContextLocale,
    IGLContext,
} from './types';

const DEFAULT_ATTRIBUTES: GLContextAttributes = Object.freeze({
    alpha: true,
    depth: true,
    stencil: false,
    antialias: true,
    premultipliedAlpha: true,
    preserveDrawingBuffer: false,
    powerPreference: 'default',
    desynchronized: false,
    failIfMajorPerformanceCaveat: false,
});

const normalizeLocale = (locale?: string): GLContextLocale =>
    locale?.toLowerCase().startsWith('tr') ? 'tr' : 'en';

const mergeAttributes = (
    base: GLContextAttributes,
    override?: GLContextAttributes
): GLContextAttributes => {
    if (!override) return base;
    return Object.freeze({ ...base, ...override });
};

const resolveCanvas = (canvas?: HTMLCanvasElement | null): HTMLCanvasElement => {
    if (canvas) return canvas;
    if (typeof document !== 'undefined') {
        return document.createElement('canvas');
    }
    throw new GLContextError('CONTEXT_CREATION_FAILED', 'en', { reason: 'No canvas available' });
};

const createRawGL = (
    canvas: HTMLCanvasElement,
    attributes: GLContextAttributes,
    locale: GLContextLocale
): WebGL2RenderingContext => {
    const gl = canvas.getContext('webgl2', attributes as WebGLContextAttributes) as WebGL2RenderingContext | null;
    if (!gl) {
        throw new GLContextError('CONTEXT_CREATION_FAILED', locale, {
            attributes,
            canvasWidth: canvas.width,
            canvasHeight: canvas.height,
        });
    }
    return gl;
};

export const createGLContext = (options: CreateGLContextOptions = {}): IGLContext => {
    const locale = normalizeLocale(options.locale);
    const attributes = mergeAttributes(DEFAULT_ATTRIBUTES, options.attributes);
    const enableStateCache = options.enableStateCache ?? true;
    const enableDebugLabels = options.enableDebugLabels ?? true;

    if (options.gl) {
        const rawCanvas = options.canvas ?? (options.gl.canvas as HTMLCanvasElement | null) ?? resolveCanvas(null);
        return new GLContext(options.gl, rawCanvas, locale, attributes, enableStateCache, enableDebugLabels);
    }

    const canvas = resolveCanvas(options.canvas ?? null);
    const gl = createRawGL(canvas, attributes, locale);

    if (options.onContextLost) canvas.addEventListener('webglcontextlost', options.onContextLost);
    if (options.onContextRestored) canvas.addEventListener('webglcontextrestored', options.onContextRestored);

    return new GLContext(gl, canvas, locale, attributes, enableStateCache, enableDebugLabels);
};

export const createGLContextFromCanvas = (
    canvas: HTMLCanvasElement,
    attributes?: GLContextAttributes,
    locale: GLContextLocale = 'en'
): IGLContext =>
    createGLContext({
        canvas,
        attributes: mergeAttributes(DEFAULT_ATTRIBUTES, attributes),
        locale: normalizeLocale(locale),
    });

export const createGLContextFromGL = (
    gl: WebGL2RenderingContext,
    canvas?: HTMLCanvasElement | null,
    locale: GLContextLocale = 'en'
): IGLContext => {
    const resolvedCanvas = (canvas ?? (gl.canvas as HTMLCanvasElement | null) ?? resolveCanvas(null)) as HTMLCanvasElement;
    return new GLContext(gl, resolvedCanvas, normalizeLocale(locale), DEFAULT_ATTRIBUTES, true, true);
};

export const wrapGLContext = (gl: WebGL2RenderingContext, canvas: HTMLCanvasElement): IGLContext =>
    new GLContext(gl, canvas, 'en', DEFAULT_ATTRIBUTES, true, true);

const weakCache = new WeakMap<WebGL2RenderingContext, IGLContext>();

export const getOrCreateGLContext = (gl: WebGL2RenderingContext, canvas?: HTMLCanvasElement | null): IGLContext => {
    const cached = weakCache.get(gl);
    if (cached && !cached.isDisposed) return cached;
    const resolvedCanvas = (canvas ?? (gl.canvas as HTMLCanvasElement | null) ?? resolveCanvas(null)) as HTMLCanvasElement;
    const ctx = new GLContext(gl, resolvedCanvas, 'en', DEFAULT_ATTRIBUTES, true, true);
    weakCache.set(gl, ctx);
    return ctx;
};

export const isContextSourceGLContext = isGLContext;

export const resolveContextGL = unwrapGL;

export const resolveContext = (source: ContextSource): IGLContext =>
    isGLContext(source) ? source : getOrCreateGLContext(source as WebGL2RenderingContext);

export const resolveContextNullable = (
    source: ContextSource | null | undefined
): IGLContext | null => (source == null ? null : resolveContext(source));

export const resolveRawGL = (
    source: ContextSource | null | undefined
): WebGL2RenderingContext | null =>
    source == null ? null : isGLContext(source) ? source.gl : (source as WebGL2RenderingContext);

export type GLContextFactory = typeof createGLContext;
