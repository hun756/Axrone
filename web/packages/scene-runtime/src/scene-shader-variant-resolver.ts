import type { SceneShaderDefinition } from './types';
import type { SceneShaderResource } from './shader-registry';
import { generateSceneShaderVariantKey } from './scene-shader-factory';

export interface SceneShaderVariantResolverDependencies {
    readonly shaders: {
        get(id: string): SceneShaderResource | undefined;
        getDefinition(id: string): SceneShaderDefinition | undefined;
        getVariant(shaderId: string, variantKey: string): SceneShaderResource | undefined;
        registerVariant(
            shaderId: string,
            variantKey: string,
            resource: SceneShaderResource
        ): void;
    };
    readonly compileVariant: (
        definition: SceneShaderDefinition,
        enabledKeywords: readonly string[]
    ) => SceneShaderResource;
}

export class SceneShaderVariantResolver {
    constructor(
        private readonly _dependencies: SceneShaderVariantResolverDependencies
    ) {}

    resolve(
        shaderId: string,
        enabledKeywords: readonly string[]
    ): SceneShaderResource | undefined {
        if (enabledKeywords.length === 0) {
            return this._dependencies.shaders.get(shaderId);
        }

        const variantKey = generateSceneShaderVariantKey(shaderId, enabledKeywords);

        const cached = this._dependencies.shaders.getVariant(shaderId, variantKey);
        if (cached) {
            return cached;
        }

        const definition = this._dependencies.shaders.getDefinition(shaderId);
        if (!definition) {
            return undefined;
        }

        const variant = this._dependencies.compileVariant(definition, enabledKeywords);
        this._dependencies.shaders.registerVariant(
            shaderId,
            variantKey,
            variant
        );

        return this._dependencies.shaders.getVariant(shaderId, variantKey) ?? variant;
    }
}
