import { cloneTextureBinding, decodeSceneValue, encodeSceneValue } from './serialization';
import type {
    SceneMaterialBlendStateDefinition,
    SceneMaterialBlendTargetStateDefinition,
    SceneMaterialDefinition,
    SceneMaterialDepthStencilStateDefinition,
    SceneMaterialHandle,
    SceneMaterialPassDefinition,
    SceneMaterialRasterizerStateDefinition,
    SceneMaterialSurfaceDefinition,
    SceneMaterialSurfaceFeaturesDefinition,
    SceneMaterialSurfaceTextureBindingDefinition,
    SceneMaterialStencilFaceStateDefinition,
    SceneTextureBindingDefinition,
    SceneUniformValue,
} from './types';
import { resolveSurfaceFeatures } from './material-feature-keyword-map';
import type { SceneMaterialObservables } from './material-observables';

export interface SceneMaterialTextureBinding {
    readonly textureId: string;
    readonly samplerId: string | null;
    readonly unit?: number;
}

export interface SceneMaterialTextureSlot {
    readonly uniformName: string;
    readonly binding: SceneMaterialTextureBinding;
    readonly resolvedUnit: number;
}

export interface SceneMaterialResource {
    readonly id: string;
    readonly shaderId: string;
    readonly uniforms: Map<string, SceneUniformValue>;
    readonly textureBindings: Map<string, SceneMaterialTextureBinding>;
    readonly surface: SceneMaterialSurfaceDefinition | null;
    readonly passes: readonly SceneMaterialPassDefinition[];
    readonly keywords: Map<string, { enabled: boolean; source: 'auto' | 'explicit' }>;
}

const cloneSceneValue = <T>(value: T): T => decodeSceneValue(encodeSceneValue(value)) as T;

const cloneSurfaceFeaturesDefinition = (
    definition: SceneMaterialSurfaceFeaturesDefinition | undefined
): SceneMaterialSurfaceFeaturesDefinition | undefined =>
    definition
        ? {
              ...definition,
          }
        : undefined;

const cloneSurfaceTextureBindingDefinition = (
    definition: SceneMaterialSurfaceTextureBindingDefinition | undefined
): SceneMaterialSurfaceTextureBindingDefinition | undefined =>
    definition
        ? {
              ...definition,
              scale: definition.scale
                  ? ([...definition.scale] as readonly [number, number])
                  : undefined,
              offset: definition.offset
                  ? ([...definition.offset] as readonly [number, number])
                  : undefined,
          }
        : undefined;

export const cloneSceneMaterialSurfaceDefinition = (
    definition: SceneMaterialSurfaceDefinition | undefined
): SceneMaterialSurfaceDefinition | undefined =>
    definition
        ? {
              ...definition,
              features: cloneSurfaceFeaturesDefinition(definition.features),
              tilingOffset: definition.tilingOffset
                  ? ([...definition.tilingOffset] as readonly [number, number, number, number])
                  : undefined,
              albedo: definition.albedo
                  ? ([...definition.albedo] as readonly [number, number, number, number])
                  : undefined,
              albedoScale: definition.albedoScale
                  ? ([...definition.albedoScale] as readonly [number, number, number])
                  : undefined,
              emissive: definition.emissive
                  ? ([...definition.emissive] as readonly [number, number, number])
                  : undefined,
              emissiveScale: definition.emissiveScale
                  ? ([...definition.emissiveScale] as readonly [number, number, number])
                  : undefined,
              albedoMap: cloneSurfaceTextureBindingDefinition(definition.albedoMap),
              normalMap: cloneSurfaceTextureBindingDefinition(definition.normalMap),
              pbrMap: cloneSurfaceTextureBindingDefinition(definition.pbrMap),
              metallicRoughnessMap: cloneSurfaceTextureBindingDefinition(
                  definition.metallicRoughnessMap
              ),
              occlusionMap: cloneSurfaceTextureBindingDefinition(definition.occlusionMap),
              emissiveMap: cloneSurfaceTextureBindingDefinition(definition.emissiveMap),
              clearcoatMap: cloneSurfaceTextureBindingDefinition(definition.clearcoatMap),
              clearcoatRoughnessMap: cloneSurfaceTextureBindingDefinition(
                  definition.clearcoatRoughnessMap
              ),
              clearcoatNormalMap: cloneSurfaceTextureBindingDefinition(
                  definition.clearcoatNormalMap
              ),
          }
        : undefined;

const cloneStencilFaceStateDefinition = (
    definition: SceneMaterialStencilFaceStateDefinition | undefined
): SceneMaterialStencilFaceStateDefinition | undefined =>
    definition
        ? {
              ...definition,
          }
        : undefined;

