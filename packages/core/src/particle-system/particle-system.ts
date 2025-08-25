import { Vec3, IVec3Like } from '@axrone/numeric';
import { AABB3D } from '../geometry';
import { ParticleId, SystemId, ParticleEvent } from './types';
import { IParticleSystem, IParticleSOA, ISpatialGrid, IParticleSystemModule } from './interfaces';
import { ParticleSOA } from './particle-soa';
import { SpatialGrid } from './spatial-grid';
import {
    EmissionModule,
    ShapeModule,
    VelocityModule,
    ForceModule,
    ColorModule,
    SizeModule,
    RotationModule,
} from './modules';

export class ParticleSystem implements IParticleSystem {
    private static _nextSystemId: number = 1;

    private readonly _id: SystemId;
    private readonly _particles: IParticleSOA;
    private readonly _spatialGrid: ISpatialGrid;
    private readonly _modules: Map<string, IParticleSystemModule>;
    private readonly _eventListeners: Map<string, ((event: ParticleEvent) => void)[]>;

    private _isPlaying: boolean = false;
    private _isPaused: boolean = false;
    private _time: number = 0;
    private _lastUpdateTime: number = 0;

    // Core modules
    private _emissionModule: EmissionModule;
    private _shapeModule: ShapeModule;
    private _velocityModule: VelocityModule;
    private _forceModule: ForceModule;
    private _colorModule: ColorModule;
    private _sizeModule: SizeModule;
    private _rotationModule: RotationModule;

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
        this._spatialGrid = new SpatialGrid(bounds, cellSize);
        this._modules = new Map();
        this._eventListeners = new Map();

        // Initialize core modules
        this._emissionModule = new EmissionModule({ enabled: true });
        this._shapeModule = new ShapeModule({ enabled: false });
        this._velocityModule = new VelocityModule({ enabled: false });
        this._forceModule = new ForceModule({ enabled: false });
        this._colorModule = new ColorModule({ enabled: false });
        this._sizeModule = new SizeModule({ enabled: false });
        this._rotationModule = new RotationModule({ enabled: false });

        this.addModule(this._emissionModule);
        this.addModule(this._shapeModule);
        this.addModule(this._velocityModule);
        this.addModule(this._forceModule);
        this.addModule(this._colorModule);
        this.addModule(this._sizeModule);
        this.addModule(this._rotationModule);
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
        // For simplicity, we'll clear and repopulate the spatial grid
        // In a production system, we'd want to track position changes and update incrementally
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
        if (!this._eventListeners.has(type)) {
            this._eventListeners.set(type, []);
        }
        this._eventListeners.get(type)!.push(listener);
    }

    removeEventListener(type: string, listener: (event: ParticleEvent) => void): void {
        const listeners = this._eventListeners.get(type);
        if (listeners) {
            const index = listeners.indexOf(listener);
            if (index !== -1) {
                listeners.splice(index, 1);
            }
        }
    }

    private dispatchEvent(event: ParticleEvent): void {
        const listeners = this._eventListeners.get(event.type);
        if (listeners) {
            for (const listener of listeners) {
                listener(event);
            }
        }
    }

    getParticlesInRadius(center: Vec3, radius: number): ParticleId[] {
        return this._spatialGrid.queryRadius(center, radius);
    }

    getParticlesInBounds(bounds: AABB3D): ParticleId[] {
        return this._spatialGrid.query(bounds);
    }

    dispose(): void {
        this.stop();
        this._spatialGrid.clear();
        this._modules.clear();
        this._eventListeners.clear();
    }
}
