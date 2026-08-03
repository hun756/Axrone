import { describe, expect, it } from 'vitest';
import { Color } from '@axrone/numeric';
import { Render2DSpriteBatchBuilder } from '../sprite-batch-builder';
import { Render2DCapacityError, Render2DValidationError } from '../errors';

const identity = Object.freeze([
    1, 0, 0, 0,
    0, 1, 0, 0,
    0, 0, 1, 0,
    0, 0, 0, 1,
] as const);

describe('Render2DSpriteBatchBuilder', () => {
    it('batches consecutive sprites that share the same texture source', () => {
        const builder = new Render2DSpriteBatchBuilder();
        const result = builder.build([
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 2, height: 4 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: new Color(1, 1, 1, 1),
            },
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 0.5, height: 0.5 },
                color: new Color(1, 0.5, 0.25, 1),
            },
            {
                source: { kind: 'material', materialId: 'mat/b' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0, y: 0 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: new Color(0.5, 1, 1, 0.5),
            },
        ]);

        expect(result.spriteCount).toBe(3);
        expect(result.quadCount).toBe(3);
        expect(result.indexCount).toBe(18);
        expect(result.batches).toHaveLength(2);
        expect(result.batches[0]?.quadCount).toBe(2);
        expect(result.batches[0]?.key.sourceKey).toBe('texture:atlas/a');
        expect(result.batches[1]?.quadCount).toBe(1);
        expect(result.batches[1]?.key.sourceKey).toBe('material:mat/b');
    });

    it('writes transformed quad vertices using row-major transform data', () => {
        const builder = new Render2DSpriteBatchBuilder();
        const translation = [
            1, 0, 0, 3,
            0, 1, 0, 4,
            0, 0, 1, 2,
            0, 0, 0, 1,
        ] as const;

        const result = builder.build([
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: translation,
                size: { width: 2, height: 2 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: Color.WHITE,
            },
        ]);

        const view = new Float32Array(result.vertexData.buffer, result.vertexData.byteOffset, result.vertexData.byteLength / 4);

        expect(view[0]).toBe(2);
        expect(view[1]).toBe(3);
        expect(view[2]).toBe(2);
        expect(view[6]).toBe(4);
        expect(view[7]).toBe(3);
        expect(view[8]).toBe(2);
        expect(view[12]).toBe(4);
        expect(view[13]).toBe(5);
        expect(view[14]).toBe(2);
        expect(view[18]).toBe(2);
        expect(view[19]).toBe(5);
        expect(view[20]).toBe(2);
    });

    it('splits batches when the configured quad limit is reached', () => {
        const builder = new Render2DSpriteBatchBuilder({ maxBatchQuads: 1 });
        const result = builder.build([
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: Color.WHITE,
            },
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: Color.WHITE,
            },
        ]);

        expect(result.batches).toHaveLength(2);
        expect(result.batches[0]?.indexOffset).toBe(0);
        expect(result.batches[1]?.indexOffset).toBe(6);
    });

    it('splits batches when clip rect state changes', () => {
        const builder = new Render2DSpriteBatchBuilder();
        const result = builder.build([
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                clipRect: { x: 0, y: 0, width: 64, height: 64 },
                color: Color.WHITE,
            },
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                clipRect: { x: 0, y: 0, width: 64, height: 64 },
                color: Color.WHITE,
            },
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                clipRect: { x: 32, y: 0, width: 64, height: 64 },
                color: Color.WHITE,
            },
        ]);

        expect(result.batches).toHaveLength(2);
        expect(result.batches[0]?.quadCount).toBe(2);
        expect(result.batches[0]?.key.clipRect).toEqual({
            x: 0,
            y: 0,
            width: 64,
            height: 64,
        });
        expect(result.batches[1]?.quadCount).toBe(1);
    });

    it('splits batches when mask state changes', () => {
        const builder = new Render2DSpriteBatchBuilder();
        const identityMask = {
            shape: 'circle' as const,
            inverseWorldMatrix: [
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1,
            ],
            size: { width: 32, height: 32 },
            anchor: { x: 0.5, y: 0.5 },
        };

        const shiftedMask = {
            ...identityMask,
            inverseWorldMatrix: [
                1, 0, 0, 4,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1,
            ],
        };

        const result = builder.build([
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                mask: identityMask,
                color: Color.WHITE,
            },
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                mask: identityMask,
                color: Color.WHITE,
            },
            {
                source: { kind: 'texture', textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0.5, y: 0.5 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                mask: shiftedMask,
                color: Color.WHITE,
            },
        ]);

        expect(result.batches).toHaveLength(2);
        expect(result.batches[0]?.quadCount).toBe(2);
        expect(result.batches[0]?.key.mask?.shape).toBe('circle');
        expect(result.batches[1]?.quadCount).toBe(1);
    });

    it('emits nine-slice quads from a single sprite submission', () => {
        const builder = new Render2DSpriteBatchBuilder();
        const result = builder.build([
            {
                source: { kind: 'texture', textureId: 'atlas/panel' },
                worldMatrix: identity,
                size: { width: 30, height: 18 },
                anchor: { x: 0, y: 0 },
                uvRect: { x: 0.25, y: 0.125, width: 0.5, height: 0.5 },
                color: Color.WHITE,
                slice: {
                    sourceSize: { width: 10, height: 6 },
                    border: { left: 2, right: 2, top: 1, bottom: 1 },
                },
            },
        ]);

        expect(result.spriteCount).toBe(1);
        expect(result.quadCount).toBe(9);
        expect(result.vertexCount).toBe(36);
        expect(result.indexCount).toBe(54);
        expect(result.batches[0]?.quadCount).toBe(9);

        const view = new Float32Array(
            result.vertexData.buffer,
            result.vertexData.byteOffset,
            result.vertexData.byteLength / 4
        );

        expect(view[0]).toBe(0);
        expect(view[6]).toBe(6);
    });

    it('throws Render2DValidationError for non-finite size values', () => {
        const builder = new Render2DSpriteBatchBuilder();
        expect(() =>
            builder.build([
                {
                    source: { kind: 'texture' as const, textureId: 'a' },
                    worldMatrix: identity,
                    size: { width: NaN, height: 1 },
                    anchor: { x: 0, y: 0 },
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                    color: Color.WHITE,
                },
            ]),
        ).toThrow(Render2DValidationError);

        expect(() =>
            builder.build([
                {
                    source: { kind: 'texture' as const, textureId: 'a' },
                    worldMatrix: identity,
                    size: { width: 1, height: Infinity },
                    anchor: { x: 0, y: 0 },
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                    color: Color.WHITE,
                },
            ]),
        ).toThrow(Render2DValidationError);
    });

    it('throws Render2DValidationError for invalid slice configuration', () => {
        const builder = new Render2DSpriteBatchBuilder();

        // Zero sourceSize
        expect(() =>
            builder.build([
                {
                    source: { kind: 'texture' as const, textureId: 'a' },
                    worldMatrix: identity,
                    size: { width: 10, height: 10 },
                    anchor: { x: 0, y: 0 },
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                    color: Color.WHITE,
                    slice: {
                        sourceSize: { width: 0, height: 10 },
                        border: { left: 0, right: 0, top: 0, bottom: 0 },
                    },
                },
            ]),
        ).toThrow(Render2DValidationError);

        // Negative border
        expect(() =>
            builder.build([
                {
                    source: { kind: 'texture' as const, textureId: 'a' },
                    worldMatrix: identity,
                    size: { width: 10, height: 10 },
                    anchor: { x: 0, y: 0 },
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                    color: Color.WHITE,
                    slice: {
                        sourceSize: { width: 10, height: 10 },
                        border: { left: -1, right: 0, top: 0, bottom: 0 },
                    },
                },
            ]),
        ).toThrow(Render2DValidationError);

        // Borders exceeding source size
        expect(() =>
            builder.build([
                {
                    source: { kind: 'texture' as const, textureId: 'a' },
                    worldMatrix: identity,
                    size: { width: 10, height: 10 },
                    anchor: { x: 0, y: 0 },
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                    color: Color.WHITE,
                    slice: {
                        sourceSize: { width: 10, height: 10 },
                        border: { left: 6, right: 6, top: 0, bottom: 0 },
                    },
                },
            ]),
        ).toThrow(Render2DValidationError);
    });

    it('throws Render2DValidationError for invalid mask configuration', () => {
        const builder = new Render2DSpriteBatchBuilder();

        // Zero mask size
        expect(() =>
            builder.build([
                {
                    source: { kind: 'texture' as const, textureId: 'a' },
                    worldMatrix: identity,
                    size: { width: 1, height: 1 },
                    anchor: { x: 0, y: 0 },
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                    color: Color.WHITE,
                    mask: {
                        shape: 'circle' as const,
                        inverseWorldMatrix: [
                            1, 0, 0, 0,
                            0, 1, 0, 0,
                            0, 0, 1, 0,
                            0, 0, 0, 1,
                        ],
                        size: { width: 0, height: 10 },
                        anchor: { x: 0.5, y: 0.5 },
                    },
                },
            ]),
        ).toThrow(Render2DValidationError);

        // Short inverseWorldMatrix
        expect(() =>
            builder.build([
                {
                    source: { kind: 'texture' as const, textureId: 'a' },
                    worldMatrix: identity,
                    size: { width: 1, height: 1 },
                    anchor: { x: 0, y: 0 },
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                    color: Color.WHITE,
                    mask: {
                        shape: 'circle' as const,
                        inverseWorldMatrix: [1, 0, 0, 0],
                        size: { width: 10, height: 10 },
                        anchor: { x: 0.5, y: 0.5 },
                    },
                },
            ]),
        ).toThrow(Render2DValidationError);
    });

    it('skips submissions with visible set to false', () => {
        const builder = new Render2DSpriteBatchBuilder();
        const result = builder.build([
            {
                source: { kind: 'texture' as const, textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0, y: 0 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: Color.WHITE,
                visible: false,
            },
            {
                source: { kind: 'texture' as const, textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0, y: 0 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: Color.WHITE,
            },
        ]);

        expect(result.spriteCount).toBe(1);
        expect(result.quadCount).toBe(1);
    });

    it('skips submissions with zero size or zero clipRect', () => {
        const builder = new Render2DSpriteBatchBuilder();
        const result = builder.build([
            {
                source: { kind: 'texture' as const, textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 0, height: 1 },
                anchor: { x: 0, y: 0 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: Color.WHITE,
            },
            {
                source: { kind: 'texture' as const, textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0, y: 0 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                clipRect: { x: 0, y: 0, width: 0, height: 64 },
                color: Color.WHITE,
            },
            {
                source: { kind: 'texture' as const, textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0, y: 0 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: Color.WHITE,
            },
        ]);

        expect(result.spriteCount).toBe(1);
        expect(result.quadCount).toBe(1);
    });

    it('throws Render2DCapacityError when a single submission exceeds maxBatchQuads', () => {
        const builder = new Render2DSpriteBatchBuilder({ maxBatchQuads: 1 });
        expect(() =>
            builder.build([
                {
                    source: { kind: 'texture' as const, textureId: 'atlas/panel' },
                    worldMatrix: identity,
                    size: { width: 30, height: 18 },
                    anchor: { x: 0, y: 0 },
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                    color: Color.WHITE,
                    slice: {
                        sourceSize: { width: 10, height: 6 },
                        border: { left: 2, right: 2, top: 1, bottom: 1 },
                    },
                },
            ]),
        ).toThrow(Render2DCapacityError);
    });

    it('swaps UV coordinates when flipX is set', () => {
        // Use separate builders since build() reuses internal buffers
        const normalBuilder = new Render2DSpriteBatchBuilder();
        const flippedBuilder = new Render2DSpriteBatchBuilder();

        const normal = normalBuilder.build([
            {
                source: { kind: 'texture' as const, textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0, y: 0 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: Color.WHITE,
            },
        ]);

        const flipped = flippedBuilder.build([
            {
                source: { kind: 'texture' as const, textureId: 'atlas/a' },
                worldMatrix: identity,
                size: { width: 1, height: 1 },
                anchor: { x: 0, y: 0 },
                uvRect: { x: 0, y: 0, width: 1, height: 1 },
                color: Color.WHITE,
                flipX: true,
            },
        ]);

        // Vertex layout: [x, y, z, u, v, color] per vertex (stride 6 floats)
        const normalView = new Float32Array(
            normal.vertexData.buffer, normal.vertexData.byteOffset, normal.vertexData.byteLength / 4
        );
        const flippedView = new Float32Array(
            flipped.vertexData.buffer, flipped.vertexData.byteOffset, flipped.vertexData.byteLength / 4
        );

        // v0 = left-bottom (uLeft), v1 = right-bottom (uRight)
        const normalU0 = normalView[3];
        const normalU1 = normalView[6 + 3];
        const flippedU0 = flippedView[3];
        const flippedU1 = flippedView[6 + 3];

        expect(normalU0).toBeLessThan(normalU1);
        expect(flippedU0).toBeGreaterThan(flippedU1);
    });

    it('uses Uint32Array index buffer when vertex count exceeds 65535', () => {
        const builder = new Render2DSpriteBatchBuilder({ maxBatchQuads: 20000 });
        const submissions = Array.from({ length: 17000 }, (_, i) => ({
            source: { kind: 'texture' as const, textureId: `tex/${i}` },
            worldMatrix: identity,
            size: { width: 1, height: 1 },
            anchor: { x: 0, y: 0 } as { x: number; y: number },
            uvRect: { x: 0, y: 0, width: 1, height: 1 },
            color: Color.WHITE,
        }));

        const result = builder.build(submissions);
        expect(result.vertexCount).toBeGreaterThan(0xffff);
        expect(result.indexData).toBeInstanceOf(Uint32Array);
    });

    it('throws Render2DValidationError for non-finite worldMatrix values', () => {
        const builder = new Render2DSpriteBatchBuilder();
        const badMatrix = [
            NaN, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1,
        ] as const;

        expect(() =>
            builder.build([
                {
                    source: { kind: 'texture' as const, textureId: 'a' },
                    worldMatrix: badMatrix,
                    size: { width: 1, height: 1 },
                    anchor: { x: 0, y: 0 },
                    uvRect: { x: 0, y: 0, width: 1, height: 1 },
                    color: Color.WHITE,
                },
            ]),
        ).toThrow(Render2DValidationError);
    });

    it('throws Render2DValidationError when maxBatchQuads is invalid', () => {
        expect(() => new Render2DSpriteBatchBuilder({ maxBatchQuads: 0 })).toThrow(Render2DValidationError);
        expect(() => new Render2DSpriteBatchBuilder({ maxBatchQuads: -1 })).toThrow(Render2DValidationError);
        expect(() => new Render2DSpriteBatchBuilder({ maxBatchQuads: 1.5 })).toThrow(Render2DValidationError);
    });
});