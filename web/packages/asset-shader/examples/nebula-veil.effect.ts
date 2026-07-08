import {
    attr,
    defineShaderEffect,
    fragStage,
    glsl,
    prop,
    varying,
    vtxStage,
} from '../src/authoring';
import { COSMIC_VARYINGS, COSMIC_VERTEX } from './shared';

/**
 * Declaration libraries authored as real GLSL via the {@link glsl} tag.
 * Each block is a single multiline string instead of a `string[]` array.
 */
const VOLUMETRIC_DECL = glsl`
    float hash13(vec3 p3) {
        p3 = fract(p3 * 0.1031);
        p3 += dot(p3, p3.yzx + 33.33);
        return fract((p3.x + p3.y) * p3.z);
    }

    float vnoise(vec3 p) {
        vec3 i = floor(p), f = fract(p);
        f = f * f * (3.0 - 2.0 * f);
        float n000 = hash13(i + vec3(0.0, 0.0, 0.0));
        float n100 = hash13(i + vec3(1.0, 0.0, 0.0));
        float n010 = hash13(i + vec3(0.0, 1.0, 0.0));
        float n110 = hash13(i + vec3(1.0, 1.0, 0.0));
        float n001 = hash13(i + vec3(0.0, 0.0, 1.0));
        float n101 = hash13(i + vec3(1.0, 0.0, 1.0));
        float n011 = hash13(i + vec3(0.0, 1.0, 1.0));
        float n111 = hash13(i + vec3(1.0, 1.0, 1.0));
        return mix(
            mix(mix(n000, n100, f.x), mix(n010, n110, f.x), f.y),
            mix(mix(n001, n101, f.x), mix(n011, n111, f.x), f.y),
            f.z
        );
    }

    float fbm(vec3 p) {
        float v = 0.0;
        float a = 0.5;
        mat3 rot = mat3(
             0.00, 0.80, 0.60,
            -0.80, 0.36, -0.48,
            -0.60, -0.48, 0.64
        );
        for (int i = 0; i < 4; i++) {
            v += a * vnoise(p);
            p = rot * p * 2.02;
            a *= 0.5;
        }
        return v;
    }

    vec3 aces(vec3 x) {
        const float a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
        return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
    }
`;

/**
 * File-based shader effect: JSON-like metadata + real GLSL template literals,
 * with shared pieces imported from `./shared`.
 */
export const nebulaVeil = defineShaderEffect({
    id: 'nebulaVeil',
    attributes: [attr('a_position')],
    varyings: COSMIC_VARYINGS,
    properties: [
        prop('u_projection', 'mat3', 'frame', undefined, { stages: ['vertex'] }),
        prop('u_transform', 'mat3', 'object', undefined, { stages: ['vertex'] }),
        prop('u_time', 'float', 'frame', undefined, { stages: ['fragment'] }),
        prop('u_baseColor', 'vec4', 'material', [0.04, 0.02, 0.1, 1], {
            stages: ['fragment'],
            inspector: { label: 'Base Color', control: 'color', group: 'Surface' },
        }),
        prop('u_opacity', 'float', 'material', 1, { stages: ['fragment'] }),
        prop('u_bodyCenter', 'vec2', 'material', [300, 395], { stages: ['fragment'] }),
        prop('u_bodySize', 'vec2', 'material', [600, 680], { stages: ['fragment'] }),
        prop('u_density', 'float', 'material', 0.4, { stages: ['fragment'] }),
        prop('u_emission', 'float', 'material', 1.2, { stages: ['fragment'] }),
        prop('u_speed', 'float', 'material', 0.9, { stages: ['fragment'] }),
        prop('u_drift', 'float', 'material', 0.14, { stages: ['fragment'] }),
        prop('u_palette', 'float', 'material', 0.6, { stages: ['fragment'] }),
        prop('u_steps', 'float', 'material', 32, { stages: ['fragment'] }),
    ],
    vertex: COSMIC_VERTEX,
    fragment: fragStage(
        glsl`
            vec2 uv = (v_worldPos - u_bodyCenter + u_bodySize * 0.5) / u_bodySize;
            vec2 p = (uv - 0.5) * 2.0;

            float yaw = 1.13;
            float pitch = 0.06;
            mat3 cam = rotY(yaw) * rotX(pitch);
            vec3 ro = cam * vec3(0.0, 0.0, -4.2);
            vec3 rd = cam * normalize(vec3(p, 1.2));

            vec3 col = mix(vec3(0.015, 0.01, 0.04), vec3(0.04, 0.02, 0.08), smoothstep(-0.6, 0.8, p.y));

            float tMax = 7.0;
            float steps = u_steps;
            float dt = (tMax - 0.5) / steps;
            float t = 0.5 + dt * hash13(vec3(v_worldPos, u_time * 100.0));
            float transmission = 1.0;
            vec3 glow = vec3(0.0);

            for (int i = 0; i < 64; i++) {
                if (i >= int(steps)) break;
                if (transmission < 0.05) break;
                vec3 pos = ro + rd * t;
                float dens = fbm(pos * 0.35 + u_time * u_speed) * u_density;
                if (dens > 0.02) {
                    vec3 albedo = vec3(0.6, 0.5, 0.9);
                    vec3 scatter = dens * albedo * u_emission;
                    glow += transmission * scatter;
                    transmission *= 0.96;
                }
                t += dt;
                if (t > tMax) break;
            }

            col = col * transmission + glow;
            col = aces(col * 1.15);
            col = pow(col, vec3(1.0 / 2.2));
            fragColor = vec4(col, u_opacity);
        `,
        [VOLUMETRIC_DECL],
        {
            precision: 'highp',
            outputs: [varying('fragColor', 'vec4')],
        }
    ),
});

export default nebulaVeil;
