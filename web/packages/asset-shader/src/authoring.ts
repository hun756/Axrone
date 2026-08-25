import {
    type AssetCustomSource,
    type AssetImporter,
} from '@axrone/asset-core';
import { isPlainObject } from '@axrone/utility';
import {
    type RenderShaderAttributeDefinition,
    type RenderShaderEffectDefinition,
    type RenderShaderEffectMigrationDefinition,
    type RenderShaderEffectRenderStateDefinition,
    type RenderShaderInspectorControlDefinition,
    type RenderShaderInterfaceDefinition,
    type RenderShaderKeywordDefinition,
    type RenderShaderLibraryDefinition,
    type RenderShaderPropertyBindingDefinition,
    type RenderShaderPropertyDefinition,
    type RenderShaderStageDefinition,
    type RenderShaderStageName,
    type RenderShaderTechniqueDefinition,
    type RenderShaderValueType,
} from '@axrone/render-core/shader-effect';
import {
    normalizeShaderEffectJsonSource,
    type AssetShaderImportSchema,
} from './shader-effect-importer';
import { extendShaderEffect, type PartialShaderEffectOverride } from './compose';
import {
    reflectShaderEffect,
    type ShaderEffectReflection,
} from './reflection';
import { registerShaderLibrary, type ShaderLibraryEntry } from './library';

/**
 * File-based shader authoring layer.
 *
 * The JSON effect format in `shader-effect-importer.ts` stays the canonical,
 * serialized representation. This module adds a `.ts` authoring surface on top
 * of it so shader effects can be written as ordinary TypeScript modules: the
 * effect metadata is a JSON-like object literal and the GLSL is authored with
 * the {@link glsl} tagged template (real multiline code, no string arrays).
 * Shared varyings, vertex stages, and declaration libraries are imported from
 * sibling modules exactly like any other TypeScript code.
 *
 * A `.ts` module exports a `RenderShaderEffectDefinition` (typically via
 * {@link defineShaderEffect}) and is fed into the existing asset pipeline
 * through {@link toShaderEffectSource} + the {@link createShaderEffectModuleImporter}
 * importer, or serialized back to `.effect.json` with {@link serializeShaderEffectToJson}.
 */

const INSPECTOR_GROUP_FALLBACK = 'Properties';

const dedent = (text: string): string => {
    const lines = text.split('\n');
    let minIndent = Number.POSITIVE_INFINITY;

    for (const line of lines) {
        if (line.trim() === '') {
            continue;
        }

        const indent = (line.match(/^[ \t]*/)?.[0] ?? '').length;
        if (indent < minIndent) {
            minIndent = indent;
        }
    }

    if (Number.isFinite(minIndent) === false) {
        return text.trim();
    }

    return lines
        .map((line) => line.slice(minIndent))
        .join('\n')
        .trim();
};

const splitGlslLines = (source: string): string[] => {
    const lines = dedent(source).split('\n');

    while (lines.length > 0 && lines[0]?.trim() === '') {
        lines.shift();
    }

    while (lines.length > 0 && lines[lines.length - 1]?.trim() === '') {
        lines.pop();
    }

    return lines.filter((line) => line.trim() !== '');
};

/**
 * Tagged template that returns a dedented GLSL source string. Use it to author
 * shader main bodies and declaration libraries as real multiline code instead
 * of `string[]` arrays.
 */
export const glsl = (
    strings: TemplateStringsArray,
    ...values: readonly unknown[]
): string => {
    let result = strings[0] ?? '';
    for (let index = 0; index < values.length; index += 1) {
        result += `${values[index]}${strings[index + 1] ?? ''}`;
    }

    return dedent(result);
};

export type ShaderEffectGlslBlock = string | readonly string[];

export type ShaderEffectStageOptions = {
    readonly precision?: 'lowp' | 'mediump' | 'highp';
    readonly directives?: readonly string[];
    readonly inputs?: readonly RenderShaderInterfaceDefinition[];
    readonly outputs?: readonly RenderShaderInterfaceDefinition[];
    readonly includes?: readonly string[];
};

export const vtxStage = (
    main: string,
    declarations: readonly ShaderEffectGlslBlock[] = [],
    options: ShaderEffectStageOptions = {}
): RenderShaderStageDefinition => ({
    precision: options.precision ?? 'highp',
    ...(options.directives !== undefined ? { directives: options.directives } : {}),
    ...(options.inputs !== undefined ? { inputs: options.inputs } : {}),
    ...(options.outputs !== undefined ? { outputs: options.outputs } : {}),
    ...(options.includes !== undefined ? { includes: options.includes } : {}),
    declarations,
    main: splitGlslLines(main),
});

