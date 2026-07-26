/**
 * Shader inheritance and composition.
 *
 * `extendShaderEffect(base, override)` produces a new effect that inherits
 * everything from `base` and applies `override` on top, merging by identity:
 * attributes, varyings, properties, keywords, libraries and techniques merge by
 * name/id (override wins; nested `inspector`/`binding` deep-merge), stage
 * declarations/includes concatenate, and a stage `main` replaces the base when
 * provided. This is the toolkit's answer to shader "parents" / ShaderLab
 * fallbacks / Cocos technique reuse.
 */

import type {
    RenderShaderAttributeDefinition,
    RenderShaderEffectDefinition,
    RenderShaderEffectRenderStateDefinition,
    RenderShaderInterfaceDefinition,
    RenderShaderKeywordDefinition,
    RenderShaderLibraryDefinition,
    RenderShaderPropertyDefinition,
    RenderShaderStageDefinition,
    RenderShaderTechniqueDefinition,
} from '@axrone/render-core/shader-effect';

export type PartialShaderEffectOverride = {
    readonly id?: string;
    readonly version?: 1 | 2;
    readonly attributes?: readonly RenderShaderAttributeDefinition[];
    readonly varyings?: readonly RenderShaderInterfaceDefinition[];
    readonly properties?: readonly RenderShaderPropertyDefinition[];
    readonly keywords?: readonly RenderShaderKeywordDefinition[];
    readonly libraries?: readonly RenderShaderLibraryDefinition[];
    readonly defaultTechnique?: string;
    readonly techniques?: readonly RenderShaderTechniqueDefinition[];
    readonly vertex?: Partial<RenderShaderStageDefinition>;
    readonly fragment?: Partial<RenderShaderStageDefinition>;
    readonly renderState?: RenderShaderEffectRenderStateDefinition;
};

const mergeByName = <T extends { readonly name: string }>(
    base: readonly T[] | undefined,
    override: readonly T[] | undefined
): T[] | undefined => {
    if (!base && !override) return undefined;
    const map = new Map<string, T>();
    for (const entry of base ?? []) map.set(entry.name, entry);
    for (const entry of override ?? []) {
        const existing = map.get(entry.name);
        map.set(entry.name, existing ? ({ ...existing, ...entry } as T) : entry);
    }
    return [...map.values()];
};

const mergeById = <T extends { readonly id: string }>(
    base: readonly T[] | undefined,
    override: readonly T[] | undefined
): T[] | undefined => {
    if (!base && !override) return undefined;
    const map = new Map<string, T>();
    for (const entry of base ?? []) map.set(entry.id, entry);
    for (const entry of override ?? []) map.set(entry.id, entry);
    return [...map.values()];
};

const concatUnique = (
    base: readonly string[] | undefined,
    override: readonly string[] | undefined
): readonly string[] | undefined => {
    if (!base && !override) return undefined;
    const seen = new Set<string>();
    const result: string[] = [];
    for (const value of [...(base ?? []), ...(override ?? [])]) {
        if (!seen.has(value)) {
            seen.add(value);
            result.push(value);
        }
    }
    return result;
};

const mergeStage = (
    base: RenderShaderStageDefinition,
    override: Partial<RenderShaderStageDefinition> | undefined
): RenderShaderStageDefinition => {
    if (!override) return base;
    return {
        version: override.version ?? base.version,
        precision: override.precision ?? base.precision,
        directives: concatUnique(base.directives, override.directives),
        inputs: mergeByName(base.inputs, override.inputs),
        outputs: mergeByName(base.outputs, override.outputs),
        declarations:
            base.declarations || override.declarations
                ? [...(base.declarations ?? []), ...(override.declarations ?? [])]
                : undefined,
        includes: concatUnique(base.includes, override.includes),
        main: override.main && override.main.length > 0 ? override.main : base.main,
    };
};

const mergeRenderState = (
    base: RenderShaderEffectRenderStateDefinition | undefined,
    override: RenderShaderEffectRenderStateDefinition | undefined
): RenderShaderEffectRenderStateDefinition | undefined => {
    if (!base) return override;
    if (!override) return base;
    return { ...base, ...override };
};

/**
 * Compose `override` on top of `base`, returning a fully-formed effect. The
 * result keeps `base.format`/`base.version` unless `override` specifies them.
 */
export const extendShaderEffect = (
    base: RenderShaderEffectDefinition,
    override: PartialShaderEffectOverride
): RenderShaderEffectDefinition => ({
    format: base.format,
    version: override.version ?? base.version,
    id: override.id ?? base.id,
    attributes: mergeByName(base.attributes, override.attributes),
    varyings: mergeByName(base.varyings, override.varyings),
    properties: mergeByName(base.properties, override.properties),
    keywords: mergeByName(base.keywords, override.keywords),
    libraries: mergeById(base.libraries, override.libraries),
    defaultTechnique: override.defaultTechnique ?? base.defaultTechnique,
    techniques: mergeById(base.techniques, override.techniques),
    vertex: mergeStage(base.vertex, override.vertex),
    fragment: mergeStage(base.fragment, override.fragment),
    renderState: mergeRenderState(base.renderState, override.renderState),
});
