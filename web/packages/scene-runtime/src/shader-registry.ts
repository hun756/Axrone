import type {
    SceneMeshSemantic,
    SceneShaderDefinition,
    SceneShaderHandle,
} from './types';
import { cloneRenderShaderEffectDefinition } from '@axrone/render-core/shader-effect';

export interface SceneShaderResource {
    readonly id: string;
    readonly program: WebGLProgram;
    readonly uniformLocations: ReadonlyMap<string, WebGLUniformLocation>;
    readonly uniformTypes: ReadonlyMap<string, number>;
    readonly uniformNames: readonly string[];
    readonly attributeNames: Readonly<Record<SceneMeshSemantic, string>>;
    readonly depthTest: boolean;
    readonly cull: boolean;
    readonly blend: boolean;
}

export interface SceneShaderRegistrationResult {
    readonly handle: SceneShaderHandle;
    readonly previous: SceneShaderResource | null;
}

const toHandle = (resource: SceneShaderResource): SceneShaderHandle => ({
    id: resource.id,
    uniformNames: resource.uniformNames,
});

export const cloneSceneShaderDefinition = (
    definition: SceneShaderDefinition
): SceneShaderDefinition => ({
    ...definition,
    uniforms: definition.uniforms ? [...definition.uniforms] : undefined,
    attributes: definition.attributes ? { ...definition.attributes } : undefined,
    effect: definition.effect
        ? cloneRenderShaderEffectDefinition(definition.effect)
        : undefined,
});

export class SceneShaderRegistry {
    private readonly _resources = new Map<string, SceneShaderResource>();
    private readonly _definitions = new Map<string, SceneShaderDefinition>();
    private readonly _variants = new Map<string, Map<string, SceneShaderResource>>();

    get size(): number {
        return this._resources.size;
    }

    get variantCount(): number {
        let count = 0;
        for (const variants of this._variants.values()) {
            count += variants.size;
        }
        return count;
    }

    register(
        definition: SceneShaderDefinition,
        resource: SceneShaderResource
    ): SceneShaderRegistrationResult {
        const previous = this._resources.get(resource.id) ?? null;
        this._resources.set(resource.id, resource);
        this._definitions.set(resource.id, cloneSceneShaderDefinition(definition));

        return {
            handle: toHandle(resource),
            previous,
        };
    }

    registerVariant(
        shaderId: string,
        variantKey: string,
        resource: SceneShaderResource
    ): void {
        let variants = this._variants.get(shaderId);
        if (!variants) {
            variants = new Map();
            this._variants.set(shaderId, variants);
        }
        variants.set(variantKey, resource);
    }

    get(id: string): SceneShaderResource | undefined {
        return this._resources.get(id);
    }

    getVariant(shaderId: string, variantKey: string): SceneShaderResource | undefined {
        return this._variants.get(shaderId)?.get(variantKey);
    }

    getDefinition(id: string): SceneShaderDefinition | undefined {
        const definition = this._definitions.get(id);
        return definition ? cloneSceneShaderDefinition(definition) : undefined;
    }

    getHandle(id: string): SceneShaderHandle | null {
        const resource = this._resources.get(id);
        return resource ? toHandle(resource) : null;
    }

    getResources(): readonly SceneShaderResource[] {
        return [...this._resources.values()];
    }

    getDefinitions(): readonly SceneShaderDefinition[] {
        return [...this._definitions.values()].map((definition) =>
            cloneSceneShaderDefinition(definition)
        );
    }

    clear(): readonly SceneShaderResource[] {
        const resources = [...this._resources.values()];
        this._resources.clear();
        this._definitions.clear();
        this._variants.clear();
        return resources;
    }

    clearVariants(): void {
        this._variants.clear();
    }

    clearVariantsForShader(shaderId: string): boolean {
        return this._variants.delete(shaderId);
    }
}
