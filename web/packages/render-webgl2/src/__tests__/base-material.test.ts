import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
    BaseMaterialComponent,
    MaterialType,
    BlendMode,
    CullMode,
    DepthTest,
    ShadowCasting,
    LightMode,
    type MaterialConfig,
    type MaterialProperty,
    type MaterialKeyword,
    type MaterialPropertyValue,
    type Vec2,
    type Vec3,
    type Vec4,
    type Mat3,
    type Mat4,
} from '../material/base-material';

class TestMaterial extends BaseMaterialComponent<MaterialConfig> {
    public setupDefaultsCalled = false;
    public setupKeywordsCalled = false;

    constructor(config: Partial<MaterialConfig> = {}) {
        super({
            materialType: MaterialType.STANDARD,
            ...config,
        });
    }

    protected _setupDefaultProperties(): void {
        this.setupDefaultsCalled = true;
        if (!this.hasProperty('_Color')) {
            this.setProperty('_Color', { x: 1, y: 1, z: 1, w: 1 });
        }
        if (!this.hasProperty('_Metallic')) {
            this.setProperty('_Metallic', 0.0);
        }
        if (!this.hasProperty('_MainTex')) {
            this.setProperty('_MainTex', null);
        }
    }

    protected _setupDefaultKeywords(): void {
        this.setupKeywordsCalled = true;
        this.enableKeyword('_ALPHATEST_ON');
    }

    protected _getAvailableProperties(): MaterialProperty[] {
        return [
            { name: '_Color', displayName: 'Color', type: 'color', defaultValue: { x: 1, y: 1, z: 1, w: 1 } },
            { name: '_Metallic', displayName: 'Metallic', type: 'float', defaultValue: 0, range: { min: 0, max: 1 } },
            { name: '_MainTex', displayName: 'Albedo', type: 'texture', defaultValue: null },
        ];
    }

    protected _getAvailableKeywords(): MaterialKeyword[] {
        return [
            { name: '_ALPHATEST_ON', displayName: 'Alpha Test', category: 'Rendering' },
        ];
    }
}

describe('Material Enums', () => {
    it('M-001 – MaterialType enum values', () => {
        expect(MaterialType.STANDARD).toBe('Standard');
        expect(MaterialType.PBR).toBe('PBR');
        expect(MaterialType.UNLIT).toBe('Unlit');
        expect(MaterialType.CUSTOM).toBe('Custom');
    });

    it('M-002 – BlendMode enum values', () => {
        expect(BlendMode.OPAQUE).toBe('Opaque');
        expect(BlendMode.ALPHA_BLEND).toBe('AlphaBlend');
        expect(BlendMode.ADDITIVE).toBe('Additive');
        expect(BlendMode.PREMULTIPLIED).toBe('Premultiplied');
    });

    it('M-003 – CullMode enum values', () => {
        expect(CullMode.NONE).toBe('None');
        expect(CullMode.FRONT).toBe('Front');
        expect(CullMode.BACK).toBe('Back');
    });

    it('M-004 – DepthTest enum values', () => {
        expect(DepthTest.LESS).toBe('Less');
        expect(DepthTest.DISABLED).toBe('Disabled');
        expect(DepthTest.ALWAYS).toBe('Always');
    });

    it('M-005 – ShadowCasting enum values', () => {
        expect(ShadowCasting.ON).toBe('On');
        expect(ShadowCasting.OFF).toBe('Off');
        expect(ShadowCasting.TWO_SIDED).toBe('TwoSided');
    });

    it('M-006 – LightMode enum values', () => {
        expect(LightMode.FORWARD_BASE).toBe('ForwardBase');
        expect(LightMode.DEFERRED).toBe('Deferred');
    });
});

