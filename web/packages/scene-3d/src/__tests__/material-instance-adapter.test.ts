import { Vec4 } from '@axrone/numeric';
import { describe, expect, it } from 'vitest';
import {
    convertSceneUniformValue,
    SceneMaterialInstanceAdapter,
    type SceneMaterialAdapterDependencies,
    type SceneMaterialAdapterTextureEntry,
    type SceneMaterialResource,
} from '@axrone/scene-3d';

describe('convertSceneUniformValue', () => {
    it('passes through numbers', () => {
        expect(convertSceneUniformValue(0.5)).toBe(0.5);
    });

    it('passes through booleans', () => {
        expect(convertSceneUniformValue(true)).toBe(true);
    });

    it('passes through Float32Array', () => {
        const arr = new Float32Array([1, 2, 3]);
        expect(convertSceneUniformValue(arr)).toBe(arr);
    });

    it('passes through Int32Array', () => {
        const arr = new Int32Array([1, 2, 3]);
        expect(convertSceneUniformValue(arr)).toBe(arr);
    });

    it('passes through Uint32Array', () => {
        const arr = new Uint32Array([1, 2, 3]);
        expect(convertSceneUniformValue(arr)).toBe(arr);
    });

    it('passes through Vec4', () => {
        const vec = new Vec4(1, 2, 3, 4);
        expect(convertSceneUniformValue(vec)).toBe(vec);
    });

    it('converts readonly number[] to Float32Array', () => {
        const arr: readonly number[] = [1, 2, 3, 4];
        const result = convertSceneUniformValue(arr);
        expect(result).toBeInstanceOf(Float32Array);
        expect(result).toEqual(new Float32Array([1, 2, 3, 4]));
    });

    it('converts Quat to Float32Array(4)', () => {
        const quat = { x: 0, y: 0.707, z: 0, w: 0.707 };
        const result = convertSceneUniformValue(quat as any);
        expect(result).toBeInstanceOf(Float32Array);
        expect(result).toEqual(new Float32Array([0, 0.707, 0, 0.707]));
    });

    it('converts null to 0', () => {
        expect(convertSceneUniformValue(null as any)).toBe(0);
    });
});

describe('SceneMaterialInstanceAdapter', () => {
    const createMockMaterial = (
        overrides?: Partial<SceneMaterialResource>
    ): SceneMaterialResource => ({
        id: 'mat/test',
        shaderId: 'shader/basic',
        uniforms: new Map([
            ['u_Color', new Vec4(1, 0, 0, 1)],
            ['u_Metallic', 0.5],
        ]),
        textureBindings: new Map(),
        surface: null,
        passes: [],
        keywords: new Map([
            ['FOG', { enabled: true, source: 'explicit' }],
            ['SHADOWS', { enabled: false, source: 'auto' }],
        ]),
        ...overrides,
    });

    const createMockDependencies = (
        textures: SceneMaterialAdapterTextureEntry[] = []
    ): SceneMaterialAdapterDependencies => ({
        resolveTextures: () => textures,
    });

    it('exposes material id and shaderId', () => {
        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies()
        );

        expect(adapter.id).toBe('mat/test');
        expect(adapter.shaderId).toBe('shader/basic');
    });

    it('returns converted uniform values via getProperty', () => {
        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies()
        );

        expect(adapter.getProperty('u_Color')).toBeInstanceOf(Vec4);
        expect(adapter.getProperty('u_Metallic')).toBe(0.5);
    });

    it('returns null for unknown properties', () => {
        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies()
        );

        expect(adapter.getProperty('u_Unknown')).toBeNull();
    });

    it('hasProperty checks existence', () => {
        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies()
        );

        expect(adapter.hasProperty('u_Color')).toBe(true);
        expect(adapter.hasProperty('u_Unknown')).toBe(false);
    });

    it('getAllProperties returns converted map', () => {
        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies()
        );

        const all = adapter.getAllProperties();
        expect(all.size).toBe(2);
        expect(all.get('u_Color')).toBeInstanceOf(Vec4);
        expect(all.get('u_Metallic')).toBe(0.5);
    });

    it('getUniformNames lists uniform names', () => {
        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies()
        );

        const names = adapter.getUniformNames();
        expect(names).toContain('u_Color');
        expect(names).toContain('u_Metallic');
    });

    it('hasKeyword returns enabled state', () => {
        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies()
        );

        expect(adapter.hasKeyword('FOG')).toBe(true);
        expect(adapter.hasKeyword('SHADOWS')).toBe(false);
        expect(adapter.hasKeyword('UNKNOWN')).toBe(false);
    });

    it('getEnabledKeywords returns only enabled keywords', () => {
        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies()
        );

        const enabled = adapter.getEnabledKeywords();
        expect(enabled).toContain('FOG');
        expect(enabled).not.toContain('SHADOWS');
    });

    it('getKeywordState returns source tracking', () => {
        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies()
        );

        expect(adapter.getKeywordState('FOG')).toEqual({
            enabled: true,
            source: 'explicit',
        });
        expect(adapter.getKeywordState('SHADOWS')).toEqual({
            enabled: false,
            source: 'auto',
        });
        expect(adapter.getKeywordState('UNKNOWN')).toBeNull();
    });

    it('getTextureBindings resolves through dependencies', () => {
        const mockTexture: SceneMaterialAdapterTextureEntry = {
            textureId: 'tex/checker',
            samplerId: 'sampler/linear',
            unit: 0,
            nativeTexture: {} as WebGLTexture,
            width: 256,
            height: 256,
        };

        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies([mockTexture])
        );

        const bindings = adapter.getTextureBindings();
        expect(bindings).toHaveLength(1);
        expect(bindings[0].textureId).toBe('tex/checker');
    });

    it('getStats returns correct counts', () => {
        const adapter = new SceneMaterialInstanceAdapter(
            createMockMaterial(),
            createMockDependencies()
        );

        const stats = adapter.getStats();
        expect(stats.uniformCount).toBe(2);
        expect(stats.keywordCount).toBe(2);
        expect(stats.enabledKeywordCount).toBe(1);
        expect(stats.textureCount).toBe(0);
    });

    it('converts readonly number[] uniforms to Float32Array', () => {
        const material = createMockMaterial({
            uniforms: new Map([['u_Matrix', [1, 0, 0, 0, 1, 0, 0, 0, 1] as readonly number[]]]),
        });

        const adapter = new SceneMaterialInstanceAdapter(
            material,
            createMockDependencies()
        );

        const result = adapter.getProperty('u_Matrix');
        expect(result).toBeInstanceOf(Float32Array);
    });
});
