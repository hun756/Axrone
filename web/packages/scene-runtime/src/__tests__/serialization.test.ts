import { describe, expect, it, vi } from 'vitest';
import { Mat4, Quat, Vec2, Vec3, Vec4 } from '@axrone/numeric';
import {
    encodeSceneValue,
    decodeSceneValue,
    cloneMeshDefinition,
    serializeMeshDefinition,
    deserializeMeshDefinition,
    cloneTextureBinding,
} from '../serialization';
import type { SceneMeshDefinition, SceneSerializedValue } from '../types';

describe('encodeSceneValue', () => {
    it('passes through null and undefined as null', () => {
        expect(encodeSceneValue(null)).toBeNull();
        expect(encodeSceneValue(undefined)).toBeNull();
    });

    it('passes through primitives unchanged', () => {
        expect(encodeSceneValue(42)).toBe(42);
        expect(encodeSceneValue(0)).toBe(0);
        expect(encodeSceneValue(-3.14)).toBe(-3.14);
        expect(encodeSceneValue('hello')).toBe('hello');
        expect(encodeSceneValue('')).toBe('');
        expect(encodeSceneValue(true)).toBe(true);
        expect(encodeSceneValue(false)).toBe(false);
    });

    it('encodes Vec2 with $type tag', () => {
        const v = new Vec2(1.5, 2.5);
        expect(encodeSceneValue(v)).toEqual({ $type: 'Vec2', value: [1.5, 2.5] });
    });

    it('encodes Vec3 with $type tag', () => {
        const v = new Vec3(1, 2, 3);
        expect(encodeSceneValue(v)).toEqual({ $type: 'Vec3', value: [1, 2, 3] });
    });

    it('encodes Vec4 with $type tag', () => {
        const v = new Vec4(0.1, 0.2, 0.3, 0.4);
        expect(encodeSceneValue(v)).toEqual({ $type: 'Vec4', value: [0.1, 0.2, 0.3, 0.4] });
    });

    it('encodes Quat with $type tag', () => {
        const q = new Quat(0, 0, 0, 1);
        expect(encodeSceneValue(q)).toEqual({ $type: 'Quat', value: [0, 0, 0, 1] });
    });

    it('encodes Mat4 with $type tag', () => {
        const m = Mat4.IDENTITY;
        const encoded = encodeSceneValue(m) as { $type: string; value: number[] };
        expect(encoded.$type).toBe('Mat4');
        expect(encoded.value).toHaveLength(16);
        expect(encoded.value[0]).toBe(1);
        expect(encoded.value[5]).toBe(1);
        expect(encoded.value[10]).toBe(1);
        expect(encoded.value[15]).toBe(1);
    });

    it('encodes typed arrays with their constructor name', () => {
        const f32 = new Float32Array([1, 2, 3]);
        const encoded = encodeSceneValue(f32) as { $type: string; value: number[] };
        expect(encoded.$type).toBe('Float32Array');
        expect(encoded.value).toEqual([1, 2, 3]);

        const u32 = new Uint32Array([10, 20]);
        const encodedU32 = encodeSceneValue(u32) as { $type: string; value: number[] };
        expect(encodedU32.$type).toBe('Uint32Array');
        expect(encodedU32.value).toEqual([10, 20]);

        const u16 = new Uint16Array([5, 6]);
        expect(encodeSceneValue(u16)).toEqual({ $type: 'Uint16Array', value: [5, 6] });

        const u8 = new Uint8Array([255, 0]);
        expect(encodeSceneValue(u8)).toEqual({ $type: 'Uint8Array', value: [255, 0] });

        const i32 = new Int32Array([-1, 2]);
        expect(encodeSceneValue(i32)).toEqual({ $type: 'Int32Array', value: [-1, 2] });
    });

    it('recursively encodes arrays', () => {
        const result = encodeSceneValue([1, 'two', new Vec3(3, 4, 5)]);
        expect(result).toEqual([
            1,
            'two',
            { $type: 'Vec3', value: [3, 4, 5] },
        ]);
    });

    it('recursively encodes plain objects', () => {
        const result = encodeSceneValue({
            name: 'test',
            position: new Vec3(1, 2, 3),
            nested: { value: 42 },
        });
        expect(result).toEqual({
            name: 'test',
            position: { $type: 'Vec3', value: [1, 2, 3] },
            nested: { value: 42 },
        });
    });

    it('converts unknown types to string', () => {
        expect(encodeSceneValue(Symbol('test'))).toBe('Symbol(test)');
    });
});

