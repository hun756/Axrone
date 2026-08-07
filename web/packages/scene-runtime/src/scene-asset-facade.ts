import type { ComponentRegistry } from '@axrone/ecs-runtime';
import type { TextureFormat } from '@axrone/render-webgl2';
import { SceneMaterialError } from './errors';
import type { SceneMaterialObservables } from './material-observables';
import type { SceneMaterialInstanceAdapter } from './scene-material-instance-adapter';
import type {
    SceneMaterialDefinition,
    SceneMaterialHandle,
    SceneMaterialTextureBindingHandle,
    SceneMeshDefinition,
    SceneMeshHandle,
    SceneOptions,
    SceneRenderPassDefinition,
    SceneRenderPassHandle,
    SceneSamplerDefinition,
    SceneSamplerHandle,
    SceneShaderDefinition,
    SceneShaderHandle,
    SceneTextureBindingDefinition,
    SceneTextureDefinition,
    SceneTextureHandle,
    SceneTextureResourceHandle,
    SceneUniformValue,
} from './types';
import { SceneLifecycleFacade } from './scene-lifecycle-facade';

export class SceneAssetFacade<
    R extends ComponentRegistry = Record<string, never>,
> extends SceneLifecycleFacade<R> {
    constructor(options: SceneOptions<R> = {}) {
        super(options);
    }

    registerShader(definition: SceneShaderDefinition): SceneShaderHandle {
        this.assertNotDisposed();
        return this._kernel.assets.registerShader(definition);
    }

    getShader(id: string): SceneShaderHandle | null {
        return this._kernel.assets.getShader(id);
    }

    createMaterial(definition: SceneMaterialDefinition): SceneMaterialHandle {
        this.assertNotDisposed();
        try {
            return this._kernel.assets.createMaterial(definition);
        } catch (error) {
            if (error instanceof SceneMaterialError) {
                throw error;
            }

            throw new SceneMaterialError(
                `Failed to create material '${definition.id}'`,
                error instanceof Error ? error : undefined
            );
        }
    }

    setMaterialUniform(materialId: string, name: string, value: SceneUniformValue): this {
        this.assertNotDisposed();
        if (!this._kernel.assets.setMaterialUniform(materialId, name, value)) {
            throw new SceneMaterialError(`Material '${materialId}' is not registered`);
        }

        return this;
    }

    setMaterialTexture(
        materialId: string,
        name: string,
        binding: SceneTextureBindingDefinition
    ): this {
        this.assertNotDisposed();
        if (!this._kernel.assets.setMaterialTexture(materialId, name, binding)) {
            throw new SceneMaterialError(`Material '${materialId}' is not registered`);
        }

        return this;
    }

    getMaterial(materialId: string): SceneMaterialHandle | null {
        return this._kernel.assets.getMaterial(materialId);
    }

    registerMesh(definition: SceneMeshDefinition): SceneMeshHandle {
        this.assertNotDisposed();
        return this._kernel.assets.registerMesh(definition);
    }

    getMesh(id: string): SceneMeshHandle | null {
        return this._kernel.assets.getMesh(id);
    }

    registerSampler(definition: SceneSamplerDefinition): SceneSamplerHandle {
        this.assertNotDisposed();
        return this._kernel.assets.registerSampler(definition);
    }

    getSampler(id: string): SceneSamplerHandle | null {
        return this._kernel.assets.getSampler(id);
    }

    async registerTexture(definition: SceneTextureDefinition): Promise<SceneTextureHandle> {
        this.assertNotDisposed();
        return await this._kernel.assets.registerTexture(definition);
    }

    getTexture(id: string): SceneTextureHandle | null {
        return this._kernel.assets.getTexture(id);
    }

    getTextureResource(id: string): SceneTextureResourceHandle | null {
        return this._kernel.assets.getTextureResource(id);
    }

    getSupportedCompressedTextureFormats(
        preferredFormats?: readonly TextureFormat[]
    ): readonly TextureFormat[] {
        this.assertNotDisposed();
        return this._kernel.assets.getSupportedCompressedTextureFormats(preferredFormats);
    }

    getMaterialTextureBindings(materialId: string): readonly SceneMaterialTextureBindingHandle[] {
        return this._kernel.assets.getMaterialTextureBindings(materialId);
    }

    getMaterialTextureBinding(
        materialId: string,
        uniformName?: string
    ): SceneMaterialTextureBindingHandle | null {
        return this._kernel.assets.getMaterialTextureBinding(materialId, uniformName);
    }

    deleteMaterial(materialId: string): this {
        this.assertNotDisposed();
        if (!this._kernel.assets.deleteMaterial(materialId)) {
            throw new SceneMaterialError(`Material '${materialId}' is not registered`);
        }

        return this;
    }

    cloneMaterial(sourceId: string, newId: string): SceneMaterialHandle {
        this.assertNotDisposed();
        try {
            return this._kernel.assets.cloneMaterial(sourceId, newId);
        } catch (error) {
            if (error instanceof SceneMaterialError) {
                throw error;
            }

            throw new SceneMaterialError(
                `Failed to clone material '${sourceId}' as '${newId}'`,
                error instanceof Error ? error : undefined
            );
        }
    }

    hasMaterial(materialId: string): boolean {
        return this._kernel.assets.hasMaterial(materialId);
    }

    getMaterialIds(): readonly string[] {
        return this._kernel.assets.getMaterialIds();
    }

    setMaterialKeyword(materialId: string, keyword: string, enabled: boolean): this {
        this.assertNotDisposed();
        if (!this._kernel.assets.setMaterialKeyword(materialId, keyword, enabled)) {
            throw new SceneMaterialError(`Material '${materialId}' is not registered`);
        }
        return this;
    }

    toggleMaterialKeyword(materialId: string, keyword: string): this {
        this.assertNotDisposed();
        if (!this._kernel.assets.toggleMaterialKeyword(materialId, keyword)) {
            throw new SceneMaterialError(`Material '${materialId}' is not registered`);
        }
        return this;
    }

    getMaterialKeyword(materialId: string, keyword: string): boolean | null {
        return this._kernel.assets.getMaterialKeyword(materialId, keyword);
    }

    getMaterialEnabledKeywords(materialId: string): readonly string[] {
        return this._kernel.assets.getMaterialEnabledKeywords(materialId);
    }

    createMaterialAdapter(materialId: string): SceneMaterialInstanceAdapter | null {
        this.assertNotDisposed();
        return this._kernel.assets.createMaterialAdapter(materialId);
    }

    getMaterialObservables(): SceneMaterialObservables {
        this.assertNotDisposed();
        return this._kernel.assets.materialObservables;
    }

    setMaterialUniforms(
        materialId: string,
        uniforms: Readonly<Record<string, SceneUniformValue>>
    ): this {
        this.assertNotDisposed();
        if (!this._kernel.assets.setMaterialUniforms(materialId, uniforms)) {
            throw new SceneMaterialError(`Material '${materialId}' is not registered`);
        }
        return this;
    }

    getMaterialUniform(materialId: string, name: string): SceneUniformValue | null {
        return this._kernel.assets.getMaterialUniform(materialId, name);
    }

    getMaterialUniforms(
        materialId: string
    ): Readonly<Record<string, SceneUniformValue>> | null {
        return this._kernel.assets.getMaterialUniforms(materialId);
    }

    registerRenderPass(definition: SceneRenderPassDefinition): SceneRenderPassHandle {
        this.assertNotDisposed();
        return this._kernel.assets.registerRenderPass(definition);
    }

    getRenderPass(id: string): SceneRenderPassHandle | null {
        return this._kernel.assets.getRenderPass(id);
    }

    getRenderPasses(): readonly SceneRenderPassHandle[] {
        return this._kernel.assets.getRenderPasses();
    }

    createBoxMesh(
        id: string,
        width: number = 1,
        height: number = 1,
        depth: number = 1
    ): SceneMeshHandle {
        this.assertNotDisposed();
        return this._kernel.assets.createBoxMesh(id, width, height, depth);
    }

    createPlaneMesh(id: string, width: number = 1, height: number = 1): SceneMeshHandle {
        this.assertNotDisposed();
        return this._kernel.assets.createPlaneMesh(id, width, height);
    }

    createSphereMesh(id: string, radius: number = 1, segments: number = 24): SceneMeshHandle {
        this.assertNotDisposed();
        return this._kernel.assets.createSphereMesh(id, radius, segments);
    }

    createQuadMesh(
        id: string,
        width: number = 1,
        height: number = 1,
        orientation: 'xy' | 'xz' | 'yz' = 'xy'
    ): SceneMeshHandle {
        this.assertNotDisposed();
        return this._kernel.assets.createQuadMesh(id, width, height, orientation);
    }

    createCylinderMesh(
        id: string,
        radiusTop: number = 0.5,
        radiusBottom: number = 0.5,
        height: number = 1,
        radialSegments: number = 24,
        heightSegments: number = 1
    ): SceneMeshHandle {
        this.assertNotDisposed();
        return this._kernel.assets.createCylinderMesh(
            id,
            radiusTop,
            radiusBottom,
            height,
            radialSegments,
            heightSegments
        );
    }

    createConeMesh(
        id: string,
        radius: number = 0.5,
        height: number = 1,
        radialSegments: number = 24,
        heightSegments: number = 1
    ): SceneMeshHandle {
        this.assertNotDisposed();
        return this._kernel.assets.createConeMesh(
            id,
            radius,
            height,
            radialSegments,
            heightSegments
        );
    }

    createCapsuleMesh(
        id: string,
        radius: number = 0.5,
        length: number = 1,
        capSegments: number = 12,
        radialSegments: number = 24
    ): SceneMeshHandle {
        this.assertNotDisposed();
        return this._kernel.assets.createCapsuleMesh(
            id,
            radius,
            length,
            capSegments,
            radialSegments
        );
    }

    createTorusMesh(
        id: string,
        radius: number = 0.56,
        tube: number = 0.18,
        radialSegments: number = 20,
        tubularSegments: number = 32
    ): SceneMeshHandle {
        this.assertNotDisposed();
        return this._kernel.assets.createTorusMesh(
            id,
            radius,
            tube,
            radialSegments,
            tubularSegments
        );
    }
}