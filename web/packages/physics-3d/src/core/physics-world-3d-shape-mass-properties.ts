import { Vec3 } from '@axrone/numeric';
import type { IVec3Like } from '@axrone/numeric';
import type { IMassData3D, Mass } from '../types';
import type { IHeightFieldShapeDef3D } from '../types/physics-3d';
import {
    SHAPE_TYPE_BOX,
    SHAPE_TYPE_CAPSULE,
    SHAPE_TYPE_CONE,
    SHAPE_TYPE_CONVEX_HULL,
    SHAPE_TYPE_CYLINDER,
    SHAPE_TYPE_HEIGHTFIELD,
    SHAPE_TYPE_SPHERE,
    SHAPE_TYPE_TRIANGLE_MESH,
    type IAabb3D,
    type IShapeDescriptor3D,
    expandAabb,
    getHeightFieldLocalVertex,
    inverseVec3,
    midpointVec3,
} from './physics-world-3d-shared';

/**
 * Computes the axis-aligned bounding box for a set of local vertices.
 * Shared by mass-properties and geometry subsystems.
 */
export function computeLocalConvexBounds(vertices: readonly IVec3Like[]): IAabb3D {
    let bounds: IAabb3D | null = null;
    for (const vertex of vertices) {
        bounds = bounds
            ? expandAabb(bounds, vertex)
            : { min: Vec3.copy(vertex), max: Vec3.copy(vertex) };
    }
    return bounds ?? { min: { x: 0, y: 0, z: 0 }, max: { x: 0, y: 0, z: 0 } };
}

/**
 * Computes the axis-aligned bounding box for a heightfield shape in local space.
 * Shared by mass-properties and geometry subsystems.
 */
export function computeLocalHeightFieldBounds(def: Readonly<IHeightFieldShapeDef3D>): IAabb3D {
    let bounds: IAabb3D | null = null;
    for (let zIndex = 0; zIndex < def.depth; zIndex += 1) {
        for (let xIndex = 0; xIndex < def.width; xIndex += 1) {
            const vertex = getHeightFieldLocalVertex(def, xIndex, zIndex);
            bounds = bounds
                ? expandAabb(bounds, vertex)
                : { min: Vec3.copy(vertex), max: Vec3.copy(vertex) };
        }
    }

    return bounds ?? { min: { x: 0, y: 0, z: 0 }, max: { x: 0, y: 0, z: 0 } };
}

/**
 * Computes mass and inertia properties for a single shape descriptor.
 * Extracted from PhysicsWorld3D — pure computation, no world state access.
 */
