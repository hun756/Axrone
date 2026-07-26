import type { SceneShaderDefinition } from './types';

export const DEFAULT_SCENE_PARTICLE_SHADER_ID = 'scene/particle-point';

/**
 * Particle point-sprite shader.
 *
 * Renders each particle as a single GPU point (`gl.POINTS`). The vertex stage
 * transforms the particle position and derives a distance-attenuated
 * `gl_PointSize`; the fragment stage shapes the point procedurally via
 * `gl_PointCoord` into one of four sprite modes (glow / disc / star / spark),
 * modulated by the per-particle color and alpha.
 *
 * Vertex layout (interleaved, managed by `SceneParticleBatchRuntime`):
 *   - location 0 `a_Position` : vec3  (particle position)
 *   - location 2 `a_UV0`      : vec2  (x = size, y = seed)
 *   - location 3 `a_Color0`   : vec4  (rgba)
 *
 * The design is intentionally minimal so an instanced-billboard variant can be
 * added later behind the same batch runtime without changing this contract.
 */
export const createParticleShaderDefinition = (
    id: string = DEFAULT_SCENE_PARTICLE_SHADER_ID
): SceneShaderDefinition => ({
    id,
    vertexSource: `#version 300 es
layout(location = 0) in vec3 a_Position;
layout(location = 2) in vec2 a_UV0;
layout(location = 3) in vec4 a_Color0;
uniform mat4 u_ViewProjection;
uniform mat4 u_Model;
uniform float u_PointScale;
uniform float u_MaxPointSize;
out vec4 v_Color;
out float v_Seed;
void main() {
    vec4 worldPosition = u_Model * vec4(a_Position, 1.0);
    gl_Position = u_ViewProjection * worldPosition;
    v_Color = a_Color0;
    v_Seed = a_UV0.y;
    float size = a_UV0.x;
    float attenuated = size * u_PointScale / max(gl_Position.w, 1e-4);
    gl_PointSize = clamp(attenuated, 1.0, u_MaxPointSize);
}`,
    fragmentSource: `#version 300 es
precision highp float;
uniform int u_SpriteMode;
in vec4 v_Color;
in float v_Seed;
out vec4 o_Color;
void main() {
    vec2 uv = gl_PointCoord - 0.5;
    float dist = length(uv);
    float mask = 0.0;
    if (u_SpriteMode == 0) {
        mask = pow(max(0.0, 1.0 - dist * 2.0), 2.1);
    } else if (u_SpriteMode == 1) {
        mask = 1.0 - smoothstep(0.40, 0.5, dist);
    } else if (u_SpriteMode == 2) {
        float diamond = abs(uv.x) + abs(uv.y);
        mask = pow(max(0.0, 1.0 - diamond * 2.0), 3.0)
             + pow(max(0.0, 1.0 - dist * 2.0), 2.0) * 0.35;
    } else {
        float crossMask = exp(-abs(uv.x) * 13.0) + exp(-abs(uv.y) * 13.0);
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
    ],
    depthTest: true,
    cull: false,
    blend: true,
});
