import { describe, it, expect, beforeEach } from 'vitest';
import {
    StandardMaterialComponent,
    MaterialType,
    BlendMode,
    CullMode,
    ShadowCasting,
    type Vec2,
    type Vec3,
    type Vec4,
} from '../material';

describe('StandardMaterialComponent – Construction', () => {
    it('S-001 – default constructor creates Standard material', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.materialType).toBe(MaterialType.STANDARD);
    });

    it('S-002 – shaderName is "Standard"', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.shaderName).toBe('Standard');
    });

    it('S-003 – default albedo is white (1,1,1,1)', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.albedo).toEqual({ x: 1, y: 1, z: 1, w: 1 });
    });

    it('S-004 – default metallic is 0', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.metallic).toBe(0);
    });

    it('S-005 – default smoothness is 0.5', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.smoothness).toBe(0.5);
    });

    it('S-006 – default renderingMode is Opaque', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.renderingMode).toBe('Opaque');
    });

    it('S-007 – default alphaCutoff is 0.5', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.alphaCutoff).toBe(0.5);
    });

    it('S-008 – default normalScale is 1', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.normalScale).toBe(1);
    });

    it('S-009 – default heightScale is 0.02', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.heightScale).toBe(0.02);
    });

    it('S-010 – default emission is (0,0,0)', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.emission).toEqual({ x: 0, y: 0, z: 0 });
    });

    it('S-011 – default emissionIntensity is 1', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.emissionIntensity).toBe(1);
    });

    it('S-012 – default occlusionStrength is 1', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.occlusionStrength).toBe(1);
    });
});

describe('StandardMaterialComponent – Albedo', () => {
    let mat: StandardMaterialComponent;

    beforeEach(() => {
        mat = new StandardMaterialComponent();
    });

    it('S-020 – albedo setter stores value', () => {
        mat.albedo = { x: 0.5, y: 0.3, z: 0.1, w: 0.9 };
        expect(mat.albedo).toEqual({ x: 0.5, y: 0.3, z: 0.1, w: 0.9 });
    });

    it('S-021 – albedoMap default is null', () => {
        expect(mat.albedoMap).toBeNull();
    });

    it('S-022 – albedoMap setter stores texture', () => {
        const tex = {} as WebGLTexture;
        mat.albedoMap = tex;
        expect(mat.albedoMap).toBe(tex);
    });
});

describe('StandardMaterialComponent – Metallic & Smoothness', () => {
    let mat: StandardMaterialComponent;

    beforeEach(() => {
        mat = new StandardMaterialComponent();
    });

    it('S-030 – metallic clamps to 0..1', () => {
        mat.metallic = 1.5;
        expect(mat.metallic).toBe(1.0);
        mat.metallic = -0.5;
        expect(mat.metallic).toBe(0.0);
    });

    it('S-031 – metallic valid value stores correctly', () => {
        mat.metallic = 0.75;
        expect(mat.metallic).toBe(0.75);
    });

    it('S-032 – metallicMap setter enables _METALLICGLOSSMAP keyword', () => {
        mat.metallicMap = {} as WebGLTexture;
        expect(mat.hasKeyword('_METALLICGLOSSMAP')).toBe(true);
    });

    it('S-033 – metallicMap null disables keyword', () => {
        mat.metallicMap = {} as WebGLTexture;
        mat.metallicMap = null;
        expect(mat.hasKeyword('_METALLICGLOSSMAP')).toBe(false);
    });

    it('S-034 – smoothness clamps max to 1', () => {
        mat.smoothness = 2.0;
        expect(mat.smoothness).toBe(1.0);
    });

    it('S-034b – smoothness clamps min', () => {
        mat.smoothness = -0.5;
        expect(mat.smoothness).toBeGreaterThanOrEqual(0);
    });

    it('S-035 – smoothness valid value stores correctly', () => {
        mat.smoothness = 0.8;
        expect(mat.smoothness).toBe(0.8);
    });
});

