import {
    ParticleSystem as CoreParticleSystem,
    EmissionModule,
    ShapeModule,
    VelocityModule,
    ColorModule,
    SizeModule,
    RotationModule,
    NoiseModule,
    CollisionModule,
    LimitVelocityModule,
    TextureSheetModule,
    EmitterShape,
    SimulationSpace,
    CurveMode,
    GradientMode,
} from '@axrone/particle-system';
import type {
    CurveConfiguration,
    GradientConfiguration,
    BurstConfiguration,
    ImmutableColor,
    ImmutableVec3,
    IParticleData,
} from '@axrone/particle-system';
import type { ParticleSystemConfig } from './particle-system';

const DEG_TO_RAD = Math.PI / 180;
const ZERO_VEC3: ImmutableVec3 = Object.freeze({ x: 0, y: 0, z: 0 });
const UNIT_VEC3: ImmutableVec3 = Object.freeze({ x: 1, y: 1, z: 1 });
const ZERO_COLOR: ImmutableColor = Object.freeze({ r: 0, g: 0, b: 0, a: 0 });
const WHITE_COLOR: ImmutableColor = Object.freeze({ r: 1, g: 1, b: 1, a: 1 });

function constantCurve(value: number): CurveConfiguration {
    return { mode: CurveMode.Constant, constant: value, constantMin: 0, constantMax: 0, curveMultiplier: 1 };
}

function constantColor(r: number, g: number, b: number, a: number = 1): GradientConfiguration {
    const color: ImmutableColor = { r, g, b, a };
    return { mode: GradientMode.Color, color, colorMin: ZERO_COLOR, colorMax: WHITE_COLOR };
}

function hexToColor(hex: string): ImmutableColor {
    const r = parseInt(hex.slice(1, 3), 16) / 255;
    const g = parseInt(hex.slice(3, 5), 16) / 255;
    const b = parseInt(hex.slice(5, 7), 16) / 255;
    return { r, g, b, a: 1 };
}

const SHAPE_MAP: Record<string, EmitterShape> = {
    cone: EmitterShape.Cone,
    sphere: EmitterShape.Sphere,
    hemisphere: EmitterShape.Hemisphere,
    box: EmitterShape.Box,
    circle: EmitterShape.Circle,
    edge: EmitterShape.Edge,
    donut: EmitterShape.Donut,
};

export interface SceneParticleRenderBuffers {
    readonly positions: Float32Array;
    readonly renderColors: Float32Array;
    readonly renderSizes: Float32Array;
    readonly renderAlphas: Float32Array;
    readonly seeds: Float32Array;
    readonly rotations: Float32Array;
    readonly maxParticles: number;
}

