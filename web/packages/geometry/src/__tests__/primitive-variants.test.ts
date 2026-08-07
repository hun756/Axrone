import { describe, expect, it } from 'vitest';
import { Vec3 } from '@axrone/numeric';
import {
    createCapsule,
    createPill,
    createUVSphere,
    createIcosphere,
    createTorusKnot,
    createSpring,
    createGrid,
    createPlane,
    createCircle,
    createRing,
} from '@axrone/geometry';

// ByteBuffer defaults to big-endian byte order, so typed-array views (native endian)
// produce incorrect values. Use the buffer's own getters for correct reads.
const readFloat32s = (bb: any): number[] => {
    bb.rewind();
    const out: number[] = [];
    while (bb.remaining >= 4) out.push(bb.getFloat32());
    return out;
};

// ─── Capsule ────────────────────────────────────────────────────────────────

describe('createCapsule', () => {
    it('produces valid geometry with non-zero counts', () => {
        const geo = createCapsule();
        expect(geo.layout.vertexCount).toBeGreaterThan(0);
        expect(geo.layout.indexCount).toBeGreaterThan(0);
    });

    it('pole vertices are at ±(halfLength + radius) on Y axis', () => {
        const radius = 0.5;
        const length = 2;
        const halfLength = length * 0.5;
        const expectedPole = halfLength + radius; // 1.5

        const geo = createCapsule({ radius, length, capSegments: 8, radialSegments: 16 });
        const floatView = readFloat32s(geo.vertices);
        const stride = geo.layout.stride / 4;

        let maxY = -Infinity;
        let minY = Infinity;
        for (let i = 0; i < geo.layout.vertexCount; i++) {
            const y = floatView[i * stride + 1]!;
            if (y > maxY) maxY = y;
            if (y < minY) minY = y;
        }

        expect(maxY).toBeCloseTo(expectedPole, 3);
        expect(minY).toBeCloseTo(-expectedPole, 3);
    });

    it('body normals are radial (XZ plane)', () => {
        const geo = createCapsule({ radius: 0.5, length: 1, radialSegments: 16 });
        const floatView = readFloat32s(geo.vertices);
        const stride = geo.layout.stride / 4;

        // The cylinder body vertices are at y = ±halfLength.
        // Their normals should be in the XZ plane (ny ≈ 0).
        const halfLength = 0.5;
        let foundBodyNormal = false;
        for (let i = 0; i < geo.layout.vertexCount; i++) {
            const offset = i * stride;
            const y = floatView[offset + 1]!;
            const ny = floatView[offset + 4]!;
            if (Math.abs(y - halfLength) < 0.01 || Math.abs(y + halfLength) < 0.01) {
                if (Math.abs(ny) < 0.01) {
                    foundBodyNormal = true;
                    break;
                }
            }
        }
        expect(foundBodyNormal).toBe(true);
    });
});

// ─── Pill ───────────────────────────────────────────────────────────────────

describe('createPill', () => {
    it('produces valid geometry', () => {
        const geo = createPill();
        expect(geo.layout.vertexCount).toBeGreaterThan(0);
        expect(geo.layout.indexCount).toBeGreaterThan(0);
    });

    it('has different vertex count from capsule (different algorithm)', () => {
        const capsule = createCapsule({ radius: 0.5, length: 1, capSegments: 8, radialSegments: 16 });
        const pill = createPill({ radius: 0.5, length: 1, capSegments: 8, radialSegments: 16 });
        // Pill uses a ring-based approach, so vertex counts differ
        expect(pill.layout.vertexCount).not.toBe(capsule.layout.vertexCount);
    });
});

// ─── UV Sphere ──────────────────────────────────────────────────────────────

