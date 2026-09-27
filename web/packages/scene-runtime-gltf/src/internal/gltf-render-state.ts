import type { SceneMaterialPassDefinition } from '@axrone/scene-runtime';
import type { GltfMaterialUniformMap } from './runtime-shaders';

const GLTF_SHADER_DOUBLE_SIDED_SUFFIX = '/double-sided';
const GLTF_SHADER_BLEND_SUFFIX = '/blend';

const normalizeNumericUniform = (value: unknown, fallback: number): number => {
    if (typeof value === 'number' && Number.isFinite(value)) {
        return value;
    }

    return fallback;
};

const isGltfBlendAlphaMode = (uniforms: GltfMaterialUniformMap | undefined): boolean =>
    normalizeNumericUniform(uniforms?._AlphaMode, 0) >= 1.5;

const isGltfDoubleSided = (uniforms: GltfMaterialUniformMap | undefined): boolean =>
    normalizeNumericUniform(uniforms?._DoubleSided, 0) >= 0.5;

export const createGltfRuntimeMaterialPasses = (
    uniforms?: GltfMaterialUniformMap
): readonly SceneMaterialPassDefinition[] => {
    const alphaMode = normalizeNumericUniform(uniforms?._AlphaMode, 0);
    const blendEnabled = alphaMode >= 1.5;
    const alphaTestEnabled = alphaMode >= 0.5 && alphaMode < 1.5;
    const doubleSided = isGltfDoubleSided(uniforms);
    const cullMode = doubleSided ? 'none' : 'back';

    return Object.freeze([
        {
            id: 'main',
            phase: 'default',
            primitive: 'triangle-list',
            rasterizerState: {
                cullMode,
                frontFace: 'ccw',
            },
            depthStencilState: {
                depthTest: true,
                depthWrite: !blendEnabled,
                depthFunc: 'less',
            },
            blendState: {
                targets: [
                    {
                        blend: blendEnabled,
                        srcColorFactor: 'src-alpha',
                        dstColorFactor: 'one-minus-src-alpha',
                        colorOp: 'add',
                        srcAlphaFactor: 'one',
                        dstAlphaFactor: 'one-minus-src-alpha',
                        alphaOp: 'add',
                    },
                ],
            },
        },
        {
            id: 'forward-add',
            phase: 'forward-add',
            primitive: 'triangle-list',
            rasterizerState: {
                cullMode,
                frontFace: 'ccw',
            },
            depthStencilState: {
                depthTest: true,
                depthWrite: false,
                depthFunc: 'lequal',
            },
            blendState: {
                targets: [
                    {
                        blend: true,
                        srcColorFactor: 'one',
                        dstColorFactor: 'one',
                        colorOp: 'add',
                        srcAlphaFactor: 'one',
                        dstAlphaFactor: 'one',
                        alphaOp: 'add',
                    },
                ],
            },
        },
        {
            id: 'shadow-caster',
            phase: 'shadow-caster',
            primitive: 'triangle-list',
            rasterizerState: {
                cullMode,
                frontFace: 'ccw',
            },
            depthStencilState: {
                depthTest: true,
                depthWrite: true,
                depthFunc: 'less',
            },
            blendState: {
                targets: [
                    {
                        blend: false,
                    },
                ],
            },
            ...(alphaTestEnabled
                ? {
                      priority: 1,
                  }
                : {}),
        },
    ]);
};

export const createVariantShaderId = (
    baseId: string,
    options: {
        readonly blend: boolean;
        readonly doubleSided: boolean;
    }
): string => {
    let variantId = baseId;
    if (options.blend) {
        variantId += GLTF_SHADER_BLEND_SUFFIX;
    }
    if (options.doubleSided) {
        variantId += GLTF_SHADER_DOUBLE_SIDED_SUFFIX;
    }
    return variantId;
};

export const resolveVariantRenderState = (uniforms: GltfMaterialUniformMap | undefined): {
    readonly blend: boolean;
    readonly cull: boolean;
} => {
    const blend = isGltfBlendAlphaMode(uniforms);
    const doubleSided = isGltfDoubleSided(uniforms);
    return {
        blend,
        cull: !doubleSided,
    };
};
