export * from './interfaces';
export * from './utils';
export * from './errors';
export * from './uniform-cache';
export * from './pool';

export { WebGLShaderCompiler } from './compiler';
export { ShaderManager } from './manager';

export { ShaderInstance } from './instance';
export { MaterialInstance } from './material';

export {
    ShaderInstanceManager,
} from './shader-instance';
export type {
    ShaderInstanceManagerOptions,
    ShaderInstanceManagerStats,
    BorrowedShaderInstance,
} from './shader-instance';
export type {
    ShaderInstancePoolOptions,
    ShaderInstancePoolStats,
} from './pool';
export type {
    UniformDescriptorLite,
    UniformValueDescriptor,
    UniformScalarValue,
} from './uniform-cache';

export {
    generateVersionDirective,
    generatePrecisionDirective,
    generateDefines,
    hashShaderSource,
    generateVariantKey,
    getShaderDataTypeSize,
    getShaderDataTypeComponentCount,
    getWebGLType,
    calculateUniformBufferLayout,
    VERTEX_SEMANTICS,
    UNIFORM_SEMANTICS,
    SHADER_KEYWORDS,
    MAX_VERTEX_ATTRIBUTES,
    MAX_TEXTURE_UNITS,
    SHADER_CACHE_LIMITS,
} from './utils';

export {
    ShaderDataType,
    ShaderQualifier,
    ShaderStage,
    BlendMode,
    CullMode,
    DepthFunc,
} from './interfaces';