describe('createUVSphere', () => {
    it('pole vertices are at ±Y', () => {
        const radius = 2;
        const geo = createUVSphere({ radius, widthSegments: 16, heightSegments: 8 });
        const floatView = readFloat32s(geo.vertices);
        const stride = geo.layout.stride / 4;

        // First vertex is the north pole
        const northY = floatView[1]!;
        expect(northY).toBeCloseTo(radius, 5);

        // Last vertex is the south pole
        const lastOffset = (geo.layout.vertexCount - 1) * stride;
        const southY = floatView[lastOffset + 1]!;
        expect(southY).toBeCloseTo(-radius, 5);
    });

    it('vertex count = (heightSegments-1)*widthSegments + 2', () => {
        const w = 16;
        const h = 8;
        const expected = (h - 1) * w + 2; // 7*16+2 = 114
        const geo = createUVSphere({ widthSegments: w, heightSegments: h });
        expect(geo.layout.vertexCount).toBe(expected);
    });
});

// ─── Icosphere ──────────────────────────────────────────────────────────────

describe('createIcosphere', () => {
    it('starts with 12 base vertices from icosahedron', () => {
        const geo = createIcosphere({ subdivisions: 0 });
        // 12 icosahedron vertices, but subdivision code clears and re-adds them
        // With subdivisions:0, the code still runs the subdivision block which clears
        // Actually with subdivisions:0, the if(subdivisions > 0) block is skipped
        // So we get 12 initial vertices + 20 faces (60 indices)
        expect(geo.layout.vertexCount).toBe(12);
    });

    it('subdivisions increase index count', () => {
        const geo0 = createIcosphere({ subdivisions: 0 });
        const geo1 = createIcosphere({ subdivisions: 1 });
        const geo2 = createIcosphere({ subdivisions: 2 });
        // Each subdivision level quadruples the face count
        expect(geo1.layout.indexCount).toBeGreaterThan(geo0.layout.indexCount);
        expect(geo2.layout.indexCount).toBeGreaterThan(geo1.layout.indexCount);
    });

    it('all vertices are at radius distance from origin', () => {
        const radius = 3;
        const geo = createIcosphere({ radius, subdivisions: 2 });
        const floatView = readFloat32s(geo.vertices);
        const stride = geo.layout.stride / 4;

        for (let i = 0; i < geo.layout.vertexCount; i++) {
            const offset = i * stride;
            const x = floatView[offset]!;
            const y = floatView[offset + 1]!;
            const z = floatView[offset + 2]!;
            const dist = Math.sqrt(x * x + y * y + z * z);
            expect(dist).toBeCloseTo(radius, 2);
        }
    });
});

// ─── Torus Knot ─────────────────────────────────────────────────────────────

describe('createTorusKnot', () => {
    it('vertex count = (radialSegments+1)*(tubularSegments+1)', () => {
        const radial = 8;
        const tubular = 24;
        const expected = (radial + 1) * (tubular + 1); // 9*25 = 225
        const geo = createTorusKnot({ radialSegments: radial, tubularSegments: tubular });
        expect(geo.layout.vertexCount).toBe(expected);
    });

    it('produces valid geometry with non-zero indices', () => {
        const geo = createTorusKnot();
        expect(geo.layout.indexCount).toBeGreaterThan(0);
        expect(geo.layout.indexCount % 3).toBe(0);
    });
});

// ─── Spring ─────────────────────────────────────────────────────────────────

describe('createSpring', () => {
    it('produces helical geometry', () => {
        const geo = createSpring({ coils: 3, pitch: 1, tubularSegments: 24, radialSegments: 8 });
        expect(geo.layout.vertexCount).toBeGreaterThan(0);
        expect(geo.layout.indexCount).toBeGreaterThan(0);
    });

    it('different coils alter vertex Y range', () => {
        const geo1 = createSpring({ coils: 2, pitch: 1, tubularSegments: 12 });
        const geo2 = createSpring({ coils: 5, pitch: 1, tubularSegments: 12 });

        const floatView1 = readFloat32s(geo1.vertices);
        const stride1 = geo1.layout.stride / 4;
        const floatView2 = readFloat32s(geo2.vertices);
        const stride2 = geo2.layout.stride / 4;

        let minY1 = Infinity,
            maxY1 = -Infinity;
        for (let i = 0; i < geo1.layout.vertexCount; i++) {
            const y = floatView1[i * stride1 + 1]!;
            if (y < minY1) minY1 = y;
            if (y > maxY1) maxY1 = y;
        }

        let minY2 = Infinity,
            maxY2 = -Infinity;
        for (let i = 0; i < geo2.layout.vertexCount; i++) {
            const y = floatView2[i * stride2 + 1]!;
            if (y < minY2) minY2 = y;
            if (y > maxY2) maxY2 = y;
        }

        const range1 = maxY1 - minY1;
        const range2 = maxY2 - minY2;
        // More coils with same pitch → larger Y range
        expect(range2).toBeGreaterThan(range1);
    });
});

