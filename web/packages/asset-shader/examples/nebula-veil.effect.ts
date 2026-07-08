import {
    attr,
    defineShaderEffect,
    extend,
    fragStage,
    glsl,
    keyword,
    library,
    prop,
    rangeProp,
    reflect,
    varying,
    vtxStage,
} from '../src/authoring';
import { COSMIC_VARYINGS, COSMIC_VERTEX } from './shared';

/**
 * File-based shader toolkit showcase.
 *
 * This single authored effect exercises every competitive capability:
 *   - GLSL authored with the `glsl` tagged template (no string arrays)
 *   - shared chunks pulled from the global library registry via `#include`
 *   - keyword-driven variants (`USE_FOG`, `QUALITY`) resolved by the
 *     preprocessor at build time
 *   - a base effect that is extended into a specialised "deep field" variant
 *   - reflection metadata derived for editor tooling
 */

// Reusable GLSL chunk registered once and `#include`d from any effect.
export const NOISE_LIBRARY = library(
    'axrone/noise',
    glsl`
        float hash13(vec3 p3) {
            p3 = fract(p3 * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return fract((p3.x + p3.y) * p.z);
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
    `
);

/** Base nebula effect. */
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
        rangeProp('u_density', 'float', 0, 1, 0.05, 0.4, {
            stages: ['fragment'],
            inspector: { label: 'Density', group: 'Surface' },
        }),
        prop('u_emission', 'float', 'material', 1.2, { stages: ['fragment'] }),
        prop('u_opacity', 'float', 'material', 1, { stages: ['fragment'] }),
    ],
    keywords: [
        keyword('USE_FOG', ['fragment'], undefined, false, {
            label: 'Volumetric Fog',
            group: 'Features',
            control: 'toggle',
        }),
        keyword('QUALITY', ['fragment'], ['LOW', 'HIGH'], 'HIGH', {
            label: 'Sample Quality',
            group: 'Features',
            control: 'select',
            options: [
                { label: 'Low', value: 'LOW' },
                { label: 'High', value: 'HIGH' },
            ],
        }),
    ],
    vertex: COSMIC_VERTEX,
    fragment: fragStage(
        glsl`
            #include <axrone/noise>
            vec3 col = u_baseColor.rgb;

            float t = u_time * 0.2;
            float density = vnoise(vec3(v_worldPos * 0.01, t)) * u_density;

            #if USE_FOG
            density *= 1.5;
            col += vec3(0.05, 0.02, 0.08) * density;
            #endif

            #ifdef HIGH
            density += vnoise(vec3(v_worldPos * 0.05, t * 1.7)) * 0.25 * u_density;
            #endif

            col *= u_emission;
            o_Color = vec4(col, u_opacity);
        `,
        [],
        {
            precision: 'highp',
            outputs: [varying('o_Color', 'vec4')],
        }
    ),
});

/** A specialised effect that inherits and overrides the base. */
export const nebulaDeepField = extend(nebulaVeil, {
    id: 'nebulaDeepField',
    properties: [
        prop('u_density', 'float', 'material', 0.85, { stages: ['fragment'] }),
        prop('u_emission', 'float', 'material', 2.4, { stages: ['fragment'] }),
    ],
    keywords: [keyword('USE_FOG', ['fragment'], undefined, true)],
});

/** Editor/tooling reflection derived from the authored definition. */
export const nebulaVeilReflection = reflect(nebulaVeil);

export default nebulaVeil;
