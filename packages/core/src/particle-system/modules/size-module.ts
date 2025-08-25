import { BaseModule } from './base-module';
import { ISizeModule, IParticleSOA } from '../interfaces';
import { Curve } from '../types';

export class SizeModule extends BaseModule implements ISizeModule {
    public size: Curve;
    public separateAxes: boolean;
    public x: Curve;
    public y: Curve;
    public z: Curve;

    constructor(config: Partial<ISizeModule> = {}) {
        super('SizeModule', config.enabled ?? false);

        this.size = config.size ?? {
            mode: 0,
            constant: 1,
            constantMin: 0,
            constantMax: 0,
            curveMultiplier: 1,
        };

        this.separateAxes = config.separateAxes ?? false;

        this.x = config.x ?? { ...this.size };
        this.y = config.y ?? { ...this.size };
        this.z = config.z ?? { ...this.size };
    }

    protected onInitialize(): void {}

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        if (!this.enabled) return;

        const sizes = particles.sizes;
        const ages = particles.ages;
        const lifetimes = particles.lifetimes;
        const activeIndices = particles.getActiveIndices();

        for (const index of activeIndices) {
            const sizeOffset = index * 3;
            const normalizedAge = Math.min(ages[index] / lifetimes[index], 1.0);

            if (this.separateAxes) {
                sizes[sizeOffset] = this.evaluateCurve(this.x, normalizedAge);
                sizes[sizeOffset + 1] = this.evaluateCurve(this.y, normalizedAge);
                sizes[sizeOffset + 2] = this.evaluateCurve(this.z, normalizedAge);
            } else {
                const uniformSize = this.evaluateCurve(this.size, normalizedAge);
                sizes[sizeOffset] = uniformSize;
                sizes[sizeOffset + 1] = uniformSize;
                sizes[sizeOffset + 2] = uniformSize;
            }
        }
    }

    protected onReset(): void {}

    private evaluateCurve(curve: Curve, t: number): number {
        switch (curve.mode) {
            case 0:
                return curve.constant;

            case 3:
                return this.lerp(curve.constantMin, curve.constantMax, t);

            case 1:
                return curve.constant * (1 - t) + curve.constant * curve.curveMultiplier * t;

            default:
                return curve.constant;
        }
    }

    private lerp(a: number, b: number, t: number): number {
        return a + (b - a) * t;
    }

    public setSize(size: number): void {
        this.size.constant = size;
        this.separateAxes = false;
    }

    public setSizeRange(min: number, max: number): void {
        this.size.mode = 3;
        this.size.constantMin = min;
        this.size.constantMax = max;
        this.separateAxes = false;
    }

    public setSizeOverLifetime(startSize: number, endSize: number): void {
        this.size.mode = 1;
        this.size.constant = startSize;
        this.size.curveMultiplier = endSize / startSize;
        this.separateAxes = false;
    }

    public setSeparateAxes(x: number, y: number, z: number): void {
        this.separateAxes = true;
        this.x.constant = x;
        this.y.constant = y;
        this.z.constant = z;
    }
}