// ─── Grid ───────────────────────────────────────────────────────────────────

describe('createGrid', () => {
    it('showLines:true produces wireframe geometry', () => {
        const geo = createGrid({ showLines: true, width: 10, height: 10, widthSegments: 5, heightSegments: 5 });
        expect(geo.layout.vertexCount).toBeGreaterThan(0);
        expect(geo.layout.indexCount).toBeGreaterThan(0);
    });

    it('showLines:false delegates to createPlane', () => {
        const grid = createGrid({ showLines: false, width: 5, height: 5, widthSegments: 2, heightSegments: 2 });
        const plane = createPlane({ width: 5, height: 5, widthSegments: 2, heightSegments: 2 });
        expect(grid.layout.vertexCount).toBe(plane.layout.vertexCount);
        expect(grid.layout.indexCount).toBe(plane.layout.indexCount);
    });
});

// ─── Plane segmented ────────────────────────────────────────────────────────

describe('createPlane segmented', () => {
    it('widthSegments:3, heightSegments:3 → 16 vertices (4×4 grid)', () => {
        const geo = createPlane({ widthSegments: 3, heightSegments: 3 });
        expect(geo.layout.vertexCount).toBe(16);
    });

    it('default plane has 4 vertices and 6 indices', () => {
        const geo = createPlane();
        expect(geo.layout.vertexCount).toBe(4);
        expect(geo.layout.indexCount).toBe(6);
    });
});

// ─── Circle ─────────────────────────────────────────────────────────────────

describe('createCircle', () => {
    it('vertex count = segments + 2 (center + ring + closing vertex)', () => {
        const segments = 16;
        const geo = createCircle({ segments });
        // center + (segments+1) ring vertices = segments + 2
        expect(geo.layout.vertexCount).toBe(segments + 2);
    });

    it('ring vertices are at radius distance', () => {
        const radius = 5;
        const geo = createCircle({ radius, segments: 16 });
        const floatView = readFloat32s(geo.vertices);
        const stride = geo.layout.stride / 4;

        // Skip center vertex (index 0), check ring vertices
        for (let i = 1; i < geo.layout.vertexCount; i++) {
            const offset = i * stride;
            const x = floatView[offset]!;
            const z = floatView[offset + 2]!;
            const dist = Math.sqrt(x * x + z * z);
            expect(dist).toBeCloseTo(radius, 3);
        }
    });
});

// ─── Ring ───────────────────────────────────────────────────────────────────

describe('createRing', () => {
    it('vertex count = (segments+1)*2 (inner + outer per segment)', () => {
        const segments = 16;
        const geo = createRing({ segments });
        expect(geo.layout.vertexCount).toBe((segments + 1) * 2);
    });

    it('inner vertices at innerRadius, outer at outerRadius', () => {
        const innerRadius = 1;
        const outerRadius = 3;
        const geo = createRing({ innerRadius, outerRadius, segments: 16 });
        const floatView = readFloat32s(geo.vertices);
        const stride = geo.layout.stride / 4;

        for (let i = 0; i < geo.layout.vertexCount; i += 2) {
            const innerOffset = i * stride;
            const outerOffset = (i + 1) * stride;

            const ix = floatView[innerOffset]!;
            const iz = floatView[innerOffset + 2]!;
            const innerDist = Math.sqrt(ix * ix + iz * iz);
            expect(innerDist).toBeCloseTo(innerRadius, 3);

            const ox = floatView[outerOffset]!;
            const oz = floatView[outerOffset + 2]!;
            const outerDist = Math.sqrt(ox * ox + oz * oz);
            expect(outerDist).toBeCloseTo(outerRadius, 3);
        }
    });
});
