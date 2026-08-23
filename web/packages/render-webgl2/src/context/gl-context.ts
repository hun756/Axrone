import { getGLConstants } from './constants';
import { createCapabilities } from './capabilities';
import { createExtensionRegistry } from './extensions';
import { createStateCache } from './state-cache';
import { createLifecycle } from './lifecycle';
import { ResourceRegistry } from './resource-registry';
import { GLContextError } from './errors';
import type {
    GLConstants,
    GLContextAttributes,
    GLContextId,
    GLContextLocale,
    IGLCapabilities,
    IExtensionRegistry,
    IGLContext,
    IGLStateCache,
} from './types';
import type { ContextLifecycle } from './lifecycle';

const CONTEXT_ID_BRAND = Symbol('GLContextId');
let idCounter = 0;

const generateId = (): GLContextId => {
    idCounter += 1;
    const base = typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
    return `${base}-${idCounter}` as GLContextId & { readonly [CONTEXT_ID_BRAND]: unknown } as GLContextId;
};

const isWebGL2 = (gl: unknown): gl is WebGL2RenderingContext =>
    typeof WebGL2RenderingContext !== 'undefined' &&
    typeof WebGL2RenderingContext === 'function' &&
    gl instanceof WebGL2RenderingContext;

export class GLContext implements IGLContext {
    readonly #id: GLContextId;
    readonly #gl: WebGL2RenderingContext;
    readonly #canvas: HTMLCanvasElement;
    readonly #constants: GLConstants;
    readonly #capabilities: IGLCapabilities;
    readonly #extensions: IExtensionRegistry;
    readonly #state: IGLStateCache;
    readonly #lifecycle: ContextLifecycle;
    readonly #registry: ResourceRegistry;
    readonly #locale: GLContextLocale;
    readonly #attributes: GLContextAttributes;
    readonly #enableDebugLabels: boolean;
    #isDisposed = false;