export function createCoreParticleSystem(config: ParticleSystemConfig): CoreParticleSystem {
    const maxParticles = config.maxParticles ?? 1024;
    const simSpace = config.simulationSpace === 'world' ? SimulationSpace.World : SimulationSpace.Local;

    const core = new CoreParticleSystem({
        maxParticles,
        bounds: { min: { x: -200, y: -200, z: -200 }, max: { x: 200, y: 200, z: 200 } },
        cellSize: { x: 20, y: 20, z: 20 },
        simulationSpace: simSpace,
        enableSpatialOptimization: false,
        enableMultithreading: false,
        preallocateMemory: true,
        autoOptimizeMemory: true,
    });

    const startColor = hexToColor(config.startColor ?? '#FFCF6B');
    const startRgb = [startColor.r, startColor.g, startColor.b];

    core.addModule(new EmissionModule({
        enabled: config.emissionEnabled !== false,
        priority: 100,
        rateOverTime: constantCurve(config.rateOverTime ?? 50),
        rateOverDistance: constantCurve(config.rateOverDistance ?? 0),
        rateMultiplier: 1,
        bursts: buildBurstConfigs(config.bursts),
        prewarm: config.prewarm ?? false,
        prewarmTime: config.startLifetime ?? 2,
        duration: config.duration ?? 5,
        startLifetime: constantCurve(config.startLifetime ?? 2),
        startLifetimeMultiplier: 1,
        startSize: constantCurve(config.startSize ?? 1),
        startSizeMultiplier: 1,
        startColor: constantColor(startRgb[0], startRgb[1], startRgb[2], 1),
    }));

    core.addModule(new ShapeModule({
        enabled: config.shapeEnabled !== false,
        priority: 50,
        shape: SHAPE_MAP[config.shapeType ?? 'cone'] ?? EmitterShape.Cone,
        radius: config.shapeRadius ?? 5,
        radiusThickness: config.shapeRadiusThickness ?? 1,
        angle: (config.shapeAngle ?? 25) * DEG_TO_RAD,
        length: config.shapeLength ?? 10,
        boxSize: { x: 10, y: 10, z: 10 },
        position: ZERO_VEC3,
        rotation: ZERO_VEC3,
        scale: UNIT_VEC3,
        alignToDirection: false,
        randomizeDirection: false,
        spherizeDirection: (config.spherizeDirection ?? 0) > 0,
        randomizePosition: false,
    }));

    core.addModule(new VelocityModule({
        enabled: config.velocityEnabled !== false,
        priority: 200,
        linear: {
            x: config.velocityLinear?.[0] ?? 0,
            y: config.velocityLinear?.[1] ?? 0,
            z: config.velocityLinear?.[2] ?? 0,
        },
        space: simSpace,
        orbital: {
            x: config.velocityOrbital?.[0] ?? 0,
            y: config.velocityOrbital?.[1] ?? 0,
            z: config.velocityOrbital?.[2] ?? 0,
        },
        radial: config.velocityRadial ?? 0,
        speedModifier: constantCurve(config.velocitySpeedModifier ?? 1),
        gravityModifier: config.gravityModifier ?? 0,
        velocityOverLifetime: constantCurve(1),
        inheritVelocity: 0,
        damping: constantCurve(0),
    }));

    core.addModule(new ColorModule({
        enabled: config.colorOverLifetimeEnabled ?? true,
        priority: 300,
        color: constantColor(startRgb[0], startRgb[1], startRgb[2], 1),
        colorOverLifetime: buildColorOverLifetimeGradient(config),
        velocityInfluence: 0,
        ageInfluence: 0,
        sizeInfluence: 0,
        randomColorVariation: 0,
    }));

    core.addModule(new SizeModule({
        enabled: config.sizeOverLifetimeEnabled ?? true,
        priority: 400,
        size: constantCurve(config.startSize ?? 1),
        sizeX: constantCurve(0),
        sizeY: constantCurve(0),
        sizeZ: constantCurve(0),
        separateAxes: false,
        minSize: 0.01,
        maxSize: 100,
        speedInfluence: 0,
        sizeDamping: 0,
        sizeAcceleration: 0,
        randomVariation: 0,
        animationMode: config.sizeOverLifetimeEnabled ? 'overLifetime' : 'constant',
        inheritFromParent: false,
        scaleWithDistance: false,
        distanceScaleFactor: 1,
    }));

    if (config.rotationOverLifetimeEnabled) {
        core.addModule(new RotationModule({
            enabled: true,
            priority: 500,
            angularVelocity: constantCurve(config.angularVelocity ?? 0),
            separateAxes: false,
            angularVelocityX: constantCurve(0),
            angularVelocityY: constantCurve(0),
            angularVelocityZ: constantCurve(0),
            mode: 'overLifetime',
            space: 'local',
            inheritVelocity: false,
            dampingFactor: 0,
            maxAngularVelocity: 3600,
            enablePhysics: false,
            momentOfInertia: 1,
            angularDrag: 0,
        }));
    }

    if (config.limitVelocityEnabled) {
        core.addModule(new LimitVelocityModule({
            enabled: true,
            priority: 300,
            separateAxes: false,
            speed: constantCurve(config.speedLimit ?? 100),
            speedX: constantCurve(0),
            speedY: constantCurve(0),
            speedZ: constantCurve(0),
            dampen: config.limitDampen ?? 3,
            drag: constantCurve(config.drag ?? 0),
            multiplyDragByParticleSize: false,
            multiplyDragByParticleVelocity: false,
        }));
    }

    if (config.noiseEnabled) {
        core.addModule(new NoiseModule({
            enabled: true,
            priority: 600,
            strength: constantCurve(config.noiseStrength?.[0] ?? 1),
            frequency: config.noiseFrequency ?? 1,
            scrollSpeed: constantCurve(0),
            damping: config.noiseDamping ?? false,
            octaves: 1,
            octaveMultiplier: 0.5,
            octaveScale: 2,
            quality: 'medium',
            positionAmount: {
                x: config.noiseStrength?.[0] ?? 1,
                y: config.noiseStrength?.[1] ?? 1,
                z: config.noiseStrength?.[2] ?? 1,
            },
            rotationAmount: ZERO_VEC3,
            sizeAmount: ZERO_VEC3,
            noiseType: 'curl',
            amplitude: 1,
            persistence: 0.5,
            lacunarity: 2,
            seed: 0,
            animationSpeed: 1,
            additive: false,
            remapRange: [0, 1] as const,
            spatialFrequency: { x: 1, y: 1, z: 1 },
            temporalFrequency: 1,
        }));
    }

    if (config.collisionEnabled) {
        core.addModule(new CollisionModule({
            enabled: true,
            priority: 700,
            type: 'planes',
            mode: 'ignore',
            bounce: config.collisionBounce ?? 0.4,
            dampen: config.collisionDampen ?? 0.5,
            lifetimeLoss: config.collisionLifetimeLoss ?? 0,
            minKillSpeed: config.minKillSpeed ?? 0,
            maxKillSpeed: 1000,
            radiusScale: 1,
            enableDynamicColliders: false,
            collisionQuality: 'medium',
            broadPhase: false,
            gridCellSize: 10,
            autoOptimize: false,
            continuousDetection: false,
            maxContacts: 4,
            groundPlane: {
                enabled: true,
                height: 0,
                bounce: config.collisionBounce ?? 0.4,
                friction: config.collisionDampen ?? 0.5,
                dampen: config.collisionDampen ?? 0.5,
            },
            maxChecksPerFrame: 16,
            spatialOptimization: false,
        }));
    }

    if (config.textureEnabled) {
        core.addModule(new TextureSheetModule({
            enabled: true,
            priority: 1100,
            tilesX: config.textureSheetTilesX ?? 1,
            tilesY: config.textureSheetTilesY ?? 1,
            animation: 'wholeSheet',
            timeMode: 'fps',
            fps: config.textureSheetFps ?? 12,
            startFrame: constantCurve(0),
            frameOverTime: constantCurve(0),
            cycleCount: config.textureSheetLoop === false ? 1 : 0,
            flipU: false,
            flipV: false,
            uvChannelMask: 1,
        }));
    }

    return core;
}

