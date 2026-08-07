/**
 * Shader variant system.
 *
 * Declared `keywords` on a `RenderShaderEffectDefinition` define the variant
 * space, mirroring Unity's `multi_compile` / Cocos's `#pragma use` semantics:
 *
 *   - a boolean keyword toggles a feature (`USE_FOG`)
 *   - an enum keyword selects one of a fixed option set (`SURFACE_MODE` with
 *     `OPAQUE` / `BLEND`)
 *
 * Each variant compiles the base effect (via `compileRenderShaderEffect`), then
 * runs the GLSL preprocessor with the keyword's defines so authored `#if` /
 * `#ifdef` blocks resolve to that variant, and finally expands `#include`
 * chunks. This is what lets a single authored effect produce many specialised,
 * driver-ready shader programs.
 */

import { compileRenderShaderEffect } from '@axrone/render-core/shader-effect';
import type {
    CompiledRenderShaderEffect,
    RenderShaderEffectDefinition,
    RenderShaderKeywordDefinition,
} from '@axrone/render-core/shader-effect';
import { type ShaderDiagnostic, createDiagnosticSink } from './diagnostics';
import { expandShaderIncludes } from './library';
import { preprocessGLSL } from './preprocessor';

export type ShaderKeywordSelection = Readonly<Record<string, boolean | string>>;

export type ShaderDefineMap = Readonly<Record<string, string | number | boolean>>;

export interface ShaderVariant {
    readonly id: string;
    readonly key: string;
    readonly selection: ShaderKeywordSelection;
    readonly defines: ShaderDefineMap;
    readonly vertexSource: string;
    readonly fragmentSource: string;
    readonly uniformNames: readonly string[];
    readonly diagnostics: readonly ShaderDiagnostic[];
}

const keywordOptions = (keyword: RenderShaderKeywordDefinition): readonly (boolean | string)[] => {
    if (keyword.options && keyword.options.length > 0) {
        return keyword.options;
    }
    return [false, true];
};

const keywordDefault = (keyword: RenderShaderKeywordDefinition): boolean | string => {
    if (keyword.options && keyword.options.length > 0) {
        if (typeof keyword.defaultValue === 'string') return keyword.defaultValue;
        return keyword.options[0];
    }
    if (typeof keyword.defaultValue === 'boolean') return keyword.defaultValue;
    return false;
};

/** Number of variants implied by a keyword set (product of each option space). */
export const shaderVariantCount = (
    keywords: readonly RenderShaderKeywordDefinition[]
): number => {
    if (keywords.length === 0) return 1;
    return keywords.reduce((total, keyword) => total * keywordOptions(keyword).length, 1);
};

/** Enumerate every keyword selection in the full variant space. */
export const enumerateShaderVariants = (
    keywords: readonly RenderShaderKeywordDefinition[]
): readonly ShaderKeywordSelection[] => {
    if (keywords.length === 0) return [{}];

    let selections: ShaderKeywordSelection[] = [{}];
    for (const keyword of keywords) {
        const next: ShaderKeywordSelection[] = [];
        for (const selection of selections) {
            for (const option of keywordOptions(keyword)) {
                next.push({ ...selection, [keyword.name]: option });
            }
        }
        selections = next;
    }
    return selections;
};

/** The keyword selection implied by each keyword's declared default. */
export const defaultShaderKeywordSelection = (
    keywords: readonly RenderShaderKeywordDefinition[]
): ShaderKeywordSelection => {
    const selection: Record<string, boolean | string> = {};
    for (const keyword of keywords) {
        selection[keyword.name] = keywordDefault(keyword);
    }
    return selection;
};

/**
 * Translate a keyword selection into the preprocessor define map. Boolean
 * keywords define their name to `1`/`0`; enum keywords define every option
 * (the selected one to `1`) plus the keyword name to the selected index, so
 * both `#ifdef OPAQUE` and `#if SURFACE_MODE == 0` styles work.
 */
export const selectionToShaderDefines = (
    keywords: readonly RenderShaderKeywordDefinition[],
    selection: ShaderKeywordSelection
): ShaderDefineMap => {
    const defines: Record<string, string | number | boolean> = {};

    for (const keyword of keywords) {
        const chosen = selection[keyword.name];

        if (keyword.options && keyword.options.length > 0) {
            const selectedIndex = keyword.options.indexOf(chosen as string);
            const resolvedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            const selectedOption = keyword.options[resolvedIndex];
            defines[selectedOption] = 1;
            defines[keyword.name] = resolvedIndex;
            continue;
        }

        defines[keyword.name] = chosen === true ? 1 : 0;
    }

    return defines;
};

/** Stable, sortable key identifying a variant within its effect. */
export const shaderVariantKey = (selection: ShaderKeywordSelection): string =>
    Object.keys(selection)
        .sort()
        .map((name) => `${name}=${selection[name]}`)
        .join('|');

export interface BuildShaderVariantOptions {
    readonly selection?: ShaderKeywordSelection;
    readonly preserveLineMarkers?: boolean;
}

/** Compile a single variant of an effect for a given keyword selection. */
export const buildShaderVariant = (
    effect: RenderShaderEffectDefinition,
    options: BuildShaderVariantOptions = {}
): ShaderVariant => {
    const keywords = effect.keywords ?? [];
    const selection = options.selection ?? defaultShaderKeywordSelection(keywords);
    const defines = selectionToShaderDefines(keywords, selection);
    const sink = createDiagnosticSink();
    const compiled: CompiledRenderShaderEffect = compileRenderShaderEffect(effect);

    const vertexProcessed = preprocessGLSL(compiled.vertexSource, {
        defines,
        sourceId: `${effect.id}.vertex`,
        sink,
        preserveLineMarkers: options.preserveLineMarkers,
    });
    const fragmentProcessed = preprocessGLSL(compiled.fragmentSource, {
        defines,
        sourceId: `${effect.id}.fragment`,
        sink,
        preserveLineMarkers: options.preserveLineMarkers,
    });

    const vertexFinal = expandShaderIncludes(vertexProcessed.code, {
        sink,
        preserveLineMarkers: options.preserveLineMarkers,
    });
    const fragmentFinal = expandShaderIncludes(fragmentProcessed.code, {
        sink,
        preserveLineMarkers: options.preserveLineMarkers,
    });

    return {
        id: effect.id,
        key: shaderVariantKey(selection),
        selection,
        defines,
        vertexSource: vertexFinal.code,
        fragmentSource: fragmentFinal.code,
        uniformNames: compiled.uniformNames,
        diagnostics: sink.diagnostics,
    };
};

/** Compile every variant in the effect's keyword space. */
export const buildShaderVariants = (
    effect: RenderShaderEffectDefinition,
    options: Omit<BuildShaderVariantOptions, 'selection'> = {}
): readonly ShaderVariant[] =>
    enumerateShaderVariants(effect.keywords ?? []).map((selection) =>
        buildShaderVariant(effect, { ...options, selection })
    );