describe('BaseMaterialComponent – Construction', () => {
    it('M-010 – constructor sets materialType', () => {
        const mat = new TestMaterial();
        expect(mat.materialType).toBe(MaterialType.STANDARD);
    });

    it('M-011 – onInitialize calls _setupDefaultProperties', () => {
        const mat = new TestMaterial();
        (mat as any).onInitialize();
        expect(mat.setupDefaultsCalled).toBe(true);
        expect(mat.hasProperty('_Color')).toBe(true);
    });

    it('M-012 – onInitialize calls _setupDefaultKeywords', () => {
        const mat = new TestMaterial();
        (mat as any).onInitialize();
        expect(mat.setupKeywordsCalled).toBe(true);
        expect(mat.hasKeyword('_ALPHATEST_ON')).toBe(true);
    });

    it('M-013 – constructor applies config overrides', () => {
        const mat = new TestMaterial({
            materialType: MaterialType.STANDARD,
            blendMode: BlendMode.ALPHA_BLEND,
            cullMode: CullMode.NONE,
            depthWrite: false,
        });
        expect(mat.blendMode).toBe(BlendMode.ALPHA_BLEND);
        expect(mat.cullMode).toBe(CullMode.NONE);
        expect(mat.depthWrite).toBe(false);
    });

    it('M-014 – default render state values', () => {
        const mat = new TestMaterial();
        expect(mat.blendMode).toBe(BlendMode.OPAQUE);
        expect(mat.cullMode).toBe(CullMode.BACK);
        expect(mat.depthTest).toBe(DepthTest.LESS);
        expect(mat.depthWrite).toBe(true);
        expect(mat.shadowCasting).toBe(ShadowCasting.ON);
        expect(mat.receiveShadows).toBe(true);
    });

    it('M-015 – isDirty starts true after construction', () => {
        const mat = new TestMaterial();
        expect(mat.isDirty).toBe(true);
    });

    it('M-016 – renderQueue defaults to 2000', () => {
        const mat = new TestMaterial();
        expect(mat.renderQueue).toBe(2000);
    });

    it('M-017 – config keywords are applied during init', () => {
        const mat = new TestMaterial({ keywords: ['_NORMALMAP', '_EMISSION'] });
        expect(mat.hasKeyword('_NORMALMAP')).toBe(true);
        expect(mat.hasKeyword('_EMISSION')).toBe(true);
    });

    it('M-018 – config properties override defaults', () => {
        const mat = new TestMaterial({
            properties: { '_Metallic': 0.8 },
        });
        expect(mat.getProperty('_Metallic')).toBe(0.8);
    });
});

describe('BaseMaterialComponent – Property Management', () => {
    let mat: TestMaterial;

    beforeEach(() => {
        mat = new TestMaterial();
    });

    it('M-020 – setProperty stores value', () => {
        mat.setProperty('_Metallic', 0.5);
        expect(mat.getProperty('_Metallic')).toBe(0.5);
    });

    it('M-021 – setProperty marks dirty', () => {
        (mat as any)._isDirty = false;
        mat.setProperty('_Metallic', 0.5);
        expect(mat.isDirty).toBe(true);
    });

    it('M-022 – setProperty with same value does NOT mark dirty', () => {
        mat.setProperty('_Metallic', 0.0); // same as default
        (mat as any)._isDirty = false;
        mat.setProperty('_Metallic', 0.0); // same value
        expect(mat.isDirty).toBe(false);
    });

    it('M-023 – getProperty returns null for missing property', () => {
        expect(mat.getProperty('_NonExistent')).toBeNull();
    });

    it('M-024 – hasProperty returns true for existing property', () => {
        mat.setProperty('_Color', { x: 1, y: 1, z: 1, w: 1 });
        expect(mat.hasProperty('_Color')).toBe(true);
    });

    it('M-025 – hasProperty returns false for missing property', () => {
        expect(mat.hasProperty('_NonExistent')).toBe(false);
    });

    it('M-026 – getPropertyNames returns all property names', () => {
        mat.setProperty('_Color', { x: 1, y: 1, z: 1, w: 1 });
        mat.setProperty('_Metallic', 0.5);
        const names = mat.getPropertyNames();
        expect(names).toContain('_Color');
        expect(names).toContain('_Metallic');
    });

    it('M-027 – setProperty with Vec4 value', () => {
        const color: Vec4 = { x: 0.1, y: 0.2, z: 0.3, w: 0.4 };
        mat.setProperty('_Color', color);
        expect(mat.getProperty('_Color')).toEqual(color);
    });

    it('M-028 – setProperty with null value', () => {
        mat.setProperty('_MainTex', null);
        expect(mat.getProperty('_MainTex')).toBeNull();
    });

    it('M-029 – setProperty with boolean value', () => {
        mat.setProperty('_Enabled', true);
        expect(mat.getProperty('_Enabled')).toBe(true);
    });

    it('M-030 – setProperty with Float32Array', () => {
        const arr = new Float32Array([1, 2, 3, 4]);
        mat.setProperty('_Data', arr);
        expect(mat.getProperty('_Data')).toBe(arr);
    });
});

