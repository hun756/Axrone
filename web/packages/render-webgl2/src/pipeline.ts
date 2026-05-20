import type {
    ReadonlyRenderResourceRegistry,
    RenderClearState,
    RenderExecutionContext,
    RenderFrameResult,
    RenderPassKind,
    RenderPipelineBackend,
    RenderResourceAllocator,
    RenderResourceName,
    RenderTextureDescriptor,
    ResolvedRenderPass,
} from '@axrone/render-core/types';

export interface WebGL2RenderTextureNativeHandle {
    readonly kind: 'texture';
    readonly texture: WebGLTexture;
    readonly target: number;
}

export interface WebGL2DefaultFramebufferHandle {
    readonly kind: 'default-framebuffer';
}

export type WebGL2RenderResourceHandle =
    | WebGL2RenderTextureNativeHandle
    | WebGL2DefaultFramebufferHandle;

export interface WebGL2RenderPassExecutionResult {
    readonly drawCalls?: number;
    readonly notes?: readonly string[];
}

export interface WebGL2ResolvedFramebufferBinding {
    readonly framebuffer: WebGLFramebuffer | null;
    readonly colorTarget: RenderResourceName | null;
    readonly depthTarget: RenderResourceName | null;
    readonly width: number;
    readonly height: number;
    readonly defaultFramebuffer: boolean;
}

export interface WebGL2RenderPassExecutionContext {
    readonly binding: WebGL2ResolvedFramebufferBinding;
    readonly pass: ResolvedRenderPass;
    readonly frame: number;
}

export type WebGL2RenderPassHandler = (
    pass: ResolvedRenderPass,
    context: RenderExecutionContext<WebGL2RenderResourceHandle>,
    execution: WebGL2RenderPassExecutionContext
) => void | WebGL2RenderPassExecutionResult | Promise<void | WebGL2RenderPassExecutionResult>;

export type WebGL2RenderPassHandlerMap = Partial<
    Record<RenderPassKind, WebGL2RenderPassHandler>
>;

export interface WebGL2RenderPassCapture {
    readonly name: string;
    readonly kind: RenderPassKind;
    readonly target: string | null;
    readonly queue: string;
    readonly itemCount: number;
    readonly lightCount: number;
    readonly probeCount: number;
    readonly defaultFramebuffer: boolean;
    readonly notes: readonly string[];
}

export interface WebGL2RenderFrameCapture {
    readonly frame: number;
    readonly passes: readonly WebGL2RenderPassCapture[];
}

export interface WebGL2RenderBackendProfilerSnapshot {
    readonly frame: number;
    readonly passCount: number;
    readonly drawCalls: number;
    readonly clears: number;
    readonly presents: number;
    readonly cpuTimeMs: number;
}

export interface ManagedWebGL2RenderPipelineBackend
    extends RenderPipelineBackend<WebGL2RenderResourceHandle> {
    getLastFrameCapture(): WebGL2RenderFrameCapture | null;
    getProfilerSnapshot(): WebGL2RenderBackendProfilerSnapshot;
    dispose(): void;
    [Symbol.dispose](): void;
}

export interface ManagedWebGL2RenderPipelineBackendOptions {
    readonly gl: WebGL2RenderingContext;
    readonly handlers?: WebGL2RenderPassHandlerMap;
    readonly defaultFramebuffer?: WebGLFramebuffer | null;
    readonly strictUnsupportedPasses?: boolean;
    readonly directFrameOutput?: boolean;
}

interface WebGL2FormatInfo {
    readonly internalFormat: number;
    readonly format: number;
    readonly type: number;
}

interface MutableProfilerState {
    frame: number;
    passCount: number;
    drawCalls: number;
    clears: number;
    presents: number;
    cpuTimeMs: number;
}

const DEFAULT_FRAMEBUFFER_HANDLE: WebGL2DefaultFramebufferHandle = Object.freeze({
    kind: 'default-framebuffer',
});

interface WebGL2TonemapUniforms {
    readonly source: WebGLUniformLocation | null;
    readonly mode: WebGLUniformLocation | null;
    readonly exposureScale: WebGLUniformLocation | null;
    readonly gamma: WebGLUniformLocation | null;
    readonly contrast: WebGLUniformLocation | null;
    readonly saturation: WebGLUniformLocation | null;
    readonly shoulderStrength: WebGLUniformLocation | null;
    readonly toeStrength: WebGLUniformLocation | null;
    readonly colorSpace: WebGLUniformLocation | null;
}

interface WebGL2TonemapResources {
    readonly program: WebGLProgram;
    readonly vertexArray: WebGLVertexArrayObject | null;
    readonly uniforms: WebGL2TonemapUniforms;
}

interface WebGL2PostProcessUniforms {
    readonly source: WebGLUniformLocation | null;
    readonly effectMode: WebGLUniformLocation | null;
    readonly texelSize: WebGLUniformLocation | null;
    readonly primary: WebGLUniformLocation | null;
    readonly color: WebGLUniformLocation | null;
    readonly lift: WebGLUniformLocation | null;
    readonly gamma: WebGLUniformLocation | null;
    readonly gain: WebGLUniformLocation | null;
    readonly frameSeed: WebGLUniformLocation | null;
}

interface WebGL2PostProcessResources {
    readonly program: WebGLProgram;
    readonly vertexArray: WebGLVertexArrayObject | null;
    readonly uniforms: WebGL2PostProcessUniforms;
}

const TONEMAP_PASS_NOTES = Object.freeze(['builtin-tonemap']) as readonly string[];
const FULLSCREEN_VERTEX_SHADER_SOURCE = `#version 300 es
precision highp float;

out vec2 vUv;

void main() {
    vec2 positions[3] = vec2[](vec2(-1.0, -1.0), vec2(3.0, -1.0), vec2(-1.0, 3.0));
    vec2 position = positions[gl_VertexID];
    vUv = position * 0.5 + 0.5;
    gl_Position = vec4(position, 0.0, 1.0);
}
`;

