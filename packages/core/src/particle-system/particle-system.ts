import { Vec3, IVec3Like } from '@axrone/numeric';
import { AABB3D } from '../geometry';
import {
    ParticleId,
    SystemId,
    ParticleEvent,
    ParticleSystemEventMap,
    CollisionEventData,
    DeathEventData,
    SubEmitterEventData,
} from './types';
import { IParticleSystem, IParticleSOA, ISpatialGrid, IParticleSystemModule } from './interfaces';
import { ParticleSOA } from './particle-soa';
import { SpatialGrid } from './spatial-grid';
import { EventEmitter } from '../event';
import {
    EmissionModule,
    ShapeModule,
    VelocityModule,
    ForceModule,
    ColorModule,
    SizeModule,
    RotationModule,
} from './modules';
import { TrailModule, LightsModule, CustomDataModule } from './modules';

export class ParticleSystem implements IParticleSystem {
    private static _nextSystemId: number = 1;

    private readonly _id: SystemId;
    private readonly _particles: IParticleSOA;
    private readonly _spatialGrid: ISpatialGrid;
    private readonly _modules: Map<string, IParticleSystemModule>;
    private readonly _eventEmitter: EventEmitter<ParticleSystemEventMap>;
    private readonly _subSystems: Map<SystemId, ParticleSystem>;
    private _subEmittersConfig: any | null = null;

    private _isPlaying: boolean = false;
    private _isPaused: boolean = false;
    private _time: number = 0;
    private _lastUpdateTime: number = 0;

    private _emissionModule: EmissionModule;
    private _shapeModule: ShapeModule;
    private _velocityModule: VelocityModule;
    private _forceModule: ForceModule;
    private _colorModule: ColorModule;
    private _sizeModule: SizeModule;
    private _rotationModule: RotationModule;
    private _trailModule: TrailModule;
    private _lightsModule: LightsModule;
    private _customDataModule: CustomDataModule;

    constructor(
        options: {
            maxParticles?: number;
            bounds?: AABB3D;
            cellSize?: Vec3;
        } = {}
    ) {
        this._id = ParticleSystem._nextSystemId++ as SystemId;

        const maxParticles = options.maxParticles ?? 1000;
        const bounds =
            options.bounds ?? new AABB3D(new Vec3(-100, -100, -100), new Vec3(100, 100, 100));
        const cellSize = options.cellSize ?? new Vec3(10, 10, 10);

        this._particles = new ParticleSOA(maxParticles);
        this._spatialGrid = new SpatialGrid(
            bounds,
            cellSize.x,
            64,
            Math.max(100, Math.floor(maxParticles / 10))
        );
        this._modules = new Map();
        this._eventEmitter = new EventEmitter<ParticleSystemEventMap>();
        this._subSystems = new Map();

        this._emissionModule = new EmissionModule({ enabled: true });
        this._shapeModule = new ShapeModule({ enabled: false });
        this._velocityModule = new VelocityModule({ enabled: false });
        this._forceModule = new ForceModule({ enabled: false });
        this._colorModule = new ColorModule({ enabled: false });
        this._sizeModule = new SizeModule({ enabled: false });
        this._rotationModule = new RotationModule({ enabled: false });
        this._trailModule = new TrailModule({ enabled: false });
        this._lightsModule = new LightsModule({ enabled: false });
        this._customDataModule = new CustomDataModule({ enabled: false });

        this.addModule(this._emissionModule);
        this.addModule(this._shapeModule);
        this.addModule(this._velocityModule);
        this.addModule(this._forceModule);
        this.addModule(this._colorModule);
        this.addModule(this._sizeModule);
        this.addModule(this._rotationModule);
        this.addModule(this._trailModule);
    }

    get id(): SystemId {
        return this._id;
    }
    get isPlaying(): boolean {
        return this._isPlaying;
    }
    get isPaused(): boolean {
        return this._isPaused;
    }
    get isStopped(): boolean {
        return !this._isPlaying && !this._isPaused;
    }
    get particleCount(): number {
        return this._particles.count;
    }
    get time(): number {
        return this._time;
    }