    constructor(
        gl: WebGL2RenderingContext,
        canvas: HTMLCanvasElement,
        locale: GLContextLocale = 'en',
        attributes: GLContextAttributes = {},
        enableStateCache = true,
        enableDebugLabels = true
    ) {
        const src = gl as unknown as Record<string, unknown>;
        const hasFn = (name: string): boolean => typeof src[name] === 'function';
        const hasGLSurface =
            hasFn('getParameter') ||
            hasFn('createTexture') ||
            hasFn('createBuffer') ||
            hasFn('createProgram') ||
            hasFn('createShader') ||
            hasFn('bindBuffer');

        if (!isWebGL2(gl) && !hasGLSurface) {
            throw new GLContextError('INVALID_VALUE', locale, { reason: 'Not a WebGL2RenderingContext' });
        }

        const preHasGetParameter = hasFn('getParameter');

        let effectiveGL: WebGL2RenderingContext = gl;
        const needsWrap =
            !preHasGetParameter ||
            typeof (gl as unknown as { getExtension?: unknown }).getExtension !== 'function' ||
            typeof (gl as unknown as { getContextAttributes?: unknown }).getContextAttributes !== 'function' ||
            typeof (gl as unknown as { isContextLost?: unknown }).isContextLost !== 'function' ||
            typeof (gl as unknown as { getError?: unknown }).getError !== 'function' ||
            !(gl as unknown as { canvas?: unknown }).canvas;

        if (needsWrap) {
            const base = gl as unknown as object;
            effectiveGL = Object.create(base) as WebGL2RenderingContext;
            const w = effectiveGL as unknown as Record<string, unknown>;
            if (!preHasGetParameter) w['getParameter'] = () => 0;
            if (typeof src['getExtension'] !== 'function') w['getExtension'] = () => null;
            if (typeof src['getContextAttributes'] !== 'function')
                w['getContextAttributes'] = () => ({ ...(attributes as object) }) as unknown;
            if (typeof src['isContextLost'] !== 'function') w['isContextLost'] = () => false;
            if (typeof src['getError'] !== 'function') w['getError'] = () => 0;
            if (!src['canvas']) w['canvas'] = canvas;
            if (typeof (src['drawingBufferWidth'] as unknown) === 'undefined')
                w['drawingBufferWidth'] = canvas.width;
            if (typeof (src['drawingBufferHeight'] as unknown) === 'undefined')
                w['drawingBufferHeight'] = canvas.height;
        }

        this.#id = generateId();
        this.#gl = effectiveGL;
        this.#canvas = canvas;
        this.#locale = locale;
        this.#attributes = Object.freeze({ ...attributes });
        this.#enableDebugLabels = enableDebugLabels;
        this.#constants = getGLConstants(effectiveGL);
        this.#capabilities = createCapabilities(effectiveGL, locale, attributes);
        this.#extensions = createExtensionRegistry(effectiveGL, locale);
        this.#state = createStateCache(effectiveGL, enableStateCache);
        this.#lifecycle = createLifecycle(effectiveGL, canvas);
        this.#registry = new ResourceRegistry(this as unknown as IGLContext);
        this.#lifecycle.subscribe((event) => {
            if (event.kind === 'lost') this.#state.invalidate();
            if (event.kind === 'restored') this.#state.reset();
        });
    }

    public get id(): GLContextId {
        return this.#id;
    }

    public get gl(): WebGL2RenderingContext {
        return this.#gl;
    }

    public get canvas(): HTMLCanvasElement {
        return this.#canvas;
    }

    public get constants(): GLConstants {
        return this.#constants;
    }

    public get capabilities(): IGLCapabilities {
        return this.#capabilities;
    }

    public get extensions(): IExtensionRegistry {
        return this.#extensions;
    }

    public get state(): IGLStateCache {
        return this.#state;
    }

    public get registry(): ResourceRegistry {
        return this.#registry;
    }

    public get isDisposed(): boolean {
        return this.#isDisposed;
    }

    public get isLost(): boolean {
        if (this.#isDisposed) return true;
        return this.#lifecycle.isLost || this.#lifecycle.syncLostState();
    }

    public get locale(): GLContextLocale {
        return this.#locale;
    }

    public get attributes(): GLContextAttributes {
        return this.#attributes;
    }

    public onLost(listener: (event: Event) => void): () => void {
        return this.#lifecycle.onLost(listener);
    }

    public onRestored(listener: (event: Event) => void): () => void {
        return this.#lifecycle.onRestored(listener);
    }

    public onDisposed(listener: () => void): () => void {
        return this.#lifecycle.onDisposed(listener);
    }

    public labelObject(type: number, object: unknown | null, label: string): void {
        if (!this.#enableDebugLabels || !label || !object) return;
        if (this.#isDisposed || this.isLost) return;
        const ext = this.#extensions.tryGet('KHR_debug') as unknown as {
            labelObject?: (t: number, o: unknown | null, l: string) => void;
        } | null;
        if (ext && typeof ext.labelObject === 'function') {
            try {
                ext.labelObject(type, object, label);
            } catch { // best-effort
            }
        }
    }

    public getError(): number {
        if (this.#isDisposed) throw new GLContextError('CONTEXT_ALREADY_DISPOSED', this.#locale);
        return this.#gl.getError();
    }

    public flush(): void {
        if (this.#isDisposed) throw new GLContextError('CONTEXT_ALREADY_DISPOSED', this.#locale);
        if (this.isLost) throw new GLContextError('CONTEXT_LOST', this.#locale);
        this.#gl.flush();
    }

    public finish(): void {
        if (this.#isDisposed) throw new GLContextError('CONTEXT_ALREADY_DISPOSED', this.#locale);
        if (this.isLost) throw new GLContextError('CONTEXT_LOST', this.#locale);
        this.#gl.finish();
    }

    public loseContext(): void {
        if (this.#isDisposed) return;
        const ext = this.#extensions.tryGet('WEBGL_lose_context') as unknown as {
            loseContext?: () => void;
        } | null;
        if (ext && typeof ext.loseContext === 'function') {
            try {
                ext.loseContext();
            } catch { // best-effort
            }
        }
    }

    public restoreContext(): void {
        if (this.#isDisposed) return;
        const ext = this.#extensions.tryGet('WEBGL_lose_context') as unknown as {
            restoreContext?: () => void;
        } | null;
        if (ext && typeof ext.restoreContext === 'function') {
            try {
                ext.restoreContext();
            } catch { // best-effort
            }
        }
    }

    public resetState(): void {
        this.#state.reset();
    }

    public invalidateState(): void {
        this.#state.invalidate();
    }

    public dispose(): void {
        if (this.#isDisposed) return;
        this.#isDisposed = true;
        this.#registry.dispose();
        this.#lifecycle.dispose();
        this.#state.invalidate();
    }

    public [Symbol.dispose](): void {
        this.dispose();
    }
}

export const isGLContext = (value: unknown): value is IGLContext =>
    value instanceof GLContext;

export const unwrapGL = (source: IGLContext | WebGL2RenderingContext): WebGL2RenderingContext =>
    isGLContext(source) ? source.gl : source;

export const resolveGL = unwrapGL;
