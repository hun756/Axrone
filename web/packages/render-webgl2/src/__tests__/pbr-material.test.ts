import { describe, it, expect, beforeEach } from 'vitest';
import {
    PBRMaterialComponent,
    MaterialType,
    BlendMode,
    CullMode,
    type Vec2,
    type Vec3,
    type Vec4,
} from '../material';

describe('PBRMaterialComponent – Construction', () => {
    it('P-001 – default constructor creates PBR material', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.materialType).toBe(MaterialType.PBR);
    });

    it('P-002 – default baseColor is white (1,1,1,1)', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.baseColor).toEqual({ x: 1, y: 1, z: 1, w: 1 });
    });

    it('P-003 – default metallicFactor is 1.0', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.metallicFactor).toBe(1.0);
    });

    it('P-004 – default roughnessFactor is 1.0', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.roughnessFactor).toBe(1.0);
    });

    it('P-005 – default alphaMode is OPAQUE', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.alphaMode).toBe('OPAQUE');
    });

    it('P-006 – default alphaCutoff is 0.5', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.alphaCutoff).toBe(0.5);
    });

    it('P-007 – default doubleSided is false', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.doubleSided).toBe(false);
    });

    it('P-008 – default blendMode is OPAQUE', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.blendMode).toBe(BlendMode.OPAQUE);
    });

    it('P-009 – default cullMode is BACK', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.cullMode).toBe(CullMode.BACK);
    });

    it('P-010 – constructor with config overrides', () => {
        const mat = new PBRMaterialComponent({
            materialType: MaterialType.PBR,
            blendMode: BlendMode.ALPHA_BLEND,
            cullMode: CullMode.NONE,
        });
        expect(mat.blendMode).toBe(BlendMode.ALPHA_BLEND);
        expect(mat.cullMode).toBe(CullMode.NONE);
    });

    it('P-011 – shaderName is "PBR"', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.shaderName).toBe('PBR');
    });

    it('P-012 – renderQueue defaults to 2000', () => {
        const mat = new PBRMaterialComponent();
        expect(mat.renderQueue).toBe(2000);
    });
});

describe('PBRMaterialComponent – Base Color', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-020 – baseColor getter returns default white', () => {
        expect(mat.baseColor).toEqual({ x: 1, y: 1, z: 1, w: 1 });
    });

    it('P-021 – baseColor setter stores value', () => {
        const color: Vec4 = { x: 0.5, y: 0.3, z: 0.1, w: 0.9 };
        mat.baseColor = color;
        expect(mat.baseColor).toEqual(color);
    });

    it('P-022 – baseColor setter marks dirty', () => {
        (mat as any)._isDirty = false;
        mat.baseColor = { x: 0, y: 0, z: 0, w: 1 };
        expect(mat.isDirty).toBe(true);
    });
});

describe('PBRMaterialComponent – Metallic & Roughness', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-030 – metallicFactor setter clamps max to 1', () => {
        mat.metallicFactor = 1.5;
        expect(mat.metallicFactor).toBe(1.0);
    });

    it('P-030b – metallicFactor setter clamps min to 0', () => {
        mat.metallicFactor = -0.5;
        expect(mat.metallicFactor).toBe(0);
    });

    it('P-031 – roughnessFactor setter clamps max to 1', () => {
        mat.roughnessFactor = 2.0;
        expect(mat.roughnessFactor).toBe(1.0);
    });

    it('P-031b – roughnessFactor setter clamps min to 0', () => {
        mat.roughnessFactor = -1.0;
        expect(mat.roughnessFactor).toBe(0);
    });

    it('P-032 – metallicFactor valid value stores correctly', () => {
        mat.metallicFactor = 0.75;
        expect(mat.metallicFactor).toBe(0.75);
    });

    it('P-033 – roughnessFactor valid value stores correctly', () => {
        mat.roughnessFactor = 0.33;
        expect(mat.roughnessFactor).toBe(0.33);
    });
});