const cloneRasterizerStateDefinition = (
    definition: SceneMaterialRasterizerStateDefinition | undefined
): SceneMaterialRasterizerStateDefinition | undefined =>
    definition
        ? {
              ...definition,
          }
        : undefined;

const cloneDepthStencilStateDefinition = (
    definition: SceneMaterialDepthStencilStateDefinition | undefined
): SceneMaterialDepthStencilStateDefinition | undefined =>
    definition
        ? {
              ...definition,
              front: cloneStencilFaceStateDefinition(definition.front),
              back: cloneStencilFaceStateDefinition(definition.back),
          }
        : undefined;

const cloneBlendTargetStateDefinition = (
    definition: SceneMaterialBlendTargetStateDefinition
): SceneMaterialBlendTargetStateDefinition => ({
    ...definition,
    colorWriteMask: definition.colorWriteMask
        ? ([...definition.colorWriteMask] as readonly [boolean, boolean, boolean, boolean])
        : undefined,
});

const cloneBlendStateDefinition = (
    definition: SceneMaterialBlendStateDefinition | undefined
): SceneMaterialBlendStateDefinition | undefined =>
    definition
        ? {
              ...definition,
              blendColor: definition.blendColor
                  ? ([...definition.blendColor] as readonly [number, number, number, number])
                  : undefined,
              targets: definition.targets
                  ? Object.freeze(definition.targets.map(cloneBlendTargetStateDefinition))
                  : undefined,
          }
        : undefined;

const cloneSceneMaterialPassDefinition = (
    definition: SceneMaterialPassDefinition
): SceneMaterialPassDefinition => ({
    ...definition,
    rasterizerState: cloneRasterizerStateDefinition(definition.rasterizerState),
    depthStencilState: cloneDepthStencilStateDefinition(definition.depthStencilState),
    blendState: cloneBlendStateDefinition(definition.blendState),
});

const cloneSceneMaterialPassDefinitions = (
    definitions: readonly SceneMaterialPassDefinition[] | undefined
): readonly SceneMaterialPassDefinition[] | undefined =>
    definitions
        ? Object.freeze(definitions.map(cloneSceneMaterialPassDefinition))
        : undefined;

const compareTextureBindings = (
    left: readonly [string, SceneMaterialTextureBinding],
    right: readonly [string, SceneMaterialTextureBinding]
): number => {
    const leftUnit = left[1].unit ?? Number.MAX_SAFE_INTEGER;
    const rightUnit = right[1].unit ?? Number.MAX_SAFE_INTEGER;
    return leftUnit - rightUnit || left[0].localeCompare(right[0]);
};

const toHandle = (material: SceneMaterialResource): SceneMaterialHandle => ({
    id: material.id,
    shaderId: material.shaderId,
    textureBindings: [...material.textureBindings.keys()],
    passIds: material.passes.map((pass) => pass.id),
});

const createTextureSlots = (
    material: SceneMaterialResource
): readonly SceneMaterialTextureSlot[] => {
    const assignments = [...material.textureBindings.entries()].sort(compareTextureBindings);
    const usedUnits = new Set<number>();
    const slots: SceneMaterialTextureSlot[] = [];
    let nextUnit = 0;

    for (const [uniformName, binding] of assignments) {
        let resolvedUnit = binding.unit;
        if (resolvedUnit === undefined) {
            while (usedUnits.has(nextUnit)) {
                nextUnit += 1;
            }
            resolvedUnit = nextUnit;
        }

        usedUnits.add(resolvedUnit);
        slots.push(
            Object.freeze({
                uniformName,
                binding,
                resolvedUnit,
            })
        );
    }

    return Object.freeze(slots);
};

export const normalizeSceneTextureBinding = (
    binding: SceneTextureBindingDefinition
): SceneMaterialTextureBinding => {
    if (typeof binding === 'string') {
        return {
            textureId: binding,
            samplerId: null,
        };
    }

    return {
        textureId: binding.textureId,
        samplerId: binding.samplerId ?? null,
        unit: binding.unit,
    };
};