describe('StandardMaterialComponent – Normal Map', () => {
    let mat: StandardMaterialComponent;

    beforeEach(() => {
        mat = new StandardMaterialComponent();
    });

    it('S-040 – normalMap setter enables _NORMALMAP keyword', () => {
        mat.normalMap = {} as WebGLTexture;
        expect(mat.hasKeyword('_NORMALMAP')).toBe(true);
    });

    it('S-041 – normalMap null disables keyword', () => {
        mat.normalMap = {} as WebGLTexture;
        mat.normalMap = null;
        expect(mat.hasKeyword('_NORMALMAP')).toBe(false);
    });

    it('S-042 – normalScale clamps max to 2', () => {
        mat.normalScale = 3.0;
        expect(mat.normalScale).toBe(2.0);
    });

    it('S-042b – normalScale clamps min', () => {
        mat.normalScale = -0.5;
        expect(mat.normalScale).toBeGreaterThanOrEqual(0);
    });
});

describe('StandardMaterialComponent – Height Map', () => {
    let mat: StandardMaterialComponent;

    beforeEach(() => {
        mat = new StandardMaterialComponent();
    });

    it('S-050 – heightMap setter enables _PARALLAXMAP keyword', () => {
        mat.heightMap = {} as WebGLTexture;
        expect(mat.hasKeyword('_PARALLAXMAP')).toBe(true);
    });

    it('S-051 – heightMap null disables keyword', () => {
        mat.heightMap = {} as WebGLTexture;
        mat.heightMap = null;
        expect(mat.hasKeyword('_PARALLAXMAP')).toBe(false);
    });

    it('S-052 – heightScale clamps to 0.005..0.08', () => {
        mat.heightScale = 0.1;
        expect(mat.heightScale).toBe(0.08);
        mat.heightScale = 0.001;
        expect(mat.heightScale).toBe(0.005);
    });

    it('S-053 – heightScale valid value stores correctly', () => {
        mat.heightScale = 0.05;
        expect(mat.heightScale).toBe(0.05);
    });
});

describe('StandardMaterialComponent – Occlusion', () => {
    let mat: StandardMaterialComponent;

    beforeEach(() => {
        mat = new StandardMaterialComponent();
    });

    it('S-060 – occlusionMap setter enables _OCCLUSIONMAP keyword', () => {
        mat.occlusionMap = {} as WebGLTexture;
        expect(mat.hasKeyword('_OCCLUSIONMAP')).toBe(true);
    });

    it('S-061 – occlusionMap null disables keyword', () => {
        mat.occlusionMap = {} as WebGLTexture;
        mat.occlusionMap = null;
        expect(mat.hasKeyword('_OCCLUSIONMAP')).toBe(false);
    });

    it('S-062 – occlusionStrength clamps max to 1', () => {
        mat.occlusionStrength = 1.5;
        expect(mat.occlusionStrength).toBe(1.0);
    });

    it('S-062b – occlusionStrength clamps min', () => {
        mat.occlusionStrength = -0.5;
        expect(mat.occlusionStrength).toBeGreaterThanOrEqual(0);
    });
});

describe('StandardMaterialComponent – Emission', () => {
    let mat: StandardMaterialComponent;

    beforeEach(() => {
        mat = new StandardMaterialComponent();
    });

    it('S-070 – emission setter enables _EMISSION keyword when non-zero', () => {
        mat.emission = { x: 1, y: 0, z: 0 };
        expect(mat.hasKeyword('_EMISSION')).toBe(true);
    });

    it('S-071 – emission setter disables _EMISSION keyword when zero', () => {
        mat.emission = { x: 1, y: 0, z: 0 };
        mat.emission = { x: 0, y: 0, z: 0 };
        expect(mat.hasKeyword('_EMISSION')).toBe(false);
    });

    it('S-072 – emission getter strips w component', () => {
        mat.emission = { x: 0.5, y: 0.3, z: 0.1 };
        const result = mat.emission;
        expect((result as any).w).toBeUndefined();
    });

    it('S-073 – emissionMap setter enables _EMISSION keyword', () => {
        mat.emissionMap = {} as WebGLTexture;
        expect(mat.hasKeyword('_EMISSION')).toBe(true);
    });

    it('S-074 – emissionIntensity clamps min', () => {
        mat.emissionIntensity = -5;
        expect(mat.emissionIntensity).toBeGreaterThanOrEqual(0);
    });

    it('S-075 – emissionIntensity valid value stores correctly', () => {
        mat.emissionIntensity = 5.0;
        expect(mat.emissionIntensity).toBe(5.0);
    });
});

