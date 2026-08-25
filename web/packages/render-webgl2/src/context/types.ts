import type { Brand, Opaque } from '@axrone/utility';

export type GLContextId = Opaque<string, 'GLContextId'>;

export type GLContextLocale = 'en' | 'tr';

export type GLContextPowerPreference = 'default' | 'low-power' | 'high-performance';

export type ContextEventKind = 'lost' | 'restored' | 'disposed';

export type ContextEvent =
    | { readonly kind: 'lost'; readonly event: Event }
    | { readonly kind: 'restored'; readonly event: Event }
    | { readonly kind: 'disposed' };

export type ContextListener = (event: ContextEvent) => void;

export type Unsubscribe = () => void;

export type ExtensionName =
    | 'KHR_debug'
    | 'EXT_texture_filter_anisotropic'
    | 'EXT_color_buffer_float'
    | 'EXT_color_buffer_half_float'
    | 'OES_texture_float_linear'
    | 'OES_texture_half_float_linear'
    | 'WEBGL_compressed_texture_s3tc'
    | 'WEBKIT_WEBGL_compressed_texture_s3tc'
    | 'WEBGL_compressed_texture_s3tc_srgb'
    | 'EXT_texture_compression_rgtc'
    | 'EXT_texture_compression_bptc'
    | 'WEBGL_compressed_texture_astc'
    | 'WEBGL_compressed_texture_etc'
    | 'WEBGL_compressed_texture_etc1'
    | 'WEBGL_debug_renderer_info'
    | 'WEBGL_lose_context'
    | 'EXT_disjoint_timer_query_webgl2'
    | 'EXT_texture_norm16';

export type ExtensionMap = {
    readonly KHR_debug: KHR_debug | null;
    readonly EXT_texture_filter_anisotropic: EXT_texture_filter_anisotropic | null;
    readonly EXT_color_buffer_float: unknown | null;
    readonly EXT_color_buffer_half_float: unknown | null;
    readonly OES_texture_float_linear: unknown | null;
    readonly OES_texture_half_float_linear: unknown | null;
    readonly WEBGL_compressed_texture_s3tc: unknown | null;
    readonly WEBKIT_WEBGL_compressed_texture_s3tc: unknown | null;
    readonly WEBGL_compressed_texture_s3tc_srgb: unknown | null;
    readonly EXT_texture_compression_rgtc: unknown | null;
    readonly EXT_texture_compression_bptc: unknown | null;
    readonly WEBGL_compressed_texture_astc: unknown | null;
    readonly WEBGL_compressed_texture_etc: unknown | null;
    readonly WEBGL_compressed_texture_etc1: unknown | null;
    readonly WEBGL_debug_renderer_info: unknown | null;
    readonly WEBGL_lose_context: WEBGL_lose_context | null;
    readonly EXT_disjoint_timer_query_webgl2: unknown | null;
    readonly EXT_texture_norm16: unknown | null;
};

export type ExtensionAvailability = {
    readonly [K in ExtensionName]: boolean;
};

export interface KHR_debug {
    readonly labelObject: (type: number, object: unknown | null, label: string) => void;
    readonly BUFFER: number;
    readonly SHADER: number;
    readonly PROGRAM: number;
    readonly VERTEX_ARRAY: number;
    readonly QUERY: number;
    readonly SAMPLER: number;
    readonly TEXTURE: number;
    readonly RENDERBUFFER: number;
    readonly FRAMEBUFFER: number;
}

export interface EXT_texture_filter_anisotropic {
    readonly TEXTURE_MAX_ANISOTROPY_EXT: number;
    readonly MAX_TEXTURE_MAX_ANISOTROPY_EXT: number;
}

export interface WEBGL_lose_context {
    loseContext(): void;
    restoreContext(): void;
}

