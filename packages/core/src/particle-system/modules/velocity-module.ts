import { Vec3 } from '@axrone/numeric';
import { BaseModule } from './base-module';
import { IVelocityModule, IParticleSOA } from '../interfaces';
import { SimulationSpace } from '../types';

export class VelocityModule extends BaseModule implements IVelocityModule {
    public linear: Vec3;
    public orbital: Vec3;
    public offset: Vec3;
    public radial: any; // Curve
    public speedModifier: any; // Curve
    public space: SimulationSpace;

    constructor(config: Partial<IVelocityModule> = {}) {
        super('VelocityModule', config.enabled ?? false);

        this.linear = config.linear ?? new Vec3(0, 0, 0);
        this.orbital = config.orbital ?? new Vec3(0, 0, 0);
        this.offset = config.offset ?? new Vec3(0, 0, 0);
        this.radial = config.radial ?? {
            mode: 0,
            constant: 0,
            constantMin: 0,
            constantMax: 0,
            curveMultiplier: 1,
        };
        this.speedModifier = config.speedModifier ?? {
            mode: 0,
            constant: 1,
            constantMin: 0,
            constantMax: 0,
            curveMultiplier: 1,
        };
        this.space = config.space ?? SimulationSpace.Local;
    }

    protected onInitialize(): void {
        // Velocity module initialization
    }

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        if (!this.enabled) return;

        const velocities = particles.velocities;
        const positions = particles.positions;
        const activeIndices = particles.getActiveIndices();

        for (const index of activeIndices) {
            const velOffset = index * 3;
            const posOffset = index * 3;

            // apply linear velocity
            velocities[velOffset] += this.linear.x * deltaTime;
            velocities[velOffset + 1] += this.linear.y * deltaTime;
            velocities[velOffset + 2] += this.linear.z * deltaTime;

            // apply orbital velocity (simplified)
            if (this.orbital.lengthSquared() > 0) {
                const px = positions[posOffset];
                const py = positions[posOffset + 1];
                const pz = positions[posOffset + 2];

                // simple orbital rotation around Y-axis
                const orbitalForceX = -py * this.orbital.y * deltaTime;
                const orbitalForceZ = px * this.orbital.y * deltaTime;

                velocities[velOffset] += orbitalForceX;
                velocities[velOffset + 2] += orbitalForceZ;
            }

            // apply offset (constant force)
            velocities[velOffset] += this.offset.x * deltaTime;
            velocities[velOffset + 1] += this.offset.y * deltaTime;
            velocities[velOffset + 2] += this.offset.z * deltaTime;
        }
    }

    protected onReset(): void {}

    public setLinearVelocity(velocity: Vec3): void {
        this.linear = velocity.clone();
    }

    public setOrbitalVelocity(velocity: Vec3): void {
        this.orbital = velocity.clone();
    }

    public setOffset(offset: Vec3): void {
        this.offset = offset.clone();
    }
}