const SURFACE_MAP_TO_UNIFORM: ReadonlyArray<{
    surfaceKey: keyof Pick<
        SceneMaterialSurfaceDefinition,
        | 'albedoMap'
        | 'normalMap'
        | 'metallicRoughnessMap'
        | 'occlusionMap'
        | 'emissiveMap'
        | 'clearcoatMap'
        | 'clearcoatRoughnessMap'
        | 'clearcoatNormalMap'
    >;
    uniformName: string;
}> = Object.freeze([
    { surfaceKey: 'albedoMap', uniformName: '_BaseColorTexture' },
    { surfaceKey: 'normalMap', uniformName: '_NormalTexture' },
    { surfaceKey: 'metallicRoughnessMap', uniformName: '_MetallicRoughnessTexture' },
    { surfaceKey: 'occlusionMap', uniformName: '_OcclusionTexture' },
    { surfaceKey: 'emissiveMap', uniformName: '_EmissiveTexture' },
    { surfaceKey: 'clearcoatMap', uniformName: '_ClearcoatTexture' },
    { surfaceKey: 'clearcoatRoughnessMap', uniformName: '_ClearcoatRoughnessTexture' },
    { surfaceKey: 'clearcoatNormalMap', uniformName: '_ClearcoatNormalTexture' },
]);

const bridgeSurfaceTextureMaps = (resource: SceneMaterialResource): void => {
    const surface = resource.surface;
    if (!surface) {
        return;
    }

    for (const entry of SURFACE_MAP_TO_UNIFORM) {
        const map = surface[entry.surfaceKey] as SceneMaterialSurfaceTextureBindingDefinition | undefined;
        if (!map?.textureId) {
            continue;
        }

        if (!resource.textureBindings.has(entry.uniformName)) {
            resource.textureBindings.set(entry.uniformName, {
                textureId: map.textureId,
                samplerId: map.samplerId ?? null,
                unit: map.unit,
            });
        }

        const texCoord = map.texCoord ?? 0;
        resource.uniforms.set(`${entry.uniformName}_TexCoord`, texCoord);

        if (map.scale || map.offset) {
            const sx = map.scale?.[0] ?? 1;
            const sy = map.scale?.[1] ?? 1;
            const ox = map.offset?.[0] ?? 0;
            const oy = map.offset?.[1] ?? 0;
            resource.uniforms.set(`${entry.uniformName}_ST`, Object.freeze([sx, sy, ox, oy]));
        }

        if (map.rotation !== undefined) {
            resource.uniforms.set(`${entry.uniformName}_Rotation`, map.rotation);
        }
    }
};

export const cloneSceneMaterialDefinition = (
    definition: SceneMaterialDefinition
): SceneMaterialDefinition => ({
    id: definition.id,
    shaderId: definition.shaderId,
    uniforms: definition.uniforms
        ? Object.fromEntries(
              Object.entries(definition.uniforms).map(([name, value]) => [
                  name,
                  cloneSceneValue(value),
              ])
          )
        : undefined,
    textures: definition.textures
        ? Object.fromEntries(
              Object.entries(definition.textures).map(([name, binding]) => [
                  name,
                  cloneTextureBinding(binding),
              ])
          )
        : undefined,
    surface: cloneSceneMaterialSurfaceDefinition(definition.surface),
    passes: cloneSceneMaterialPassDefinitions(definition.passes),
});

export const resolveSceneMaterialPass = (
    material: Pick<SceneMaterialResource, 'passes'>,
    materialPassId: string | null | undefined
): SceneMaterialPassDefinition | null => {
    const passes = material.passes ?? [];

    if (materialPassId === null || materialPassId === undefined) {
        return passes[0] ?? null;
    }

    return passes.find((pass) => pass.id === materialPassId) ?? null;
};

/**
 * Property aliases for common uniform names.
 * Mirrors MaterialInstance's alias table in render-webgl2.
 * Note: These aliases target engine built-in shader conventions and may not
 * apply to glTF-loaded materials which use different uniform naming.
 */
const SCENE_MATERIAL_ALIASES: Readonly<Record<string, string>> = Object.freeze({
    mainTexture: 'u_MainTexture',
    color: 'u_Color',
    tint: 'u_Color',
    albedo: 'u_AlbedoColor',
    emission: 'u_EmissionColor',
    metallic: 'u_Metallic',
    roughness: 'u_Roughness',
    normalScale: 'u_NormalScale',
    emissionIntensity: 'u_EmissionIntensity',
});

const resolveAlias = (name: string): string => SCENE_MATERIAL_ALIASES[name] ?? name;

export class SceneMaterialRegistry {
    private readonly _resources = new Map<string, SceneMaterialResource>();
    private readonly _definitions = new Map<string, SceneMaterialDefinition>();
    private readonly _handles = new Map<string, SceneMaterialHandle>();
    private readonly _textureSlots = new Map<string, readonly SceneMaterialTextureSlot[]>();
    private readonly _observables: SceneMaterialObservables | null;