    get emissionModule(): EmissionModule {
        return this._emissionModule;
    }
    get shapeModule(): ShapeModule {
        return this._shapeModule;
    }
    get velocityModule(): VelocityModule {
        return this._velocityModule;
    }
    get forceModule(): ForceModule {
        return this._forceModule;
    }
    get colorModule(): ColorModule {
        return this._colorModule;
    }
    get sizeModule(): SizeModule {
        return this._sizeModule;
    }
    get rotationModule(): RotationModule {
        return this._rotationModule;
    }

    get trailModule(): TrailModule {
        return this._trailModule;
    }
    get lightsModule(): LightsModule {
        return this._lightsModule;
    }

    get customDataModule(): CustomDataModule {
        return this._customDataModule;
    }

    play(): void {
        this._isPlaying = true;
        this._isPaused = false;
        this._lastUpdateTime = performance.now() / 1000;
    }

    pause(): void {
        this._isPaused = true;
    }

    stop(): void {
        this._isPlaying = false;
        this._isPaused = false;
        this.clear();
        this.reset();
    }

    clear(): void {
        this._particles.clear();
        this._spatialGrid.clear();
        this._time = 0;
    }

    private reset(): void {
        for (const module of this._modules.values()) {
            module.reset();
        }
    }

    emit(count: number): void {
        for (let i = 0; i < count; i++) {
            this.emitSingleParticle();
        }
    }

    emitFromPosition(position: IVec3Like): void {
        const pos = Vec3.from(position);
        const vel = this._shapeModule.enabled
            ? this._shapeModule.getEmissionDirection()
            : new Vec3(0, 1, 0);

        const particleId = this._particles.addParticle(pos, vel, 5.0, 1.0, 0xffffffff);
        if (particleId !== null) {
            this._spatialGrid.insert(particleId, pos);
            this.dispatchEvent({
                type: 'birth',
                particleId,
                position: pos,
                velocity: vel,
            });
            this.triggerSubEmitters('birth', particleId);
        }
    }

    private emitSingleParticle(): void {
        const position = this._shapeModule.enabled
            ? this._shapeModule.getEmissionPosition()
            : new Vec3(0, 0, 0);

        const velocity = this._shapeModule.enabled
            ? this._shapeModule.getEmissionDirection().multiplyScalar(5.0)
            : new Vec3(0, 1, 0);

        const particleId = this._particles.addParticle(position, velocity, 5.0, 1.0, 0xffffffff);
        if (particleId !== null) {
            this._spatialGrid.insert(particleId, position);
            this.dispatchEvent({
                type: 'birth',
                particleId,
                position,
                velocity,
            });
            this.triggerSubEmitters('birth', particleId);
        }
    }

    update(deltaTime?: number): void {
        if (!this._isPlaying || this._isPaused) {
            return;
        }

        const currentTime = performance.now() / 1000;
        const dt = deltaTime ?? currentTime - this._lastUpdateTime;
        this._lastUpdateTime = currentTime;
        this._time += dt;

        for (const module of this._modules.values()) {
            module.update(dt, this._particles);
        }

        this.updateParticlePhysics(dt);
        this.updateSpatialGrid();
        this.removeDeadParticles();
    }

    private updateParticlePhysics(deltaTime: number): void {
        const positions = this._particles.positions;
        const velocities = this._particles.velocities;
        const accelerations = this._particles.accelerations;
        const ages = this._particles.ages;
        const lifetimes = this._particles.lifetimes;

        const activeIndices = this._particles.getActiveIndices();

        for (const index of activeIndices) {
            const posOffset = index * 3;

            ages[index] += deltaTime;

            accelerations[posOffset + 1] -= 9.81 * deltaTime; // Gravity

            velocities[posOffset] += accelerations[posOffset] * deltaTime;
            velocities[posOffset + 1] += accelerations[posOffset + 1] * deltaTime;
            velocities[posOffset + 2] += accelerations[posOffset + 2] * deltaTime;

            positions[posOffset] += velocities[posOffset] * deltaTime;
            positions[posOffset + 1] += velocities[posOffset + 1] * deltaTime;
            positions[posOffset + 2] += velocities[posOffset + 2] * deltaTime;

            accelerations[posOffset] = 0;
            accelerations[posOffset + 1] = 0;
            accelerations[posOffset + 2] = 0;
        }
    }

