import type {
    CustomRenderCommand,
    EdgeInsets,
    FontGlyphBitmapFormat,
    GlyphAtlasEntry,
    ImageRenderCommand,
    CornerRadii,
    QuadRenderCommand,
    RectLike,
    StrokeRenderCommand,
    TextGlyphPlacement,
    TextRenderCommand,
    UIFrame,
} from '@axrone/ui/types';
import type { UIFrameSink } from '@axrone/ui/render';
import { DisposedUIError } from '@axrone/ui/errors';
import { Color } from '@axrone/numeric';
import type {
    WebGL2UICustomCommandContext,
    WebGL2UIMaterialImageContext,
    WebGL2UIResolvedImageResource,
    WebGL2UIRendererOptions,
    WebGL2UIRendererStatistics,
    WebGL2UIRenderOptions,
} from './types';

const QUAD_FLOATS_PER_INSTANCE = 23;
const IMAGE_FLOATS_PER_INSTANCE = 22;
const TEXT_FLOATS_PER_INSTANCE = 26;

/** Instance attribute layout: vertex location, component count, float offset. */
interface InstanceAttributeLayout {
    readonly location: number;
    readonly size: number;
    readonly floatOffset: number;
}

const QUAD_INSTANCE_ATTRIBUTES: readonly InstanceAttributeLayout[] = [
    { location: 1, size: 4, floatOffset: 0 },
    { location: 2, size: 4, floatOffset: 4 },
    { location: 3, size: 4, floatOffset: 8 },
    { location: 4, size: 4, floatOffset: 12 },
    { location: 5, size: 1, floatOffset: 16 },
    { location: 6, size: 3, floatOffset: 17 },
    { location: 7, size: 3, floatOffset: 20 },
];

const IMAGE_INSTANCE_ATTRIBUTES: readonly InstanceAttributeLayout[] = [
    { location: 1, size: 4, floatOffset: 0 },
    { location: 2, size: 4, floatOffset: 4 },
    { location: 3, size: 4, floatOffset: 8 },
    { location: 4, size: 4, floatOffset: 12 },
    { location: 5, size: 3, floatOffset: 16 },
    { location: 6, size: 3, floatOffset: 19 },
];

const TEXT_INSTANCE_ATTRIBUTES: readonly InstanceAttributeLayout[] = [
    { location: 1, size: 4, floatOffset: 0 },
    { location: 2, size: 4, floatOffset: 4 },
    { location: 3, size: 4, floatOffset: 8 },
    { location: 4, size: 4, floatOffset: 12 },
    { location: 5, size: 4, floatOffset: 16 },
    { location: 6, size: 3, floatOffset: 20 },
    { location: 7, size: 3, floatOffset: 23 },
];

type SliceSpan = {
    offset: number;
    size: number;
    uvOffset: number;
    uvSize: number;
};

const createSliceSpan = (): SliceSpan => ({ offset: 0, size: 0, uvOffset: 0, uvSize: 0 });

// Scratch spans reused across sliced commands — computed and consumed within a
// single pushSlicedImage call, so no reentrancy hazard exists.
const sliceColumnsScratch: readonly [SliceSpan, SliceSpan, SliceSpan] = [createSliceSpan(), createSliceSpan(), createSliceSpan()];
const sliceRowsScratch: readonly [SliceSpan, SliceSpan, SliceSpan] = [createSliceSpan(), createSliceSpan(), createSliceSpan()];

const resolveSliceSpans = (
    extent: number,
    startBorder: number,
    endBorder: number,
    sourceExtent: number,
    uvOffset: number,
    uvExtent: number,
    out: readonly [SliceSpan, SliceSpan, SliceSpan]
): void => {
    const totalBorder = startBorder + endBorder;
    const scale = totalBorder > extent && totalBorder > 0 ? extent / totalBorder : 1;
    const start = startBorder * scale;
    const end = endBorder * scale;
    const startUv = sourceExtent > 0 ? startBorder / sourceExtent : 0;
    const endUv = sourceExtent > 0 ? endBorder / sourceExtent : 0;
    const first = out[0];
    const middle = out[1];
    const last = out[2];
    first.offset = 0;
    first.size = start;
    first.uvOffset = uvOffset;
    first.uvSize = startUv;
    middle.offset = start;
    middle.size = Math.max(0, extent - start - end);
    middle.uvOffset = uvOffset + startUv;
    middle.uvSize = Math.max(0, uvExtent - startUv - endUv);
    last.offset = extent - end;
    last.size = end;
    last.uvOffset = uvOffset + uvExtent - endUv;
    last.uvSize = endUv;
};

const ZERO_RADII: CornerRadii = Object.freeze({
    topLeft: 0,
    topRight: 0,
    bottomRight: 0,
    bottomLeft: 0,
});

const sliceImageCommand = (
    command: ImageRenderCommand,
    border: EdgeInsets
): readonly ImageRenderCommand[] => {
    resolveSliceSpans(
        command.width,
        border.left,
        border.right,
        Math.max(1, command.source.width),
        command.uvRect.x,
        command.uvRect.width,
        sliceColumnsScratch
    );
    resolveSliceSpans(
        command.height,
        border.top,
        border.bottom,
        Math.max(1, command.source.height),
        command.uvRect.y,
        command.uvRect.height,
        sliceRowsScratch
    );
    const fillCenter = command.fillCenter !== false;
    const slices: ImageRenderCommand[] = [];
    for (let row = 0; row < sliceRowsScratch.length; row += 1) {
        const vertical = sliceRowsScratch[row];
        if (vertical.size <= 0 || vertical.uvSize <= 0) {
            continue;
        }
        for (let column = 0; column < sliceColumnsScratch.length; column += 1) {
            if (row === 1 && column === 1 && !fillCenter) {
                continue;
            }
            const horizontal = sliceColumnsScratch[column];
            if (horizontal.size <= 0 || horizontal.uvSize <= 0) {
                continue;
            }
            slices.push({
                ...command,
                x: command.x + horizontal.offset,
                y: command.y + vertical.offset,
                width: horizontal.size,
                height: vertical.size,
                uvRect: {
                    x: horizontal.uvOffset,
                    y: vertical.uvOffset,
                    width: horizontal.uvSize,
                    height: vertical.uvSize,
                },
                radius: ZERO_RADII,
                border: undefined,
            });
        }
    }
    return slices;
};

const QUAD_VERTEX_SOURCE = `#version 300 es
precision mediump float;
layout(location = 0) in vec2 a_Unit;
layout(location = 1) in vec4 a_Rect;
layout(location = 2) in vec4 a_FillColor;
layout(location = 3) in vec4 a_BorderColor;
layout(location = 4) in vec4 a_Radius;
layout(location = 5) in float a_BorderWidth;
layout(location = 6) in vec3 a_TransformRow0;
layout(location = 7) in vec3 a_TransformRow1;
uniform vec2 u_Viewport;
out vec2 v_Local;
out vec2 v_Size;
out vec4 v_FillColor;
out vec4 v_BorderColor;
out vec4 v_Radius;
out float v_BorderWidth;
void main() {
    vec2 pixel = a_Rect.xy + a_Unit * a_Rect.zw;
    pixel = vec2(
        a_TransformRow0.x * pixel.x + a_TransformRow0.y * pixel.y + a_TransformRow0.z,
        a_TransformRow1.x * pixel.x + a_TransformRow1.y * pixel.y + a_TransformRow1.z
    );
    vec2 ndc = vec2((pixel.x / u_Viewport.x) * 2.0 - 1.0, 1.0 - (pixel.y / u_Viewport.y) * 2.0);
    gl_Position = vec4(ndc, 0.0, 1.0);
    v_Local = a_Unit * a_Rect.zw;
    v_Size = a_Rect.zw;
    v_FillColor = a_FillColor;
    v_BorderColor = a_BorderColor;
    v_Radius = a_Radius;
    v_BorderWidth = a_BorderWidth;
}`;

const QUAD_FRAGMENT_SOURCE = `#version 300 es
precision mediump float;
in vec2 v_Local;
in vec2 v_Size;
in vec4 v_FillColor;
in vec4 v_BorderColor;
in vec4 v_Radius;
in float v_BorderWidth;
out vec4 o_Color;
float selectRadius(vec2 local, vec2 size, vec4 radii) {
    bool left = local.x <= size.x * 0.5;
    bool top = local.y <= size.y * 0.5;
    if (left && top) return radii.x;
    if (!left && top) return radii.y;
    if (!left && !top) return radii.z;
    return radii.w;
}
float roundedRectSdf(vec2 local, vec2 size, vec4 radii) {
    float radius = selectRadius(local, size, radii);
    vec2 center = size * 0.5;
    vec2 halfSize = max(center - vec2(radius), vec2(0.0));
    vec2 delta = abs(local - center) - halfSize;
    return length(max(delta, 0.0)) + min(max(delta.x, delta.y), 0.0) - radius;
}
void main() {
    float outer = roundedRectSdf(v_Local, v_Size, v_Radius);
    float aa = max(fwidth(outer), 0.75);
    float outerAlpha = 1.0 - smoothstep(-aa, aa, outer);
    vec4 color = v_FillColor;
    if (v_BorderWidth > 0.0 && v_BorderColor.a > 0.0) {
        vec2 innerSize = max(v_Size - vec2(v_BorderWidth * 2.0), vec2(0.0));
        vec2 innerLocal = clamp(v_Local - vec2(v_BorderWidth), vec2(0.0), innerSize);
        vec4 innerRadii = max(v_Radius - vec4(v_BorderWidth), vec4(0.0));
        float inner = roundedRectSdf(innerLocal, innerSize, innerRadii);
        float innerAlpha = innerSize.x <= 0.0 || innerSize.y <= 0.0 ? 0.0 : 1.0 - smoothstep(-aa, aa, inner);
        float borderAlpha = max(0.0, outerAlpha - innerAlpha);
        color = mix(v_BorderColor * borderAlpha, v_FillColor * innerAlpha, step(0.0001, innerAlpha));
        color.a = borderAlpha * v_BorderColor.a + innerAlpha * v_FillColor.a;
    } else {
        color *= outerAlpha;
    }
    if (color.a <= 0.0) {
        discard;
    }
    o_Color = color;
}`;