    constructor(options?: { observables?: SceneMaterialObservables }) {
        this._observables = options?.observables ?? null;
    }

    get size(): number {
        return this._resources.size;
    }

    create(definition: SceneMaterialDefinition): SceneMaterialHandle {
        return this._createInternal(definition, false);
    }

    /** @internal — when suppressCreated is true, skips materialCreated notification (used by clone). */
    private _createInternal(
        definition: SceneMaterialDefinition,
        suppressCreated: boolean
    ): SceneMaterialHandle {
        const resource: SceneMaterialResource = {
            id: definition.id,
            shaderId: definition.shaderId,
            uniforms: new Map(Object.entries(definition.uniforms ?? {})),
            textureBindings: new Map(
                Object.entries(definition.textures ?? {}).map(([name, binding]) => [
                    name,
                    normalizeSceneTextureBinding(binding),
                ])
            ),
            surface: cloneSceneMaterialSurfaceDefinition(definition.surface) ?? null,
            passes: cloneSceneMaterialPassDefinitions(definition.passes) ?? Object.freeze([]),
            keywords: new Map(),
        };

        if (this._resources.has(definition.id)) {
            console.warn(
                `[SceneMaterialRegistry] Material '${definition.id}' is already registered. Overwriting existing material. This may cause unexpected rendering behavior.`
            );
            this._observables?._notifyMaterialDeleted(definition.id);
        }

        this._resources.set(resource.id, resource);
        this._definitions.set(resource.id, cloneSceneMaterialDefinition(definition));

        // Auto-sync surface features to keywords
        if (resource.surface?.features) {
            const resolved = resolveSurfaceFeatures(resource.surface.features);
            for (const [keyword, enabled] of Object.entries(resolved)) {
                resource.keywords.set(keyword, { enabled, source: 'auto' });
            }
        }

        // Bridge surface texture maps → texture bindings + TexCoord/ST/Rotation uniforms.
        // The glTF importer populates both `textures` and uniforms directly, but the
        // Editor material-graph path only sets surface.<map>Map. Without this bridge
        // the shader's `if (_BaseColorTexture_TexCoord >= 0)` guard stays at -1 and
        // the texture is never sampled.
        bridgeSurfaceTextureMaps(resource);

        const handle = toHandle(resource);
        this._handles.set(resource.id, handle);
        this._textureSlots.set(resource.id, createTextureSlots(resource));
        if (!suppressCreated) {
            this._observables?._notifyMaterialCreated(resource.id);
        }
        return handle;
    }

    get(id: string): SceneMaterialResource | undefined {
        return this._resources.get(id);
    }

    getHandle(id: string): SceneMaterialHandle | null {
        return this._handles.get(id) ?? null;
    }

    setUniform(id: string, name: string, value: SceneUniformValue): boolean {
        const material = this._resources.get(id);
        if (!material) {
            return false;
        }

        const resolvedName = resolveAlias(name);
        material.uniforms.set(resolvedName, value);
        const definition = this._definitions.get(id);
        if (definition) {
            const uniforms = { ...(definition.uniforms ?? {}) };
            uniforms[resolvedName] = cloneSceneValue(value);
            this._definitions.set(id, {
                ...definition,
                uniforms,
            });
        }

        this._observables?._notifyUniformChanged(id, resolvedName);
        return true;
    }

    getUniform(id: string, name: string): SceneUniformValue | null {
        const material = this._resources.get(id);
        if (!material) {
            return null;
        }
        return material.uniforms.get(resolveAlias(name)) ?? null;
    }

    setUniforms(id: string, uniforms: Readonly<Record<string, SceneUniformValue>>): boolean {
        const material = this._resources.get(id);
        if (!material) {
            return false;
        }

        for (const [name, value] of Object.entries(uniforms)) {
            const resolvedName = resolveAlias(name);
            material.uniforms.set(resolvedName, value);
            const definition = this._definitions.get(id);
            if (definition) {
                const defUniforms = { ...(definition.uniforms ?? {}) };
                defUniforms[resolvedName] = cloneSceneValue(value);
                this._definitions.set(id, {
                    ...definition,
                    uniforms: defUniforms,
                });
            }
            this._observables?._notifyUniformChanged(id, resolvedName);
        }

        return true;
    }

    getUniforms(id: string): Readonly<Record<string, SceneUniformValue>> | null {
        const material = this._resources.get(id);
        if (!material) {
            return null;
        }
        return Object.freeze(Object.fromEntries(material.uniforms));
    }

