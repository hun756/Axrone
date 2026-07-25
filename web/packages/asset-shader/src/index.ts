export const ASSET_SHADER_CAPABILITY_ID = 'asset/shader';
export const ASSET_SHADER_CAPABILITY_PACKAGE = '@axrone/asset-shader';
export const ASSET_SHADER_OWNER_PACKAGE = '@axrone/asset-core';

const ASSET_SHADER_CAPABILITY = Object.freeze({
    id: ASSET_SHADER_CAPABILITY_ID,
    packageName: ASSET_SHADER_CAPABILITY_PACKAGE,
    ownerPackage: ASSET_SHADER_OWNER_PACKAGE,
});

export type AssetShaderCapability = typeof ASSET_SHADER_CAPABILITY;

export const getAssetShaderCapability = (): AssetShaderCapability => ASSET_SHADER_CAPABILITY;

export type {
    AssetShaderImportKind,
    AssetShaderImportPipelineOptions,
    AssetShaderImportResult,
    AssetShaderImportSchema,
    ShaderEffectJsonSource,
} from './shader-effect-importer';
export {
    createAssetShaderImportPipeline,
    createShaderEffectJsonImporter,
    normalizeShaderEffectJsonSource,
} from './shader-effect-importer';
export {
    attr,
    createShaderEffectModuleImporter,
    defineShaderEffect,
    fragStage,
    glsl,
    prop,
    serializeShaderEffectToJson,
    toShaderEffectSource,
    varying,
    vtxStage,
    type DefineShaderEffectInput,
    type ShaderEffectGlslBlock,
    type ShaderEffectPropertyOptions,
    type ShaderEffectStageOptions,
} from './authoring';
export {
    enumProp,
    extend,
    keyword,
    library,
    pass,
    rangeProp,
    reflect,
    technique,
    toggleProp,
    type RenderShaderPassDefinitionInput,
} from './authoring';
export {
    type ShaderDiagnostic,
    type ShaderDiagnosticLocation,
    type ShaderDiagnosticSeverity,
    type ShaderDiagnosticSink,
    createDiagnosticSink,
    formatShaderDiagnostic,
} from './diagnostics';
export {
    type ShaderPreprocessOptions,
    type ShaderPreprocessResult,
    ShaderPreprocessError,
    preprocessGLSL,
} from './preprocessor';
export {
    type ShaderIncludeOptions,
    type ShaderIncludeResult,
    type ShaderLibraryEntry,
    clearShaderLibraries,
    defineShaderLibrary,
    expandShaderIncludes,
    getShaderLibrary,
    hasShaderLibrary,
    listShaderLibraries,
    registerShaderLibrary,
    resolveShaderLibraries,
} from './library';
export {
    type BuildShaderVariantOptions,
    type ShaderDefineMap,
    type ShaderKeywordSelection,
    type ShaderVariant,
    buildShaderVariant,
    buildShaderVariants,
    defaultShaderKeywordSelection,
    enumerateShaderVariants,
    selectionToShaderDefines,
    shaderVariantCount,
    shaderVariantKey,
} from './variants';
export { extendShaderEffect, type PartialShaderEffectOverride } from './compose';
export {
    type ShaderAttributeReflection,
    type ShaderEffectReflection,
    type ShaderKeywordReflection,
    type ShaderStageReflection,
    type ShaderTechniquePassReflection,
    type ShaderTechniqueReflection,
    type ShaderUniformReflection,
    reflectShaderEffect,
} from './reflection';

export type {
    CompiledRenderShaderEffect,
    RenderShaderAttributeDefinition,
    RenderShaderEffectDefinition,
    RenderShaderEffectRenderStateDefinition,
    RenderShaderInspectorControlDefinition,
    RenderShaderInspectorOptionDefinition,
    RenderShaderInterfaceDefinition,
    RenderShaderLibraryDefinition,
    RenderShaderPropertyDefinition,
    RenderShaderSerializableValue,
    RenderShaderStageDefinition,
    RenderShaderStageName,
    RenderShaderValueType,
} from '@axrone/render-core/shader-effect';
export {
    cloneRenderShaderEffectDefinition,
    compileRenderShaderEffect,
} from '@axrone/render-core/shader-effect';

export * from '@axrone/asset-core';