export const fragStage = vtxStage;

export const attr = (
    name: string,
    type: RenderShaderValueType = 'vec3',
    location?: number
): RenderShaderAttributeDefinition => ({
    name,
    type,
    ...(location !== undefined ? { location } : {}),
});

export const varying = (
    name: string,
    type: RenderShaderValueType,
    interpolation?: 'flat' | 'smooth'
): RenderShaderInterfaceDefinition => ({
    name,
    type,
    ...(interpolation !== undefined ? { interpolation } : {}),
});

export type ShaderEffectPropertyOptions = {
    readonly scope?: RenderShaderPropertyDefinition['scope'];
    readonly stages?: readonly RenderShaderStageName[];
    readonly arrayLength?: number;
    readonly inspector?: RenderShaderInspectorControlDefinition;
    readonly binding?: RenderShaderPropertyBindingDefinition;
};

export const prop = (
    name: string,
    type: RenderShaderValueType,
    scope: RenderShaderPropertyDefinition['scope'] = 'material',
    defaultValue?: RenderShaderPropertyDefinition['defaultValue'],
    options?: ShaderEffectPropertyOptions
): RenderShaderPropertyDefinition => ({
    name,
    type,
    scope,
    ...(defaultValue !== undefined ? { defaultValue } : {}),
    ...(options?.stages !== undefined ? { stages: options.stages } : {}),
    ...(options?.arrayLength !== undefined ? { arrayLength: options.arrayLength } : {}),
    ...(options?.inspector !== undefined ? { inspector: options.inspector } : {}),
    ...(options?.binding !== undefined ? { binding: options.binding } : {}),
});

export interface DefineShaderEffectInput {
    readonly id: string;
    readonly vertex: RenderShaderStageDefinition;
    readonly fragment: RenderShaderStageDefinition;
    readonly attributes?: readonly RenderShaderAttributeDefinition[];
    readonly varyings?: readonly RenderShaderInterfaceDefinition[];
    readonly properties?: readonly RenderShaderPropertyDefinition[];
    readonly keywords?: readonly RenderShaderKeywordDefinition[];
    readonly libraries?: readonly RenderShaderLibraryDefinition[];
    readonly defaultTechnique?: string;
    readonly techniques?: readonly RenderShaderTechniqueDefinition[];
    readonly renderState?: RenderShaderEffectRenderStateDefinition;
    readonly version?: 1 | 2;
    readonly migrations?: RenderShaderEffectMigrationDefinition;
}

export const defineShaderEffect = (
    input: DefineShaderEffectInput
): RenderShaderEffectDefinition => ({
    format: 'axrone.shader/effect',
    version: input.version ?? 2,
    id: input.id,
    ...(input.attributes !== undefined ? { attributes: input.attributes } : {}),
    ...(input.varyings !== undefined ? { varyings: input.varyings } : {}),
    ...(input.properties !== undefined ? { properties: input.properties } : {}),
    ...(input.keywords !== undefined ? { keywords: input.keywords } : {}),
    ...(input.libraries !== undefined ? { libraries: input.libraries } : {}),
    ...(input.defaultTechnique !== undefined
        ? { defaultTechnique: input.defaultTechnique }
        : {}),
    ...(input.techniques !== undefined ? { techniques: input.techniques } : {}),
    vertex: input.vertex,
    fragment: input.fragment,
    ...(input.renderState !== undefined ? { renderState: input.renderState } : {}),
    ...(input.migrations !== undefined ? { migrations: input.migrations } : {}),
});

/**
 * Serialize a `.ts`-authored effect definition back into the canonical JSON
 * shape so it can be persisted as `.effect.json` and round-trip through the
 * JSON importer.
 */
export const serializeShaderEffectToJson = (
    effect: RenderShaderEffectDefinition
): Record<string, unknown> => JSON.parse(JSON.stringify(effect)) as Record<string, unknown>;

/** Declare a shader keyword (variant switch). */
export const keyword = (
    name: string,
    stages?: readonly RenderShaderStageName[],
    options?: readonly string[],
    defaultValue?: boolean | string,
    inspector?: RenderShaderInspectorControlDefinition
): RenderShaderKeywordDefinition => ({
    name,
    ...(stages !== undefined ? { stages } : {}),
    ...(options !== undefined ? { options } : {}),
    ...(defaultValue !== undefined ? { defaultValue } : {}),
    ...(inspector !== undefined ? { inspector } : {}),
});

/** Declare a multi-pass technique. */
export const technique = (
    id: string,
    passes: readonly RenderShaderPassDefinitionInput[],
    label?: string
): RenderShaderTechniqueDefinition => ({
    id,
    ...(label !== undefined ? { label } : {}),
    passes: passes.map((entry) => pass(entry.id, entry)),
});