describe('PBRMaterialComponent – Textures', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-040 – baseColorTexture default is null', () => {
        expect(mat.baseColorTexture).toBeNull();
    });

    it('P-041 – baseColorTexture setter enables keyword', () => {
        const fakeTexture = {} as WebGLTexture;
        mat.baseColorTexture = fakeTexture;
        expect(mat.baseColorTexture).toBe(fakeTexture);
        expect(mat.hasKeyword('_BASECOLORTEXTURE')).toBe(true);
    });

    it('P-042 – baseColorTexture null disables keyword', () => {
        mat.baseColorTexture = {} as WebGLTexture;
        mat.baseColorTexture = null;
        expect(mat.hasKeyword('_BASECOLORTEXTURE')).toBe(false);
    });

    it('P-043 – normalTexture setter enables _NORMALTEXTURE keyword', () => {
        mat.normalTexture = {} as WebGLTexture;
        expect(mat.hasKeyword('_NORMALTEXTURE')).toBe(true);
    });

    it('P-044 – metallicRoughnessTexture setter enables keyword', () => {
        mat.metallicRoughnessTexture = {} as WebGLTexture;
        expect(mat.hasKeyword('_METALLICROUGHNESSTEXTURE')).toBe(true);
    });

    it('P-045 – occlusionTexture setter enables keyword', () => {
        mat.occlusionTexture = {} as WebGLTexture;
        expect(mat.hasKeyword('_OCCLUSIONTEXTURE')).toBe(true);
    });

    it('P-046 – emissiveTexture setter enables keyword', () => {
        mat.emissiveTexture = {} as WebGLTexture;
        expect(mat.hasKeyword('_EMISSIVETEXTURE')).toBe(true);
    });

    it('P-047 – clearcoatTexture setter enables keyword', () => {
        mat.clearcoatTexture = {} as WebGLTexture;
        expect(mat.hasKeyword('_CLEARCOATTEXTURE')).toBe(true);
    });

    it('P-048 – clearcoatRoughnessTexture setter enables keyword', () => {
        mat.clearcoatRoughnessTexture = {} as WebGLTexture;
        expect(mat.hasKeyword('_CLEARCOATROUGHNESSTEXTURE')).toBe(true);
    });

    it('P-049 – clearcoatNormalTexture setter enables keyword', () => {
        mat.clearcoatNormalTexture = {} as WebGLTexture;
        expect(mat.hasKeyword('_CLEARCOATNORMALTEXTURE')).toBe(true);
    });
});

describe('PBRMaterialComponent – Clearcoat', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-050 – clearcoatFactor default is 0', () => {
        expect(mat.clearcoatFactor).toBe(0);
    });

    it('P-051 – clearcoatFactor clamps to 0..1', () => {
        mat.clearcoatFactor = 2.0;
        expect(mat.clearcoatFactor).toBe(1.0);
        mat.clearcoatFactor = -1.0;
        expect(mat.clearcoatFactor).toBe(0.0);
    });

    it('P-052 – clearcoatRoughnessFactor clamps to 0..1', () => {
        mat.clearcoatRoughnessFactor = 1.5;
        expect(mat.clearcoatRoughnessFactor).toBe(1.0);
    });

    it('P-053 – clearcoatNormalScale default is 1', () => {
        expect(mat.clearcoatNormalScale).toBe(1);
    });

    it('P-054 – clearcoatNormalScale clamps max to 2', () => {
        mat.clearcoatNormalScale = 3.0;
        expect(mat.clearcoatNormalScale).toBe(2.0);
    });

    it('P-054b – clearcoatNormalScale clamps min to 0', () => {
        mat.clearcoatNormalScale = -0.5;
        expect(mat.clearcoatNormalScale).toBe(0);
    });
});

describe('PBRMaterialComponent – Normal & Occlusion', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-060 – normalScale default is 1', () => {
        expect(mat.normalScale).toBe(1);
    });

    it('P-061 – normalScale clamps to 0..2', () => {
        mat.normalScale = 3.0;
        expect(mat.normalScale).toBe(2.0);
    });

    it('P-062 – occlusionStrength default is 1', () => {
        expect(mat.occlusionStrength).toBe(1);
    });

    it('P-063 – occlusionStrength clamps to 0..1', () => {
        mat.occlusionStrength = 1.5;
        expect(mat.occlusionStrength).toBe(1.0);
    });
});

describe('PBRMaterialComponent – Emission', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-070 – emissiveFactor default is (0,0,0)', () => {
        expect(mat.emissiveFactor).toEqual({ x: 0, y: 0, z: 0 });
    });

    it('P-071 – emissiveFactor setter stores Vec3', () => {
        mat.emissiveFactor = { x: 1, y: 0.5, z: 0.25 };
        expect(mat.emissiveFactor).toEqual({ x: 1, y: 0.5, z: 0.25 });
    });

    it('P-072 – emissiveFactor getter strips w component', () => {
        mat.emissiveFactor = { x: 0.1, y: 0.2, z: 0.3 };
        const result = mat.emissiveFactor;
        expect((result as any).w).toBeUndefined();
    });
});

