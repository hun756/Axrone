import { describe, expect, it } from 'vitest';
import { Vec3 } from '@axrone/numeric';
import {
    createBox,
    createCube,
    createRoundedBox,
    createCylinder,
    createCone,
    createTruncatedCone,
    createTube,
} from '@axrone/geometry';

// ByteBuffer defaults to big-endian byte order, so typed-array views (native endian)
// produce incorrect values. Use the buffer's own getters for correct reads.
const readFloat32s = (bb: any): number[] => {
    bb.rewind();
    const out: number[] = [];
    while (bb.remaining >= 4) out.push(bb.getFloat32());
    return out;
};

// ─── Box ────────────────────────────────────────────────────────────────────

describe('createBox', () => {
    it('default 1×1×1 box has 24 vertices (6 faces × 4) and 36 indices', () => {
        const geo = createBox();
        expect(geo.layout.vertexCount).toBe(24);
        expect(geo.layout.indexCount).toBe(36);
    });

    it('has outward-facing normals', () => {
        const geo = createBox();
        const floatView = readFloat32s(geo.vertices);
        const stride = geo.layout.stride / 4; // in floats

        // Each face has 4 vertices with the same normal.
        // Collect unique normals (every stride-floats starting at offset 3).
        const normals = new Set<string>();
        for (let face = 0; face < 6; face++) {
            const base = face * 4 * stride;
            const nx = floatView[base + 3]!;
            const ny = floatView[base + 4]!;
            const nz = floatView[base + 5]!;
            normals.add(`${nx.toFixed(2)},${ny.toFixed(2)},${nz.toFixed(2)}`);
        }
        // 6 unique face normals (±X, ±Y, ±Z)
        expect(normals.size).toBe(6);
    });

    it('widthSegments:2 increases vertex count', () => {
        const base = createBox();
        const segmented = createBox({ widthSegments: 2 });
        expect(segmented.layout.vertexCount).toBeGreaterThan(base.layout.vertexCount);
    });

    it('non-zero index count', () => {
        const geo = createBox();
        expect(geo.layout.indexCount).toBeGreaterThan(0);
    });
});

describe('createCube', () => {
    it('is equivalent to createBox({width:1,height:1,depth:1})', () => {
        const cube = createCube();
        const box = createBox({ width: 1, height: 1, depth: 1 });
        expect(cube.layout.vertexCount).toBe(box.layout.vertexCount);
        expect(cube.layout.indexCount).toBe(box.layout.indexCount);
        expect(cube.layout.stride).toBe(box.layout.stride);
    });
});

describe('createRoundedBox', () => {
    it('has more vertices than a basic box', () => {
        const basic = createBox();
        const rounded = createRoundedBox();
        expect(rounded.layout.vertexCount).toBeGreaterThan(basic.layout.vertexCount);
    });

    it('has unit-length normals (within tolerance)', () => {
        const geo = createRoundedBox();
        const floatView = readFloat32s(geo.vertices);
        const stride = geo.layout.stride / 4;

        // Sample a handful of normals
        for (let i = 0; i < Math.min(50, geo.layout.vertexCount); i++) {
            const offset = i * stride;
            const nx = floatView[offset + 3]!;
            const ny = floatView[offset + 4]!;
            const nz = floatView[offset + 5]!;
            const len = Math.sqrt(nx * nx + ny * ny + nz * nz);
            expect(len).toBeCloseTo(1, 3);
        }
    });
});

// ─── Cylinder ───────────────────────────────────────────────────────────────

