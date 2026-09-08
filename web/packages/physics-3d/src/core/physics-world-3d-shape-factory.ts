import { Vec3 } from '@axrone/numeric';
import type { IMaterial } from '../types';
import type {
    BodyId3D,
    IBoxShapeDef3D,
    ICapsuleShapeDef3D,
    ICollisionFilter3D,
    IConeShapeDef3D,
    IConvexHullShapeDef3D,
    ICylinderShapeDef3D,
    IHeightFieldShapeDef3D,
    ISphereShapeDef3D,
    ITriangleMeshShapeDef3D,
    ShapeId3D,
} from '../types/physics-3d';
import type { ShapeManager3D } from './physics-managers-3d';
import {
    SHAPE_TYPE_BOX,
    SHAPE_TYPE_CAPSULE,
    SHAPE_TYPE_CONE,
    SHAPE_TYPE_CONVEX_HULL,
    SHAPE_TYPE_CYLINDER,
    SHAPE_TYPE_HEIGHTFIELD,
    SHAPE_TYPE_SPHERE,
    SHAPE_TYPE_TRIANGLE_MESH,
    type IShapeDescriptor3D,
    type IShapeOptions3D,
    makeFilter,
    makeMaterial,
} from './physics-world-3d-shared';

/**
 * Common descriptor builder for all shape types.
 * Reduces duplication across the 8 create*Shape factory functions.
 */
function buildDescriptor(
    shapeId: ShapeId3D,
    bodyId: BodyId3D,
    type: number,
    def: IShapeDescriptor3D['def'],
    material?: Partial<IMaterial>,
    filter?: ICollisionFilter3D,
    options?: IShapeOptions3D
): IShapeDescriptor3D {
    return {
        id: shapeId,
        bodyId,
        type,
        def,
        material: makeMaterial(material),
        isSensor: options?.isSensor ?? false,
        filter: makeFilter(filter),
        ...(options?.userData !== undefined ? { userData: options.userData } : {}),
    };
}

export function createSphereShape(
    shapeManager: ShapeManager3D,
    shapeDescriptors: Map<ShapeId3D, IShapeDescriptor3D>,
    bodyId: BodyId3D,
    def: ISphereShapeDef3D,
    material?: Partial<IMaterial>,
    filter?: ICollisionFilter3D,
    options?: IShapeOptions3D
): ShapeId3D {
    const shapeId = shapeManager.createSphere(bodyId, def, material, filter);
    shapeDescriptors.set(shapeId, buildDescriptor(shapeId, bodyId, SHAPE_TYPE_SPHERE, { ...def, kind: SHAPE_TYPE_SPHERE }, material, filter, options));
    return shapeId;
}

export function createBoxShape(
    shapeManager: ShapeManager3D,
    shapeDescriptors: Map<ShapeId3D, IShapeDescriptor3D>,
    bodyId: BodyId3D,
    def: IBoxShapeDef3D,
    material?: Partial<IMaterial>,
    filter?: ICollisionFilter3D,
    options?: IShapeOptions3D
): ShapeId3D {
    const shapeId = shapeManager.createBox(bodyId, def, material, filter);
    shapeDescriptors.set(shapeId, buildDescriptor(shapeId, bodyId, SHAPE_TYPE_BOX, { ...def, kind: SHAPE_TYPE_BOX }, material, filter, options));
    return shapeId;
}

export function createCapsuleShape(
    shapeManager: ShapeManager3D,
    shapeDescriptors: Map<ShapeId3D, IShapeDescriptor3D>,
    bodyId: BodyId3D,
    def: ICapsuleShapeDef3D,
    material?: Partial<IMaterial>,
    filter?: ICollisionFilter3D,
    options?: IShapeOptions3D
): ShapeId3D {
    const shapeId = shapeManager.createCapsule(bodyId, def, material, filter);
    shapeDescriptors.set(shapeId, buildDescriptor(shapeId, bodyId, SHAPE_TYPE_CAPSULE, { ...def, kind: SHAPE_TYPE_CAPSULE }, material, filter, options));
    return shapeId;
}