const TEXT_VERTEX_SOURCE = `#version 300 es
precision mediump float;
layout(location = 0) in vec2 a_Unit;
layout(location = 1) in vec4 a_Rect;
layout(location = 2) in vec4 a_UvRect;
layout(location = 3) in vec4 a_Color;
layout(location = 4) in vec4 a_OutlineColor;
layout(location = 5) in vec4 a_SdfParams;
layout(location = 6) in vec3 a_TransformRow0;
layout(location = 7) in vec3 a_TransformRow1;
uniform vec2 u_Viewport;
out vec2 v_Uv;
out vec4 v_Color;
out vec4 v_OutlineColor;
out vec4 v_SdfParams;
void main() {
    vec2 pixel = a_Rect.xy + a_Unit * a_Rect.zw;
    pixel = vec2(
        a_TransformRow0.x * pixel.x + a_TransformRow0.y * pixel.y + a_TransformRow0.z,
        a_TransformRow1.x * pixel.x + a_TransformRow1.y * pixel.y + a_TransformRow1.z
    );
    vec2 ndc = vec2((pixel.x / u_Viewport.x) * 2.0 - 1.0, 1.0 - (pixel.y / u_Viewport.y) * 2.0);
    gl_Position = vec4(ndc, 0.0, 1.0);
    v_Uv = a_UvRect.xy + a_Unit * a_UvRect.zw;
    v_Color = a_Color;
    v_OutlineColor = a_OutlineColor;
    v_SdfParams = a_SdfParams;
}`;

const TEXT_FRAGMENT_SOURCE = `#version 300 es
precision mediump float;
uniform sampler2D u_Atlas;
in vec2 v_Uv;
in vec4 v_Color;
in vec4 v_OutlineColor;
in vec4 v_SdfParams;
out vec4 o_Color;
void main() {
    float alpha = texture(u_Atlas, v_Uv).r;
    vec4 color;
    if (v_SdfParams.x > 0.5) {
        float distanceRange = max(v_SdfParams.y, 1.0);
        float smoothing = max(fwidth(alpha) * max(1.0, v_SdfParams.w) * distanceRange, 0.0001);
        float fillAlpha = smoothstep(0.5 - smoothing, 0.5 + smoothing, alpha);
        float outlineThreshold = 0.5 - (v_SdfParams.z / distanceRange);
        float outlineAlpha = smoothstep(outlineThreshold - smoothing, outlineThreshold + smoothing, alpha);
        vec4 fill = vec4(v_Color.rgb, v_Color.a * fillAlpha);
        vec4 outline = vec4(v_OutlineColor.rgb, v_OutlineColor.a * max(0.0, outlineAlpha - fillAlpha));
        color = fill + outline;
    } else {
        color = vec4(v_Color.rgb, v_Color.a * alpha);
    }
    if (color.a <= 0.0) {
        discard;
    }
    o_Color = color;
}`;

const IMAGE_VERTEX_SOURCE = `#version 300 es
precision mediump float;
layout(location = 0) in vec2 a_Unit;
layout(location = 1) in vec4 a_Rect;
layout(location = 2) in vec4 a_UvRect;
layout(location = 3) in vec4 a_Tint;
layout(location = 4) in vec4 a_Radius;
layout(location = 5) in vec3 a_TransformRow0;
layout(location = 6) in vec3 a_TransformRow1;
uniform vec2 u_Viewport;
out vec2 v_Local;
out vec2 v_Size;
out vec2 v_Uv;
out vec4 v_Tint;
out vec4 v_Radius;
void main() {
    vec2 pixel = a_Rect.xy + a_Unit * a_Rect.zw;
    pixel = vec2(
        a_TransformRow0.x * pixel.x + a_TransformRow0.y * pixel.y + a_TransformRow0.z,
        a_TransformRow1.x * pixel.x + a_TransformRow1.y * pixel.y + a_TransformRow1.z
    );
    vec2 ndc = vec2((pixel.x / u_Viewport.x) * 2.0 - 1.0, 1.0 - (pixel.y / u_Viewport.y) * 2.0);
    gl_Position = vec4(ndc, 0.0, 1.0);
    v_Local = a_Unit * a_Rect.zw;
    v_Size = a_Rect.zw;
    v_Uv = a_UvRect.xy + a_Unit * a_UvRect.zw;
    v_Tint = a_Tint;
    v_Radius = a_Radius;
}`;

const IMAGE_FRAGMENT_SOURCE = `#version 300 es
precision mediump float;
uniform sampler2D u_Image;
in vec2 v_Local;
in vec2 v_Size;
in vec2 v_Uv;
in vec4 v_Tint;
in vec4 v_Radius;
out vec4 o_Color;
float selectRadius(vec2 local, vec2 size, vec4 radii) {
    bool left = local.x <= size.x * 0.5;
    bool top = local.y <= size.y * 0.5;
    if (left && top) return radii.x;
    if (!left && top) return radii.y;
    if (!left && !top) return radii.z;
    return radii.w;
}
float roundedRectSdf(vec2 local, vec2 size, vec4 radii) {
    float radius = selectRadius(local, size, radii);
    vec2 center = size * 0.5;
    vec2 halfSize = max(center - vec2(radius), vec2(0.0));
    vec2 delta = abs(local - center) - halfSize;
    return length(max(delta, 0.0)) + min(max(delta.x, delta.y), 0.0) - radius;
}
void main() {
    float sdf = roundedRectSdf(v_Local, v_Size, v_Radius);
    float aa = max(fwidth(sdf), 0.75);
    float mask = 1.0 - smoothstep(-aa, aa, sdf);
    vec4 color = texture(u_Image, v_Uv) * v_Tint;
    color *= mask;
    if (color.a <= 0.0) {
        discard;
    }
    o_Color = color;
}`;

interface ClipState {
    readonly x: number;
    readonly y: number;
    readonly width: number;
    readonly height: number;
}

interface TexturePage {
    readonly texture: WebGLTexture;
    readonly width: number;
    readonly height: number;
    readonly format: FontGlyphBitmapFormat;
    readonly uploadedGlyphs: Set<number>;
}

/**
 * External GL state groups the renderer may clobber during a frame.
 *
 * Each group is captured lazily — only when (and before) the renderer first
 * writes to it — and restored after the frame only when it was actually
 * touched. This replaces the previous eager capture (~30 getParameter calls,
 * two active-texture round trips, and several tuple allocations per frame)
 * with a zero-allocation fast path that reads at most 15 state values and
 * skips groups the frame never touched entirely.
 */
const GL_STATE_FRAMEBUFFER = 1 << 0;
const GL_STATE_VIEWPORT = 1 << 1;
const GL_STATE_SCISSOR_BOX = 1 << 2;
const GL_STATE_SCISSOR_TEST = 1 << 3;
const GL_STATE_PROGRAM = 1 << 4;
const GL_STATE_VERTEX_ARRAY = 1 << 5;
const GL_STATE_ARRAY_BUFFER = 1 << 6;
const GL_STATE_UNPACK_ALIGNMENT = 1 << 7;
const GL_STATE_CULL_FACE = 1 << 8;
const GL_STATE_DEPTH_TEST = 1 << 9;
const GL_STATE_BLEND = 1 << 10;
const GL_STATE_BLEND_FUNC = 1 << 11;
const GL_STATE_ACTIVE_TEXTURE = 1 << 12;
const GL_STATE_UNIT0_TEXTURE = 1 << 13;
const GL_STATE_UNIT0_SAMPLER = 1 << 14;

