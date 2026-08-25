import { GLContextError } from './errors';
import type { ExtensionAvailability, ExtensionMap, ExtensionName, GLContextLocale, IExtensionRegistry } from './types';

const EXTENSION_NAMES: readonly ExtensionName[] = Object.freeze([
    'KHR_debug',
    'EXT_texture_filter_anisotropic',
    'EXT_color_buffer_float',
    'EXT_color_buffer_half_float',
    'OES_texture_float_linear',
    'OES_texture_half_float_linear',
    'WEBGL_compressed_texture_s3tc',
    'WEBKIT_WEBGL_compressed_texture_s3tc',
    'WEBGL_compressed_texture_s3tc_srgb',
    'EXT_texture_compression_rgtc',
    'EXT_texture_compression_bptc',
    'WEBGL_compressed_texture_astc',
    'WEBGL_compressed_texture_etc',
    'WEBGL_compressed_texture_etc1',
    'WEBGL_debug_renderer_info',
    'WEBGL_lose_context',
    'EXT_disjoint_timer_query_webgl2',
    'EXT_texture_norm16',
]);

export class ExtensionRegistry implements IExtensionRegistry {
    readonly #gl: WebGL2RenderingContext;
    readonly #locale: GLContextLocale;
    readonly #cache = new Map<string, unknown | null>();
    readonly #availability: ExtensionAvailability;
    #sealed = false;

    constructor(gl: WebGL2RenderingContext, locale: GLContextLocale = 'en') {
        this.#gl = gl;
        this.#locale = locale;
        const avail = {} as Record<ExtensionName, boolean>;
        for (const name of EXTENSION_NAMES) {
            const ext = this.#load(name);
            avail[name] = ext !== null;
        }
        this.#availability = Object.freeze({ ...avail }) as ExtensionAvailability;
        this.#sealed = true;
    }

    public get availability(): ExtensionAvailability {
        return this.#availability;
    }

    public has<K extends ExtensionName>(name: K): boolean {
        return Boolean(this.#availability[name as ExtensionName]);
    }

    public get<K extends ExtensionName>(name: K): ExtensionMap[K] {
        return this.#load(name) as ExtensionMap[K];
    }

    public getOptional<K extends ExtensionName>(name: K): ExtensionMap[K] | null {
        return this.#load(name) as ExtensionMap[K] | null;
    }

    public require<K extends ExtensionName>(name: K): NonNullable<ExtensionMap[K]> {
        const ext = this.#load(name);
        if (ext === null || ext === undefined) {
            throw new GLContextError('EXTENSION_NOT_SUPPORTED', this.#locale, { extension: name });
        }
        return ext as NonNullable<ExtensionMap[K]>;
    }

    public tryGet(name: string): unknown | null {
        return this.#load(name);
    }

    public keys(): readonly ExtensionName[] {
        return EXTENSION_NAMES;
    }

    #load = (name: string): unknown | null => {
        if (this.#cache.has(name)) return this.#cache.get(name) ?? null;
        let value: unknown | null = null;
        try {
            value = this.#gl.getExtension(name);
        } catch { // best-effort
            value = null;
        }
        if (!this.#sealed || !this.#cache.has(name)) {
            this.#cache.set(name, value);
        }
        return value;
    };
}

export const createExtensionRegistry = (
    gl: WebGL2RenderingContext,
    locale: GLContextLocale = 'en'
): IExtensionRegistry => new ExtensionRegistry(gl, locale);