function buildBurstConfigs(sceneBursts?: ParticleSystemConfig['bursts']): BurstConfiguration[] {
    if (!sceneBursts || sceneBursts.length === 0) {
        return [];
    }
    return sceneBursts.map(b => ({
        time: b.time,
        count: { value: b.count, variance: 0 },
        cycles: b.cycles ?? 1,
        interval: b.interval ?? 0.01,
        probability: b.probability ?? 1,
    }));
}

function buildColorOverLifetimeGradient(config: ParticleSystemConfig): GradientConfiguration | undefined {
    if (!config.colorOverLifetimeEnabled) {
        return undefined;
    }
    const startColor = hexToColor(config.startColor ?? '#FFCF6B');
    const endColor = hexToColor(config.endColor ?? '#FF3D00');
    return {
        mode: GradientMode.TwoColors,
        color: startColor,
        colorMin: startColor,
        colorMax: endColor,
        gradientKeys: [
            { time: 0, color: startColor, interpolation: 'linear' },
            { time: 1, color: endColor, interpolation: 'linear' },
        ],
    };
}

export function syncCoreRenderData(
    coreData: IParticleData,
    buffers: SceneParticleRenderBuffers,
): number {
    const { positions, colors, sizes, ages, lifetimes, alive, rotations: coreRotations } = coreData;
    const capacity = coreData.capacity;
    const maxParticles = buffers.maxParticles;

    const dstPos = buffers.positions;
    const dstCol = buffers.renderColors;
    const dstSize = buffers.renderSizes;
    const dstAlpha = buffers.renderAlphas;
    const dstSeeds = buffers.seeds;
    const dstRot = buffers.rotations;

    let aliveCount = 0;

    for (let i = 0; i < capacity && aliveCount < maxParticles; i += 1) {
        if (!alive[i]) {
            continue;
        }

        const age = ages[i];
        const lifetime = lifetimes[i];
        if (age >= lifetime) {
            continue;
        }

        const src3 = i * 3;
        const src4 = i * 4;
        const dst3 = aliveCount * 3;

        dstPos[dst3] = positions[src3] ?? 0;
        dstPos[dst3 + 1] = positions[src3 + 1] ?? 0;
        dstPos[dst3 + 2] = positions[src3 + 2] ?? 0;

        dstCol[dst3] = colors[src4] ?? 1;
        dstCol[dst3 + 1] = colors[src4 + 1] ?? 1;
        dstCol[dst3 + 2] = colors[src4 + 2] ?? 1;

        const lifeT = lifetime > 0 ? age / lifetime : 1;
        const fadeIn = Math.min(1, lifeT / 0.08);
        const fadeOut = 1 - Math.max(0, (lifeT - 0.7) / 0.3);
        dstAlpha[aliveCount] = fadeIn * fadeOut * (colors[src4 + 3] ?? 1);

        dstSize[aliveCount] = sizes[src3] ?? 1;

        dstSeeds[aliveCount] = (i * 0.618) % 10;

        if (coreRotations) {
            dstRot[aliveCount] = coreRotations[src3] ?? 0;
        }

        aliveCount += 1;
    }

    for (let j = aliveCount; j < maxParticles; j += 1) {
        dstAlpha[j] = 0;
    }

    return aliveCount;
}
