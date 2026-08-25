import type { IGLStateCache, ITextureSampler } from '@axrone/render-webgl2';
import type { SceneMaterialResource } from './material-registry';
import type { SceneShaderResource } from './shader-registry';
import type { SceneTextureResource } from './texture-registry';
import type { SceneUniformValue } from './types';

export interface SceneMaterialTextureBinderResources {
    readonly materials: {
        getTextureSlots(materialId: string): readonly {
            readonly uniformName: string;
            readonly binding: {
                readonly textureId: string;
                readonly samplerId: string | null;
            };
            readonly resolvedUnit: number;
        }[];
    };
    readonly textures: {
        get(textureId: string): SceneTextureResource | undefined;
    };
    resolveSampler(id: string | null): ITextureSampler;
}

export type SceneMaterialTextureUniformSetter = (
    shader: SceneShaderResource,
    name: string,
    value: SceneUniformValue | null | undefined
) => void;

export class SceneMaterialTextureBinder {
    private readonly _boundUnits: number[] = [];
    private readonly _state: IGLStateCache | null;
    private readonly _gl: WebGL2RenderingContext;

    constructor(gl: WebGL2RenderingContext, state?: IGLStateCache) {
        this._gl = gl;
        this._state = state ?? null;
    }

    bind(
        shader: SceneShaderResource,
        material: SceneMaterialResource,
        resources: SceneMaterialTextureBinderResources,
        setUniform: SceneMaterialTextureUniformSetter
    ): readonly number[] {
        this._boundUnits.length = 0;

        for (const slot of resources.materials.getTextureSlots(material.id)) {
            const texture = resources.textures.get(slot.binding.textureId);
            if (!texture) {
                continue;
            }

            this._boundUnits.push(slot.resolvedUnit);
            texture.texture.bind(slot.resolvedUnit);

            const sampler = resources.resolveSampler(slot.binding.samplerId ?? texture.samplerId);
            sampler.bind(slot.resolvedUnit);
            setUniform(shader, slot.uniformName, slot.resolvedUnit);
        }

        return this._boundUnits;
    }

    unbind(): void {
        if (this._state) {
            for (let index = 0; index < this._boundUnits.length; index += 1) {
                const unit = this._boundUnits[index]!;
                this._state.bindSampler(unit, null);
                this._state.activeTexture(unit);
                this._state.bindTexture(this._gl.TEXTURE_2D, null);
            }
        } else {
            for (let index = 0; index < this._boundUnits.length; index += 1) {
                const unit = this._boundUnits[index]!;
                this._gl.bindSampler(unit, null);
                this._gl.activeTexture(this._gl.TEXTURE0 + unit);
                this._gl.bindTexture(this._gl.TEXTURE_2D, null);
            }
        }

        this._boundUnits.length = 0;
    }
}