describe('PBRMaterialComponent – Alpha Mode', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-080 – alphaMode default is OPAQUE', () => {
        expect(mat.alphaMode).toBe('OPAQUE');
    });

    it('P-081 – alphaMode MASK sets ALPHA_TEST blend and keyword', () => {
        mat.alphaMode = 'MASK';
        expect(mat.alphaMode).toBe('MASK');
        expect(mat.blendMode).toBe(BlendMode.ALPHA_TEST);
        expect(mat.renderQueue).toBe(2450);
        expect(mat.hasKeyword('_ALPHAMODE_MASK')).toBe(true);
        expect(mat.hasKeyword('_ALPHAMODE_OPAQUE')).toBe(false);
    });

    it('P-082 – alphaMode BLEND sets ALPHA_BLEND and keyword', () => {
        mat.alphaMode = 'BLEND';
        expect(mat.alphaMode).toBe('BLEND');
        expect(mat.blendMode).toBe(BlendMode.ALPHA_BLEND);
        expect(mat.renderQueue).toBe(3000);
        expect(mat.hasKeyword('_ALPHAMODE_BLEND')).toBe(true);
    });

    it('P-083 – alphaMode OPAQUE resets to opaque state', () => {
        mat.alphaMode = 'BLEND';
        mat.alphaMode = 'OPAQUE';
        expect(mat.blendMode).toBe(BlendMode.OPAQUE);
        expect(mat.renderQueue).toBe(2000);
        expect(mat.hasKeyword('_ALPHAMODE_OPAQUE')).toBe(true);
        expect(mat.hasKeyword('_ALPHAMODE_BLEND')).toBe(false);
    });

    it('P-084 – alphaCutoff clamps max to 1', () => {
        mat.alphaCutoff = 1.5;
        expect(mat.alphaCutoff).toBe(1.0);
    });

    it('P-084b – alphaCutoff clamps min to 0', () => {
        mat.alphaCutoff = -0.5;
        expect(mat.alphaCutoff).toBe(0);
    });

    it('P-085 – alphaCutoff valid value stores correctly', () => {
        mat.alphaCutoff = 0.3;
        expect(mat.alphaCutoff).toBe(0.3);
    });
});

describe('PBRMaterialComponent – Double Sided', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-090 – doubleSided=true sets CullMode.NONE and keyword', () => {
        mat.doubleSided = true;
        expect(mat.doubleSided).toBe(true);
        expect(mat.cullMode).toBe(CullMode.NONE);
        expect(mat.hasKeyword('_DOUBLESIDED')).toBe(true);
    });

    it('P-091 – doubleSided=false restores CullMode.BACK', () => {
        mat.doubleSided = true;
        mat.doubleSided = false;
        expect(mat.doubleSided).toBe(false);
        expect(mat.cullMode).toBe(CullMode.BACK);
        expect(mat.hasKeyword('_DOUBLESIDED')).toBe(false);
    });
});

describe('PBRMaterialComponent – Texture Transforms', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-100 – setTextureTransform stores scale and offset', () => {
        mat.setTextureTransform('_BaseColorTexture', { x: 2, y: 2 }, { x: 0.5, y: 0.5 });
        const scale = mat.getTextureScale('_BaseColorTexture');
        const offset = mat.getTextureOffset('_BaseColorTexture');
        expect(scale).toEqual({ x: 2, y: 2 });
        expect(offset).toEqual({ x: 0.5, y: 0.5 });
    });

    it('P-101 – getTextureScale defaults to (1,1)', () => {
        const scale = mat.getTextureScale('_BaseColorTexture');
        expect(scale).toEqual({ x: 1, y: 1 });
    });

    it('P-102 – getTextureOffset defaults to (0,0)', () => {
        const offset = mat.getTextureOffset('_BaseColorTexture');
        expect(offset).toEqual({ x: 0, y: 0 });
    });
});