interface GLStateShadow {
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

const createGLStateShadow = (): GLStateShadow => ({
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

const UNIT_QUAD = new Float32Array([0, 0, 1, 0, 0, 1, 1, 1]);

const clipKey = (clip: RectLike | null): string =>
    clip === null ? 'none' : `${clip.x}|${clip.y}|${clip.width}|${clip.height}`;

const toClipState = (clip: RectLike | null): ClipState | null =>
    clip === null ? null : { x: clip.x, y: clip.y, width: clip.width, height: clip.height };

const toUint8Array = (value: ArrayBuffer | ArrayBufferView): Uint8Array => {
    if (value instanceof ArrayBuffer) {
        return new Uint8Array(value);
    }
    return new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
};

const createUploadedGlyphKey = (entry: GlyphAtlasEntry): number =>
    (entry.codePoint << 16) | (entry.rasterSize ?? 0);

const createGlyphPageKey = (entry: GlyphAtlasEntry): number =>
    ((entry.faceId as number) << 16) | (entry.page as number);

const multiplyAlpha = (alpha: number, opacity: number): number => alpha * opacity;

const writeBlendedColor = (
    batch: Float32Array,
    offset: number,
    color: QuadRenderCommand['color'] | TextRenderCommand['color'],
    opacity: number
): void => {
    // Runtime-authored commands always carry resolved color objects; the
    // scalar/array branch only serves hand-built commands.
    if (typeof color === 'string' || typeof color === 'number' || Array.isArray(color)) {
        const resolved = normalizeStrokeColor(color);
        batch[offset] = resolved[0];
        batch[offset + 1] = resolved[1];
        batch[offset + 2] = resolved[2];
        batch[offset + 3] = multiplyAlpha(resolved[3], opacity);
        return;
    }
    batch[offset] = color.r;
    batch[offset + 1] = color.g;
    batch[offset + 2] = color.b;
    batch[offset + 3] = multiplyAlpha(color.a ?? 1, opacity);
};

/**
 * Converts a ColorInput (hex string, number, array, or ColorLike) to a
 * normalized [r, g, b, a] tuple for stroke rendering.
 */
const normalizeStrokeColor = (color: StrokeRenderCommand['strokes'][number]['color']): readonly [number, number, number, number] => {
    if (typeof color === 'string') {
        try {
            const c = Color.fromHex(color);
            return [c.r, c.g, c.b, c.a] as const;
        } catch {
            return [1, 1, 1, 1] as const;
        }
    }
    if (typeof color === 'number') {
        return [
            ((color >>> 24) & 0xff) / 255,
            ((color >>> 16) & 0xff) / 255,
            ((color >>> 8) & 0xff) / 255,
            (color & 0xff) / 255,
        ];
    }
    // Object branch first: Array.isArray cannot narrow readonly tuple members
    // out of the union, but the `in` check cleanly separates object from tuple.
    if ('r' in color) {
        return [color.r, color.g, color.b, color.a ?? 1];
    }
    return color.length === 3
        ? [color[0], color[1], color[2], 1]
        : [color[0], color[1], color[2], color[3]];
};

const IDENTITY_TRANSFORM = [1, 0, 0, 1, 0, 0] as const;

const sameClip = (left: ClipState | null, right: RectLike | null): boolean => {
    if (left === null || right === null) {
        return left === null && right === null;
    }
    return (
        left.x === right.x &&
        left.y === right.y &&
        left.width === right.width &&
        left.height === right.height
    );
};

function readGLParameter<TValue>(
    gl: WebGL2RenderingContext,
    parameter: number,
    fallback: undefined
): TValue | undefined;
function readGLParameter<TValue>(
    gl: WebGL2RenderingContext,
    parameter: number,
    fallback: null
): TValue | null;
function readGLParameter<TValue>(
    gl: WebGL2RenderingContext,
    parameter: number,
    fallback: TValue
): TValue;
function readGLParameter<TValue>(
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

const readGLEnabled = (
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

const restoreGLEnableState = (
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


/**
 * Restores newline separators in shader source that has been flattened
 * by build-time minification (esbuild template literal transformation).
 *
 * When newlines are stripped, GLSL tokens merge across former line boundaries:
 *   #version 300 esprecision mediump float;   (es + precision)
 *   float;layout(location = 0) in vec2 ...    (float; + layout)
 *
 * This function detects and repairs such corruption by re-inserting
 * newlines at statement boundaries. Uses split/join instead of regex
 * to avoid esbuild transformation issues with replacement strings.
 */
const normalizeShaderSource = (source: string): string => {
    // Fast path: if the first line is a valid #version directive, source is intact.
    // Uses a strict regex to reject corrupted lines like '#version 300 esprecision ...'
    // where esbuild merged tokens across a stripped newline boundary.
    const firstNewline = source.indexOf('\n');
    if (firstNewline > 0 && /^#version\s+\d+\s+\w+$/.test(source.slice(0, firstNewline).trim())) {
        return source;
    }

    // Corruption detected — rebuild newlines at GLSL statement boundaries.
    // Uses split/join instead of regex to avoid esbuild transformation issues.
    const parts: string[] = [];
    for (const segment of source.split(/([;{}])/)) {
        if (segment === ';') {
            parts.push(';\n');
        } else if (segment === '{') {
            parts.push('{\n');
        } else if (segment === '}') {
            parts.push('\n}\n');
        } else {
            parts.push(segment);
        }
    }
    let restored = parts.join('');

    // Fix #version directive: ensure newline after 'es' before next token.
    const versionEnd = restored.indexOf('es') + 2;
    if (versionEnd > 2 && versionEnd < restored.length) {
        const after = restored[versionEnd];
        if (after !== '\n' && after !== ' ' && after !== '\t' && after !== '\r') {
            restored = restored.slice(0, versionEnd) + '\n' + restored.slice(versionEnd);
        }
    }

    return restored;
};

const createShader = (gl: WebGL2RenderingContext, type: number, source: string): WebGLShader => {
    const shader = gl.createShader(type);
    if (!shader) {
        throw new Error('Failed to create UI shader.');
    }
    const resolvedSource = normalizeShaderSource(source);
    if (resolvedSource !== source) {
        // eslint-disable-next-line no-console
        console.warn('[WebGL2UIRenderer] Shader source was corrupted — newlines were restored. This indicates esbuild minification stripped newlines from the shader template literal.');
    }
    gl.shaderSource(shader, resolvedSource);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const message = gl.getShaderInfoLog(shader) ?? 'Unknown shader compile failure.';
        gl.deleteShader(shader);
        const stageLabel = type === gl.VERTEX_SHADER ? 'vertex' : 'fragment';
        throw new Error(
            `UI ${stageLabel} shader compilation failed: ${message}\nSource (first 200 chars): ${resolvedSource.slice(0, 200)}`
        );
    }
    return shader;
};

const createProgram = (
    gl: WebGL2RenderingContext,
    vertexSource: string,
    fragmentSource: string
): WebGLProgram => {
    const program = gl.createProgram();
    if (!program) {
        throw new Error('Failed to create UI program.');
    }
    const vertex = createShader(gl, gl.VERTEX_SHADER, vertexSource);
    const fragment = createShader(gl, gl.FRAGMENT_SHADER, fragmentSource);
    gl.attachShader(program, vertex);
    gl.attachShader(program, fragment);
    gl.linkProgram(program);
    gl.deleteShader(vertex);
    gl.deleteShader(fragment);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        const message = gl.getProgramInfoLog(program) ?? 'Unknown program link failure.';
        gl.deleteProgram(program);
        throw new Error(message);
    }
    return program;
};

export class WebGL2UIRenderer<TPayload = unknown> implements UIFrameSink<TPayload> {
    private readonly gl: WebGL2RenderingContext;
    // GPU-dependent handles are mutable: they are re-created wholesale after a
    // WebGL context loss/restore cycle (see createGpuResources). Assigned via
    // createGpuResources() in the constructor, hence the definite assignment.
    private quadProgram!: WebGLProgram;
    private imageProgram!: WebGLProgram;
    private textProgram!: WebGLProgram;
    private quadViewportUniform!: WebGLUniformLocation | null;
    private imageViewportUniform!: WebGLUniformLocation | null;
    private imageTextureUniform!: WebGLUniformLocation | null;
    private textViewportUniform!: WebGLUniformLocation | null;
    private textAtlasUniform!: WebGLUniformLocation | null;
    private quadVao!: WebGLVertexArrayObject | null;
    private imageVao!: WebGLVertexArrayObject | null;
    private textVao!: WebGLVertexArrayObject | null;
    private quadStaticBuffer!: WebGLBuffer | null;
    private quadInstanceBuffer!: WebGLBuffer | null;
    private imageStaticBuffer!: WebGLBuffer | null;
    private imageInstanceBuffer!: WebGLBuffer | null;
    private textStaticBuffer!: WebGLBuffer | null;
    private textInstanceBuffer!: WebGLBuffer | null;
    private readonly pages = new Map<number, TexturePage>();
    private readonly quadBatch: Float32Array;
    private readonly imageBatch: Float32Array;
    private readonly textBatch: Float32Array;
    private readonly resolveImageResource?: WebGL2UIRendererOptions<TPayload>['resolveImageResource'];
    private readonly customCommandRenderer?: WebGL2UIRendererOptions<TPayload>['customCommandRenderer'];
    private readonly atlasFilter: 'nearest' | 'linear';
    private readonly statisticsState = {
        drawCalls: 0,
        quadCount: 0,
        imageCount: 0,
        materialImageCount: 0,
        glyphCount: 0,
        customCommandCount: 0,
        uploadedGlyphCount: 0,
    };
    private quadCount = 0;
    private imageCount = 0;
    private textCount = 0;
    private activeImageTexture: WebGLTexture | null = null;
    private activeImageSampler: WebGLSampler | null = null;
    private activeTextPageKey: number | null = null;
    private activeQuadClip: ClipState | null = null;
    private activeImageClip: ClipState | null = null;
    private activeTextClip: ClipState | null = null;
    private currentFrame: UIFrame<TPayload> | null = null;
    private disposed = false;
    private contextLost = false;
    private readonly glShadow: GLStateShadow = createGLStateShadow();
    private glCapturedGroups = 0;
    private glTouchedGroups = 0;
    private currentGLTextureUnit = -1;

    constructor(options: WebGL2UIRendererOptions<TPayload>) {
        this.gl = options.gl;
        this.resolveImageResource = options.resolveImageResource;
        this.customCommandRenderer = options.customCommandRenderer;
        this.atlasFilter = options.atlasFilter ?? 'linear';
        this.quadBatch = new Float32Array((options.quadBatchCapacity ?? 1024) * QUAD_FLOATS_PER_INSTANCE);
        this.imageBatch = new Float32Array((options.imageBatchCapacity ?? 1024) * IMAGE_FLOATS_PER_INSTANCE);
        this.textBatch = new Float32Array((options.glyphBatchCapacity ?? 4096) * TEXT_FLOATS_PER_INSTANCE);
        this.createGpuResources();
        this.attachContextLossHandlers();
    }

    /**
     * Creates every GPU-dependent object: programs, uniform locations, static
     * and instance buffers, VAOs and their vertex layouts. Called by the
     * constructor and again after a webglcontextrestored event. CPU-side batch
     * storage intentionally lives outside so it survives context loss.
     */
    private createGpuResources(): void {
        const gl = this.gl;
        this.quadProgram = createProgram(gl, QUAD_VERTEX_SOURCE, QUAD_FRAGMENT_SOURCE);
        this.imageProgram = createProgram(gl, IMAGE_VERTEX_SOURCE, IMAGE_FRAGMENT_SOURCE);
        this.textProgram = createProgram(gl, TEXT_VERTEX_SOURCE, TEXT_FRAGMENT_SOURCE);
        this.quadViewportUniform = gl.getUniformLocation(this.quadProgram, 'u_Viewport');
        this.imageViewportUniform = gl.getUniformLocation(this.imageProgram, 'u_Viewport');
        this.imageTextureUniform = gl.getUniformLocation(this.imageProgram, 'u_Image');
        this.textViewportUniform = gl.getUniformLocation(this.textProgram, 'u_Viewport');
        this.textAtlasUniform = gl.getUniformLocation(this.textProgram, 'u_Atlas');
        this.quadStaticBuffer = gl.createBuffer();
        this.quadInstanceBuffer = gl.createBuffer();
        this.imageStaticBuffer = gl.createBuffer();
        this.imageInstanceBuffer = gl.createBuffer();
        this.textStaticBuffer = gl.createBuffer();
        this.textInstanceBuffer = gl.createBuffer();
        this.quadVao = gl.createVertexArray();
        this.imageVao = gl.createVertexArray();
        this.textVao = gl.createVertexArray();
        this.initializeQuadPipeline();
        this.initializeImagePipeline();
        this.initializeTextPipeline();
    }

    private readonly handleContextLost = (event: Event): void => {
        // Preventing default keeps the context alive for a potential restore.
        event.preventDefault();
        this.contextLost = true;
        // Every GPU handle — including glyph page textures — is now invalid.
        // Dropping the pages forces re-upload after restore instead of reusing
        // stale texture handles.
        this.pages.clear();
    };

    private readonly handleContextRestored = (): void => {
        try {
            this.createGpuResources();
            this.contextLost = false;
        } catch (error) {
            // A failed restore must not throw inside the browser event handler;
            // rendering stays disabled until a successful recreation.
            this.contextLost = true;
            // eslint-disable-next-line no-console
            console.error('[WebGL2UIRenderer] Failed to restore GPU resources after context loss.', error);
        }
    };

    private attachContextLossHandlers(): void {
        const canvas = this.gl.canvas as HTMLCanvasElement | undefined;
        if (canvas && typeof canvas.addEventListener === 'function') {
            canvas.addEventListener('webglcontextlost', this.handleContextLost);
            canvas.addEventListener('webglcontextrestored', this.handleContextRestored);
        }
    }

    private detachContextLossHandlers(): void {
        const canvas = this.gl.canvas as HTMLCanvasElement | undefined;
        if (canvas && typeof canvas.removeEventListener === 'function') {
            canvas.removeEventListener('webglcontextlost', this.handleContextLost);
            canvas.removeEventListener('webglcontextrestored', this.handleContextRestored);
        }
    }

    getStats(): WebGL2UIRendererStatistics {
        return {
            drawCalls: this.statisticsState.drawCalls,
            quadCount: this.statisticsState.quadCount,
            imageCount: this.statisticsState.imageCount,
            materialImageCount: this.statisticsState.materialImageCount,
            glyphCount: this.statisticsState.glyphCount,
            customCommandCount: this.statisticsState.customCommandCount,
            uploadedGlyphCount: this.statisticsState.uploadedGlyphCount,
            atlasPageCount: this.pages.size,
        };
    }

    render(frame: Readonly<UIFrame<TPayload>>, options?: WebGL2UIRenderOptions): void {
        this.ensureActive();
        if (this.contextLost) {
            // GPU resources are invalid; skip the frame until the context is
            // restored and resources are recreated.
            return;
        }
        this.currentFrame = frame as UIFrame<TPayload>;
        this.statisticsState.drawCalls = 0;
        this.statisticsState.quadCount = 0;
        this.statisticsState.imageCount = 0;
        this.statisticsState.materialImageCount = 0;
        this.statisticsState.glyphCount = 0;
        this.statisticsState.customCommandCount = 0;
        this.statisticsState.uploadedGlyphCount = 0;
        this.quadCount = 0;
        this.imageCount = 0;
        this.textCount = 0;
        this.activeQuadClip = null;
        this.activeImageClip = null;
        this.activeImageSampler = null;
        this.activeTextClip = null;
        this.activeImageTexture = null;
        this.activeTextPageKey = null;

        try {
            if (options && 'framebuffer' in options) {
                // Offscreen target (world-space UI). The previously bound
                // framebuffer is captured lazily here and restored on the way
                // out by restoreGLState().
                this.captureGLState(GL_STATE_FRAMEBUFFER);
                this.glTouchedGroups |= GL_STATE_FRAMEBUFFER;
                this.gl.bindFramebuffer(this.gl.FRAMEBUFFER, options.framebuffer ?? null);
            }
            this.prepareFrame(frame.viewportWidth, frame.viewportHeight);

            for (const command of frame.commands) {
                if (command.kind === 'quad') {
                    if (!sameClip(this.activeQuadClip, command.clip)) {
                        this.flushQuadBatch(frame.viewportHeight);
                        this.activeQuadClip = toClipState(command.clip);
                    }
                    this.pushQuad(command, frame.viewportHeight);
                    continue;
                }
                if (command.kind === 'image') {
                    this.pushImageCommand(command, frame);
                    continue;
                }
                if (command.kind === 'text') {
                    this.pushTextCommand(command, frame.viewportHeight);
                    continue;
                }
                if (command.kind === 'stroke') {
                    if (!this.activeQuadClip || !sameClip(this.activeQuadClip, command.clip)) {
                        this.flushQuadBatch(frame.viewportHeight);
                        this.activeQuadClip = toClipState(command.clip);
                    }
                    this.pushStrokeCommand(command, frame.viewportHeight);
                    continue;
                }
                this.flushQuadBatch(frame.viewportHeight);
                this.flushImageBatch(frame.viewportHeight);
                this.flushTextBatch(frame.viewportHeight);
                if (this.customCommandRenderer) {
                    this.statisticsState.customCommandCount += 1;
                    this.customCommandRenderer(command as CustomRenderCommand<TPayload>, {
                        gl: this.gl,
                        frame,
                        clip: command.clip,
                        viewport: {
                            width: frame.viewportWidth,
                            height: frame.viewportHeight,
                        },
                    });
                    // Custom renderers may rebind the active texture unit;
                    // force re-issue on the next unit-sensitive call.
                    this.currentGLTextureUnit = -1;
                }
            }

            this.flushQuadBatch(frame.viewportHeight);
            this.flushImageBatch(frame.viewportHeight);
            this.flushTextBatch(frame.viewportHeight);
        } finally {
            this.currentFrame = null;
            this.restoreGLState();
        }
    }

    dispose(): void {
        if (this.disposed) {
            return;
        }
        this.detachContextLossHandlers();
        for (const page of this.pages.values()) {
            this.gl.deleteTexture(page.texture);
        }
        this.pages.clear();
        this.gl.deleteBuffer(this.quadStaticBuffer);
        this.gl.deleteBuffer(this.quadInstanceBuffer);
        this.gl.deleteBuffer(this.imageStaticBuffer);
        this.gl.deleteBuffer(this.imageInstanceBuffer);
        this.gl.deleteBuffer(this.textStaticBuffer);
        this.gl.deleteBuffer(this.textInstanceBuffer);
        this.gl.deleteVertexArray(this.quadVao);
        this.gl.deleteVertexArray(this.imageVao);
        this.gl.deleteVertexArray(this.textVao);
        this.gl.deleteProgram(this.quadProgram);
        this.gl.deleteProgram(this.imageProgram);
        this.gl.deleteProgram(this.textProgram);
        this.disposed = true;
    }

    [Symbol.dispose](): void {
        this.dispose();
    }

    /**
     * Shared VAO/buffer setup for the quad/image/text pipelines. Each pipeline
     * differs only by its instance stride and attribute table.
     */
    private initializeInstancePipeline(
        vao: WebGLVertexArrayObject | null,
        staticBuffer: WebGLBuffer | null,
        instanceBuffer: WebGLBuffer | null,
        batchByteLength: number,
        instanceStrideBytes: number,
        attributes: readonly InstanceAttributeLayout[]
    ): void {
        const gl = this.gl;
        gl.bindVertexArray(vao);
        gl.bindBuffer(gl.ARRAY_BUFFER, staticBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, UNIT_QUAD, gl.STATIC_DRAW);
        gl.enableVertexAttribArray(0);
        gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 8, 0);
        gl.bindBuffer(gl.ARRAY_BUFFER, instanceBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, batchByteLength, gl.DYNAMIC_DRAW);
        for (const attribute of attributes) {
            gl.enableVertexAttribArray(attribute.location);
            gl.vertexAttribPointer(
                attribute.location,
                attribute.size,
                gl.FLOAT,
                false,
                instanceStrideBytes,
                attribute.floatOffset * 4
            );
            gl.vertexAttribDivisor(attribute.location, 1);
        }
        gl.bindVertexArray(null);
    }

    private initializeQuadPipeline(): void {
        this.initializeInstancePipeline(
            this.quadVao,
            this.quadStaticBuffer,
            this.quadInstanceBuffer,
            this.quadBatch.byteLength,
            QUAD_FLOATS_PER_INSTANCE * 4,
            QUAD_INSTANCE_ATTRIBUTES
        );
    }

    private initializeImagePipeline(): void {
        this.initializeInstancePipeline(
            this.imageVao,
            this.imageStaticBuffer,
            this.imageInstanceBuffer,
            this.imageBatch.byteLength,
            IMAGE_FLOATS_PER_INSTANCE * 4,
            IMAGE_INSTANCE_ATTRIBUTES
        );
    }

    private initializeTextPipeline(): void {
        this.initializeInstancePipeline(
            this.textVao,
            this.textStaticBuffer,
            this.textInstanceBuffer,
            this.textBatch.byteLength,
            TEXT_FLOATS_PER_INSTANCE * 4,
            TEXT_INSTANCE_ATTRIBUTES
        );
    }

    private prepareFrame(width: number, height: number): void {
        // Pin all rendering to texture unit 0 so glyph uploads and texture
        // binds never clobber the external context's active-unit bindings.
        this.captureGLState(GL_STATE_ACTIVE_TEXTURE);
        if (this.glShadow.activeTexture !== this.gl.TEXTURE0) {
            this.gl.activeTexture(this.gl.TEXTURE0);
            this.glTouchedGroups |= GL_STATE_ACTIVE_TEXTURE;
        }
        this.currentGLTextureUnit = this.gl.TEXTURE0;
        this.captureGLState(GL_STATE_VIEWPORT);
        this.glTouchedGroups |= GL_STATE_VIEWPORT;
        this.gl.viewport(0, 0, width, height);
        this.captureGLState(GL_STATE_CULL_FACE);
        this.glTouchedGroups |= GL_STATE_CULL_FACE;
        this.gl.disable(this.gl.CULL_FACE);
        this.captureGLState(GL_STATE_DEPTH_TEST);
        this.glTouchedGroups |= GL_STATE_DEPTH_TEST;
        this.gl.disable(this.gl.DEPTH_TEST);
        this.captureGLState(GL_STATE_BLEND);
        this.glTouchedGroups |= GL_STATE_BLEND;
        this.gl.enable(this.gl.BLEND);
        this.captureGLState(GL_STATE_BLEND_FUNC);
        this.glTouchedGroups |= GL_STATE_BLEND_FUNC;
        this.gl.blendFunc(this.gl.SRC_ALPHA, this.gl.ONE_MINUS_SRC_ALPHA);
    }

    private pushQuad(command: QuadRenderCommand, viewportHeight: number): void {
        let base = this.quadCount * QUAD_FLOATS_PER_INSTANCE;
        if (base + QUAD_FLOATS_PER_INSTANCE > this.quadBatch.length) {
            this.flushQuadBatch(viewportHeight);
            base = 0;
        }
        this.quadBatch[base] = command.x;
        this.quadBatch[base + 1] = command.y;
        this.quadBatch[base + 2] = command.width;
        this.quadBatch[base + 3] = command.height;
        writeBlendedColor(this.quadBatch, base + 4, command.color, command.opacity);
        writeBlendedColor(this.quadBatch, base + 8, command.borderColor, command.opacity);
        this.quadBatch[base + 12] = command.radius.topLeft;
        this.quadBatch[base + 13] = command.radius.topRight;
        this.quadBatch[base + 14] = command.radius.bottomRight;
        this.quadBatch[base + 15] = command.radius.bottomLeft;
        this.quadBatch[base + 16] = command.borderWidth;
        const transform = command.transform ?? IDENTITY_TRANSFORM;
        this.quadBatch[base + 17] = transform[0];
        this.quadBatch[base + 18] = transform[1];
        this.quadBatch[base + 19] = transform[4];
        this.quadBatch[base + 20] = transform[2];
        this.quadBatch[base + 21] = transform[3];
        this.quadBatch[base + 22] = transform[5];
        this.quadCount += 1;
        this.statisticsState.quadCount += 1;
    }

    /**
     * Converts each stroke segment into an oriented strip rendered through the
     * existing quad pipeline. Normalized 0–1 points are mapped into the widget
     * rect in pixels, the strip is emitted at its real pixel size so the
     * rounded-rect SDF and its antialiasing stay in pixel units, and the command
     * transform is composed on top of the segment transform so position and
     * thickness scale together with the camera.
     */
    private pushStrokeCommand(command: StrokeRenderCommand, viewportHeight: number): void {
        const widgetX = command.x;
        const widgetY = command.y;
        const widgetW = command.width;
        const widgetH = command.height;
        const camera = command.transform ?? IDENTITY_TRANSFORM;
        for (const stroke of command.strokes) {
            const color = normalizeStrokeColor(stroke.color);
            const weight = Math.max(0.5, stroke.weight);
            const halfWeight = weight * 0.5;
            const points = stroke.points;
            for (let i = 0; i < points.length - 1; i++) {
                const p0 = points[i];
                const p1 = points[i + 1];
                // Map normalized points to widget pixel space.
                const x0 = widgetX + p0[0] * widgetW;
                const y0 = widgetY + p0[1] * widgetH;
                const x1 = widgetX + p1[0] * widgetW;
                const y1 = widgetY + p1[1] * widgetH;
                const dx = x1 - x0;
                const dy = y1 - y0;
                const segLen = Math.sqrt(dx * dx + dy * dy);
                if (segLen < 0.001) continue;
                const dirX = dx / segLen;
                const dirY = dy / segLen;
                const nx = -dirY;
                const ny = dirX;
                // Extend the strip by half the weight at both ends so adjacent
                // segments overlap at their joint instead of leaving a notch.
                const extent = segLen + weight;
                // Pure rotation + translation: the instance rect already carries the
                // strip's own pixel extents (a_Rect.zw), and the shader feeds
                // a_Rect.xy + a_Unit * a_Rect.zw through this transform. Folding
                // extent/weight in here as well would scale them a second time.
                const s0 = dirX;
                const s2 = dirY;
                const s1 = nx;
                const s3 = ny;
                const s4 = x0 - dirX * halfWeight - nx * halfWeight;
                const s5 = y0 - dirY * halfWeight - ny * halfWeight;
                // Compose the camera on top of the segment transform.
                const r0 = camera[0] * s0 + camera[1] * s2;
                const r1 = camera[0] * s1 + camera[1] * s3;
                const r2 = camera[2] * s0 + camera[3] * s2;
                const r3 = camera[2] * s1 + camera[3] * s3;
                const r4 = camera[0] * s4 + camera[1] * s5 + camera[4];
                const r5 = camera[2] * s4 + camera[3] * s5 + camera[5];
                const base = this.quadCount * QUAD_FLOATS_PER_INSTANCE;
                if (base + QUAD_FLOATS_PER_INSTANCE > this.quadBatch.length) {
                    this.flushQuadBatch(viewportHeight);
                    continue;
                }
                // Local rect carries the strip's pixel size; the transform places it.
                this.quadBatch[base] = 0;
                this.quadBatch[base + 1] = 0;
                this.quadBatch[base + 2] = extent;
                this.quadBatch[base + 3] = weight;
                this.quadBatch[base + 4] = color[0];
                this.quadBatch[base + 5] = color[1];
                this.quadBatch[base + 6] = color[2];
                this.quadBatch[base + 7] = color[3] * command.opacity;
                // No border, no radius, no border width.
                this.quadBatch[base + 8] = 0;
                this.quadBatch[base + 9] = 0;
                this.quadBatch[base + 10] = 0;
                this.quadBatch[base + 11] = 0;
                this.quadBatch[base + 12] = 0;
                this.quadBatch[base + 13] = 0;
                this.quadBatch[base + 14] = 0;
                this.quadBatch[base + 15] = 0;
                this.quadBatch[base + 16] = 0;
                this.quadBatch[base + 17] = r0;
                this.quadBatch[base + 18] = r1;
                this.quadBatch[base + 19] = r4;
                this.quadBatch[base + 20] = r2;
                this.quadBatch[base + 21] = r3;
                this.quadBatch[base + 22] = r5;
                this.quadCount += 1;
                this.statisticsState.quadCount += 1;
            }
        }
    }

    private pushTextCommand(command: TextRenderCommand, viewportHeight: number): void {
        for (const glyph of command.layout.glyphs) {
            if (!this.pushGlyph(command, glyph, viewportHeight)) {
                this.flushTextBatch(viewportHeight);
                if (!this.pushGlyph(command, glyph, viewportHeight)) {
                    throw new Error('Glyph batch capacity exceeded.');
                }
            }
        }
    }

    private pushImageCommand(command: ImageRenderCommand, frame: Readonly<UIFrame<TPayload>>): void {
        const border = command.border;
        if (
            border &&
            (border.left > 0 || border.top > 0 || border.right > 0 || border.bottom > 0)
        ) {
            this.pushSlicedImage(command, border, frame);
            return;
        }
        this.pushImageQuad(command, frame);
    }

    private pushImageQuad(command: ImageRenderCommand, frame: Readonly<UIFrame<TPayload>>): void {
        const resource = this.resolveImageResource?.(command.source, {
            gl: this.gl,
            frame,
            command,
        });
        if (!resource) {
            return;
        }
        if (resource.kind === 'material') {
            this.statisticsState.imageCount += 1;
            this.flushImageBatch(frame.viewportHeight);
            this.statisticsState.materialImageCount += 1;
            this.applyClip(toClipState(command.clip), frame.viewportHeight);
            resource.render({
                gl: this.gl,
                frame,
                command,
                clip: command.clip,
                viewport: { width: frame.viewportWidth, height: frame.viewportHeight },
            } satisfies WebGL2UIMaterialImageContext<TPayload>);
            return;
        }
        this.pushImageInstance(
            command,
            resource.texture,
            resource.sampler ?? null,
            toClipState(command.clip),
            frame,
            command.x,
            command.y,
            command.width,
            command.height,
            command.uvRect.x,
            command.uvRect.y,
            command.uvRect.width,
            command.uvRect.height
        );
    }

    /**
     * Expands a nine-slice image directly into the image instance batch: the
     * resource resolves once and each emitted cell writes raw instance floats.
     * This avoids the nine spread-copied commands, two span arrays and nine
     * uv rect objects the slice-command path allocated per panel per frame.
     * Material-backed sources keep the slice-command path because material
     * renderers consume full commands.
     */
    private pushSlicedImage(command: ImageRenderCommand, border: EdgeInsets, frame: Readonly<UIFrame<TPayload>>): void {
        const resource = this.resolveImageResource?.(command.source, {
            gl: this.gl,
            frame,
            command,
        });
        if (!resource) {
            return;
        }
        if (resource.kind === 'material') {
            for (const slice of sliceImageCommand(command, border)) {
                this.pushImageQuad(slice, frame);
            }
            return;
        }
        resolveSliceSpans(
            command.width,
            border.left,
            border.right,
            Math.max(1, command.source.width),
            command.uvRect.x,
            command.uvRect.width,
            sliceColumnsScratch
        );
        resolveSliceSpans(
            command.height,
            border.top,
            border.bottom,
            Math.max(1, command.source.height),
            command.uvRect.y,
            command.uvRect.height,
            sliceRowsScratch
        );
        const fillCenter = command.fillCenter !== false;
        const sampler = resource.sampler ?? null;
        const clip = toClipState(command.clip);
        for (let row = 0; row < sliceRowsScratch.length; row += 1) {
            const vertical = sliceRowsScratch[row];
            if (vertical.size <= 0 || vertical.uvSize <= 0) {
                continue;
            }
            for (let column = 0; column < sliceColumnsScratch.length; column += 1) {
                if (row === 1 && column === 1 && !fillCenter) {
                    continue;
                }
                const horizontal = sliceColumnsScratch[column];
                if (horizontal.size <= 0 || horizontal.uvSize <= 0) {
                    continue;
                }
                this.pushImageInstance(
                    command,
                    resource.texture,
                    sampler,
                    clip,
                    frame,
                    command.x + horizontal.offset,
                    command.y + vertical.offset,
                    horizontal.size,
                    vertical.size,
                    horizontal.uvOffset,
                    vertical.uvOffset,
                    horizontal.uvSize,
                    vertical.uvSize
                );
            }
        }
    }

    private pushImageInstance(
        command: ImageRenderCommand,
        texture: WebGLTexture,
        sampler: WebGLSampler | null,
        clip: ClipState | null,
        frame: Readonly<UIFrame<TPayload>>,
        x: number,
        y: number,
        width: number,
        height: number,
        uvX: number,
        uvY: number,
        uvWidth: number,
        uvHeight: number
    ): void {
        if (
            (this.activeImageTexture !== null && this.activeImageTexture !== texture) ||
            (this.activeImageSampler !== sampler) ||
            (this.activeImageClip !== null && !sameClip(this.activeImageClip, command.clip))
        ) {
            this.flushImageBatch(frame.viewportHeight);
        }
        let base = this.imageCount * IMAGE_FLOATS_PER_INSTANCE;
        if (base + IMAGE_FLOATS_PER_INSTANCE > this.imageBatch.length) {
            this.flushImageBatch(frame.viewportHeight);
            base = 0;
        }
        this.activeImageTexture = texture;
        this.activeImageSampler = sampler;
        this.activeImageClip = clip;
        this.imageBatch[base] = x;
        this.imageBatch[base + 1] = y;
        this.imageBatch[base + 2] = width;
        this.imageBatch[base + 3] = height;
        this.imageBatch[base + 4] = uvX;
        this.imageBatch[base + 5] = uvY;
        this.imageBatch[base + 6] = uvWidth;
        this.imageBatch[base + 7] = uvHeight;
        writeBlendedColor(this.imageBatch, base + 8, command.tint, command.opacity);
        this.imageBatch[base + 12] = command.radius.topLeft;
        this.imageBatch[base + 13] = command.radius.topRight;
        this.imageBatch[base + 14] = command.radius.bottomRight;
        this.imageBatch[base + 15] = command.radius.bottomLeft;
        const transform = command.transform ?? IDENTITY_TRANSFORM;
        this.imageBatch[base + 16] = transform[0];
        this.imageBatch[base + 17] = transform[1];
        this.imageBatch[base + 18] = transform[4];
        this.imageBatch[base + 19] = transform[2];
        this.imageBatch[base + 20] = transform[3];
        this.imageBatch[base + 21] = transform[5];
        this.imageCount += 1;
        this.statisticsState.imageCount += 1;
    }

    private pushGlyph(
        command: TextRenderCommand,
        glyph: TextGlyphPlacement,
        viewportHeight: number
    ): boolean {
        const entry = glyph.atlasEntry;
        if (!entry) {
            return true;
        }
        const pageKey = createGlyphPageKey(entry);
        if (this.activeTextPageKey !== null && this.activeTextPageKey !== pageKey) {
            return false;
        }
        if (this.activeTextClip !== null && !sameClip(this.activeTextClip, command.clip)) {
            return false;
        }
        const page = this.ensureGlyphPage(entry);
        if (page === null) {
            return true;
        }
        const base = this.textCount * TEXT_FLOATS_PER_INSTANCE;
        if (base + TEXT_FLOATS_PER_INSTANCE > this.textBatch.length) {
            return false;
        }
        this.activeTextPageKey = pageKey;
        this.activeTextClip = toClipState(command.clip);
        this.textBatch[base] = command.x + glyph.x;
        this.textBatch[base + 1] = command.y + glyph.y;
        this.textBatch[base + 2] = glyph.width;
        this.textBatch[base + 3] = glyph.height;
        this.textBatch[base + 4] = entry.u0;
        this.textBatch[base + 5] = entry.v0;
        this.textBatch[base + 6] = entry.u1 - entry.u0;
        this.textBatch[base + 7] = entry.v1 - entry.v0;
        writeBlendedColor(this.textBatch, base + 8, command.color, command.opacity);
        writeBlendedColor(this.textBatch, base + 12, command.outlineColor, command.opacity);
        this.textBatch[base + 16] = entry.format === 'sdf8' ? 1 : 0;
        this.textBatch[base + 17] = entry.distanceRange;
        this.textBatch[base + 18] = command.outlineWidth;
        this.textBatch[base + 19] = command.edgeSoftness;
        const transform = command.transform ?? IDENTITY_TRANSFORM;
        this.textBatch[base + 20] = transform[0];
        this.textBatch[base + 21] = transform[1];
        this.textBatch[base + 22] = transform[4];
        this.textBatch[base + 23] = transform[2];
        this.textBatch[base + 24] = transform[3];
        this.textBatch[base + 25] = transform[5];
        this.textCount += 1;
        this.statisticsState.glyphCount += 1;
        void page;
        void viewportHeight;
        return true;
    }

    private flushQuadBatch(viewportHeight: number): void {
        if (this.quadCount === 0 || !this.currentFrame) {
            return;
        }
        this.applyClip(this.activeQuadClip, viewportHeight);
        this.captureGLState(GL_STATE_PROGRAM);
        this.glTouchedGroups |= GL_STATE_PROGRAM;
        this.gl.useProgram(this.quadProgram);
        this.gl.uniform2f(this.quadViewportUniform, this.currentFrame.viewportWidth, this.currentFrame.viewportHeight);
        this.captureGLState(GL_STATE_VERTEX_ARRAY | GL_STATE_ARRAY_BUFFER);
        this.glTouchedGroups |= GL_STATE_VERTEX_ARRAY | GL_STATE_ARRAY_BUFFER;
        this.gl.bindVertexArray(this.quadVao);
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.quadInstanceBuffer);
        this.gl.bufferSubData(
            this.gl.ARRAY_BUFFER,
            0,
            this.quadBatch.subarray(0, this.quadCount * QUAD_FLOATS_PER_INSTANCE)
        );
        this.gl.drawArraysInstanced(this.gl.TRIANGLE_STRIP, 0, 4, this.quadCount);
        this.gl.bindVertexArray(null);
        this.statisticsState.drawCalls += 1;
        this.quadCount = 0;
    }

    private flushImageBatch(viewportHeight: number): void {
        if (this.imageCount === 0 || !this.currentFrame || !this.activeImageTexture) {
            this.imageCount = 0;
            this.activeImageTexture = null;
            return;
        }
        this.applyClip(this.activeImageClip, viewportHeight);
        this.captureGLState(GL_STATE_PROGRAM);
        this.glTouchedGroups |= GL_STATE_PROGRAM;
        this.gl.useProgram(this.imageProgram);
        this.gl.uniform2f(this.imageViewportUniform, this.currentFrame.viewportWidth, this.currentFrame.viewportHeight);
        this.bindUnit0Texture(this.activeImageTexture);
        this.captureGLState(GL_STATE_UNIT0_SAMPLER);
        this.glTouchedGroups |= GL_STATE_UNIT0_SAMPLER;
        this.gl.bindSampler?.(0, this.activeImageSampler);
        this.gl.uniform1i(this.imageTextureUniform, 0);
        this.captureGLState(GL_STATE_VERTEX_ARRAY | GL_STATE_ARRAY_BUFFER);
        this.glTouchedGroups |= GL_STATE_VERTEX_ARRAY | GL_STATE_ARRAY_BUFFER;
        this.gl.bindVertexArray(this.imageVao);
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.imageInstanceBuffer);
        this.gl.bufferSubData(
            this.gl.ARRAY_BUFFER,
            0,
            this.imageBatch.subarray(0, this.imageCount * IMAGE_FLOATS_PER_INSTANCE)
        );
        this.gl.drawArraysInstanced(this.gl.TRIANGLE_STRIP, 0, 4, this.imageCount);
        this.gl.bindVertexArray(null);
        this.statisticsState.drawCalls += 1;
        this.imageCount = 0;
        this.activeImageTexture = null;
        this.activeImageSampler = null;
    }

    private flushTextBatch(viewportHeight: number): void {
        if (this.textCount === 0 || !this.currentFrame || this.activeTextPageKey === null) {
            return;
        }
        const page = this.pages.get(this.activeTextPageKey);
        if (!page) {
            this.textCount = 0;
            this.activeTextPageKey = null;
            return;
        }
        this.applyClip(this.activeTextClip, viewportHeight);
        this.captureGLState(GL_STATE_PROGRAM);
        this.glTouchedGroups |= GL_STATE_PROGRAM;
        this.gl.useProgram(this.textProgram);
        this.gl.uniform2f(this.textViewportUniform, this.currentFrame.viewportWidth, this.currentFrame.viewportHeight);
        this.bindUnit0Texture(page.texture);
        this.gl.uniform1i(this.textAtlasUniform, 0);
        this.captureGLState(GL_STATE_VERTEX_ARRAY | GL_STATE_ARRAY_BUFFER);
        this.glTouchedGroups |= GL_STATE_VERTEX_ARRAY | GL_STATE_ARRAY_BUFFER;
        this.gl.bindVertexArray(this.textVao);
        this.gl.bindBuffer(this.gl.ARRAY_BUFFER, this.textInstanceBuffer);
        this.gl.bufferSubData(
            this.gl.ARRAY_BUFFER,
            0,
            this.textBatch.subarray(0, this.textCount * TEXT_FLOATS_PER_INSTANCE)
        );
        this.gl.drawArraysInstanced(this.gl.TRIANGLE_STRIP, 0, 4, this.textCount);
        this.gl.bindVertexArray(null);
        this.statisticsState.drawCalls += 1;
        this.textCount = 0;
        this.activeTextPageKey = null;
    }

    private applyClip(clip: ClipState | null, viewportHeight: number): void {
        this.captureGLState(GL_STATE_SCISSOR_TEST);
        this.glTouchedGroups |= GL_STATE_SCISSOR_TEST;
        if (clip === null) {
            this.gl.disable(this.gl.SCISSOR_TEST);
            return;
        }
        this.captureGLState(GL_STATE_SCISSOR_BOX);
        this.glTouchedGroups |= GL_STATE_SCISSOR_BOX;
        this.gl.enable(this.gl.SCISSOR_TEST);
        const x = Math.max(0, Math.floor(clip.x));
        const y = Math.max(0, Math.floor(viewportHeight - (clip.y + clip.height)));
        const width = Math.max(0, Math.ceil(clip.width));
        const height = Math.max(0, Math.ceil(clip.height));
        this.gl.scissor(x, y, width, height);
    }

    /**
     * Captures the external GL state for the given groups if (and only if)
     * they have not been captured yet this frame. Called immediately before
     * the renderer clobbers the corresponding state, so untouched groups are
     * never read and never restored.
     */
    private captureGLState(groups: number): void {
        const pending = groups & ~this.glCapturedGroups;
        if (pending === 0) {
            return;
        }
        const gl = this.gl;
        const shadow = this.glShadow;
        if ((pending & GL_STATE_FRAMEBUFFER) !== 0) {
            shadow.framebuffer = readGLParameter<WebGLFramebuffer | null>(gl, gl.FRAMEBUFFER_BINDING, null);
        }
        if ((pending & GL_STATE_VIEWPORT) !== 0) {
            const viewport = readGLParameter<Int32Array | readonly number[] | null>(gl, gl.VIEWPORT, null);
            const valid = viewport !== null && viewport.length >= 4;
            shadow.viewportX = valid ? viewport![0] ?? 0 : undefined;
            shadow.viewportY = valid ? viewport![1] ?? 0 : undefined;
            shadow.viewportWidth = valid ? viewport![2] ?? 0 : undefined;
            shadow.viewportHeight = valid ? viewport![3] ?? 0 : undefined;
        }
        if ((pending & GL_STATE_SCISSOR_BOX) !== 0) {
            const scissorBox = readGLParameter<Int32Array | readonly number[] | null>(gl, gl.SCISSOR_BOX, null);
            const valid = scissorBox !== null && scissorBox.length >= 4;
            shadow.scissorX = valid ? scissorBox![0] ?? 0 : undefined;
            shadow.scissorY = valid ? scissorBox![1] ?? 0 : undefined;
            shadow.scissorWidth = valid ? scissorBox![2] ?? 0 : undefined;
            shadow.scissorHeight = valid ? scissorBox![3] ?? 0 : undefined;
        }
        if ((pending & GL_STATE_SCISSOR_TEST) !== 0) {
            shadow.scissorTest = readGLEnabled(gl, gl.SCISSOR_TEST);
        }
        if ((pending & GL_STATE_PROGRAM) !== 0) {
            shadow.program = readGLParameter<WebGLProgram | null>(gl, gl.CURRENT_PROGRAM, null);
        }
        if ((pending & GL_STATE_VERTEX_ARRAY) !== 0) {
            shadow.vertexArray = readGLParameter<WebGLVertexArrayObject | null>(gl, gl.VERTEX_ARRAY_BINDING, null);
        }
        if ((pending & GL_STATE_ARRAY_BUFFER) !== 0) {
            shadow.arrayBuffer = readGLParameter<WebGLBuffer | null>(gl, gl.ARRAY_BUFFER_BINDING, null);
        }
        if ((pending & GL_STATE_UNPACK_ALIGNMENT) !== 0) {
            shadow.unpackAlignment = readGLParameter<number>(gl, gl.UNPACK_ALIGNMENT, undefined);
        }
        if ((pending & GL_STATE_CULL_FACE) !== 0) {
            shadow.cullFace = readGLEnabled(gl, gl.CULL_FACE);
        }
        if ((pending & GL_STATE_DEPTH_TEST) !== 0) {
            shadow.depthTest = readGLEnabled(gl, gl.DEPTH_TEST);
        }
        if ((pending & GL_STATE_BLEND) !== 0) {
            shadow.blend = readGLEnabled(gl, gl.BLEND);
        }
        if ((pending & GL_STATE_BLEND_FUNC) !== 0) {
            shadow.blendSrcRgb = readGLParameter<number>(gl, gl.BLEND_SRC_RGB, undefined);
            shadow.blendDstRgb = readGLParameter<number>(gl, gl.BLEND_DST_RGB, undefined);
            shadow.blendSrcAlpha = readGLParameter<number>(gl, gl.BLEND_SRC_ALPHA, undefined);
            shadow.blendDstAlpha = readGLParameter<number>(gl, gl.BLEND_DST_ALPHA, undefined);
        }
        if ((pending & GL_STATE_ACTIVE_TEXTURE) !== 0) {
            shadow.activeTexture = readGLParameter<number>(gl, gl.ACTIVE_TEXTURE, gl.TEXTURE0);
        }
        if ((pending & GL_STATE_UNIT0_TEXTURE) !== 0) {
            shadow.unit0Texture = readGLParameter<WebGLTexture | null>(gl, gl.TEXTURE_BINDING_2D, null);
        }
        if ((pending & GL_STATE_UNIT0_SAMPLER) !== 0) {
            shadow.unit0Sampler = readGLParameter<WebGLSampler | null>(gl, gl.SAMPLER_BINDING, null);
        }
        this.glCapturedGroups |= pending;
    }

    /** Activates texture unit 0, issuing the call only when the tracked unit differs. */
    private ensureActiveUnit0(): void {
        if (this.currentGLTextureUnit === this.gl.TEXTURE0) {
            return;
        }
        this.captureGLState(GL_STATE_ACTIVE_TEXTURE);
        this.gl.activeTexture(this.gl.TEXTURE0);
        this.currentGLTextureUnit = this.gl.TEXTURE0;
        this.glTouchedGroups |= GL_STATE_ACTIVE_TEXTURE;
    }

    /** Binds a texture on unit 0, capturing the external binding beforehand. */
    private bindUnit0Texture(texture: WebGLTexture | null): void {
        this.ensureActiveUnit0();
        this.captureGLState(GL_STATE_UNIT0_TEXTURE);
        this.glTouchedGroups |= GL_STATE_UNIT0_TEXTURE;
        this.gl.bindTexture(this.gl.TEXTURE_2D, texture);
    }

    /**
     * Restores the external GL state clobbered during the frame. Only groups
     * the renderer actually wrote to are restored; the shadow is reset so the
     * next frame captures fresh external values.
     */
    private restoreGLState(): void {
        const gl = this.gl;
        const shadow = this.glShadow;
        const touched = this.glTouchedGroups;
        if ((touched & GL_STATE_FRAMEBUFFER) !== 0) {
            gl.bindFramebuffer(gl.FRAMEBUFFER, shadow.framebuffer);
        }
        if ((touched & GL_STATE_VIEWPORT) !== 0 && shadow.viewportX !== undefined) {
            gl.viewport(shadow.viewportX, shadow.viewportY ?? 0, shadow.viewportWidth ?? 0, shadow.viewportHeight ?? 0);
        }
        if ((touched & GL_STATE_CULL_FACE) !== 0) {
            restoreGLEnableState(gl, gl.CULL_FACE, shadow.cullFace);
        }
        if ((touched & GL_STATE_DEPTH_TEST) !== 0) {
            restoreGLEnableState(gl, gl.DEPTH_TEST, shadow.depthTest);
        }
        if ((touched & GL_STATE_BLEND) !== 0) {
            restoreGLEnableState(gl, gl.BLEND, shadow.blend);
        }
        if ((touched & GL_STATE_BLEND_FUNC) !== 0 && shadow.blendSrcRgb !== undefined) {
            if (
                shadow.blendDstRgb !== undefined &&
                shadow.blendSrcAlpha !== undefined &&
                shadow.blendDstAlpha !== undefined &&
                typeof gl.blendFuncSeparate === 'function'
            ) {
                gl.blendFuncSeparate(shadow.blendSrcRgb, shadow.blendDstRgb, shadow.blendSrcAlpha, shadow.blendDstAlpha);
            } else if (shadow.blendDstRgb !== undefined) {
                gl.blendFunc(shadow.blendSrcRgb, shadow.blendDstRgb);
            }
        }
        if ((touched & GL_STATE_SCISSOR_TEST) !== 0) {
            restoreGLEnableState(gl, gl.SCISSOR_TEST, shadow.scissorTest);
        }
        if ((touched & GL_STATE_SCISSOR_BOX) !== 0 && shadow.scissorX !== undefined) {
            gl.scissor(shadow.scissorX, shadow.scissorY ?? 0, shadow.scissorWidth ?? 0, shadow.scissorHeight ?? 0);
        }
        if ((touched & GL_STATE_PROGRAM) !== 0) {
            gl.useProgram(shadow.program);
        }
        if ((touched & GL_STATE_VERTEX_ARRAY) !== 0) {
            gl.bindVertexArray(shadow.vertexArray);
        }
        if ((touched & GL_STATE_ARRAY_BUFFER) !== 0) {
            gl.bindBuffer(gl.ARRAY_BUFFER, shadow.arrayBuffer);
        }
        if ((touched & GL_STATE_UNPACK_ALIGNMENT) !== 0 && shadow.unpackAlignment !== undefined) {
            gl.pixelStorei?.(gl.UNPACK_ALIGNMENT, shadow.unpackAlignment);
        }
        if ((touched & (GL_STATE_UNIT0_TEXTURE | GL_STATE_UNIT0_SAMPLER)) !== 0) {
            // The unit-0 bindings must be restored while unit 0 is active.
            this.ensureActiveUnit0();
            if ((touched & GL_STATE_UNIT0_TEXTURE) !== 0) {
                gl.bindTexture(gl.TEXTURE_2D, shadow.unit0Texture);
            }
            if ((touched & GL_STATE_UNIT0_SAMPLER) !== 0) {
                gl.bindSampler?.(0, shadow.unit0Sampler);
            }
        }
        if ((touched & GL_STATE_ACTIVE_TEXTURE) !== 0) {
            gl.activeTexture(shadow.activeTexture);
            this.currentGLTextureUnit = shadow.activeTexture;
        }
        this.glCapturedGroups = 0;
        this.glTouchedGroups = 0;
    }

    private ensureGlyphPage(entry: GlyphAtlasEntry): TexturePage | null {
        const key = createGlyphPageKey(entry);
        let page = this.pages.get(key);
        if (!page) {
            const texture = this.gl.createTexture();
            if (!texture) {
                return null;
            }
            this.bindUnit0Texture(texture);
            const internalFormat = entry.format === 'rgba8' ? this.gl.RGBA8 : this.gl.R8;
            const format = entry.format === 'rgba8' ? this.gl.RGBA : this.gl.RED;
            this.gl.texParameteri(
                this.gl.TEXTURE_2D,
                this.gl.TEXTURE_MIN_FILTER,
                this.atlasFilter === 'linear' ? this.gl.LINEAR : this.gl.NEAREST
            );
            this.gl.texParameteri(
                this.gl.TEXTURE_2D,
                this.gl.TEXTURE_MAG_FILTER,
                this.atlasFilter === 'linear' ? this.gl.LINEAR : this.gl.NEAREST
            );
            this.gl.texParameteri(this.gl.TEXTURE_2D, this.gl.TEXTURE_WRAP_S, this.gl.CLAMP_TO_EDGE);
            this.gl.texParameteri(this.gl.TEXTURE_2D, this.gl.TEXTURE_WRAP_T, this.gl.CLAMP_TO_EDGE);
            this.captureGLState(GL_STATE_UNPACK_ALIGNMENT);
            this.glTouchedGroups |= GL_STATE_UNPACK_ALIGNMENT;
            this.gl.pixelStorei?.(this.gl.UNPACK_ALIGNMENT, 1);
            this.gl.texImage2D(
                this.gl.TEXTURE_2D,
                0,
                internalFormat,
                entry.pageWidth,
                entry.pageHeight,
                0,
                format,
                this.gl.UNSIGNED_BYTE,
                null
            );
            page = {
                texture,
                width: entry.pageWidth,
                height: entry.pageHeight,
                format: entry.format,
                uploadedGlyphs: new Set<number>(),
            };
            this.pages.set(key, page);
        }
        const glyphKey = createUploadedGlyphKey(entry);
        if (!page.uploadedGlyphs.has(glyphKey)) {
            if (!entry.data) {
                return null;
            }
            const packed = this.packGlyphData(entry);
            this.bindUnit0Texture(page.texture);
            this.captureGLState(GL_STATE_UNPACK_ALIGNMENT);
            this.glTouchedGroups |= GL_STATE_UNPACK_ALIGNMENT;
            this.gl.pixelStorei?.(this.gl.UNPACK_ALIGNMENT, 1);
            this.gl.texSubImage2D(
                this.gl.TEXTURE_2D,
                0,
                entry.x,
                entry.y,
                entry.width,
                entry.height,
                entry.format === 'rgba8' ? this.gl.RGBA : this.gl.RED,
                this.gl.UNSIGNED_BYTE,
                packed
            );
            page.uploadedGlyphs.add(glyphKey);
            this.statisticsState.uploadedGlyphCount += 1;
            // Drop CPU bitmap after GPU upload to prevent memory retention
            (entry as any).data = null;
        }
        return page;
    }

    private packGlyphData(entry: GlyphAtlasEntry): Uint8Array {
        const bytesPerPixel = entry.format === 'rgba8' ? 4 : 1;
        const expectedStride = entry.width * bytesPerPixel;
        const source = toUint8Array(entry.data!);
        if (entry.rowStride === expectedStride) {
            return source;
        }
        const packed = new Uint8Array(expectedStride * entry.height);
        for (let row = 0; row < entry.height; row += 1) {
            const sourceOffset = row * entry.rowStride;
            const targetOffset = row * expectedStride;
            packed.set(source.subarray(sourceOffset, sourceOffset + expectedStride), targetOffset);
        }
        return packed;
    }

    private ensureActive(): void {
        if (this.disposed) {
            throw new DisposedUIError('WebGL2UIRenderer');
        }
    }
}

export type { WebGL2UICustomCommandContext, WebGL2UIRendererOptions, WebGL2UIRendererStatistics };