export type GLConstants = Readonly<{
    ARRAY_BUFFER: number;
    ELEMENT_ARRAY_BUFFER: number;
    COPY_READ_BUFFER: number;
    COPY_WRITE_BUFFER: number;
    TRANSFORM_FEEDBACK_BUFFER: number;
    UNIFORM_BUFFER: number;
    PIXEL_PACK_BUFFER: number;
    PIXEL_UNPACK_BUFFER: number;
    STATIC_DRAW: number;
    DYNAMIC_DRAW: number;
    STREAM_DRAW: number;
    STATIC_READ: number;
    DYNAMIC_READ: number;
    STREAM_READ: number;
    STATIC_COPY: number;
    DYNAMIC_COPY: number;
    STREAM_COPY: number;
    TEXTURE_2D: number;
    TEXTURE_CUBE_MAP: number;
    TEXTURE_2D_ARRAY: number;
    TEXTURE_3D: number;
    TEXTURE0: number;
    RGB: number;
    RGBA: number;
    RED: number;
    RG: number;
    RGBA8: number;
    RGB8: number;
    RG8: number;
    R8: number;
    RGBA16F: number;
    RGB16F: number;
    RG16F: number;
    R16F: number;
    RGBA32F: number;
    RGB32F: number;
    RG32F: number;
    R32F: number;
    DEPTH_COMPONENT16: number;
    DEPTH_COMPONENT24: number;
    DEPTH_COMPONENT32F: number;
    DEPTH24_STENCIL8: number;
    DEPTH32F_STENCIL8: number;
    COLOR_ATTACHMENT0: number;
    DEPTH_ATTACHMENT: number;
    STENCIL_ATTACHMENT: number;
    DEPTH_STENCIL_ATTACHMENT: number;
    FRAMEBUFFER: number;
    READ_FRAMEBUFFER: number;
    DRAW_FRAMEBUFFER: number;
    RENDERBUFFER: number;
    COLOR_BUFFER_BIT: number;
    DEPTH_BUFFER_BIT: number;
    STENCIL_BUFFER_BIT: number;
    NEAREST: number;
    LINEAR: number;
    CLAMP_TO_EDGE: number;
    REPEAT: number;
    MIRRORED_REPEAT: number;
    UNSIGNED_BYTE: number;
    UNSIGNED_SHORT: number;
    UNSIGNED_INT: number;
    FLOAT: number;
    HALF_FLOAT: number;
    BYTE: number;
    SHORT: number;
    INT: number;
    FRAMEBUFFER_COMPLETE: number;
    TRIANGLES: number;
    LINES: number;
    POINTS: number;
    VERTEX_SHADER: number;
    FRAGMENT_SHADER: number;
    COMPILE_STATUS: number;
    LINK_STATUS: number;
    VERSION: number;
    SHADING_LANGUAGE_VERSION: number;
    VENDOR: number;
    RENDERER: number;
    MAX_TEXTURE_SIZE: number;
    MAX_CUBE_MAP_TEXTURE_SIZE: number;
    MAX_ARRAY_TEXTURE_LAYERS: number;
    MAX_3D_TEXTURE_SIZE: number;
    MAX_VERTEX_ATTRIBS: number;
    MAX_VARYING_VECTORS: number;
    MAX_FRAGMENT_UNIFORM_VECTORS: number;
    MAX_VERTEX_UNIFORM_VECTORS: number;
    MAX_COMBINED_TEXTURE_IMAGE_UNITS: number;
    MAX_TEXTURE_IMAGE_UNITS: number;
    MAX_RENDERBUFFER_SIZE: number;
    MAX_VIEWPORT_DIMS: number;
    ALIASED_LINE_WIDTH_RANGE: number;
    ALIASED_POINT_SIZE_RANGE: number;
    BOOL: number;
    SAMPLER_2D: number;
    SAMPLER_CUBE: number;
    SAMPLER_2D_ARRAY: number;
    SAMPLER_3D: number;
}>;