export interface RenderShaderPassDefinitionInput {
    readonly id: string;
    readonly vertex?: RenderShaderStageDefinition;
    readonly fragment?: RenderShaderStageDefinition;
    readonly renderState?: RenderShaderEffectRenderStateDefinition;
    readonly keywords?: readonly string[];
}

/** Declare a single render pass within a technique. */
export const pass = (
    id: string,
    options: Omit<RenderShaderPassDefinitionInput, 'id'> = {}
): RenderShaderTechniqueDefinition['passes'][number] => ({
    id,
    ...(options.vertex !== undefined ? { vertex: options.vertex } : {}),
    ...(options.fragment !== undefined ? { fragment: options.fragment } : {}),
    ...(options.renderState !== undefined ? { renderState: options.renderState } : {}),
    ...(options.keywords !== undefined ? { keywords: options.keywords } : {}),
});

/**
 * Register a reusable GLSL chunk in the global library registry and return it.
 * Chunks are pulled into effects via `#include <id>`.
 */
export const library = (id: string, code: string): ShaderLibraryEntry =>
    registerShaderLibrary(id, code);

/** Inherit and override a base effect (shader "parent"). */
export const extend = (
    base: RenderShaderEffectDefinition,
    override: PartialShaderEffectOverride
): RenderShaderEffectDefinition => extendShaderEffect(base, override);

/** Reflect an effect into stable metadata for editors / tooling. */
export const reflect = (effect: RenderShaderEffectDefinition): ShaderEffectReflection =>
    reflectShaderEffect(effect);

/** Convenience: a `slider`-backed ranged numeric property. */
export const rangeProp = (
    name: string,
    type: RenderShaderValueType,
    min: number,
    max: number,
    step: number,
    defaultValue?: RenderShaderPropertyDefinition['defaultValue'],
    options?: ShaderEffectPropertyOptions
): RenderShaderPropertyDefinition =>
    prop(name, type, options?.scope ?? 'material', defaultValue, {
        ...options,
        inspector: { control: 'slider', min, max, step, ...(options?.inspector ?? {}) },
    });

/** Convenience: a `toggle`-backed boolean property. */
export const toggleProp = (
    name: string,
    defaultValue: boolean = false,
    options?: ShaderEffectPropertyOptions
): RenderShaderPropertyDefinition =>
    prop(name, 'bool', options?.scope ?? 'material', defaultValue, {
        ...options,
        inspector: { control: 'toggle', ...(options?.inspector ?? {}) },
    });

/** Convenience: a `select`-backed integer enum property. */
export const enumProp = (
    name: string,
    optionLabels: readonly string[],
    defaultIndex: number = 0,
    options?: ShaderEffectPropertyOptions
): RenderShaderPropertyDefinition =>
    prop(name, 'int', options?.scope ?? 'material', defaultIndex, {
        ...options,
        inspector: {
            control: 'select',
            options: optionLabels.map((label, value) => ({ label, value })),
            ...(options?.inspector ?? {}),
        },
    });

/**
 * Wrap a `.ts`-authored effect into an `AssetCustomSource` so it can be passed
 * straight into the existing asset pipeline. The {@link createShaderEffectModuleImporter}
 * claims sources with `format: 'axrone.shader/effect'`.
 */
export const toShaderEffectSource = (
    effect: RenderShaderEffectDefinition,
    uri?: string
): AssetCustomSource<RenderShaderEffectDefinition> => ({
    kind: 'custom',
    format: 'axrone.shader/effect',
    ...(uri !== undefined ? { uri } : {}),
    data: effect,
});

export const createShaderEffectModuleImporter = (): AssetImporter<AssetShaderImportSchema> => ({
    id: 'asset-shader.effect.module',
    priority: 20,
    sourceKinds: ['custom'],
    canImport: ({ source }) =>
        source.kind === 'custom' &&
        source.format === 'axrone.shader/effect' &&
        isPlainObject(source.data),
    import: ({ source }) => {
        if (source.kind !== 'custom') {
            throw new Error('Shader effect module source must be a custom source');
        }

        const definition = normalizeShaderEffectJsonSource(source, source.data);

        return {
            primary: {
                kind: 'shaderEffect',
                data: definition,
                name: definition.id,
                metadata: source.uri
                    ? {
                          uri: source.uri,
                          mimeType: source.mimeType,
                          properties: {
                              inspectorGroup: INSPECTOR_GROUP_FALLBACK,
                          },
                      }
                    : undefined,
            },
        };
    },
});