describe('BaseMaterialComponent – Typed Accessors', () => {
    let mat: TestMaterial;

    beforeEach(() => {
        mat = new TestMaterial();
    });

    it('M-040 – setFloat / getFloat', () => {
        mat.setFloat('_Metallic', 0.75);
        expect(mat.getFloat('_Metallic')).toBe(0.75);
    });

    it('M-041 – getFloat returns 0 for missing property', () => {
        expect(mat.getFloat('_Missing')).toBe(0);
    });

    it('M-042 – setColor with Vec3', () => {
        const c: Vec3 = { x: 1, y: 0, z: 0 };
        mat.setColor('_Emission', c);
        expect(mat.getProperty('_Emission')).toEqual(c);
    });

    it('M-043 – getColor returns Vec4 for Vec3 stored value (promoted)', () => {
        const c: Vec3 = { x: 0.5, y: 0.5, z: 0.5 };
        mat.setColor('_Color', c);
        const result = mat.getColor('_Color');
        expect(result).toEqual({ x: 0.5, y: 0.5, z: 0.5, w: 1.0 });
    });

    it('M-044 – getColor returns Vec4 directly', () => {
        const c: Vec4 = { x: 0.1, y: 0.2, z: 0.3, w: 0.4 };
        mat.setProperty('_Color', c);
        expect(mat.getColor('_Color')).toEqual(c);
    });

    it('M-045 – getColor returns null for non-color value', () => {
        mat.setProperty('_Foo', 42);
        expect(mat.getColor('_Foo')).toBeNull();
    });

    it('M-046 – getColor returns null for missing property', () => {
        expect(mat.getColor('_Missing')).toBeNull();
    });

    it('M-047 – setVector / getVector', () => {
        const v: Vec4 = { x: 1, y: 2, z: 3, w: 4 };
        mat.setVector('_Dir', v);
        expect(mat.getVector('_Dir')).toEqual(v);
    });

    it('M-048 – getVector returns null for missing', () => {
        expect(mat.getVector('_Missing')).toBeNull();
    });

    it('M-049 – setMatrix / getMatrix', () => {
        const m: Mat4 = { elements: new Float32Array(16) };
        mat.setMatrix('_MVP', m);
        expect(mat.getMatrix('_MVP')).toBe(m);
    });

    it('M-050 – getMatrix returns null for missing', () => {
        expect(mat.getMatrix('_Missing')).toBeNull();
    });
});

describe('BaseMaterialComponent – Keyword Management', () => {
    let mat: TestMaterial;

    beforeEach(() => {
        mat = new TestMaterial();
    });

    it('M-060 – enableKeyword adds keyword', () => {
        mat.enableKeyword('_NORMALMAP');
        expect(mat.hasKeyword('_NORMALMAP')).toBe(true);
    });

    it('M-061 – disableKeyword removes keyword', () => {
        mat.enableKeyword('_NORMALMAP');
        mat.disableKeyword('_NORMALMAP');
        expect(mat.hasKeyword('_NORMALMAP')).toBe(false);
    });

    it('M-062 – enableKeyword marks dirty', () => {
        (mat as any)._isDirty = false;
        mat.enableKeyword('_EMISSION');
        expect(mat.isDirty).toBe(true);
    });

    it('M-063 – enableKeyword with existing keyword does NOT mark dirty', () => {
        mat.enableKeyword('_EMISSION');
        (mat as any)._isDirty = false;
        mat.enableKeyword('_EMISSION'); // already enabled
        expect(mat.isDirty).toBe(false);
    });

    it('M-064 – disableKeyword with missing keyword does NOT mark dirty', () => {
        (mat as any)._isDirty = false;
        mat.disableKeyword('_MISSING');
        expect(mat.isDirty).toBe(false);
    });

    it('M-065 – getKeywords returns all enabled keywords', () => {
        mat.enableKeyword('_A');
        mat.enableKeyword('_B');
        const kw = mat.getKeywords();
        expect(kw).toContain('_A');
        expect(kw).toContain('_B');
    });

    it('M-066 – hasKeyword returns false for missing keyword', () => {
        expect(mat.hasKeyword('_MISSING')).toBe(false);
    });
});

