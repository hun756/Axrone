import type { Vec2, Vec3, Vec4, Mat4, Quat } from '@axrone/numeric';
import type { ShaderUniformValue } from '@axrone/render-webgl2/shader';
import type { SceneMaterialResource } from './material-registry';
import type { SceneUniformValue } from './types';

export interface SceneMaterialAdapterTextureEntry {
    readonly textureId: string;
    readonly samplerId: string | null;
    readonly unit: number;
    readonly nativeTexture: WebGLTexture;
    readonly width: number;
    readonly height: number;
}

export interface SceneMaterialAdapterDependencies {
    readonly resolveTextures: (
        materialId: string
    ) => readonly SceneMaterialAdapterTextureEntry[];
}

/**
 * Converts a SceneUniformValue to a ShaderUniformValue compatible with
 * render-webgl2's MaterialInstance. Handles:
 * - readonly number[] → Float32Array
 * - Quat → Float32Array(4)
 * - Pass-through for all other types
 */
export function convertSceneUniformValue(
    value: SceneUniformValue
): ShaderUniformValue {
    if (value === null || value === undefined) {
        return 0;
    }

    if (typeof value === 'number' || typeof value === 'boolean') {
        return value;
    }

    if (value instanceof Float32Array) {
        return value;
    }
    if (value instanceof Int32Array) {
        return value;
    }
    if (value instanceof Uint32Array) {
        return value;
    }

    if (Array.isArray(value)) {
        return new Float32Array(value);
    }

    if (isQuat(value)) {
        return new Float32Array([value.x, value.y, value.z, value.w]);
    }

    return value as Vec2 | Vec3 | Vec4 | Mat4;
}

function isQuat(value: unknown): value is Quat {
    return (
        typeof value === 'object' &&
        value !== null &&
        'x' in value &&
        'y' in value &&
        'z' in value &&
        'w' in value &&
        typeof (value as Quat).x === 'number' &&
        typeof (value as Quat).w === 'number'
    );
}

/**
 * Read-only adapter that exposes SceneMaterialResource state in a
 * MaterialInstance-compatible shape. Bridges the scene-runtime material
 * data layer to render-webgl2's type system without replacing the
 * scene draw path.
 */
export class SceneMaterialInstanceAdapter {
    private readonly _dependencies: SceneMaterialAdapterDependencies;
    private readonly _material: SceneMaterialResource;

    constructor(
        material: SceneMaterialResource,
        dependencies: SceneMaterialAdapterDependencies
    ) {
        this._material = material;
        this._dependencies = dependencies;
    }

    get id(): string {
        return this._material.id;
    }

    get shaderId(): string {
        return this._material.shaderId;
    }

    getUniformNames(): readonly string[] {
        return Object.freeze([...this._material.uniforms.keys()]);
    }

    getProperty(name: string): ShaderUniformValue | null {
        const value = this._material.uniforms.get(name);
        if (value === undefined) {
            return null;
        }
        return convertSceneUniformValue(value);
    }

    hasProperty(name: string): boolean {
        return this._material.uniforms.has(name);
    }

    getAllProperties(): ReadonlyMap<string, ShaderUniformValue> {
        const result = new Map<string, ShaderUniformValue>();
        for (const [name, value] of this._material.uniforms) {
            result.set(name, convertSceneUniformValue(value));
        }
        return result;
    }

    hasKeyword(keyword: string): boolean {
        const entry = this._material.keywords.get(keyword);
        return entry?.enabled ?? false;
    }

    getEnabledKeywords(): readonly string[] {
        const enabled: string[] = [];
        for (const [keyword, entry] of this._material.keywords) {
            if (entry.enabled) {
                enabled.push(keyword);
            }
        }
        return Object.freeze(enabled);
    }

    getKeywordState(
        keyword: string
    ): { enabled: boolean; source: 'auto' | 'explicit' } | null {
        return this._material.keywords.get(keyword) ?? null;
    }

    getTextureBindings(): readonly SceneMaterialAdapterTextureEntry[] {
        return this._dependencies.resolveTextures(this._material.id);
    }

    getTextureSlotCount(): number {
        return this.getTextureBindings().length;
    }

    getStats(): {
        uniformCount: number;
        keywordCount: number;
        enabledKeywordCount: number;
        textureCount: number;
    } {
        return {
            uniformCount: this._material.uniforms.size,
            keywordCount: this._material.keywords.size,
            enabledKeywordCount: this.getEnabledKeywords().length,
            textureCount: this.getTextureSlotCount(),
        };
    }
}