    private updateSpatialGrid(): void {
        this._spatialGrid.clear();

        const positions = this._particles.positions;
        const ids = this._particles.ids;
        const activeIndices = this._particles.getActiveIndices();

        for (const index of activeIndices) {
            const posOffset = index * 3;
            const position = new Vec3(
                positions[posOffset],
                positions[posOffset + 1],
                positions[posOffset + 2]
            );
            this._spatialGrid.insert(ids[index] as ParticleId, position);
        }
    }

    private removeDeadParticles(): void {
        const ages = this._particles.ages;
        const lifetimes = this._particles.lifetimes;
        const ids = this._particles.ids;
        const activeIndices = this._particles.getActiveIndices();

        for (const index of activeIndices) {
            if (ages[index] >= lifetimes[index]) {
                const particleId = ids[index] as ParticleId;
                const position = this._particles.getParticlePosition(index);

                this._spatialGrid.remove(particleId);
                this._particles.removeParticle(index);

                this.dispatchEvent({
                    type: 'death',
                    particleId,
                    position,
                    velocity: new Vec3(0, 0, 0),
                    lifetime: lifetimes[index],
                    age: ages[index],
                } as any);
                this.triggerSubEmitters('death', particleId);
            }
        }
    }

    public registerSubSystem(id: SystemId, system: ParticleSystem): void {
        this._subSystems.set(id, system);
    }

    public setSubEmittersConfig(config: any): void {
        this._subEmittersConfig = config;
    }

    private triggerSubEmitters(trigger: string, particleId: SystemId | ParticleId): void {
        if (!this._subEmittersConfig || !this._subEmittersConfig.enabled) return;

        const list = (this._subEmittersConfig as any)[trigger] as SystemId[] | undefined;
        if (!list || list.length === 0) return;

        const ids = this._particles.ids;
        let index = -1;
        for (let i = 0; i < ids.length; i++) {
            if (ids[i] === (particleId as number)) {
                index = i;
                break;
            }
        }
        if (index === -1) return;

        for (const subId of list) {
            const subSystem = this._subSystems.get(subId as SystemId);
            if (subSystem && Math.random() <= (this._subEmittersConfig.emitProbability ?? 1)) {
                const particlePos = this._particles.getParticlePosition(index);
                subSystem.emitFromPosition(particlePos);
            }
        }
    }

    addModule(module: IParticleSystemModule): void {
        module.initialize(this);
        this._modules.set(module.name, module);
    }

    removeModule(name: string): boolean {
        return this._modules.delete(name);
    }

    getModule<T extends IParticleSystemModule>(name: string): T | undefined {
        return this._modules.get(name) as T;
    }

    getParticles(): IParticleSOA {
        return this._particles;
    }

    getSpatialGrid(): ISpatialGrid {
        return this._spatialGrid;
    }

    addEventListener(type: string, listener: (event: ParticleEvent) => void): void {
        if (type === 'collision') {
            this._eventEmitter.on('collision', (data) => {
                listener({
                    type: 'collision',
                    particleId: data.particleId,
                    position: data.position,
                    velocity: data.velocity,
                    data: data.data,
                    collider: data.collider,
                    normal: data.normal,
                    surfaceVelocity: data.surfaceVelocity,
                } as any);
            });
        } else if (type === 'death') {
            this._eventEmitter.on('death', (data) => {
                listener({
                    type: 'death',
                    particleId: data.particleId,
                    position: data.position,
                    velocity: data.velocity,
                    data: data.data,
                    lifetime: data.lifetime,
                    age: data.age,
                } as any);
            });
        } else if (type === 'subemitter') {
            this._eventEmitter.on('subemitter', (data) => {
                listener({
                    type: 'subemitter',
                    particleId: data.particleId,
                    position: data.position,
                    velocity: data.velocity,
                    data: data.data,
                    subSystem: data.subSystem,
                    trigger: data.trigger,
                } as any);
            });
        }
    }