const TONEMAP_FRAGMENT_SHADER_SOURCE = `#version 300 es
precision highp float;

uniform sampler2D uSource;
uniform int uMode;
uniform float uExposureScale;
uniform float uGamma;
uniform float uContrast;
uniform float uSaturation;
uniform float uShoulderStrength;
uniform float uToeStrength;
uniform int uColorSpace;

in vec2 vUv;
out vec4 outColor;

vec3 applyReinhard(vec3 color) {
    return color / (1.0 + color);
}

vec3 applyAces(vec3 color) {
    return clamp(
        (color * (2.51 * color + 0.03)) / (color * (2.43 * color + 0.59) + 0.14),
        0.0,
        1.0
    );
}

vec3 rrtAndOdtFit(vec3 value) {
    vec3 a = value * (value + 0.0245786) - 0.000090537;
    vec3 b = value * (0.983729 * value + 0.4329510) + 0.238081;
    return a / b;
}

vec3 applyAcesFitted(vec3 color) {
    const mat3 inputMatrix = mat3(
        0.59719, 0.07600, 0.02840,
        0.35458, 0.90834, 0.13383,
        0.04823, 0.01566, 0.83777
    );
    const mat3 outputMatrix = mat3(
        1.60475, -0.10208, -0.00327,
        -0.53108, 1.10813, -0.07276,
        -0.07367, -0.00605, 1.07602
    );

    color = inputMatrix * color;
    color = rrtAndOdtFit(color);
    return clamp(outputMatrix * color, 0.0, 1.0);
}

vec3 applyFilmic(vec3 color) {
    color = max(vec3(0.0), color - vec3(uToeStrength * 0.02));
    return clamp(
        (color * (6.2 * color + 0.5 + uShoulderStrength)) /
            (color * (6.2 * color + 1.7 + uShoulderStrength * 2.0) + 0.06 + uToeStrength * 0.02),
        0.0,
        1.0
    );
}

vec3 agxDefaultContrastApprox(vec3 value) {
    vec3 value2 = value * value;
    vec3 value4 = value2 * value2;
    return 15.5 * value4 * value2 -
        40.14 * value4 * value +
        31.96 * value4 -
        6.868 * value2 * value +
        0.4298 * value2 +
        0.1191 * value -
        0.00232;
}

vec3 applyAgx(vec3 color) {
    const mat3 inputMatrix = mat3(
        0.842479062253094, 0.0423282422610123, 0.0423756549057051,
        0.0784335999999992, 0.878468636469772, 0.0784336,
        0.0792237451477643, 0.0791661274605434, 0.879142973793104
    );
    const mat3 outputMatrix = mat3(
        1.19687900512017, -0.0528968517574562, -0.0529716355144438,
        -0.0980208811401368, 1.15190312990417, -0.0980434501171241,
        -0.0990297440797205, -0.0989611768448433, 1.15107367264116
    );

    color = inputMatrix * max(color, vec3(0.0));
    color = log2(max(color, vec3(1e-6)));
    color = clamp((color + 12.47393) / 16.5, 0.0, 1.0);
    color = agxDefaultContrastApprox(color);
    return clamp(outputMatrix * color, 0.0, 1.0);
}

vec3 applyNeutral(vec3 color) {
    const float startCompression = 0.76;
    const float desaturation = 0.15;

    float minimumChannel = min(color.r, min(color.g, color.b));
    float offset = minimumChannel < 0.08
        ? minimumChannel - 6.25 * minimumChannel * minimumChannel
        : 0.04;
    color -= offset;

    float peak = max(color.r, max(color.g, color.b));
    if (peak < startCompression) {
        return color;
    }

    float distance = 1.0 - startCompression;
    float compressedPeak = 1.0 - distance * distance / (peak + distance - startCompression);
    color *= compressedPeak / peak;

    float g = 1.0 - 1.0 / (desaturation * (peak - compressedPeak) + 1.0);
    return mix(color, vec3(compressedPeak), g);
}

vec3 applyTonemap(vec3 color) {
    if (uMode == 1) {
        return applyReinhard(color);
    }
    if (uMode == 2) {
        return applyAces(color);
    }
    if (uMode == 3) {
        return applyAcesFitted(color);
    }
    if (uMode == 4) {
        return applyFilmic(color);
    }
    if (uMode == 5) {
        return applyAgx(color);
    }
    if (uMode == 6) {
        return applyNeutral(color);
    }
    return max(color, vec3(0.0));
}

void main() {
    vec4 sampled = texture(uSource, vUv);
    vec3 color = sampled.rgb * uExposureScale;
    color = applyTonemap(color);

    float luminance = dot(color, vec3(0.2126, 0.7152, 0.0722));
    color = mix(vec3(luminance), color, uSaturation);
    color = (color - 0.5) * uContrast + 0.5;
    color = clamp(color, 0.0, 1.0);

    if (uColorSpace == 0 || uColorSpace == 1 || uColorSpace == 2) {
        color = pow(color, vec3(1.0 / max(uGamma, 0.0001)));
    }

    outColor = vec4(color, sampled.a);
}
`;

