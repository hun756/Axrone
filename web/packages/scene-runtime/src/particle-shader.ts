import type { SceneShaderDefinition } from './types';

export const DEFAULT_SCENE_PARTICLE_SHADER_ID = 'scene/particle-point';

/**
 * Particle point-sprite shader.
 *
 * Renders each particle as a single GPU point (`gl.POINTS`). The vertex stage
 * transforms the particle position and derives a distance-attenuated
 * `gl_PointSize`; the fragment stage either shapes the point procedurally via
 * `gl_PointCoord` into one of four sprite modes (glow / disc / star / spark),
 * or samples a texture (single texture, atlas region, or flipbook tile).
 *
 * Vertex layout (interleaved, managed by `SceneParticleBatchRuntime`):
 *   - location 0 `a_Position` : vec3  (particle position)
 *   - location 2 `a_UV0`      : vec2  (x = size, y = seed)
 *   - location 3 `a_Color0`   : vec4  (rgba)
 *
 * Texture uniforms:
 *   - u_Texture          : sampler2D (particle texture / atlas)
 *   - u_UseTexture       : float     (0 = procedural, 1 = texture)
 *   - u_TexSheetParams   : vec4      (tilesX, tilesY, frameIndex, totalFrames)
 *   - u_TexRegion        : vec4      (u, v, width, height in atlas UV space)
 *
 * When u_UseTexture == 1 and u_TexSheetParams.w > 0, the shader computes the
 * flipbook tile UV from frameIndex and gl_PointCoord. When u_TexSheetParams.w
 * == 0, the shader uses u_TexRegion directly for atlas sub-region sampling.
 * When u_UseTexture == 0, the procedural sprite modes are used as before.
 */
export const createParticleShaderDefinition = (
    id: string = DEFAULT_SCENE_PARTICLE_SHADER_ID
): SceneShaderDefinition => ({
    id,
    vertexSource: `#version 300 es
layout(location = 0) in vec3 a_Position;
layout(location = 1) in float a_Rotation;
layout(location = 2) in vec2 a_UV0;
layout(location = 3) in vec4 a_Color0;
uniform mat4 u_ViewProjection;
uniform mat4 u_Model;
uniform float u_PointScale;
uniform float u_MaxPointSize;
out vec4 v_Color;
out float v_Seed;
out float v_Rotation;
void main() {
    vec4 worldPosition = u_Model * vec4(a_Position, 1.0);
    gl_Position = u_ViewProjection * worldPosition;
    v_Color = a_Color0;
    v_Seed = a_UV0.y;
    v_Rotation = a_Rotation;
    float size = a_UV0.x;
    float attenuated = size * u_PointScale / max(gl_Position.w, 1e-4);
    gl_PointSize = clamp(attenuated, 1.0, u_MaxPointSize);
}`,
    fragmentSource: `#version 300 es
precision mediump float;
uniform int u_SpriteMode;
uniform sampler2D u_Texture;
uniform float u_UseTexture;
uniform vec4 u_TexSheetParams;
uniform vec4 u_TexRegion;
in vec4 v_Color;
in float v_Seed;
in float v_Rotation;
out vec4 o_Color;

vec2 rotateUV(vec2 uv, float angle) {
    vec2 centered = uv - 0.5;
    float c = cos(angle);
    float s = sin(angle);
    return vec2(c * centered.x - s * centered.y, s * centered.x + c * centered.y) + 0.5;
}

vec2 computeTexUV(vec2 pointCoord) {
    float tilesX = u_TexSheetParams.x;
    float tilesY = u_TexSheetParams.y;
    float frameIndex = u_TexSheetParams.z;
    float totalFrames = u_TexSheetParams.w;

    if (totalFrames > 0.0) {
        float tileW = 1.0 / tilesX;
        float tileH = 1.0 / tilesY;
        float col = mod(frameIndex, tilesX);
        float row = floor(frameIndex / tilesX);
        vec2 tileOrigin = vec2(col * tileW, 1.0 - (row + 1.0) * tileH);
        return tileOrigin + pointCoord * vec2(tileW, tileH);
    }

    return u_TexRegion.xy + pointCoord * u_TexRegion.zw;
}

void main() {
    vec2 pointCoord = rotateUV(gl_PointCoord, v_Rotation);
    vec2 centered = pointCoord - 0.5;
    float dist = length(centered);

    if (u_UseTexture > 0.5) {
        vec2 texUV = computeTexUV(pointCoord);
        vec4 texel = texture(u_Texture, texUV);
        vec4 result = texel * v_Color;
        if (result.a < 0.012) {
            discard;
        }
        o_Color = result;
        return;
    }

    float mask = 0.0;
    if (u_SpriteMode == 0) {
        mask = pow(max(0.0, 1.0 - dist * 2.0), 2.1);
    } else if (u_SpriteMode == 1) {
        mask = 1.0 - smoothstep(0.40, 0.5, dist);
    } else if (u_SpriteMode == 2) {
        float diamond = abs(centered.x) + abs(centered.y);
        mask = pow(max(0.0, 1.0 - diamond * 2.0), 3.0)
             + pow(max(0.0, 1.0 - dist * 2.0), 2.0) * 0.35;
    } else {
        float crossMask = exp(-abs(centered.x) * 13.0) + exp(-abs(centered.y) * 13.0);
        mask = min(1.0, crossMask) * pow(max(0.0, 1.0 - dist * 2.0), 1.4);
    }
    if (mask < 0.012) {
        discard;
    }
    o_Color = vec4(v_Color.rgb * mask, v_Color.a * mask);
}`,
    attributes: {
        position: 'a_Position',
        uv0: 'a_UV0',
        color0: 'a_Color0',
    },
    uniforms: [
        'u_ViewProjection',
        'u_Model',
        'u_PointScale',
        'u_MaxPointSize',
        'u_SpriteMode',
        'u_Texture',
        'u_UseTexture',
        'u_TexSheetParams',
        'u_TexRegion',
    ],
    depthTest: true,
    cull: false,
    blend: true,
});
