declare const __brand: unique symbol;

type Branded<T, TBrand extends string> = T & { readonly [K in TBrand]: typeof __brand };

export type NodeId = Branded<string, 'NodeId'>;
export type MeshKey = Branded<string, 'MeshKey'>;
export type MaterialKey = Branded<string, 'MaterialKey'>;
export type TextureKey = Branded<string, 'TextureKey'>;
export type SkinKey = Branded<string, 'SkinKey'>;
export type AnimationKey = Branded<string, 'AnimationKey'>;
export type PrefabKey = Branded<string, 'PrefabKey'>;

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