    setTexture(id: string, name: string, binding: SceneTextureBindingDefinition): boolean {
        const material = this._resources.get(id);
        if (!material) {
            return false;
        }

        material.textureBindings.set(name, normalizeSceneTextureBinding(binding));
        const definition = this._definitions.get(id);
        if (definition) {
            this._definitions.set(id, {
                ...definition,
                textures: {
                    ...(definition.textures ?? {}),
                    [name]: cloneTextureBinding(binding),
                },
            });
        }

        this._handles.set(id, toHandle(material));
        this._textureSlots.set(id, createTextureSlots(material));
        this._observables?._notifyTextureChanged(id, name);

        return true;
    }

    delete(id: string): boolean {
        const existed = this._resources.has(id);
        this._resources.delete(id);
        this._definitions.delete(id);
        this._handles.delete(id);
        this._textureSlots.delete(id);
        if (existed) {
            this._observables?._notifyMaterialDeleted(id);
        }
        return existed;
    }

    clone(sourceId: string, newId: string): SceneMaterialHandle {
        const source = this._resources.get(sourceId);
        if (!source) {
            throw new Error(`Material '${sourceId}' is not registered`);
        }
        if (this._resources.has(newId)) {
            throw new Error(`Material '${newId}' is already registered`);
        }

        const definition: SceneMaterialDefinition = {
            id: newId,
            shaderId: source.shaderId,
            uniforms:
                source.uniforms.size > 0
                    ? Object.fromEntries(
                          [...source.uniforms.entries()].map(([k, v]) => [
                              k,
                              cloneSceneValue(v),
                          ])
                      )
                    : undefined,
            textures:
                source.textureBindings.size > 0
                    ? Object.fromEntries(
                          [...source.textureBindings.entries()].map(([k, v]) => [
                              k,
                              {
                                  textureId: v.textureId,
                                  samplerId: v.samplerId ?? undefined,
                                  unit: v.unit,
                              },
                          ])
                      )
                    : undefined,
            surface: cloneSceneMaterialSurfaceDefinition(source.surface ?? undefined),
            passes:
                source.passes.length > 0
                    ? cloneSceneMaterialPassDefinitions(source.passes)
                    : undefined,
        };

        const handle = this._createInternal(definition, true);

        const clonedResource = this._resources.get(newId);
        if (clonedResource && source.keywords.size > 0) {
            for (const [keyword, entry] of source.keywords) {
                clonedResource.keywords.set(keyword, { enabled: entry.enabled, source: entry.source });
            }
        }

        this._observables?._notifyMaterialCloned(sourceId, newId);
        return handle;
    }

    has(id: string): boolean {
        return this._resources.has(id);
    }

    getMaterialIds(): readonly string[] {
        return Object.freeze([...this._resources.keys()]);
    }

    setKeyword(id: string, keyword: string, enabled: boolean): boolean {
        const material = this._resources.get(id);
        if (!material) {
            return false;
        }
        material.keywords.set(keyword, { enabled, source: 'explicit' });
        this._observables?._notifyKeywordChanged(id, keyword, enabled);
        return true;
    }

    toggleKeyword(id: string, keyword: string): boolean {
        const material = this._resources.get(id);
        if (!material) {
            return false;
        }
        const entry = material.keywords.get(keyword);
        const current = entry?.enabled ?? false;
        material.keywords.set(keyword, { enabled: !current, source: 'explicit' });
        this._observables?._notifyKeywordChanged(id, keyword, !current);
        return true;
    }

    getKeyword(id: string, keyword: string): boolean | null {
        const material = this._resources.get(id);
        if (!material) {
            return null;
        }
        return material.keywords.get(keyword)?.enabled ?? false;
    }

    getEnabledKeywords(id: string): readonly string[] {
        const material = this._resources.get(id);
        if (!material) {
            return Object.freeze([]);
        }
        const enabled: string[] = [];
        for (const [keyword, entry] of material.keywords) {
            if (entry.enabled) {
                enabled.push(keyword);
            }
        }
        return Object.freeze(enabled);
    }

    getTextureSlots(id: string): readonly SceneMaterialTextureSlot[] {
        return this._textureSlots.get(id) ?? Object.freeze([]);
    }

    getDefinitions(): readonly SceneMaterialDefinition[] {
        return [...this._definitions.values()].map((definition) =>
            cloneSceneMaterialDefinition(definition)
        );
    }

    clear(): void {
        if (this._observables) {
            for (const id of this._resources.keys()) {
                this._observables._notifyMaterialDeleted(id);
            }
        }
        this._resources.clear();
        this._definitions.clear();
        this._handles.clear();
        this._textureSlots.clear();
    }
}
