import { beforeAll, describe, expect, it } from 'vitest';
import {
    MeshWebGLConstants,
    VertexAttributeInfo,
    MeshUtils,
    BoundingVolumeUtils,
    MeshGenerationUtils,
} from '../utils';
import {
    VertexAttributeType,
    VertexDataType,
    PrimitiveTopology,
    BufferUsage,
    IndexType,
    MeshError,
} from '../interfaces';
import type { IVertexAttributeDescriptor, IVertexLayout } from '../interfaces';

// WebGL2 constant stubs needed for MeshWebGLConstants
const webglConstants: Record<string, number> = {
    BYTE: 0x1400,
    UNSIGNED_BYTE: 0x1401,
    SHORT: 0x1402,
    UNSIGNED_SHORT: 0x1403,
    INT: 0x1404,
    UNSIGNED_INT: 0x1405,
    FLOAT: 0x1406,
    HALF_FLOAT: 0x140b,
    POINTS: 0x0000,
    LINES: 0x0001,
    LINE_STRIP: 0x0003,
    LINE_LOOP: 0x0002,
    TRIANGLES: 0x0004,
    TRIANGLE_STRIP: 0x0005,
    TRIANGLE_FAN: 0x0006,
    STATIC_DRAW: 0x88e4,
    DYNAMIC_DRAW: 0x88e8,
    STREAM_DRAW: 0x88e0,
    STATIC_READ: 0x88e5,
    DYNAMIC_READ: 0x88e9,
    STREAM_READ: 0x88e1,
    STATIC_COPY: 0x88e6,
    DYNAMIC_COPY: 0x88ea,
    STREAM_COPY: 0x88e2,
};

beforeAll(() => {
    const stub = new Proxy(webglConstants, {
        get: (target, property) => {
            if (typeof property === 'string' && property in target) {
                return target[property]!;
            }
            return 0;
        },
    });
    Object.assign(globalThis, {
        WebGL2RenderingContext: stub,
    });
});

function makeDescriptor(
    type: VertexAttributeType,
    dataType: VertexDataType,
    componentCount: number,
    offset: number,
    stride: number
): IVertexAttributeDescriptor {
    return { type, dataType, componentCount, normalized: false, offset, stride };
}

describe('MeshWebGLConstants', () => {
    it('maps all vertex data types to numeric constants', () => {
        const types = [
            VertexDataType.BYTE, VertexDataType.UNSIGNED_BYTE,
            VertexDataType.SHORT, VertexDataType.UNSIGNED_SHORT,
            VertexDataType.INT, VertexDataType.UNSIGNED_INT,
            VertexDataType.FLOAT, VertexDataType.HALF_FLOAT,
        ];
        for (const type of types) {
            expect(typeof MeshWebGLConstants.getVertexDataTypeConstant(type)).toBe('number');
        }
        // All mapped constants should be distinct
        const values = types.map((t) => MeshWebGLConstants.getVertexDataTypeConstant(t));
        expect(new Set(values).size).toBe(values.length);
    });

    it('maps all primitive topologies to numeric constants', () => {
        const topologies = [
            PrimitiveTopology.POINTS, PrimitiveTopology.LINES,
            PrimitiveTopology.LINE_STRIP, PrimitiveTopology.LINE_LOOP,
            PrimitiveTopology.TRIANGLES, PrimitiveTopology.TRIANGLE_STRIP,
            PrimitiveTopology.TRIANGLE_FAN,
        ];
        for (const topo of topologies) {
            expect(typeof MeshWebGLConstants.getPrimitiveTopologyConstant(topo)).toBe('number');
        }
        const values = topologies.map((t) => MeshWebGLConstants.getPrimitiveTopologyConstant(t));
        expect(new Set(values).size).toBe(values.length);
    });

    it('maps all buffer usages to numeric constants', () => {
        const usages = [
            BufferUsage.STATIC_DRAW, BufferUsage.DYNAMIC_DRAW, BufferUsage.STREAM_DRAW,
            BufferUsage.STATIC_READ, BufferUsage.DYNAMIC_READ, BufferUsage.STREAM_READ,
            BufferUsage.STATIC_COPY, BufferUsage.DYNAMIC_COPY, BufferUsage.STREAM_COPY,
        ];
        for (const usage of usages) {
            expect(typeof MeshWebGLConstants.getBufferUsageConstant(usage)).toBe('number');
        }
    });

    it('maps all index types to numeric constants', () => {
        const types = [IndexType.UNSIGNED_BYTE, IndexType.UNSIGNED_SHORT, IndexType.UNSIGNED_INT];
        for (const type of types) {
            expect(typeof MeshWebGLConstants.getIndexTypeConstant(type)).toBe('number');
        }
    });

    it('throws on invalid vertex data type', () => {
        expect(() => MeshWebGLConstants.getVertexDataTypeConstant('INVALID' as any)).toThrow();
    });

    it('throws on invalid primitive topology', () => {
        expect(() => MeshWebGLConstants.getPrimitiveTopologyConstant('INVALID' as any)).toThrow();
    });

    it('throws on invalid index type', () => {
        expect(() => MeshWebGLConstants.getIndexTypeConstant('INVALID' as any)).toThrow();
    });
});

