import {
    type RenderShaderInterfaceDefinition,
    type RenderShaderStageDefinition,
} from '@axrone/render-core/shader-effect';

/**
 * Shared, reusable shader pieces authored as `.ts` modules.
 *
 * Because effects are now plain TypeScript, varyings, vertex stages, and
 * declaration libraries are imported exactly like any other code — no more
 * copy/pasted string arrays across every effect file.
 */

export const COSMIC_VARYINGS: readonly RenderShaderInterfaceDefinition[] = [
    { name: 'v_worldPos', type: 'vec2' },
    { name: 'v_uv', type: 'vec2' },
];

export const COSMIC_VERTEX: RenderShaderStageDefinition = {
    main: [
        'v_worldPos = (u_transform * vec3(a_position, 1.0)).xy;',
        'v_uv = (u_projection * vec3(a_position, 1.0)).xy;',
        'gl_Position = vec4(a_position, 0.0, 1.0);',
    ],
};