const POST_PROCESS_FRAGMENT_SHADER_SOURCE = `#version 300 es
precision highp float;

uniform sampler2D uSource;
uniform int uEffectMode;
uniform vec2 uTexelSize;
uniform vec4 uPrimary;
uniform vec3 uColor;
uniform vec3 uLift;
uniform vec3 uGammaVec;
uniform vec3 uGain;
uniform float uFrameSeed;

in vec2 vUv;
out vec4 outColor;

float luma(vec3 color) {
    return dot(color, vec3(0.299, 0.587, 0.114));
}

float hash12(vec2 value) {
    vec3 value3 = fract(vec3(value.xyx) * 0.1031);
    value3 += dot(value3, value3.yzx + 33.33);
    return fract((value3.x + value3.y) * value3.z);
}

vec3 sampleSource(vec2 uv) {
    return texture(uSource, clamp(uv, uTexelSize * 0.5, vec2(1.0) - uTexelSize * 0.5)).rgb;
}

vec3 applyFxaa(vec2 uv) {
    vec3 rgbM = sampleSource(uv);
    vec3 rgbNW = sampleSource(uv + vec2(-1.0, -1.0) * uTexelSize);
    vec3 rgbNE = sampleSource(uv + vec2(1.0, -1.0) * uTexelSize);
    vec3 rgbSW = sampleSource(uv + vec2(-1.0, 1.0) * uTexelSize);
    vec3 rgbSE = sampleSource(uv + vec2(1.0, 1.0) * uTexelSize);

    float lumaNW = luma(rgbNW);
    float lumaNE = luma(rgbNE);
    float lumaSW = luma(rgbSW);
    float lumaSE = luma(rgbSE);
    float lumaM = luma(rgbM);
    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
    float edgeThreshold = max(uPrimary.z, lumaMax * uPrimary.y);

    if ((lumaMax - lumaMin) < edgeThreshold) {
        return rgbM;
    }

    vec2 direction;
    direction.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    direction.y = (lumaNW + lumaSW) - (lumaNE + lumaSE);

    float directionReduce = max(
        (lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * max(uPrimary.x, 0.001)),
        1.0 / 128.0
    );
    float inverseDirectionAdjustment = 1.0 / (min(abs(direction.x), abs(direction.y)) + directionReduce);
    direction = clamp(direction * inverseDirectionAdjustment, vec2(-8.0), vec2(8.0)) * uTexelSize;

    vec3 rgbA = 0.5 * (
        sampleSource(uv + direction * (1.0 / 3.0 - 0.5)) +
        sampleSource(uv + direction * (2.0 / 3.0 - 0.5))
    );
    vec3 rgbB = rgbA * 0.5 + 0.25 * (
        sampleSource(uv + direction * -0.5) +
        sampleSource(uv + direction * 0.5)
    );
    float lumaB = luma(rgbB);

    return (lumaB < lumaMin || lumaB > lumaMax)
        ? rgbA
        : mix(rgbA, rgbB, clamp(uPrimary.x, 0.0, 1.0));
}

vec3 applyVignette(vec3 color, vec2 uv) {
    float intensity = clamp(uPrimary.x, 0.0, 1.0);
    float smoothness = clamp(uPrimary.y, 0.001, 1.0);
    float roundness = clamp(uPrimary.z, 0.05, 1.0);
    vec2 centered = uv * 2.0 - 1.0;
    centered.x *= mix(1.0, 1.35, 1.0 - roundness);
    float distanceToCenter = length(centered);
    float mask = smoothstep(1.0 - smoothness, 1.0, distanceToCenter);
    return mix(color, uColor, mask * intensity);
}

vec3 applyFilmGrain(vec3 color, vec2 uv) {
    float intensity = max(uPrimary.x, 0.0);
    float response = clamp(uPrimary.y, 0.0, 1.0);
    float noise = hash12(uv * vec2(1280.0, 720.0) + vec2(uFrameSeed, uFrameSeed * 1.37));
    float grain = (noise - 0.5) * intensity;
    float luminanceResponse = mix(1.0, 1.0 - luma(color), response);
    return clamp(color + vec3(grain * luminanceResponse), 0.0, 1.0);
}

vec3 applyChromaticAberration(vec2 uv) {
    vec2 offset = (uv - 0.5) * (uPrimary.x * 0.075);
    float red = sampleSource(uv + offset).r;
    float green = sampleSource(uv).g;
    float blue = sampleSource(uv - offset).b;
    return vec3(red, green, blue);
}

vec3 applyColorGrading(vec3 color) {
    float contrast = uPrimary.x;
    float saturation = uPrimary.y;
    float temperature = uPrimary.z;
    float tint = uPrimary.w;

    color *= vec3(
        1.0 + temperature * 0.05 - tint * 0.02,
        1.0,
        1.0 - temperature * 0.05 + tint * 0.02
    );
    color = max(color * uGain + (uLift - vec3(1.0)), vec3(0.0));
    color = pow(max(color, vec3(1e-4)), 1.0 / max(uGammaVec, vec3(1e-4)));

    float luminance = luma(color);
    color = mix(vec3(luminance), color, saturation);
    color = (color - 0.5) * contrast + 0.5;
    return clamp(color, 0.0, 1.0);
}

void main() {
    vec4 sampled = texture(uSource, vUv);
    vec3 color = sampled.rgb;

    if (uEffectMode == 1) {
        color = applyFxaa(vUv);
    } else if (uEffectMode == 2) {
        color = applyVignette(color, vUv);
    } else if (uEffectMode == 3) {
        color = applyFilmGrain(color, vUv);
    } else if (uEffectMode == 4) {
        color = applyChromaticAberration(vUv);
    } else if (uEffectMode == 5) {
        color = applyColorGrading(color);
    }

    outColor = vec4(clamp(color, 0.0, 1.0), sampled.a);
}
`;

const SUPPORTED_BUILTIN_POST_PROCESS_EFFECTS = new Set<string>([
    'color-grading',
    'chromatic-aberration',
    'film-grain',
    'fxaa',
    'vignette',
]);

const PASS_REQUIRES_HANDLER = new Set<RenderPassKind>([
    'depth-prepass',
    'shadow',
    'opaque',
    'reflection-probe',
    'global-illumination',
    'volumetric',
    'skybox',
    'transparent',
    'post-process',
    'tonemap',
    'light-bake',
]);

const getNow = (): number =>
    typeof performance !== 'undefined' && typeof performance.now === 'function'
        ? performance.now()
        : Date.now();

const isPromiseLike = <T>(value: T | PromiseLike<T>): value is PromiseLike<T> =>
    typeof value === 'object' && value !== null && 'then' in value;

const isFrameResourceName = (value: RenderResourceName | null): boolean =>
    value !== null && value.startsWith('frame:');

