/**
 * Shader reflection API.
 *
 * `reflectShaderEffect` derives a stable, serialisable description of an effect
 * — every uniform, attribute, varying, keyword, technique and stage, plus the
 * declared keyword variant count — from its definition. Editors and tooling use
 * this to auto-generate inspector UIs, validate materials, and reason about
 * shader complexity without compiling GLSL.
 */

import type {
    RenderShaderEffectDefinition,
    RenderShaderInspectorControlDefinition,
    RenderShaderKeywordDefinition,
    RenderShaderPropertyDefinition,
    RenderShaderStageDefinition,
    RenderShaderValueType,
} from '@axrone/render-core/shader-effect';
import { shaderVariantCount } from './variants';

export interface ShaderAttributeReflection {
    readonly name: string;
    readonly type: RenderShaderValueType;
    readonly location?: number;
}

export interface ShaderUniformReflection {
    readonly name: string;
    readonly type: RenderShaderValueType;
    readonly arrayLength?: number;
    readonly scope?: RenderShaderPropertyDefinition['scope'];
    readonly stages: readonly ('vertex' | 'fragment')[];
    readonly hasDefault: boolean;
    readonly inspector?: RenderShaderInspectorControlDefinition;
    readonly inspectorGroup?: string;
}

export interface ShaderKeywordReflection {
    readonly name: string;
    readonly kind: 'toggle' | 'enum';
    readonly options?: readonly string[];
    readonly defaultValue?: boolean | string;
    readonly stages?: readonly ('vertex' | 'fragment')[];
}

export interface ShaderStageReflection {
    readonly precision?: 'lowp' | 'mediump' | 'highp';
    readonly version?: string;
    readonly inputCount: number;
    readonly outputCount: number;
    readonly declarationCount: number;
    readonly mainLineCount: number;
}

export interface ShaderTechniquePassReflection {
    readonly id: string;
    readonly keywords: readonly string[];
    readonly hasVertexStage: boolean;
    readonly hasFragmentStage: boolean;
    readonly depthTest?: boolean;
    readonly cull?: boolean;
    readonly blend?: boolean;
}

export interface ShaderTechniqueReflection {
    readonly id: string;
    readonly label?: string;
    readonly passes: readonly ShaderTechniquePassReflection[];
}

export interface ShaderEffectReflection {
    readonly id: string;
    readonly format: string;
    readonly version: 1 | 2;
    readonly attributes: readonly ShaderAttributeReflection[];
    readonly varyings: readonly ShaderAttributeReflection[];
    readonly uniforms: readonly ShaderUniformReflection[];
    readonly keywords: readonly ShaderKeywordReflection[];
    readonly techniques: readonly ShaderTechniqueReflection[];
    readonly defaultTechnique?: string;
    readonly variantCount: number;
    readonly stages: {
        readonly vertex: ShaderStageReflection;
        readonly fragment: ShaderStageReflection;
    };
}

const reflectStage = (stage: RenderShaderStageDefinition): ShaderStageReflection => ({
    precision: stage.precision,
    version: stage.version,
    inputCount: stage.inputs?.length ?? 0,
    outputCount: stage.outputs?.length ?? 0,
    declarationCount: stage.declarations?.length ?? 0,
    mainLineCount: stage.main.length,
});

const reflectKeyword = (keyword: RenderShaderKeywordDefinition): ShaderKeywordReflection => ({
    name: keyword.name,
    kind: keyword.options && keyword.options.length > 0 ? 'enum' : 'toggle',
    options: keyword.options,
    defaultValue: keyword.defaultValue,
    stages: keyword.stages as readonly ('vertex' | 'fragment')[] | undefined,
});

export const reflectShaderEffect = (
    effect: RenderShaderEffectDefinition
): ShaderEffectReflection => ({
    id: effect.id,
    format: effect.format,
    version: effect.version,
    attributes:
        effect.attributes?.map((attribute) => ({
            name: attribute.name,
            type: attribute.type,
            location: attribute.location,
        })) ?? [],
    varyings:
        effect.varyings?.map((varying) => ({
            name: varying.name,
            type: varying.type,
        })) ?? [],
    uniforms:
        effect.properties?.map((property) => ({
            name: property.name,
            type: property.type,
            arrayLength: property.arrayLength,
            scope: property.scope,
            stages: (property.stages ?? ['vertex', 'fragment']) as readonly (
                | 'vertex'
                | 'fragment'
            )[],
            hasDefault: property.defaultValue !== undefined,
            inspector: property.inspector,
            inspectorGroup: property.inspector?.group,
        })) ?? [],
    keywords: effect.keywords?.map(reflectKeyword) ?? [],
    techniques:
        effect.techniques?.map((technique) => ({
            id: technique.id,
            label: technique.label,
            passes: technique.passes.map((pass) => ({
                id: pass.id,
                keywords: pass.keywords ?? [],
                hasVertexStage: pass.vertex !== undefined,
                hasFragmentStage: pass.fragment !== undefined,
                depthTest: pass.renderState?.depthTest,
                cull: pass.renderState?.cull,
                blend: pass.renderState?.blend,
            })),
        })) ?? [],
    defaultTechnique: effect.defaultTechnique,
    variantCount: shaderVariantCount(effect.keywords ?? []),
    stages: {
        vertex: reflectStage(effect.vertex),
        fragment: reflectStage(effect.fragment),
    },
});
