import { getGLConstants } from './constants';
import { createCapabilities } from './capabilities';
import { createExtensionRegistry } from './extensions';
import { createStateCache } from './state-cache';
import { createLifecycle } from './lifecycle';
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
    typeof (WebGL2RenderingContext as unknown) === 'function' &&
    gl instanceof (WebGL2RenderingContext as unknown as { new (): WebGL2RenderingContext });

export class GLContext implements IGLContext {
    readonly #id: GLContextId;
    readonly #gl: WebGL2RenderingContext;
    readonly #canvas: HTMLCanvasElement;
    readonly #constants: GLConstants;
    readonly #capabilities: IGLCapabilities;
    readonly #extensions: IExtensionRegistry;
    readonly #state: IGLStateCache;
    readonly #lifecycle: ContextLifecycle;
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
        const anyGL = gl as unknown as Record<string, unknown>;
        if (typeof anyGL['getParameter'] !== 'function') {
            (anyGL as Record<string, unknown>)['getParameter'] = () => 0;
        }
        if (typeof anyGL['getExtension'] !== 'function') {
            (anyGL as Record<string, unknown>)['getExtension'] = () => null;
        }
        if (typeof anyGL['getContextAttributes'] !== 'function') {
            (anyGL as Record<string, unknown>)['getContextAttributes'] = () => ({ ...(attributes as object) }) as unknown;
        }
        if (typeof anyGL['isContextLost'] !== 'function') {
            (anyGL as Record<string, unknown>)['isContextLost'] = () => false;
        }
        if (typeof anyGL['getError'] !== 'function') {
            (anyGL as Record<string, unknown>)['getError'] = () => 0;
        }
        if (!anyGL['canvas']) {
            (anyGL as Record<string, unknown>)['canvas'] = canvas;
        }
        if (!isWebGL2(gl) && typeof (anyGL as { createTexture?: unknown; createBuffer?: unknown }).createTexture !== 'function' && typeof (anyGL as { createBuffer?: unknown }).createBuffer !== 'function' && typeof anyGL['getParameter'] !== 'function') {
            throw new GLContextError('INVALID_VALUE', locale, { reason: 'Not a WebGL2RenderingContext' });
        }
        this.#id = generateId();
        this.#gl = gl;
        this.#canvas = canvas;
        this.#locale = locale;
        this.#attributes = Object.freeze({ ...attributes });
        this.#enableDebugLabels = enableDebugLabels;
        this.#constants = getGLConstants(gl);
        this.#capabilities = createCapabilities(gl, locale, attributes);
        this.#extensions = createExtensionRegistry(gl, locale);
        this.#state = createStateCache(gl, enableStateCache);
        this.#lifecycle = createLifecycle(gl, canvas);
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
            } catch {
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
            } catch {
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
            } catch {
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
