import { GLContextError } from './errors';
import type { GLCapabilitiesSnapshot, GLContextLocale, IGLCapabilities } from './types';

const clampInt = (value: unknown, fallback: number): number => {
    const n = typeof value === 'number' ? value : Number(value);
    return Number.isFinite(n) ? Math.max(0, Math.floor(n)) : fallback;
};

const readString = (gl: WebGL2RenderingContext, pname: number, fallback: string): string => {
    try {
        const v = gl.getParameter(pname);
        return typeof v === 'string' ? v : fallback;
    } catch { // best-effort
        return fallback;
    }
};

const readInt = (gl: WebGL2RenderingContext, pname: number, fallback: number): number => {
    try {
        return clampInt(gl.getParameter(pname), fallback);
    } catch { // best-effort
        return fallback;
    }
};

const readViewportDims = (gl: WebGL2RenderingContext): readonly [number, number] => {
    try {
        const v = gl.getParameter(gl.MAX_VIEWPORT_DIMS);
        if (v && typeof v === 'object' && '0' in v && '1' in v) {
            return Object.freeze([clampInt((v as Int32Array)[0], 0), clampInt((v as Int32Array)[1], 0)]) as readonly [
                number,
                number,
            ];
        }
        if (Array.isArray(v) && v.length >= 2) {
            return Object.freeze([clampInt(v[0], 0), clampInt(v[1], 0)]) as readonly [number, number];
        }
        return Object.freeze([0, 0]) as readonly [number, number];
    } catch { // best-effort
        return Object.freeze([0, 0]) as readonly [number, number];
    }
};

const readRange = (gl: WebGL2RenderingContext, pname: number): readonly [number, number] => {
    try {
        const v = gl.getParameter(pname);
        if (v && typeof v === 'object' && '0' in v && '1' in v) {
            const a = Number((v as Float32Array)[0]);
            const b = Number((v as Float32Array)[1]);
            return Object.freeze([
                Number.isFinite(a) ? a : 0,
                Number.isFinite(b) ? b : 0,
            ]) as readonly [number, number];
        }
        if (Array.isArray(v) && v.length >= 2) {
            return Object.freeze([Number(v[0]) || 0, Number(v[1]) || 0]) as readonly [number, number];
        }
        return Object.freeze([0, 0]) as readonly [number, number];
    } catch { // best-effort
        return Object.freeze([0, 0]) as readonly [number, number];
    }
};

const queryAnisotropy = (gl: WebGL2RenderingContext): { max: number; supported: boolean } => {
    const ext = gl.getExtension('EXT_texture_filter_anisotropic');
    if (!ext) return { max: 1, supported: false };
    try {
        const max = clampInt(gl.getParameter((ext as unknown as { MAX_TEXTURE_MAX_ANISOTROPY_EXT: number }).MAX_TEXTURE_MAX_ANISOTROPY_EXT), 1);
        return { max: Math.max(1, max), supported: true };
    } catch { // best-effort
        return { max: 1, supported: false };
    }
};

export class GLCapabilities implements IGLCapabilities {
    readonly #snapshot: GLCapabilitiesSnapshot;
    readonly #compressedSet: ReadonlySet<string>;

