import type { SizeConfiguration } from '../core/configuration';
import type { IParticleBuffer } from '../core/interfaces';
import { BaseModule } from './base-module';

export class SizeModule extends BaseModule<'size'> {
    constructor(configuration: SizeConfiguration) {
        super('size', configuration, 400);
    }

    protected onInitialize(): void {}

    protected onDestroy(): void {}

    protected onReset(): void {}

    protected onUpdate(deltaTime: number): void {}

    protected onProcess(particles: IParticleBuffer, deltaTime: number): void {
        const config = this.config;
        if (!config.enabled) return;

        const sizes = particles.sizes as Float32Array;
        const ages = particles.ages as Float32Array;
        const lifetimes = particles.lifetimes as Float32Array;
        const count = particles.count;

        for (let i = 0; i < count; i++) {
            if (!(particles.alive as Uint32Array)[i]) continue;

            const age = ages[i];
            const lifetime = lifetimes[i];
            const normalizedAge = lifetime > 0 ? age / lifetime : 0;
            const i3 = i * 3;

            if (config.separateAxes) {
                const sizeX = this._evaluateSize(config.sizeX, normalizedAge);
                const sizeY = this._evaluateSize(config.sizeY, normalizedAge);
                const sizeZ = this._evaluateSize(config.sizeZ, normalizedAge);

                sizes[i3] = sizeX;
                sizes[i3 + 1] = sizeY;
                sizes[i3 + 2] = sizeZ;
            } else {
                const size = this._evaluateSize(config.size, normalizedAge);
                sizes[i3] = size;
                sizes[i3 + 1] = size;
                sizes[i3 + 2] = size;
            }
        }
    }

    protected onConfigure(newConfig: SizeConfiguration, oldConfig: SizeConfiguration): void {}

    private _evaluateSize(curve: SizeConfiguration['size'], t: number): number {
        switch (curve.mode) {
            case 0:
                return curve.constant;

            case 2:
                return curve.constantMin + (curve.constantMax - curve.constantMin) * t;

            default:
                return curve.constant;
        }
    }
}