describe('StandardMaterialComponent – Rendering Mode', () => {
    let mat: StandardMaterialComponent;

    beforeEach(() => {
        mat = new StandardMaterialComponent();
    });

    it('S-080 – renderingMode Opaque sets correct state', () => {
        mat.renderingMode = 'Opaque';
        expect(mat.renderingMode).toBe('Opaque');
        expect(mat.blendMode).toBe(BlendMode.OPAQUE);
        expect(mat.renderQueue).toBe(2000);
        expect(mat.hasKeyword('_ALPHATEST_ON')).toBe(false);
        expect(mat.hasKeyword('_ALPHABLEND_ON')).toBe(false);
        expect(mat.hasKeyword('_ALPHAPREMULTIPLY_ON')).toBe(false);
    });

    it('S-081 – renderingMode Cutout sets ALPHA_TEST', () => {
        mat.renderingMode = 'Cutout';
        expect(mat.renderingMode).toBe('Cutout');
        expect(mat.blendMode).toBe(BlendMode.ALPHA_TEST);
        expect(mat.renderQueue).toBe(2450);
        expect(mat.hasKeyword('_ALPHATEST_ON')).toBe(true);
    });

    it('S-082 – renderingMode Fade sets ALPHA_BLEND', () => {
        mat.renderingMode = 'Fade';
        expect(mat.renderingMode).toBe('Fade');
        expect(mat.blendMode).toBe(BlendMode.ALPHA_BLEND);
        expect(mat.renderQueue).toBe(3000);
        expect(mat.hasKeyword('_ALPHABLEND_ON')).toBe(true);
    });

    it('S-083 – renderingMode Transparent sets PREMULTIPLIED', () => {
        mat.renderingMode = 'Transparent';
        expect(mat.renderingMode).toBe('Transparent');
        expect(mat.blendMode).toBe(BlendMode.PREMULTIPLIED);
        expect(mat.renderQueue).toBe(3000);
        expect(mat.hasKeyword('_ALPHAPREMULTIPLY_ON')).toBe(true);
    });

    it('S-084 – alphaCutoff clamps max to 1', () => {
        mat.alphaCutoff = 1.5;
        expect(mat.alphaCutoff).toBe(1.0);
    });

    it('S-084b – alphaCutoff clamps min', () => {
        mat.alphaCutoff = -0.5;
        expect(mat.alphaCutoff).toBeGreaterThanOrEqual(0);
    });

    it('S-085 – rapid rendering mode switching maintains consistency', () => {
        mat.renderingMode = 'Cutout';
        mat.renderingMode = 'Fade';
        mat.renderingMode = 'Transparent';
        mat.renderingMode = 'Opaque';

        expect(mat.renderingMode).toBe('Opaque');
        expect(mat.blendMode).toBe(BlendMode.OPAQUE);
        expect(mat.hasKeyword('_ALPHATEST_ON')).toBe(false);
        expect(mat.hasKeyword('_ALPHABLEND_ON')).toBe(false);
        expect(mat.hasKeyword('_ALPHAPREMULTIPLY_ON')).toBe(false);
    });
});

describe('StandardMaterialComponent – Texture Tiling & Offset', () => {
    let mat: StandardMaterialComponent;

    beforeEach(() => {
        mat = new StandardMaterialComponent();
    });

    it('S-090 – mainTextureScale default is (1,1)', () => {
        expect(mat.mainTextureScale).toEqual({ x: 1, y: 1 });
    });

    it('S-091 – mainTextureOffset default is (0,0)', () => {
        expect(mat.mainTextureOffset).toEqual({ x: 0, y: 0 });
    });

    it('S-092 – mainTextureScale setter stores value', () => {
        mat.mainTextureScale = { x: 2, y: 3 };
        expect(mat.mainTextureScale).toEqual({ x: 2, y: 3 });
    });

    it('S-093 – mainTextureOffset setter stores value', () => {
        mat.mainTextureOffset = { x: 0.5, y: 0.25 };
        expect(mat.mainTextureOffset).toEqual({ x: 0.5, y: 0.25 });
    });

    it('S-094 – setting scale preserves offset', () => {
        mat.mainTextureOffset = { x: 0.5, y: 0.5 };
        mat.mainTextureScale = { x: 2, y: 2 };
        expect(mat.mainTextureOffset).toEqual({ x: 0.5, y: 0.5 });
    });

    it('S-095 – setting offset preserves scale', () => {
        mat.mainTextureScale = { x: 3, y: 3 };
        mat.mainTextureOffset = { x: 0.1, y: 0.2 };
        expect(mat.mainTextureScale).toEqual({ x: 3, y: 3 });
    });
});

