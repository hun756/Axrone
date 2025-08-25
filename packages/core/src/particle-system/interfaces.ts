import { Vec3, IVec3Like } from '@axrone/numeric';
import { AABB3D } from '../geometry';
import { MemoryPool, PoolableObject } from '@axrone/utility';
import {
    ParticleId,
    SystemId,
    EmitterId,
    EmitterShape,
    SimulationSpace,
    SortMode,
    Curve,
    Gradient,
    Burst,
    ParticleEvent,
} from './types';

export interface IParticleSystemModule {
    readonly name: string;
    readonly enabled: boolean;
    initialize(system: IParticleSystem): void;
    update(deltaTime: number, particles: IParticleSOA): void;
    reset(): void;
}

export interface IParticleSOA {
    readonly capacity: number;
    readonly count: number;
    readonly positions: Float32Array;
    readonly velocities: Float32Array;
    readonly accelerations: Float32Array;
    readonly lifetimes: Float32Array;
    readonly ages: Float32Array;
    readonly sizes: Float32Array;
    readonly colors: Float32Array;
    readonly rotations: Float32Array;
    readonly angularVelocities: Float32Array;
    readonly customData1: Float32Array;
    readonly customData2: Float32Array;
    readonly ids: Uint32Array;

    addParticle(
        position: IVec3Like,
        velocity: IVec3Like,
        lifetime: number,
        size: number,
        color: number
    ): ParticleId | null;
    removeParticle(index: number): void;
    getParticlePosition(index: number): Vec3;
    setParticlePosition(index: number, position: IVec3Like): void;
    getParticleVelocity(index: number): Vec3;
    setParticleVelocity(index: number, velocity: IVec3Like): void;
    getActiveIndices(): number[];
    clear(): void;
    resize(newCapacity: number): void;
}

export interface ISpatialCell extends PoolableObject {
    bounds: AABB3D;
    particles: ParticleId[];
    neighborCells: ISpatialCell[];
}

export interface ISpatialGrid {
    readonly cellSize: Vec3;
    readonly bounds: AABB3D;
    insert(particleId: ParticleId, position: Vec3): void;
    remove(particleId: ParticleId): void;
    update(particleId: ParticleId, oldPosition: Vec3, newPosition: Vec3): void;
    query(bounds: AABB3D): ParticleId[];
    queryRadius(center: Vec3, radius: number): ParticleId[];
    clear(): void;
    getCellAt(position: Vec3): ISpatialCell | null;
    getNeighborCells(cell: ISpatialCell): ISpatialCell[];
}

export interface IParticleSystem {
    readonly id: SystemId;
    readonly isPlaying: boolean;
    readonly isPaused: boolean;
    readonly isStopped: boolean;
    readonly particleCount: number;
    readonly time: number;

    play(): void;
    pause(): void;
    stop(): void;
    clear(): void;
    emit(count: number): void;
    emitFromPosition(position: IVec3Like): void;

    getParticles(): IParticleSOA;
    getSpatialGrid(): ISpatialGrid;
    addEventListener(type: string, listener: (event: ParticleEvent) => void): void;
    removeEventListener(type: string, listener: (event: ParticleEvent) => void): void;
}

export interface IEmissionModule extends IParticleSystemModule {
    rateOverTime: Curve;
    rateOverDistance: Curve;
    burstList: Burst[];
    enabled: boolean;
}