describe('VertexAttributeInfo', () => {
    describe('getAttributeLocation', () => {
        it('returns correct locations for standard attributes', () => {
            expect(VertexAttributeInfo.getAttributeLocation(VertexAttributeType.POSITION)).toBe(0);
            expect(VertexAttributeInfo.getAttributeLocation(VertexAttributeType.NORMAL)).toBe(1);
            expect(VertexAttributeInfo.getAttributeLocation(VertexAttributeType.TANGENT)).toBe(2);
            expect(VertexAttributeInfo.getAttributeLocation(VertexAttributeType.TEXCOORD_0)).toBe(3);
            expect(VertexAttributeInfo.getAttributeLocation(VertexAttributeType.COLOR_0)).toBe(7);
        });

        it('returns locations for custom attributes', () => {
            expect(VertexAttributeInfo.getAttributeLocation(VertexAttributeType.CUSTOM_0)).toBe(11);
            expect(VertexAttributeInfo.getAttributeLocation(VertexAttributeType.CUSTOM_3)).toBe(14);
        });
    });

    describe('getDataTypeSize', () => {
        it('returns correct byte sizes', () => {
            expect(VertexAttributeInfo.getDataTypeSize(VertexDataType.BYTE)).toBe(1);
            expect(VertexAttributeInfo.getDataTypeSize(VertexDataType.UNSIGNED_BYTE)).toBe(1);
            expect(VertexAttributeInfo.getDataTypeSize(VertexDataType.SHORT)).toBe(2);
            expect(VertexAttributeInfo.getDataTypeSize(VertexDataType.UNSIGNED_SHORT)).toBe(2);
            expect(VertexAttributeInfo.getDataTypeSize(VertexDataType.INT)).toBe(4);
            expect(VertexAttributeInfo.getDataTypeSize(VertexDataType.UNSIGNED_INT)).toBe(4);
            expect(VertexAttributeInfo.getDataTypeSize(VertexDataType.FLOAT)).toBe(4);
            expect(VertexAttributeInfo.getDataTypeSize(VertexDataType.HALF_FLOAT)).toBe(2);
        });
    });

    describe('getDefaultComponentCount', () => {
        it('returns 3 for position/normal/tangent', () => {
            expect(VertexAttributeInfo.getDefaultComponentCount(VertexAttributeType.POSITION)).toBe(3);
            expect(VertexAttributeInfo.getDefaultComponentCount(VertexAttributeType.NORMAL)).toBe(3);
            expect(VertexAttributeInfo.getDefaultComponentCount(VertexAttributeType.TANGENT)).toBe(3);
        });

        it('returns 2 for texcoords', () => {
            expect(VertexAttributeInfo.getDefaultComponentCount(VertexAttributeType.TEXCOORD_0)).toBe(2);
            expect(VertexAttributeInfo.getDefaultComponentCount(VertexAttributeType.TEXCOORD_3)).toBe(2);
        });

        it('returns 4 for color/joints/weights', () => {
            expect(VertexAttributeInfo.getDefaultComponentCount(VertexAttributeType.COLOR_0)).toBe(4);
            expect(VertexAttributeInfo.getDefaultComponentCount(VertexAttributeType.JOINTS_0)).toBe(4);
            expect(VertexAttributeInfo.getDefaultComponentCount(VertexAttributeType.WEIGHTS_0)).toBe(4);
        });

        it('returns 1 for custom attributes', () => {
            expect(VertexAttributeInfo.getDefaultComponentCount(VertexAttributeType.CUSTOM_0)).toBe(1);
        });
    });

    describe('getDefaultDataType', () => {
        it('returns UNSIGNED_SHORT for JOINTS_0', () => {
            expect(VertexAttributeInfo.getDefaultDataType(VertexAttributeType.JOINTS_0)).toBe(VertexDataType.UNSIGNED_SHORT);
        });

        it('returns UNSIGNED_BYTE for color attributes', () => {
            expect(VertexAttributeInfo.getDefaultDataType(VertexAttributeType.COLOR_0)).toBe(VertexDataType.UNSIGNED_BYTE);
            expect(VertexAttributeInfo.getDefaultDataType(VertexAttributeType.COLOR_1)).toBe(VertexDataType.UNSIGNED_BYTE);
        });

        it('returns FLOAT for most attributes', () => {
            expect(VertexAttributeInfo.getDefaultDataType(VertexAttributeType.POSITION)).toBe(VertexDataType.FLOAT);
            expect(VertexAttributeInfo.getDefaultDataType(VertexAttributeType.NORMAL)).toBe(VertexDataType.FLOAT);
        });
    });

    describe('shouldNormalize', () => {
        it('returns true for COLOR with UNSIGNED_BYTE', () => {
            expect(VertexAttributeInfo.shouldNormalize(VertexAttributeType.COLOR_0, VertexDataType.UNSIGNED_BYTE)).toBe(true);
        });

        it('returns false for COLOR with FLOAT', () => {
            expect(VertexAttributeInfo.shouldNormalize(VertexAttributeType.COLOR_0, VertexDataType.FLOAT)).toBe(false);
        });

        it('returns true for WEIGHTS_0 with UNSIGNED_BYTE or UNSIGNED_SHORT', () => {
            expect(VertexAttributeInfo.shouldNormalize(VertexAttributeType.WEIGHTS_0, VertexDataType.UNSIGNED_BYTE)).toBe(true);
            expect(VertexAttributeInfo.shouldNormalize(VertexAttributeType.WEIGHTS_0, VertexDataType.UNSIGNED_SHORT)).toBe(true);
        });

        it('returns false for POSITION', () => {
            expect(VertexAttributeInfo.shouldNormalize(VertexAttributeType.POSITION, VertexDataType.FLOAT)).toBe(false);
        });
    });

    describe('getAttributeByteSize', () => {
        it('calculates correctly', () => {
            const desc = makeDescriptor(VertexAttributeType.POSITION, VertexDataType.FLOAT, 3, 0, 12);
            expect(VertexAttributeInfo.getAttributeByteSize(desc)).toBe(12);
        });
    });
});