describe('decodeSceneValue', () => {
    it('passes through null and primitives', () => {
        expect(decodeSceneValue(null)).toBeNull();
        expect(decodeSceneValue(42)).toBe(42);
        expect(decodeSceneValue('hello')).toBe('hello');
        expect(decodeSceneValue(true)).toBe(true);
    });

    it('decodes Vec2 from $type tag', () => {
        const result = decodeSceneValue({ $type: 'Vec2', value: [1.5, 2.5] });
        expect(result).toBeInstanceOf(Vec2);
        expect((result as Vec2).x).toBe(1.5);
        expect((result as Vec2).y).toBe(2.5);
    });

    it('decodes Vec3 from $type tag', () => {
        const result = decodeSceneValue({ $type: 'Vec3', value: [1, 2, 3] });
        expect(result).toBeInstanceOf(Vec3);
        expect((result as Vec3).x).toBe(1);
        expect((result as Vec3).y).toBe(2);
        expect((result as Vec3).z).toBe(3);
    });

    it('decodes Vec4 from $type tag', () => {
        const result = decodeSceneValue({ $type: 'Vec4', value: [0.1, 0.2, 0.3, 0.4] });
        expect(result).toBeInstanceOf(Vec4);
    });

    it('decodes Quat from $type tag', () => {
        const result = decodeSceneValue({ $type: 'Quat', value: [0, 0, 0, 1] });
        expect(result).toBeInstanceOf(Quat);
    });

    it('decodes Mat4 from $type tag', () => {
        const identityData = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        const result = decodeSceneValue({ $type: 'Mat4', value: identityData });
        expect(result).toBeInstanceOf(Mat4);
    });

    it('decodes typed arrays from $type tag', () => {
        const f32 = decodeSceneValue({ $type: 'Float32Array', value: [1, 2, 3] });
        expect(f32).toBeInstanceOf(Float32Array);
        expect([...(f32 as Float32Array)]).toEqual([1, 2, 3]);

        const i32 = decodeSceneValue({ $type: 'Int32Array', value: [-1, 2] });
        expect(i32).toBeInstanceOf(Int32Array);

        const u32 = decodeSceneValue({ $type: 'Uint32Array', value: [10, 20] });
        expect(u32).toBeInstanceOf(Uint32Array);

        const u16 = decodeSceneValue({ $type: 'Uint16Array', value: [5, 6] });
        expect(u16).toBeInstanceOf(Uint16Array);

        const u8 = decodeSceneValue({ $type: 'Uint8Array', value: [255, 0] });
        expect(u8).toBeInstanceOf(Uint8Array);
    });

    it('falls back to raw array for unknown $type', () => {
        const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
        const result = decodeSceneValue({ $type: 'UnknownType', value: [1, 2, 3] });
        expect(Array.isArray(result)).toBe(true);
        expect(result).toEqual([1, 2, 3]);
        warnSpy.mockRestore();
    });

    it('recursively decodes arrays', () => {
        const result = decodeSceneValue([1, { $type: 'Vec3', value: [3, 4, 5] }]);
        expect(result).toEqual([1, expect.any(Vec3)]);
    });

    it('recursively decodes plain objects', () => {
        const result = decodeSceneValue({
            name: 'test',
            position: { $type: 'Vec3', value: [1, 2, 3] },
        });
        const obj = result as Record<string, unknown>;
        expect(obj.name).toBe('test');
        expect(obj.position).toBeInstanceOf(Vec3);
    });
});

describe('encodeSceneValue ↔ decodeSceneValue roundtrip', () => {
    it('roundtrips Vec3', () => {
        const original = new Vec3(1.5, -2.3, 4.7);
        const decoded = decodeSceneValue(encodeSceneValue(original)) as Vec3;
        expect(decoded).toBeInstanceOf(Vec3);
        expect(decoded.x).toBeCloseTo(original.x);
        expect(decoded.y).toBeCloseTo(original.y);
        expect(decoded.z).toBeCloseTo(original.z);
    });

    it('roundtrips nested object with mixed types', () => {
        const original = {
            name: 'entity',
            position: new Vec3(1, 2, 3),
            rotation: new Quat(0, 0, 0, 1),
            scale: new Vec3(1, 1, 1),
            data: new Float32Array([0.5, 0.25]),
        };
        const decoded = decodeSceneValue(encodeSceneValue(original)) as Record<string, unknown>;
        expect(decoded.name).toBe('entity');
        expect(decoded.position).toBeInstanceOf(Vec3);
        expect(decoded.rotation).toBeInstanceOf(Quat);
        expect(decoded.scale).toBeInstanceOf(Vec3);
        expect(decoded.data).toBeInstanceOf(Float32Array);
    });
});

