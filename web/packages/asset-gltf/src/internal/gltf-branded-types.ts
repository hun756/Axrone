import type { Brand } from '@axrone/utility';

export type NodeId = Brand<string, 'NodeId'>;
export type MeshKey = Brand<string, 'MeshKey'>;
export type MaterialKey = Brand<string, 'MaterialKey'>;
export type TextureKey = Brand<string, 'TextureKey'>;
export type SkinKey = Brand<string, 'SkinKey'>;
export type AnimationKey = Brand<string, 'AnimationKey'>;
export type PrefabKey = Brand<string, 'PrefabKey'>;

export const nodeId = (index: number): NodeId => `node/${index}` as NodeId;
export const meshKey = (meshIndex: number, primitiveIndex: number): MeshKey =>
    `mesh/${meshIndex}/primitive/${primitiveIndex}` as MeshKey;
export const materialKey = (index: number): MaterialKey => `material/${index}` as MaterialKey;
export const textureKey = (index: number): TextureKey => `texture/${index}` as TextureKey;
export const skinKey = (index: number): SkinKey => `skin/${index}` as SkinKey;
export const animationKey = (index: number): AnimationKey => `animation/${index}` as AnimationKey;
export const prefabKey = (sceneIndex: number): PrefabKey => `scene/${sceneIndex}/prefab` as PrefabKey;

export type Vec2Tuple = readonly [number, number];
export type Vec3Tuple = readonly [number, number, number];
export type Vec4Tuple = readonly [number, number, number, number];
export type QuatTuple = readonly [number, number, number, number];
export type Mat4Tuple = readonly [
    number, number, number, number,
    number, number, number, number,
    number, number, number, number,
    number, number, number, number,
];

export interface TransformDecomposition {
    readonly position: Vec3Tuple;
    readonly rotation: QuatTuple;
    readonly scale: Vec3Tuple;
}

export interface LightReference {
    readonly index: number;
    readonly light: import('../types').GltfPunctualLightJson;
}

export type InterpolationMode = 'LINEAR' | 'STEP' | 'CUBICSPLINE';
export type AnimationPath = 'translation' | 'rotation' | 'scale' | 'weights';
