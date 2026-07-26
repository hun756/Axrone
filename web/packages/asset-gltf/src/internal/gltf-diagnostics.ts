export type GltfDiagnosticSeverity = 'error' | 'warning' | 'info';

export type GltfDiagnosticDomain = 'animation' | 'texture' | 'material' | 'mesh' | 'skin' | 'light' | 'camera' | 'extension' | 'schema' | 'prefab' | 'node';

export type GltfDiagnosticCode = `gltf.${GltfDiagnosticDomain}.${string}`;

export interface GltfDiagnostic<TCode extends GltfDiagnosticCode = GltfDiagnosticCode> {
    readonly level: GltfDiagnosticSeverity;
    readonly code: TCode;
    readonly message: string;
}

export const createDiagnostic = <TCode extends GltfDiagnosticCode>(
    level: GltfDiagnosticSeverity,
    code: TCode,
    message: string
): GltfDiagnostic<TCode> => Object.freeze({ level, code, message });

export const createWarning = <TCode extends GltfDiagnosticCode>(
    code: TCode,
    message: string
): GltfDiagnostic<TCode> => createDiagnostic('warning', code, message);

export const createError = <TCode extends GltfDiagnosticCode>(
    code: TCode,
    message: string
): GltfDiagnostic<TCode> => createDiagnostic('error', code, message);

export interface GltfValidationResult<T> {
    readonly value: T;
    readonly diagnostics: readonly GltfDiagnostic[];
}

export const validationResult = <T>(
    value: T,
    diagnostics: readonly GltfDiagnostic[] = []
): GltfValidationResult<T> => Object.freeze({ value, diagnostics });

export const withDiagnostic = <T>(
    result: GltfValidationResult<T>,
    diagnostic: GltfDiagnostic
): GltfValidationResult<T> =>
    Object.freeze({
        value: result.value,
        diagnostics: Object.freeze([...result.diagnostics, diagnostic]),
    });

export const mergeValidationResults = <T>(
    results: readonly GltfValidationResult<T>[]
): GltfValidationResult<readonly T[]> => {
    const values: T[] = [];
    const diagnostics: GltfDiagnostic[] = [];
    for (const result of results) {
        values.push(result.value);
        diagnostics.push(...result.diagnostics);
    }
    return Object.freeze({
        value: Object.freeze(values),
        diagnostics: Object.freeze(diagnostics),
    });
};

export type AnimationMetadataDiagnosticCode =
    | 'gltf.animation.metadata.invalid'
    | 'gltf.animation.manifest.invalid'
    | 'gltf.animation.parameter.invalid'
    | 'gltf.animation.layer.invalid'
    | 'gltf.animation.statemachine.invalid'
    | 'gltf.animation.ik.invalid'
    | 'gltf.animation.rootmotion.invalid'
    | 'gltf.animation.clip.invalid'
    | 'gltf.animation.feature.invalid';

export type TextureDiagnosticCode =
    | 'gltf.texture.missing-source'
    | 'gltf.texture.transcode.failed'
    | 'gltf.texture.format.unsupported';

export type MaterialDiagnosticCode =
    | 'gltf.material.texture.missing'
    | 'gltf.material.shader.invalid';

export type ExtensionDiagnosticCode =
    | 'gltf.extension.unsupported'
    | 'gltf.extension.required.missing';

export type PrefabDiagnosticCode =
    | 'gltf.light.directional.runtime-limit'
    | 'gltf.light.local.runtime-limit'
    | 'gltf.prefab.node.missing';

export type GltfKnownDiagnosticCode =
    | AnimationMetadataDiagnosticCode
    | TextureDiagnosticCode
    | MaterialDiagnosticCode
    | ExtensionDiagnosticCode
    | PrefabDiagnosticCode;

export const ANIMATION_METADATA_DIAGNOSTIC = 'gltf.animation.metadata.invalid' as const;
export const ANIMATION_MANIFEST_DIAGNOSTIC = 'gltf.animation.manifest.invalid' as const;