const createMinimalMesh = (): SceneMeshDefinition => ({
    id: 'mesh/test',
    vertices: new Float32Array([0, 0, 0, 1, 0, 0, 0, 1, 0]),
    attributes: [
        { semantic: 'position' as const, componentCount: 3 as const, offset: 0, stride: 12 },
    ],
});

describe('cloneMeshDefinition', () => {
    it('creates a deep copy of vertices as Uint8Array byte-level copy', () => {
        const mesh = createMinimalMesh();
        const cloned = cloneMeshDefinition(mesh);
        expect(cloned.vertices).not.toBe(mesh.vertices);
        expect(cloned.vertices).toBeInstanceOf(Uint8Array);
        const originalBytes = new Uint8Array(
            (mesh.vertices as Float32Array).buffer,
            (mesh.vertices as Float32Array).byteOffset,
            (mesh.vertices as Float32Array).byteLength
        );
        expect([...(cloned.vertices as Uint8Array)]).toEqual([...originalBytes]);
    });

    it('preserves indices with correct typed array type', () => {
        const mesh: SceneMeshDefinition = {
            ...createMinimalMesh(),
            indices: new Uint16Array([0, 1, 2]),
        };
        const cloned = cloneMeshDefinition(mesh);
        expect(cloned.indices).not.toBe(mesh.indices);
        expect(cloned.indices).toBeInstanceOf(Uint16Array);
        expect([...(cloned.indices as Uint16Array)]).toEqual([0, 1, 2]);
    });

    it('preserves Uint32Array indices', () => {
        const mesh: SceneMeshDefinition = {
            ...createMinimalMesh(),
            indices: new Uint32Array([0, 1, 2]),
        };
        const cloned = cloneMeshDefinition(mesh);
        expect(cloned.indices).toBeInstanceOf(Uint32Array);
    });

    it('preserves Uint8Array indices', () => {
        const mesh: SceneMeshDefinition = {
            ...createMinimalMesh(),
            indices: new Uint8Array([0, 1, 2]),
        };
        const cloned = cloneMeshDefinition(mesh);
        expect(cloned.indices).toBeInstanceOf(Uint8Array);
    });

    it('clones bounding sphere', () => {
        const mesh: SceneMeshDefinition = {
            ...createMinimalMesh(),
            bounds: { kind: 'sphere', center: [1, 2, 3], radius: 5 },
        };
        const cloned = cloneMeshDefinition(mesh);
        expect(cloned.bounds).not.toBe(mesh.bounds);
        expect(cloned.bounds).toEqual({ kind: 'sphere', center: [1, 2, 3], radius: 5 });
    });

    it('clones morph targets with deep-copied Float32Array values', () => {
        const mesh: SceneMeshDefinition = {
            ...createMinimalMesh(),
            morphTargets: [
                {
                    name: 'smile',
                    attributes: [
                        {
                            semantic: 'position' as const,
                            componentCount: 3 as const,
                            values: new Float32Array([0.1, 0.2, 0.3]),
                        },
                    ],
                },
            ],
        };
        const cloned = cloneMeshDefinition(mesh);
        expect(cloned.morphTargets).toBeDefined();
        expect(cloned.morphTargets![0]!.name).toBe('smile');
        expect(cloned.morphTargets![0]!.attributes[0]!.values).toBeInstanceOf(Float32Array);
        expect(cloned.morphTargets![0]!.attributes[0]!.values).not.toBe(
            mesh.morphTargets![0]!.attributes[0]!.values
        );
        const clonedValues = cloned.morphTargets![0]!.attributes[0]!.values;
        expect(clonedValues[0]).toBeCloseTo(0.1, 5);
        expect(clonedValues[1]).toBeCloseTo(0.2, 5);
        expect(clonedValues[2]).toBeCloseTo(0.3, 5);
    });

    it('handles mesh without optional fields', () => {
        const mesh = createMinimalMesh();
        const cloned = cloneMeshDefinition(mesh);
        expect(cloned.id).toBe('mesh/test');
        expect(cloned.indices).toBeUndefined();
        expect(cloned.bounds).toBeUndefined();
        expect(cloned.morphTargets).toBeUndefined();
    });
});