describe('PBRMaterialComponent – GLTF Import', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-110 – copyFromGLTF null is a no-op', () => {
        mat.copyFromGLTF(null);
        expect(mat.baseColor).toEqual({ x: 1, y: 1, z: 1, w: 1 });
    });

    it('P-111 – copyFromGLTF with pbrMetallicRoughness', () => {
        mat.copyFromGLTF({
            pbrMetallicRoughness: {
                baseColorFactor: [0.5, 0.3, 0.1, 0.9],
                metallicFactor: 0.8,
                roughnessFactor: 0.4,
            },
        });
        expect(mat.baseColor).toEqual({ x: 0.5, y: 0.3, z: 0.1, w: 0.9 });
        expect(mat.metallicFactor).toBe(0.8);
        expect(mat.roughnessFactor).toBe(0.4);
    });

    it('P-112 – copyFromGLTF with normalTexture scale', () => {
        mat.copyFromGLTF({
            normalTexture: { scale: 1.5 },
        });
        expect(mat.normalScale).toBe(1.5);
    });

    it('P-113 – copyFromGLTF with occlusionTexture strength', () => {
        mat.copyFromGLTF({
            occlusionTexture: { strength: 0.7 },
        });
        expect(mat.occlusionStrength).toBe(0.7);
    });

    it('P-114 – copyFromGLTF with emissiveFactor', () => {
        mat.copyFromGLTF({
            emissiveFactor: [1.0, 0.5, 0.25],
        });
        expect(mat.emissiveFactor).toEqual({ x: 1, y: 0.5, z: 0.25 });
    });

    it('P-115 – copyFromGLTF with alphaMode MASK', () => {
        mat.copyFromGLTF({
            alphaMode: 'MASK',
            alphaCutoff: 0.3,
        });
        expect(mat.alphaMode).toBe('MASK');
        expect(mat.alphaCutoff).toBe(0.3);
    });

    it('P-116 – copyFromGLTF with doubleSided', () => {
        mat.copyFromGLTF({
            doubleSided: true,
        });
        expect(mat.doubleSided).toBe(true);
    });

    it('P-117 – copyFromGLTF with KHR_materials_clearcoat extension', () => {
        mat.copyFromGLTF({
            extensions: {
                KHR_materials_clearcoat: {
                    clearcoatFactor: 0.8,
                    clearcoatRoughnessFactor: 0.2,
                    clearcoatNormalTexture: { scale: 1.2 },
                },
            },
        });
        expect(mat.clearcoatFactor).toBe(0.8);
        expect(mat.clearcoatRoughnessFactor).toBe(0.2);
        expect(mat.clearcoatNormalScale).toBe(1.2);
    });

    it('P-118 – copyFromGLTF missing fields preserve defaults', () => {
        mat.copyFromGLTF({});
        expect(mat.baseColor).toEqual({ x: 1, y: 1, z: 1, w: 1 });
        expect(mat.metallicFactor).toBe(1.0);
    });
});

describe('PBRMaterialComponent – GLTF Export', () => {
    let mat: PBRMaterialComponent;

    beforeEach(() => {
        mat = new PBRMaterialComponent();
    });

    it('P-120 – exportToGLTF produces correct structure', () => {
        mat.baseColor = { x: 0.5, y: 0.3, z: 0.1, w: 0.9 };
        mat.metallicFactor = 0.8;
        mat.roughnessFactor = 0.4;

        const gltf = mat.exportToGLTF();
        expect(gltf.pbrMetallicRoughness.baseColorFactor).toEqual([0.5, 0.3, 0.1, 0.9]);
        expect(gltf.pbrMetallicRoughness.metallicFactor).toBe(0.8);
        expect(gltf.pbrMetallicRoughness.roughnessFactor).toBe(0.4);
    });

    it('P-121 – exportToGLTF includes emissiveFactor', () => {
        mat.emissiveFactor = { x: 1, y: 0.5, z: 0.25 };
        const gltf = mat.exportToGLTF();
        expect(gltf.emissiveFactor).toEqual([1, 0.5, 0.25]);
    });

    it('P-122 – exportToGLTF includes alphaMode', () => {
        const gltf = mat.exportToGLTF();
        expect(gltf.alphaMode).toBe('OPAQUE');
    });

    it('P-123 – exportToGLTF includes alphaCutoff only for MASK', () => {
        mat.alphaMode = 'MASK';
        mat.alphaCutoff = 0.3;
        const gltf = mat.exportToGLTF();
        expect(gltf.alphaCutoff).toBe(0.3);

        mat.alphaMode = 'OPAQUE';
        const gltf2 = mat.exportToGLTF();
        expect(gltf2.alphaCutoff).toBeUndefined();
    });

    it('P-124 – exportToGLTF includes clearcoat extension when active', () => {
        mat.clearcoatFactor = 0.5;
        const gltf = mat.exportToGLTF();
        expect(gltf.extensions).toBeDefined();
        expect(gltf.extensions!.KHR_materials_clearcoat).toBeDefined();
    });

    it('P-125 – exportToGLTF omits clearcoat extension when inactive', () => {
        const gltf = mat.exportToGLTF();
        expect(gltf.extensions).toBeUndefined();
    });

    it('P-126 – exportToGLTF includes normalTexture when set', () => {
        mat.normalTexture = {} as WebGLTexture;
        mat.normalScale = 1.5;
        const gltf = mat.exportToGLTF();
        expect(gltf.normalTexture).toBeDefined();
        expect(gltf.normalTexture!.scale).toBe(1.5);
    });

    it('P-127 – exportToGLTF omits normalTexture when not set', () => {
        const gltf = mat.exportToGLTF();
        expect(gltf.normalTexture).toBeUndefined();
    });

    it('P-128 – GLTF round-trip preserves data', () => {
        mat.baseColor = { x: 0.2, y: 0.4, z: 0.6, w: 0.8 };
        mat.metallicFactor = 0.7;
        mat.roughnessFactor = 0.3;
        mat.emissiveFactor = { x: 0.1, y: 0.2, z: 0.3 };

        const gltf = mat.exportToGLTF();

        const mat2 = new PBRMaterialComponent();
        mat2.copyFromGLTF(gltf);

        expect(mat2.baseColor).toEqual({ x: 0.2, y: 0.4, z: 0.6, w: 0.8 });
        expect(mat2.metallicFactor).toBe(0.7);
        expect(mat2.roughnessFactor).toBe(0.3);
        expect(mat2.emissiveFactor).toEqual({ x: 0.1, y: 0.2, z: 0.3 });
    });
});

