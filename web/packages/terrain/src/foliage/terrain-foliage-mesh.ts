import { TerrainError, TerrainErrorCode } from '../errors';
import type { TerrainFoliageInstance, TerrainMeshData } from '../types';

/**
 * Batches all scattered instances of one foliage layer into a single
 * {@link TerrainMeshData}, avoiding per-instance draw calls. Positions are
 * terrain-local, matching the terrain surface mesh space.
 */

export interface TerrainFoliageSourceMesh {
    readonly positions: Float32Array;
    readonly normals: Float32Array;
    readonly uvs: Float32Array;
    readonly indices: Uint32Array;
}

/**
 * Built-in grass card: two perpendicular unit quads crossing at the origin,
 * 1 unit tall, facing outward on both sides (renderers should disable
 * backface culling for foliage).
 */
export const createTerrainFoliageCardMesh = (): TerrainFoliageSourceMesh => {
    const positions: number[] = [];
    const normals: number[] = [];
    const uvs: number[] = [];
    const indices: number[] = [];

    const addQuad = (nx: number, nz: number): void => {
        // Quad duzlemi normale dik: yatay ekseni (nz, 0, -nx).
        const base = positions.length / 3;
        const halfX = nz * 0.5;
        const halfZ = -nx * 0.5;
        positions.push(
            -halfX, 0, -halfZ,
            halfX, 0, halfZ,
            halfX, 1, halfZ,
            -halfX, 1, -halfZ
        );
        for (let corner = 0; corner < 4; corner += 1) {
            normals.push(nx, 0, nz);
        }
        uvs.push(0, 1, 1, 1, 1, 0, 0, 0);
        indices.push(base, base + 1, base + 2, base, base + 2, base + 3);
    };

    addQuad(0, 1);
    addQuad(1, 0);

    return Object.freeze({
        positions: new Float32Array(positions),
        normals: new Float32Array(normals),
        uvs: new Float32Array(uvs),
        indices: new Uint32Array(indices),
    });
};

const validateSourceMesh = (source: TerrainFoliageSourceMesh): void => {
    const vertexCount = source.positions.length / 3;
    if (
        source.positions.length % 3 !== 0 ||
        source.normals.length !== source.positions.length ||
        source.uvs.length !== vertexCount * 2 ||
        source.indices.length % 3 !== 0
    ) {
        throw new TerrainError(
            'Foliage source mesh buffers are inconsistent (positions/normals xyz triples, uvs pairs, indices triangles).',
            TerrainErrorCode.VALIDATION_FAILED,
            {
                positions: source.positions.length,
                normals: source.normals.length,
                uvs: source.uvs.length,
                indices: source.indices.length,
            }
        );
    }
};

/**
 * Expands every instance (translate/rotateY/scale) into one merged mesh.
 * Returns an empty mesh when there are no instances.
 */
export const buildTerrainFoliageBatchMesh = (
    instances: readonly TerrainFoliageInstance[],
    sourceMesh: TerrainFoliageSourceMesh = createTerrainFoliageCardMesh()
): TerrainMeshData => {
    validateSourceMesh(sourceMesh);

    const sourceVertexCount = sourceMesh.positions.length / 3;
    const vertexCount = sourceVertexCount * instances.length;
    const positions = new Float32Array(vertexCount * 3);
    const normals = new Float32Array(vertexCount * 3);
    const uvs = new Float32Array(vertexCount * 2);
    const indices = new Uint32Array(sourceMesh.indices.length * instances.length);

    for (let instanceIndex = 0; instanceIndex < instances.length; instanceIndex += 1) {
        const instance = instances[instanceIndex]!;
        const cos = Math.cos(instance.rotationY);
        const sin = Math.sin(instance.rotationY);
        const vertexOffset = instanceIndex * sourceVertexCount;

        for (let vertex = 0; vertex < sourceVertexCount; vertex += 1) {
            const sourceOffset = vertex * 3;
            const localX = sourceMesh.positions[sourceOffset]! * instance.scale;
            const localY = sourceMesh.positions[sourceOffset + 1]! * instance.scale;
            const localZ = sourceMesh.positions[sourceOffset + 2]! * instance.scale;

            const targetOffset = (vertexOffset + vertex) * 3;
            positions[targetOffset] = localX * cos + localZ * sin + instance.x;
            positions[targetOffset + 1] = localY + instance.y;
            positions[targetOffset + 2] = -localX * sin + localZ * cos + instance.z;

            const normalX = sourceMesh.normals[sourceOffset]!;
            const normalY = sourceMesh.normals[sourceOffset + 1]!;
            const normalZ = sourceMesh.normals[sourceOffset + 2]!;
            normals[targetOffset] = normalX * cos + normalZ * sin;
            normals[targetOffset + 1] = normalY;
            normals[targetOffset + 2] = -normalX * sin + normalZ * cos;

            const uvSource = vertex * 2;
            const uvTarget = (vertexOffset + vertex) * 2;
            uvs[uvTarget] = sourceMesh.uvs[uvSource]!;
            uvs[uvTarget + 1] = sourceMesh.uvs[uvSource + 1]!;
        }

        const indexOffset = instanceIndex * sourceMesh.indices.length;
        for (let index = 0; index < sourceMesh.indices.length; index += 1) {
            indices[indexOffset + index] = sourceMesh.indices[index]! + vertexOffset;
        }
    }

    return Object.freeze({
        positions,
        normals,
        uvs,
        indices,
        vertexCount,
        triangleCount: indices.length / 3,
    });
};