describe('serializeMeshDefinition ↔ deserializeMeshDefinition roundtrip', () => {
    it('roundtrips a minimal mesh', () => {
        const mesh = createMinimalMesh();
        const serialized = serializeMeshDefinition(mesh);
        const deserialized = deserializeMeshDefinition(serialized);

        expect(deserialized.id).toBe('mesh/test');
        expect(deserialized.vertices).toBeInstanceOf(Uint8Array);
        expect(deserialized.attributes).toHaveLength(1);
        expect(deserialized.attributes[0]!.semantic).toBe('position');
    });

    it('roundtrips mesh with indices', () => {
        const mesh: SceneMeshDefinition = {
            ...createMinimalMesh(),
            indices: new Uint16Array([0, 1, 2]),
        };
        const serialized = serializeMeshDefinition(mesh);
        const deserialized = deserializeMeshDefinition(serialized);

        expect(deserialized.indices).toBeInstanceOf(Uint16Array);
        expect([...(deserialized.indices as Uint16Array)]).toEqual([0, 1, 2]);
    });

    it('roundtrips mesh with bounds', () => {
        const mesh: SceneMeshDefinition = {
            ...createMinimalMesh(),
            bounds: { kind: 'sphere', center: [1, 2, 3], radius: 5 },
        };
        const serialized = serializeMeshDefinition(mesh);
        const deserialized = deserializeMeshDefinition(serialized);

        expect(deserialized.bounds).toBeDefined();
        expect(deserialized.bounds!.kind).toBe('sphere');
        expect(deserialized.bounds!.center).toEqual([1, 2, 3]);
        expect(deserialized.bounds!.radius).toBe(5);
    });

    it('roundtrips mesh with morph targets', () => {
        const mesh: SceneMeshDefinition = {
            ...createMinimalMesh(),
            morphTargets: [
                {
                    name: 'blink',
                    attributes: [
                        {
                            semantic: 'position' as const,
                            componentCount: 3 as const,
                            values: new Float32Array([0, 0.5, 0]),
                        },
                    ],
                },
            ],
        };
        const serialized = serializeMeshDefinition(mesh);
        const deserialized = deserializeMeshDefinition(serialized);

        expect(deserialized.morphTargets).toBeDefined();
        expect(deserialized.morphTargets).toHaveLength(1);
        expect(deserialized.morphTargets![0]!.name).toBe('blink');
        expect(deserialized.morphTargets![0]!.attributes[0]!.values).toBeInstanceOf(Float32Array);
        expect([...deserialized.morphTargets![0]!.attributes[0]!.values]).toEqual([0, 0.5, 0]);
    });

    it('preserves topology and vertexCount', () => {
        const mesh: SceneMeshDefinition = {
            ...createMinimalMesh(),
            topology: 'lines',
            vertexCount: 3,
        };
        const serialized = serializeMeshDefinition(mesh);
        const deserialized = deserializeMeshDefinition(serialized);

        expect(deserialized.topology).toBe('lines');
        expect(deserialized.vertexCount).toBe(3);
    });

    it('defaults topology to triangles when missing', () => {
        const serialized: SceneSerializedValue = {
            id: 'mesh/test',
            vertices: [],
            indices: null,
            morphTargets: null,
            bounds: null,
            vertexCount: null,
            topology: null,
            usage: null,
            attributes: [],
            vertexArrayType: 'Uint8Array',
            indexArrayType: null,
        };
        const deserialized = deserializeMeshDefinition(serialized);
        expect(deserialized.topology).toBe('triangles');
    });

    it('throws on invalid serialized data', () => {
        expect(() => deserializeMeshDefinition(null)).toThrow('Invalid serialized mesh definition');
        expect(() => deserializeMeshDefinition([])).toThrow('Invalid serialized mesh definition');
    });

    it('throws on invalid morph target data', () => {
        const serialized: SceneSerializedValue = {
            id: 'mesh/test',
            vertices: [],
            indices: null,
            morphTargets: [null],
            bounds: null,
            vertexCount: null,
            topology: 'triangles',
            usage: null,
            attributes: [],
            vertexArrayType: 'Uint8Array',
            indexArrayType: null,
        };
        expect(() => deserializeMeshDefinition(serialized)).toThrow(
            'Invalid serialized mesh morph target'
        );
    });

    it('restores Uint32Array indices from indexArrayType', () => {
        const mesh: SceneMeshDefinition = {
            ...createMinimalMesh(),
            indices: new Uint32Array([100, 200, 300]),
        };
        const serialized = serializeMeshDefinition(mesh);
        const deserialized = deserializeMeshDefinition(serialized);
        expect(deserialized.indices).toBeInstanceOf(Uint32Array);
        expect([...(deserialized.indices as Uint32Array)]).toEqual([100, 200, 300]);
    });
});

describe('cloneTextureBinding', () => {
    it('returns string bindings unchanged', () => {
        expect(cloneTextureBinding('texture/albedo')).toBe('texture/albedo');
    });

    it('creates a shallow copy of object bindings', () => {
        const binding = { id: 'texture/albedo', srgb: true };
        const cloned = cloneTextureBinding(binding);
        expect(cloned).not.toBe(binding);
        expect(cloned).toEqual(binding);
    });
});