export function computeShapeMassData(descriptor: IShapeDescriptor3D, density: number): IMassData3D {
    const safeDensity = Math.max(0, density);
    switch (descriptor.def.kind) {
        case SHAPE_TYPE_SPHERE: {
            const radius = descriptor.def.radius;
            const mass = ((4 / 3) * Math.PI * radius * radius * radius) * safeDensity;
            const inertia = (2 / 5) * mass * radius * radius;
            return {
                mass: mass as Mass,
                inverseMass: mass > 0 ? 1 / mass : 0,
                inertiaTensor: { x: inertia, y: inertia, z: inertia },
                inverseInertiaTensor: inverseVec3({ x: inertia, y: inertia, z: inertia }),
                center: Vec3.copy(descriptor.def.center),
            };
        }
        case SHAPE_TYPE_BOX: {
            const halfExtents = descriptor.def.halfExtents;
            const fullExtents = Vec3.multiplyScalar(halfExtents, 2);
            const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
            const inertiaTensor = {
                x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
            };
            return {
                mass: mass as Mass,
                inverseMass: mass > 0 ? 1 / mass : 0,
                inertiaTensor,
                inverseInertiaTensor: inverseVec3(inertiaTensor),
                center: Vec3.copy(descriptor.def.center),
            };
        }
        case SHAPE_TYPE_CAPSULE: {
            const segment = Vec3.subtract(descriptor.def.p2, descriptor.def.p1);
            const segmentLength = Vec3.len(segment);
            const radius = descriptor.def.radius;
            const cylinderMass = Math.PI * radius * radius * segmentLength * safeDensity;
            const sphereMass = ((4 / 3) * Math.PI * radius * radius * radius) * safeDensity;
            const mass = cylinderMass + sphereMass;
            const inertia = radius * radius * mass;
            return {
                mass: mass as Mass,
                inverseMass: mass > 0 ? 1 / mass : 0,
                inertiaTensor: { x: inertia, y: inertia, z: inertia },
                inverseInertiaTensor: inverseVec3({ x: inertia, y: inertia, z: inertia }),
                center: midpointVec3(descriptor.def.p1, descriptor.def.p2),
            };
        }
        case SHAPE_TYPE_CYLINDER: {
            const radius = descriptor.def.radius;
            const height = descriptor.def.height;
            const mass = Math.PI * radius * radius * height * safeDensity;
            const radial = (mass * (3 * radius * radius + height * height)) / 12;
            const axial = 0.5 * mass * radius * radius;
            const axis = descriptor.def.axis ?? 1;
            const inertiaTensor =
                axis === 0
                    ? { x: axial, y: radial, z: radial }
                    : axis === 2
                      ? { x: radial, y: radial, z: axial }
                      : { x: radial, y: axial, z: radial };
            return {
                mass: mass as Mass,
                inverseMass: mass > 0 ? 1 / mass : 0,
                inertiaTensor,
                inverseInertiaTensor: inverseVec3(inertiaTensor),
                center: Vec3.copy(descriptor.def.center),
            };
        }
        case SHAPE_TYPE_CONE: {
            const radius = descriptor.def.radius;
            const height = descriptor.def.height;
            const mass = ((Math.PI * radius * radius * height) / 3) * safeDensity;
            const axis = descriptor.def.axis ?? 1;
            const transverse = ((3 / 20) * mass * radius * radius) + ((3 / 5) * mass * height * height);
            const axial = (3 / 10) * mass * radius * radius;
            const inertiaTensor =
                axis === 0
                    ? { x: axial, y: transverse, z: transverse }
                    : axis === 2
                      ? { x: transverse, y: transverse, z: axial }
                      : { x: transverse, y: axial, z: transverse };
            return {
                mass: mass as Mass,
                inverseMass: mass > 0 ? 1 / mass : 0,
                inertiaTensor,
                inverseInertiaTensor: inverseVec3(inertiaTensor),
                center: Vec3.copy(descriptor.def.center),
            };
        }
        case SHAPE_TYPE_CONVEX_HULL: {
            const bounds = computeLocalConvexBounds(descriptor.def.vertices);
            const fullExtents = Vec3.subtract(bounds.max, bounds.min);
            const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
            const inertiaTensor = {
                x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
            };
            return {
                mass: mass as Mass,
                inverseMass: mass > 0 ? 1 / mass : 0,
                inertiaTensor,
                inverseInertiaTensor: inverseVec3(inertiaTensor),
                center: midpointVec3(bounds.min, bounds.max),
            };
        }
        case SHAPE_TYPE_TRIANGLE_MESH: {
            const bounds = computeLocalConvexBounds(descriptor.def.vertices);
            const fullExtents = Vec3.subtract(bounds.max, bounds.min);
            const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
            const inertiaTensor = {
                x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
            };
            return {
                mass: mass as Mass,
                inverseMass: mass > 0 ? 1 / mass : 0,
                inertiaTensor,
                inverseInertiaTensor: inverseVec3(inertiaTensor),
                center: midpointVec3(bounds.min, bounds.max),
            };
        }
        case SHAPE_TYPE_HEIGHTFIELD: {
            const bounds = computeLocalHeightFieldBounds(descriptor.def);
            const fullExtents = Vec3.subtract(bounds.max, bounds.min);
            const mass = fullExtents.x * fullExtents.y * fullExtents.z * safeDensity;
            const inertiaTensor = {
                x: (mass * (fullExtents.y * fullExtents.y + fullExtents.z * fullExtents.z)) / 12,
                y: (mass * (fullExtents.x * fullExtents.x + fullExtents.z * fullExtents.z)) / 12,
                z: (mass * (fullExtents.x * fullExtents.x + fullExtents.y * fullExtents.y)) / 12,
            };
            return {
                mass: mass as Mass,
                inverseMass: mass > 0 ? 1 / mass : 0,
                inertiaTensor,
                inverseInertiaTensor: inverseVec3(inertiaTensor),
                center: midpointVec3(bounds.min, bounds.max),
            };
        }
        default:
            return {
                mass: 0 as Mass,
                inverseMass: 0,
                inertiaTensor: { x: 0, y: 0, z: 0 },
                inverseInertiaTensor: { x: 0, y: 0, z: 0 },
                center: { x: 0, y: 0, z: 0 },
            };
    }
}