const resolveFormatInfo = (
    gl: WebGL2RenderingContext,
    format: RenderTextureDescriptor['format']
): WebGL2FormatInfo => {
    switch (format) {
        case 'r11g11b10f':
            return {
                internalFormat: gl.R11F_G11F_B10F,
                format: gl.RGB,
                type: gl.UNSIGNED_INT_10F_11F_11F_REV,
            };
        case 'rgba8':
            return {
                internalFormat: gl.RGBA8,
                format: gl.RGBA,
                type: gl.UNSIGNED_BYTE,
            };
        case 'rgba16f':
            return {
                internalFormat: gl.RGBA16F,
                format: gl.RGBA,
                type: gl.HALF_FLOAT,
            };
        case 'rgba32f':
            return {
                internalFormat: gl.RGBA32F,
                format: gl.RGBA,
                type: gl.FLOAT,
            };
        case 'rg16f':
            return {
                internalFormat: gl.RG16F,
                format: gl.RG,
                type: gl.HALF_FLOAT,
            };
        case 'rg32f':
            return {
                internalFormat: gl.RG32F,
                format: gl.RG,
                type: gl.FLOAT,
            };
        case 'r16f':
            return {
                internalFormat: gl.R16F,
                format: gl.RED,
                type: gl.HALF_FLOAT,
            };
        case 'r32f':
            return {
                internalFormat: gl.R32F,
                format: gl.RED,
                type: gl.FLOAT,
            };
        case 'depth24':
            return {
                internalFormat: gl.DEPTH_COMPONENT24,
                format: gl.DEPTH_COMPONENT,
                type: gl.UNSIGNED_INT,
            };
        case 'depth32f':
            return {
                internalFormat: gl.DEPTH_COMPONENT32F,
                format: gl.DEPTH_COMPONENT,
                type: gl.FLOAT,
            };
        case 'depth24-stencil8':
            return {
                internalFormat: gl.DEPTH24_STENCIL8,
                format: gl.DEPTH_STENCIL,
                type: gl.UNSIGNED_INT_24_8,
            };
    }
};

const createTextureTarget = (
    gl: WebGL2RenderingContext,
    descriptor: Readonly<RenderTextureDescriptor>
): number => {
    if (descriptor.cube) {
        return gl.TEXTURE_CUBE_MAP;
    }
    if ((descriptor.arrayLayers ?? 1) > 1) {
        return gl.TEXTURE_2D_ARRAY;
    }
    if ((descriptor.depth ?? 1) > 1) {
        return gl.TEXTURE_3D;
    }
    return gl.TEXTURE_2D;
};