export interface IShapeModule extends IParticleSystemModule {
    enabled: boolean;
    shape: EmitterShape;
    angle: number;
    radius: number;
    donutRadius: number;
    length: number;
    box: Vec3;
    circle: { radius: number; arc: number; arcMode: number; arcSpread: number; thickness: number };
    hemisphere: { radius: number; emitFromShell: boolean };
    cone: {
        angle: number;
        radius: number;
        length: number;
        emitFrom: number;
        randomizeDirection: number;
    };
    donut: { radius: number; donutRadius: number; arc: number; arcMode: number };
    mesh: {
        mesh: any;
        useMeshMaterialIndex: boolean;
        materialIndex: number;
        useMeshColors: boolean;
        normalOffset: number;
    };
    sprite: { sprite: any; normalOffset: number };
    spriteRenderer: { sprite: any; normalOffset: number };
    skinnedMeshRenderer: {
        mesh: any;
        useMeshMaterialIndex: boolean;
        materialIndex: number;
        useMeshColors: boolean;
        normalOffset: number;
    };
    rectangle: { x: number; y: number; z: number };
    edge: { radius: number; radiusMode: number; arc: number; arcMode: number };
    position: Vec3;
    rotation: Vec3;
    scale: Vec3;
    alignToDirection: boolean;
    randomDirectionAmount: number;
    sphericalDirectionAmount: number;
    randomPositionAmount: number;
    biasType: number;
    bias: number;
    texture: any;
    textureClipChannel: number;
    textureClipThreshold: number;
    textureColorAffectsParticles: boolean;
    textureAlphaAffectsParticles: boolean;
    textureBilinearFiltering: boolean;
    textureUVChannel: number;
}

export interface IVelocityModule extends IParticleSystemModule {
    enabled: boolean;
    linear: Vec3;
    orbital: Vec3;
    offset: Vec3;
    radial: Curve;
    speedModifier: Curve;
    space: SimulationSpace;
}

export interface IForceModule extends IParticleSystemModule {
    enabled: boolean;
    force: Vec3;
    relativeTo: SimulationSpace;
    randomizePerFrame: boolean;
}

export interface IColorModule extends IParticleSystemModule {
    enabled: boolean;
    color: Gradient;
}

export interface ISizeModule extends IParticleSystemModule {
    enabled: boolean;
    size: Curve;
    separateAxes: boolean;
    x: Curve;
    y: Curve;
    z: Curve;
}

export interface IRotationModule extends IParticleSystemModule {
    enabled: boolean;
    angularVelocity: Vec3;
    separateAxes: boolean;
    x: Curve;
    y: Curve;
    z: Curve;
}

export interface ICollisionModule extends IParticleSystemModule {
    enabled: boolean;
    type: number;
    mode: number;
    dampen: Curve;
    bounce: Curve;
    lifetimeLoss: Curve;
    minKillSpeed: number;
    maxKillSpeed: number;
    radiusScale: number;
    planes: any[];
    visualization: number;
    visualizeBounds: boolean;
    enableDynamicColliders: boolean;
    maxCollisionShapes: number;
    quality: number;
    voxelSize: number;
    collidesWith: number;
    collidesWithDynamic: boolean;
    interiorCollisions: boolean;
}

export interface INoiseModule extends IParticleSystemModule {
    enabled: boolean;
    separateAxes: boolean;
    strength: Vec3;
    frequency: number;
    scrollSpeed: Vec3;
    damping: boolean;
    octaves: number;
    octaveMultiplier: number;
    octaveScale: number;
    quality: number;
    remap: Vec3;
    remapEnabled: boolean;
    positionAmount: Vec3;
    rotationAmount: Vec3;
    sizeAmount: Vec3;
}

export interface ILimitVelocityModule extends IParticleSystemModule {
    enabled: boolean;
    separateAxes: boolean;
    limit: Vec3;
    limitX: Curve;
    limitY: Curve;
    limitZ: Curve;
    dampen: number;
    space: SimulationSpace;
    drag: Curve;
    multiplyDragByParticleSize: boolean;
    multiplyDragByParticleVelocity: boolean;
}

export interface ITextureSheetModule extends IParticleSystemModule {
    enabled: boolean;
    numTilesX: number;
    numTilesY: number;
    animation: number;
    useRandomRow: boolean;
    frameOverTime: Curve;
    startFrame: Curve;
    cycleCount: number;
    flipU: number;
    flipV: number;
    uvChannelMask: number;
    tiles: Vec3;
    animationType: number;
    rowMode: number;
    sprites: any[];
    speedRange: Vec3;
}