describe('BaseMaterialComponent – Render State', () => {
    let mat: TestMaterial;

    beforeEach(() => {
        mat = new TestMaterial();
    });

    it('M-070 – blendMode setter marks dirty', () => {
        (mat as any)._isDirty = false;
        mat.blendMode = BlendMode.ADDITIVE;
        expect(mat.isDirty).toBe(true);
        expect(mat.blendMode).toBe(BlendMode.ADDITIVE);
    });

    it('M-071 – blendMode setter with same value does NOT mark dirty', () => {
        (mat as any)._isDirty = false;
        mat.blendMode = BlendMode.OPAQUE; // same as default
        expect(mat.isDirty).toBe(false);
    });

    it('M-072 – cullMode setter marks dirty', () => {
        (mat as any)._isDirty = false;
        mat.cullMode = CullMode.NONE;
        expect(mat.isDirty).toBe(true);
    });

    it('M-073 – depthTest setter marks dirty', () => {
        (mat as any)._isDirty = false;
        mat.depthTest = DepthTest.ALWAYS;
        expect(mat.isDirty).toBe(true);
    });

    it('M-074 – depthWrite setter marks dirty', () => {
        (mat as any)._isDirty = false;
        mat.depthWrite = false;
        expect(mat.isDirty).toBe(true);
        expect(mat.depthWrite).toBe(false);
    });

    it('M-075 – shadowCasting setter marks dirty', () => {
        (mat as any)._isDirty = false;
        mat.shadowCasting = ShadowCasting.OFF;
        expect(mat.isDirty).toBe(true);
    });

    it('M-076 – receiveShadows setter marks dirty', () => {
        (mat as any)._isDirty = false;
        mat.receiveShadows = false;
        expect(mat.isDirty).toBe(true);
    });

    it('M-077 – renderQueue setter clamps to 0..5000', () => {
        mat.renderQueue = 9999;
        expect(mat.renderQueue).toBe(5000);

        mat.renderQueue = -100;
        expect(mat.renderQueue).toBe(0);
    });

    it('M-078 – renderQueue setter marks dirty', () => {
        (mat as any)._isDirty = false;
        mat.renderQueue = 3000;
        expect(mat.isDirty).toBe(true);
    });
});

describe('BaseMaterialComponent – Render Queue Auto-Update', () => {
    it('M-080 – switching to OPAQUE clamps renderQueue below 2500', () => {
        const mat = new TestMaterial({ blendMode: BlendMode.ALPHA_BLEND });
        mat.renderQueue = 3000;
        mat.blendMode = BlendMode.OPAQUE;
        expect(mat.renderQueue).toBe(2000);
    });

    it('M-081 – switching to ALPHA_TEST sets renderQueue to 2450', () => {
        const mat = new TestMaterial();
        mat.renderQueue = 2000;
        mat.blendMode = BlendMode.ALPHA_TEST;
        expect(mat.renderQueue).toBe(2450);
    });

    it('M-082 – switching to ALPHA_BLEND sets renderQueue to 3000', () => {
        const mat = new TestMaterial();
        mat.blendMode = BlendMode.ALPHA_BLEND;
        expect(mat.renderQueue).toBe(3000);
    });

    it('M-083 – switching to ADDITIVE sets renderQueue to 3000', () => {
        const mat = new TestMaterial();
        mat.blendMode = BlendMode.ADDITIVE;
        expect(mat.renderQueue).toBe(3000);
    });
});

describe('BaseMaterialComponent – Render Tags', () => {
    let mat: TestMaterial;

    beforeEach(() => {
        mat = new TestMaterial();
    });

    it('M-090 – render tags set after onInitialize', () => {
        (mat as any).onInitialize();
        expect(mat.getRenderTag('RenderType')).toBe('Opaque');
        expect(mat.getRenderTag('Queue')).toBe('AlphaTest');
    });

    it('M-091 – setRenderTag stores custom tag', () => {
        mat.setRenderTag('CustomTag', 'Value');
        expect(mat.getRenderTag('CustomTag')).toBe('Value');
    });

    it('M-092 – hasRenderTag returns true for existing tag', () => {
        mat.setRenderTag('RenderType', 'Opaque');
        expect(mat.hasRenderTag('RenderType')).toBe(true);
    });

    it('M-093 – hasRenderTag returns false for missing tag', () => {
        expect(mat.hasRenderTag('Missing')).toBe(false);
    });

    it('M-094 – getRenderTag returns null for missing tag', () => {
        expect(mat.getRenderTag('Missing')).toBeNull();
    });

    it('M-095 – RenderType tag is "Transparent" for non-opaque blend after onInitialize', () => {
        const mat2 = new TestMaterial({ blendMode: BlendMode.ALPHA_BLEND });
        (mat2 as any).onInitialize();
        expect(mat2.getRenderTag('RenderType')).toBe('Transparent');
    });
});

