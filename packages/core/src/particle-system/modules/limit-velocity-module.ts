import { Vec3 } from '@axrone/numeric';
import { BaseModule } from './base-module';
import { ILimitVelocityModule, IParticleSOA } from '../interfaces';

export class LimitVelocityModule extends BaseModule implements ILimitVelocityModule {
    public separateAxes: boolean = false;
    public limit: Vec3 = new Vec3(1, 1, 1);
    public limitX: any = {
        mode: 0,
        constant: 1,
        constantMin: 0,
        constantMax: 0,
        curveMultiplier: 1,
    };
    public limitY: any = {
        mode: 0,
        constant: 1,
        constantMin: 0,
        constantMax: 0,
        curveMultiplier: 1,
    };
    public limitZ: any = {
        mode: 0,
        constant: 1,
        constantMin: 0,
        constantMax: 0,
        curveMultiplier: 1,
    };
    public dampen: number = 0.0;
    public space: number = 0;
    public drag: any = { mode: 0, constant: 0, constantMin: 0, constantMax: 0, curveMultiplier: 1 };
    public multiplyDragByParticleSize: boolean = false;
    public multiplyDragByParticleVelocity: boolean = false;

    constructor(config: Partial<ILimitVelocityModule & { limitCurve?: any[]; multiplyDragBySize?: boolean; multiplyDragByVelocity?: boolean }> = {}) {
        super('LimitVelocityModule', config.enabled ?? false);

        Object.assign(this, config);

        // accept backup aliases
        this.multiplyDragByParticleSize = (config as any).multiplyDragBySize ?? this.multiplyDragByParticleSize;
        this.multiplyDragByParticleVelocity = (config as any).multiplyDragByVelocity ?? this.multiplyDragByParticleVelocity;

        if ((config as any).limitCurve) {
            (this as any).limitCurve = (config as any).limitCurve;
        }
    }

    protected onInitialize(): void {}

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        if (!this.enabled) return;

        const velocities = particles.velocities;
        const sizes = particles.sizes;
        const active = particles.getActiveIndices();

        for (const idx of active) {
            const off = idx * 3;
            let vx = velocities[off];
            let vy = velocities[off + 1];
            let vz = velocities[off + 2];

            if (this.separateAxes) {
                vx = Math.max(-this.limit.x, Math.min(this.limit.x, vx));
                vy = Math.max(-this.limit.y, Math.min(this.limit.y, vy));
                vz = Math.max(-this.limit.z, Math.min(this.limit.z, vz));
            } else {
                const mag = Math.sqrt(vx * vx + vy * vy + vz * vz);
                if (mag > this.limit.x) {
                    const scale = this.limit.x / mag;
                    vx *= scale;
                    vy *= scale;
                    vz *= scale;
                }
            }

            velocities[off] = vx;
            velocities[off + 1] = vy;
            velocities[off + 2] = vz;

            velocities[off] *= 1 - this.dampen;
            velocities[off + 1] *= 1 - this.dampen;
            velocities[off + 2] *= 1 - this.dampen;
        }
    }

    protected onReset(): void {}
}