export type GLCapabilitiesSnapshot = Readonly<{
    maxTextureSize: number;
    maxCubeMapTextureSize: number;
    maxArrayTextureLayers: number;
    max3DTextureSize: number;
    maxVertexAttribs: number;
    maxVaryingVectors: number;
    maxFragmentUniformVectors: number;
    maxVertexUniformVectors: number;
    maxCombinedTextureImageUnits: number;
    maxTextureImageUnits: number;
    maxRenderbufferSize: number;
    maxViewportDims: readonly [number, number];
    aliasedLineWidthRange: readonly [number, number];
    aliasedPointSizeRange: readonly [number, number];
    maxAnisotropy: number;
    supportsAnisotropy: boolean;
    supportsFloatLinear: boolean;
    supportsHalfFloatLinear: boolean;
    supportsColorBufferFloat: boolean;
    supportsColorBufferHalfFloat: boolean;
    compressedFormats: readonly string[];
    version: string;
    shadingLanguageVersion: string;
    vendor: string;
    renderer: string;
    isWebGL2: boolean;
    antialias: boolean;
    depth: boolean;
    stencil: boolean;
    alpha: boolean;
    premultipliedAlpha: boolean;
    preserveDrawingBuffer: boolean;
    powerPreference: GLContextPowerPreference;
}>;

export type GLStateSnapshot = Readonly<{
    boundArrayBuffer: WebGLBuffer | null;
    boundElementArrayBuffer: WebGLBuffer | null;
    boundFramebuffer: WebGLFramebuffer | null;
    boundReadFramebuffer: WebGLFramebuffer | null;
    boundDrawFramebuffer: WebGLFramebuffer | null;
    boundRenderbuffer: WebGLRenderbuffer | null;
    boundVertexArray: WebGLVertexArrayObject | null;
    currentProgram: WebGLProgram | null;
    activeTextureUnit: number;
    viewport: readonly [number, number, number, number];
    scissor: readonly [number, number, number, number];
    blendEnabled: boolean;
    cullFaceEnabled: boolean;
    depthTestEnabled: boolean;
    scissorTestEnabled: boolean;
    stencilTestEnabled: boolean;
}>;

export type GLContextAttributes = WebGLContextAttributes & {
    readonly powerPreference?: GLContextPowerPreference;
    readonly failIfMajorPerformanceCaveat?: boolean;
};

export type GLContextOptions = Readonly<{
    readonly canvas?: HTMLCanvasElement;
    readonly attributes?: GLContextAttributes;
    readonly locale?: GLContextLocale;
    readonly enableStateCache?: boolean;
    readonly enableDebugLabels?: boolean;
    readonly onContextLost?: (event: Event) => void;
    readonly onContextRestored?: (event: Event) => void;
}>;

export type GLContextFactoryOptions = GLContextOptions & Readonly<{
    readonly gl?: WebGL2RenderingContext | null;
}>;

export type CreateGLContextOptions = GLContextFactoryOptions;

export type ContextSource = IGLContext | WebGL2RenderingContext;

export interface IGLCapabilities {
    readonly snapshot: GLCapabilitiesSnapshot;
    readonly maxTextureSize: number;
    readonly maxAnisotropy: number;
    readonly supportsAnisotropy: boolean;
    isCompressedFormatSupported(format: string): boolean;
    getMaxAnisotropy(): number;
    toJSON(): GLCapabilitiesSnapshot;
}

export interface IExtensionRegistry {
    readonly availability: ExtensionAvailability;
    has<K extends ExtensionName>(name: K): boolean;
    get<K extends ExtensionName>(name: K): ExtensionMap[K];
    getOptional<K extends ExtensionName>(name: K): ExtensionMap[K] | null;
    require<K extends ExtensionName>(name: K): NonNullable<ExtensionMap[K]>;
    tryGet(name: string): unknown | null;
    keys(): readonly ExtensionName[];
}

