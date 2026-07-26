import { type IVec3Like, Vec3 } from '@axrone/numeric';
import { script } from '@axrone/ecs-runtime/decorators';
import type { IConvexHullShapeDef3D, ITriangleMeshShapeDef3D } from '../types';
import { Collider3D, INVALID_SHAPE_ID } from './collider3d';

@script({ scriptName: 'MeshCollider3D' })
export class MeshCollider3D extends Collider3D {
    private _convex: boolean = true;
    private _vertices: Float32Array = new Float32Array(0);
    private _indices: Uint32Array = new Uint32Array(0);
    get convex(): boolean {
        return this._convex;
    }
    set convex(value: boolean) {
        this._convex = value;
        this._updateShape();
    }
    get vertices(): Readonly<Float32Array> {
        return this._vertices;
    }
    get indices(): Readonly<Uint32Array> {
        return this._indices;
    }
    setMesh(vertices: Float32Array, indices: Uint32Array): void {
        this._vertices = vertices;
        this._indices = indices;
        this._updateShape();
    }

    protected override _createShape(): void {
        if (!this._rigidbody || this._vertices.length === 0) return;
        if (this._convex) {
            const vArr: IVec3Like[] = [];
            for (let i = 0; i < this._vertices.length; i += 3)
                vArr.push({
                    x: this._vertices[i],
                    y: this._vertices[i + 1],
                    z: this._vertices[i + 2],
                });
            const def: IConvexHullShapeDef3D = { vertices: vArr };
            if (this._world) {
                this._shapeId = this._world.createConvexHullShape(
                    this._rigidbody.bodyId,
                    def,
                    this._getMaterial(),
                    this._getFilter(),
                    { isSensor: this.isTrigger }
                );
                return;
            }

            if (!this._shapeManager) return;
            this._shapeId = this._shapeManager.createConvexHull(this._rigidbody.bodyId, def, this._getMaterial(), this._getFilter());
            return;
        }

        const vertices: IVec3Like[] = [];
        for (let i = 0; i < this._vertices.length; i += 3) {
            vertices.push({
                x: this._vertices[i],
                y: this._vertices[i + 1],
                z: this._vertices[i + 2],
            });
        }

        const indices = Array.from(this._indices, (value) => value as number);
        const def: ITriangleMeshShapeDef3D = { vertices, indices };

        if (this._world) {
            this._shapeId = this._world.createTriangleMeshShape(
                this._rigidbody.bodyId,
                def,
                this._getMaterial(),
                this._getFilter(),
                { isSensor: this.isTrigger }
            );
            return;
        }

        if (!this._shapeManager) return;
        this._shapeId = this._shapeManager.createTriangleMesh(this._rigidbody.bodyId, def, this._getMaterial(), this._getFilter());
    }

    protected override _updateShape(): void {
        if (this._shapeId === INVALID_SHAPE_ID) return;
        this._destroyCurrentShape();
        this._createShape();
    }

    protected override _calculateBounds(): void {
        if (this._vertices.length === 0) return;
        let mnX = Infinity;
        let mnY = Infinity;
        let mnZ = Infinity;
        let mxX = -Infinity;
        let mxY = -Infinity;
        let mxZ = -Infinity;
        for (let i = 0; i < this._vertices.length; i += 3) {
            const x = this._vertices[i];
            const y = this._vertices[i + 1];
            const z = this._vertices[i + 2];
            if (x < mnX) mnX = x;
            if (y < mnY) mnY = y;
            if (z < mnZ) mnZ = z;
            if (x > mxX) mxX = x;
            if (y > mxY) mxY = y;
            if (z > mxZ) mxZ = z;
        }
        const wp = this.transform?.worldPosition ?? Vec3.ZERO;
        this._setBounds(wp.x + mnX, wp.y + mnY, wp.z + mnZ, wp.x + mxX, wp.y + mxY, wp.z + mxZ);
    }

    protected override _closestPointOnCollider(position: IVec3Like): IVec3Like {
        return position;
    }
    protected override _raycastCollider(
        ray: { origin: IVec3Like; direction: IVec3Like },
        maxDistance: number
    ): { hit: boolean; point?: IVec3Like; normal?: IVec3Like; distance?: number } {
        if (this._world && this._shapeId !== INVALID_SHAPE_ID) {
            const hit = this._world.getShape(this._shapeId)?.rayCast(
                ray.origin,
                ray.direction,
                maxDistance
            );
            if (hit) {
                return {
                    hit: true,
                    point: {
                        x: ray.origin.x + ray.direction.x * hit.fraction,
                        y: ray.origin.y + ray.direction.y * hit.fraction,
                        z: ray.origin.z + ray.direction.z * hit.fraction,
                    },
                    normal: hit.normal,
                    distance: hit.fraction,
                };
            }
        }

        return { hit: false };
    }
}