    removeEventListener(type: string, listener: (event: ParticleEvent) => void): void {
        if (type === 'collision') {
            this._eventEmitter.removeAllListeners('collision');
        } else if (type === 'death') {
            this._eventEmitter.removeAllListeners('death');
        } else if (type === 'subemitter') {
            this._eventEmitter.removeAllListeners('subemitter');
        }
    }

    onCollision(callback: (event: CollisionEventData) => void): () => void {
        return this._eventEmitter.on('collision', callback);
    }

    onDeath(callback: (event: DeathEventData) => void): () => void {
        return this._eventEmitter.on('death', callback);
    }

    onSubEmitter(callback: (event: SubEmitterEventData) => void): () => void {
        return this._eventEmitter.on('subemitter', callback);
    }

    getEventEmitter(): EventEmitter<ParticleSystemEventMap> {
        return this._eventEmitter;
    }

    private dispatchEvent(event: ParticleEvent): void {
        if (event.type === 'collision') {
            const collisionEvent = event as any;
            this._eventEmitter.emit('collision', {
                particleId: collisionEvent.particleId,
                position: collisionEvent.position,
                velocity: collisionEvent.velocity,
                data: collisionEvent.data,
                collider: collisionEvent.collider,
                normal: collisionEvent.normal,
                surfaceVelocity: collisionEvent.surfaceVelocity,
            });
        } else if (event.type === 'death') {
            const deathEvent = event as any;
            this._eventEmitter.emit('death', {
                particleId: deathEvent.particleId,
                position: deathEvent.position,
                velocity: deathEvent.velocity,
                data: deathEvent.data,
                lifetime: deathEvent.lifetime,
                age: deathEvent.age,
            });
        } else if (event.type === 'subemitter') {
            const subEmitterEvent = event as any;
            this._eventEmitter.emit('subemitter', {
                particleId: subEmitterEvent.particleId,
                position: subEmitterEvent.position,
                velocity: subEmitterEvent.velocity,
                data: subEmitterEvent.data,
                subSystem: subEmitterEvent.subSystem,
                trigger: subEmitterEvent.trigger,
            });
        }
    }

    getParticlesInRadius(center: Vec3, radius: number): ParticleId[] {
        return this._spatialGrid.queryRadius(center, radius);
    }

    getParticlesInBounds(bounds: AABB3D): ParticleId[] {
        return this._spatialGrid.query(bounds);
    }

    public getLightData(maxLights?: number): Float32Array {
        if (!this._lightsModule || !this._lightsModule.enabled) return new Float32Array(0);
        const cfg: any = (this._lightsModule as any).config || {};
        const configuredMax = typeof cfg.maxLights === 'number' ? cfg.maxLights : Infinity;
        const ratio = typeof cfg.ratio === 'number' ? cfg.ratio : 0;
        const desired = Math.min(maxLights ?? configuredMax, configuredMax);

        const active = this._particles.getActiveIndices();
        const candidates: { idx: number; pos: any }[] = [];
        for (const i of active) {
            const p = this._particles.getParticlePosition(i);
            candidates.push({ idx: i, pos: p });
        }

        const keepBase = ratio > 0 ? Math.ceil(candidates.length * ratio) : candidates.length;
        const keepCount = Math.min(desired, keepBase);

        const outCount = Math.min(keepCount, candidates.length);
        const out = new Float32Array(outCount * 4);
        for (let i = 0; i < outCount; i++) {
            const pos = candidates[i].pos;
            const dst = i * 4;
            out[dst] = pos.x;
            out[dst + 1] = pos.y;
            out[dst + 2] = pos.z;
            out[dst + 3] = (cfg.intensity && (cfg.intensity.constant ?? cfg.intensity)) ?? 1.0;
        }

        return out;
    }

    dispose(): void {
        this.stop();
        this._spatialGrid.clear();
        this._modules.clear();
        this._eventEmitter.dispose();
    }
}