export interface IGLStateCache {
    readonly snapshot: GLStateSnapshot;
    bindBuffer(target: number, buffer: WebGLBuffer | null): void;
    bindFramebuffer(target: number, framebuffer: WebGLFramebuffer | null): void;
    bindRenderbuffer(target: number, renderbuffer: WebGLRenderbuffer | null): void;
    bindVertexArray(vao: WebGLVertexArrayObject | null): void;
    useProgram(program: WebGLProgram | null): void;
    activeTexture(unit: number): void;
    bindTexture(target: number, texture: WebGLTexture | null): void;
    bindSampler(unit: number, sampler: WebGLSampler | null): void;
    enable(cap: number): void;
    disable(cap: number): void;
    viewport(x: number, y: number, width: number, height: number): void;
    scissor(x: number, y: number, width: number, height: number): void;
    blendFunc(srcRGB: number, dstRGB: number): void;
    blendFuncSeparate(srcRGB: number, dstRGB: number, srcAlpha: number, dstAlpha: number): void;
    blendEquation(mode: number): void;
    blendEquationSeparate(modeRGB: number, modeAlpha: number): void;
    depthFunc(func: number): void;
    depthMask(flag: boolean): void;
    colorMask(r: boolean, g: boolean, b: boolean, a: boolean): void;
    cullFace(mode: number): void;
    frontFace(mode: number): void;
    polygonOffset(factor: number, units: number): void;
    stencilFunc(func: number, ref: number, mask: number): void;
    stencilFuncSeparate(face: number, func: number, ref: number, mask: number): void;
    stencilOp(sfail: number, dpfail: number, dppass: number): void;
    stencilOpSeparate(face: number, sfail: number, dpfail: number, dppass: number): void;
    stencilMask(mask: number): void;
    stencilMaskSeparate(face: number, mask: number): void;
    bindBufferBase(target: number, index: number, buffer: WebGLBuffer | null): void;
    readonly isDeduplicationActive: boolean;
    reset(): void;
    invalidate(): void;
}

export interface IGLContext {
    readonly id: GLContextId;
    readonly gl: WebGL2RenderingContext;
    readonly canvas: HTMLCanvasElement;
    readonly constants: GLConstants;
    readonly capabilities: IGLCapabilities;
    readonly extensions: IExtensionRegistry;
    readonly state: IGLStateCache;
    readonly registry: {
        register(resource: { readonly id: number | string; handleContextLost(): void; handleContextRestored(ctx: IGLContext): void }, kind: string, priority?: number): () => void;
        unregister(id: number | string): boolean;
        readonly size: number;
    };
    readonly isDisposed: boolean;
    readonly isLost: boolean;
    readonly locale: GLContextLocale;
    readonly attributes: GLContextAttributes;
    onLost(listener: (event: Event) => void): Unsubscribe;
    onRestored(listener: (event: Event) => void): Unsubscribe;
    onDisposed(listener: () => void): Unsubscribe;
    labelObject(type: number, object: unknown | null, label: string): void;
    getError(): number;
    flush(): void;
    finish(): void;
    loseContext(): void;
    restoreContext(): void;
    resetState(): void;
    invalidateState(): void;
    dispose(): void;
    [Symbol.dispose](): void;
}

export type InferExtension<T extends ExtensionName> = ExtensionMap[T];

export type StrictOmit<T, K extends keyof T> = Omit<T, K>;

export type DeepBrand<T, B extends PropertyKey> = T & Brand<T, B>;

export type ReadonlyKeys<T> = {
    readonly [K in keyof T]: T[K];
};

export type GLContextFactory = {
    (options: CreateGLContextOptions): IGLContext;
    fromCanvas(canvas: HTMLCanvasElement, attributes?: GLContextAttributes, locale?: GLContextLocale): IGLContext;
    fromGL(gl: WebGL2RenderingContext, canvas?: HTMLCanvasElement | null, locale?: GLContextLocale): IGLContext;
    wrap(gl: WebGL2RenderingContext, canvas: HTMLCanvasElement): IGLContext;
    isGLContext(value: unknown): value is IGLContext;
    resolve(source: ContextSource): WebGL2RenderingContext;
    unwrap(source: ContextSource): WebGL2RenderingContext;
};
