import { describe, expect, it } from 'vitest';
import { Vec3 } from '@axrone/numeric';
import {
    GeometryBuilder,
    getAttributeTypeSize,
    createVertexAttribute,
    createGeometryLayout,
    DEFAULT_PRIMITIVE_CONFIG,
    VERTEX_ATTRIBUTES,
} from '@axrone/geometry';

// GLAttributeType is a const enum – values are inlined at compile time.
const GL_FLOAT = 0x1406;
const GL_UNSIGNED_SHORT = 0x1403;
const GL_UNSIGNED_INT = 0x1405;
const GL_BYTE = 0x1400;
const GL_UNSIGNED_BYTE = 0x1401;
const GL_SHORT = 0x1402;
const GL_INT = 0x1404;

// ByteBuffer defaults to big-endian byte order, so typed-array views (native endian)
// produce incorrect values. Use the buffer's own getters for correct reads.
const readFloat32s = (bb: any): number[] => {
    bb.rewind();
    const out: number[] = [];
    while (bb.remaining >= 4) out.push(bb.getFloat32());
    return out;
};
const readUint16s = (bb: any): number[] => {
    bb.rewind();
    const out: number[] = [];
    while (bb.remaining >= 2) out.push(bb.getUint16());
    return out;
};
const readUint32s = (bb: any): number[] => {
    bb.rewind();
    const out: number[] = [];
    while (bb.remaining >= 4) out.push(bb.getUint32());
    return out;
};