export function createCylinderShape(
    shapeManager: ShapeManager3D,
    shapeDescriptors: Map<ShapeId3D, IShapeDescriptor3D>,
    bodyId: BodyId3D,
    def: ICylinderShapeDef3D,
    material?: Partial<IMaterial>,
    filter?: ICollisionFilter3D,
    options?: IShapeOptions3D
): ShapeId3D {
    const shapeId = shapeManager.createCylinder(bodyId, def, material, filter);
    shapeDescriptors.set(shapeId, buildDescriptor(shapeId, bodyId, SHAPE_TYPE_CYLINDER, { ...def, kind: SHAPE_TYPE_CYLINDER }, material, filter, options));
    return shapeId;
}

export function createConeShape(
    shapeManager: ShapeManager3D,
    shapeDescriptors: Map<ShapeId3D, IShapeDescriptor3D>,
    bodyId: BodyId3D,
    def: IConeShapeDef3D,
    material?: Partial<IMaterial>,
    filter?: ICollisionFilter3D,
    options?: IShapeOptions3D
): ShapeId3D {
    const shapeId = shapeManager.createCone(bodyId, def, material, filter);
    shapeDescriptors.set(shapeId, buildDescriptor(shapeId, bodyId, SHAPE_TYPE_CONE, { ...def, kind: SHAPE_TYPE_CONE }, material, filter, options));
    return shapeId;
}

export function createConvexHullShape(
    shapeManager: ShapeManager3D,
    shapeDescriptors: Map<ShapeId3D, IShapeDescriptor3D>,
    bodyId: BodyId3D,
    def: IConvexHullShapeDef3D,
    material?: Partial<IMaterial>,
    filter?: ICollisionFilter3D,
    options?: IShapeOptions3D
): ShapeId3D {
    const shapeId = shapeManager.createConvexHull(bodyId, def, material, filter);
    shapeDescriptors.set(shapeId, buildDescriptor(
        shapeId, bodyId, SHAPE_TYPE_CONVEX_HULL,
        { ...def, vertices: def.vertices.map(Vec3.copy), kind: SHAPE_TYPE_CONVEX_HULL },
        material, filter, options
    ));
    return shapeId;
}

export function createTriangleMeshShape(
    shapeManager: ShapeManager3D,
    shapeDescriptors: Map<ShapeId3D, IShapeDescriptor3D>,
    bodyId: BodyId3D,
    def: ITriangleMeshShapeDef3D,
    material?: Partial<IMaterial>,
    filter?: ICollisionFilter3D,
    options?: IShapeOptions3D
): ShapeId3D {
    const shapeId = shapeManager.createTriangleMesh(bodyId, def, material, filter);
    shapeDescriptors.set(shapeId, buildDescriptor(
        shapeId, bodyId, SHAPE_TYPE_TRIANGLE_MESH,
        {
            vertices: def.vertices.map((v) => Vec3.copy(v)),
            indices: [...def.indices],
            kind: SHAPE_TYPE_TRIANGLE_MESH,
        },
        material, filter, options
    ));
    return shapeId;
}

export function createHeightFieldShape(
    shapeManager: ShapeManager3D,
    shapeDescriptors: Map<ShapeId3D, IShapeDescriptor3D>,
    bodyId: BodyId3D,
    def: IHeightFieldShapeDef3D,
    material?: Partial<IMaterial>,
    filter?: ICollisionFilter3D,
    options?: IShapeOptions3D
): ShapeId3D {
    const shapeId = shapeManager.createHeightField(bodyId, def, material, filter);
    shapeDescriptors.set(shapeId, buildDescriptor(
        shapeId, bodyId, SHAPE_TYPE_HEIGHTFIELD,
        {
            heights: new Float32Array(def.heights),
            width: def.width,
            depth: def.depth,
            scaleX: def.scaleX,
            scaleY: def.scaleY,
            scaleZ: def.scaleZ,
            kind: SHAPE_TYPE_HEIGHTFIELD,
        },
        material, filter, options
    ));
    return shapeId;
}