    constructor(gl: WebGL2RenderingContext, locale: GLContextLocale = 'en', attributes?: WebGLContextAttributes) {
        let snapshot: GLCapabilitiesSnapshot;
        try {
            const aniso = queryAnisotropy(gl);
            const hasFloatLinear = gl.getExtension('OES_texture_float_linear') !== null;
            const hasHalfFloatLinear = gl.getExtension('OES_texture_half_float_linear') !== null;
            const hasColorFloat = gl.getExtension('EXT_color_buffer_float') !== null;
            const hasColorHalfFloat = gl.getExtension('EXT_color_buffer_half_float') !== null;
            const compressed: string[] = [];
            const compressedExts = [
                'WEBGL_compressed_texture_s3tc',
                'WEBKIT_WEBGL_compressed_texture_s3tc',
                'WEBGL_compressed_texture_s3tc_srgb',
                'EXT_texture_compression_rgtc',
                'EXT_texture_compression_bptc',
                'WEBGL_compressed_texture_astc',
                'WEBGL_compressed_texture_etc',
                'WEBGL_compressed_texture_etc1',
            ] as const;
            for (const name of compressedExts) {
                if (gl.getExtension(name) !== null) compressed.push(name);
            }
            const ctxAttrs = gl.getContextAttributes();
            snapshot = Object.freeze({
                maxTextureSize: readInt(gl, gl.MAX_TEXTURE_SIZE, 0),
                maxCubeMapTextureSize: readInt(gl, gl.MAX_CUBE_MAP_TEXTURE_SIZE, 0),
                maxArrayTextureLayers: readInt(gl, gl.MAX_ARRAY_TEXTURE_LAYERS, 0),
                max3DTextureSize: readInt(gl, gl.MAX_3D_TEXTURE_SIZE, 0),
                maxVertexAttribs: readInt(gl, gl.MAX_VERTEX_ATTRIBS, 0),
                maxVaryingVectors: readInt(gl, gl.MAX_VARYING_VECTORS, 0),
                maxFragmentUniformVectors: readInt(gl, gl.MAX_FRAGMENT_UNIFORM_VECTORS, 0),
                maxVertexUniformVectors: readInt(gl, gl.MAX_VERTEX_UNIFORM_VECTORS, 0),
                maxCombinedTextureImageUnits: readInt(gl, gl.MAX_COMBINED_TEXTURE_IMAGE_UNITS, 0),
                maxTextureImageUnits: readInt(gl, gl.MAX_TEXTURE_IMAGE_UNITS, 0),
                maxRenderbufferSize: readInt(gl, gl.MAX_RENDERBUFFER_SIZE, 0),
                maxViewportDims: readViewportDims(gl),
                aliasedLineWidthRange: readRange(gl, gl.ALIASED_LINE_WIDTH_RANGE),
                aliasedPointSizeRange: readRange(gl, gl.ALIASED_POINT_SIZE_RANGE),
                maxAnisotropy: aniso.max,
                supportsAnisotropy: aniso.supported,
                supportsFloatLinear: hasFloatLinear,
                supportsHalfFloatLinear: hasHalfFloatLinear,
                supportsColorBufferFloat: hasColorFloat,
                supportsColorBufferHalfFloat: hasColorHalfFloat,
                compressedFormats: Object.freeze([...compressed]),
                version: readString(gl, gl.VERSION, ''),
                shadingLanguageVersion: readString(gl, gl.SHADING_LANGUAGE_VERSION, ''),
                vendor: readString(gl, gl.VENDOR, ''),
                renderer: readString(gl, gl.RENDERER, ''),
                isWebGL2: true,
                antialias: Boolean(ctxAttrs?.antialias ?? attributes?.antialias ?? false),
                depth: Boolean(ctxAttrs?.depth ?? attributes?.depth ?? true),
                stencil: Boolean(ctxAttrs?.stencil ?? attributes?.stencil ?? false),
                alpha: Boolean(ctxAttrs?.alpha ?? attributes?.alpha ?? true),
                premultipliedAlpha: Boolean(ctxAttrs?.premultipliedAlpha ?? attributes?.premultipliedAlpha ?? true),
                preserveDrawingBuffer: Boolean(ctxAttrs?.preserveDrawingBuffer ?? attributes?.preserveDrawingBuffer ?? false),
                powerPreference: ((attributes as unknown as { powerPreference?: string })?.powerPreference as GLCapabilitiesSnapshot['powerPreference']) ?? 'default',
            });
        } catch (error) {
            throw new GLContextError('CAPABILITY_QUERY_FAILED', locale, undefined, error);
        }
        this.#snapshot = snapshot;
        this.#compressedSet = new Set(snapshot.compressedFormats);
    }

    public get snapshot(): GLCapabilitiesSnapshot {
        return this.#snapshot;
    }

    public get maxTextureSize(): number {
        return this.#snapshot.maxTextureSize;
    }

    public get maxAnisotropy(): number {
        return this.#snapshot.maxAnisotropy;
    }

    public get supportsAnisotropy(): boolean {
        return this.#snapshot.supportsAnisotropy;
    }

    public isCompressedFormatSupported(format: string): boolean {
        return this.#compressedSet.has(format);
    }

    public getMaxAnisotropy(): number {
        return this.#snapshot.maxAnisotropy;
    }

    public toJSON(): GLCapabilitiesSnapshot {
        return this.#snapshot;
    }
}

export const createCapabilities = (
    gl: WebGL2RenderingContext,
    locale: GLContextLocale = 'en',
    attributes?: WebGLContextAttributes
): IGLCapabilities => new GLCapabilities(gl, locale, attributes);