const createTextureStorage = (
    gl: WebGL2RenderingContext,
    texture: WebGLTexture,
    descriptor: Readonly<RenderTextureDescriptor>
): WebGL2RenderTextureNativeHandle => {
    const target = createTextureTarget(gl, descriptor);
    const formatInfo = resolveFormatInfo(gl, descriptor.format);
    const mipLevels = Math.max(1, descriptor.mipLevels ?? 1);

    gl.bindTexture(target, texture);
    gl.texParameteri(target, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(target, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(target, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(target, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

    if (target === gl.TEXTURE_3D || target === gl.TEXTURE_2D_ARRAY) {
        gl.texParameteri(target, gl.TEXTURE_WRAP_R, gl.CLAMP_TO_EDGE);
    }

    if (target === gl.TEXTURE_2D || target === gl.TEXTURE_CUBE_MAP) {
        gl.texStorage2D(target, mipLevels, formatInfo.internalFormat, descriptor.width, descriptor.height);
    } else {
        gl.texStorage3D(
            target,
            mipLevels,
            formatInfo.internalFormat,
            descriptor.width,
            descriptor.height,
            descriptor.depth ?? descriptor.arrayLayers ?? 1
        );
    }

    gl.bindTexture(target, null);

    return {
        kind: 'texture',
        texture,
        target,
    };
};

export const createWebGL2RenderResourceAllocator = (
    gl: WebGL2RenderingContext
): RenderResourceAllocator<WebGL2RenderResourceHandle> => ({
    createTexture(descriptor, previous) {
        if (descriptor.usage.includes('present')) {
            if (previous?.kind === 'texture') {
                gl.deleteTexture(previous.texture);
            }
            return DEFAULT_FRAMEBUFFER_HANDLE;
        }

        if ((descriptor.samples ?? 1) !== 1) {
            throw new Error('WebGL2 render resource allocator does not support multisampled textures yet');
        }

        if (previous?.kind === 'texture') {
            gl.deleteTexture(previous.texture);
        }

        const texture = gl.createTexture();
        if (!texture) {
            throw new Error('Failed to allocate WebGL2 render texture');
        }

        return createTextureStorage(gl, texture, descriptor);
    },
    destroyTexture(native, _descriptor) {
        if (native.kind === 'texture') {
            gl.deleteTexture(native.texture);
        }
    },
});

const resolveFramebufferTargets = (
    pass: ResolvedRenderPass
): {
    readonly colorTarget: RenderResourceName | null;
    readonly depthTarget: RenderResourceName | null;
} => {
    switch (pass.kind) {
        case 'depth-prepass':
            return {
                colorTarget: null,
                depthTarget: pass.metadata.depth,
            };
        case 'shadow':
            return {
                colorTarget: null,
                depthTarget: pass.metadata.atlas,
            };
        case 'opaque':
            return {
                colorTarget: pass.metadata.color,
                depthTarget: pass.metadata.depth,
            };
        case 'skybox':
            return {
                colorTarget: pass.metadata.color,
                depthTarget: pass.metadata.depth,
            };
        case 'transparent':
            return {
                colorTarget: pass.metadata.color,
                depthTarget: pass.metadata.depth,
            };
        case 'post-process':
            return {
                colorTarget: pass.metadata.target,
                depthTarget: null,
            };
        case 'tonemap':
            return {
                colorTarget: pass.metadata.target,
                depthTarget: null,
            };
        case 'reflection-probe':
            return {
                colorTarget: pass.metadata.target,
                depthTarget: null,
            };
        case 'global-illumination':
            return {
                colorTarget: pass.metadata.target,
                depthTarget: null,
            };
        case 'volumetric':
            return {
                colorTarget: pass.metadata.froxelGrid,
                depthTarget: null,
            };
        case 'present':
        case 'light-bake':
            return {
                colorTarget: null,
                depthTarget: null,
            };
    }
};

const clearFramebuffer = (
    gl: WebGL2RenderingContext,
    clearState: RenderClearState | null | undefined
): number => {
    if (!clearState) {
        return 0;
    }

    let bits = 0;

    if (clearState.color !== undefined && clearState.color !== null) {
        const color = clearState.color as readonly number[];
        gl.clearColor(color[0] ?? 0, color[1] ?? 0, color[2] ?? 0, color[3] ?? 1);
        bits |= gl.COLOR_BUFFER_BIT;
    }

    if (clearState.depth !== undefined && clearState.depth !== null) {
        gl.clearDepth(clearState.depth);
        bits |= gl.DEPTH_BUFFER_BIT;
    }

    if (clearState.stencil !== undefined && clearState.stencil !== null) {
        gl.clearStencil(clearState.stencil);
        bits |= gl.STENCIL_BUFFER_BIT;
    }

    if (bits !== 0) {
        gl.clear(bits);
    }

    return bits;
};

const createFramebufferCacheKey = (
    colorTarget: RenderResourceName | null,
    depthTarget: RenderResourceName | null
): string => `${colorTarget ?? 'none'}|${depthTarget ?? 'none'}`;

const getTextureHandle = (
    graph: ReadonlyRenderResourceRegistry<WebGL2RenderResourceHandle>,
    resourceName: RenderResourceName | null
): WebGL2RenderTextureNativeHandle | null => {
    if (!resourceName) {
        return null;
    }

    const snapshot = graph.getTexture(resourceName);
    if (!snapshot || !snapshot.native || snapshot.native.kind !== 'texture') {
        return null;
    }

    return snapshot.native;
};

const compileManagedShader = (
    gl: WebGL2RenderingContext,
    type: number,
    source: string,
    label: string
): WebGLShader => {
    const shader = gl.createShader(type);
    if (!shader) {
        throw new Error(`Failed to create WebGL2 ${label} shader`);
    }

    gl.shaderSource(shader, source);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const infoLog = gl.getShaderInfoLog(shader) ?? 'unknown compile error';
        gl.deleteShader(shader);
        throw new Error(`Failed to compile WebGL2 ${label} shader: ${infoLog}`);
    }

    return shader;
};

const createFullscreenProgram = (
    gl: WebGL2RenderingContext,
    fragmentSource: string,
    label: string
): WebGLProgram => {
    const program = gl.createProgram();
    if (!program) {
        throw new Error(`Failed to create WebGL2 ${label} program`);
    }

    const vertexShader = compileManagedShader(
        gl,
        gl.VERTEX_SHADER,
        FULLSCREEN_VERTEX_SHADER_SOURCE,
        `${label} vertex`
    );
    const fragmentShader = compileManagedShader(
        gl,
        gl.FRAGMENT_SHADER,
        fragmentSource,
        `${label} fragment`
    );

    try {
        gl.attachShader(program, vertexShader);
        gl.attachShader(program, fragmentShader);
        gl.linkProgram(program);

        if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
            const infoLog = gl.getProgramInfoLog(program) ?? 'unknown link error';
            throw new Error(`Failed to link WebGL2 ${label} program: ${infoLog}`);
        }
    } catch (error) {
        gl.deleteProgram(program);
        throw error;
    } finally {
        gl.deleteShader(vertexShader);
        gl.deleteShader(fragmentShader);
    }

    return program;
};

const createTonemapProgram = (gl: WebGL2RenderingContext): WebGLProgram => {
    return createFullscreenProgram(gl, TONEMAP_FRAGMENT_SHADER_SOURCE, 'tonemap');
};

const createPostProcessProgram = (gl: WebGL2RenderingContext): WebGLProgram => {
    return createFullscreenProgram(gl, POST_PROCESS_FRAGMENT_SHADER_SOURCE, 'post-process');
};

const resolveTonemapModeId = (
    mode: Extract<ResolvedRenderPass, { kind: 'tonemap' }>['metadata']['mode']
): number => {
    switch (mode) {
        case 'reinhard':
            return 1;
        case 'aces':
            return 2;
        case 'aces-fitted':
            return 3;
        case 'filmic':
            return 4;
        case 'agx':
            return 5;
        case 'neutral':
            return 6;
        default:
            return 0;
    }
};

const resolveTonemapColorSpaceId = (
    colorSpace: Extract<ResolvedRenderPass, { kind: 'tonemap' }>['metadata']['colorSpace']
): number => {
    switch (colorSpace) {
        case 'display-p3':
            return 1;
        case 'rec2020':
            return 2;
        default:
            return 0;
    }
};

const resolveTonemapExposureScale = (
    exposure: Extract<ResolvedRenderPass, { kind: 'tonemap' }>['metadata']['exposure']
): number => {
    if (!exposure) {
        return 1;
    }

    if (exposure.mode === 'manual') {
        return Math.pow(2, exposure.exposure);
    }

    return Math.max(0, exposure.keyValue ?? 0.18) / 0.18;
};

const resolvePostProcessEffectId = (effectName: string): number => {
    switch (effectName) {
        case 'fxaa':
            return 1;
        case 'vignette':
            return 2;
        case 'film-grain':
            return 3;
        case 'chromatic-aberration':
            return 4;
        case 'color-grading':
            return 5;
        default:
            return 0;
    }
};

export const createManagedWebGL2RenderPipelineBackend = (
    options: ManagedWebGL2RenderPipelineBackendOptions
): ManagedWebGL2RenderPipelineBackend => {
    const { gl } = options;
    const framebufferCache = new Map<string, WebGLFramebuffer>();
    const strictUnsupportedPasses = options.strictUnsupportedPasses ?? true;
    const directFrameOutput = options.directFrameOutput ?? false;
    let lastFrameCapture: WebGL2RenderFrameCapture | null = null;
    let frameCaptures: WebGL2RenderPassCapture[] = [];
    let frameStartTime = 0;
    let tonemapResources: WebGL2TonemapResources | null = null;
    let postProcessResources: WebGL2PostProcessResources | null = null;

    const profiler: MutableProfilerState = {
        frame: 0,
        passCount: 0,
        drawCalls: 0,
        clears: 0,
        presents: 0,
        cpuTimeMs: 0,
    };

    const deleteFramebufferCache = (): void => {
        for (const framebuffer of framebufferCache.values()) {
            gl.deleteFramebuffer(framebuffer);
        }
        framebufferCache.clear();
    };

    const deleteTonemapResources = (): void => {
        if (tonemapResources?.vertexArray) {
            gl.deleteVertexArray?.(tonemapResources.vertexArray);
        }

        if (tonemapResources?.program) {
            gl.deleteProgram?.(tonemapResources.program);
        }

        tonemapResources = null;
    };

    const deletePostProcessResources = (): void => {
        if (postProcessResources?.vertexArray) {
            gl.deleteVertexArray?.(postProcessResources.vertexArray);
        }

        if (postProcessResources?.program) {
            gl.deleteProgram?.(postProcessResources.program);
        }

        postProcessResources = null;
    };

    const ensureTonemapResources = (): WebGL2TonemapResources => {
        if (tonemapResources) {
            return tonemapResources;
        }

        const program = createTonemapProgram(gl);
        const vertexArray = gl.createVertexArray?.() ?? null;
        tonemapResources = {
            program,
            vertexArray,
            uniforms: {
                source: gl.getUniformLocation(program, 'uSource'),
                mode: gl.getUniformLocation(program, 'uMode'),
                exposureScale: gl.getUniformLocation(program, 'uExposureScale'),
                gamma: gl.getUniformLocation(program, 'uGamma'),
                contrast: gl.getUniformLocation(program, 'uContrast'),
                saturation: gl.getUniformLocation(program, 'uSaturation'),
                shoulderStrength: gl.getUniformLocation(program, 'uShoulderStrength'),
                toeStrength: gl.getUniformLocation(program, 'uToeStrength'),
                colorSpace: gl.getUniformLocation(program, 'uColorSpace'),
            },
        };

        return tonemapResources;
    };

    const ensurePostProcessResources = (): WebGL2PostProcessResources => {
        if (postProcessResources) {
            return postProcessResources;
        }

        const program = createPostProcessProgram(gl);
        const vertexArray = gl.createVertexArray?.() ?? null;
        postProcessResources = {
            program,
            vertexArray,
            uniforms: {
                source: gl.getUniformLocation(program, 'uSource'),
                effectMode: gl.getUniformLocation(program, 'uEffectMode'),
                texelSize: gl.getUniformLocation(program, 'uTexelSize'),
                primary: gl.getUniformLocation(program, 'uPrimary'),
                color: gl.getUniformLocation(program, 'uColor'),
                lift: gl.getUniformLocation(program, 'uLift'),
                gamma: gl.getUniformLocation(program, 'uGammaVec'),
                gain: gl.getUniformLocation(program, 'uGain'),
                frameSeed: gl.getUniformLocation(program, 'uFrameSeed'),
            },
        };

        return postProcessResources;
    };

    const resolveTextureSnapshot = (
        graph: ReadonlyRenderResourceRegistry<WebGL2RenderResourceHandle>,
        resourceName: RenderResourceName | null
    ) => (resourceName ? graph.getTexture(resourceName) : null);

    const resolveManagedFramebuffer = (
        colorTarget: RenderResourceName | null,
        depthTarget: RenderResourceName | null,
        context: RenderExecutionContext<WebGL2RenderResourceHandle>
    ): WebGL2ResolvedFramebufferBinding => {
        if (
            directFrameOutput &&
            (isFrameResourceName(colorTarget) || isFrameResourceName(depthTarget))
        ) {
            return {
                framebuffer: options.defaultFramebuffer ?? null,
                colorTarget,
                depthTarget,
                width: context.viewport.width,
                height: context.viewport.height,
                defaultFramebuffer: true,
            };
        }

        const colorHandle = getTextureHandle(context.graph, colorTarget);
        const depthHandle = getTextureHandle(context.graph, depthTarget);

        if (!colorHandle && !depthHandle) {
            return {
                framebuffer: options.defaultFramebuffer ?? null,
                colorTarget,
                depthTarget,
                width: context.viewport.width,
                height: context.viewport.height,
                defaultFramebuffer: true,
            };
        }

        if (
            (colorHandle && colorHandle.target !== gl.TEXTURE_2D) ||
            (depthHandle && depthHandle.target !== gl.TEXTURE_2D)
        ) {
            throw new Error('Managed WebGL2 render backend currently supports 2D framebuffer attachments only');
        }

        const cacheKey = createFramebufferCacheKey(colorTarget, depthTarget);
        let framebuffer = framebufferCache.get(cacheKey);
        if (!framebuffer) {
            framebuffer = gl.createFramebuffer();
            if (!framebuffer) {
                throw new Error('Failed to create WebGL2 framebuffer for render backend');
            }
            framebufferCache.set(cacheKey, framebuffer);
        }

        gl.bindFramebuffer(gl.FRAMEBUFFER, framebuffer);

        if (colorHandle) {
            gl.framebufferTexture2D(
                gl.FRAMEBUFFER,
                gl.COLOR_ATTACHMENT0,
                gl.TEXTURE_2D,
                colorHandle.texture,
                0
            );
            gl.drawBuffers([gl.COLOR_ATTACHMENT0]);
        } else {
            gl.drawBuffers([]);
        }

        if (depthHandle) {
            const depthDescriptor = resolveTextureSnapshot(context.graph, depthTarget)?.descriptor;
            const attachment = depthDescriptor?.format === 'depth24-stencil8'
                ? gl.DEPTH_STENCIL_ATTACHMENT
                : gl.DEPTH_ATTACHMENT;

            gl.framebufferTexture2D(
                gl.FRAMEBUFFER,
                attachment,
                gl.TEXTURE_2D,
                depthHandle.texture,
                0
            );
        }

        const sizeSnapshot = resolveTextureSnapshot(context.graph, colorTarget ?? depthTarget);

        return {
            framebuffer,
            colorTarget,
            depthTarget,
            width: sizeSnapshot?.descriptor.width ?? context.viewport.width,
            height: sizeSnapshot?.descriptor.height ?? context.viewport.height,
            defaultFramebuffer: false,
        };
    };

    const resolveFramebuffer = (
        pass: ResolvedRenderPass,
        context: RenderExecutionContext<WebGL2RenderResourceHandle>
    ): WebGL2ResolvedFramebufferBinding => {
        if (pass.kind === 'present') {
            return {
                framebuffer: options.defaultFramebuffer ?? null,
                colorTarget: null,
                depthTarget: null,
                width: context.viewport.width,
                height: context.viewport.height,
                defaultFramebuffer: true,
            };
        }

        const targets = resolveFramebufferTargets(pass);
        return resolveManagedFramebuffer(targets.colorTarget, targets.depthTarget, context);
    };

    const performPresent = (
        pass: Extract<ResolvedRenderPass, { kind: 'present' }>,
        context: RenderExecutionContext<WebGL2RenderResourceHandle>
    ): void => {
        if (directFrameOutput && isFrameResourceName(pass.metadata.source)) {
            return;
        }

        const sourceBinding = resolveManagedFramebuffer(pass.metadata.source, null, context);
        gl.bindFramebuffer(gl.READ_FRAMEBUFFER, sourceBinding.framebuffer);
        gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, options.defaultFramebuffer ?? null);
        gl.blitFramebuffer(
            0,
            0,
            sourceBinding.width,
            sourceBinding.height,
            0,
            0,
            context.viewport.width,
            context.viewport.height,
            gl.COLOR_BUFFER_BIT,
            gl.NEAREST
        );
    };

    const executeBuiltinTonemapPass = (
        pass: Extract<ResolvedRenderPass, { kind: 'tonemap' }>,
        context: RenderExecutionContext<WebGL2RenderResourceHandle>
    ): WebGL2RenderPassExecutionResult => {
        const sourceTexture = getTextureHandle(context.graph, pass.metadata.source);

        if (!sourceTexture || sourceTexture.target !== gl.TEXTURE_2D) {
            throw new Error('Built-in WebGL2 tonemap passes require a 2D texture source');
        }

        const resources = ensureTonemapResources();

        gl.disable(gl.DEPTH_TEST);
        gl.disable(gl.CULL_FACE);
        gl.disable(gl.BLEND);
        gl.depthMask?.(false);
        gl.colorMask?.(true, true, true, true);
        gl.useProgram(resources.program);
        gl.bindVertexArray?.(resources.vertexArray);
        gl.activeTexture?.(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, sourceTexture.texture);
        gl.uniform1i?.(resources.uniforms.source, 0);
        gl.uniform1i?.(resources.uniforms.mode, resolveTonemapModeId(pass.metadata.mode));
        gl.uniform1f?.(
            resources.uniforms.exposureScale,
            resolveTonemapExposureScale(pass.metadata.exposure)
        );
        gl.uniform1f?.(resources.uniforms.gamma, pass.metadata.gamma);
        gl.uniform1f?.(resources.uniforms.contrast, pass.metadata.contrast);
        gl.uniform1f?.(resources.uniforms.saturation, pass.metadata.saturation);
        gl.uniform1f?.(resources.uniforms.shoulderStrength, pass.metadata.shoulderStrength);
        gl.uniform1f?.(resources.uniforms.toeStrength, pass.metadata.toeStrength);
        gl.uniform1i?.(
            resources.uniforms.colorSpace,
            resolveTonemapColorSpaceId(pass.metadata.colorSpace)
        );
        gl.drawArrays(gl.TRIANGLES, 0, 3);
        gl.bindVertexArray?.(null);
        gl.bindTexture(gl.TEXTURE_2D, null);
        gl.useProgram(null);
        gl.depthMask?.(true);

        return {
            drawCalls: 1,
            notes: TONEMAP_PASS_NOTES,
        };
    };

    const executeBuiltinPostProcessPass = (
        pass: Extract<ResolvedRenderPass, { kind: 'post-process' }>,
        context: RenderExecutionContext<WebGL2RenderResourceHandle>
    ): WebGL2RenderPassExecutionResult | null => {
        const effect = pass.metadata.effect;
        if (
            effect.category !== 'builtin' ||
            !SUPPORTED_BUILTIN_POST_PROCESS_EFFECTS.has(effect.name)
        ) {
            return null;
        }

        const sourceSnapshot = resolveTextureSnapshot(context.graph, pass.metadata.source);
        const sourceTexture = getTextureHandle(context.graph, pass.metadata.source);
        if (!sourceTexture || sourceTexture.target !== gl.TEXTURE_2D) {
            throw new Error('Built-in WebGL2 post-process passes require a 2D texture source');
        }

        const width = sourceSnapshot?.descriptor.width ?? context.viewport.width;
        const height = sourceSnapshot?.descriptor.height ?? context.viewport.height;
        const resources = ensurePostProcessResources();

        let primary: readonly [number, number, number, number] = [0, 0, 0, 0];
        let color: readonly [number, number, number] = [0, 0, 0];
        let lift: readonly [number, number, number] = [1, 1, 1];
        let gamma: readonly [number, number, number] = [1, 1, 1];
        let gain: readonly [number, number, number] = [1, 1, 1];

        switch (effect.name) {
            case 'fxaa':
                primary = [
                    effect.settings.subpixel ?? 0.75,
                    effect.settings.edgeThreshold ?? 0.166,
                    effect.settings.edgeThresholdMin ?? 0.0833,
                    0,
                ];
                break;
            case 'vignette':
                primary = [
                    effect.settings.intensity ?? 0.2,
                    effect.settings.smoothness ?? 0.55,
                    effect.settings.roundness ?? 1,
                    0,
                ];
                color = effect.settings.color ?? [0, 0, 0];
                break;
            case 'film-grain':
                primary = [
                    effect.settings.intensity ?? 0.12,
                    effect.settings.response ?? 0.85,
                    0,
                    0,
                ];
                break;
            case 'chromatic-aberration':
                primary = [effect.settings.intensity ?? 0.03, 0, 0, 0];
                break;
            case 'color-grading':
                primary = [
                    effect.settings.contrast ?? 1,
                    effect.settings.saturation ?? 1,
                    effect.settings.temperature ?? 0,
                    effect.settings.tint ?? 0,
                ];
                lift = effect.settings.lift ?? [1, 1, 1];
                gamma = effect.settings.gamma ?? [1, 1, 1];
                gain = effect.settings.gain ?? [1, 1, 1];
                break;
        }

        gl.disable(gl.DEPTH_TEST);
        gl.disable(gl.CULL_FACE);
        gl.disable(gl.BLEND);
        gl.depthMask?.(false);
        gl.colorMask?.(true, true, true, true);
        gl.useProgram(resources.program);
        gl.bindVertexArray?.(resources.vertexArray);
        gl.activeTexture?.(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, sourceTexture.texture);
        gl.uniform1i?.(resources.uniforms.source, 0);
        gl.uniform1i?.(resources.uniforms.effectMode, resolvePostProcessEffectId(effect.name));
        gl.uniform2f?.(
            resources.uniforms.texelSize,
            1 / Math.max(1, width),
            1 / Math.max(1, height)
        );
        gl.uniform4f?.(
            resources.uniforms.primary,
            primary[0],
            primary[1],
            primary[2],
            primary[3]
        );
        gl.uniform3f?.(resources.uniforms.color, color[0], color[1], color[2]);
        gl.uniform3f?.(resources.uniforms.lift, lift[0], lift[1], lift[2]);
        gl.uniform3f?.(resources.uniforms.gamma, gamma[0], gamma[1], gamma[2]);
        gl.uniform3f?.(resources.uniforms.gain, gain[0], gain[1], gain[2]);
        gl.uniform1f?.(resources.uniforms.frameSeed, context.frame);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
        gl.bindVertexArray?.(null);
        gl.bindTexture(gl.TEXTURE_2D, null);
        gl.useProgram(null);
        gl.depthMask?.(true);

        return {
            drawCalls: 1,
            notes: Object.freeze([`builtin-post-process:${effect.name}`]),
        };
    };

    return {
        beginFrame(context) {
            profiler.frame = context.frame;
            profiler.passCount = 0;
            profiler.drawCalls = 0;
            profiler.clears = 0;
            profiler.presents = 0;
            profiler.cpuTimeMs = 0;
            frameCaptures = [];
            frameStartTime = getNow();
        },
        executePass(pass, context) {
            const handler = options.handlers?.[pass.kind];
            const binding = resolveFramebuffer(pass, context);

            gl.bindFramebuffer(gl.FRAMEBUFFER, binding.framebuffer);
            gl.viewport(0, 0, binding.width, binding.height);

            if (clearFramebuffer(gl, pass.clearState) !== 0) {
                profiler.clears += 1;
            }

            const finalizePass = (result: WebGL2RenderPassExecutionResult | undefined): void => {
                profiler.passCount += 1;
                profiler.drawCalls += (pass.kind === 'present' ? 1 : 0) + (result?.drawCalls ?? 0);
                frameCaptures.push(
                    Object.freeze({
                        name: pass.name,
                        kind: pass.kind,
                        target: pass.target,
                        queue: pass.queue,
                        itemCount: pass.items?.length ?? 0,
                        lightCount: pass.lights?.length ?? 0,
                        probeCount: pass.probes?.length ?? 0,
                        defaultFramebuffer: binding.defaultFramebuffer,
                        notes: result?.notes ?? Object.freeze([]),
                    })
                );
            };

            if (pass.kind === 'present') {
                performPresent(pass, context);
                profiler.presents += 1;
                finalizePass(undefined);
                return;
            } else if (handler) {
                const handlerResult = handler(pass, context, {
                    binding,
                    pass,
                    frame: context.frame,
                });
                if (isPromiseLike(handlerResult)) {
                    return handlerResult.then((resolvedResult) => {
                        finalizePass(resolvedResult ?? undefined);
                    });
                }

                finalizePass(handlerResult ?? undefined);
                return;
            } else if (pass.kind === 'post-process') {
                const postProcessResult = executeBuiltinPostProcessPass(pass, context);
                if (postProcessResult) {
                    finalizePass(postProcessResult);
                    return;
                }

                if (strictUnsupportedPasses) {
                    throw new Error(
                        `No WebGL2 render pass handler is registered for '${pass.kind}' passes`
                    );
                }
            } else if (pass.kind === 'tonemap') {
                const tonemapResult = executeBuiltinTonemapPass(pass, context);
                finalizePass(tonemapResult);
                return;
            } else if (strictUnsupportedPasses && PASS_REQUIRES_HANDLER.has(pass.kind)) {
                throw new Error(
                    `No WebGL2 render pass handler is registered for '${pass.kind}' passes`
                );
            }

            finalizePass(undefined);
        },
        endFrame(result, _context) {
            gl.bindFramebuffer(gl.FRAMEBUFFER, options.defaultFramebuffer ?? null);
            gl.bindFramebuffer(gl.READ_FRAMEBUFFER, null);
            gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, null);

            profiler.cpuTimeMs = getNow() - frameStartTime;
            lastFrameCapture = Object.freeze({
                frame: result.frame,
                passes: Object.freeze([...frameCaptures]),
            });
        },
        getLastFrameCapture() {
            return lastFrameCapture;
        },
        getProfilerSnapshot() {
            return Object.freeze({
                frame: profiler.frame,
                passCount: profiler.passCount,
                drawCalls: profiler.drawCalls,
                clears: profiler.clears,
                presents: profiler.presents,
                cpuTimeMs: profiler.cpuTimeMs,
            });
        },
        dispose() {
            deleteFramebufferCache();
            deleteTonemapResources();
            deletePostProcessResources();
            lastFrameCapture = null;
            frameCaptures = [];
        },
        [Symbol.dispose]() {
            deleteFramebufferCache();
            deleteTonemapResources();
            deletePostProcessResources();
            lastFrameCapture = null;
            frameCaptures = [];
        },
    };
};