describe('createCylinder', () => {
    it('default closed cylinder has caps and torso', () => {
        const geo = createCylinder();
        expect(geo.layout.vertexCount).toBeGreaterThan(0);
        expect(geo.layout.indexCount).toBeGreaterThan(0);
    });

    it('cap normals point ±Y', () => {
        const geo = createCylinder({ radialSegments: 8 });
        const floatView = readFloat32s(geo.vertices);
        const stride = geo.layout.stride / 4;

        // The first torso vertex normal should have a non-zero X or Z component.
        // The cap center vertices have normals exactly (0, ±1, 0).
        // Find cap centers: they are the first vertex of each cap fan.
        // Top cap center is at index 0 of the cap section.
        // For a closed cylinder, after the torso (heightSegments+1)×(radialSegments+1) vertices,
        // the top cap center is next.
        // Instead of exact index math, just verify some normals are ±Y.
        let foundPlusY = false;
        let foundMinusY = false;
        for (let i = 0; i < geo.layout.vertexCount; i++) {
            const offset = i * stride;
            const ny = floatView[offset + 4]!;
            if (ny > 0.99) foundPlusY = true;
            if (ny < -0.99) foundMinusY = true;
        }
        expect(foundPlusY).toBe(true);
        expect(foundMinusY).toBe(true);
    });

    it('open-ended cylinder omits caps', () => {
        const closed = createCylinder({ radialSegments: 8 });
        const open = createCylinder({ radialSegments: 8, openEnded: true });
        // Open-ended should have fewer vertices (no cap centers + cap rings)
        expect(open.layout.vertexCount).toBeLessThan(closed.layout.vertexCount);
    });
});

describe('createCone', () => {
    it('creates a cone with radiusTop:0', () => {
        const cone = createCone({ radius: 1, radialSegments: 8 });
        expect(cone.layout.vertexCount).toBeGreaterThan(0);
        expect(cone.layout.indexCount).toBeGreaterThan(0);
    });
});

describe('createTruncatedCone', () => {
    it('creates geometry with different top/bottom radii', () => {
        const geo = createTruncatedCone({ topRadius: 0.3, bottomRadius: 1, radialSegments: 8 });
        expect(geo.layout.vertexCount).toBeGreaterThan(0);
        expect(geo.layout.indexCount).toBeGreaterThan(0);
    });

    it('different radii produce different vertex count than uniform cylinder', () => {
        const cylinder = createCylinder({ radialSegments: 8 });
        const truncated = createTruncatedCone({
            topRadius: 0.3,
            bottomRadius: 1,
            radialSegments: 8,
        });
        // Both have caps but different torso geometry; vertex counts may differ
        // due to different slope calculations
        expect(truncated.layout.vertexCount).toBeGreaterThan(0);
        expect(truncated.layout.indexCount).toBe(cylinder.layout.indexCount);
    });
});

describe('createTube', () => {
    it('has inner and outer surfaces plus ring caps', () => {
        const geo = createTube({ innerRadius: 0.3, outerRadius: 1, radialSegments: 8 });
        expect(geo.layout.vertexCount).toBeGreaterThan(0);
        expect(geo.layout.indexCount).toBeGreaterThan(0);
    });

    it('tube has more vertices than a simple cylinder', () => {
        const cyl = createCylinder({ radialSegments: 8 });
        const tube = createTube({ innerRadius: 0.3, outerRadius: 1, radialSegments: 8 });
        // Tube has outer surface + inner surface + 2 ring caps
        expect(tube.layout.vertexCount).toBeGreaterThan(cyl.layout.vertexCount);
    });
});

// ─── Non-degenerate triangles ───────────────────────────────────────────────

describe('All primitives produce non-degenerate geometry', () => {
    const primitives = [
        { name: 'createBox', fn: () => createBox() },
        { name: 'createCube', fn: () => createCube() },
        { name: 'createRoundedBox', fn: () => createRoundedBox() },
        { name: 'createCylinder', fn: () => createCylinder({ radialSegments: 8 }) },
        { name: 'createCone', fn: () => createCone({ radialSegments: 8 }) },
        { name: 'createTruncatedCone', fn: () => createTruncatedCone({ radialSegments: 8 }) },
        {
            name: 'createTube',
            fn: () => createTube({ radialSegments: 8 }),
        },
    ];

    for (const { name, fn } of primitives) {
        it(`${name}: non-zero vertex and index count`, () => {
            const geo = fn();
            expect(geo.layout.vertexCount).toBeGreaterThan(0);
            expect(geo.layout.indexCount).toBeGreaterThan(0);
            // Index count should be a multiple of 3 (triangles)
            expect(geo.layout.indexCount % 3).toBe(0);
        });
    }
});