describe('GeometryBuilder', () => {
    describe('Construction', () => {
        it('creates with default config', () => {
            const builder = new GeometryBuilder();
            expect(builder.config.generateNormals).toBe(true);
            expect(builder.config.generateTexCoords).toBe(true);
            expect(builder.config.generateTangents).toBe(false);
            expect(builder.config.flipWindingOrder).toBe(false);
            expect(builder.config.useIndexBuffer).toBe(true);
        });

        it('creates with custom config', () => {
            const builder = new GeometryBuilder({ generateNormals: false, generateTexCoords: false });
            expect(builder.config.generateNormals).toBe(false);
            expect(builder.config.generateTexCoords).toBe(false);
        });

        it('GeometryBuilder.create() factory works', () => {
            const builder = GeometryBuilder.create({ generateNormals: false });
            expect(builder).toBeInstanceOf(GeometryBuilder);
            expect(builder.config.generateNormals).toBe(false);
        });
    });

    describe('addVertex', () => {
        it('returns incrementing index starting from 0', () => {
            const builder = new GeometryBuilder();
            expect(builder.addVertex(Vec3.create(0, 0, 0))).toBe(0);
            expect(builder.addVertex(Vec3.create(1, 0, 0))).toBe(1);
            expect(builder.addVertex(Vec3.create(0, 1, 0))).toBe(2);
        });

        it('stores position', () => {
            const builder = new GeometryBuilder();
            builder.addVertex(Vec3.create(0, 0, 0));
            builder.addVertex(Vec3.create(1, 0, 0));
            builder.addVertex(Vec3.create(0, 1, 0));
            builder.addTriangle(0, 1, 2);
            const result = builder.build();
            expect(result.layout.vertexCount).toBe(3);
        });

        it('stores normal when generateNormals is true and normal is provided', () => {
            const builder = new GeometryBuilder({ generateNormals: true });
            builder.addVertex(Vec3.create(0, 0, 0), Vec3.create(0, 1, 0));
            builder.addVertex(Vec3.create(1, 0, 0), Vec3.create(0, 1, 0));
            builder.addVertex(Vec3.create(0, 1, 0), Vec3.create(0, 1, 0));
            builder.addTriangle(0, 1, 2);
            const result = builder.build();
            // stride = 3*4 (position) + 3*4 (normal) + 2*4 (texCoord, default on) = 32
            expect(result.layout.stride).toBe(32);
        });

        it('stores texCoord when generateTexCoords is true and texCoord is provided', () => {
            const builder = new GeometryBuilder({ generateTexCoords: true, generateNormals: false });
            builder.addVertex(Vec3.create(0, 0, 0), undefined, { u: 0, v: 0 });
            builder.addVertex(Vec3.create(1, 0, 0), undefined, { u: 1, v: 0 });
            builder.addVertex(Vec3.create(0, 1, 0), undefined, { u: 0, v: 1 });
            builder.addTriangle(0, 1, 2);
            const result = builder.build();
            // stride = 3*4 (position) + 2*4 (texCoord) = 20
            expect(result.layout.stride).toBe(20);
        });

        it('ignores normal when generateNormals is false', () => {
            const builder = new GeometryBuilder({ generateNormals: false });
            builder.addVertex(Vec3.create(0, 0, 0), Vec3.create(0, 1, 0));
            builder.addVertex(Vec3.create(1, 0, 0), Vec3.create(0, 1, 0));
            builder.addVertex(Vec3.create(0, 1, 0), Vec3.create(0, 1, 0));
            builder.addTriangle(0, 1, 2);
            const result = builder.build();
            // stride = 3*4 (position) + 2*4 (texCoord, default on) = 20
            expect(result.layout.stride).toBe(20);
        });
    });

    describe('addTriangle', () => {
        it('stores 3 indices in order', () => {
            const builder = new GeometryBuilder({ generateNormals: false, generateTexCoords: false });
            builder.addVertex(Vec3.create(0, 0, 0));
            builder.addVertex(Vec3.create(1, 0, 0));
            builder.addVertex(Vec3.create(0, 1, 0));
            builder.addTriangle(0, 1, 2);
            expect(builder.indexCount).toBe(3);
        });

        it('reverses winding order when flipWindingOrder is true', () => {
            const builder = new GeometryBuilder({
                flipWindingOrder: true,
                generateNormals: false,
                generateTexCoords: false,
            });
            builder.addVertex(Vec3.create(0, 0, 0));
            builder.addVertex(Vec3.create(1, 0, 0));
            builder.addVertex(Vec3.create(0, 1, 0));
            builder.addTriangle(0, 1, 2);
            // With flip, indices should be (0, 2, 1) instead of (0, 1, 2)
            expect(builder.indexCount).toBe(3);
            // Verify by reading the index buffer (16-bit indices)
            const result = builder.build();
            const view = readUint16s(result.indices);
            expect(view[0]).toBe(0);
            expect(view[1]).toBe(2);
            expect(view[2]).toBe(1);
        });
    });

    describe('addQuad', () => {
        it('stores 6 indices (2 triangles)', () => {
            const builder = new GeometryBuilder({ generateNormals: false, generateTexCoords: false });
            builder.addVertex(Vec3.create(0, 0, 0));
            builder.addVertex(Vec3.create(1, 0, 0));
            builder.addVertex(Vec3.create(1, 1, 0));
            builder.addVertex(Vec3.create(0, 1, 0));
            builder.addQuad(0, 1, 2, 3);
            expect(builder.indexCount).toBe(6);
        });
    });

    describe('computeNormals', () => {
        it('auto-computes face normals and normalizes them', () => {
            const builder = new GeometryBuilder({
                generateNormals: true,
                generateTexCoords: false,
            });
            builder.addVertex(Vec3.create(0, 0, 0));
            builder.addVertex(Vec3.create(1, 0, 0));
            builder.addVertex(Vec3.create(0, 1, 0));
            builder.addTriangle(0, 1, 2);
            builder.computeNormals();

            const result = builder.build();
            // Read normals from vertex buffer (stride = 24: 12 pos + 12 normal)
            const floatView = readFloat32s(result.vertices);
            // First vertex normal starts at offset 3 (after x,y,z position)
            const nx = floatView[3]!;
            const ny = floatView[4]!;
            const nz = floatView[5]!;
            const len = Math.sqrt(nx * nx + ny * ny + nz * nz);
            expect(len).toBeCloseTo(1, 5);
        });

        it('skips computation when generateNormals is false', () => {
            const builder = new GeometryBuilder({
                generateNormals: false,
                generateTexCoords: false,
            });
            builder.addVertex(Vec3.create(0, 0, 0));
            builder.addVertex(Vec3.create(1, 0, 0));
            builder.addVertex(Vec3.create(0, 1, 0));
            builder.addTriangle(0, 1, 2);
            // Should return without error
            builder.computeNormals();
            expect(builder.vertexCount).toBe(3);
        });
    });

    describe('computeTangents', () => {
        it('skips when generateTangents is false', () => {
            const builder = new GeometryBuilder({
                generateTangents: false,
                generateTexCoords: true,
            });
            builder.addVertex(Vec3.create(0, 0, 0), Vec3.create(0, 1, 0), { u: 0, v: 0 });
            // Should return without error
            builder.computeTangents();
            expect(builder.vertexCount).toBe(1);
        });

        it('skips when generateTexCoords is false', () => {
            const builder = new GeometryBuilder({
                generateTangents: true,
                generateTexCoords: false,
            });
            builder.addVertex(Vec3.create(0, 0, 0), Vec3.create(0, 1, 0));
            builder.computeTangents();
            expect(builder.vertexCount).toBe(1);
        });
    });

    describe('build', () => {
        it('auto-computes normals if generateNormals is true and vertices lack normals', () => {
            const builder = new GeometryBuilder({
                generateNormals: true,
                generateTexCoords: false,
            });
            builder.addVertex(Vec3.create(0, 0, 0));
            builder.addVertex(Vec3.create(1, 0, 0));
            builder.addVertex(Vec3.create(0, 1, 0));
            builder.addTriangle(0, 1, 2);

            const result = builder.build();
            // Layout should include normal attribute
            expect(result.layout.attributes.length).toBe(2); // position + normal
            expect(result.layout.stride).toBe(24); // 12 pos + 12 normal
        });

        it('produces correct buffer sizes based on enabled attributes', () => {
            const builder = new GeometryBuilder({
                generateNormals: true,
                generateTexCoords: true,
                generateTangents: false,
            });
            builder.addVertex(Vec3.create(0, 0, 0), Vec3.create(0, 1, 0), { u: 0, v: 0 });
            builder.addVertex(Vec3.create(1, 0, 0), Vec3.create(0, 1, 0), { u: 1, v: 0 });
            builder.addVertex(Vec3.create(0, 1, 0), Vec3.create(0, 1, 0), { u: 0, v: 1 });
            builder.addTriangle(0, 1, 2);

            const result = builder.build();
            // stride = 12 (pos) + 12 (normal) + 8 (texcoord) = 32
            expect(result.layout.stride).toBe(32);
            expect(result.layout.vertexCount).toBe(3);
            expect(result.layout.indexCount).toBe(3);
        });
    });

    describe('clear', () => {
        it('resets vertex and index count to 0', () => {
            const builder = new GeometryBuilder({ generateNormals: false, generateTexCoords: false });
            builder.addVertex(Vec3.create(0, 0, 0));
            builder.addVertex(Vec3.create(1, 0, 0));
            builder.addVertex(Vec3.create(0, 1, 0));
            builder.addTriangle(0, 1, 2);
            expect(builder.vertexCount).toBe(3);
            expect(builder.indexCount).toBe(3);

            builder.clear();
            expect(builder.vertexCount).toBe(0);
            expect(builder.indexCount).toBe(0);
        });
    });

    describe('vertexCount / indexCount', () => {
        it('tracks counts accurately across operations', () => {
            const builder = new GeometryBuilder({ generateNormals: false, generateTexCoords: false });
            expect(builder.vertexCount).toBe(0);
            expect(builder.indexCount).toBe(0);

            builder.addVertex(Vec3.create(0, 0, 0));
            expect(builder.vertexCount).toBe(1);

            builder.addVertex(Vec3.create(1, 0, 0));
            builder.addVertex(Vec3.create(0, 1, 0));
            expect(builder.vertexCount).toBe(3);

            builder.addTriangle(0, 1, 2);
            expect(builder.indexCount).toBe(3);

            builder.addQuad(0, 1, 2, 0);
            expect(builder.indexCount).toBe(9); // 3 + 6
        });
    });

    describe('32-bit index auto-promotion', () => {
        it('uses UNSIGNED_INT when vertex count exceeds 65535', () => {
            const builder = new GeometryBuilder({
                generateNormals: false,
                generateTexCoords: false,
                useIndexBuffer: true,
            });

            // Add 65537 vertices (> 65535 limit for 16-bit)
            for (let i = 0; i < 65537; i++) {
                builder.addVertex(Vec3.create(i * 0.001, 0, 0));
            }
            builder.addTriangle(0, 1, 2);

            const result = builder.build();
            // With 32-bit indices, 3 indices = 12 bytes
            expect(readUint32s(result.indices).length).toBe(3);
        });
    });

    describe('Index buffer disabled', () => {
        it('useIndexBuffer false is accepted in config', () => {
            const builder = new GeometryBuilder({
                useIndexBuffer: false,
                generateNormals: false,
                generateTexCoords: false,
            });
            expect(builder.config.useIndexBuffer).toBe(false);
        });
    });
});