describe('BaseMaterialComponent – Serialization', () => {
    it('M-100 – serialize produces correct structure', () => {
        const mat = new TestMaterial({
            blendMode: BlendMode.ALPHA_BLEND,
            cullMode: CullMode.NONE,
        });
        mat.setProperty('_Metallic', 0.8);
        mat.enableKeyword('_NORMALMAP');

        const data = mat.serialize();
        expect(data.materialType).toBe(MaterialType.STANDARD);
        expect(data.blendMode).toBe(BlendMode.ALPHA_BLEND);
        expect(data.cullMode).toBe(CullMode.NONE);
        expect(data.properties['_Metallic']).toBe(0.8);
        expect(data.keywords).toContain('_NORMALMAP');
    });

    it('M-101 – deserialize restores state', () => {
        const mat = new TestMaterial();
        const data = {
            materialType: MaterialType.PBR,
            shaderName: 'Custom',
            renderQueue: 3000,
            blendMode: BlendMode.ADDITIVE,
            cullMode: CullMode.FRONT,
            depthTest: DepthTest.ALWAYS,
            depthWrite: false,
            shadowCasting: ShadowCasting.OFF,
            receiveShadows: false,
            properties: { '_Metallic': 0.9 },
            keywords: ['_EMISSION'],
            renderTags: { 'RenderType': 'Transparent' },
        };

        mat.deserialize(data);
        expect(mat.blendMode).toBe(BlendMode.ADDITIVE);
        expect(mat.cullMode).toBe(CullMode.FRONT);
        expect(mat.depthWrite).toBe(false);
        expect(mat.getProperty('_Metallic')).toBe(0.9);
        expect(mat.hasKeyword('_EMISSION')).toBe(true);
        expect(mat.isDirty).toBe(true);
    });

    it('M-102 – serialize/deserialize round-trip preserves data', () => {
        const mat = new TestMaterial({
            blendMode: BlendMode.ALPHA_TEST,
            properties: { '_Metallic': 0.5 },
        });
        mat.enableKeyword('_NORMALMAP');

        const serialized = mat.serialize();
        const mat2 = new TestMaterial();
        mat2.deserialize(serialized);

        expect(mat2.blendMode).toBe(BlendMode.ALPHA_TEST);
        expect(mat2.getProperty('_Metallic')).toBe(0.5);
        expect(mat2.hasKeyword('_NORMALMAP')).toBe(true);
    });

    it('M-103 – deserialize clears previous properties', () => {
        const mat = new TestMaterial();
        mat.setProperty('_Old', 42);

        mat.deserialize({
            materialType: MaterialType.STANDARD,
            properties: { '_New': 100 },
            keywords: [],
            renderTags: {},
        });

        expect(mat.hasProperty('_Old')).toBe(false);
        expect(mat.getProperty('_New')).toBe(100);
    });
});

describe('BaseMaterialComponent – Dirty Flag', () => {
    it('M-110 – _markDirty sets isDirty and updates lastModified', () => {
        const mat = new TestMaterial();
        const before = mat.lastModified;
        (mat as any)._isDirty = false;
        mat.setProperty('_Metallic', 1.0);

        expect(mat.isDirty).toBe(true);
        expect(mat.lastModified).toBeGreaterThanOrEqual(before);
    });

    it('M-111 – lastModified is updated after _markDirty is called', () => {
        const mat = new TestMaterial();
        mat.setProperty('_Test', 1);
        expect(mat.lastModified).toBeGreaterThan(0);
        expect(mat.lastModified).toBeLessThanOrEqual(performance.now());
    });
});

describe('BaseMaterialComponent – Destroy', () => {
    it('M-120 – onDestroy clears all maps', () => {
        const mat = new TestMaterial();
        mat.setProperty('_Metallic', 0.5);
        mat.enableKeyword('_NORMALMAP');
        mat.setRenderTag('Custom', 'Value');

        mat.onDestroy();

        expect(mat.getPropertyNames()).toHaveLength(0);
        expect(mat.getKeywords()).toHaveLength(0);
    });
});

describe('BaseMaterialComponent – Clone', () => {
    it('M-130 – clone creates a new instance with same data', () => {
        const mat = new TestMaterial({ blendMode: BlendMode.ADDITIVE });
        mat.setProperty('_Metallic', 0.7);
        mat.enableKeyword('_NORMALMAP');

        const cloned = mat.clone();
        expect(cloned).not.toBe(mat);
        expect(cloned.blendMode).toBe(BlendMode.ADDITIVE);
        expect(cloned.hasKeyword('_NORMALMAP')).toBe(true);
    });
});