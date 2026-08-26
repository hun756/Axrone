import { describe, it, expect } from 'vitest';
import { Vec3, Vec4, Mat4 } from '@axrone/numeric';
import {
    buildLineRibbon,
    buildTrailRibbon,
    LINE_VERTEX_FLOATS,
} from '../rendering/line-geometry-builder';

const CAMERA_POS = new Vec3(0, 0, 10);

const makeLineInput = (overrides: Record<string, unknown> = {}) => ({
    positions: [new Vec3(0, 0, 0), new Vec3(1, 0, 0)],
    startWidth: 1,
    endWidth: 1,
    widthCurve: [],
    startColor: new Vec4(1, 1, 1, 1),
    endColor: new Vec4(1, 1, 1, 1),
    colorGradientStops: [],
    textureMode: 'stretch' as const,
    alignment: 'view' as const,
    textureScaleX: 1,
    textureScaleY: 1,
    loop: false,
    useWorldSpace: true,
    worldMatrix: null,
    ...overrides,
});

function snapshotVertexData(result: { vertexData: Float32Array; vertexCount: number }): Float32Array {
    return new Float32Array(result.vertexData.subarray(0, result.vertexCount * LINE_VERTEX_FLOATS));
}

function snapshotIndexData(result: { indexData: Uint16Array; indexCount: number }): Uint16Array {
    return new Uint16Array(result.indexData.subarray(0, result.indexCount));
}

describe('buildLineRibbon', () => {
    it('returns zero vertices for fewer than 2 positions', () => {
        const result = buildLineRibbon(
            makeLineInput({ positions: [new Vec3(0, 0, 0)] }),
            CAMERA_POS
        );
        expect(result.vertexCount).toBe(0);
        expect(result.indexCount).toBe(0);
    });

    it('returns zero vertices for empty positions', () => {
        const result = buildLineRibbon(makeLineInput({ positions: [] }), CAMERA_POS);
        expect(result.vertexCount).toBe(0);
    });

    it('generates 4 vertices and 6 indices for 2 positions (1 segment)', () => {
        const result = buildLineRibbon(
            makeLineInput({
                positions: [new Vec3(0, 0, 0), new Vec3(1, 0, 0)],
            }),
            CAMERA_POS
        );
        expect(result.vertexCount).toBe(4);
        expect(result.indexCount).toBe(6);
    });

    it('generates 8 vertices and 12 indices for 3 positions (2 segments)', () => {
        const result = buildLineRibbon(
            makeLineInput({
                positions: [
                    new Vec3(0, 0, 0),
                    new Vec3(1, 0, 0),
                    new Vec3(2, 0, 0),
                ],
            }),
            CAMERA_POS
        );
        expect(result.vertexCount).toBe(8);
        expect(result.indexCount).toBe(12);
    });

    it('applies width correctly — vertices are offset from center line', () => {
        const result = buildLineRibbon(
            makeLineInput({
                positions: [new Vec3(0, 0, 0), new Vec3(10, 0, 0)],
                startWidth: 2,
                endWidth: 2,
            }),
            CAMERA_POS
        );

        const data = snapshotVertexData(result);
        expect(result.vertexCount).toBe(4);

        const p0LeftX = data[0]!;
        const p0LeftY = data[1]!;
        const p0RightX = data[LINE_VERTEX_FLOATS]!;
        const p0RightY = data[LINE_VERTEX_FLOATS + 1]!;

        const centerX = (p0LeftX + p0RightX) / 2;
        const centerY = (p0LeftY + p0RightY) / 2;

        expect(Math.abs(centerX)).toBeLessThan(0.01);
        expect(Math.abs(centerY)).toBeLessThan(0.01);

        const dx = p0RightX - p0LeftX;
        const dy = p0RightY - p0LeftY;
        const width = Math.sqrt(dx * dx + dy * dy);
        expect(width).toBeCloseTo(2, 1);
    });

    it('applies color gradient at vertices', () => {
        const result = buildLineRibbon(
            makeLineInput({
                positions: [new Vec3(0, 0, 0), new Vec3(1, 0, 0)],
                startColor: new Vec4(1, 0, 0, 1),
                endColor: new Vec4(0, 0, 1, 1),
            }),
            CAMERA_POS
        );

        const data = snapshotVertexData(result);

        const r0 = data[5]!;
        const g0 = data[6]!;
        const b0 = data[7]!;
        expect(r0).toBeCloseTo(1, 2);
        expect(g0).toBeCloseTo(0, 2);
        expect(b0).toBeCloseTo(0, 2);

        const lastOffset = (result.vertexCount - 1) * LINE_VERTEX_FLOATS;
        const rLast = data[lastOffset + 5]!;
        const gLast = data[lastOffset + 6]!;
        const bLast = data[lastOffset + 7]!;
        expect(rLast).toBeCloseTo(0, 2);
        expect(gLast).toBeCloseTo(0, 2);
        expect(bLast).toBeCloseTo(1, 2);
    });

    it('generates loop geometry when loop=true', () => {
        const result = buildLineRibbon(
            makeLineInput({
                positions: [
                    new Vec3(0, 0, 0),
                    new Vec3(1, 0, 0),
                    new Vec3(1, 1, 0),
                ],
                loop: true,
            }),
            CAMERA_POS
        );

        expect(result.vertexCount).toBe(12);
        expect(result.indexCount).toBe(18);
    });

    it('does not loop when fewer than 3 points even if loop=true', () => {
        const result = buildLineRibbon(
            makeLineInput({
                positions: [new Vec3(0, 0, 0), new Vec3(1, 0, 0)],
                loop: true,
            }),
            CAMERA_POS
        );

        expect(result.vertexCount).toBe(4);
        expect(result.indexCount).toBe(6);
    });

    it('indices reference valid vertex range', () => {
        const result = buildLineRibbon(
            makeLineInput({
                positions: [
                    new Vec3(0, 0, 0),
                    new Vec3(1, 0, 0),
                    new Vec3(2, 1, 0),
                    new Vec3(3, 0, 0),
                ],
            }),
            CAMERA_POS
        );

        const indices = snapshotIndexData(result);

        for (let i = 0; i < result.indexCount; i++) {
            expect(indices[i]).toBeLessThan(result.vertexCount);
            expect(indices[i]).toBeGreaterThanOrEqual(0);
        }
    });

    it('width curve modulates width along the line', () => {
        const result = buildLineRibbon(
            makeLineInput({
                positions: [
                    new Vec3(0, 0, 0),
                    new Vec3(1, 0, 0),
                    new Vec3(2, 0, 0),
                ],
                startWidth: 0,
                endWidth: 0,
                widthCurve: [0, 1, 0],
            }),
            CAMERA_POS
        );

        expect(result.vertexCount).toBe(8);
    });

    it('transform-z alignment uses fixed side direction', () => {
        const result = buildLineRibbon(
            makeLineInput({
                positions: [new Vec3(0, 0, 0), new Vec3(1, 0, 0)],
                alignment: 'transform-z',
                worldMatrix: Mat4.IDENTITY,
            }),
            CAMERA_POS
        );

        const data = snapshotVertexData(result);
        expect(result.vertexCount).toBe(4);

        const z0 = data[2]!;
        const z1 = data[LINE_VERTEX_FLOATS + 2]!;
        expect(Math.abs(z0 - z1)).toBeCloseTo(1, 1);
    });
});