describe('types.ts helpers', () => {
    describe('getAttributeTypeSize', () => {
        it('returns correct sizes for all 7 GL types', () => {
            expect(getAttributeTypeSize(GL_BYTE)).toBe(1);
            expect(getAttributeTypeSize(GL_UNSIGNED_BYTE)).toBe(1);
            expect(getAttributeTypeSize(GL_SHORT)).toBe(2);
            expect(getAttributeTypeSize(GL_UNSIGNED_SHORT)).toBe(2);
            expect(getAttributeTypeSize(GL_INT)).toBe(4);
            expect(getAttributeTypeSize(GL_UNSIGNED_INT)).toBe(4);
            expect(getAttributeTypeSize(GL_FLOAT)).toBe(4);
        });

        it('throws for unknown type', () => {
            expect(() => getAttributeTypeSize(0x9999 as any)).toThrow();
        });
    });

    describe('createVertexAttribute', () => {
        it('creates attribute with correct structure', () => {
            const attr = createVertexAttribute('position' as any, 3, GL_FLOAT, false, 0);
            expect(attr.name).toBe('position');
            expect(attr.size).toBe(3);
            expect(attr.type).toBe(GL_FLOAT);
            expect(attr.normalized).toBe(false);
            expect(attr.offset).toBe(0);
        });

        it('defaults normalized to false and offset to 0', () => {
            const attr = createVertexAttribute('normal' as any, 3, GL_FLOAT);
            expect(attr.normalized).toBe(false);
            expect(attr.offset).toBe(0);
        });
    });

    describe('createGeometryLayout', () => {
        it('calculates stride correctly', () => {
            const attrs = [
                createVertexAttribute('position' as any, 3, GL_FLOAT, false, 0),
                createVertexAttribute('normal' as any, 3, GL_FLOAT, false, 12),
            ];
            const layout = createGeometryLayout(attrs, 100, 50);
            // stride = 3*4 + 3*4 = 24
            expect(layout.stride).toBe(24);
            expect(layout.vertexCount).toBe(100);
            expect(layout.indexCount).toBe(50);
            expect(layout.primitiveType).toBe('triangles');
        });

        it('supports custom primitive type', () => {
            const attrs = [createVertexAttribute('position' as any, 3, GL_FLOAT, false, 0)];
            const layout = createGeometryLayout(attrs, 10, 0, 'lines');
            expect(layout.primitiveType).toBe('lines');
        });

        it('generates a deterministic id from attributes', () => {
            const attrs = [createVertexAttribute('position' as any, 3, GL_FLOAT, false, 0)];
            const layout = createGeometryLayout(attrs, 10, 0);
            expect(layout.id).toContain('layout_position');
        });
    });

    describe('DEFAULT_PRIMITIVE_CONFIG', () => {
        it('has expected default values', () => {
            expect(DEFAULT_PRIMITIVE_CONFIG.generateNormals).toBe(true);
            expect(DEFAULT_PRIMITIVE_CONFIG.generateTexCoords).toBe(true);
            expect(DEFAULT_PRIMITIVE_CONFIG.generateTangents).toBe(false);
            expect(DEFAULT_PRIMITIVE_CONFIG.flipWindingOrder).toBe(false);
            expect(DEFAULT_PRIMITIVE_CONFIG.useIndexBuffer).toBe(true);
        });
    });

    describe('VERTEX_ATTRIBUTES', () => {
        it('has expected attribute names', () => {
            expect(VERTEX_ATTRIBUTES.POSITION).toBe('position');
            expect(VERTEX_ATTRIBUTES.NORMAL).toBe('normal');
            expect(VERTEX_ATTRIBUTES.TEXCOORD).toBe('texCoord');
            expect(VERTEX_ATTRIBUTES.TANGENT).toBe('tangent');
            expect(VERTEX_ATTRIBUTES.COLOR).toBe('color');
        });
    });
});