describe('MeshUtils', () => {
    describe('calculateLayoutStride', () => {
        it('returns max end offset', () => {
            const attrs = [
                makeDescriptor(VertexAttributeType.POSITION, VertexDataType.FLOAT, 3, 0, 12),
                makeDescriptor(VertexAttributeType.NORMAL, VertexDataType.FLOAT, 3, 12, 24),
            ];
            expect(MeshUtils.calculateLayoutStride(attrs)).toBe(24);
        });

        it('handles single attribute', () => {
            const attrs = [
                makeDescriptor(VertexAttributeType.POSITION, VertexDataType.FLOAT, 3, 0, 12),
            ];
            expect(MeshUtils.calculateLayoutStride(attrs)).toBe(12);
        });
    });

    describe('validateVertexLayout', () => {
        it('throws for empty attributes', () => {
            const layout: IVertexLayout = { attributes: [], stride: 12, vertexCount: 0 };
            expect(() => MeshUtils.validateVertexLayout(layout)).toThrow(MeshError);
        });

        it('throws for overlapping attributes', () => {
            const layout: IVertexLayout = {
                attributes: [
                    makeDescriptor(VertexAttributeType.POSITION, VertexDataType.FLOAT, 3, 0, 12),
                    makeDescriptor(VertexAttributeType.NORMAL, VertexDataType.FLOAT, 3, 4, 12),
                ],
                stride: 24,
                vertexCount: 0,
            };
            expect(() => MeshUtils.validateVertexLayout(layout)).toThrow(/Overlapping/);
        });

        it('throws when stride is too small', () => {
            const layout: IVertexLayout = {
                attributes: [
                    makeDescriptor(VertexAttributeType.POSITION, VertexDataType.FLOAT, 3, 0, 12),
                ],
                stride: 8, // less than the 12 bytes needed
                vertexCount: 0,
            };
            expect(() => MeshUtils.validateVertexLayout(layout)).toThrow(/Invalid stride/);
        });

        it('passes for valid layout', () => {
            const layout = MeshUtils.createStandardLayout();
            expect(() => MeshUtils.validateVertexLayout(layout)).not.toThrow();
        });
    });

    describe('createPositionLayout', () => {
        it('creates a layout with single POSITION attribute', () => {
            const layout = MeshUtils.createPositionLayout();
            expect(layout.attributes).toHaveLength(1);
            expect(layout.attributes[0]!.type).toBe(VertexAttributeType.POSITION);
            expect(layout.stride).toBe(12);
        });
    });

    describe('createStandardLayout', () => {
        it('creates POSITION + NORMAL + TEXCOORD_0 layout', () => {
            const layout = MeshUtils.createStandardLayout();
            expect(layout.attributes).toHaveLength(3);
            expect(layout.attributes[0]!.type).toBe(VertexAttributeType.POSITION);
            expect(layout.attributes[1]!.type).toBe(VertexAttributeType.NORMAL);
            expect(layout.attributes[2]!.type).toBe(VertexAttributeType.TEXCOORD_0);
            expect(layout.stride).toBe(32);
        });
    });

    describe('calculateVertexMemoryUsage', () => {
        it('returns stride * vertexCount', () => {
            const layout = MeshUtils.createStandardLayout();
            expect(MeshUtils.calculateVertexMemoryUsage(layout, 100)).toBe(3200);
        });
    });

    describe('calculateIndexMemoryUsage', () => {
        it('returns correct sizes for each index type', () => {
            expect(MeshUtils.calculateIndexMemoryUsage(IndexType.UNSIGNED_BYTE, 100)).toBe(100);
            expect(MeshUtils.calculateIndexMemoryUsage(IndexType.UNSIGNED_SHORT, 100)).toBe(200);
            expect(MeshUtils.calculateIndexMemoryUsage(IndexType.UNSIGNED_INT, 100)).toBe(400);
        });
    });

    describe('validateIndexType', () => {
        it('passes for valid combinations', () => {
            expect(() => MeshUtils.validateIndexType(IndexType.UNSIGNED_BYTE, 255)).not.toThrow();
            expect(() => MeshUtils.validateIndexType(IndexType.UNSIGNED_SHORT, 65535)).not.toThrow();
            expect(() => MeshUtils.validateIndexType(IndexType.UNSIGNED_INT, 100000)).not.toThrow();
        });

        it('throws when vertex count exceeds max for index type', () => {
            expect(() => MeshUtils.validateIndexType(IndexType.UNSIGNED_BYTE, 256)).toThrow(MeshError);
            expect(() => MeshUtils.validateIndexType(IndexType.UNSIGNED_SHORT, 65536)).toThrow(MeshError);
        });
    });

    describe('getOptimalIndexType', () => {
        it('returns UNSIGNED_BYTE for <=255 vertices', () => {
            expect(MeshUtils.getOptimalIndexType(255)).toBe(IndexType.UNSIGNED_BYTE);
        });

        it('returns UNSIGNED_SHORT for <=65535 vertices', () => {
            expect(MeshUtils.getOptimalIndexType(256)).toBe(IndexType.UNSIGNED_SHORT);
            expect(MeshUtils.getOptimalIndexType(65535)).toBe(IndexType.UNSIGNED_SHORT);
        });

        it('returns UNSIGNED_INT for >65535 vertices', () => {
            expect(MeshUtils.getOptimalIndexType(65536)).toBe(IndexType.UNSIGNED_INT);
        });
    });

    describe('generateMeshId', () => {
        it('returns unique ids with mesh_ prefix', () => {
            const id1 = MeshUtils.generateMeshId();
            const id2 = MeshUtils.generateMeshId();
            expect(id1).toMatch(/^mesh_/);
            expect(id1).not.toBe(id2);
        });
    });
});