describe('StandardMaterialComponent – Helpers', () => {
    let mat: StandardMaterialComponent;

    beforeEach(() => {
        mat = new StandardMaterialComponent();
    });

    it('S-100 – enableGlobalIllumination sets correct render tag', () => {
        mat.enableGlobalIllumination();
        expect(mat.getRenderTag('GlobalIllumination')).toBe('RealtimeEmissive');
    });

    it('S-101 – disableGlobalIllumination sets correct render tag', () => {
        mat.disableGlobalIllumination();
        expect(mat.getRenderTag('GlobalIllumination')).toBe('EmissiveIsBlack');
    });

    it('S-102 – setSmoothnessSource metallic sets correct keywords', () => {
        mat.setSmoothnessSource('metallic');
        expect(mat.hasKeyword('_SMOOTHNESS_TEXTURE_METALLIC_CHANNEL_A')).toBe(true);
        expect(mat.hasKeyword('_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A')).toBe(false);
    });

    it('S-103 – setSmoothnessSource albedo sets correct keywords', () => {
        mat.setSmoothnessSource('albedo');
        expect(mat.hasKeyword('_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A')).toBe(true);
        expect(mat.hasKeyword('_SMOOTHNESS_TEXTURE_METALLIC_CHANNEL_A')).toBe(false);
    });

    it('S-104 – setSmoothnessSource toggles correctly', () => {
        mat.setSmoothnessSource('metallic');
        mat.setSmoothnessSource('albedo');
        expect(mat.hasKeyword('_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A')).toBe(true);
        expect(mat.hasKeyword('_SMOOTHNESS_TEXTURE_METALLIC_CHANNEL_A')).toBe(false);
    });
});

describe('StandardMaterialComponent – Keyword Setup', () => {
    it('S-110 – _setupDefaultKeywords enables keywords based on textures after onInitialize', () => {
        const mat = new StandardMaterialComponent({
            materialType: MaterialType.STANDARD,
            properties: {
                '_MainTex': {} as WebGLTexture,
                '_BumpMap': {} as WebGLTexture,
                '_MetallicGlossMap': {} as WebGLTexture,
            },
        });
        (mat as any).onInitialize();
        expect(mat.hasKeyword('_MAINTEX') || mat.hasKeyword('_NORMALMAP') || mat.hasKeyword('_METALLICGLOSSMAP')).toBe(true);
    });
});

describe('StandardMaterialComponent – Edge Cases', () => {
    it('S-120 – metallic boundary: exactly 0', () => {
        const mat = new StandardMaterialComponent();
        mat.metallic = 0;
        expect(mat.metallic).toBe(0);
    });

    it('S-121 – metallic boundary: exactly 1', () => {
        const mat = new StandardMaterialComponent();
        mat.metallic = 1;
        expect(mat.metallic).toBe(1);
    });

    it('S-123 – smoothness boundary: exactly 1', () => {
        const mat = new StandardMaterialComponent();
        mat.smoothness = 1;
        expect(mat.smoothness).toBe(1);
    });

    it('S-122 – metallic boundary: exactly 0 stores correctly', () => {
        const mat = new StandardMaterialComponent();
        mat.metallic = 0;
        expect(mat.metallic).toBeGreaterThanOrEqual(0);
    });

    it('S-124 – heightScale boundary: exactly 0.005', () => {
        const mat = new StandardMaterialComponent();
        mat.heightScale = 0.005;
        expect(mat.heightScale).toBe(0.005);
    });

    it('S-125 – heightScale boundary: exactly 0.08', () => {
        const mat = new StandardMaterialComponent();
        mat.heightScale = 0.08;
        expect(mat.heightScale).toBe(0.08);
    });

    it('S-126 – default render state values', () => {
        const mat = new StandardMaterialComponent();
        expect(mat.blendMode).toBe(BlendMode.OPAQUE);
        expect(mat.cullMode).toBe(CullMode.BACK);
        expect(mat.shadowCasting).toBe(ShadowCasting.ON);
        expect(mat.receiveShadows).toBe(true);
    });
});