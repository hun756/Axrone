import { Vec3 } from '@axrone/numeric';
import { BaseModule } from './base-module';
import { IForceModule, IParticleSOA } from '../interfaces';
import { SimulationSpace } from '../types';

export class ForceModule extends BaseModule implements IForceModule {
    public force: Vec3;
    public relativeTo: SimulationSpace;
    public randomizePerFrame: boolean;

    private _randomForce: Vec3 = new Vec3(0, 0, 0);

    constructor(config: Partial<IForceModule> = {}) {
        super('ForceModule', config.enabled ?? false);

        this.force = config.force ?? new Vec3(0, 0, 0);
        this.relativeTo = config.relativeTo ?? SimulationSpace.World;
        this.randomizePerFrame = config.randomizePerFrame ?? false;
    }

    protected onInitialize(): void {
        this.updateRandomForce();
    }

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        if (!this.enabled) return;

        if (this.randomizePerFrame) {
            this.updateRandomForce();
        }

        const accelerations = particles.accelerations;
        const activeIndices = particles.getActiveIndices();

        const effectiveForce = this.randomizePerFrame ? this._randomForce : this.force;

        for (const index of activeIndices) {
            const accelOffset = index * 3;

            accelerations[accelOffset] += effectiveForce.x;
            accelerations[accelOffset + 1] += effectiveForce.y;
            accelerations[accelOffset + 2] += effectiveForce.z;
        }
    }

    protected onReset(): void {
        this.updateRandomForce();
    }

    private updateRandomForce(): void {
        if (this.randomizePerFrame) {
            this._randomForce = new Vec3(
                this.force.x * (Math.random() * 2 - 1),
                this.force.y * (Math.random() * 2 - 1),
                this.force.z * (Math.random() * 2 - 1)
            );
        } else {
            this._randomForce = this.force.clone();
        }
    }

    public setForce(force: Vec3): void {
        this.force = force.clone();
        if (!this.randomizePerFrame) {
            this._randomForce = this.force.clone();
        }
    }

    public addForce(force: Vec3): void {
        this.force = this.force.add(force);
        if (!this.randomizePerFrame) {
            this._randomForce = this.force.clone();
        }
    }
}