describe('BoundingVolumeUtils', () => {
    describe('computeBoundingBox', () => {
        it('returns zero box for empty positions', () => {
            const box = BoundingVolumeUtils.computeBoundingBox(new Float32Array(0));
            expect(box.radius).toBe(0);
            expect(box.min.x).toBe(0);
        });

        it('computes correct box for single point', () => {
            const box = BoundingVolumeUtils.computeBoundingBox(new Float32Array([1, 2, 3]));
            expect(box.min.x).toBe(1);
            expect(box.min.y).toBe(2);
            expect(box.min.z).toBe(3);
            expect(box.max.x).toBe(1);
            expect(box.radius).toBe(0);
        });

        it('computes correct box for multiple points', () => {
            const positions = new Float32Array([
                -1, -2, -3,
                 1,  2,  3,
                 0,  0,  0,
            ]);
            const box = BoundingVolumeUtils.computeBoundingBox(positions);
            expect(box.min.x).toBe(-1);
            expect(box.min.y).toBe(-2);
            expect(box.max.x).toBe(1);
            expect(box.max.y).toBe(2);
            expect(box.center.x).toBeCloseTo(0);
            expect(box.center.y).toBeCloseTo(0);
        });
    });

    describe('computeBoundingSphere', () => {
        it('returns sphere from bounding box', () => {
            const positions = new Float32Array([-1, 0, 0, 1, 0, 0]);
            const sphere = BoundingVolumeUtils.computeBoundingSphere(positions);
            expect(sphere.center.x).toBeCloseTo(0);
            expect(sphere.radius).toBeCloseTo(1);
        });
    });

    describe('transformBoundingBox', () => {
        it('transforms box with identity matrix', () => {
            const box = BoundingVolumeUtils.computeBoundingBox(new Float32Array([-1, -1, -1, 1, 1, 1]));
            const identity = { data: new Float32Array([1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1]) } as any;
            const transformed = BoundingVolumeUtils.transformBoundingBox(box, identity);
            expect(transformed.min.x).toBeCloseTo(-1);
            expect(transformed.max.x).toBeCloseTo(1);
        });
    });

    describe('transformBoundingSphere', () => {
        it('scales radius by max scale factor', () => {
            const sphere = { center: { x: 0, y: 0, z: 0 } as any, radius: 1 };
            const scale2x = { data: new Float32Array([2,0,0,0, 0,2,0,0, 0,0,2,0, 0,0,0,1]) } as any;
            const transformed = BoundingVolumeUtils.transformBoundingSphere(sphere, scale2x);
            expect(transformed.radius).toBeCloseTo(2);
        });
    });

    describe('mergeBoundingBoxes', () => {
        it('returns zero box for empty array', () => {
            const merged = BoundingVolumeUtils.mergeBoundingBoxes([]);
            expect(merged.radius).toBe(0);
        });

        it('merges multiple boxes', () => {
            const box1 = BoundingVolumeUtils.computeBoundingBox(new Float32Array([-1, -1, -1, 0, 0, 0]));
            const box2 = BoundingVolumeUtils.computeBoundingBox(new Float32Array([0, 0, 0, 1, 1, 1]));
            const merged = BoundingVolumeUtils.mergeBoundingBoxes([box1, box2]);
            expect(merged.min.x).toBeCloseTo(-1);
            expect(merged.max.x).toBeCloseTo(1);
            expect(merged.min.y).toBeCloseTo(-1);
            expect(merged.max.y).toBeCloseTo(1);
        });

        it('returns same box for single input', () => {
            const box = BoundingVolumeUtils.computeBoundingBox(new Float32Array([-2, -3, -4, 2, 3, 4]));
            const merged = BoundingVolumeUtils.mergeBoundingBoxes([box]);
            expect(merged.min.x).toBeCloseTo(-2);
            expect(merged.max.z).toBeCloseTo(4);
        });
    });
});

describe('MeshGenerationUtils', () => {
    describe('generateSmoothNormals', () => {
        it('produces normals array of same length as positions', () => {
            const positions = new Float32Array([
                0, 0, 0,
                1, 0, 0,
                0, 1, 0,
            ]);
            const indices = new Uint16Array([0, 1, 2]);
            const normals = MeshGenerationUtils.generateSmoothNormals(positions, indices);
            expect(normals.length).toBe(positions.length);
        });

        it('produces normalized normals', () => {
            const positions = new Float32Array([
                0, 0, 0,
                1, 0, 0,
                0, 1, 0,
            ]);
            const indices = new Uint16Array([0, 1, 2]);
            const normals = MeshGenerationUtils.generateSmoothNormals(positions, indices);
            // Each normal should be roughly [0, 0, 1] for a flat triangle in XY plane
            expect(Math.abs(normals[2]!)).toBeCloseTo(1, 1);
        });
    });
});