describe('buildTrailRibbon', () => {
    it('returns zero vertices for fewer than 2 positions', () => {
        const result = buildTrailRibbon(
            {
                positions: [new Vec3(0, 0, 0)],
                startWidth: 1,
                endWidth: 0,
                widthCurve: [],
                colorGradientStops: [
                    { position: 0, color: new Vec4(1, 1, 1, 1) },
                    { position: 1, color: new Vec4(1, 1, 1, 0) },
                ],
                textureMode: 'stretch' as const,
                alignment: 'view' as const,
                textureScaleX: 1,
                textureScaleY: 1,
                worldMatrix: null,
            },
            CAMERA_POS
        );
        expect(result.vertexCount).toBe(0);
    });

    it('generates correct vertex/index count for trail', () => {
        const result = buildTrailRibbon(
            {
                positions: [
                    new Vec3(0, 0, 0),
                    new Vec3(1, 0, 0),
                    new Vec3(2, 0, 0),
                    new Vec3(3, 0, 0),
                ],
                startWidth: 1,
                endWidth: 0,
                widthCurve: [],
                colorGradientStops: [
                    { position: 0, color: new Vec4(1, 1, 1, 1) },
                    { position: 1, color: new Vec4(1, 1, 1, 0) },
                ],
                textureMode: 'stretch' as const,
                alignment: 'view' as const,
                textureScaleX: 1,
                textureScaleY: 1,
                worldMatrix: null,
            },
            CAMERA_POS
        );

        expect(result.vertexCount).toBe(12);
        expect(result.indexCount).toBe(18);
    });

    it('trail width tapers from start to end', () => {
        const result = buildTrailRibbon(
            {
                positions: [new Vec3(0, 0, 0), new Vec3(10, 0, 0)],
                startWidth: 2,
                endWidth: 0,
                widthCurve: [],
                colorGradientStops: [],
                textureMode: 'stretch' as const,
                alignment: 'view' as const,
                textureScaleX: 1,
                textureScaleY: 1,
                worldMatrix: null,
            },
            CAMERA_POS
        );

        const data = snapshotVertexData(result);

        const left0 = { x: data[0]!, y: data[1]! };
        const right0 = { x: data[LINE_VERTEX_FLOATS]!, y: data[LINE_VERTEX_FLOATS + 1]! };
        const width0 = Math.sqrt(
            (right0.x - left0.x) ** 2 + (right0.y - left0.y) ** 2
        );
        expect(width0).toBeCloseTo(2, 1);

        const left1 = { x: data[3 * LINE_VERTEX_FLOATS]!, y: data[3 * LINE_VERTEX_FLOATS + 1]! };
        const right1 = { x: data[2 * LINE_VERTEX_FLOATS]!, y: data[2 * LINE_VERTEX_FLOATS + 1]! };
        const width1 = Math.sqrt(
            (right1.x - left1.x) ** 2 + (right1.y - left1.y) ** 2
        );
        expect(width1).toBeCloseTo(0, 1);
    });
});