describe('PBRMaterialComponent – Keyword Setup', () => {
    it('P-130 – _ALPHAMODE_OPAQUE keyword enabled after onInitialize', () => {
        const mat = new PBRMaterialComponent();
        (mat as any).onInitialize();
        expect(mat.hasKeyword('_ALPHAMODE_OPAQUE')).toBe(true);
    });

    it('P-131 – config keywords + textures enable correct keywords after onInitialize', () => {
        const mat = new PBRMaterialComponent({
            materialType: MaterialType.PBR,
            properties: {
                '_BaseColorTexture': {} as WebGLTexture,
                '_NormalTexture': {} as WebGLTexture,
            },
            keywords: ['_BASECOLORTEXTURE', '_NORMALTEXTURE'],
        });
        (mat as any).onInitialize();
        expect(mat.hasKeyword('_ALPHAMODE_OPAQUE')).toBe(true);
        expect(mat.hasKeyword('_BASECOLORTEXTURE')).toBe(true);
        expect(mat.hasKeyword('_NORMALTEXTURE')).toBe(true);
    });
});

describe('PBRMaterialComponent – Edge Cases', () => {
    it('P-140 – rapid alpha mode switching maintains consistency', () => {
        const mat = new PBRMaterialComponent();
        mat.alphaMode = 'MASK';
        mat.alphaMode = 'BLEND';
        mat.alphaMode = 'OPAQUE';
        mat.alphaMode = 'MASK';

        expect(mat.alphaMode).toBe('MASK');
        expect(mat.blendMode).toBe(BlendMode.ALPHA_TEST);
        expect(mat.hasKeyword('_ALPHAMODE_MASK')).toBe(true);
        expect(mat.hasKeyword('_ALPHAMODE_BLEND')).toBe(false);
        expect(mat.hasKeyword('_ALPHAMODE_OPAQUE')).toBe(false);
    });

    it('P-141 – doubleSided toggling maintains cullMode consistency', () => {
        const mat = new PBRMaterialComponent();
        mat.doubleSided = true;
        expect(mat.cullMode).toBe(CullMode.NONE);

        mat.doubleSided = false;
        expect(mat.cullMode).toBe(CullMode.BACK);

        mat.doubleSided = true;
        expect(mat.cullMode).toBe(CullMode.NONE);
    });

    it('P-142 – metallicFactor boundary: exactly 1', () => {
        const mat = new PBRMaterialComponent();
        mat.metallicFactor = 1;
        expect(mat.metallicFactor).toBe(1);
    });

    it('P-143 – roughnessFactor boundary: exactly 1', () => {
        const mat = new PBRMaterialComponent();
        mat.roughnessFactor = 1;
        expect(mat.roughnessFactor).toBe(1);
    });
});