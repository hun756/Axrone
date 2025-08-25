import { Vec3 } from '@axrone/numeric';
import { BaseModule } from './base-module';
import { IRotationModule, IParticleSOA } from '../interfaces';
import { Curve } from '../types';

export class RotationModule extends BaseModule implements IRotationModule {
    public angularVelocity: Vec3;
    public separateAxes: boolean;
    public x: Curve;
    public y: Curve;
    public z: Curve;

    constructor(config: Partial<IRotationModule> = {}) {
        super('RotationModule', config.enabled ?? false);

        this.angularVelocity = config.angularVelocity ?? new Vec3(0, 0, 0);
        this.separateAxes = config.separateAxes ?? false;

        const defaultCurve = {
            mode: 0,
            constant: 0,
            constantMin: 0,
            constantMax: 0,
            curveMultiplier: 1,
        };

        this.x = config.x ?? { ...defaultCurve };
        this.y = config.y ?? { ...defaultCurve };
        this.z = config.z ?? { ...defaultCurve };
    }

    protected onInitialize(): void {}

    protected onUpdate(deltaTime: number, particles: IParticleSOA): void {
        if (!this.enabled) return;

        const rotations = particles.rotations;
        const angularVelocities = particles.angularVelocities;
        const ages = particles.ages;
        const lifetimes = particles.lifetimes;
        const activeIndices = particles.getActiveIndices();

        for (const index of activeIndices) {
            const rotOffset = index * 3;
            const normalizedAge = Math.min(ages[index] / lifetimes[index], 1.0);

            if (this.separateAxes) {
                angularVelocities[rotOffset] = this.evaluateCurve(this.x, normalizedAge);
                angularVelocities[rotOffset + 1] = this.evaluateCurve(this.y, normalizedAge);
                angularVelocities[rotOffset + 2] = this.evaluateCurve(this.z, normalizedAge);
            } else {
                angularVelocities[rotOffset] = this.angularVelocity.x;
                angularVelocities[rotOffset + 1] = this.angularVelocity.y;
                angularVelocities[rotOffset + 2] = this.angularVelocity.z;
            }

            rotations[rotOffset] += angularVelocities[rotOffset] * deltaTime;
            rotations[rotOffset + 1] += angularVelocities[rotOffset + 1] * deltaTime;
            rotations[rotOffset + 2] += angularVelocities[rotOffset + 2] * deltaTime;

            rotations[rotOffset] = this.normalizeAngle(rotations[rotOffset]);
            rotations[rotOffset + 1] = this.normalizeAngle(rotations[rotOffset + 1]);
            rotations[rotOffset + 2] = this.normalizeAngle(rotations[rotOffset + 2]);
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

    private normalizeAngle(angle: number): number {
        const twoPi = Math.PI * 2;
        return ((angle % twoPi) + twoPi) % twoPi;
    }

    public setAngularVelocity(velocity: Vec3): void {
        this.angularVelocity = velocity.clone();
        this.separateAxes = false;
    }

    public setAngularVelocityRange(min: Vec3, max: Vec3): void {
        this.separateAxes = true;
        this.x.mode = 3;
        this.x.constantMin = min.x;
        this.x.constantMax = max.x;

        this.y.mode = 3;
        this.y.constantMin = min.y;
        this.y.constantMax = max.y;

        this.z.mode = 3;
        this.z.constantMin = min.z;
        this.z.constantMax = max.z;
    }

    public setRotationOverLifetime(startRotation: Vec3, endRotation: Vec3): void {
        this.separateAxes = true;

        this.x.mode = 1;
        this.x.constant = startRotation.x;
        this.x.curveMultiplier = endRotation.x / (startRotation.x || 1);

        this.y.mode = 1;
        this.y.constant = startRotation.y;
        this.y.curveMultiplier = endRotation.y / (startRotation.y || 1);

        this.z.mode = 1;
        this.z.constant = startRotation.z;
        this.z.curveMultiplier = endRotation.z / (startRotation.z || 1);
    }
}
